using System.Buffers.Binary;
using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using TrustCabinet.Application.Recordings;
using TrustCabinet.Application.Transcription;
using TrustCabinet.Domain.Entities;
using TrustCabinet.Domain.Enums;
using TrustCabinet.Domain.Meetings;
using TrustCabinet.Infrastructure.GuestAccess;

namespace TrustCabinet.Web.Tests.Recordings;

/// <summary>
/// Round 1 gap tests for FE-09 Recording (Report 5.2.2): the <c>Manual only</c> cases of
/// <c>cases/FE-09/api.json</c>. Storage is the fixture's temp directory, the speech worker is the
/// fixture's fake; no LiveKit or outside service is involved.
/// </summary>
public sealed class RecordingRound1Tests(RecordingFixture fixture) : IClassFixture<RecordingFixture>
{
    private const int Part = RecordingFixture.RoomPartBytes;

    private static readonly byte[] Audio = [.. "RIFF"u8, .. new byte[2048]];

    private static readonly byte[] Wav = [.. "RIFF"u8, .. new byte[2048]];

    private static readonly SpeechTranscription OneSentence = new(2400, "vi", "gipformer-1.5-68m-int8@abc123def456", "",
        [new(0, 1200, null, "Tôi đồng ý với dự thảo.", -24.5), new(1300, 2400, null, "Đề nghị biểu quyết.", -25.0)], []);

    /// <summary>What the browser writes first: 16 kHz mono 16-bit PCM whose length is not known yet.</summary>
    private static byte[] Header()
    {
        var h = new byte[44];
        "RIFF"u8.CopyTo(h);
        BinaryPrimitives.WriteUInt32LittleEndian(h.AsSpan(4), uint.MaxValue);
        "WAVEfmt "u8.CopyTo(h.AsSpan(8));
        BinaryPrimitives.WriteUInt32LittleEndian(h.AsSpan(16), 16);
        BinaryPrimitives.WriteUInt16LittleEndian(h.AsSpan(20), 1);
        BinaryPrimitives.WriteUInt16LittleEndian(h.AsSpan(22), 1);
        BinaryPrimitives.WriteUInt32LittleEndian(h.AsSpan(24), 16_000);
        BinaryPrimitives.WriteUInt32LittleEndian(h.AsSpan(28), 32_000);
        BinaryPrimitives.WriteUInt16LittleEndian(h.AsSpan(32), 2);
        BinaryPrimitives.WriteUInt16LittleEndian(h.AsSpan(34), 16);
        "data"u8.CopyTo(h.AsSpan(36));
        BinaryPrimitives.WriteUInt32LittleEndian(h.AsSpan(40), uint.MaxValue - 36);
        return h;
    }

    private static byte[] Tape(int bytes, byte fill)
    {
        var tape = Enumerable.Repeat(fill, bytes).ToArray();
        Header().CopyTo(tape, 0);
        return tape;
    }

    private static MultipartFormDataContent Form(byte[]? audio = null, string type = "audio/wav", bool consent = true,
        string? duration = "65000", string fileName = "phien-hop.wav")
    {
        var form = new MultipartFormDataContent();
        if (consent) form.Add(new StringContent("true"), "consentConfirmed");
        form.Add(new StringContent("Lập biên bản phiên họp"), "consentPurpose");
        if (duration is not null) form.Add(new StringContent(duration), "durationMs");
        var file = new ByteArrayContent(audio ?? Audio);
        file.Headers.ContentType = MediaTypeHeaderValue.Parse(type);
        form.Add(file, "file", fileName);
        return form;
    }

    private static async Task<JsonElement> AssertCode(HttpResponseMessage response, int status, string code)
    {
        var raw = await response.Content.ReadAsStringAsync();
        Assert.True((int)response.StatusCode == status, $"Expected {status}, got {(int)response.StatusCode}: {raw}");
        var json = JsonDocument.Parse(raw).RootElement.Clone();
        Assert.Equal(code, json.GetProperty("code").GetString());
        return json;
    }

    private static async Task NoBody(HttpResponseMessage response, HttpStatusCode status)
    {
        var text = await response.Content.ReadAsStringAsync();
        Assert.True(response.StatusCode == status, $"Expected {status}, got {(int)response.StatusCode}: {text}");
        Assert.Equal(string.Empty, text);
    }

