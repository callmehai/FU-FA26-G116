using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Security.Cryptography;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using TrustCabinet.Domain.Audit;
using TrustCabinet.Domain.Transcription;
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
        Assert.All(speakers.Values, x => Assert.Equal(JsonValueKind.Null, x.GetProperty("assignedUserId").ValueKind));
        Assert.Equal(3, transcript.GetProperty("people").GetArrayLength());
        Assert.DoesNotContain("embedding", transcript.GetRawText(), StringComparison.OrdinalIgnoreCase);

        using var member = fixture.Client(s.Member);
        var seen = await member.GetFromJsonAsync<JsonElement>($"/api/recordings/{id}/transcript");
        Assert.Equal(0, seen.GetProperty("people").GetArrayLength());
        await AssertCode(await member.PutAsJsonAsync($"/api/recordings/{id}/speakers/SPEAKER_00", new { userId = s.Member }), 403, "MSG295");

        var named = await AssertCode(await secretary.PutAsJsonAsync($"/api/recordings/{id}/speakers/SPEAKER_00", new { userId = s.Secretary }), 200, "MSG316");
        Assert.Equal("Recording test 0", named.GetProperty("speaker").GetProperty("assignedName").GetString());
        await AssertCode(await secretary.PutAsJsonAsync($"/api/recordings/{id}/speakers/SPEAKER_02", new { userId = s.Outsider }), 400, "MSG317");
        await AssertCode(await secretary.PutAsJsonAsync($"/api/recordings/{id}/speakers/SPEAKER_02", new { userId = s.Chair }), 200, "MSG316");
        await AssertCode(await secretary.PutAsJsonAsync($"/api/recordings/{id}/speakers/SPEAKER_02", new { userId = (Guid?)null }), 200, "MSG316");
        Assert.Equal(HttpStatusCode.NotFound, (await secretary.PutAsJsonAsync($"/api/recordings/{id}/speakers/SPEAKER_09", new { userId = s.Chair })).StatusCode);

        await using var db = fixture.Db();
        var audit = await db.AuditLog.Where(a => a.Action == AuditActions.TranscriptSpeakerAssigned).OrderBy(a => a.OccurredAt).FirstAsync();
        Assert.Contains("\"acceptedSuggestion\":true", audit.Details.Replace(" ", ""));
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
