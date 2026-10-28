using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using TrustCabinet.Domain.Entities;
using TrustCabinet.Domain.Enums;
using TrustCabinet.Domain.Recordings;
using TrustCabinet.Domain.Transcription;

namespace TrustCabinet.Web.Tests.Recordings;

/// <summary>SCRUM-109 — sentences from each attendee's own mic, and the capture mode chosen before the meeting starts.</summary>
public sealed class SpeechClipEndpointTests(RecordingFixture fixture) : IClassFixture<RecordingFixture>
{
    private static readonly byte[] Wav = [.. "RIFF"u8, .. new byte[2048]];

    private static readonly SpeechTranscription OneSentence = new(2400, "vi", "gipformer-1.5-68m-int8@abc123def456", "",
        [new(0, 1200, null, "Tôi đồng ý với dự thảo.", -24.5), new(1300, 2400, null, "Đề nghị biểu quyết.", -25.0)], []);

    /// <summary>The same words as heard by a neighbour's laptop: 14 dB quieter.</summary>
    private static readonly SpeechTranscription Overheard = new(2400, "vi", "gipformer-1.5-68m-int8@abc123def456", "",
        [new(0, 1200, null, "tôi đồng ý với dự thảo", -38.5), new(1300, 2400, null, "đề nghị biểu quyết", -39.0)], []);

    private static readonly SpeechTranscription Silence = new(2400, "vi", "gipformer-1.5-68m-int8@abc123def456", "", [], []);

    private static async Task<JsonElement> AssertCode(HttpResponseMessage response, int status, string code)
    {
        var raw = await response.Content.ReadAsStringAsync();
        Assert.True((int)response.StatusCode == status, $"Expected {status}, got {(int)response.StatusCode}: {raw}");
        var json = JsonDocument.Parse(raw).RootElement.Clone();
        Assert.Equal(code, json.GetProperty("code").GetString());
        return json;
    }

    private Task<HttpResponseMessage> Send(HttpClient client, Guid meetingId, Guid clipId, byte[]? audio = null,
        string type = "audio/wav", string? startedAt = null, string duration = "2400")
    {
        var content = new ByteArrayContent(audio ?? Wav);
        content.Headers.ContentType = MediaTypeHeaderValue.Parse(type);
        var started = startedAt ?? fixture.Clock.Now.AddSeconds(-3).ToString("O");
        return client.PutAsync($"/api/meetings/{meetingId}/speech-clips/{clipId}?startedAt={Uri.EscapeDataString(started)}&durationMs={duration}",
            content);
    }

    private static Task<JsonElement> List(HttpClient client, Guid meetingId, string query = "")
        => client.GetFromJsonAsync<JsonElement>($"/api/meetings/{meetingId}/speech-clips{query}");

    private async Task<Guid> StoreSentence(RecordingFixture.Scenario s, Guid speaker)
    {
        fixture.Speech.Failure = null;
        fixture.Speech.Result = OneSentence;
        using var client = fixture.Client(speaker);
        var json = await AssertCode(await Send(client, s.MeetingId, Guid.NewGuid()), 200, "MSG318");
        return json.GetProperty("clip").GetProperty("id").GetGuid();
    }

