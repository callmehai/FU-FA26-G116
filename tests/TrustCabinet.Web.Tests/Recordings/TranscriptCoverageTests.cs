using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using TrustCabinet.Application.Transcription;
using TrustCabinet.Application.VoiceId;
using TrustCabinet.Domain.Audit;
using TrustCabinet.Domain.Entities;
using TrustCabinet.Domain.Enums;
using TrustCabinet.Domain.VoiceId;

namespace TrustCabinet.Web.Tests.Recordings;

/// <summary>Round 1 gap coverage for FE-10: the transcript lifecycle, its boundaries and the callers
/// who may not see it, plus the Voice ID quality checks and limits.</summary>
public sealed class TranscriptCoverageTests(RecordingFixture fixture) : IClassFixture<RecordingFixture>
{
    private static readonly byte[] Audio = [.. "RIFF"u8, .. new byte[4096]];
    private static readonly byte[] Wav = [.. "RIFF"u8, .. new byte[2048]];

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

    private Task<HttpResponseMessage> SendClip(HttpClient client, Guid meetingId, Guid clipId)
    {
        var content = new ByteArrayContent(Wav);
        content.Headers.ContentType = MediaTypeHeaderValue.Parse("audio/wav");
        var started = fixture.Clock.Now.AddSeconds(-3).ToString("O");
        return client.PutAsync(
            $"/api/meetings/{meetingId}/speech-clips/{clipId}?startedAt={Uri.EscapeDataString(started)}&durationMs=2400",
            content);
    }

    private async Task<Guid> StoreSentence(RecordingFixture.Scenario s)
    {
        var before = fixture.Speech.Result;
        try
        {
            fixture.Speech.Failure = null;
            fixture.Speech.Result = new(2400, "vi", "gipformer-1.5-68m-int8@abc123def456", "",
                [new(0, 1200, null, "Tôi đồng ý với dự thảo.", -24.5)], []);
            using var client = fixture.Client(s.Member);
            var json = await AssertCode(await SendClip(client, s.MeetingId, Guid.NewGuid()), 200, "MSG318");
            return json.GetProperty("clip").GetProperty("id").GetGuid();
        }
        finally
        {
            fixture.Speech.Result = before;
        }
    }

    private static MultipartFormDataContent Sample(string type = "audio/webm")
    {
        var file = new ByteArrayContent([.. "OggS"u8, .. new byte[512]]);
        file.Headers.ContentType = MediaTypeHeaderValue.Parse(type);
        return new MultipartFormDataContent { { file, "file", "giong-mau.webm" } };
    }

    private Task<HttpResponseMessage> Consent(HttpClient client, string version = VoiceIdRules.NoticeVersion)
        => client.PostAsJsonAsync("/api/me/voice/consent", new { noticeVersion = version });

    [Fact]
    public async Task Calls_without_signing_in_are_refused()
    {
        // TRANS_API_IT08/16/23/28/35/42, IT53/62 (401 step), IT102/109/117/130/135/140 (401 step).
        var s = await fixture.Seed();
        fixture.Speech.Failure = null;
        var id = await Upload(s);
        await ProcessAll();
        using var anonymous = fixture.Client();
        Assert.Equal(HttpStatusCode.Unauthorized, (await anonymous.PostAsync($"/api/recordings/{id}/transcribe", null)).StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized, (await anonymous.GetAsync($"/api/recordings/{id}/transcript")).StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized, (await anonymous.GetAsync($"/api/recordings/{id}/transcript.txt")).StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized, (await anonymous.GetAsync($"/api/meetings/{s.MeetingId}/speech-clips.txt")).StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized,
            (await anonymous.PutAsJsonAsync($"/api/transcript-segments/{Guid.NewGuid()}", new { text = "x" })).StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized,
            (await anonymous.PutAsJsonAsync($"/api/speech-clips/{Guid.NewGuid()}/text", new { text = "x" })).StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized,
            (await anonymous.PutAsJsonAsync($"/api/transcript-segments/{Guid.NewGuid()}/speaker", new { label = "SPEAKER_00" })).StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized,
            (await anonymous.PutAsJsonAsync($"/api/recordings/{id}/speakers/SPEAKER_00", new { userId = s.Member })).StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized, (await anonymous.GetAsync("/api/me/voice")).StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized, (await anonymous.PostAsJsonAsync("/api/me/voice/consent", new { noticeVersion = "1.3" })).StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized, (await anonymous.DeleteAsync("/api/me/voice/consent")).StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized, (await anonymous.PostAsync("/api/me/voice/samples", Sample())).StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized, (await anonymous.GetAsync($"/api/me/voice/samples/{Guid.NewGuid()}/audio")).StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized, (await anonymous.DeleteAsync($"/api/me/voice/samples/{Guid.NewGuid()}")).StatusCode);
    }

