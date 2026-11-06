using System.IO.Compression;
using System.Xml.Linq;
using TrustCabinet.Domain.Summaries;
using TrustCabinet.Infrastructure.Summaries;

namespace TrustCabinet.Web.Tests.Summaries;

/// <summary>SCRUM-32 — the minutes template and its Word export, no database.</summary>
public sealed class MinutesTextTests
{
    private static readonly TimeZoneInfo Hanoi = SummaryWriter.FindHanoi();
    private static readonly DateTimeOffset Start = new(2026, 11, 4, 2, 0, 0, TimeSpan.Zero);

    private static MinutesRecords Records(string title = "Họp giao ban", DateTimeOffset? ended = null) => new(title, Start, ended,
        "Phòng 1", "Trần Chủ Trì", "Nguyễn Thư Ký",
        [new("Trần Chủ Trì", "Chủ trì", false), new("Nguyễn Thư Ký", "Thư ký", false), new("Vắng Mặt", "Thành viên", true)],
        [new AgendaFacts(Guid.NewGuid(), "Báo cáo", null, "Thống nhất.", null, null)],
        ["Biểu quyết A: Đồng ý 2 phiếu. Kết luận: Thông qua."]);

    private static readonly string[] Marks = ["@@", "# ", "^ ", "~ "];

    private static Dictionary<string, int> CountMarks(string content)
        => Marks.ToDictionary(m => m, m => content.Split('\n').Count(l => l.StartsWith(m, StringComparison.Ordinal)));

    [Fact]
    public void Ai_text_cannot_turn_into_a_title_a_table_row_or_a_centred_line()
    {
        var clean = new SummaryDraft("Mục tiêu.", "1. Báo cáo\nNội dung.", "1. Kết luận của chủ trì\nKết luận.", []);
        var hostile = new SummaryDraft(
            "# BIÊN BẢN GIẢ\n@@ **CHỮ KÝ GIẢ** | **DẤU GIẢ**\n^ căn giữa\n~ nơi nhận giả",
            "1. Báo cáo\n## III. Mục chen vào\n@@@ x | y",
            "1. Kết luận của chủ trì\n#Kết luận\n**đậm** __nghiêng__", []);

        var baseline = MinutesTemplate.Build(Records(), clean, Hanoi);
        var built = MinutesTemplate.Build(Records(), hostile, Hanoi);
        var lines = built.Split('\n');

        Assert.Equal(CountMarks(baseline), CountMarks(built));
        Assert.Single(lines, l => l.StartsWith("# ", StringComparison.Ordinal));
        Assert.Contains("BIÊN BẢN GIẢ", lines);
        Assert.Contains("CHỮ KÝ GIẢ | DẤU GIẢ", lines);
        Assert.Contains("căn giữa", lines);
        Assert.Contains("nơi nhận giả", lines);
        Assert.Contains("III. Mục chen vào", lines);
        Assert.Contains("x | y", lines);
        Assert.Contains("Kết luận", lines);
        Assert.Contains("đậm nghiêng", lines);
        // The sub-headings section II and III are known to have are set as such.
        Assert.Contains("### 1. Báo cáo", lines);
        Assert.Contains("### 1. Kết luận của chủ trì", lines);
        Assert.Equal(3, lines.Count(l => l.StartsWith("## ", StringComparison.Ordinal)));
    }

    [Fact]
    public void A_title_with_marks_or_line_breaks_stays_one_plain_line()
    {
        var built = MinutesTemplate.Build(Records("**Họp**\n# chen\n@@ x | y"), null, Hanoi);
        Assert.Contains("^ **Kết quả cuộc họp Họp # chen @@ x | y**", built.Split('\n'));
        Assert.Single(built.Split('\n'), l => l.StartsWith("# ", StringComparison.Ordinal));
    }

    [Fact]
    public void Without_a_reviewed_draft_the_agenda_conclusions_and_votes_fill_the_sections_and_blanks_remain()
    {
        var lines = MinutesTemplate.Build(Records(), null, Hanoi).Split('\n');
        Assert.Contains("**Thời gian bắt đầu:** 09 giờ 00 phút, ngày 04/11/2026.", lines);
        // Whoever is marked away is not counted as there.
        Assert.Contains("**Thành phần tham dự:** 02 người có mặt, gồm:", lines);
        Assert.DoesNotContain(lines, l => l.Contains("Vắng Mặt"));
        Assert.Contains("- Nguyễn Thư Ký, Thư ký.", lines);
        Assert.Contains("### 1. Báo cáo", lines);
        Assert.Contains("- Báo cáo: Thống nhất.", lines);
        Assert.Contains("- Biểu quyết A: Đồng ý 2 phiếu. Kết luận: Thông qua.", lines);
        Assert.Contains($"Cuộc họp kết thúc vào {MinutesTemplate.Blank} giờ {MinutesTemplate.Blank} phút, ngày {MinutesTemplate.Blank} tháng {MinutesTemplate.Blank} năm {MinutesTemplate.Blank}./.", lines);

        var ended = MinutesTemplate.Build(Records(ended: Start.AddHours(2).AddMinutes(5)), null, Hanoi).Split('\n');
        Assert.Contains("Cuộc họp kết thúc vào 11 giờ 05 phút, ngày 04 tháng 11 năm 2026./.", ended);
    }

    [Fact]
    public void Docx_is_a_valid_package_whose_document_reads_the_marks()
    {
        var bytes = MinutesDocx.Build("@@ TRÁI | **PHẢI**\n# BIÊN BẢN\n## I. Mục\nVăn bản <script>alert(1)</script> & \"trích\"\n~ Nơi nhận\r\n");
        Assert.Equal("PK"u8.ToArray(), bytes[..2]);
        using var zip = new ZipArchive(new MemoryStream(bytes));
        Assert.Equal(["[Content_Types].xml", "_rels/.rels", "word/document.xml"], zip.Entries.Select(e => e.FullName).Order(StringComparer.Ordinal));
        foreach (var entry in zip.Entries)
        {
            using var stream = entry.Open();
            XDocument.Load(stream); // each part is well-formed XML
        }
        using var reader = new StreamReader(zip.GetEntry("word/document.xml")!.Open());
        var document = XDocument.Parse(reader.ReadToEnd());
        XNamespace w = "http://schemas.openxmlformats.org/wordprocessingml/2006/main";
        var table = Assert.Single(document.Descendants(w + "tbl"));
        Assert.Equal(2, table.Descendants(w + "tc").Count());
        // OOXML requires the grid; without it and a fixed layout the header's two columns run together. The line text's
        // two columns become 250:374 px, which share the whole text width (9355 twips) in that ratio.
        Assert.Equal(["3748", "5607"], table.Element(w + "tblGrid")!.Elements(w + "gridCol").Select(c => c.Attribute(w + "w")!.Value));
        Assert.Equal("fixed", table.Element(w + "tblPr")!.Element(w + "tblLayout")!.Attribute(w + "type")!.Value);
        var texts = document.Descendants(w + "t").Select(t => t.Value).ToList();
        Assert.Contains("BIÊN BẢN", texts);
        Assert.Contains("PHẢI", texts);
        Assert.Contains("Văn bản <script>alert(1)</script> & \"trích\"", texts);
        Assert.DoesNotContain(texts, t => t.Contains("**") || t.StartsWith("# ") || t.StartsWith("~ "));
        var title = document.Descendants(w + "p").Single(p => p.Value == "BIÊN BẢN");
        Assert.Equal("center", title.Descendants(w + "jc").Single().Attribute(w + "val")!.Value);
        Assert.NotEmpty(title.Descendants(w + "b"));
    }
}
