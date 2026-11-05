using System.IO.Compression;
using System.Xml.Linq;
using TrustCabinet.Infrastructure.Summaries;
using UglyToad.PdfPig;

namespace TrustCabinet.Web.Tests.Summaries;

/// <summary>SCRUM-32, ADR-0055 — the Word and PDF files drawn from the minutes' HTML, no database.</summary>
public sealed class MinutesFileTests
{
    private static readonly XNamespace W = "http://schemas.openxmlformats.org/wordprocessingml/2006/main";

    private static XDocument Word(string content)
    {
        using var zip = new ZipArchive(new MemoryStream(MinutesDocx.Build(content)));
        foreach (var entry in zip.Entries)
        {
            using var stream = entry.Open();
            XDocument.Load(stream); // each part is well-formed XML
        }
        using var reader = new StreamReader(zip.GetEntry("word/document.xml")!.Open());
        return XDocument.Parse(reader.ReadToEnd());
    }

    private static XElement Run(XDocument document, string text)
        => document.Descendants(W + "r").Single(r => r.Element(W + "t")?.Value == text);

    private static string? Val(XElement? element, string name = "val") => element?.Attribute(W + name)?.Value;

    private static XElement Props(XElement paragraph) => paragraph.Element(W + "pPr")!;

    private static XElement Paragraph(XDocument document, string text)
        => document.Descendants(W + "p").Single(p => string.Concat(p.Descendants(W + "t").Select(t => t.Value)) == text);

    [Fact]
    public void Word_runs_carry_the_inline_formatting()
    {
        var document = Word("<p><strong>đậm</strong> <em>nghiêng</em> <u>gạch</u> <s>xoá</s> H<sub>2</sub>O m<sup>3</sup>"
            + "<span style=\"color: #c00000; font-size: 12pt\">đỏ</span><span style=\"background-color: #ffff00\">vàng</span>"
            + "<strong><em><u>cả ba</u></em></strong>dòng một<br>dòng hai</p>");
        Assert.NotNull(Run(document, "đậm").Descendants(W + "b").SingleOrDefault());
        Assert.Null(Run(document, "nghiêng").Descendants(W + "b").SingleOrDefault());
        Assert.NotNull(Run(document, "nghiêng").Descendants(W + "i").SingleOrDefault());
        Assert.Equal("single", Val(Run(document, "gạch").Descendants(W + "u").Single()));
        Assert.NotNull(Run(document, "xoá").Descendants(W + "strike").SingleOrDefault());
        Assert.Equal("subscript", Val(Run(document, "2").Descendants(W + "vertAlign").Single()));
        Assert.Equal("superscript", Val(Run(document, "3").Descendants(W + "vertAlign").Single()));
        var red = Run(document, "đỏ");
        Assert.Equal("C00000", Val(red.Descendants(W + "color").Single()));
        Assert.Equal("24", Val(red.Descendants(W + "sz").Single()));
        Assert.Equal("26", Val(Run(document, "đậm").Descendants(W + "sz").Single()));
        Assert.Equal("FFFF00", Val(Run(document, "vàng").Descendants(W + "shd").Single(), "fill"));
        var all = Run(document, "cả ba");
        Assert.NotNull(all.Descendants(W + "b").SingleOrDefault());
        Assert.NotNull(all.Descendants(W + "i").SingleOrDefault());
        Assert.NotNull(all.Descendants(W + "u").SingleOrDefault());
        // A line break stays inside the paragraph.
        var paragraph = Assert.Single(document.Descendants(W + "body").Elements(W + "p"));
        Assert.Single(paragraph.Descendants(W + "br"));
    }