    [Fact]
    public async Task A_deactivated_account_is_refused_while_its_token_is_still_valid()
    {
        // TRANS_API_IT09 (transcribe) and TRANS_API_IT102 (voice profile): the gate re-reads the account.
        var s = await fixture.Seed();
        fixture.Speech.Failure = null;
        var id = await Upload(s);
        await using (var db = fixture.Db())
            await db.Transcripts.Where(t => t.RecordingId == id).ExecuteDeleteAsync();
        using var secretary = fixture.Client(s.Secretary);
        await using (var db = fixture.Db())
            await db.Users.Where(u => u.Id == s.Secretary).ExecuteUpdateAsync(u => u.SetProperty(x => x.IsActive, false));
        try
        {
            var refused = await secretary.PostAsync($"/api/recordings/{id}/transcribe", null);
            Assert.Equal(HttpStatusCode.Forbidden, refused.StatusCode);
            Assert.Equal(string.Empty, await refused.Content.ReadAsStringAsync());
            Assert.Equal(HttpStatusCode.Forbidden, (await secretary.GetAsync("/api/me/voice")).StatusCode);
            await using var db = fixture.Db();
            Assert.False(await db.Transcripts.AnyAsync(t => t.RecordingId == id));
        }
        finally
        {
            await using var db = fixture.Db();
            await db.Users.Where(u => u.Id == s.Secretary).ExecuteUpdateAsync(u => u.SetProperty(x => x.IsActive, true));
        }
    }

    [Fact]
    public async Task Redoing_a_finished_transcript_replaces_corrections_and_names_and_is_audited()
    {
        // TRANS_API_IT03.
        var s = await fixture.Seed();
        fixture.Speech.Failure = null;
        var id = await Upload(s);
        await ProcessAll();
        using var secretary = fixture.Client(s.Secretary);
        var segment = (await Transcript(secretary, id)).GetProperty("segments")[0];
        await AssertCode(await secretary.PutAsJsonAsync($"/api/transcript-segments/{segment.GetProperty("id").GetGuid()}",
            new { text = "Đã sửa bởi Thư ký." }), 200, "MSG302");
        await AssertCode(await secretary.PutAsJsonAsync($"/api/recordings/{id}/speakers/SPEAKER_00",
            new { userId = s.Member }), 200, "MSG316");

        await AssertCode(await secretary.PostAsync($"/api/recordings/{id}/transcribe", null), 202, "MSG300");
        Assert.Equal("Pending", (await Transcript(secretary, id)).GetProperty("status").GetString());
        await ProcessAll();
        var done = await Transcript(secretary, id);
        Assert.Equal("Completed", done.GetProperty("status").GetString());
        // The new result replaced the old one: the correction and the name are gone.
        Assert.Equal("Kính thưa các đồng chí.", done.GetProperty("segments")[0].GetProperty("text").GetString());
        Assert.All(done.GetProperty("speakers").EnumerateArray(),
            v => Assert.Equal(JsonValueKind.Null, v.GetProperty("assignedUserId").ValueKind));
        await using var db = fixture.Db();
        Assert.Equal(1, await db.AuditLog.CountAsync(a => a.ObjectId == id && a.Action == AuditActions.TranscriptRerun));
    }

