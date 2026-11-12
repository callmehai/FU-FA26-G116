using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using TrustCabinet.Domain.Audit;
using TrustCabinet.Domain.Entities;
using TrustCabinet.Domain.Enums;
using TrustCabinet.Domain.Transcription;
using TrustCabinet.Domain.Users;
using TrustCabinet.Domain.VoiceId;

namespace TrustCabinet.Web.Tests.Recordings;

/// <summary>
/// ADR-0060, the person's side: one Voice ID consent covers the samples they record and the ones a meeting's
/// Secretary records for them on the room's mic; withdrawing it erases both. The Secretary's samples stay the person's
/// to see, hear and delete.
/// </summary>
public sealed class VoiceIdOneConsentTests(RecordingFixture fixture) : IClassFixture<RecordingFixture>
{
    private const string Msg305Granted = "MSG305";
    private const string Msg306Withdrawn = "MSG306";
    private const string Msg475NoConsent = "MSG475";

    private async Task<(RecordingFixture.Scenario S, HttpClient Secretary)> Arrange()
    {
        var s = await fixture.Seed();
        await VoiceCollection.MakeCollectors(fixture, s.Secretary);
        return (s, VoiceCollection.Client(fixture, s.Secretary, Capability.VoiceCollector));
    }

    private static async Task<Guid> EnrolOwn(HttpClient owner)
    {
        await owner.PostAsJsonAsync("/api/me/voice/consent", new { noticeVersion = VoiceIdRules.NoticeVersion });
        return (await VoiceCollection.AssertCode(await owner.PostAsync("/api/me/voice/samples", VoiceCollection.Sample()), 201, "MSG307"))
            .GetProperty("data").GetProperty("id").GetGuid();
    }

    [Fact]
    public async Task There_is_no_second_consent_to_give()
    {
        var s = await fixture.Seed();
        using var member = fixture.Client(s.Member);
        var proxy = (await member.GetFromJsonAsync<JsonElement>("/api/me/voice")).GetProperty("proxy");
        Assert.False(proxy.GetProperty("allowed").GetBoolean());
        Assert.Equal(0, proxy.GetProperty("samples").GetArrayLength());
        Assert.Equal(VoiceIdRules.MaxSamples, proxy.GetProperty("maxSamples").GetInt32());
        Assert.False(proxy.TryGetProperty("consent", out _));
        Assert.False((await member.PostAsJsonAsync("/api/me/voice/proxy-consent", new { noticeVersion = "1.0" })).IsSuccessStatusCode);
        Assert.False((await member.DeleteAsync("/api/me/voice/proxy-consent")).IsSuccessStatusCode);
    }

    [Fact]
    public async Task One_consent_covers_both_sets()
    {
        var (s, secretary) = await Arrange();
        using var member = fixture.Client(s.Member);
        var granted = await VoiceCollection.AssertCode(await VoiceCollection.Agree(member), 200, Msg305Granted);
        var data = granted.GetProperty("data");
        Assert.Equal(VoiceIdRules.NoticeVersion, data.GetProperty("consent").GetProperty("noticeVersion").GetString());
        Assert.Contains("Thư ký", data.GetProperty("purpose").GetString());
        Assert.True(data.GetProperty("proxy").GetProperty("allowed").GetBoolean());

        var own = await EnrolOwn(member);
        var taken = await VoiceCollection.Collect(fixture, secretary, s.MeetingId, s.Member);
        await using var db = fixture.Db();
        var consent = await db.VoiceConsents.SingleAsync(c => c.UserId == s.Member);
        Assert.Null(consent.WithdrawnAt);
        Assert.Equal(consent.Id, (await db.VoiceSamples.SingleAsync(x => x.Id == own)).ConsentId);
        Assert.Equal(consent.Id, (await db.VoiceSamples.SingleAsync(x => x.Id == taken)).ConsentId);
    }

