using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Security.Cryptography;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using TrustCabinet.Domain.Audit;
using TrustCabinet.Domain.Transcription;
using TrustCabinet.Domain.Users;
using TrustCabinet.Domain.VoiceId;

namespace TrustCabinet.Web.Tests.Recordings;

public sealed class VoiceIdEndpointTests(RecordingFixture fixture) : IClassFixture<RecordingFixture>
{
    private const string Model = "eres2net-base-200k@0123456789ab";

    private static async Task<JsonElement> AssertCode(HttpResponseMessage response, int status, string code)
    {
        var raw = await response.Content.ReadAsStringAsync();
        Assert.True((int)response.StatusCode == status, $"Expected {status}, got {(int)response.StatusCode}: {raw}");
        var json = JsonDocument.Parse(raw).RootElement.Clone();
        Assert.Equal(code, json.GetProperty("code").GetString());
        return json;
    }

    private static MultipartFormDataContent Sample(string type = "audio/webm")
    {
        var file = new ByteArrayContent([.. "OggS"u8, .. new byte[512]]);
        file.Headers.ContentType = MediaTypeHeaderValue.Parse(type);
        return new MultipartFormDataContent { { file, "file", "giong-mau.webm" } };
    }

    private Task<HttpResponseMessage> Consent(HttpClient client, string version = VoiceIdRules.NoticeVersion)
        => client.PostAsJsonAsync("/api/me/voice/consent", new { noticeVersion = version });

    private async Task Enrol(Guid user, params float[] embedding)
    {
        using var client = fixture.Client(user);
        await Consent(client);
        fixture.Speech.NextEmbedding = embedding;
        await AssertCode(await client.PostAsync("/api/me/voice/samples", Sample()), 201, "MSG307");
    }

    private async Task ProcessAll()
    {
        while (true)
        {
            using var scope = fixture.Host.Services.CreateScope();
            if (!await scope.ServiceProvider.GetRequiredService<ITranscriptionService>().ProcessNextAsync()) return;
        }
    }

    private async Task<Guid> UploadAndTranscribe(RecordingFixture.Scenario s, params SpeechSpeaker[] speakers)
    {
        await ProcessAll();
        using var client = fixture.Client(s.Secretary);
        var form = new MultipartFormDataContent { { new StringContent("true"), "consentConfirmed" } };
        var file = new ByteArrayContent([.. "RIFF"u8, .. new byte[256]]);
        file.Headers.ContentType = MediaTypeHeaderValue.Parse("audio/wav");
        form.Add(file, "file", "hop.wav");
        var id = (await (await client.PostAsync(s.Url, form)).Content.ReadFromJsonAsync<JsonElement>()).GetProperty("recordingId").GetGuid();
        fixture.Speech.Failure = null;
        fixture.Speech.Result = new(60000, "vi", "gipformer-1.5-68m-int8@abc123def456", Model,
            [.. speakers.Select((sp, i) => new SpeechSegment(i * 10000, i * 10000 + 9000, sp.Label, $"Phát biểu {i + 1}."))], speakers);
        await ProcessAll();
        return id;
    }

    [Fact]
    public async Task Consent_is_explicit_versioned_and_given_once()
    {
        var s = await fixture.Seed();
        using var client = fixture.Client(s.Member);
        var profile = await client.GetFromJsonAsync<JsonElement>("/api/me/voice");
        Assert.Equal(JsonValueKind.Null, profile.GetProperty("consent").ValueKind);
        Assert.Equal(VoiceIdRules.NoticeVersion, profile.GetProperty("noticeVersion").GetString());
        Assert.Equal(VoiceIdRules.MaxSamples, profile.GetProperty("maxSamples").GetInt32());
        Assert.False(string.IsNullOrWhiteSpace(profile.GetProperty("controller").GetString()));

        var stale = await AssertCode(await Consent(client, "0.9"), 400, "MSG313");
        Assert.True(stale.GetProperty("errors").TryGetProperty("noticeVersion", out _));
        var granted = await AssertCode(await Consent(client), 200, "MSG305");
        Assert.Equal(VoiceIdRules.Purpose, granted.GetProperty("data").GetProperty("consent").GetProperty("purpose").GetString());
        await AssertCode(await Consent(client), 200, "MSG305");
        await using var db = fixture.Db();
        Assert.Equal(1, await db.VoiceConsents.CountAsync(c => c.UserId == s.Member && c.WithdrawnAt == null));
        Assert.Equal(1, await db.AuditLog.CountAsync(a => a.Action == AuditActions.VoiceConsentGranted && a.ActorId == s.Member));
        using var anonymous = fixture.Client();
        Assert.Equal(HttpStatusCode.Unauthorized, (await anonymous.GetAsync("/api/me/voice")).StatusCode);
    }