    [Fact]
    public async Task A_sentence_from_my_mic_is_transcribed_without_diarization_and_named_after_me_once()
    {
        var s = await fixture.Seed();
        fixture.Speech.Failure = null;
        fixture.Speech.Result = OneSentence;
        var calls = fixture.Speech.Calls;
        using var member = fixture.Client(s.Member);
        var clientId = Guid.NewGuid();

        var saved = await AssertCode(await Send(member, s.MeetingId, clientId), 200, "MSG318");
        var clip = saved.GetProperty("clip");
        Assert.Equal(s.Member, clip.GetProperty("speakerId").GetGuid());
        Assert.Equal("Tôi đồng ý với dự thảo. Đề nghị biểu quyết.", clip.GetProperty("text").GetString());
        Assert.False(fixture.Speech.LastDiarize);
        Assert.True(fixture.Speech.LastClip);
        Assert.Equal(Wav, fixture.Speech.LastAudio);

        // The response was lost and the browser sent it again: one line, and the worker is not asked twice.
        var again = await AssertCode(await Send(member, s.MeetingId, clientId), 200, "MSG318");
        Assert.Equal(clip.GetProperty("id").GetGuid(), again.GetProperty("clip").GetProperty("id").GetGuid());
        Assert.Equal(calls + 1, fixture.Speech.Calls);

        using var chair = fixture.Client(s.Chair);
        var list = await List(chair, s.MeetingId);
        Assert.Equal("PerMic", list.GetProperty("captureMode").GetString());
        Assert.True(list.GetProperty("canCapture").GetBoolean());
        Assert.False(list.GetProperty("canEdit").GetBoolean());
        var line = Assert.Single(list.GetProperty("items").EnumerateArray());
        Assert.Equal("Recording test 2", line.GetProperty("speakerName").GetString());
        Assert.False(line.GetProperty("canDelete").GetBoolean());

        // The live view asks only for what changed since its last read.
        var since = Uri.EscapeDataString(list.GetProperty("readAt").GetDateTimeOffset().ToString("O"));
        Assert.Empty((await List(chair, s.MeetingId, $"?since={since}")).GetProperty("items").EnumerateArray());

        // No audio is kept anywhere: the row holds words, time, speaker and the loudness of each stretch only.
        await using var db = fixture.Db();
        var row = await db.SpeechClips.SingleAsync(c => c.MeetingId == s.MeetingId);
        Assert.Equal([new(0, 1200, 6, -24.5), new(1300, 2400, 4, -25.0)], row.Segments);
        Assert.Equal(0, fixture.StoredFileCount());
    }

    [Fact]
    public async Task Silence_stores_nothing()
    {
        var s = await fixture.Seed();
        fixture.Speech.Failure = null;
        fixture.Speech.Result = Silence;
        using var member = fixture.Client(s.Member);
        Assert.Equal(HttpStatusCode.NoContent, (await Send(member, s.MeetingId, Guid.NewGuid())).StatusCode);
        Assert.Empty((await List(member, s.MeetingId)).GetProperty("items").EnumerateArray());
    }

    [Fact]
    public async Task Sentences_are_taken_only_from_people_of_a_running_per_mic_meeting()
    {
        var room = await fixture.Seed(capture: CaptureMode.Room);
        using (var member = fixture.Client(room.Member))
        {
            await AssertCode(await Send(member, room.MeetingId, Guid.NewGuid()), 409, "MSG320");
            Assert.False((await List(member, room.MeetingId)).GetProperty("canCapture").GetBoolean());
        }

        var published = await fixture.Seed(MeetingStatus.Published);
        using (var member = fixture.Client(published.Member))
            await AssertCode(await Send(member, published.MeetingId, Guid.NewGuid()), 409, "MSG320");

        var s = await fixture.Seed();
        using var outsider = fixture.Client(s.Outsider);
        Assert.Equal(HttpStatusCode.NotFound, (await Send(outsider, s.MeetingId, Guid.NewGuid())).StatusCode);
        // The organizer may read the meeting but is not on its roster: no one's words are theirs to read.
        using var organizer = fixture.Client(s.Organizer);
        Assert.Equal(HttpStatusCode.NotFound, (await organizer.GetAsync($"/api/meetings/{s.MeetingId}/speech-clips")).StatusCode);
    }

    [Fact]
    public async Task A_malformed_or_oversized_sentence_is_refused_before_the_worker_hears_it()
    {
        var s = await fixture.Seed();
        fixture.Speech.Failure = null;
        var calls = fixture.Speech.Calls;
        using var member = fixture.Client(s.Member);
        await AssertCode(await Send(member, s.MeetingId, Guid.NewGuid(), type: "text/plain"), 400, "MSG319");
        await AssertCode(await Send(member, s.MeetingId, Guid.NewGuid(), startedAt: "hôm qua"), 400, "MSG319");
        await AssertCode(await Send(member, s.MeetingId, Guid.NewGuid(), duration: "0"), 400, "MSG319");
        await AssertCode(await Send(member, s.MeetingId, Guid.NewGuid(), new byte[3 * 1024 * 1024 + 1]), 413, "MSG321");
        Assert.Equal(calls, fixture.Speech.Calls);
    }