    [Fact]
    public async Task A_consent_to_an_older_notice_lets_no_secretary_record_until_renewed()
    {
        var (s, secretary) = await Arrange();
        await VoiceCollection.AgreeToOldNotice(fixture, s.Member);
        using var member = fixture.Client(s.Member);
        Assert.False((await member.GetFromJsonAsync<JsonElement>("/api/me/voice")).GetProperty("proxy").GetProperty("allowed").GetBoolean());
        var people = (await secretary.GetFromJsonAsync<JsonElement>(VoiceCollection.Url(s.MeetingId))).GetProperty("people");
        Assert.False(people.EnumerateArray().Single(p => p.GetProperty("userId").GetGuid() == s.Member).GetProperty("consented").GetBoolean());
        await VoiceCollection.AssertCode(await secretary.PostAsync($"{VoiceCollection.Url(s.MeetingId)}/{s.Member}/samples",
            VoiceCollection.Sample()), 409, Msg475NoConsent);

        // Agreeing to the current notice renews the same consent in place, and now covers the Secretary.
        var renewed = await VoiceCollection.AssertCode(await VoiceCollection.Agree(member), 200, Msg305Granted);
        Assert.True(renewed.GetProperty("data").GetProperty("proxy").GetProperty("allowed").GetBoolean());
        await VoiceCollection.Collect(fixture, secretary, s.MeetingId, s.Member);
        await using var db = fixture.Db();
        Assert.Equal(1, await db.VoiceConsents.CountAsync(c => c.UserId == s.Member));
        var audit = await db.AuditLog.SingleAsync(a => a.Action == AuditActions.VoiceConsentRenewed && a.ActorId == s.Member);
        Assert.Contains("\"from\":\"1.2\"", audit.Details.Replace(" ", ""));
    }

    [Fact]
    public async Task Two_grants_at_once_leave_one_standing_consent()
    {
        for (var round = 0; round < 5; round++)
        {
            var s = await fixture.Seed();
            using var first = fixture.Client(s.Member);
            using var second = fixture.Client(s.Member);
            var both = await Task.WhenAll(VoiceCollection.Agree(first), VoiceCollection.Agree(second));
            // A double click: there is no row yet for FOR UPDATE to lock, so both insert and the unique index keeps one;
            // the request that lost the race answers as if it had won — the person has agreed.
            foreach (var response in both) await VoiceCollection.AssertCode(response, 200, Msg305Granted);
            await using var db = fixture.Db();
            Assert.Equal(1, await db.VoiceConsents.CountAsync(c => c.UserId == s.Member && c.WithdrawnAt == null));
        }
    }

    [Fact]
    public async Task Withdrawing_erases_both_sets_at_once()
    {
        var (s, secretary) = await Arrange();
        using var member = fixture.Client(s.Member);
        var own = await EnrolOwn(member);
        var taken = await VoiceCollection.Collect(fixture, secretary, s.MeetingId, s.Member);

        var withdrawn = await VoiceCollection.AssertCode(await member.DeleteAsync("/api/me/voice/consent"), 200, Msg306Withdrawn);
        var data = withdrawn.GetProperty("data");
        Assert.Equal(JsonValueKind.Null, data.GetProperty("consent").ValueKind);
        Assert.Equal(0, data.GetProperty("samples").GetArrayLength());
        Assert.Equal(0, data.GetProperty("proxy").GetProperty("samples").GetArrayLength());
        Assert.False(data.GetProperty("proxy").GetProperty("allowed").GetBoolean());
        await using var db = fixture.Db();
        Assert.False(await db.VoiceSamples.AnyAsync(x => x.Id == own || x.Id == taken));
        var ended = await db.AuditLog.SingleAsync(a => a.Action == AuditActions.VoiceConsentWithdrawn && a.ActorId == s.Member);
        Assert.Contains("\"erasedSamples\":2", ended.Details.Replace(" ", ""));
        // The Secretary can take nothing more, and the withdrawn row stays as evidence.
        await VoiceCollection.AssertCode(await secretary.PostAsync($"{VoiceCollection.Url(s.MeetingId)}/{s.Member}/samples",
            VoiceCollection.Sample()), 409, Msg475NoConsent);
        Assert.NotNull((await db.VoiceConsents.SingleAsync(c => c.UserId == s.Member)).WithdrawnAt);
    }