    [Fact]
    public void Word_paragraphs_follow_the_decree_defaults_and_their_own_styles()
    {
        var document = Word("<h1>BIÊN BẢN</h1><h2>I. Mục</h2><p>Thân bài.</p>"
            + "<p style=\"text-align: right; text-indent: 0mm; margin-left: 10mm; line-height: 1.5; margin-top: 6pt; margin-bottom: 0pt\">Riêng.</p>");
        var title = Props(Paragraph(document, "BIÊN BẢN"));
        Assert.Equal("center", Val(title.Element(W + "jc")));
        Assert.Equal("240", Val(title.Element(W + "spacing"), "before"));
        Assert.Null(title.Element(W + "ind"));
        var heading = Props(Paragraph(document, "I. Mục"));
        Assert.Equal("both", Val(heading.Element(W + "jc")));
        Assert.Equal("720", Val(heading.Element(W + "ind"), "firstLine"));
        Assert.Equal("120", Val(heading.Element(W + "spacing"), "before"));
        var body = Props(Paragraph(document, "Thân bài."));
        Assert.Equal("both", Val(body.Element(W + "jc")));
        Assert.Equal("720", Val(body.Element(W + "ind"), "firstLine"));
        Assert.Equal("60", Val(body.Element(W + "spacing"), "after"));
        Assert.Equal("288", Val(body.Element(W + "spacing"), "line"));
        var own = Props(Paragraph(document, "Riêng."));
        Assert.Equal("right", Val(own.Element(W + "jc")));
        Assert.Equal("567", Val(own.Element(W + "ind"), "left"));
        Assert.Null(Val(own.Element(W + "ind"), "firstLine"));
        Assert.Equal("360", Val(own.Element(W + "spacing"), "line"));
        Assert.Equal("120", Val(own.Element(W + "spacing"), "before"));
        Assert.Equal("0", Val(own.Element(W + "spacing"), "after"));
    }

    [Fact]
    public void Word_lists_hang_their_marker_and_count_from_start()
    {
        var document = Word("<ul><li><p>chấm</p><ul><li><p>con</p></li></ul></li></ul><ol start=\"3\"><li><p>ba</p></li><li><p>bốn</p></li></ol>");
        var bullet = Paragraph(document, "•chấm");
        // The marker sits at the list's margin, the text 6 mm (340 twips) further.
        Assert.Equal("340", Val(Props(bullet).Element(W + "ind"), "left"));
        Assert.Equal("340", Val(Props(bullet).Element(W + "ind"), "hanging"));
        Assert.Single(bullet.Descendants(W + "tab"));
        Assert.Equal("left", Val(Props(bullet).Element(W + "jc")));
        Assert.Equal("680", Val(Props(Paragraph(document, "•con")).Element(W + "ind"), "left"));
        Assert.NotNull(Paragraph(document, "3.ba"));
        Assert.NotNull(Paragraph(document, "4.bốn"));
    }

    [Fact]
    public void Word_tables_have_borders_unless_a_frame_and_share_the_text_width()
    {
        var document = Word("<table><colgroup><col style=\"width: 100px\"><col></colgroup><tbody>"
            + "<tr><th colwidth=\"100\"><p>A</p></th><th><p>B</p></th></tr>"
            + "<tr><td colspan=\"2\" style=\"vertical-align: middle\"><p>gộp</p></td></tr>"
            + "<tr><td rowspan=\"2\"><p>dọc</p></td><td><p>x</p></td></tr><tr><td><p>y</p></td></tr></tbody></table>"
            + "<table data-layout=\"frame\"><tbody><tr><td><p>khung</p></td></tr></tbody></table>");
        var tables = document.Descendants(W + "tbl").ToList();
        Assert.Equal(2, tables.Count);
        var grid = tables[0].Element(W + "tblGrid")!.Elements(W + "gridCol").Select(c => int.Parse(Val(c, "w")!)).ToList();
        // A column without a width gets the average of the others: here the same as the first.
        Assert.Equal(9355, grid.Sum());
        Assert.InRange(Math.Abs(grid[0] - grid[1]), 0, 1);
        Assert.Equal("single", Val(tables[0].Descendants(W + "tblBorders").Single().Element(W + "insideV")));
        Assert.Equal("4", Val(tables[0].Descendants(W + "tblBorders").Single().Element(W + "top"), "sz"));
        Assert.Equal("nil", Val(tables[1].Descendants(W + "tblBorders").Single().Element(W + "top")));
        // A header cell is bold and centred.
        var header = Paragraph(document, "A");
        Assert.Equal("center", Val(Props(header).Element(W + "jc")));
        Assert.NotNull(Run(document, "A").Descendants(W + "b").SingleOrDefault());
        Assert.Equal("left", Val(Props(Paragraph(document, "x")).Element(W + "jc")));
        Assert.Equal("0", Val(Props(Paragraph(document, "x")).Element(W + "spacing"), "after"));
        var rows = tables[0].Elements(W + "tr").ToList();
        Assert.Equal("2", Val(rows[1].Descendants(W + "gridSpan").Single()));
        Assert.Equal("center", Val(rows[1].Descendants(W + "vAlign").Single()));
        Assert.Equal("restart", Val(rows[2].Descendants(W + "vMerge").Single()));
        // The row below holds a continuation cell, then its own.
        var below = rows[3].Elements(W + "tc").ToList();
        Assert.Equal(2, below.Count);
        Assert.NotNull(below[0].Descendants(W + "vMerge").SingleOrDefault());
        Assert.Null(Val(below[0].Descendants(W + "vMerge").Single()));
        // Two tables in a row stay two tables: a paragraph between them.
        Assert.Equal(W + "p", tables[0].ElementsAfterSelf().First().Name);
    }