    [Fact]
    public async Task A_recording_the_caller_may_not_hear_is_not_confirmed_to_exist()
    {
        // TRANS_API_IT06: a stranger's request and a deleted recording both read as 404.
        var s = await fixture.Seed();
        fixture.Speech.Failure = null;
        var id = await Upload(s);
        using var outsider = fixture.Client(s.Outsider);
        var refused = await outsider.PostAsync($"/api/recordings/{id}/transcribe", null);
        Assert.Equal(HttpStatusCode.NotFound, refused.StatusCode);
        Assert.Equal(string.Empty, await refused.Content.ReadAsStringAsync());

        using var secretary = fixture.Client(s.Secretary);
        await AssertCode(await secretary.DeleteAsync($"/api/recordings/{id}"), 200, "MSG296");
        Assert.Equal(HttpStatusCode.NotFound,
            (await secretary.PostAsync($"/api/recordings/{id}/transcribe", null)).StatusCode);
    }

    [Fact]
    public async Task Chair_may_read_and_export_but_not_edit()
    {
        // TRANS_API_IT12.
        var s = await fixture.Seed();
        fixture.Speech.Failure = null;
        var id = await Upload(s);
        await ProcessAll();
        using var chair = fixture.Client(s.Chair);
        var view = await Transcript(chair, id);
        Assert.Equal("Completed", view.GetProperty("status").GetString());
        Assert.False(view.GetProperty("canEdit").GetBoolean());
        Assert.True(view.GetProperty("canExport").GetBoolean());

        await using (var db = fixture.Db())
        {
            var meeting = await db.Meetings.SingleAsync(m => m.Id == s.MeetingId);
            meeting.Sensitivity = Sensitivity.Secret;
            await db.SaveChangesAsync();
        }
        var secret = await Transcript(chair, id);
        Assert.False(secret.GetProperty("canEdit").GetBoolean());
        Assert.False(secret.GetProperty("canExport").GetBoolean());
    }

    [Fact]
    public async Task A_recording_not_yet_sent_has_no_transcript()
    {
        // TRANS_API_IT13.
        var s = await fixture.Seed();
        fixture.Speech.Failure = null;
        var id = await Upload(s);
        await using (var db = fixture.Db())
            await db.Transcripts.Where(t => t.RecordingId == id).ExecuteDeleteAsync();
        using var secretary = fixture.Client(s.Secretary);
        var none = await Transcript(secretary, id);
        Assert.Equal("None", none.GetProperty("status").GetString());
        Assert.Empty(none.GetProperty("segments").EnumerateArray());
        Assert.Empty(none.GetProperty("speakers").EnumerateArray());
        Assert.True(none.GetProperty("canEdit").GetBoolean());
    }

    [Fact]
    public async Task Strangers_learn_nothing_about_a_transcript_or_its_text_file()
    {
        // TRANS_API_IT15 and TRANS_API_IT22: the roster check answers 404 with no body.
        var s = await fixture.Seed();
        fixture.Speech.Failure = null;
        var id = await Upload(s);
        await ProcessAll();
        using var outsider = fixture.Client(s.Outsider);
        foreach (var response in new[]
        {
            await outsider.GetAsync($"/api/recordings/{id}/transcript"),
            await outsider.GetAsync($"/api/recordings/{id}/transcript.txt"),
            await outsider.GetAsync($"/api/recordings/{Guid.NewGuid()}/transcript"),
        })
        {
            Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
            Assert.Equal(string.Empty, await response.Content.ReadAsStringAsync());
        }
    }

    [Fact]
    public async Task Chair_saves_the_finished_transcript_as_text()
    {
        // TRANS_API_IT18.
        var s = await fixture.Seed();
        fixture.Speech.Failure = null;
        var id = await Upload(s);
        await ProcessAll();
        using var chair = fixture.Client(s.Chair);
        var file = await chair.GetAsync($"/api/recordings/{id}/transcript.txt");
        Assert.Equal(HttpStatusCode.OK, file.StatusCode);
        Assert.Equal("text/plain", file.Content.Headers.ContentType?.MediaType);
        var text = await file.Content.ReadAsStringAsync();
        Assert.Contains("Kính thưa các đồng chí.", text);
        using var secretary = fixture.Client(s.Secretary);
        Assert.Equal(text, await (await secretary.GetAsync($"/api/recordings/{id}/transcript.txt")).Content.ReadAsStringAsync());
    }

