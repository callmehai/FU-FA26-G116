using System.IO.Compression;
using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using System.Xml.Linq;
using Microsoft.EntityFrameworkCore;
using TrustCabinet.Domain.Enums;
using TrustCabinet.Domain.Summaries;
using TrustCabinet.Infrastructure.Summaries;
using UglyToad.PdfPig;
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

    /// <summary>A template line as the HTML the minutes hold (ADR-0055).</summary>
    private static string Html(string line) => MinutesHtml.FromLines(line);

    [Fact]
    public async Task Secretary_builds_the_draft_from_the_records_of_the_meeting()
    {
        var start = TimeZoneInfo.ConvertTime(f.Clock.Now, SummaryWriter.FindHanoi());
        var s = await f.Seed();
        using var secretary = f.Client(s.Secretary);
        var before = await State(secretary, s);
        Assert.Equal(JsonValueKind.Null, before.GetProperty("minutes").ValueKind);
        Assert.Equal(JsonValueKind.Null, before.GetProperty("draftChangedAt").ValueKind);

        var minutes = await Create(secretary, s);
        var content = minutes.GetProperty("content").GetString()!;
        // Sanitized HTML, as stored.
        Assert.Equal(content, MinutesHtml.Sanitize(content));
        Assert.Contains("<h1>BIÊN BẢN</h1>", content);
        Assert.Contains("<p style=\"text-align: center; text-indent: 0mm; margin-bottom: 12pt\">"
            + $"<strong>Kết quả cuộc họp {SummaryFixture.Title}</strong></p>", content);
        // The minutes read Hanoi time, from when the meeting actually started.
        Assert.Equal(TimeSpan.FromHours(7), start.Offset);
        Assert.Contains($"<p><strong>Thời gian bắt đầu:</strong> {start:HH} giờ {start:mm} phút, ngày {start.Day:00}/{start.Month:00}/{start.Year}.</p>", content);
        Assert.Contains("<p><strong>Địa điểm:</strong> Phòng họp số 1.</p>", content);
        Assert.Contains("<p><strong>Thành phần tham dự:</strong> 03 người có mặt, gồm:</p>", content);
        Assert.Contains("<p><strong>Chủ trì (chủ tọa):</strong> Trần Chủ Trì.</p>", content);
        Assert.Contains("<p><strong>Thư ký (người ghi biên bản):</strong> Nguyễn Thư Ký.</p>", content);
        // No AI draft: section II lists the agenda, section III the recorded conclusions and the votes.
        Assert.Contains(Html($"### 1. {SummaryFixture.FirstItemTitle}"), content);
        Assert.Contains("<p>Lê Thành Viên trình bày. ……</p>", content);
        Assert.Contains(Html($"### 2. {SummaryFixture.SecondItemTitle}"), content);
        Assert.Contains(Html($"- {SummaryFixture.FirstItemTitle}: Thống nhất báo cáo."), content);
        Assert.Contains(Html($"- {SummaryFixture.VoteLine}"), content);
        // The header and the signature blocks are borderless two-column tables.
        Assert.Equal(2, content.Split("<table data-layout=\"frame\">").Length - 1);
        Assert.Contains("<tr><td colwidth=\"250\"><p style=\"text-align: center\"><strong>Nguyễn Thư Ký</strong></p></td>"
            + "<td colwidth=\"374\"><p style=\"text-align: center\"><strong>Trần Chủ Trì</strong></p></td></tr>", content);
        Assert.Equal("Draft", minutes.GetProperty("status").GetString());
        Assert.Equal(JsonValueKind.Null, minutes.GetProperty("builtFromDraftAt").ValueKind);
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
    public async Task The_ai_draft_fills_sections_one_to_three_whether_reviewed_or_not()
    {
        f.Model.Reset();
        var s = await f.Seed(MeetingStatus.Ended);
        using var secretary = f.Client(s.Secretary);
        using var member = f.Client(s.Member);
        await AssertCode(await secretary.PostAsync(s.Summary, null), 202, "MSG384");
        await f.ProcessAll();

        // Written, not reviewed: the minutes take it as eCab's Compose does. Both stay the Chair's and the Secretary's.
        var built = await AssertCode(await secretary.PostAsJsonAsync(s.Minutes, new { rebuild = false }), 201, "MSG395");
        var content = built.GetProperty("minutes").GetProperty("content").GetString()!;
        Assert.Contains(Html(SummaryFixture.FakeLanguageModel.Objectives), content);
        Assert.Contains(Html($"### 1. {SummaryFixture.FirstItemTitle}"), content);
        Assert.Contains("<h3>1. Kết luận của chủ trì</h3>", content);
        Assert.Contains("<h3>2. Nhiệm vụ, sản phẩm và thời hạn</h3>", content);
        Assert.Contains(Html($"- {SummaryFixture.VoteLine}"), content);
        // The minutes keep the stamp of the draft they were built from: the same as the draft's own, so not stale.
        var builtFrom = built.GetProperty("minutes").GetProperty("builtFromDraftAt").GetDateTimeOffset();
        Assert.Equal(built.GetProperty("draftChangedAt").GetDateTimeOffset(), builtFrom);
        Assert.Equal(JsonValueKind.Null, (await State(member, s)).GetProperty("draftChangedAt").ValueKind);

        // Saved by hand afterwards, the minutes still come from that same draft.
        f.Clock.Now = f.Clock.Now.AddMinutes(1);
        var saved = await AssertCode(await Save(secretary, built.GetProperty("minutes").GetProperty("id").GetGuid(), "<p>Bản tay.</p>",
            built.GetProperty("minutes").GetProperty("updatedAt").GetString()), 200, "MSG396");
        Assert.Equal(builtFrom, saved.GetProperty("minutes").GetProperty("builtFromDraftAt").GetDateTimeOffset());

        // The draft edited afterwards has moved on: its stamp differs, and the minutes keep the old text until rebuilt.
        f.Clock.Now = f.Clock.Now.AddMinutes(5);
        await AssertCode(await secretary.PutAsJsonAsync(s.Summary,
            new { objectives = "Mục tiêu sửa sau.", discussion = "1. Báo cáo\nĐã sửa.", conclusions = "1. Kết luận của chủ trì\nĐã sửa." }), 200, "MSG388");
        var stale = await State(secretary, s);
        Assert.True(stale.GetProperty("draftChangedAt").GetDateTimeOffset() > builtFrom);
        Assert.Equal(builtFrom, stale.GetProperty("minutes").GetProperty("builtFromDraftAt").GetDateTimeOffset());
        Assert.DoesNotContain("Mục tiêu sửa sau.", stale.GetProperty("minutes").GetProperty("content").GetString());
        var rebuilt = await AssertCode(await secretary.PostAsJsonAsync(s.Minutes, new { rebuild = true }), 201, "MSG395");
        Assert.Contains("Mục tiêu sửa sau.", rebuilt.GetProperty("minutes").GetProperty("content").GetString());
        Assert.Equal(rebuilt.GetProperty("draftChangedAt").GetDateTimeOffset(),
            rebuilt.GetProperty("minutes").GetProperty("builtFromDraftAt").GetDateTimeOffset());
    }

    [Fact]
    public async Task A_second_create_needs_rebuild_which_overwrites_the_same_draft()
    {
        var s = await f.Seed();
        using var chair = f.Client(s.Chair);
        var first = await Create(chair, s);
        var id = first.GetProperty("id").GetGuid();
        f.Clock.Now = f.Clock.Now.AddMinutes(1);
        await AssertCode(await Save(chair, id, "<p>Bản Thư ký đã sửa.</p>", first.GetProperty("updatedAt").GetString()), 200, "MSG396");

        await AssertCode(await chair.PostAsJsonAsync(s.Minutes, new { rebuild = false }), 409, "MSG399");
        await AssertCode(await chair.PostAsync(s.Minutes, null), 409, "MSG399");
        await using (var db = f.Db())
            Assert.Equal("<p>Bản Thư ký đã sửa.</p>", (await db.MeetingMinutes.SingleAsync(m => m.MeetingId == s.MeetingId)).Content);

        f.Clock.Now = f.Clock.Now.AddMinutes(1);
        var rebuilt = await Create(chair, s, rebuild: true);
        Assert.Equal(id, rebuilt.GetProperty("id").GetGuid());
        Assert.Contains("<h1>BIÊN BẢN</h1>", rebuilt.GetProperty("content").GetString());
        await using var check = f.Db();
        var row = await check.MeetingMinutes.SingleAsync(m => m.MeetingId == s.MeetingId);
        Assert.Contains("<h1>BIÊN BẢN</h1>", row.Content);
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
        Assert.Equal(HttpStatusCode.NotFound, (await member.GetAsync($"/api/minutes/{id}/export?format=pdf")).StatusCode);

        Assert.Equal(HttpStatusCode.NotFound, (await outsider.GetAsync(s.Minutes)).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await outsider.PostAsJsonAsync(s.Minutes, new { rebuild = true })).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound,
            (await Save(outsider, id, "x", minutes.GetProperty("updatedAt").GetString())).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await outsider.GetAsync($"/api/minutes/{id}/export")).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await outsider.GetAsync($"/api/minutes/{id}/export?format=pdf")).StatusCode);
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
        var saved = await AssertCode(await Save(chair, id, "<p>Bản của Chủ trì.</p>\r\n<p>Dòng hai.</p>", loaded), 200, "MSG396");
        Assert.Equal("<p>Bản của Chủ trì.</p><p>Dòng hai.</p>", saved.GetProperty("minutes").GetProperty("content").GetString());
        f.Clock.Now = f.Clock.Now.AddMinutes(1);
        await AssertCode(await Save(secretary, id, "Bản của Thư ký.", loaded), 409, "MSG401");
        await AssertCode(await Save(secretary, id, "Bản của Thư ký.", null), 409, "MSG401");

        await using var db = f.Db();
        var row = await db.MeetingMinutes.SingleAsync(m => m.Id == id);
        Assert.Equal("<p>Bản của Chủ trì.</p><p>Dòng hai.</p>", row.Content);
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
        // A document of empty paragraphs and rules, or of nothing but what the allow-list drops, says nothing.
        await AssertCode(await Save(secretary, id, "<p></p><p> </p><p><br></p><hr><hr data-page-break=\"true\">", loaded), 400, "MSG402");
        await AssertCode(await Save(secretary, id, "<script>alert('biên bản')</script><img src=x onerror=alert(1)>", loaded), 400, "MSG402");
        await AssertCode(await Save(secretary, id, new string('a', MinutesLimits.Content + 1), loaded), 400, "MSG402");
        // Within the limit as sent, but escaping makes the stored HTML longer than it.
        await AssertCode(await Save(secretary, id, "<p>" + new string('&', MinutesLimits.Content - 7) + "</p>", loaded), 400, "MSG402");
        await using var db = f.Db();
        Assert.Equal(minutes.GetProperty("content").GetString(), (await db.MeetingMinutes.SingleAsync(m => m.Id == id)).Content);
    }

    [Fact]
    public async Task Saved_html_is_kept_to_the_allow_list()
    {
        var s = await f.Seed();
        using var secretary = f.Client(s.Secretary);
        var minutes = await Create(secretary, s);
        var id = minutes.GetProperty("id").GetGuid();
        f.Clock.Now = f.Clock.Now.AddMinutes(1);
        const string sent = "<h1 onclick=\"alert(1)\">BIÊN BẢN</h1><script>alert(document.cookie)</script>"
            + "<p style=\"text-align: center; position: fixed\"><b>Đậm</b> <a href=\"javascript:alert(1)\">link</a>"
            + "<span style=\"color: rgb(192, 0, 0)\">đỏ</span><img src=x onerror=alert(1)></p><iframe src=\"https://x\"></iframe>"
            + "<table data-layout=\"frame\"><tbody><tr><td colwidth=\"250\" class=\"x\"><p>ký</p></td></tr></tbody></table>";
        const string kept = "<h1>BIÊN BẢN</h1><p style=\"text-align: center\"><strong>Đậm</strong> link"
            + "<span style=\"color: #c00000\">đỏ</span></p>"
            + "<table data-layout=\"frame\"><tbody><tr><td colwidth=\"250\"><p>ký</p></td></tr></tbody></table>";
        var saved = await AssertCode(await Save(secretary, id, sent, minutes.GetProperty("updatedAt").GetString()), 200, "MSG396");
        Assert.Equal(kept, saved.GetProperty("minutes").GetProperty("content").GetString());
        Assert.Equal(kept, (await State(secretary, s)).GetProperty("minutes").GetProperty("content").GetString());
        await using var db = f.Db();
        Assert.Equal(kept, (await db.MeetingMinutes.SingleAsync(m => m.Id == id)).Content);
    }

    [Fact]
    public async Task A_row_of_line_text_reads_back_as_html_and_exports()
    {
        var s = await f.Seed();
        using var secretary = f.Client(s.Secretary);
        var id = (await Create(secretary, s)).GetProperty("id").GetGuid();
        // Written before ADR-0055, when the minutes were line-marked text.
        const string legacy = "@@ **THƯ KÝ** | **CHỦ TỌA**\n# BIÊN BẢN\n**Địa điểm:** Phòng họp số 1.\n";
        await using (var db = f.Db())
            await db.MeetingMinutes.Where(m => m.Id == id).ExecuteUpdateAsync(u => u.SetProperty(m => m.Content, legacy));

        var content = (await State(secretary, s)).GetProperty("minutes").GetProperty("content").GetString()!;
        Assert.Equal(MinutesHtml.FromLines(legacy), content);
        Assert.StartsWith("<table data-layout=\"frame\">", content);
        Assert.Contains("<h1>BIÊN BẢN</h1><p><strong>Địa điểm:</strong> Phòng họp số 1.</p>", content);

        var word = await secretary.GetAsync($"/api/minutes/{id}/export");
        Assert.Equal(HttpStatusCode.OK, word.StatusCode);
        using var zip = new ZipArchive(new MemoryStream(await word.Content.ReadAsByteArrayAsync()));
        using var reader = new StreamReader(zip.GetEntry("word/document.xml")!.Open());
        var text = string.Concat(XDocument.Parse(await reader.ReadToEndAsync()).Descendants().Where(e => e.Name.LocalName == "t")
            .Select(e => e.Value));
        Assert.Equal("THƯ KÝCHỦ TỌABIÊN BẢNĐịa điểm: Phòng họp số 1.", text);

        var pdf = await secretary.GetAsync($"/api/minutes/{id}/export?format=pdf");
        Assert.Equal(HttpStatusCode.OK, pdf.StatusCode);
        using var document = PdfDocument.Open(await pdf.Content.ReadAsByteArrayAsync());
        var words = string.Join(" ", document.GetPages().SelectMany(p => p.GetWords()).Select(w => w.Text));
        Assert.Contains("BIÊN BẢN", words);
        Assert.DoesNotContain("**", words);
        Assert.DoesNotContain("@@", words);
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
            await AssertCode(await member.GetAsync($"/api/minutes/{id}/export?format=pdf"), 403, "MSG397");
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
        Assert.True(response.Headers.CacheControl!.NoStore);
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
    public async Task Export_returns_a_pdf_of_the_draft_in_its_own_font()
    {
        var s = await f.Seed();
        using var secretary = f.Client(s.Secretary);
        var id = (await Create(secretary, s)).GetProperty("id").GetGuid();
        var response = await secretary.GetAsync($"/api/minutes/{id}/export?format=pdf");
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal("application/pdf", response.Content.Headers.ContentType!.MediaType);
        Assert.Equal($"Biên bản - {SummaryFixture.Title}.pdf", response.Content.Headers.ContentDisposition!.FileNameStar);
        Assert.True(response.Headers.CacheControl!.NoStore);
        var bytes = await response.Content.ReadAsByteArrayAsync();
        Assert.Equal("%PDF"u8.ToArray(), bytes[..4]);

        using var pdf = PdfDocument.Open(bytes);
        var page = pdf.GetPage(1);
        // A4 in points.
        Assert.Equal(595, Math.Round(page.Width));
        Assert.Equal(842, Math.Round(page.Height));
        var text = string.Join(" ", pdf.GetPages().Select(p => string.Join(" ", p.GetWords().Select(w => w.Text))));
        Assert.Contains("BIÊN BẢN", text);
        Assert.Contains("CỘNG HÒA XÃ HỘI CHỦ NGHĨA VIỆT NAM", text);
        Assert.Contains("Trần Chủ Trì", text);
        Assert.Contains("Nguyễn Thư Ký", text);
        Assert.DoesNotContain("**", text);
        // Every letter comes from the bundled Liberation Serif, never a font the server happens to have.
        var fonts = pdf.GetPages().SelectMany(p => p.Letters).Select(l => l.FontName ?? "").Distinct().ToList();
        Assert.NotEmpty(fonts);
        Assert.All(fonts, name => Assert.Contains("LiberationSerif", name));
        Assert.Contains(fonts, name => name.EndsWith("LiberationSerif-Bold", StringComparison.Ordinal));
    }

    [Fact]
    public void The_pdf_draws_every_vietnamese_letter_and_survives_characters_its_font_lacks()
    {
        const string letters = "aàáảãạăằắẳẵặâầấẩẫậeèéẻẽẹêềếểễệiìíỉĩịoòóỏõọôồốổỗộơờớởỡợuùúủũụưừứửữựyỳýỷỹỵđ";
        // A decomposed "ị" (i + dot below), a tab, an emoji with its variation selector and one without.
        var content = $"# {letters.ToUpperInvariant()}\n{letters}\n**__{letters}__**\nNgh\u0069\u0323 quyết\tsố 1 \u2705\uFE0F 🙂";
        var bytes = MinutesPdf.Build(content);
        using var pdf = PdfDocument.Open(bytes);
        var text = string.Concat(pdf.GetPages().SelectMany(p => p.Letters).Select(l => l.Value).Where(v => !string.IsNullOrWhiteSpace(v)));
        Assert.Contains(letters, text);
        Assert.Contains(letters.ToUpperInvariant(), text);
        // Composed into one letter, the tab drawn as a space, each emoji as one "?".
        Assert.EndsWith("Nghịquyếtsố1??", text);
    }

    [Fact]
    public void The_pdf_keeps_what_the_editor_kept_from_reading_as_a_mark()
    {
        // The editor puts a zero-width space before a typed "# " or "@@" and between typed "**": the PDF must show them
        // as typed, as the page and the Word file do, not as a title, a table and a bold run.
        var bytes = MinutesPdf.Build("​# không phải tiêu đề\nA*​*b\n​@@ x | y\n");
        using var pdf = PdfDocument.Open(bytes);
        var text = string.Concat(pdf.GetPages().SelectMany(p => p.Letters).Select(l => l.Value).Where(v => !string.IsNullOrWhiteSpace(v)));
        Assert.Equal("#khôngphảitiêuđềA**b@@x|y", text);
    }

    [Theory]
    [InlineData("xlsx")]
    [InlineData("html")]
    public async Task An_unknown_export_format_is_refused(string format)
    {
        var s = await f.Seed();
        using var secretary = f.Client(s.Secretary);
        var id = (await Create(secretary, s)).GetProperty("id").GetGuid();
        await AssertCode(await secretary.GetAsync($"/api/minutes/{id}/export?format={format}"), 400, "MSG406");
        // Word is the default, and either name may be asked for.
        foreach (var url in new[] { $"/api/minutes/{id}/export?format=docx", $"/api/minutes/{id}/export?format=PDF" })
            Assert.Equal(HttpStatusCode.OK, (await secretary.GetAsync(url)).StatusCode);
    }

    [Fact]
    public async Task A_secret_meeting_minutes_are_not_exported()
    {
        var s = await f.Seed(sensitivity: Sensitivity.Secret);
        using var secretary = f.Client(s.Secretary);
        var id = (await Create(secretary, s)).GetProperty("id").GetGuid();
        await AssertCode(await secretary.GetAsync($"/api/minutes/{id}/export"), 403, "MSG405");
        await AssertCode(await secretary.GetAsync($"/api/minutes/{id}/export?format=pdf"), 403, "MSG405");
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
            await AssertCode(await secretary.GetAsync($"/api/minutes/{id}/export?format=pdf"), 500, "MSG403");
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
