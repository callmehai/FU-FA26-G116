using System.IO.Compression;
using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using System.Xml.Linq;
using Microsoft.EntityFrameworkCore;
using TrustCabinet.Domain.Enums;
using TrustCabinet.Domain.Summaries;
using TrustCabinet.Infrastructure.Summaries;
using static TrustCabinet.Web.Tests.Agenda.AgendaEndpointTests;

namespace TrustCabinet.Web.Tests.Summaries;

/// <summary>SCRUM-32 — the draft minutes: GET/POST /api/meetings/{id}/minutes, PUT /api/minutes/{id}, GET …/export.</summary>
public sealed class MinutesEndpointTests(SummaryFixture f) : IClassFixture<SummaryFixture>
{
    private static async Task<JsonElement> Create(HttpClient client, SummaryFixture.Scenario s, bool rebuild = false)
        => (await AssertCode(await client.PostAsJsonAsync(s.Minutes, new { rebuild }), 201, "MSG395")).GetProperty("minutes");

    private static Task<HttpResponseMessage> Save(HttpClient client, Guid id, string? content, string? updatedAt)
        => client.PutAsJsonAsync($"/api/minutes/{id}", new { content, updatedAt });

    private static Task<JsonElement> State(HttpClient client, SummaryFixture.Scenario s)
        => client.GetFromJsonAsync<JsonElement>(s.Minutes);

    [Fact]
    public async Task Secretary_builds_the_draft_from_the_records_of_the_meeting()
    {
        var start = TimeZoneInfo.ConvertTime(f.Clock.Now, SummaryWriter.FindHanoi());
        var s = await f.Seed();
        using var secretary = f.Client(s.Secretary);
        var before = await State(secretary, s);
        Assert.Equal(JsonValueKind.Null, before.GetProperty("minutes").ValueKind);
        Assert.False(before.GetProperty("summaryReviewed").GetBoolean());

        var minutes = await Create(secretary, s);
        var content = minutes.GetProperty("content").GetString()!;
        var lines = content.Split('\n');
        Assert.Contains("# BIÊN BẢN", lines);
        Assert.Contains($"^ **Kết quả cuộc họp {SummaryFixture.Title}**", lines);
        // The minutes read Hanoi time, from when the meeting actually started.
        Assert.Equal(TimeSpan.FromHours(7), start.Offset);
        Assert.Contains($"**Thời gian bắt đầu:** {start:HH} giờ {start:mm} phút, ngày {start.Day:00}/{start.Month:00}/{start.Year}.", lines);
        Assert.Contains("**Địa điểm:** Phòng họp số 1.", lines);
        Assert.Contains("**Thành phần tham dự:** 03 người có mặt, gồm:", lines);
        Assert.Contains("**Chủ trì (chủ tọa):** Trần Chủ Trì.", lines);
        Assert.Contains("**Thư ký (người ghi biên bản):** Nguyễn Thư Ký.", lines);
        // No reviewed AI draft: section II lists the agenda, section III the recorded conclusions and the votes.
        Assert.Contains($"### 1. {SummaryFixture.FirstItemTitle}", lines);
        Assert.Contains("Lê Thành Viên trình bày. ……", lines);
        Assert.Contains($"### 2. {SummaryFixture.SecondItemTitle}", lines);
        Assert.Contains($"- {SummaryFixture.FirstItemTitle}: Thống nhất báo cáo.", lines);
        Assert.Contains($"- {SummaryFixture.VoteLine}", lines);
        Assert.Contains("@@ **Nguyễn Thư Ký** | **Trần Chủ Trì**", lines);
        Assert.Equal("Draft", minutes.GetProperty("status").GetString());
        Assert.Equal(1, minutes.GetProperty("revisionNumber").GetInt32());
        Assert.Equal(s.Secretary, minutes.GetProperty("createdBy").GetProperty("id").GetGuid());

        await using var db = f.Db();
        var row = await db.MeetingMinutes.SingleAsync(m => m.MeetingId == s.MeetingId);
        Assert.Equal(minutes.GetProperty("id").GetGuid(), row.Id);
        Assert.Equal(MinutesStatus.Draft, row.Status);
        Assert.Equal(s.Secretary, row.CreatedById);
        Assert.Equal(content, row.Content);
    }

