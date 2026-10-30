using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using TrustCabinet.Domain.Enums;
using TrustCabinet.Domain.Transcription;

namespace TrustCabinet.Web.Tests.Recordings;

public sealed class TranscriptEndpointTests(RecordingFixture fixture) : IClassFixture<RecordingFixture>
{
    private static readonly byte[] Audio = [.. "RIFF"u8, .. new byte[4096]];

    private static async Task<JsonElement> AssertCode(HttpResponseMessage response, int status, string code)
    {
        var raw = await response.Content.ReadAsStringAsync();
        Assert.True((int)response.StatusCode == status, $"Expected {status}, got {(int)response.StatusCode}: {raw}");
        var json = JsonDocument.Parse(raw).RootElement.Clone();
        Assert.Equal(code, json.GetProperty("code").GetString());
        return json;
    }

    private async Task<Guid> Upload(RecordingFixture.Scenario s)
    {
        using var client = fixture.Client(s.Secretary);
        var form = new MultipartFormDataContent { { new StringContent("true"), "consentConfirmed" } };
        var file = new ByteArrayContent(Audio);
        file.Headers.ContentType = MediaTypeHeaderValue.Parse("audio/wav");
        form.Add(file, "file", "phien-hop.wav");
        var created = await client.PostAsync(s.Url, form);
        Assert.Equal(HttpStatusCode.Created, created.StatusCode);
        return (await created.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("recordingId").GetGuid();
    }

    /// <summary>Drains the queue the way TranscriptionWorker does, one recording at a time.</summary>
    private async Task ProcessAll()
    {
        while (true)
        {
            using var scope = fixture.Host.Services.CreateScope();
            if (!await scope.ServiceProvider.GetRequiredService<ITranscriptionService>().ProcessNextAsync()) return;
        }
    }

    private static Task<JsonElement> Transcript(HttpClient client, Guid id)
        => client.GetFromJsonAsync<JsonElement>($"/api/recordings/{id}/transcript");

    [Fact]
    public async Task Upload_queues_speech_to_text_and_the_result_keeps_speakers_and_timestamps()
    {
        var s = await fixture.Seed();
        fixture.Speech.Failure = null;
        var id = await Upload(s);
        using var member = fixture.Client(s.Member);
        Assert.Equal("Pending", (await member.GetFromJsonAsync<JsonElement>(s.Url)).GetProperty("items")[0].GetProperty("transcriptStatus").GetString());
        var waiting = await Transcript(member, id);
        Assert.Equal("Pending", waiting.GetProperty("status").GetString());
        Assert.Empty(waiting.GetProperty("segments").EnumerateArray());
        Assert.False(waiting.GetProperty("canEdit").GetBoolean());

        await ProcessAll();
        Assert.Equal(Audio, fixture.Speech.LastAudio);
        var done = await Transcript(member, id);
        Assert.Equal("Completed", done.GetProperty("status").GetString());
        Assert.Equal("self-hosted:gipformer-1.5-68m-int8@abc123def456", done.GetProperty("provider").GetString());
        Assert.Equal(23380, done.GetProperty("durationMs").GetInt64());
        var segments = done.GetProperty("segments").EnumerateArray().ToArray();
        Assert.Equal(2, segments.Length);
        Assert.Equal((1, 0L, 5800L, "SPEAKER_00", "Kính thưa các đồng chí."), (segments[0].GetProperty("position").GetInt32(),
            segments[0].GetProperty("startMs").GetInt64(), segments[0].GetProperty("endMs").GetInt64(),
            segments[0].GetProperty("speakerLabel").GetString(), segments[0].GetProperty("text").GetString()));
        Assert.Equal("SPEAKER_01", segments[1].GetProperty("speakerLabel").GetString());
        Assert.Equal("Completed", (await member.GetFromJsonAsync<JsonElement>(s.Url)).GetProperty("items")[0].GetProperty("transcriptStatus").GetString());
        Assert.DoesNotContain("embedding", done.GetRawText(), StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task A_failure_is_explained_and_only_the_secretary_runs_it_again()
    {
        var s = await fixture.Seed();
        await ProcessAll();
        fixture.Speech.Failure = new SpeechEngineException("Không kết nối được máy chủ nhận dạng giọng nói.");
        var id = await Upload(s);
        await ProcessAll();
        using var secretary = fixture.Client(s.Secretary);
        var failed = await Transcript(secretary, id);
        Assert.Equal("Failed", failed.GetProperty("status").GetString());
        Assert.Equal("Không kết nối được máy chủ nhận dạng giọng nói.", failed.GetProperty("errorMessage").GetString());

        using var chair = fixture.Client(s.Chair);
        await AssertCode(await chair.PostAsync($"/api/recordings/{id}/transcribe", null), 403, "MSG295");
        await AssertCode(await secretary.PostAsync($"/api/recordings/{id}/transcribe", null), 202, "MSG300");
        await AssertCode(await secretary.PostAsync($"/api/recordings/{id}/transcribe", null), 409, "MSG301");
        Assert.Equal("Pending", (await Transcript(secretary, id)).GetProperty("status").GetString());

        fixture.Speech.Failure = null;
        await ProcessAll();
        var done = await Transcript(secretary, id);
        Assert.Equal("Completed", done.GetProperty("status").GetString());
        Assert.Equal("", done.GetProperty("errorMessage").GetString());
        // A finished transcript can be made again (a newer model, voices enrolled since); the redo is audited.
        await AssertCode(await secretary.PostAsync($"/api/recordings/{id}/transcribe", null), 202, "MSG300");
        await AssertCode(await secretary.PostAsync($"/api/recordings/{id}/transcribe", null), 409, "MSG301");
        await using var db = fixture.Db();
        Assert.Equal(1, await db.AuditLog.CountAsync(a => a.ObjectId == id && a.Action == "transcript.rerun"));
    }

    [Fact]
    public async Task A_recording_without_a_transcript_can_be_sent_to_speech_to_text()
    {
        var s = await fixture.Seed();
        await ProcessAll();
        fixture.Speech.Failure = null;
        var id = await Upload(s);
        await using (var db = fixture.Db())
            await db.Transcripts.Where(t => t.RecordingId == id).ExecuteDeleteAsync();
        using var secretary = fixture.Client(s.Secretary);
        var none = await Transcript(secretary, id);
        Assert.Equal("None", none.GetProperty("status").GetString());
        Assert.True(none.GetProperty("canEdit").GetBoolean());
        await AssertCode(await secretary.PostAsync($"/api/recordings/{id}/transcribe", null), 202, "MSG300");
        await ProcessAll();
        Assert.Equal("Completed", (await Transcript(secretary, id)).GetProperty("status").GetString());
    }

    [Fact]
    public async Task A_missing_audio_file_fails_the_transcript_instead_of_the_queue()
    {
        var s = await fixture.Seed();
        await ProcessAll();
        fixture.Speech.Failure = null;
        var id = await Upload(s);
        await using (var db = fixture.Db())
        {
            var path = (await db.Recordings.SingleAsync(r => r.Id == id)).StoragePath;
            File.Delete(Path.Combine(fixture.StorageRoot, path));
        }
        await ProcessAll();
        using var secretary = fixture.Client(s.Secretary);
        var failed = await Transcript(secretary, id);
        Assert.Equal("Failed", failed.GetProperty("status").GetString());
        Assert.Equal("Tệp âm thanh không còn trên máy chủ.", failed.GetProperty("errorMessage").GetString());
    }

    [Fact]
    public async Task Secretary_corrects_a_segment_and_the_correction_is_attributed()
    {
        var s = await fixture.Seed();
        fixture.Speech.Failure = null;
        var id = await Upload(s);
        await ProcessAll();
        using var secretary = fixture.Client(s.Secretary);
        var segment = (await Transcript(secretary, id)).GetProperty("segments")[1];
        var url = $"/api/transcript-segments/{segment.GetProperty("id").GetGuid()}";

        var saved = await AssertCode(await secretary.PutAsJsonAsync(url, new { text = "  Tôi đề nghị bổ sung kinh phí đào tạo.  " }), 200, "MSG302");
        Assert.Equal("Tôi đề nghị bổ sung kinh phí đào tạo.", saved.GetProperty("segment").GetProperty("text").GetString());
        var after = (await Transcript(secretary, id)).GetProperty("segments")[1];
        Assert.Equal("Tôi đề nghị bổ sung kinh phí đào tạo.", after.GetProperty("text").GetString());
        Assert.Equal("Recording test 0", after.GetProperty("editedByName").GetString());
        Assert.Equal(fixture.Clock.Now, after.GetProperty("editedAt").GetDateTimeOffset());

        var invalid = await AssertCode(await secretary.PutAsJsonAsync(url, new { text = "   " }), 400, "MSG303");
        Assert.True(invalid.GetProperty("errors").TryGetProperty("text", out _));
        await AssertCode(await secretary.PutAsJsonAsync(url, new { text = new string('a', 8001) }), 400, "MSG303");
        using var member = fixture.Client(s.Member);
        await AssertCode(await member.PutAsJsonAsync(url, new { text = "Sửa trộm" }), 403, "MSG295");
        using var outsider = fixture.Client(s.Outsider);
        Assert.Equal(HttpStatusCode.NotFound, (await outsider.PutAsJsonAsync(url, new { text = "x" })).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await outsider.GetAsync($"/api/recordings/{id}/transcript")).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await secretary.PutAsJsonAsync($"/api/transcript-segments/{Guid.NewGuid()}", new { text = "x" })).StatusCode);
    }

    [Fact]
    public async Task A_recording_erased_while_the_worker_runs_gets_no_transcript_back()
    {
        var s = await fixture.Seed();
        await ProcessAll();
        var id = await Upload(s);
        fixture.Speech.During = async () =>
        {
            using var secretary = fixture.Client(s.Secretary);
            await AssertCode(await secretary.DeleteAsync($"/api/recordings/{id}"), 200, "MSG296");
        };
        try { await ProcessAll(); }
        finally { fixture.Speech.During = null; }
        await using var db = fixture.Db();
        Assert.False(await db.Transcripts.AnyAsync(t => t.RecordingId == id));
        Assert.Equal(RecordingStatus.Deleted, (await db.Recordings.SingleAsync(r => r.Id == id)).Status);
    }

    [Fact]
    public async Task Interrupted_runs_are_requeued_and_outages_have_their_codes()
    {
        var s = await fixture.Seed();
        await ProcessAll();
        var id = await Upload(s);
        await using (var db = fixture.Db())
            await db.Transcripts.Where(t => t.RecordingId == id).ExecuteUpdateAsync(u => u.SetProperty(t => t.Status, TranscriptStatus.Processing));
        using (var scope = fixture.Host.Services.CreateScope())
            Assert.True(await scope.ServiceProvider.GetRequiredService<ITranscriptionService>().RequeueInterruptedAsync() >= 1);
        using var secretary = fixture.Client(s.Secretary);
        Assert.Equal("Pending", (await Transcript(secretary, id)).GetProperty("status").GetString());

        fixture.Failures.FailRead = true;
        try { await AssertCode(await secretary.GetAsync($"/api/recordings/{id}/transcript"), 500, "MSG299"); }
        finally { fixture.Failures.FailRead = false; }
        await ProcessAll();
        var segment = (await Transcript(secretary, id)).GetProperty("segments")[0].GetProperty("id").GetGuid();
        fixture.Failures.FailSave = true;
        try { await AssertCode(await secretary.PutAsJsonAsync($"/api/transcript-segments/{segment}", new { text = "x" }), 503, "MSG304"); }
        finally { fixture.Failures.FailSave = false; }
    }
}