    [Fact]
    public async Task Strangers_learn_nothing_about_the_per_mic_text_file()
    {
        // TRANS_API_IT27.
        var s = await fixture.Seed(mode: MeetingMode.Hybrid);
        await StoreSentence(s);
        using var outsider = fixture.Client(s.Outsider);
        var refused = await outsider.GetAsync($"/api/meetings/{s.MeetingId}/speech-clips.txt");
        Assert.Equal(HttpStatusCode.NotFound, refused.StatusCode);
        Assert.Equal(string.Empty, await refused.Content.ReadAsStringAsync());
    }

    [Fact]
    public async Task Segment_text_boundary_is_8000_characters()
    {
        // TRANS_API_IT31 and TRANS_API_IT32.
        var s = await fixture.Seed();
        fixture.Speech.Failure = null;
        var id = await Upload(s);
        await ProcessAll();
        using var secretary = fixture.Client(s.Secretary);
        var segment = (await Transcript(secretary, id)).GetProperty("segments")[0];
        var url = $"/api/transcript-segments/{segment.GetProperty("id").GetGuid()}";
        var full = new string('a', 8000);
        await AssertCode(await secretary.PutAsJsonAsync(url, new { text = full }), 200, "MSG302");
        Assert.Equal(full, (await Transcript(secretary, id)).GetProperty("segments")[0].GetProperty("text").GetString());

        var tooLong = await AssertCode(await secretary.PutAsJsonAsync(url, new { text = full + "a" }), 400, "MSG303");
        Assert.True(tooLong.GetProperty("errors").TryGetProperty("text", out _));
        Assert.Equal(full, (await Transcript(secretary, id)).GetProperty("segments")[0].GetProperty("text").GetString());
    }

    [Fact]
    public async Task Strangers_and_unknown_sentences_learn_nothing_about_corrections()
    {
        // TRANS_API_IT41: an unknown sentence and a caller outside the roster both read as 404.
        var s = await fixture.Seed(mode: MeetingMode.Hybrid);
        var clip = await StoreSentence(s);
        using var secretary = fixture.Client(s.Secretary);
        var unknown = await secretary.PutAsJsonAsync($"/api/speech-clips/{Guid.NewGuid()}/text", new { text = "Sửa" });
        Assert.Equal(HttpStatusCode.NotFound, unknown.StatusCode);
        Assert.Equal(string.Empty, await unknown.Content.ReadAsStringAsync());
        using var outsider = fixture.Client(s.Outsider);
        var refused = await outsider.PutAsJsonAsync($"/api/speech-clips/{clip}/text", new { text = "Sửa" });
        Assert.Equal(HttpStatusCode.NotFound, refused.StatusCode);
        Assert.Equal(string.Empty, await refused.Content.ReadAsStringAsync());
    }

    [Fact]
    public async Task Speech_clip_text_boundary_is_8000_characters()
    {
        // TRANS_API_IT38.
        var s = await fixture.Seed(mode: MeetingMode.Hybrid);
        var clip = await StoreSentence(s);
        using var secretary = fixture.Client(s.Secretary);
        var url = $"/api/speech-clips/{clip}/text";
        var full = new string('b', 8000);
        await AssertCode(await secretary.PutAsJsonAsync(url, new { text = full }), 200, "MSG302");
        Assert.Contains(full, await (await secretary.GetAsync($"/api/meetings/{s.MeetingId}/speech-clips.txt")).Content.ReadAsStringAsync());

        var tooLong = await AssertCode(await secretary.PutAsJsonAsync(url, new { text = full + "b" }), 400, "MSG303");
        Assert.True(tooLong.GetProperty("errors").TryGetProperty("text", out _));
        Assert.Contains(full, await (await secretary.GetAsync($"/api/meetings/{s.MeetingId}/speech-clips.txt")).Content.ReadAsStringAsync());
    }

