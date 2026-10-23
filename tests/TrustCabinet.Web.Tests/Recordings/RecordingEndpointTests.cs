using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using TrustCabinet.Domain.Audit;
using TrustCabinet.Domain.Enums;
using TrustCabinet.Domain.Recordings;

namespace TrustCabinet.Web.Tests.Recordings;

public sealed class RecordingEndpointTests(RecordingFixture fixture) : IClassFixture<RecordingFixture>
{
    private static readonly byte[] Audio = [.. "RIFF"u8, .. new byte[2048]];

    private static MultipartFormDataContent Form(byte[]? audio = null, string type = "audio/wav", bool consent = true,
        string? source = null, string? duration = "65000", string fileName = "phien-hop.wav")
    {
        var form = new MultipartFormDataContent();
        if (consent) form.Add(new StringContent("true"), "consentConfirmed");
        form.Add(new StringContent("Lập biên bản phiên họp"), "consentPurpose");
        if (source is not null) form.Add(new StringContent(source), "source");
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

    private async Task<Guid> Upload(RecordingFixture.Scenario s)
    {
        using var client = fixture.Client(s.Secretary);
        var body = await AssertCode(await client.PostAsync(s.Url, Form()), 201, "MSG290");
        return body.GetProperty("recordingId").GetGuid();
    }

    [Theory]
    [InlineData(MeetingStatus.InProgress)]
    [InlineData(MeetingStatus.Ended)]
    public async Task Secretary_stores_a_recording_with_consent_purpose_and_retention(MeetingStatus status)
    {
        var s = await fixture.Seed(status);
        var before = fixture.StoredFileCount();
        var id = await Upload(s);
        Assert.Equal(before + 1, fixture.StoredFileCount());

        using var member = fixture.Client(s.Member);
        var list = await member.GetFromJsonAsync<JsonElement>(s.Url);
        Assert.False(list.GetProperty("canUpload").GetBoolean());
        var item = Assert.Single(list.GetProperty("items").EnumerateArray());
        Assert.Equal(id, item.GetProperty("id").GetGuid());
        Assert.Equal("phien-hop.wav", item.GetProperty("fileName").GetString());
        Assert.Equal("Available", item.GetProperty("status").GetString());
        Assert.Equal("Upload", item.GetProperty("source").GetString());
        Assert.Equal(65000, item.GetProperty("durationMs").GetInt64());
        Assert.Equal("Lập biên bản phiên họp", item.GetProperty("consentPurpose").GetString());
        Assert.Equal(fixture.Clock.Now, item.GetProperty("consentObtainedAt").GetDateTimeOffset());
        Assert.Equal(fixture.Clock.Now.AddDays(180), item.GetProperty("retainUntil").GetDateTimeOffset());
        Assert.False(item.GetProperty("canErase").GetBoolean());
        Assert.DoesNotContain("storagePath", list.GetRawText(), StringComparison.OrdinalIgnoreCase);

        using var secretary = fixture.Client(s.Secretary);
        var own = await secretary.GetFromJsonAsync<JsonElement>(s.Url);
        Assert.True(own.GetProperty("canUpload").GetBoolean());
        Assert.True(own.GetProperty("items")[0].GetProperty("canErase").GetBoolean());

        await using var db = fixture.Db();
        Assert.True(await db.AuditLog.AnyAsync(a => a.Action == AuditActions.RecordingUploaded && a.ObjectId == id && a.ActorId == s.Secretary));
    }

    [Fact]
    public async Task Browser_capture_is_recorded_as_such_and_codec_parameters_are_accepted()
    {
        var s = await fixture.Seed();
        using var client = fixture.Client(s.Secretary);
        await AssertCode(await client.PostAsync(s.Url, Form(type: "audio/webm;codecs=opus", source: "BrowserCapture",
            fileName: "ghi-truc-tiep.webm", duration: null)), 201, "MSG290");
        var item = (await client.GetFromJsonAsync<JsonElement>(s.Url)).GetProperty("items")[0];
        Assert.Equal("BrowserCapture", item.GetProperty("source").GetString());
        Assert.Equal("audio/webm", item.GetProperty("contentType").GetString());
        Assert.Equal(0, item.GetProperty("durationMs").GetInt64());
    }

    [Fact]
    public async Task Refused_uploads_leave_no_audio_behind()
    {
        var s = await fixture.Seed();
        var before = fixture.StoredFileCount();
        using var client = fixture.Client(s.Secretary);

        var noConsent = await AssertCode(await client.PostAsync(s.Url, Form(consent: false)), 400, "MSG291");
        Assert.True(noConsent.GetProperty("errors").TryGetProperty("consentConfirmed", out _));
        await AssertCode(await client.PostAsync(s.Url, Form(source: "Telepathy")), 400, "MSG291");
        await AssertCode(await client.PostAsync(s.Url, Form(duration: "-5")), 400, "MSG291");
        await AssertCode(await client.PostAsync(s.Url, Form(audio: [])), 400, "MSG291");
        await AssertCode(await client.PostAsync(s.Url, JsonContent.Create(new { file = "x" })), 400, "MSG291");
        await AssertCode(await client.PostAsync(s.Url, Form(type: "text/plain")), 415, "MSG292");
        var big = new byte[RecordingFixture.MaxUploadMegabytes * 1024 * 1024 + 1];
        await AssertCode(await client.PostAsync(s.Url, Form(audio: big)), 413, "MSG293");

        Assert.Equal(before, fixture.StoredFileCount());
        Assert.Empty((await client.GetFromJsonAsync<JsonElement>(s.Url)).GetProperty("items").EnumerateArray());
    }

    [Theory]
    [InlineData(MeetingStatus.Draft)]
    [InlineData(MeetingStatus.Approved)]
    [InlineData(MeetingStatus.Published)]
    public async Task Nothing_is_recorded_before_the_meeting_starts(MeetingStatus status)
    {
        var s = await fixture.Seed(status);
        using var client = fixture.Client(s.Secretary);
        await AssertCode(await client.PostAsync(s.Url, Form()), 409, "MSG294");
        Assert.False((await client.GetFromJsonAsync<JsonElement>(s.Url)).GetProperty("canUpload").GetBoolean());
    }

    [Fact]
    public async Task Only_the_secretary_manages_and_only_the_roster_listens()
    {
        var s = await fixture.Seed();
        var id = await Upload(s);
        var before = fixture.StoredFileCount();

        foreach (var actor in new[] { s.Chair, s.Member })
        {
            using var client = fixture.Client(actor);
            await AssertCode(await client.PostAsync(s.Url, Form()), 403, "MSG295");
            await AssertCode(await client.DeleteAsync($"/api/recordings/{id}"), 403, "MSG295");
            Assert.Equal(HttpStatusCode.OK, (await client.GetAsync($"/api/recordings/{id}/playback")).StatusCode);
        }
        Assert.Equal(before, fixture.StoredFileCount());

        // The organizer can read the meeting but is not on its roster; an outsider cannot read it at all.
        foreach (var actor in new[] { s.Organizer, s.Outsider })
        {
            using var client = fixture.Client(actor);
            Assert.Equal(HttpStatusCode.NotFound, (await client.GetAsync(s.Url)).StatusCode);
            Assert.Equal(HttpStatusCode.NotFound, (await client.PostAsync(s.Url, Form())).StatusCode);
            Assert.Equal(HttpStatusCode.NotFound, (await client.GetAsync($"/api/recordings/{id}/playback")).StatusCode);
            Assert.Equal(HttpStatusCode.NotFound, (await client.DeleteAsync($"/api/recordings/{id}")).StatusCode);
        }
        using var anonymous = fixture.Client();
        Assert.Equal(HttpStatusCode.Unauthorized, (await anonymous.GetAsync(s.Url)).StatusCode);
    }

    [Fact]
    public async Task Playback_url_streams_with_ranges_and_cannot_be_forged_or_reused_late()
    {
        var s = await fixture.Seed();
        var id = await Upload(s);
        using var member = fixture.Client(s.Member);
        var playback = await member.GetFromJsonAsync<JsonElement>($"/api/recordings/{id}/playback");
        var url = playback.GetProperty("url").GetString()!;
        Assert.StartsWith($"/api/recordings/{id}/audio?expires=", url);

        using var browser = fixture.Client();
        var full = await browser.GetAsync(url);
        Assert.Equal(HttpStatusCode.OK, full.StatusCode);
        Assert.Equal("audio/wav", full.Content.Headers.ContentType!.MediaType);
        Assert.Equal(Audio, await full.Content.ReadAsByteArrayAsync());

        var ranged = new HttpRequestMessage(HttpMethod.Get, url);
        ranged.Headers.Range = new RangeHeaderValue(0, 3);
        var part = await browser.SendAsync(ranged);
        Assert.Equal(HttpStatusCode.PartialContent, part.StatusCode);
        Assert.Equal("RIFF"u8.ToArray(), await part.Content.ReadAsByteArrayAsync());

        Assert.Equal(HttpStatusCode.NotFound, (await browser.GetAsync(url[..^2] + "xx")).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await browser.GetAsync($"/api/recordings/{id}/audio")).StatusCode);
        var otherId = url.Replace(id.ToString(), Guid.NewGuid().ToString());
        Assert.Equal(HttpStatusCode.NotFound, (await browser.GetAsync(otherId)).StatusCode);

        var saved = fixture.Clock.Now;
        fixture.Clock.Now = playback.GetProperty("expiresAt").GetDateTimeOffset().AddSeconds(1);
        try { Assert.Equal(HttpStatusCode.NotFound, (await browser.GetAsync(url)).StatusCode); }
        finally { fixture.Clock.Now = saved; }
    }