    [Fact]
    public async Task The_own_limit_counts_only_the_samples_the_person_recorded()
    {
        var (s, secretary) = await Arrange();
        await VoiceCollection.Agree(fixture, s.Member);
        for (var i = 0; i < VoiceIdRules.MaxSamples; i++) await VoiceCollection.Collect(fixture, secretary, s.MeetingId, s.Member);
        using var member = fixture.Client(s.Member);
        await EnrolOwn(member);
        for (var i = 1; i < VoiceIdRules.MaxSamples; i++)
            await VoiceCollection.AssertCode(await member.PostAsync("/api/me/voice/samples", VoiceCollection.Sample()), 201, "MSG307");
        await VoiceCollection.AssertCode(await member.PostAsync("/api/me/voice/samples", VoiceCollection.Sample()), 409, "MSG310");

        var profile = await member.GetFromJsonAsync<JsonElement>("/api/me/voice");
        Assert.Equal(VoiceIdRules.MaxSamples, profile.GetProperty("samples").GetArrayLength());
        Assert.Equal(VoiceIdRules.MaxSamples, profile.GetProperty("proxy").GetProperty("samples").GetArrayLength());
    }

    [Fact]
    public async Task The_person_sees_hears_and_deletes_a_sample_a_secretary_took_and_nobody_else_does()
    {
        var (s, secretary) = await Arrange();
        await VoiceCollection.Agree(fixture, s.Member);
        var taken = await VoiceCollection.Collect(fixture, secretary, s.MeetingId, s.Member);
        using var member = fixture.Client(s.Member);

        var profile = await member.GetFromJsonAsync<JsonElement>("/api/me/voice");
        Assert.Equal(0, profile.GetProperty("samples").GetArrayLength());
        var listed = Assert.Single(profile.GetProperty("proxy").GetProperty("samples").EnumerateArray());
        Assert.Equal(taken, listed.GetProperty("id").GetGuid());
        Assert.Equal(s.MeetingId, listed.GetProperty("meetingId").GetGuid());
        Assert.Equal("Recording test", listed.GetProperty("meetingTitle").GetString());
        Assert.Equal("Recording test 0", listed.GetProperty("collectedByName").GetString());
        Assert.True(listed.GetProperty("playable").GetBoolean());

        var heard = await member.GetAsync($"/api/me/voice/samples/{taken}/audio");
        Assert.Equal(HttpStatusCode.OK, heard.StatusCode);
        Assert.Equal(Encoding.ASCII.GetBytes(VoiceCollection.Recorded), await heard.Content.ReadAsByteArrayAsync());
        // The person's own listening is not audited; only someone else's is.
        await using var db = fixture.Db();
        Assert.False(await db.AuditLog.AnyAsync(a => a.Action == AuditActions.VoiceSamplePlayed && a.ObjectId == taken));
        // The Secretary reaches it only through their meeting, never through the owner's routes.
        Assert.Equal(HttpStatusCode.NotFound, (await secretary.GetAsync($"/api/me/voice/samples/{taken}/audio")).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await secretary.DeleteAsync($"/api/me/voice/samples/{taken}")).StatusCode);
        using var chair = fixture.Client(s.Chair);
        Assert.Equal(HttpStatusCode.NotFound, (await chair.GetAsync($"/api/me/voice/samples/{taken}/audio")).StatusCode);