    [Fact]
    public async Task A_reviewed_ai_draft_fills_sections_one_to_three()
    {
        f.Model.Reset();
        var s = await f.Seed(MeetingStatus.Ended);
        using var secretary = f.Client(s.Secretary);
        await AssertCode(await secretary.PostAsync(s.Summary, null), 202, "MSG384");
        await f.ProcessAll();
        // Written but not reviewed: the minutes do not take it.
        var unreviewed = (await Create(secretary, s)).GetProperty("content").GetString()!;
        Assert.DoesNotContain(SummaryFixture.FakeLanguageModel.Objectives, unreviewed);

        await AssertCode(await secretary.PostAsync(s.Summary + "/review", null), 200, "MSG391");
        var rebuilt = await AssertCode(await secretary.PostAsJsonAsync(s.Minutes, new { rebuild = true }), 201, "MSG395");
        Assert.True(rebuilt.GetProperty("summaryReviewed").GetBoolean());
        var lines = rebuilt.GetProperty("minutes").GetProperty("content").GetString()!.Split('\n');
        Assert.Contains(SummaryFixture.FakeLanguageModel.Objectives, lines);
        Assert.Contains($"### 1. {SummaryFixture.FirstItemTitle}", lines);
        Assert.Contains("### 1. Kết luận của chủ trì", lines);
        Assert.Contains("### 2. Nhiệm vụ, sản phẩm và thời hạn", lines);
        Assert.Contains($"- {SummaryFixture.VoteLine}", lines);
    }

    [Fact]
    public async Task A_second_create_needs_rebuild_which_overwrites_the_same_draft()
    {
        var s = await f.Seed();
        using var chair = f.Client(s.Chair);
        var first = await Create(chair, s);
        var id = first.GetProperty("id").GetGuid();
        f.Clock.Now = f.Clock.Now.AddMinutes(1);
        await AssertCode(await Save(chair, id, "Bản Thư ký đã sửa.", first.GetProperty("updatedAt").GetString()), 200, "MSG396");

        await AssertCode(await chair.PostAsJsonAsync(s.Minutes, new { rebuild = false }), 409, "MSG399");
        await AssertCode(await chair.PostAsync(s.Minutes, null), 409, "MSG399");
        await using (var db = f.Db())
            Assert.Equal("Bản Thư ký đã sửa.", (await db.MeetingMinutes.SingleAsync(m => m.MeetingId == s.MeetingId)).Content);

        f.Clock.Now = f.Clock.Now.AddMinutes(1);
        var rebuilt = await Create(chair, s, rebuild: true);
        Assert.Equal(id, rebuilt.GetProperty("id").GetGuid());
        Assert.Contains("# BIÊN BẢN", rebuilt.GetProperty("content").GetString());
        await using var check = f.Db();
        var row = await check.MeetingMinutes.SingleAsync(m => m.MeetingId == s.MeetingId);
        Assert.Contains("# BIÊN BẢN", row.Content);
        Assert.Equal(f.Clock.Now, row.UpdatedAt);
    }

    [Fact]
    public async Task Members_neither_build_nor_see_a_draft_and_outsiders_get_404()
    {
        var s = await f.Seed();
        using var secretary = f.Client(s.Secretary);
        using var member = f.Client(s.Member);
        using var outsider = f.Client(s.Outsider);
        await AssertCode(await member.PostAsJsonAsync(s.Minutes, new { rebuild = false }), 403, "MSG397");
        var minutes = await Create(secretary, s);
        var id = minutes.GetProperty("id").GetGuid();

        var seen = await State(member, s);
        Assert.Equal(JsonValueKind.Null, seen.GetProperty("minutes").ValueKind);
        Assert.False(seen.GetProperty("canManage").GetBoolean());
        Assert.DoesNotContain("BIÊN BẢN", await member.GetStringAsync(s.Minutes));
        // A draft they may not read does not exist for them.
        Assert.Equal(HttpStatusCode.NotFound,
            (await Save(member, id, "x", minutes.GetProperty("updatedAt").GetString())).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await member.GetAsync($"/api/minutes/{id}/export")).StatusCode);