    [Fact]
    public async Task A_worker_outage_keeps_nothing_so_the_browser_sends_it_again()
    {
        var s = await fixture.Seed();
        fixture.Speech.Failure = new SpeechEngineException("Không kết nối được máy chủ nhận dạng giọng nói.");
        try
        {
            using var member = fixture.Client(s.Member);
            await AssertCode(await Send(member, s.MeetingId, Guid.NewGuid()), 503, "MSG312");
            Assert.Empty((await List(member, s.MeetingId)).GetProperty("items").EnumerateArray());
        }
        finally
        {
            fixture.Speech.Failure = null;
        }
    }

    [Fact]
    public async Task The_secretary_corrects_a_sentence_and_nobody_else_can()
    {
        var s = await fixture.Seed();
        var clip = await StoreSentence(s, s.Member);
        using var member = fixture.Client(s.Member);
        await AssertCode(await member.PutAsJsonAsync($"/api/speech-clips/{clip}/text", new { text = "Sửa" }), 403, "MSG295");
        using var secretary = fixture.Client(s.Secretary);
        await AssertCode(await secretary.PutAsJsonAsync($"/api/speech-clips/{clip}/text", new { text = "  " }), 400, "MSG303");
        await AssertCode(await secretary.PutAsJsonAsync($"/api/speech-clips/{clip}/text",
            new { text = "Tôi đồng ý với dự thảo, đề nghị biểu quyết." }), 200, "MSG302");

        var line = Assert.Single((await List(secretary, s.MeetingId)).GetProperty("items").EnumerateArray());
        Assert.Equal("Tôi đồng ý với dự thảo, đề nghị biểu quyết.", line.GetProperty("text").GetString());
        Assert.Equal("Recording test 0", line.GetProperty("editedByName").GetString());

        fixture.Failures.FailSave = true;
        try
        {
            await AssertCode(await secretary.PutAsJsonAsync($"/api/speech-clips/{clip}/text", new { text = "Khác" }), 503, "MSG304");
        }
        finally
        {
            fixture.Failures.FailSave = false;
        }
    }

    [Fact]
    public async Task The_speaker_or_the_secretary_takes_a_sentence_back()
    {
        var s = await fixture.Seed();
        var mine = await StoreSentence(s, s.Member);
        var other = await StoreSentence(s, s.Member);
        using var chair = fixture.Client(s.Chair);
        await AssertCode(await chair.DeleteAsync($"/api/speech-clips/{mine}"), 403, "MSG323");
        using var member = fixture.Client(s.Member);
        await AssertCode(await member.DeleteAsync($"/api/speech-clips/{mine}"), 200, "MSG322");
        using var secretary = fixture.Client(s.Secretary);
        await AssertCode(await secretary.DeleteAsync($"/api/speech-clips/{other}"), 200, "MSG322");
        Assert.Empty((await List(member, s.MeetingId)).GetProperty("items").EnumerateArray());
        Assert.Equal(HttpStatusCode.NotFound, (await member.DeleteAsync($"/api/speech-clips/{mine}")).StatusCode);
    }

    [Fact]
    public async Task The_chair_or_a_secretary_picks_the_capture_mode_until_the_meeting_starts()
    {
        var s = await fixture.Seed(MeetingStatus.Published);
        using var secretary = fixture.Client(s.Secretary);
        Assert.True((await List(secretary, s.MeetingId)).GetProperty("canChangeMode").GetBoolean());
        var changed = await AssertCode(await secretary.PutAsJsonAsync($"/api/meetings/{s.MeetingId}/capture-mode", new { mode = "Room" }),
            200, "MSG324");
        Assert.Equal("Room", changed.GetProperty("captureMode").GetString());
        await using (var db = fixture.Db())
            Assert.Equal(CaptureMode.Room, (await db.Meetings.SingleAsync(m => m.Id == s.MeetingId)).CaptureMode);

        using var chair = fixture.Client(s.Chair);
        await AssertCode(await chair.PutAsJsonAsync($"/api/meetings/{s.MeetingId}/capture-mode", new { mode = "PerMic" }), 200, "MSG324");
        using var member = fixture.Client(s.Member);
        await AssertCode(await member.PutAsJsonAsync($"/api/meetings/{s.MeetingId}/capture-mode", new { mode = "Room" }), 403, "MSG327");
        await AssertCode(await chair.PutAsJsonAsync($"/api/meetings/{s.MeetingId}/capture-mode", new { mode = "Mic" }), 400, "MSG326");

        var running = await fixture.Seed();
        using var runningChair = fixture.Client(running.Chair);
        await AssertCode(await runningChair.PutAsJsonAsync($"/api/meetings/{running.MeetingId}/capture-mode", new { mode = "Room" }),
            409, "MSG325");
        Assert.False((await List(runningChair, running.MeetingId)).GetProperty("canChangeMode").GetBoolean());

        var cancelled = await fixture.Seed(MeetingStatus.Cancelled);
        using var cancelledChair = fixture.Client(cancelled.Chair);
        await AssertCode(await cancelledChair.PutAsJsonAsync($"/api/meetings/{cancelled.MeetingId}/capture-mode", new { mode = "Room" }),
            409, "MSG349");
        Assert.False((await List(cancelledChair, cancelled.MeetingId)).GetProperty("canChangeMode").GetBoolean());
    }

