using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using TrustCabinet.Domain.Audit;
using TrustCabinet.Domain.Entities;
using TrustCabinet.Domain.Enums;
using TrustCabinet.Domain.Meetings;
using TrustCabinet.Domain.Recordings;
using TrustCabinet.Domain.Transcription;
using TrustCabinet.Infrastructure.GuestAccess;

namespace TrustCabinet.Web.Tests.Recordings;

/// <summary>
/// SCRUM-129 — a guest's own mic in the meeting's per-mic record (ADR-0051): sent through the guest's session only
/// after the call room's recording notice, named after the guest, read back by the guest only for their own sentences.
/// </summary>
public sealed class GuestSpeechClipTests(RecordingFixture fixture) : IClassFixture<RecordingFixture>
{
    private static readonly byte[] Wav = [.. "RIFF"u8, .. new byte[2048]];

    private static readonly SpeechTranscription OneSentence = new(2400, "vi", "gipformer-1.5-68m-int8@abc123def456", "",
        [new(0, 1200, null, "Tôi xin góp ý về dự thảo.", -24.5)], []);

    private sealed record Guest(Guid InvitationId, Guid SessionId, string Token);

    /// <summary>
    /// An invited guest who verified (UC-04) and gave their name, as GuestAccessService leaves them: an invitation and a
    /// current session. The token is the one GuestCredentials hands out, on the real clock the bearer handler checks.
    /// </summary>
    private async Task<Guest> InviteGuest(RecordingFixture.Scenario s, string? displayName = "Khách tự khai")
    {
        await using var db = fixture.Db();
        var invitation = new MeetingGuestInvitation
        {
            MeetingId = s.MeetingId, InvitedName = "Khách được mời", Email = $"{Guid.NewGuid():N}@guests.test",
            InvitedById = s.Secretary, SelfDeclaredDisplayName = displayName,
        };
        var session = new GuestSession { InvitationId = invitation.Id, ExpiresAt = fixture.Clock.Now.AddHours(12) };
        db.MeetingGuestInvitations.Add(invitation);
        db.GuestSessions.Add(session);
        await db.SaveChangesAsync();
        var token = fixture.Host.Services.GetRequiredService<GuestCredentials>()
            .AccessToken(new MeetingGuestSession(s.MeetingId, invitation.Id, session.Id), DateTimeOffset.UtcNow.AddHours(1));
        return new(invitation.Id, session.Id, token);
    }

    private HttpClient GuestClient(Guest guest)
    {
        var client = fixture.Host.CreateClient();
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", guest.Token);
        return client;
    }

    /// <summary>The call room's pre-join notice, confirmed: what the guest's mic needs before anything is recorded.</summary>
    private static async Task Join(HttpClient guest, Guid meetingId)
        => await Expect(await guest.PostAsJsonAsync($"/api/meetings/{meetingId}/call/join",
            new { recordingNoticeAcknowledged = true }), 200);

    private async Task<(Guest Guest, HttpClient Client)> JoinedGuest(RecordingFixture.Scenario s)
    {
        var guest = await InviteGuest(s);
        var client = GuestClient(guest);
        await Join(client, s.MeetingId);
        return (guest, client);
    }

    private Task<HttpResponseMessage> Send(HttpClient client, Guid meetingId, Guid? clipId = null)
    {
        fixture.Speech.Failure = null;
        fixture.Speech.Result = OneSentence;
        var content = new ByteArrayContent(Wav);
        content.Headers.ContentType = MediaTypeHeaderValue.Parse("audio/wav");
        var started = Uri.EscapeDataString(fixture.Clock.Now.AddSeconds(-3).ToString("O"));
        return client.PutAsync($"/api/meetings/{meetingId}/speech-clips/{clipId ?? Guid.NewGuid()}?startedAt={started}&durationMs=2400",
            content);
    }

    private async Task<Guid> Said(HttpClient client, Guid meetingId)
        => (await Expect(await Send(client, meetingId), 200, "MSG318")).GetProperty("clip").GetProperty("id").GetGuid();

    private static Task<JsonElement> List(HttpClient client, Guid meetingId)
        => client.GetFromJsonAsync<JsonElement>($"/api/meetings/{meetingId}/speech-clips");

    private static async Task<JsonElement> Expect(HttpResponseMessage response, int status, string? code = null)
    {
        var raw = await response.Content.ReadAsStringAsync();
        Assert.True((int)response.StatusCode == status, $"Expected {status}/{code}, got {(int)response.StatusCode}: {raw}");
        if (code is null) return raw.Length == 0 ? default : JsonDocument.Parse(raw).RootElement.Clone();
        var json = JsonDocument.Parse(raw).RootElement.Clone();
        Assert.Equal(code, json.GetProperty("code").GetString());
        return json;
    }