    [Fact]
    public async Task Samples_need_consent_pass_the_worker_checks_and_are_stored_only_encrypted()
    {
        var s = await fixture.Seed();
        using var client = fixture.Client(s.Chair);
        await AssertCode(await client.PostAsync("/api/me/voice/samples", Sample()), 409, "MSG308");
        await Consent(client);

        fixture.Speech.EmbedFailure = new VoiceSampleRejectedException("multiple_speakers");
        var rejected = await AssertCode(await client.PostAsync("/api/me/voice/samples", Sample()), 422, "MSG309");
        Assert.Equal("multiple_speakers", rejected.GetProperty("reason").GetString());
        Assert.Contains("hơn một người", rejected.GetProperty("message").GetString());
        fixture.Speech.EmbedFailure = new SpeechEngineException("Không kết nối được máy chủ nhận dạng giọng nói.");
        await AssertCode(await client.PostAsync("/api/me/voice/samples", Sample()), 503, "MSG312");
        fixture.Speech.EmbedFailure = null;
        await AssertCode(await client.PostAsync("/api/me/voice/samples", Sample("text/plain")), 415, "MSG292");
        await AssertCode(await client.PostAsync("/api/me/voice/samples", JsonContent.Create(new { })), 400, "MSG313");

        fixture.Speech.NextEmbedding = [0.25f, -0.5f, 0.75f];
        var added = await AssertCode(await client.PostAsync("/api/me/voice/samples", Sample()), 201, "MSG307");
        var sampleId = added.GetProperty("data").GetProperty("id").GetGuid();
        Assert.Equal(7200, added.GetProperty("data").GetProperty("speechMs").GetInt64());
        for (var i = 1; i < VoiceIdRules.MaxSamples; i++)
            await AssertCode(await client.PostAsync("/api/me/voice/samples", Sample()), 201, "MSG307");
        await AssertCode(await client.PostAsync("/api/me/voice/samples", Sample()), 409, "MSG310");

        await using var db = fixture.Db();
        var stored = await db.VoiceSamples.SingleAsync(x => x.Id == sampleId);
        var plain = new byte[12];
        Buffer.BlockCopy(new[] { 0.25f, -0.5f, 0.75f }, 0, plain, 0, 12);
        Assert.Equal(12 + 12 + 16, stored.Ciphertext.Length);
        Assert.DoesNotContain(Convert.ToHexString(plain), Convert.ToHexString(stored.Ciphertext));
        using var scope = fixture.Host.Services.CreateScope();
        var protector = scope.ServiceProvider.GetRequiredService<IVoiceprintProtector>();
        Assert.Equal([0.25f, -0.5f, 0.75f], protector.Unprotect(stored.Ciphertext, s.Chair, sampleId, Model));
        // Bound to its owner and its id: moved to someone else, or replayed under another id, it does not decrypt.
        Assert.ThrowsAny<CryptographicException>(() => protector.Unprotect(stored.Ciphertext, s.Member, sampleId, Model));
        Assert.ThrowsAny<CryptographicException>(() => protector.Unprotect(stored.Ciphertext, s.Chair, Guid.NewGuid(), Model));
        var profile = await client.GetFromJsonAsync<JsonElement>("/api/me/voice");
        Assert.Equal(VoiceIdRules.MaxSamples, profile.GetProperty("samples").GetArrayLength());
        Assert.DoesNotContain("ciphertext", profile.GetRawText(), StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task A_person_deletes_only_their_own_samples()
    {
        var s = await fixture.Seed();
        await Enrol(s.Member, 1, 0, 0);
        await using var db = fixture.Db();
        var sample = await db.VoiceSamples.Where(x => x.UserId == s.Member).Select(x => x.Id).SingleAsync();
        using var other = fixture.Client(s.Chair);
        Assert.Equal(HttpStatusCode.NotFound, (await other.DeleteAsync($"/api/me/voice/samples/{sample}")).StatusCode);
        using var owner = fixture.Client(s.Member);
        await AssertCode(await owner.DeleteAsync($"/api/me/voice/samples/{sample}"), 200, "MSG311");
        Assert.False(await db.VoiceSamples.AnyAsync(x => x.Id == sample));
    }

    [Fact]
    public async Task A_sample_recording_is_kept_only_under_the_newer_notice_and_only_its_owner_hears_it()
    {
        var s = await fixture.Seed();
        await using (var seed = fixture.Db())
        {
            // Agreed before ADR-0042, to a notice that said the recording is not kept.
            var now = DateTimeOffset.UtcNow;
            seed.VoiceConsents.Add(new() { Id = Guid.CreateVersion7(), UserId = s.Member, GrantedAt = now, NoticeVersion = "1.0",
                Purpose = VoiceIdRules.Purpose, CreatedAt = now, UpdatedAt = now });
            await seed.SaveChangesAsync();
        }
        using var owner = fixture.Client(s.Member);
        fixture.Speech.NextEmbedding = [1f, 0f, 0f];
        var old = await AssertCode(await owner.PostAsync("/api/me/voice/samples", Sample()), 201, "MSG307");
        Assert.False(old.GetProperty("data").GetProperty("playable").GetBoolean());
        var oldId = old.GetProperty("data").GetProperty("id").GetGuid();
        Assert.Equal(HttpStatusCode.NotFound, (await owner.GetAsync($"/api/me/voice/samples/{oldId}/audio")).StatusCode);

        // Agreeing to the newer notice renews the same consent; the samples stay.
        var renewed = await AssertCode(await Consent(owner), 200, "MSG305");
        Assert.Equal(VoiceIdRules.NoticeVersion, renewed.GetProperty("data").GetProperty("consent").GetProperty("noticeVersion").GetString());
        Assert.Equal(1, renewed.GetProperty("data").GetProperty("samples").GetArrayLength());
        await using var db = fixture.Db();
        Assert.Equal(1, await db.VoiceConsents.CountAsync(c => c.UserId == s.Member));
        Assert.Equal(1, await db.AuditLog.CountAsync(a => a.Action == AuditActions.VoiceConsentRenewed && a.ActorId == s.Member));

        fixture.Speech.NextEmbedding = [0f, 1f, 0f];
        var added = await AssertCode(await owner.PostAsync("/api/me/voice/samples", Sample()), 201, "MSG307");
        Assert.True(added.GetProperty("data").GetProperty("playable").GetBoolean());
        var id = added.GetProperty("data").GetProperty("id").GetGuid();
        var heard = await owner.GetAsync($"/api/me/voice/samples/{id}/audio");
        Assert.Equal(HttpStatusCode.OK, heard.StatusCode);
        Assert.Equal("audio/webm", heard.Content.Headers.ContentType?.MediaType);
        var body = await heard.Content.ReadAsByteArrayAsync();
        Assert.Equal([.. "OggS"u8, .. new byte[512]], body);
        Assert.Contains("no-store", heard.Headers.CacheControl?.ToString());

        // Stored encrypted, bound to its owner: someone else, or nobody signed in, never hears it.
        var stored = await db.VoiceSamples.AsNoTracking().SingleAsync(x => x.Id == id);
        Assert.DoesNotContain(Convert.ToHexString("OggS"u8), Convert.ToHexString(stored.AudioCiphertext!));
        using var other = fixture.Client(s.Chair);
        Assert.Equal(HttpStatusCode.NotFound, (await other.GetAsync($"/api/me/voice/samples/{id}/audio")).StatusCode);
        using var anonymous = fixture.Client();
        Assert.Equal(HttpStatusCode.Unauthorized, (await anonymous.GetAsync($"/api/me/voice/samples/{id}/audio")).StatusCode);
    }

    [Fact]
    public async Task One_line_is_named_apart_from_its_voice_and_every_other_line_keeps_the_voice_s_name()
    {
        var s = await fixture.Seed();
        await ProcessAll();
        using var secretary = fixture.Client(s.Secretary);
        var form = new MultipartFormDataContent { { new StringContent("true"), "consentConfirmed" } };
        var file = new ByteArrayContent([.. "RIFF"u8, .. new byte[256]]);
        file.Headers.ContentType = MediaTypeHeaderValue.Parse("audio/wav");
        form.Add(file, "file", "hop.wav");
        var id = (await (await secretary.PostAsync(s.Url, form)).Content.ReadFromJsonAsync<JsonElement>()).GetProperty("recordingId").GetGuid();
        fixture.Speech.Failure = null;
        // The diarizer put a line of someone else under the first voice.
        fixture.Speech.Result = new(30000, "vi", "gipformer-1.5-68m-int8@abc123def456", Model,
            [new(0, 9000, "SPEAKER_00", "Câu một."), new(10000, 19000, "SPEAKER_00", "Câu hai, của người khác."),
                new(20000, 29000, "SPEAKER_00", "Câu ba.")],
            [new("SPEAKER_00", 27000, [0f, 0f, 1f])]);
        await ProcessAll();
        await AssertCode(await secretary.PutAsJsonAsync($"/api/recordings/{id}/speakers/SPEAKER_00", new { userId = s.Chair }), 200, "MSG316");
        var segments = (await secretary.GetFromJsonAsync<JsonElement>($"/api/recordings/{id}/transcript")).GetProperty("segments")
            .EnumerateArray().Select(x => x.GetProperty("id").GetGuid()).ToArray();

        using var member = fixture.Client(s.Member);
        await AssertCode(await member.PutAsJsonAsync($"/api/transcript-segments/{segments[1]}/speaker", new { userId = s.Member }), 403, "MSG295");
        await AssertCode(await secretary.PutAsJsonAsync($"/api/transcript-segments/{segments[1]}/speaker", new { userId = s.Outsider }), 400, "MSG317");
        var saved = await AssertCode(await secretary.PutAsJsonAsync($"/api/transcript-segments/{segments[1]}/speaker", new { userId = s.Member }), 200, "MSG407");
        Assert.Equal("Recording test 2", saved.GetProperty("segment").GetProperty("speakerName").GetString());

        var transcript = await secretary.GetFromJsonAsync<JsonElement>($"/api/recordings/{id}/transcript");
        var lines = transcript.GetProperty("segments").EnumerateArray().ToArray();
        Assert.Equal(s.Member, lines[1].GetProperty("speakerUserId").GetGuid());
        Assert.Equal("Recording test 2", lines[1].GetProperty("speakerName").GetString());
        // Only that line moved: the voice and its other lines still read as the Chair.
        Assert.All([lines[0], lines[2]], x => Assert.Equal(JsonValueKind.Null, x.GetProperty("speakerUserId").ValueKind));
        Assert.Equal(s.Chair, transcript.GetProperty("speakers")[0].GetProperty("assignedUserId").GetGuid());
        var text = await (await secretary.GetAsync($"/api/recordings/{id}/transcript.txt")).Content.ReadAsStringAsync();
        Assert.Contains("Recording test 2: Câu hai, của người khác.", text);
        Assert.Contains("Recording test 1: Câu ba.", text);

        // Both null hands the line back to its voice.
        await AssertCode(await secretary.PutAsJsonAsync($"/api/transcript-segments/{segments[1]}/speaker", new { userId = (Guid?)null }), 200, "MSG407");
        await using var db = fixture.Db();
        Assert.Null((await db.TranscriptSegments.SingleAsync(x => x.Id == segments[1])).SpeakerUserId);
        Assert.Equal(2, await db.AuditLog.CountAsync(a => a.Action == AuditActions.TranscriptSegmentSpeakerAssigned && a.ObjectId == id));
    }

    [Fact]
    public async Task Voices_are_suggested_only_as_consenting_people_of_the_meeting_and_named_only_by_the_secretary()
    {
        var s = await fixture.Seed();
        await Enrol(s.Secretary, 1, 0, 0);
        await Enrol(s.Member, 0, 1, 0);
        // Sounds exactly like the third voice, but is not in this meeting.
        await Enrol(s.Outsider, 0, 0, 1);
        var id = await UploadAndTranscribe(s,
            new("SPEAKER_00", 30000, [0.95f, 0.2f, 0f]),
            new("SPEAKER_01", 20000, [0.1f, 0.97f, 0.05f]),
            new("SPEAKER_02", 10000, [0f, 0.05f, 1f]));

        using var secretary = fixture.Client(s.Secretary);
        var transcript = await secretary.GetFromJsonAsync<JsonElement>($"/api/recordings/{id}/transcript");
        var speakers = transcript.GetProperty("speakers").EnumerateArray().ToDictionary(x => x.GetProperty("label").GetString()!);
        Assert.Equal(s.Secretary, speakers["SPEAKER_00"].GetProperty("suggestedUserId").GetGuid());
        Assert.Equal("Recording test 0", speakers["SPEAKER_00"].GetProperty("suggestedName").GetString());
        Assert.True(speakers["SPEAKER_00"].GetProperty("suggestionScore").GetDouble() > 0.9);
        Assert.Equal(s.Member, speakers["SPEAKER_01"].GetProperty("suggestedUserId").GetGuid());
        Assert.Equal(JsonValueKind.Null, speakers["SPEAKER_02"].GetProperty("suggestedUserId").ValueKind);
        // Both matches are sure, so the machine named them itself (PO 01/11/2026); the stranger's voice stays unnamed.
        Assert.Equal(s.Secretary, speakers["SPEAKER_00"].GetProperty("assignedUserId").GetGuid());
        Assert.True(speakers["SPEAKER_00"].GetProperty("autoAssigned").GetBoolean());
        Assert.Equal(s.Member, speakers["SPEAKER_01"].GetProperty("assignedUserId").GetGuid());
        Assert.Equal(JsonValueKind.Null, speakers["SPEAKER_02"].GetProperty("assignedUserId").ValueKind);
        Assert.False(speakers["SPEAKER_02"].GetProperty("autoAssigned").GetBoolean());
        Assert.Equal(3, transcript.GetProperty("people").GetArrayLength());
        Assert.DoesNotContain("embedding", transcript.GetRawText(), StringComparison.OrdinalIgnoreCase);

        using var member = fixture.Client(s.Member);
        var seen = await member.GetFromJsonAsync<JsonElement>($"/api/recordings/{id}/transcript");
        Assert.Equal(0, seen.GetProperty("people").GetArrayLength());
        await AssertCode(await member.PutAsJsonAsync($"/api/recordings/{id}/speakers/SPEAKER_00", new { userId = s.Member }), 403, "MSG295");

        var named = await AssertCode(await secretary.PutAsJsonAsync($"/api/recordings/{id}/speakers/SPEAKER_00", new { userId = s.Secretary }), 200, "MSG316");
        Assert.Equal("Recording test 0", named.GetProperty("speaker").GetProperty("assignedName").GetString());
        // Confirmed by the Secretary, the name is theirs now, no longer the machine's.
        Assert.False(named.GetProperty("speaker").GetProperty("autoAssigned").GetBoolean());
        await AssertCode(await secretary.PutAsJsonAsync($"/api/recordings/{id}/speakers/SPEAKER_02", new { userId = s.Outsider }), 400, "MSG317");
        await AssertCode(await secretary.PutAsJsonAsync($"/api/recordings/{id}/speakers/SPEAKER_02", new { userId = s.Chair }), 200, "MSG316");
        await AssertCode(await secretary.PutAsJsonAsync($"/api/recordings/{id}/speakers/SPEAKER_02", new { userId = (Guid?)null }), 200, "MSG316");
        Assert.Equal(HttpStatusCode.NotFound, (await secretary.PutAsJsonAsync($"/api/recordings/{id}/speakers/SPEAKER_09", new { userId = s.Chair })).StatusCode);

        await using var db = fixture.Db();
        var audit = await db.AuditLog.Where(a => a.Action == AuditActions.TranscriptSpeakerAssigned && a.ActorId == s.Secretary)
            .OrderBy(a => a.OccurredAt).FirstAsync();
        Assert.Contains("\"acceptedSuggestion\":true", audit.Details.Replace(" ", ""));
        var automatic = (await db.AuditLog.Where(a => a.Action == AuditActions.TranscriptSpeakerAssigned && a.ActorId == null).ToListAsync())
            .Where(a => a.Details.Contains(id.ToString())).ToList();
        Assert.Equal(2, automatic.Count);
        Assert.All(automatic, a => Assert.Contains("\"automatic\":true", a.Details.Replace(" ", "")));
    }

    [Fact]
    public async Task Only_a_sure_match_is_named_by_the_machine_and_the_name_goes_with_the_consent()
    {
        var s = await fixture.Seed();
        await Enrol(s.Secretary, 1, 0, 0);
        await Enrol(s.Member, 0, 1, 0);
        // Member at 0.995: named at once. Secretary at 0.66: over the suggestion threshold, under the sure one.
        var id = await UploadAndTranscribe(s, new("SPEAKER_00", 30000, [0.66f, 0.15f, 0.736f]), new("SPEAKER_01", 20000, [0f, 1f, 0.1f]));

        await using var db = fixture.Db();
        var fair = await db.TranscriptSpeakers.SingleAsync(x => x.Transcript.RecordingId == id && x.Label == "SPEAKER_00");
        Assert.Equal(s.Secretary, fair.SuggestedUserId);
        Assert.Null(fair.AssignedUserId);
        var sure = await db.TranscriptSpeakers.SingleAsync(x => x.Transcript.RecordingId == id && x.Label == "SPEAKER_01");
        Assert.Equal(s.Member, sure.AssignedUserId);
        Assert.Null(sure.AssignedById);

        // The machine's name came from the voice, so it goes when the person withdraws consent.
        using var member = fixture.Client(s.Member);
        await AssertCode(await member.DeleteAsync("/api/me/voice/consent"), 200, "MSG306");
        await db.Entry(sure).ReloadAsync();
        Assert.Null(sure.AssignedUserId);
        Assert.Null(sure.AssignedAt);
        Assert.Null(sure.SuggestedUserId);
    }

    [Fact]
    public async Task Someone_who_agreed_to_an_older_notice_is_only_suggested_until_they_renew()
    {
        var s = await fixture.Seed();
        await Enrol(s.Member, 0, 1, 0);
        await using var db = fixture.Db();
        // Agreed before ADR-0048, to a notice that said the machine only suggests.
        await db.VoiceConsents.Where(c => c.UserId == s.Member).ExecuteUpdateAsync(u => u.SetProperty(c => c.NoticeVersion, "1.1"));
        var before = await UploadAndTranscribe(s, new SpeechSpeaker("SPEAKER_00", 20000, [0f, 1f, 0.1f]));
        var suggested = await db.TranscriptSpeakers.SingleAsync(x => x.Transcript.RecordingId == before && x.Label == "SPEAKER_00");
        Assert.Equal(s.Member, suggested.SuggestedUserId);
        Assert.Null(suggested.AssignedUserId);

        using var member = fixture.Client(s.Member);
        await AssertCode(await Consent(member), 200, "MSG305");
        var after = await UploadAndTranscribe(s, new SpeechSpeaker("SPEAKER_00", 20000, [0f, 1f, 0.1f]));
        var named = await db.TranscriptSpeakers.SingleAsync(x => x.Transcript.RecordingId == after && x.Label == "SPEAKER_00");
        Assert.Equal(s.Member, named.AssignedUserId);
        Assert.Null(named.AssignedById);
    }

    [Fact]
    public async Task Withdrawing_erases_samples_and_suggestions_but_keeps_what_the_secretary_decided()
    {
        var s = await fixture.Seed();
        await Enrol(s.Secretary, 1, 0, 0);
        await Enrol(s.Member, 0, 1, 0);
        var id = await UploadAndTranscribe(s, new("SPEAKER_00", 30000, [1f, 0.1f, 0f]), new("SPEAKER_01", 20000, [0f, 1f, 0.1f]));
        using var secretary = fixture.Client(s.Secretary);
        await AssertCode(await secretary.PutAsJsonAsync($"/api/recordings/{id}/speakers/SPEAKER_01", new { userId = s.Member }), 200, "MSG316");

        using var member = fixture.Client(s.Member);
        var withdrawn = await AssertCode(await member.DeleteAsync("/api/me/voice/consent"), 200, "MSG306");
        Assert.Equal(JsonValueKind.Null, withdrawn.GetProperty("data").GetProperty("consent").ValueKind);
        Assert.Equal(0, withdrawn.GetProperty("data").GetProperty("samples").GetArrayLength());
        await AssertCode(await member.PostAsync("/api/me/voice/samples", Sample()), 409, "MSG308");

        await using var db = fixture.Db();
        Assert.False(await db.VoiceSamples.AnyAsync(x => x.UserId == s.Member));
        Assert.NotNull((await db.VoiceConsents.SingleAsync(c => c.UserId == s.Member)).WithdrawnAt);
        var speaker = await db.TranscriptSpeakers.SingleAsync(x => x.Transcript.RecordingId == id && x.Label == "SPEAKER_01");
        Assert.Null(speaker.SuggestedUserId);
        Assert.Null(speaker.SuggestionScore);
        Assert.Equal(s.Member, speaker.AssignedUserId);
        Assert.Equal(s.Secretary, (await db.TranscriptSpeakers.SingleAsync(x => x.Transcript.RecordingId == id && x.Label == "SPEAKER_00")).SuggestedUserId);
        Assert.Single(db.AuditLog.Where(a => a.Action == AuditActions.VoiceConsentWithdrawn && a.ActorId == s.Member));

        // Agreeing again starts a new consent; the withdrawn one stays as it was.
        await AssertCode(await Consent(member), 200, "MSG305");
        Assert.Equal(2, await db.VoiceConsents.CountAsync(c => c.UserId == s.Member));
    }

    [Fact]
    public async Task Deactivating_an_account_ends_its_voice_id_and_reactivating_restores_nothing()
    {
        var s = await fixture.Seed();
        await Enrol(s.Secretary, 1, 0, 0);
        await Enrol(s.Member, 0, 1, 0);
        // SPEAKER_01 is the member, named by the machine; SPEAKER_02 matches nobody and the Secretary names the member.
        var id = await UploadAndTranscribe(s, new("SPEAKER_00", 30000, [1f, 0.1f, 0f]), new("SPEAKER_01", 20000, [0f, 1f, 0.1f]),
            new("SPEAKER_02", 10000, [0f, 0f, 1f]));
        using var secretary = fixture.Client(s.Secretary);
        await AssertCode(await secretary.PutAsJsonAsync($"/api/recordings/{id}/speakers/SPEAKER_02", new { userId = s.Member }), 200, "MSG316");

        // The administrator is the actor; the member cannot sign in to withdraw any more.
        using (var scope = fixture.Host.Services.CreateScope())
            Assert.True((await scope.ServiceProvider.GetRequiredService<IUserService>().ChangeStatusAsync(s.Member, false, s.Organizer)).Succeeded);

        await using var db = fixture.Db();
        Assert.False(await db.VoiceSamples.AnyAsync(x => x.UserId == s.Member));
        Assert.NotNull((await db.VoiceConsents.SingleAsync(c => c.UserId == s.Member)).WithdrawnAt);
        var machine = await db.TranscriptSpeakers.SingleAsync(x => x.Transcript.RecordingId == id && x.Label == "SPEAKER_01");
        Assert.Null(machine.SuggestedUserId);
        Assert.Null(machine.AssignedUserId);
        var decided = await db.TranscriptSpeakers.SingleAsync(x => x.Transcript.RecordingId == id && x.Label == "SPEAKER_02");
        Assert.Equal(s.Member, decided.AssignedUserId);
        // Someone else's Voice ID is untouched.
        Assert.True(await db.VoiceSamples.AnyAsync(x => x.UserId == s.Secretary));
        var ended = await db.AuditLog.SingleAsync(a => a.Action == AuditActions.VoiceConsentWithdrawn && a.ActorId == s.Organizer);
        Assert.Contains("\"reason\":\"accountDeactivated\"", ended.Details.Replace(" ", ""));

        using (var scope = fixture.Host.Services.CreateScope())
            Assert.True((await scope.ServiceProvider.GetRequiredService<IUserService>().ChangeStatusAsync(s.Member, true, s.Organizer)).Succeeded);
        Assert.Equal(0, await db.VoiceConsents.CountAsync(c => c.UserId == s.Member && c.WithdrawnAt == null));
        Assert.False(await db.VoiceSamples.AnyAsync(x => x.UserId == s.Member));
    }

    [Fact]
    public async Task A_withdrawal_while_the_worker_matches_still_clears_what_it_writes()
    {
        var s = await fixture.Seed();
        await Enrol(s.Secretary, 1, 0, 0);
        await Enrol(s.Member, 0, 1, 0);
        Task<HttpResponseMessage>? withdrawal = null;
        using var member = fixture.Client(s.Member);
        // Right after the worker read the samples: the member withdraws before the result is written.
        fixture.Failures.AfterVoiceSampleRead = async () =>
        {
            withdrawal = Task.Run(() => member.DeleteAsync("/api/me/voice/consent"));
            // Long enough for a withdrawal that does not wait for the worker to finish first.
            await Task.WhenAny(withdrawal, Task.Delay(TimeSpan.FromSeconds(1)));
        };
        Guid id;
        try { id = await UploadAndTranscribe(s, new("SPEAKER_00", 30000, [1f, 0.1f, 0f]), new("SPEAKER_01", 20000, [0f, 1f, 0.1f])); }
        finally { fixture.Failures.AfterVoiceSampleRead = null; }
        Assert.NotNull(withdrawal);
        await AssertCode(await withdrawal, 200, "MSG306");

        await using var db = fixture.Db();
        var speaker = await db.TranscriptSpeakers.SingleAsync(x => x.Transcript.RecordingId == id && x.Label == "SPEAKER_01");
        Assert.Null(speaker.SuggestedUserId);
        Assert.Null(speaker.AssignedUserId);
        Assert.Equal(s.Secretary, (await db.TranscriptSpeakers.SingleAsync(x => x.Transcript.RecordingId == id && x.Label == "SPEAKER_00")).AssignedUserId);
    }

    [Fact]
    public async Task Outages_answer_with_their_own_codes()
    {
        var s = await fixture.Seed();
        using var client = fixture.Client(s.Member);
        fixture.Failures.FailSave = true;
        try { await AssertCode(await Consent(client), 503, "MSG315"); }
        finally { fixture.Failures.FailSave = false; }
        fixture.Failures.FailVoiceRead = true;
        try { await AssertCode(await client.GetAsync("/api/me/voice"), 500, "MSG314"); }
        finally { fixture.Failures.FailVoiceRead = false; }
    }
}