    [Fact]
    public async Task A_database_outage_on_read_says_so()
    {
        var s = await fixture.Seed();
        using var member = fixture.Client(s.Member);
        fixture.Failures.FailClipRead = true;
        try
        {
            await AssertCode(await member.GetAsync($"/api/meetings/{s.MeetingId}/speech-clips"), 500, "MSG299");
        }
        finally
        {
            fixture.Failures.FailClipRead = false;
        }
    }

    private async Task<Guid> StoreAt(RecordingFixture.Scenario s, Guid speaker, SpeechTranscription heard, DateTimeOffset at)
    {
        fixture.Speech.Failure = null;
        fixture.Speech.Result = heard;
        using var client = fixture.Client(speaker);
        var json = await AssertCode(await Send(client, s.MeetingId, Guid.NewGuid(), startedAt: at.ToString("O")), 200, "MSG318");
        return json.GetProperty("clip").GetProperty("id").GetGuid();
    }

    [Fact]
    public async Task A_quiet_copy_of_a_neighbours_sentence_is_marked_for_everyone_and_kept()
    {
        var s = await fixture.Seed();
        var at = fixture.Clock.Now.AddSeconds(-10);
        // The neighbour's laptop sends first; its clock is 300 ms ahead.
        var copy = await StoreAt(s, s.Member, Overheard, at.AddMilliseconds(300));
        using var member = fixture.Client(s.Member);
        var before = await List(member, s.MeetingId);
        Assert.Equal(JsonValueKind.Null, Assert.Single(before.GetProperty("items").EnumerateArray()).GetProperty("leak").ValueKind);

        fixture.Clock.Now = fixture.Clock.Now.AddSeconds(1);
        var source = await StoreAt(s, s.Chair, OneSentence, at);
        // The live view asks only for what changed, and still learns that its older line is now a copy.
        var since = Uri.EscapeDataString(before.GetProperty("readAt").GetDateTimeOffset().ToString("O"));
        var changed = (await List(member, s.MeetingId, $"?since={since}")).GetProperty("items").EnumerateArray().ToList();
        var ids = changed.Select(c => c.GetProperty("id").GetGuid()).ToList();
        Assert.Contains(copy, ids);
        Assert.Contains(source, ids);
        var marked = changed.Single(c => c.GetProperty("id").GetGuid() == copy).GetProperty("leak");
        Assert.Equal(source, marked.GetProperty("clipId").GetGuid());
        Assert.Equal("Recording test 1", marked.GetProperty("speakerName").GetString());
        Assert.True(marked.GetProperty("whole").GetBoolean());
        Assert.Equal(JsonValueKind.Null, changed.Single(c => c.GetProperty("id").GetGuid() == source).GetProperty("leak").ValueKind);

        // Marked, not hidden: the words are still there for everyone.
        Assert.Equal(2, (await List(member, s.MeetingId)).GetProperty("items").GetArrayLength());
    }