    [Fact]
    public async Task Moving_a_line_to_its_own_voice_writes_nothing()
    {
        // TRANS_API_IT47.
        var s = await fixture.Seed();
        fixture.Speech.Failure = null;
        var id = await Upload(s);
        await ProcessAll();
        using var secretary = fixture.Client(s.Secretary);
        var before = await Transcript(secretary, id);
        var line = before.GetProperty("segments")[0];
        var label = line.GetProperty("speakerLabel").GetString()!;
        var speechMs = before.GetProperty("speakers").EnumerateArray()
            .Single(v => v.GetProperty("label").GetString() == label).GetProperty("speechMs").GetInt64();

        var kept = await AssertCode(await secretary.PutAsJsonAsync($"/api/transcript-segments/{line.GetProperty("id").GetGuid()}/speaker",
            new { label }), 200, "MSG407");
        Assert.Equal(label, kept.GetProperty("segment").GetProperty("speakerLabel").GetString());
        Assert.Equal(label, kept.GetProperty("segment").GetProperty("previousLabel").GetString());
        Assert.False(kept.GetProperty("segment").GetProperty("newVoice").GetBoolean());

        var after = await Transcript(secretary, id);
        Assert.Equal(speechMs, after.GetProperty("speakers").EnumerateArray()
            .Single(v => v.GetProperty("label").GetString() == label).GetProperty("speechMs").GetInt64());
        await using var db = fixture.Db();
        Assert.Equal(0, await db.AuditLog.CountAsync(a => a.ObjectId == id
            && a.Action == AuditActions.TranscriptSegmentSpeakerAssigned));
    }

    [Fact]
    public async Task A_hundredth_voice_is_the_last_one()
    {
        // TRANS_API_IT50: SPEAKER_00 to SPEAKER_99 taken leaves no free label.
        var s = await fixture.Seed();
        fixture.Speech.Failure = null;
        var id = await Upload(s);
        await ProcessAll();
        await using var db = fixture.Db();
        var transcriptId = await db.Transcripts.Where(t => t.RecordingId == id).Select(t => t.Id).SingleAsync();
        db.TranscriptSegments.AddRange(Enumerable.Range(2, 98).Select(i => new TranscriptSegment
        {
            TranscriptId = transcriptId, Position = i + 1, StartMs = i * 10000L, EndMs = i * 10000L + 5000,
            Text = $"Câu {i}.", SpeakerLabel = $"SPEAKER_{i:00}",
        }));
        await db.SaveChangesAsync();

        using var secretary = fixture.Client(s.Secretary);
        await AssertCode(await secretary.PutAsJsonAsync(
            $"/api/transcript-segments/{(await Transcript(secretary, id)).GetProperty("segments")[0].GetProperty("id").GetGuid()}/speaker",
            new { userId = s.Member }), 400, "MSG317");
        Assert.Equal(2, await db.TranscriptSpeakers.CountAsync(v => v.Transcript.RecordingId == id));
    }

    [Fact]
    public async Task Unknown_voices_learn_nothing_and_change_nothing()
    {
        // TRANS_API_IT51 (a label that is not a voice of the recording) and TRANS_API_IT60
        // (a label the recording does not have): 404 with no body, the line and the voices unchanged.
        var s = await fixture.Seed();
        fixture.Speech.Failure = null;
        var id = await Upload(s);
        await ProcessAll();
        using var secretary = fixture.Client(s.Secretary);
        var line = (await Transcript(secretary, id)).GetProperty("segments")[0];
        var lineId = line.GetProperty("id").GetGuid();
        var label = line.GetProperty("speakerLabel").GetString();

        var moved = await secretary.PutAsJsonAsync($"/api/transcript-segments/{lineId}/speaker", new { label = "SPEAKER_77" });
        Assert.Equal(HttpStatusCode.NotFound, moved.StatusCode);
        Assert.Equal(string.Empty, await moved.Content.ReadAsStringAsync());
        Assert.Equal(label, (await Transcript(secretary, id)).GetProperty("segments")[0].GetProperty("speakerLabel").GetString());

        var named = await secretary.PutAsJsonAsync($"/api/recordings/{id}/speakers/SPEAKER_77", new { userId = s.Chair });
        Assert.Equal(HttpStatusCode.NotFound, named.StatusCode);
        Assert.Equal(string.Empty, await named.Content.ReadAsStringAsync());
        await using var db = fixture.Db();
        Assert.Equal(2, await db.TranscriptSpeakers.CountAsync(v => v.Transcript.RecordingId == id));
    }