        Assert.Equal(HttpStatusCode.NotFound, (await outsider.GetAsync(s.Minutes)).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await outsider.PostAsJsonAsync(s.Minutes, new { rebuild = true })).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound,
            (await Save(outsider, id, "x", minutes.GetProperty("updatedAt").GetString())).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await outsider.GetAsync($"/api/minutes/{id}/export")).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await secretary.GetAsync($"/api/minutes/{Guid.NewGuid()}/export")).StatusCode);

        await using var db = f.Db();
        Assert.Equal(minutes.GetProperty("content").GetString(), (await db.MeetingMinutes.SingleAsync(m => m.Id == id)).Content);
    }

    [Fact]
    public async Task Not_signed_in_gets_401()
    {
        var s = await f.Seed();
        using var anonymous = f.Client();
        Assert.Equal(HttpStatusCode.Unauthorized, (await anonymous.GetAsync(s.Minutes)).StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized, (await anonymous.PostAsJsonAsync(s.Minutes, new { rebuild = false })).StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized, (await Save(anonymous, Guid.NewGuid(), "x", null)).StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized, (await anonymous.GetAsync($"/api/minutes/{Guid.NewGuid()}/export")).StatusCode);
    }

    [Theory]
    [InlineData(MeetingStatus.Draft)]
    [InlineData(MeetingStatus.Published)]
    [InlineData(MeetingStatus.Cancelled)]
    public async Task Minutes_are_built_only_once_the_meeting_has_started(MeetingStatus status)
    {
        var s = await f.Seed(status);
        using var secretary = f.Client(s.Secretary);
        await AssertCode(await secretary.PostAsJsonAsync(s.Minutes, new { rebuild = false }), 409, "MSG398");
        await using var db = f.Db();
        Assert.False(await db.MeetingMinutes.AnyAsync(m => m.MeetingId == s.MeetingId));
    }

    [Fact]
    public async Task Save_with_a_stale_updated_at_is_refused_and_keeps_the_newer_text()
    {
        var s = await f.Seed();
        using var secretary = f.Client(s.Secretary);
        using var chair = f.Client(s.Chair);
        var minutes = await Create(secretary, s);
        var id = minutes.GetProperty("id").GetGuid();
        var loaded = minutes.GetProperty("updatedAt").GetString();

        f.Clock.Now = f.Clock.Now.AddMinutes(1);
        var saved = await AssertCode(await Save(chair, id, "Bản của Chủ trì.\r\nDòng hai.", loaded), 200, "MSG396");
        Assert.Equal("Bản của Chủ trì.\nDòng hai.", saved.GetProperty("minutes").GetProperty("content").GetString());
        f.Clock.Now = f.Clock.Now.AddMinutes(1);
        await AssertCode(await Save(secretary, id, "Bản của Thư ký.", loaded), 409, "MSG401");
        await AssertCode(await Save(secretary, id, "Bản của Thư ký.", null), 409, "MSG401");

        await using var db = f.Db();
        var row = await db.MeetingMinutes.SingleAsync(m => m.Id == id);
        Assert.Equal("Bản của Chủ trì.\nDòng hai.", row.Content);
        Assert.Equal(f.Clock.Now.AddMinutes(-1), row.UpdatedAt);

        // Reloading gives the value that saves again.
        var fresh = (await State(secretary, s)).GetProperty("minutes").GetProperty("updatedAt").GetString();
        await AssertCode(await Save(secretary, id, "Bản của Thư ký.", fresh), 200, "MSG396");
    }

    [Fact]
    public async Task Two_saves_from_the_same_load_cannot_both_succeed()
    {
        var s = await f.Seed();
        using var secretary = f.Client(s.Secretary);
        using var chair = f.Client(s.Chair);
        var minutes = await Create(secretary, s);
        var id = minutes.GetProperty("id").GetGuid();
        var loaded = minutes.GetProperty("updatedAt").GetString();
        f.Clock.Now = f.Clock.Now.AddMinutes(1);

        HttpResponseMessage? chairSave = null;
        // The secretary's save has read the row; the chair saves in between.
        f.Failures.AfterMinutesRead = async () => chairSave = await Save(chair, id, "Bản của Chủ trì.", loaded);
        var secretarySave = await Save(secretary, id, "Bản của Thư ký.", loaded);
        f.Failures.AfterMinutesRead = null;

        Assert.NotNull(chairSave);
        var outcomes = new[] { (int)chairSave!.StatusCode, (int)secretarySave.StatusCode }.Order().ToArray();
        Assert.True(outcomes is [200, 409], $"Expected one 200 and one 409/MSG401, got {string.Join(", ", outcomes)}");
    }

    [Fact]
    public async Task Empty_or_oversized_content_is_refused()
    {
        var s = await f.Seed();
        using var secretary = f.Client(s.Secretary);
        var minutes = await Create(secretary, s);
        var id = minutes.GetProperty("id").GetGuid();
        var loaded = minutes.GetProperty("updatedAt").GetString();
        var empty = await AssertCode(await Save(secretary, id, "   ", loaded), 400, "MSG402");
        Assert.True(empty.GetProperty("errors").TryGetProperty("content", out _));
        await AssertCode(await Save(secretary, id, null, loaded), 400, "MSG402");
        await AssertCode(await Save(secretary, id, new string('a', MinutesLimits.Content + 1), loaded), 400, "MSG402");
        await using var db = f.Db();
        Assert.Equal(minutes.GetProperty("content").GetString(), (await db.MeetingMinutes.SingleAsync(m => m.Id == id)).Content);
    }

    [Theory]
    [InlineData(MinutesStatus.WaitingApproval)]
    [InlineData(MinutesStatus.Issued)]
    public async Task Minutes_past_draft_are_locked(MinutesStatus status)
    {
        var s = await f.Seed();
        using var secretary = f.Client(s.Secretary);
        using var member = f.Client(s.Member);
        var minutes = await Create(secretary, s);
        var id = minutes.GetProperty("id").GetGuid();
        await using (var db = f.Db())
            await db.MeetingMinutes.Where(m => m.Id == id).ExecuteUpdateAsync(u => u.SetProperty(m => m.Status, status));

        await AssertCode(await Save(secretary, id, "Sửa sau khi gửi.", minutes.GetProperty("updatedAt").GetString()), 409, "MSG400");
        await AssertCode(await secretary.PostAsJsonAsync(s.Minutes, new { rebuild = true }), 409, "MSG400");
        await AssertCode(await secretary.PostAsJsonAsync(s.Minutes, new { rebuild = false }), 409, "MSG400");
        if (status == MinutesStatus.Issued)
        {
            // Issued minutes are everyone's to read, but only the Chair and the Secretary write or export them.
            Assert.Equal("Issued", (await State(member, s)).GetProperty("minutes").GetProperty("status").GetString());
            await AssertCode(await Save(member, id, "x", minutes.GetProperty("updatedAt").GetString()), 403, "MSG397");
            await AssertCode(await member.GetAsync($"/api/minutes/{id}/export"), 403, "MSG397");
        }
        await using var check = f.Db();
        Assert.Equal(minutes.GetProperty("content").GetString(), (await check.MeetingMinutes.SingleAsync(m => m.Id == id)).Content);
    }

    [Fact]
    public async Task Export_returns_a_word_file_of_the_draft()
    {
        var s = await f.Seed();
        using var secretary = f.Client(s.Secretary);
        var id = (await Create(secretary, s)).GetProperty("id").GetGuid();
        var response = await secretary.GetAsync($"/api/minutes/{id}/export");
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal("application/vnd.openxmlformats-officedocument.wordprocessingml.document",
            response.Content.Headers.ContentType!.MediaType);
        Assert.Equal($"Biên bản - {SummaryFixture.Title}.docx", response.Content.Headers.ContentDisposition!.FileNameStar);
        var bytes = await response.Content.ReadAsByteArrayAsync();
        Assert.Equal("PK"u8.ToArray(), bytes[..2]);
        using var zip = new ZipArchive(new MemoryStream(bytes));
        using var reader = new StreamReader(zip.GetEntry("word/document.xml")!.Open());
        var text = string.Concat(XDocument.Parse(await reader.ReadToEndAsync()).Descendants()
            .Where(e => e.Name.LocalName == "t").Select(e => e.Value));
        Assert.Contains("BIÊN BẢN", text);
        Assert.Contains(SummaryFixture.Title, text);
        Assert.DoesNotContain("**", text);
    }

    [Fact]
    public async Task A_secret_meeting_minutes_are_not_exported()
    {
        var s = await f.Seed(sensitivity: Sensitivity.Secret);
        using var secretary = f.Client(s.Secretary);
        var id = (await Create(secretary, s)).GetProperty("id").GetGuid();
        await AssertCode(await secretary.GetAsync($"/api/minutes/{id}/export"), 403, "MSG405");
        // Still read on screen.
        Assert.NotEqual(JsonValueKind.Null, (await State(secretary, s)).GetProperty("minutes").ValueKind);
    }

    [Fact]
    public async Task Database_outages_have_their_codes()
    {
        var s = await f.Seed();
        using var secretary = f.Client(s.Secretary);
        var minutes = await Create(secretary, s);
        var id = minutes.GetProperty("id").GetGuid();

        f.Failures.FailMinutesRead = true;
        try
        {
            await AssertCode(await secretary.GetAsync(s.Minutes), 500, "MSG403");
            await AssertCode(await secretary.GetAsync($"/api/minutes/{id}/export"), 500, "MSG403");
        }
        finally { f.Failures.FailMinutesRead = false; }

        f.Clock.Now = f.Clock.Now.AddMinutes(1);
        f.Failures.FailSave = true;
        try { await AssertCode(await Save(secretary, id, "Không lưu được.", minutes.GetProperty("updatedAt").GetString()), 503, "MSG404"); }
        finally { f.Failures.FailSave = false; }
        await using var db = f.Db();
        Assert.Equal(minutes.GetProperty("content").GetString(), (await db.MeetingMinutes.SingleAsync(m => m.Id == id)).Content);
    }
}