    [Fact]
    public async Task Erasing_removes_the_audio_and_its_transcript_but_keeps_the_evidence()
    {
        var s = await fixture.Seed();
        var id = await Upload(s);
        await using (var db = fixture.Db())
        {
            // The upload queued a transcript already; give it a result as if the worker had finished.
            var transcript = await db.Transcripts.SingleAsync(t => t.RecordingId == id);
            transcript.Status = TranscriptStatus.Completed;
            db.TranscriptSegments.Add(new() { TranscriptId = transcript.Id, Position = 1, Text = "Kính thưa các đồng chí." });
            await db.SaveChangesAsync();
        }
        using var member = fixture.Client(s.Member);
        var url = (await member.GetFromJsonAsync<JsonElement>($"/api/recordings/{id}/playback")).GetProperty("url").GetString()!;
        var before = fixture.StoredFileCount();

        using var secretary = fixture.Client(s.Secretary);
        await AssertCode(await secretary.DeleteAsync($"/api/recordings/{id}"), 200, "MSG296");
        await AssertCode(await secretary.DeleteAsync($"/api/recordings/{id}"), 200, "MSG296");
        Assert.Equal(before - 1, fixture.StoredFileCount());

        var item = (await secretary.GetFromJsonAsync<JsonElement>(s.Url)).GetProperty("items")[0];
        Assert.Equal("Deleted", item.GetProperty("status").GetString());
        Assert.Equal(fixture.Clock.Now, item.GetProperty("deletedAt").GetDateTimeOffset());
        Assert.False(item.GetProperty("canErase").GetBoolean());
        using var browser = fixture.Client();
        Assert.Equal(HttpStatusCode.NotFound, (await browser.GetAsync(url)).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await member.GetAsync($"/api/recordings/{id}/playback")).StatusCode);