    [Fact]
    public void Word_draws_rules_and_page_breaks()
    {
        var document = Word("<p>một</p><hr><p>hai</p><hr data-page-break=\"true\"><p>ba</p>");
        var body = document.Descendants(W + "body").Single().Elements(W + "p").ToList();
        Assert.Equal(5, body.Count);
        Assert.Equal("single", Val(body[1].Descendants(W + "pBdr").Single().Element(W + "bottom")));
        Assert.Equal("page", Val(body[3].Descendants(W + "br").Single(), "type"));
        Assert.Single(document.Descendants(W + "br"));
    }

    [Fact]
    public void Word_text_is_escaped_and_keeps_only_what_xml_can_carry()
    {
        var document = Word("<p>a &lt;b&gt; &amp; \"c\" ￾ 🙂</p>");
        Assert.Equal("a <b> & \"c\"  🙂", string.Concat(document.Descendants(W + "t").Select(t => t.Value)));
    }

    private static string PdfText(byte[] bytes)
    {
        using var pdf = PdfDocument.Open(bytes);
        return string.Join(" ", pdf.GetPages().Select(p => string.Join(" ", p.GetWords().Select(w => w.Text))));
    }

    [Fact]
    public void Pdf_draws_the_html_with_its_formatting()
    {
        var bytes = MinutesPdf.Build("<h1>BIÊN BẢN</h1><p><strong>Địa điểm:</strong> Phòng 1.</p>"
            + "<p><span style=\"color: #c00000; font-size: 20pt\">Đỏ</span> <u>gạch</u> <s>xoá</s> H<sub>2</sub>O<br>dòng hai</p>"
            + "<ul><li><p>chấm</p></li></ul><ol start=\"5\"><li><p>năm</p></li></ol>"
            + "<table><tbody><tr><th><p>Cột A</p></th><td colwidth=\"300\"><p>Ô B</p></td></tr>"
            + "<tr><td colspan=\"2\" style=\"vertical-align: bottom\"><p>gộp</p></td></tr></tbody></table><hr>"
            + "<table data-layout=\"frame\"><tbody><tr><td rowspan=\"2\"><p>dọc</p></td><td><p>trên</p></td></tr><tr><td><p>dưới</p></td></tr></tbody></table>");
        var text = PdfText(bytes);
        foreach (var word in new[] { "BIÊN BẢN", "Địa điểm:", "Đỏ", "gạch", "xoá", "dòng hai", "•", "chấm", "5.", "năm", "Cột A", "Ô B", "gộp", "dọc", "dưới" })
            Assert.Contains(word, text);
        using var pdf = PdfDocument.Open(bytes);
        var letters = pdf.GetPages().SelectMany(p => p.Letters).ToList();
        Assert.All(letters.Select(l => l.FontName ?? ""), name => Assert.Contains("LiberationSerif", name));
        var red = letters.First(l => l.Value == "Đ" && l.PointSize > 15);
        Assert.Equal(20, Math.Round(red.PointSize));
        // The heading is the bold face, the body the regular one.
        Assert.EndsWith("LiberationSerif-Bold", letters.First(l => l.Value == "B").FontName, StringComparison.Ordinal);
        Assert.EndsWith("LiberationSerif", letters.First(l => l.Value == "P").FontName, StringComparison.Ordinal);
    }

    [Fact]
    public void Pdf_breaks_the_page_where_the_minutes_say_so()
    {
        using (var one = PdfDocument.Open(MinutesPdf.Build("<p>trang một</p><hr><p>vẫn trang một</p>")))
            Assert.Equal(1, one.NumberOfPages);
        using var two = PdfDocument.Open(MinutesPdf.Build("<p>trang một</p><hr data-page-break=\"true\"><p>trang hai</p>"));
        Assert.Equal(2, two.NumberOfPages);
        Assert.Contains("hai", string.Join(" ", two.GetPage(2).GetWords().Select(w => w.Text)));
        Assert.DoesNotContain("hai", string.Join(" ", two.GetPage(1).GetWords().Select(w => w.Text)));
    }