    [Fact]
    public async Task A_guest_is_recorded_only_after_the_notice_and_reads_in_the_transcript_under_their_name_as_a_guest()
    {
        var s = await fixture.Seed(mode: MeetingMode.Online);
        var guest = await InviteGuest(s);
        using var client = GuestClient(guest);
        var calls = fixture.Speech.Calls;

        // Not joined yet, so never told the meeting is recorded: refused before the worker hears anything.
        await Expect(await Send(client, s.MeetingId), 400, "MSG380");
        Assert.Equal(calls, fixture.Speech.Calls);
        Assert.False((await List(client, s.MeetingId)).GetProperty("canCapture").GetBoolean());
        await using (var db = fixture.Db())
            Assert.False(await db.SpeechClips.AnyAsync(c => c.MeetingId == s.MeetingId));

        await Join(client, s.MeetingId);
        Assert.True((await List(client, s.MeetingId)).GetProperty("canCapture").GetBoolean());
        var clientId = Guid.NewGuid();
        var clip = (await Expect(await Send(client, s.MeetingId, clientId), 200, "MSG318")).GetProperty("clip");
        Assert.Equal(guest.InvitationId, clip.GetProperty("speakerId").GetGuid());
        Assert.Equal("Khách tự khai", clip.GetProperty("speakerName").GetString());
        Assert.True(clip.GetProperty("speakerIsGuest").GetBoolean());
        Assert.True(clip.GetProperty("canDelete").GetBoolean());
        Assert.False(fixture.Speech.LastDiarize);

        // A retry after a lost response: one line, the worker asked once.
        var again = (await Expect(await Send(client, s.MeetingId, clientId), 200, "MSG318")).GetProperty("clip");
        Assert.Equal(clip.GetProperty("id").GetGuid(), again.GetProperty("id").GetGuid());
        Assert.Equal(calls + 1, fixture.Speech.Calls);

        await using (var db = fixture.Db())
        {
            var row = await db.SpeechClips.SingleAsync(c => c.MeetingId == s.MeetingId);
            Assert.Equal(((Guid?)null, (Guid?)guest.InvitationId), (row.SpeakerId, row.GuestInvitationId));
        }

        // The members read it in the meeting's transcript; only the Secretary may delete it.
        using var chair = fixture.Client(s.Chair);
        var line = Assert.Single((await List(chair, s.MeetingId)).GetProperty("items").EnumerateArray());
        Assert.Equal(("Khách tự khai", true, false), (line.GetProperty("speakerName").GetString(),
            line.GetProperty("speakerIsGuest").GetBoolean(), line.GetProperty("canDelete").GetBoolean()));
        using var secretary = fixture.Client(s.Secretary);
        Assert.True(Assert.Single((await List(secretary, s.MeetingId)).GetProperty("items").EnumerateArray())
            .GetProperty("canDelete").GetBoolean());
        var text = await (await chair.GetAsync($"/api/meetings/{s.MeetingId}/speech-clips.txt")).Content.ReadAsStringAsync();
        Assert.Contains("Khách tự khai (Khách)", text);

        // Without a name of their own, the guest is named as they were invited, as the transcripts already do.
        await using (var db = fixture.Db())
            await db.MeetingGuestInvitations.Where(g => g.Id == guest.InvitationId)
                .ExecuteUpdateAsync(u => u.SetProperty(g => g.SelfDeclaredDisplayName, (string?)null));
        Assert.Equal("Khách được mời", Assert.Single((await List(chair, s.MeetingId)).GetProperty("items").EnumerateArray())
            .GetProperty("speakerName").GetString());
    }

