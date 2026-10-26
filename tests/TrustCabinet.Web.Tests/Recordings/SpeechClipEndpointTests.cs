using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using TrustCabinet.Domain.Enums;
using TrustCabinet.Domain.Transcription;

namespace TrustCabinet.Web.Tests.Recordings;

/// <summary>SCRUM-109 — sentences from each attendee's own mic, and the capture mode chosen before the meeting starts.</summary>
public sealed class SpeechClipEndpointTests(RecordingFixture fixture) : IClassFixture<RecordingFixture>
{
    private static readonly byte[] Wav = [.. "RIFF"u8, .. new byte[2048]];

    private static readonly SpeechTranscription OneSentence = new(2400, "vi", "gipformer-1.5-68m-int8@abc123def456", "",
        [new(0, 1200, null, "Tôi đồng ý với dự thảo."), new(1300, 2400, null, "Đề nghị biểu quyết.")], []);

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

        // No audio is kept anywhere: the row holds words, time and speaker only.
        await using var db = fixture.Db();
        Assert.Equal(1, await db.SpeechClips.CountAsync(c => c.MeetingId == s.MeetingId));
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
}