    [Fact]
    public async Task Clearing_a_voice_name_takes_it_back_to_speaker_numbers()
    {
        // TRANS_API_IT57.
        var s = await fixture.Seed();
        fixture.Speech.Failure = null;
        var id = await Upload(s);
        await ProcessAll();
        using var secretary = fixture.Client(s.Secretary);
        await AssertCode(await secretary.PutAsJsonAsync($"/api/recordings/{id}/speakers/SPEAKER_00",
            new { userId = s.Chair }), 200, "MSG316");

        var cleared = await AssertCode(await secretary.PutAsJsonAsync($"/api/recordings/{id}/speakers/SPEAKER_00",
            new { userId = (Guid?)null, guestId = (Guid?)null }), 200, "MSG316");
        Assert.Equal(JsonValueKind.Null, cleared.GetProperty("speaker").GetProperty("assignedUserId").ValueKind);
        Assert.Equal(JsonValueKind.Null, cleared.GetProperty("speaker").GetProperty("assignedGuestId").ValueKind);
        Assert.Contains("Người nói 1:",
            await (await secretary.GetAsync($"/api/recordings/{id}/transcript.txt")).Content.ReadAsStringAsync());
    }

    [Fact]
    public async Task Strangers_learn_nothing_about_naming_voices()
    {
        // TRANS_API_IT62 (404 step).
        var s = await fixture.Seed();
        fixture.Speech.Failure = null;
        var id = await Upload(s);
        await ProcessAll();
        using var outsider = fixture.Client(s.Outsider);
        Assert.Equal(HttpStatusCode.NotFound,
            (await outsider.PutAsJsonAsync($"/api/recordings/{id}/speakers/SPEAKER_00", new { userId = s.Member })).StatusCode);
    }

    [Fact]
    public async Task Withdrawing_nothing_answers_200_and_changes_nothing()
    {
        // TRANS_API_IT112.
        var s = await fixture.Seed();
        using var client = fixture.Client(s.Member);
        var withdrawn = await AssertCode(await client.DeleteAsync("/api/me/voice/consent"), 200, "MSG306");
        Assert.Equal(JsonValueKind.Null, withdrawn.GetProperty("data").GetProperty("consent").ValueKind);
        await using var db = fixture.Db();
        Assert.Equal(0, await db.AuditLog.CountAsync(a => a.ActorId == s.Member && a.Action == AuditActions.VoiceConsentWithdrawn));
    }