    [Fact]
    public async Task A_guest_reads_hears_and_deletes_only_their_own_sentences_and_never_the_transcript()
    {
        var s = await fixture.Seed(mode: MeetingMode.Online);
        var (guest, client) = await JoinedGuest(s);
        var (other, otherClient) = await JoinedGuest(s);
        using var member = fixture.Client(s.Member);
        var members = await Said(member, s.MeetingId);
        var mine = await Said(client, s.MeetingId);
        var theirs = await Said(otherClient, s.MeetingId);

        var list = await List(client, s.MeetingId);
        var only = Assert.Single(list.GetProperty("items").EnumerateArray());
        Assert.Equal((mine, guest.InvitationId), (only.GetProperty("id").GetGuid(), only.GetProperty("speakerId").GetGuid()));
        Assert.Equal(JsonValueKind.Null, only.GetProperty("leak").ValueKind);
        Assert.False(list.GetProperty("canEdit").GetBoolean());
        Assert.False(list.GetProperty("canExclude").GetBoolean());
        Assert.False(list.GetProperty("canExport").GetBoolean());
        Assert.Equal(180, list.GetProperty("retentionDays").GetInt32());

        // Someone else's sentence does not exist for a guest.
        foreach (var id in new[] { members, theirs })
        {
            Assert.Equal(HttpStatusCode.NotFound, (await client.GetAsync($"/api/speech-clips/{id}/playback")).StatusCode);
            Assert.Equal(HttpStatusCode.NotFound, (await client.DeleteAsync($"/api/speech-clips/{id}")).StatusCode);
        }
        // The members' own tools — the transcript as text, correcting, excluding, the recordings — stay closed to a guest.
        foreach (var response in new[]
                 {
                     await client.GetAsync($"/api/meetings/{s.MeetingId}/speech-clips.txt"),
                     await client.PutAsJsonAsync($"/api/speech-clips/{mine}/text", new { text = "Sửa" }),
                     await client.PutAsJsonAsync($"/api/speech-clips/{mine}/exclusion", new { excluded = true, reason = "Other" }),
                     await client.GetAsync($"/api/meetings/{s.MeetingId}/recordings"),
                 })
            Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);