        await using var check = fixture.Db();
        Assert.False(await check.Transcripts.AnyAsync(t => t.RecordingId == id));
        Assert.False(await check.TranscriptSegments.AnyAsync(seg => seg.Transcript.RecordingId == id));
        Assert.Single(check.AuditLog.Where(a => a.Action == AuditActions.RecordingErased && a.ObjectId == id));
    }

    [Fact]
    public async Task Retention_sweep_erases_only_what_has_expired()
    {
        var s = await fixture.Seed();
        var old = await Upload(s);
        fixture.Clock.Now = fixture.Clock.Now.AddDays(100);
        var recent = await Upload(s);
        fixture.Clock.Now = fixture.Clock.Now.AddDays(81);
        try
        {
            using var scope = fixture.Host.Services.CreateScope();
            Assert.True(await scope.ServiceProvider.GetRequiredService<IRecordingService>().EraseExpiredAsync() >= 1);
            await using var db = fixture.Db();
            Assert.Equal(RecordingStatus.Deleted, (await db.Recordings.SingleAsync(r => r.Id == old)).Status);
            Assert.Equal(RecordingStatus.Available, (await db.Recordings.SingleAsync(r => r.Id == recent)).Status);
            var audit = await db.AuditLog.SingleAsync(a => a.Action == AuditActions.RecordingErased && a.ObjectId == old);
            Assert.Null(audit.ActorId);
            Assert.Contains("retention", audit.Details);
        }
        finally { fixture.Clock.Now = new(2026, 10, 23, 8, 0, 0, TimeSpan.Zero); }
    }

    [Fact]
    public async Task Database_outages_answer_with_their_own_codes()
    {
        var s = await fixture.Seed();
        var id = await Upload(s);
        using var secretary = fixture.Client(s.Secretary);
        var before = fixture.StoredFileCount();

        fixture.Failures.FailRead = true;
        try { await AssertCode(await secretary.GetAsync(s.Url), 500, "MSG297"); }
        finally { fixture.Failures.FailRead = false; }

        fixture.Failures.FailSave = true;
        try
        {
            await AssertCode(await secretary.PostAsync(s.Url, Form()), 503, "MSG298");
            await AssertCode(await secretary.DeleteAsync($"/api/recordings/{id}"), 503, "MSG298");
        }
        finally { fixture.Failures.FailSave = false; }
        Assert.Equal(before, fixture.StoredFileCount());
    }
}