    [Fact]
    public async Task Sample_quality_checks_reject_short_quiet_and_oversize_audio()
    {
        // TRANS_API_IT124 and TRANS_API_IT125.
        var s = await fixture.Seed();
        using var client = fixture.Client(s.Member);
        await AssertCode(await Consent(client), 200, "MSG305");
        try
        {
            var tooMany = new Dictionary<string, string>
            {
                ["multiple_speakers"] = "Nghe như có hơn một người nói — hãy ghi ở nơi yên tĩnh, chỉ mình bạn nói.",
                ["unreadable_audio"] = "Không đọc được tệp âm thanh.",
                ["too_long"] = "Giọng mẫu quá dài — tối đa 60 giây.",
            };
            foreach (var (reason, message) in tooMany)
            {
                fixture.Speech.EmbedFailure = new VoiceSampleRejectedException(reason);
                var rejected = await AssertCode(await client.PostAsync("/api/me/voice/samples", Sample()), 422, "MSG309");
                Assert.Equal(reason, rejected.GetProperty("reason").GetString());
                Assert.Equal(message, rejected.GetProperty("message").GetString());
            }
            fixture.Speech.EmbedFailure = new SpeechEngineException("Không kết nối được máy chủ nhận dạng giọng nói.");

            // Over the 10 MB file limit the endpoint refuses before the worker is reached.
            var calls = fixture.Speech.Calls;
            var big = new ByteArrayContent(new byte[10 * 1024 * 1024 + 128 * 1024]);
            big.Headers.ContentType = MediaTypeHeaderValue.Parse("audio/wav");
            var oversize = await AssertCode(
                await client.PostAsync("/api/me/voice/samples", new MultipartFormDataContent { { big, "file", "lon.wav" } }),
                422, "MSG309");
            Assert.Equal("too_long", oversize.GetProperty("reason").GetString());
            Assert.Equal(calls, fixture.Speech.Calls);
        }
        finally
        {
            fixture.Speech.EmbedFailure = null;
        }
        await using var db = fixture.Db();
        Assert.Equal(0, await db.VoiceSamples.CountAsync(x => x.UserId == s.Member));
    }

    [Fact]
    public async Task Unsupported_audio_and_missing_files_are_refused_before_the_worker()
    {
        // TRANS_API_IT126 and TRANS_API_IT127.
        var s = await fixture.Seed();
        using var client = fixture.Client(s.Member);
        await AssertCode(await Consent(client), 200, "MSG305");
        var unsupported = await AssertCode(await client.PostAsync("/api/me/voice/samples", Sample("application/pdf")), 415, "MSG292");
        Assert.Equal("Định dạng âm thanh không được hỗ trợ. Dùng mp3, m4a, wav, webm hoặc ogg.",
            unsupported.GetProperty("message").GetString());
        var notForm = await AssertCode(await client.PostAsync("/api/me/voice/samples",
            JsonContent.Create(new { })), 400, "MSG313");
        Assert.Equal("Yêu cầu phải gửi dạng multipart/form-data có tệp giọng mẫu.", notForm.GetProperty("message").GetString());
        var form = new MultipartFormDataContent { { new StringContent("giong-cua-toi"), "note" } };
        var empty = await AssertCode(await client.PostAsync("/api/me/voice/samples", form), 400, "MSG313");
        Assert.Equal("Chưa có tệp giọng mẫu.", empty.GetProperty("message").GetString());
        await using var db = fixture.Db();
        Assert.Equal(0, await db.VoiceSamples.CountAsync(x => x.UserId == s.Member));
    }

    [Fact]
    public async Task Own_samples_are_refused_when_the_worker_is_down()
    {
        // TRANS_API_IT128: nothing is stored.
        var s = await fixture.Seed();
        using var client = fixture.Client(s.Member);
        await AssertCode(await Consent(client), 200, "MSG305");
        fixture.Speech.EmbedFailure = new SpeechEngineException("Không kết nối được máy chủ nhận dạng giọng nói.");
        try
        {
            await AssertCode(await client.PostAsync("/api/me/voice/samples", Sample()), 503, "MSG312");
        }
        finally
        {
            fixture.Speech.EmbedFailure = null;
        }
        await using var db = fixture.Db();
        Assert.Equal(0, await db.VoiceSamples.CountAsync(x => x.UserId == s.Member));
    }

    [Fact]
    public async Task Sample_audio_of_unknown_ids_learns_nothing()
    {
        // TRANS_API_IT135 (404 step).
        var s = await fixture.Seed();
        using var client = fixture.Client(s.Member);
        Assert.Equal(HttpStatusCode.NotFound,
            (await client.GetAsync($"/api/me/voice/samples/{Guid.NewGuid()}/audio")).StatusCode);
    }