        var deleted = await VoiceCollection.AssertCode(await member.DeleteAsync($"/api/me/voice/samples/{taken}"), 200, "MSG311");
        Assert.Equal(0, deleted.GetProperty("data").GetProperty("proxy").GetProperty("samples").GetArrayLength());
        Assert.False(await db.VoiceSamples.AnyAsync(x => x.Id == taken));
        var audit = await db.AuditLog.SingleAsync(a => a.Action == AuditActions.VoiceSampleDeleted && a.ObjectId == taken);
        Assert.Equal(s.Member, audit.ActorId);
        Assert.Contains("\"scope\":\"proxy\"", audit.Details.Replace(" ", ""));
        Assert.Contains(s.Secretary.ToString(), audit.Details);
        // The consent stands: the Secretary can take another one.
        await VoiceCollection.Collect(fixture, secretary, s.MeetingId, s.Member);
    }

    [Fact]
    public async Task Deactivating_an_account_ends_the_consent_and_erases_both_sets()
    {
        var (s, secretary) = await Arrange();
        using (var member = fixture.Client(s.Member)) await EnrolOwn(member);
        await VoiceCollection.Collect(fixture, secretary, s.MeetingId, s.Member);

        using (var scope = fixture.Host.Services.CreateScope())
            Assert.True((await scope.ServiceProvider.GetRequiredService<IUserService>().ChangeStatusAsync(s.Member, false, s.Organizer)).Succeeded);

        await using var db = fixture.Db();
        Assert.False(await db.VoiceSamples.AnyAsync(x => x.UserId == s.Member));
        Assert.NotNull((await db.VoiceConsents.SingleAsync(c => c.UserId == s.Member)).WithdrawnAt);
        var ended = await db.AuditLog.SingleAsync(a => a.Action == AuditActions.VoiceConsentWithdrawn && a.ActorId == s.Organizer);
        Assert.Contains("\"reason\":\"accountDeactivated\"", ended.Details.Replace(" ", ""));
        Assert.Contains("\"erasedSamples\":2", ended.Details.Replace(" ", ""));
    }

    [Fact]
    public async Task A_sample_a_secretary_took_names_the_person_in_the_transcript()
    {
        var (s, secretary) = await Arrange();
        await VoiceCollection.Agree(fixture, s.Member);
        await VoiceCollection.Collect(fixture, secretary, s.MeetingId, s.Member, 0f, 1f, 0f);
        while (true)
        {
            using var scope = fixture.Host.Services.CreateScope();
            if (!await scope.ServiceProvider.GetRequiredService<ITranscriptionService>().ProcessNextAsync()) break;
        }
        var form = new MultipartFormDataContent { { new StringContent("true"), "consentConfirmed" } };
        var file = new ByteArrayContent([.. "RIFF"u8, .. new byte[256]]);
        file.Headers.ContentType = MediaTypeHeaderValue.Parse("audio/wav");
        form.Add(file, "file", "hop.wav");
        using var uploader = fixture.Client(s.Secretary);
        var id = (await (await uploader.PostAsync(s.Url, form)).Content.ReadFromJsonAsync<JsonElement>()).GetProperty("recordingId").GetGuid();
        fixture.Speech.Failure = null;
        fixture.Speech.Result = new(30000, "vi", "gipformer-1.5-68m-int8@abc123def456", "eres2net-base-200k@0123456789ab",
            [new(0, 9000, "SPEAKER_00", "Tôi xin phát biểu.")], [new SpeechSpeaker("SPEAKER_00", 9000, [0f, 1f, 0.1f])]);
        while (true)
        {
            using var scope = fixture.Host.Services.CreateScope();
            if (!await scope.ServiceProvider.GetRequiredService<ITranscriptionService>().ProcessNextAsync()) break;
        }

        await using var db = fixture.Db();
        var voice = await db.TranscriptSpeakers.SingleAsync(x => x.Transcript.RecordingId == id && x.Label == "SPEAKER_00");
        Assert.Equal(s.Member, voice.SuggestedUserId);
        Assert.Equal(s.Member, voice.AssignedUserId);
        Assert.Null(voice.AssignedById);
    }
}