    [Fact]
    public async Task The_chair_or_a_secretary_takes_a_sentence_out_and_puts_it_back()
    {
        var s = await fixture.Seed();
        var clip = await StoreSentence(s, s.Member);
        string url = $"/api/speech-clips/{clip}/exclusion";
        using var member = fixture.Client(s.Member);
        Assert.False((await List(member, s.MeetingId)).GetProperty("canExclude").GetBoolean());
        await AssertCode(await member.PutAsJsonAsync(url, new { excluded = true, reason = "Leak" }), 403, "MSG353");
        using var chair = fixture.Client(s.Chair);
        Assert.True((await List(chair, s.MeetingId)).GetProperty("canExclude").GetBoolean());
        await AssertCode(await chair.PutAsJsonAsync(url, new { excluded = true, reason = "Rò rỉ" }), 400, "MSG354");
        await AssertCode(await chair.PutAsJsonAsync(url, new { excluded = true, reason = "Leak" }), 200, "MSG351");
        // A second tap changes and audits nothing.
        await AssertCode(await chair.PutAsJsonAsync(url, new { excluded = true, reason = "Leak" }), 200, "MSG351");

        var officerLine = Assert.Single((await List(chair, s.MeetingId)).GetProperty("items").EnumerateArray());
        Assert.True(officerLine.GetProperty("excluded").GetBoolean());
        Assert.Equal("Leak", officerLine.GetProperty("exclusionReason").GetString());
        Assert.Equal("Recording test 1", officerLine.GetProperty("excludedByName").GetString());
        Assert.Equal("Tôi đồng ý với dự thảo. Đề nghị biểu quyết.", officerLine.GetProperty("text").GetString());
        // Everyone else sees that a line was there, not its words.
        var memberLine = Assert.Single((await List(member, s.MeetingId)).GetProperty("items").EnumerateArray());
        Assert.True(memberLine.GetProperty("excluded").GetBoolean());
        Assert.Equal("", memberLine.GetProperty("text").GetString());
        Assert.Equal(JsonValueKind.Null, memberLine.GetProperty("excludedByName").ValueKind);

        using var secretary = fixture.Client(s.Secretary);
        await AssertCode(await secretary.PutAsJsonAsync(url, new { excluded = false }), 200, "MSG352");
        Assert.False(Assert.Single((await List(member, s.MeetingId)).GetProperty("items").EnumerateArray())
            .GetProperty("excluded").GetBoolean());

        await using var db = fixture.Db();
        var actions = await db.AuditLog.Where(a => a.ObjectId == clip).OrderBy(a => a.OccurredAt).Select(a => a.Action)
            .ToListAsync();
        Assert.Equal(["speech_clip.excluded", "speech_clip.restored"], actions);
        Assert.Equal(HttpStatusCode.NotFound, (await chair.PutAsJsonAsync($"/api/speech-clips/{Guid.NewGuid()}/exclusion",
            new { excluded = true, reason = "Other" })).StatusCode);
    }

    [Fact]
    public async Task A_sentence_kept_on_the_device_still_arrives_up_to_a_week_after_the_meeting()
    {
        var s = await fixture.Seed(MeetingStatus.Ended);
        var ended = fixture.Clock.Now.AddDays(-1);
        await using (var db = fixture.Db())
        {
            var meeting = await db.Meetings.SingleAsync(m => m.Id == s.MeetingId);
            meeting.ActualStartedAt = ended.AddHours(-1);
            meeting.ActualEndedAt = ended;
            await db.SaveChangesAsync();
        }
        fixture.Speech.Failure = null;
        fixture.Speech.Result = OneSentence;
        using var member = fixture.Client(s.Member);
        Assert.False((await List(member, s.MeetingId)).GetProperty("canCapture").GetBoolean());
        var said = ended.AddMinutes(-5);
        var saved = await AssertCode(await Send(member, s.MeetingId, Guid.NewGuid(), startedAt: said.ToString("O")), 200, "MSG318");
        Assert.Equal(said, saved.GetProperty("clip").GetProperty("startedAt").GetDateTimeOffset());
        // A browser clock that puts it after the meeting: it ended when the meeting did, at the latest.
        var late = await AssertCode(await Send(member, s.MeetingId, Guid.NewGuid(), startedAt: fixture.Clock.Now.AddSeconds(-3).ToString("O")),
            200, "MSG318");
        Assert.Equal(ended.AddMilliseconds(-2400), late.GetProperty("clip").GetProperty("startedAt").GetDateTimeOffset());

        await using (var db = fixture.Db())
        {
            var meeting = await db.Meetings.SingleAsync(m => m.Id == s.MeetingId);
            meeting.ActualEndedAt = fixture.Clock.Now - SpeechClipRules.LateUpload - TimeSpan.FromMinutes(1);
            await db.SaveChangesAsync();
        }
        await AssertCode(await Send(member, s.MeetingId, Guid.NewGuid(), startedAt: said.ToString("O")), 409, "MSG320");
    }
}