    [Fact]
    public async Task Deleting_a_sample_frees_a_place_under_the_limit()
    {
        // TRANS_API_IT137.
        var s = await fixture.Seed();
        using var client = fixture.Client(s.Member);
        await AssertCode(await Consent(client), 200, "MSG305");
        fixture.Speech.NextEmbedding = [1f, 0f, 0f];
        var first = Guid.Empty;
        for (var i = 0; i < VoiceIdRules.MaxSamples; i++)
        {
            var added = await AssertCode(await client.PostAsync("/api/me/voice/samples", Sample()), 201, "MSG307");
            if (i == 0) first = added.GetProperty("data").GetProperty("id").GetGuid();
        }
        await AssertCode(await client.PostAsync("/api/me/voice/samples", Sample()), 409, "MSG310");
        await AssertCode(await client.DeleteAsync($"/api/me/voice/samples/{first}"), 200, "MSG311");
        await AssertCode(await client.PostAsync("/api/me/voice/samples", Sample()), 201, "MSG307");
    }

    [Fact]
    public async Task Proxy_samples_pass_the_same_quality_checks()
    {
        // TRANS_API_IT152.
        var s = await fixture.Seed(MeetingStatus.Ended);
        await VoiceCollection.MakeCollectors(fixture, s.Outsider);
        await VoiceCollection.Agree(fixture, s.Member);
        using var secretary = VoiceCollection.Client(fixture, s.Outsider, Capability.VoiceCollector);
        try
        {
            fixture.Speech.EmbedFailure = new VoiceSampleRejectedException("too_short");
            var short_ = await AssertCode(
                await secretary.PostAsync($"{VoiceCollection.Url(s.Member)}/samples", VoiceCollection.Sample()),
                422, "MSG309");
            Assert.Equal("too_short", short_.GetProperty("reason").GetString());
            fixture.Speech.EmbedFailure = null;
            var unsupported = await AssertCode(await secretary.PostAsync($"{VoiceCollection.Url(s.Member)}/samples",
                VoiceCollection.Sample("application/pdf")), 415, "MSG292");
            Assert.Equal("Định dạng âm thanh không được hỗ trợ. Dùng mp3, m4a, wav, webm hoặc ogg.",
                unsupported.GetProperty("message").GetString());
            var missing = await AssertCode(await secretary.PostAsync($"{VoiceCollection.Url(s.Member)}/samples",
                new MultipartFormDataContent { { new StringContent("thu-ho"), "note" } }), 400, "MSG313");
            Assert.Equal("Chưa có tệp giọng mẫu.", missing.GetProperty("message").GetString());
        }
        finally
        {
            fixture.Speech.EmbedFailure = null;
        }
        await using var db = fixture.Db();
        Assert.Equal(0, await db.VoiceSamples.CountAsync(x => x.UserId == s.Member));
    }

    [Fact]
    public async Task Proxy_samples_are_refused_when_the_worker_is_down()
    {
        // TRANS_API_IT153: nothing is stored and nobody is told.
        var s = await fixture.Seed(MeetingStatus.Ended);
        await VoiceCollection.MakeCollectors(fixture, s.Outsider);
        await VoiceCollection.Agree(fixture, s.Member);
        await using var db = fixture.Db();
        var before = await db.Notifications.CountAsync(n => n.UserId == s.Member && n.Type == NotificationType.VoiceSampleCollected);
        fixture.Speech.EmbedFailure = new SpeechEngineException("Không kết nối được máy chủ nhận dạng giọng nói.");
        try
        {
            using var secretary = VoiceCollection.Client(fixture, s.Outsider, Capability.VoiceCollector);
            await AssertCode(await secretary.PostAsync($"{VoiceCollection.Url(s.Member)}/samples",
                VoiceCollection.Sample()), 503, "MSG312");
        }
        finally
        {
            fixture.Speech.EmbedFailure = null;
        }
        Assert.Equal(0, await db.VoiceSamples.CountAsync(x => x.UserId == s.Member));
        Assert.Equal(before, await db.Notifications.CountAsync(n => n.UserId == s.Member && n.Type == NotificationType.VoiceSampleCollected));
    }
}