        Assert.StartsWith($"/api/speech-clips/{mine}/audio?",
            (await Expect(await client.GetAsync($"/api/speech-clips/{mine}/playback"), 200)).GetProperty("url").GetString());
        await Expect(await client.DeleteAsync($"/api/speech-clips/{mine}"), 200, "MSG322");
        Assert.Empty((await List(client, s.MeetingId)).GetProperty("items").EnumerateArray());
        await using var db = fixture.Db();
        Assert.Equal(2, await db.SpeechClips.CountAsync(c => c.MeetingId == s.MeetingId));
        var deleted = await db.AuditLog.SingleAsync(a => a.ObjectId == mine && a.Action == AuditActions.SpeechClipDeleted);
        Assert.Null(deleted.ActorId);
        Assert.Equal(other.InvitationId, (await db.SpeechClips.SingleAsync(c => c.Id == theirs)).GuestInvitationId);
    }

    [Fact]
    public async Task A_guest_of_another_meeting_or_before_the_meeting_runs_is_refused()
    {
        var running = await fixture.Seed(mode: MeetingMode.Online);
        var (_, client) = await JoinedGuest(running);
        var elsewhere = await fixture.Seed(mode: MeetingMode.Online);

        // The token names one meeting: every other one is not there.
        await Expect(await Send(client, elsewhere.MeetingId), 404);
        Assert.Equal(HttpStatusCode.NotFound, (await client.GetAsync($"/api/meetings/{elsewhere.MeetingId}/speech-clips")).StatusCode);

        // Invited to a meeting that has not started: the room is not open, the mic records nothing.
        var waiting = await fixture.Seed(MeetingStatus.Published, MeetingMode.Online);
        using var early = GuestClient(await InviteGuest(waiting));
        await Expect(await Send(early, waiting.MeetingId), 409, "MSG320");
        Assert.False((await List(early, waiting.MeetingId)).GetProperty("canCapture").GetBoolean());

        // A meeting held in person has no call room and no per-mic record.
        var inPerson = await fixture.Seed(mode: MeetingMode.InPerson);
        using var there = GuestClient(await InviteGuest(inPerson));
        await Expect(await Send(there, inPerson.MeetingId), 409, "MSG320");

        // A guest who never gave their name (UC-04 step 5) has no session the meeting accepts.
        using var unnamed = GuestClient(await InviteGuest(running, displayName: null));
        await Expect(await Send(unnamed, running.MeetingId), 404);

        await using var db = fixture.Db();
        Assert.False(await db.SpeechClips.AnyAsync(c => c.MeetingId == elsewhere.MeetingId || c.MeetingId == waiting.MeetingId
            || c.MeetingId == inPerson.MeetingId));
    }

    [Fact]
    public async Task Revoking_the_invitation_stops_the_mic_keeps_what_was_said_and_the_Secretary_deletes_it_on_request()
    {
        var s = await fixture.Seed(mode: MeetingMode.Online);
        var (guest, client) = await JoinedGuest(s);
        var said = await Said(client, s.MeetingId);

        await using (var db = fixture.Db())
            await db.MeetingGuestInvitations.Where(g => g.Id == guest.InvitationId).ExecuteUpdateAsync(u => u
                .SetProperty(g => g.RevokedAt, fixture.Clock.Now).SetProperty(g => g.RevokedById, (Guid?)s.Secretary));

        await Expect(await Send(client, s.MeetingId), 404);
        Assert.Equal(HttpStatusCode.NotFound, (await client.GetAsync($"/api/meetings/{s.MeetingId}/speech-clips")).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await client.DeleteAsync($"/api/speech-clips/{said}")).StatusCode);

        // Spoken before the revocation, so still in the record.
        using var secretary = fixture.Client(s.Secretary);
        var line = Assert.Single((await List(secretary, s.MeetingId)).GetProperty("items").EnumerateArray());
        Assert.Equal((said, true), (line.GetProperty("id").GetGuid(), line.GetProperty("speakerIsGuest").GetBoolean()));

        // The guest asks the Secretary to take it out: deleted, words and audio.
        using var member = fixture.Client(s.Member);
        await Expect(await member.DeleteAsync($"/api/speech-clips/{said}"), 403, "MSG323");
        await Expect(await secretary.DeleteAsync($"/api/speech-clips/{said}"), 200, "MSG322");
        await using var check = fixture.Db();
        Assert.False(await check.SpeechClips.AnyAsync(c => c.Id == said));
    }

    [Fact]
    public async Task A_signed_out_session_or_an_ended_meeting_stops_the_guest_mic()
    {
        var s = await fixture.Seed(mode: MeetingMode.Online);
        var (guest, client) = await JoinedGuest(s);
        await Said(client, s.MeetingId);

        await using (var db = fixture.Db())
            await db.GuestSessions.Where(x => x.Id == guest.SessionId)
                .ExecuteUpdateAsync(u => u.SetProperty(x => x.RevokedAt, fixture.Clock.Now));
        await Expect(await Send(client, s.MeetingId), 404);

        var ended = await fixture.Seed(mode: MeetingMode.Online);
        var (_, late) = await JoinedGuest(ended);
        await using (var db = fixture.Db())
            await db.Meetings.Where(m => m.Id == ended.MeetingId).ExecuteUpdateAsync(u => u
                .SetProperty(m => m.Status, MeetingStatus.Ended).SetProperty(m => m.ActualEndedAt, fixture.Clock.Now));
        // A member's browser may still send what it kept for 7 days; a guest's session ended with the meeting.
        await Expect(await Send(late, ended.MeetingId), 404);
    }

    [Fact]
    public async Task A_guests_sentences_go_when_the_recordings_retention_runs_out()
    {
        var s = await fixture.Seed(mode: MeetingMode.Online);
        var (_, client) = await JoinedGuest(s);
        var said = await Said(client, s.MeetingId);
        await using var db = fixture.Db();
        var file = Path.Combine(fixture.StorageRoot, (await db.SpeechClips.SingleAsync(c => c.Id == said)).AudioPath!);
        Assert.True(File.Exists(file));

        await db.SpeechClips.Where(c => c.Id == said).ExecuteUpdateAsync(u => u.SetProperty(c => c.CreatedAt,
            fixture.Clock.Now.AddDays(-181)));
        using (var scope = fixture.Host.Services.CreateScope())
            Assert.True(await scope.ServiceProvider.GetRequiredService<ISpeechClipService>().EraseExpiredAsync() >= 1);
        Assert.False(await db.SpeechClips.AnyAsync(c => c.Id == said));
        Assert.False(File.Exists(file));
    }

    [Fact]
    public async Task A_sentence_belongs_to_exactly_one_member_or_guest()
    {
        var s = await fixture.Seed(mode: MeetingMode.Online);
        var guest = await InviteGuest(s);
        foreach (var (speaker, invitation) in new (Guid?, Guid?)[] { (null, null), (s.Member, guest.InvitationId) })
        {
            await using var db = fixture.Db();
            db.SpeechClips.Add(new SpeechClip { MeetingId = s.MeetingId, SpeakerId = speaker, GuestInvitationId = invitation,
                ClientClipId = Guid.NewGuid(), StartedAt = fixture.Clock.Now, DurationMs = 1000, Text = "x" });
            var error = await Assert.ThrowsAsync<DbUpdateException>(() => db.SaveChangesAsync());
            Assert.Contains("ck_speech_clips_one_speaker", error.InnerException!.Message);
        }
    }
}
