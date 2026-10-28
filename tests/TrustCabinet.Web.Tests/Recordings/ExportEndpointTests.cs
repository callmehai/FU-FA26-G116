using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using TrustCabinet.Domain.Enums;
using TrustCabinet.Domain.Transcription;

namespace TrustCabinet.Web.Tests.Recordings;

/// <summary>
/// SCRUM-29/31/109, as AI-eCabinet has them: the Chair or a Secretary saves a recording's file or a transcript as
/// text; nobody else does, and a Secret meeting's never leave. Suggested names reach the Secretary only.
/// </summary>
public sealed class ExportEndpointTests(RecordingFixture fixture) : IClassFixture<RecordingFixture>
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

    private async Task<Guid> UploadAndTranscribe(RecordingFixture.Scenario s)
    {
        fixture.Speech.Failure = null;
        using var client = fixture.Client(s.Secretary);
        var form = new MultipartFormDataContent { { new StringContent("true"), "consentConfirmed" } };
        var file = new ByteArrayContent(Audio);
        file.Headers.ContentType = MediaTypeHeaderValue.Parse("audio/wav");
        form.Add(file, "file", "phien-hop.wav");
        var created = await client.PostAsync(s.Url, form);
        Assert.Equal(HttpStatusCode.Created, created.StatusCode);
        var id = (await created.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("recordingId").GetGuid();
        while (true)
        {
            using var scope = fixture.Host.Services.CreateScope();
            if (!await scope.ServiceProvider.GetRequiredService<ITranscriptionService>().ProcessNextAsync()) break;
        }
        return id;
    }

    private async Task MakeSecret(Guid meetingId)
    {
        await using var db = fixture.Db();
        var meeting = await db.Meetings.SingleAsync(m => m.Id == meetingId);
        meeting.Sensitivity = Sensitivity.Secret;
        await db.SaveChangesAsync();
    }

    [Fact]
    public async Task The_chair_or_a_secretary_saves_the_original_file_and_a_playback_link_cannot_be_turned_into_one()
    {
        var s = await fixture.Seed();
        var id = await UploadAndTranscribe(s);
        using var member = fixture.Client(s.Member);
        Assert.False((await member.GetFromJsonAsync<JsonElement>(s.Url)).GetProperty("items")[0].GetProperty("canDownload").GetBoolean());
        await AssertCode(await member.GetAsync($"/api/recordings/{id}/download"), 403, "MSG355");

        using var chair = fixture.Client(s.Chair);
        Assert.True((await chair.GetFromJsonAsync<JsonElement>(s.Url)).GetProperty("items")[0].GetProperty("canDownload").GetBoolean());
        var link = await chair.GetFromJsonAsync<JsonElement>($"/api/recordings/{id}/download");
        var url = link.GetProperty("url").GetString()!;
        Assert.EndsWith("&download=1", url);
        using var anonymous = fixture.Client();
        var saved = await anonymous.GetAsync(url);
        Assert.Equal(HttpStatusCode.OK, saved.StatusCode);
        Assert.Equal("attachment", saved.Content.Headers.ContentDisposition?.DispositionType);
        Assert.Equal(Audio, await saved.Content.ReadAsByteArrayAsync());

        // Every listener gets a playback link; adding the flag to it must not make it a download.
        var playback = (await member.GetFromJsonAsync<JsonElement>($"/api/recordings/{id}/playback")).GetProperty("url").GetString()!;
        Assert.Equal(HttpStatusCode.NotFound, (await anonymous.GetAsync(playback + "&download=1")).StatusCode);
        Assert.Equal(HttpStatusCode.OK, (await anonymous.GetAsync(playback)).StatusCode);

        await using var db = fixture.Db();
        Assert.Equal(1, await db.AuditLog.CountAsync(a => a.ObjectId == id && a.Action == "recording.downloaded"));
    }

    [Fact]
    public async Task A_finished_transcript_is_saved_as_text_with_the_names_the_secretary_gave()
    {
        var s = await fixture.Seed();
        var id = await UploadAndTranscribe(s);
        using var secretary = fixture.Client(s.Secretary);
        await secretary.PutAsJsonAsync($"/api/recordings/{id}/speakers/SPEAKER_00", new { userId = s.Chair });
        Assert.True((await secretary.GetFromJsonAsync<JsonElement>($"/api/recordings/{id}/transcript")).GetProperty("canExport").GetBoolean());

        var file = await secretary.GetAsync($"/api/recordings/{id}/transcript.txt");
        Assert.Equal(HttpStatusCode.OK, file.StatusCode);
        Assert.Equal("text/plain", file.Content.Headers.ContentType?.MediaType);
        Assert.StartsWith("ban-go-bang-", file.Content.Headers.ContentDisposition?.FileNameStar ?? file.Content.Headers.ContentDisposition?.FileName);
        var text = Encoding.UTF8.GetString(await file.Content.ReadAsByteArrayAsync()).TrimStart('﻿');
        Assert.StartsWith("Recording test\nBản gỡ băng — bản ghi phien-hop.wav\n", text);
        Assert.Contains("[00:00:00] Recording test 1: Kính thưa các đồng chí.\n", text);
        Assert.Contains("[00:00:06] Người nói 2: Tôi đề nghị bổ sung kinh phí.\n", text);

        using var member = fixture.Client(s.Member);
        Assert.False((await member.GetFromJsonAsync<JsonElement>($"/api/recordings/{id}/transcript")).GetProperty("canExport").GetBoolean());
        await AssertCode(await member.GetAsync($"/api/recordings/{id}/transcript.txt"), 403, "MSG355");

        // Not finished yet: nothing to save.
        await AssertCode(await secretary.PostAsync($"/api/recordings/{id}/transcribe", null), 202, "MSG300");
        await AssertCode(await secretary.GetAsync($"/api/recordings/{id}/transcript.txt"), 409, "MSG357");
    }

    [Fact]
    public async Task A_secret_meeting_keeps_its_audio_and_words_inside()
    {
        var s = await fixture.Seed();
        var id = await UploadAndTranscribe(s);
        await MakeSecret(s.MeetingId);
        using var chair = fixture.Client(s.Chair);
        await AssertCode(await chair.GetAsync($"/api/recordings/{id}/download"), 403, "MSG356");
        await AssertCode(await chair.GetAsync($"/api/recordings/{id}/transcript.txt"), 403, "MSG356");
        await AssertCode(await chair.GetAsync($"/api/meetings/{s.MeetingId}/speech-clips.txt"), 403, "MSG356");
        Assert.False((await chair.GetFromJsonAsync<JsonElement>(s.Url)).GetProperty("items")[0].GetProperty("canDownload").GetBoolean());
    }

    [Fact]
    public async Task The_per_mic_transcript_is_saved_without_the_sentences_taken_out()
    {
        var s = await fixture.Seed();
        fixture.Speech.Failure = null;
        fixture.Speech.Result = new(2000, "vi", "gipformer-1.5-68m-int8@abc123def456", "", [new(0, 2000, null, "Tôi đồng ý.", -24)], []);
        using var member = fixture.Client(s.Member);
        async Task<Guid> Say(string text, int secondsAgo)
        {
            fixture.Speech.Result = fixture.Speech.Result with { Segments = [new(0, 2000, null, text, -24)] };
            var content = new ByteArrayContent(Audio);
            content.Headers.ContentType = MediaTypeHeaderValue.Parse("audio/wav");
            var at = Uri.EscapeDataString(fixture.Clock.Now.AddSeconds(-secondsAgo).ToString("O"));
            var saved = await member.PutAsync($"/api/meetings/{s.MeetingId}/speech-clips/{Guid.NewGuid()}?startedAt={at}&durationMs=2000", content);
            return (await saved.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("clip").GetProperty("id").GetGuid();
        }
        await Say("Tôi đồng ý.", 30);
        var aside = await Say("Ai tắt giúp cái điều hoà.", 20);
        using var secretary = fixture.Client(s.Secretary);
        await secretary.PutAsJsonAsync($"/api/speech-clips/{aside}/exclusion", new { excluded = true, reason = "Other" });
        await AssertCode(await member.GetAsync($"/api/meetings/{s.MeetingId}/speech-clips.txt"), 403, "MSG355");

        var text = Encoding.UTF8.GetString(await secretary.GetByteArrayAsync($"/api/meetings/{s.MeetingId}/speech-clips.txt"))
            .TrimStart('﻿');
        Assert.Contains("Bản gỡ băng — ghi theo micro từng người\n", text);
        Assert.Contains("] Recording test 2: Tôi đồng ý.\n", text);
        Assert.DoesNotContain("điều hoà", text);
        await using var db = fixture.Db();
        Assert.Equal(1, await db.AuditLog.CountAsync(a => a.ObjectId == s.MeetingId && a.Action == "speech_clip.exported"));
    }

    [Fact]
    public async Task Only_the_secretary_gets_the_machines_guess_and_a_correction_is_audited()
    {
        var s = await fixture.Seed();
        var id = await UploadAndTranscribe(s);
        await using (var db = fixture.Db())
        {
            var speaker = await db.TranscriptSpeakers.FirstAsync(t => t.Transcript.RecordingId == id && t.Label == "SPEAKER_00");
            speaker.SuggestedUserId = s.Chair;
            speaker.SuggestionScore = 0.91;
            await db.SaveChangesAsync();
        }
        using var secretary = fixture.Client(s.Secretary);
        var mine = (await secretary.GetFromJsonAsync<JsonElement>($"/api/recordings/{id}/transcript")).GetProperty("speakers")[0];
        Assert.Equal(s.Chair, mine.GetProperty("suggestedUserId").GetGuid());
        using var member = fixture.Client(s.Member);
        var theirs = (await member.GetFromJsonAsync<JsonElement>($"/api/recordings/{id}/transcript")).GetProperty("speakers")[0];
        Assert.Equal(JsonValueKind.Null, theirs.GetProperty("suggestedUserId").ValueKind);
        Assert.Equal(JsonValueKind.Null, theirs.GetProperty("suggestedName").ValueKind);
        Assert.Equal(JsonValueKind.Null, theirs.GetProperty("suggestionScore").ValueKind);

        var segment = (await secretary.GetFromJsonAsync<JsonElement>($"/api/recordings/{id}/transcript")).GetProperty("segments")[0]
            .GetProperty("id").GetGuid();
        await secretary.PutAsJsonAsync($"/api/transcript-segments/{segment}", new { text = "Kính thưa các đồng chí, hôm nay" });
        await using var check = fixture.Db();
        Assert.Equal(1, await check.AuditLog.CountAsync(a => a.ObjectId == id && a.Action == "transcript.segment_edited"));
    }
}