    [Fact]
    public void Line_text_and_its_html_draw_the_same_words()
    {
        const string lines = "@@ TÊN CƠ QUAN | **CỘNG HÒA XÃ HỘI CHỦ NGHĨA VIỆT NAM**\n# BIÊN BẢN\n**Địa điểm:** Phòng 1.\n~ **__Nơi nhận:__**";
        Assert.Equal(PdfText(MinutesPdf.Build(lines)), PdfText(MinutesPdf.Build(MinutesHtml.FromLines(lines))));
        string WordText(string content) => string.Concat(Word(content).Descendants(W + "t").Select(t => t.Value));
        Assert.Equal(WordText(lines), WordText(MinutesHtml.FromLines(lines)));
        Assert.Contains("CỘNG HÒA XÃ HỘI CHỦ NGHĨA VIỆT NAM", WordText(lines));
    }

    /// <summary>Runs on a thread with the stack a Linux thread-pool thread has, where a deep recursion would end the process.</summary>
    private static T OnSmallStack<T>(Func<T> work)
    {
        T result = default!;
        Exception? failure = null;
        var thread = new Thread(() =>
        {
            try { result = work(); }
            catch (Exception e) { failure = e; }
        }, 1536 * 1024);
        thread.Start();
        thread.Join();
        if (failure is not null) throw failure;
        return result;
    }

    [Fact]
    public void Nesting_deeper_than_any_editor_document_is_flagged_and_never_walked()
    {
        var lists = string.Concat(Enumerable.Repeat("<ul><li>", 2000)) + "sâu";
        var marks = string.Concat(Enumerable.Repeat("<u>", 4000)) + "gạch";
        Assert.True(MinutesHtml.TooDeep(lists));
        Assert.True(MinutesHtml.TooDeep(marks));
        Assert.False(MinutesHtml.TooDeep("<table><tbody><tr><td><ul><li><p><strong><em>x</em></strong></p></li></ul></td></tr></tbody></table>"));
        // Content that somehow got in deeper is read as its text, on every path, without overflowing the stack.
        Assert.Equal("<p>sâu</p>", OnSmallStack(() => MinutesHtml.Sanitize(lists)));
        Assert.Equal("<p>gạch</p>", OnSmallStack(() => MinutesHtml.Sanitize(marks)));
        Assert.NotEmpty(OnSmallStack(() => MinutesPdf.Build(marks)));
        Assert.NotEmpty(OnSmallStack(() => MinutesDocx.Build(lists)));
    }

    [Fact]
    public void A_table_keeps_to_word_s_63_columns_whatever_its_spans_say()
    {
        var wide = "<table><tbody><tr>" + string.Concat(Enumerable.Repeat("<td colspan=\"50\"><p>x</p></td>", 200)) + "</tr></tbody></table>";
        var tall = "<table><tbody>" + string.Concat(Enumerable.Repeat("<tr><td rowspan=\"200\" colspan=\"50\"><p>x</p></td></tr>", 400)) + "</tbody></table>";
        foreach (var html in new[] { wide, tall })
        {
            var grid = Word(html).Descendants(W + "gridCol").ToList();
            Assert.InRange(grid.Count, 1, MinutesLayout.MaxColumns);
            Assert.All(grid, col => Assert.True(int.Parse(Val(col, "w")!) > 0));
            Assert.NotEmpty(MinutesPdf.Build(html));
        }
    }

    [Fact]
    public void An_indent_wider_than_its_cell_still_lays_out_in_the_pdf()
    {
        // Six columns (the largest table the grid picker offers) and two indent steps, a deep list and a big letter in a cell.
        var cells = string.Concat(Enumerable.Repeat("<td><p style=\"margin-left: 25.4mm\">thụt</p></td>", 5))
            + "<td colwidth=\"40\"><ul><li><ul><li><ul><li><ul><li><p><span style=\"font-size: 24pt\">W</span></p></li></ul></li></ul></li></ul></li></ul></td>";
        var html = $"<table><tbody><tr>{cells}</tr></tbody></table>";
        using var pdf = PdfDocument.Open(MinutesPdf.Build(html));
        Assert.Contains("thụt", string.Concat(pdf.GetPages().SelectMany(p => p.Letters).Select(l => l.Value)));
    }

    [Fact]
    public void After_a_line_break_the_pdf_does_not_indent_the_first_line_again()
    {
        using var pdf = PdfDocument.Open(MinutesPdf.Build("<p>Dòng đầu<br>Ngắt</p>"));
        var letters = pdf.GetPages().Single().Letters;
        var first = letters.First(l => l.Value == "D").StartBaseLine.X;
        var second = letters.First(l => l.Value == "N").StartBaseLine.X;
        // The first line starts 12.7 mm (36 pt) in; the line after the break starts at the margin.
        Assert.InRange(first - second, 30, 42);
    }
}