    private async Task<Guid> Upload(RecordingFixture.Scenario s)
    {
        using var client = fixture.Client(s.Secretary);
        var body = await AssertCode(await client.PostAsync(s.Url, Form()), 201, "MSG290");
        return body.GetProperty("recordingId").GetGuid();
    }

    private static string Takes(RecordingFixture.Scenario s) => $"{s.Url}/takes";

    private static Task<HttpResponseMessage> Begin(HttpClient client, RecordingFixture.Scenario s, bool consent = true)
        => client.PostAsJsonAsync(Takes(s), new { consentConfirmed = consent, consentPurpose = "Lập biên bản phiên họp" });

    private static Task<HttpResponseMessage> Put(HttpClient client, Guid take, int part, byte[] bytes, bool final = false)
    {
        var body = new ByteArrayContent(bytes);
        body.Headers.ContentType = new MediaTypeHeaderValue("application/octet-stream");
        return client.PutAsync($"/api/recordings/{take}/parts/{part}{(final ? "?final=true" : "")}", body);
    }

    private static async Task<Guid> Open(HttpClient client, RecordingFixture.Scenario s, bool consent = true)
    {
        var opened = await Begin(client, s, consent);
        Assert.Equal(HttpStatusCode.Created, opened.StatusCode);
        return (await opened.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("recordingId").GetGuid();
    }

    /// <summary>Streams the tape as the browser does: full parts, then a short last one, then the mic goes off.</summary>
    private static async Task<Guid> Record(HttpClient client, Guid take, byte[] tape)
    {
        var parts = tape.Chunk(Part).ToArray();
        for (var i = 0; i < parts.Length; i++)
        {
            var final = i == parts.Length - 1 && parts[i].Length < Part;
            Assert.Equal(HttpStatusCode.NoContent, (await Put(client, take, i + 1, parts[i], final)).StatusCode);
        }
        var saved = await AssertCode(await client.PostAsync($"/api/recordings/{take}/complete", null), 200, "MSG363");
        return saved.GetProperty("recordingId").GetGuid();
    }

    private Task<HttpResponseMessage> SendSentence(HttpClient client, Guid meetingId, Guid clipId)
    {
        fixture.Speech.Failure = null;
        fixture.Speech.Result = OneSentence;
        var content = new ByteArrayContent(Wav);
        content.Headers.ContentType = MediaTypeHeaderValue.Parse("audio/wav");
        var started = Uri.EscapeDataString(fixture.Clock.Now.AddSeconds(-3).ToString("O"));
        return client.PutAsync($"/api/meetings/{meetingId}/speech-clips/{clipId}?startedAt={started}&durationMs=2400", content);
    }

    private async Task<Guid> StoreSentence(RecordingFixture.Scenario s)
    {
        using var client = fixture.Client(s.Member);
        var json = await AssertCode(await SendSentence(client, s.MeetingId, Guid.NewGuid()), 200, "MSG318");
        return json.GetProperty("clip").GetProperty("id").GetGuid();
    }

    private sealed record Guest(Guid InvitationId, Guid SessionId, string Token);

    private async Task<Guest> InviteGuest(RecordingFixture.Scenario s)
    {
        await using var db = fixture.Db();
        var invitation = new MeetingGuestInvitation
        {
            MeetingId = s.MeetingId, InvitedName = "Khách được mời", Email = $"{Guid.NewGuid():N}@guests.test",
            InvitedById = s.Secretary, SelfDeclaredDisplayName = "Khách tự khai",
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

    [Fact]
    public async Task Recordings_list_needs_a_token()
    {
        var s = await fixture.Seed();
        using var anonymous = fixture.Client();
        Assert.Equal(HttpStatusCode.Unauthorized, (await anonymous.GetAsync(s.Url)).StatusCode);
    }

    [Fact]
    public async Task Upload_rejects_a_non_multipart_or_fileless_request()
    {
        // The two MSG291 refusals, each with its own message, and nothing is stored.
        var s = await fixture.Seed();
        using var client = fixture.Client(s.Secretary);

        var json = await AssertCode(await client.PostAsync(s.Url, JsonContent.Create(new { file = "x" })), 400, "MSG291");
        Assert.Equal("Yêu cầu phải gửi dạng multipart/form-data có tệp ghi âm.", json.GetProperty("message").GetString());

        var fileless = new MultipartFormDataContent { { new StringContent("true"), "consentConfirmed" } };
        json = await AssertCode(await client.PostAsync(s.Url, fileless), 400, "MSG291");
        Assert.Equal("Chưa chọn tệp ghi âm.", json.GetProperty("message").GetString());

        Assert.Empty((await client.GetFromJsonAsync<JsonElement>(s.Url)).GetProperty("items").EnumerateArray());
    }

    [Fact]
    public async Task Recording_upload_needs_a_token()
    {
        var s = await fixture.Seed();
        using var anonymous = fixture.Client();
        Assert.Equal(HttpStatusCode.Unauthorized, (await anonymous.PostAsync(s.Url, Form())).StatusCode);
    }

    [Fact]
    public async Task Take_part_upload_needs_a_token()
    {
        // The mic part route answers before any byte is read.
        using var anonymous = fixture.Client();
        Assert.Equal(HttpStatusCode.Unauthorized,
            (await anonymous.PutAsync($"/api/recordings/{Guid.CreateVersion7()}/parts/1", new ByteArrayContent(Audio))).StatusCode);
    }

    [Fact]
    public async Task Completing_a_take_needs_the_roster_or_a_token()
    {
        // An outsider's completion is the same empty 404 as an unknown take.
        var s = await fixture.Seed();
        using var secretary = fixture.Client(s.Secretary);
        var take = await Open(secretary, s);

        using var outsider = fixture.Client(s.Outsider);
        await NoBody(await outsider.PostAsync($"/api/recordings/{take}/complete", null), HttpStatusCode.NotFound);

        using var anonymous = fixture.Client();
        Assert.Equal(HttpStatusCode.Unauthorized,
            (await anonymous.PostAsync($"/api/recordings/{take}/complete", null)).StatusCode);
    }

    [Fact]
    public async Task Beginning_a_take_needs_a_token()
    {
        var s = await fixture.Seed();
        using var anonymous = fixture.Client();
        Assert.Equal(HttpStatusCode.Unauthorized, (await anonymous.PostAsync(Takes(s), null)).StatusCode);
    }

    [Fact]
    public async Task Deleting_a_take_needs_the_roster_or_a_token()
    {
        // The room recording keeps both of its takes.
        var s = await fixture.Seed();
        using var secretary = fixture.Client(s.Secretary);
        var room = await Record(secretary, await Open(secretary, s), Tape(2 * Part + 12, 1));
        await Record(secretary, await Open(secretary, s, consent: false), Tape(Part + 460, 2));
        await using (var db = fixture.Db())
            await db.Meetings.Where(m => m.Id == s.MeetingId)
                .ExecuteUpdateAsync(m => m.SetProperty(x => x.Status, MeetingStatus.Ended));

        using var outsider = fixture.Client(s.Outsider);
        await NoBody(await outsider.DeleteAsync($"/api/recordings/{room}/takes/1"), HttpStatusCode.NotFound);

        using var anonymous = fixture.Client();
        Assert.Equal(HttpStatusCode.Unauthorized,
            (await anonymous.DeleteAsync($"/api/recordings/{room}/takes/1")).StatusCode);

        var takes = (await secretary.GetFromJsonAsync<JsonElement>(s.Url)).GetProperty("items")[0]
            .GetProperty("takes").EnumerateArray().ToArray();
        Assert.Equal(2, takes.Length);
    }

    [Fact]
    public async Task Erasing_a_recording_needs_a_token()
    {
        var s = await fixture.Seed(MeetingStatus.Ended);
        var id = await Upload(s);
        using var anonymous = fixture.Client();
        Assert.Equal(HttpStatusCode.Unauthorized, (await anonymous.DeleteAsync($"/api/recordings/{id}")).StatusCode);
    }

    [Fact]
    public async Task Playback_needs_a_token_and_stays_closed_to_guests()
    {
        // A guest admitted to the call still does not listen to the room recording (403, no body).
        var s = await fixture.Seed(mode: MeetingMode.Online);
        var guest = await InviteGuest(s);
        using var guestClient = GuestClient(guest);
        Assert.Equal(HttpStatusCode.OK, (await guestClient.PostAsJsonAsync($"/api/meetings/{s.MeetingId}/call/join",
            new { recordingNoticeAcknowledged = true })).StatusCode);

        var playback = $"/api/recordings/{Guid.CreateVersion7()}/playback";
        using var anonymous = fixture.Client();
        Assert.Equal(HttpStatusCode.Unauthorized, (await anonymous.GetAsync(playback)).StatusCode);
        await NoBody(await guestClient.GetAsync(playback), HttpStatusCode.Forbidden);
    }

    [Fact]
    public async Task Downloading_an_erased_recording_needs_the_roster_or_a_token()
    {
        // An erased recording is gone for the Chair as for anyone else.
        var s = await fixture.Seed(MeetingStatus.Ended);
        var id = await Upload(s);
        using var secretary = fixture.Client(s.Secretary);
        await AssertCode(await secretary.DeleteAsync($"/api/recordings/{id}"), 200, "MSG296");

        using var chair = fixture.Client(s.Chair);
        await NoBody(await chair.GetAsync($"/api/recordings/{id}/download"), HttpStatusCode.NotFound);

        using var anonymous = fixture.Client();
        Assert.Equal(HttpStatusCode.Unauthorized, (await anonymous.GetAsync($"/api/recordings/{id}/download")).StatusCode);
    }

    [Fact]
    public async Task Speech_clips_list_needs_a_token()
    {
        var s = await fixture.Seed(mode: MeetingMode.Online);
        using var anonymous = fixture.Client();
        Assert.Equal(HttpStatusCode.Unauthorized,
            (await anonymous.GetAsync($"/api/meetings/{s.MeetingId}/speech-clips")).StatusCode);
    }

    [Fact]
    public async Task Sending_a_sentence_needs_the_roster_or_a_token()
    {
        // An outsider's sentence is the same empty 404 as an unknown meeting.
        var s = await fixture.Seed(mode: MeetingMode.Online);
        var clipId = Guid.NewGuid();
        var started = Uri.EscapeDataString(fixture.Clock.Now.AddSeconds(-3).ToString("O"));
        var url = $"/api/meetings/{s.MeetingId}/speech-clips/{clipId}?startedAt={started}&durationMs=2400";

        using var outsider = fixture.Client(s.Outsider);
        var audio = new ByteArrayContent(Wav);
        audio.Headers.ContentType = MediaTypeHeaderValue.Parse("audio/wav");
        await NoBody(await outsider.PutAsync(url, audio), HttpStatusCode.NotFound);

        using var anonymous = fixture.Client();
        var anonymousAudio = new ByteArrayContent(Wav);
        anonymousAudio.Headers.ContentType = MediaTypeHeaderValue.Parse("audio/wav");
        Assert.Equal(HttpStatusCode.Unauthorized, (await anonymous.PutAsync(url, anonymousAudio)).StatusCode);
    }

    [Fact]
    public async Task Sentence_playback_needs_a_token()
    {
        using var anonymous = fixture.Client();
        Assert.Equal(HttpStatusCode.Unauthorized,
            (await anonymous.GetAsync($"/api/speech-clips/{Guid.CreateVersion7()}/playback")).StatusCode);
    }

    [Fact]
    public async Task Excluding_a_sentence_needs_the_roster_or_a_token()
    {
        // Only the roster reaches the sentence; anyone else gets the same empty 404.
        var s = await fixture.Seed(mode: MeetingMode.Online);
        var clip = await StoreSentence(s);

        using var outsider = fixture.Client(s.Outsider);
        await NoBody(await outsider.PutAsJsonAsync($"/api/speech-clips/{clip}/exclusion",
            new { excluded = true, reason = "Other" }), HttpStatusCode.NotFound);

        using var anonymous = fixture.Client();
        Assert.Equal(HttpStatusCode.Unauthorized, (await anonymous.PutAsJsonAsync($"/api/speech-clips/{clip}/exclusion",
            new { excluded = true, reason = "Other" })).StatusCode);
    }

    [Fact]
    public async Task Deleting_a_sentence_needs_the_roster_or_a_token()
    {
        var s = await fixture.Seed(mode: MeetingMode.Online);
        var clip = await StoreSentence(s);

        using var outsider = fixture.Client(s.Outsider);
        await NoBody(await outsider.DeleteAsync($"/api/speech-clips/{clip}"), HttpStatusCode.NotFound);

        using var anonymous = fixture.Client();
        Assert.Equal(HttpStatusCode.Unauthorized, (await anonymous.DeleteAsync($"/api/speech-clips/{clip}")).StatusCode);
    }
}
