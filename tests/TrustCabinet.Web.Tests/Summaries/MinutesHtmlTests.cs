using TrustCabinet.Infrastructure.Summaries;

namespace TrustCabinet.Web.Tests.Summaries;

/// <summary>SCRUM-32, ADR-0055 — the minutes' HTML allow-list, and the line-marked text of older rows converted to it.</summary>
public sealed class MinutesHtmlTests
{
    /// <summary>What the editor (TipTap) writes: every one of these is stored exactly as sent.</summary>
    [Theory]
    [InlineData("<p style=\"text-align: center\">x</p>")]
    [InlineData("<p style=\"text-indent: 0mm; margin-left: 12.7mm; line-height: 1.5\">Đoạn văn.</p>")]
    [InlineData("<p><span style=\"color: #c00000; font-size: 12pt\">đỏ</span> và <span style=\"background-color: #ffff00\">vàng</span></p>")]
    [InlineData("<table style=\"min-width: 50px\" data-layout=\"frame\"><colgroup><col style=\"width: 250px\"><col style=\"min-width: 25px\"></colgroup><tbody><tr><td colspan=\"1\" rowspan=\"1\" colwidth=\"250\"><p>a</p></td><td colspan=\"1\" rowspan=\"1\"><p>b</p></td></tr></tbody></table>")]
    [InlineData("<table><tbody><tr><th colspan=\"2\" rowspan=\"1\" colwidth=\"100,200\" style=\"text-align: left; vertical-align: middle\"><p>Tiêu đề</p></th></tr><tr><td><p>1</p></td><td><p>2</p></td></tr></tbody></table>")]
    [InlineData("<ul><li><p>một</p></li><li><p>hai</p><ul><li><p>hai một</p></li></ul></li></ul>")]
    [InlineData("<ol start=\"3\" style=\"margin-left: 6mm\"><li style=\"line-height: 1.5\"><p>ba</p></li></ol>")]
    [InlineData("<hr>")]
    [InlineData("<hr data-page-break=\"true\">")]
    [InlineData("<p></p>")]
    [InlineData("<h1>BIÊN BẢN</h1><h2>I. Mục</h2><h3>1. Tiểu mục</h3>")]
    [InlineData("<p><strong>đậm</strong> <em>nghiêng</em> <u>gạch</u> <s>xoá</s> H<sub>2</sub>O m<sup>2</sup><br>dòng mới</p>")]
    [InlineData("<p>a &amp; b &lt;c&gt; &nbsp;d \"e\"</p>")]
    [InlineData("<p style=\"margin-top: 6pt; margin-bottom: 0pt; text-indent: -5mm; margin-left: 10pt\">x</p>")]
    public void What_the_editor_writes_survives_unchanged(string html)
    {
        Assert.Equal(html, MinutesHtml.Sanitize(html));
        Assert.Equal(html, MinutesHtml.Normalize(html));
    }

    [Fact]
    public void Scripts_frames_and_their_kind_go_with_their_content()
    {
        var html = MinutesHtml.Sanitize(
            "<p>a<script>alert(1)</script>b</p><iframe src=\"x\">trong khung</iframe><style>p{}</style><svg><text>vẽ</text></svg>"
            + "<template><p>mẫu</p></template><img src=x onerror=alert(1)><form><button>bấm</button></form><p>c</p>");
        Assert.Equal("<p>ab</p><p>c</p>", html);
    }

    [Fact]
    public void Attributes_outside_the_list_go()
    {
        var html = MinutesHtml.Sanitize(
            "<p onclick=\"alert(1)\" class=\"x\" id=\"y\" data-x=\"1\">a<a href=\"javascript:alert(1)\" target=\"_blank\">link</a></p>"
            + "<span style=\"color: #c00000\" onmouseover=\"x\">s</span>");
        Assert.Equal("<p>alink</p><p><span style=\"color: #c00000\">s</span></p>", html);
    }

    [Fact]
    public void Unknown_elements_are_unwrapped_and_old_names_renamed()
    {
        Assert.Equal("<p>một <strong>hai</strong> ba</p><p>bốn</p>",
            MinutesHtml.Sanitize("<div><font face=\"Arial\">một <b>hai</b> ba</font></div><section><div>bốn</div></section>"));
        Assert.Equal("<p><strong>b</strong><em>i</em><s>strike</s><s>del</s><u>ins</u></p><h3>h4</h3><h3>h5</h3><h3>h6</h3>",
            MinutesHtml.Sanitize("<p><b>b</b><i>i</i><strike>strike</strike><del>del</del><ins>ins</ins></p><h4>h4</h4><h5>h5</h5><h6>h6</h6>"));
    }

    [Fact]
    public void Inline_content_among_blocks_is_wrapped_in_a_paragraph()
    {
        Assert.Equal("<p>chữ <strong>rời</strong></p><p>đoạn</p><p>đuôi</p>",
            MinutesHtml.Sanitize("chữ <strong>rời</strong><p>đoạn</p>\n  đuôi  \n"));
        Assert.Equal("<ul><li><p>mục</p></li><li><p>trước</p><p>đoạn</p></li></ul>",
            MinutesHtml.Sanitize("<ul><li>mục</li><li>trước<p>đoạn</p></li></ul>"));
        Assert.Equal("<table><tbody><tr><td><p>ô</p></td></tr></tbody></table>",
            MinutesHtml.Sanitize("<table><tr><td>ô</td></tr></table>"));
        // Whitespace between blocks is not a paragraph.
        Assert.Equal("<p>a</p><p>b</p>", MinutesHtml.Sanitize("<p>a</p>\n\n<p>b</p>\n"));
    }

    [Theory]
    // A property on an element that cannot carry it.
    [InlineData("<p style=\"color: #c00000; font-size: 12pt; text-align: center\">x</p>", "<p style=\"text-align: center\">x</p>")]
    [InlineData("<td style=\"text-indent: 5mm\">x</td>", "<p>x</p>")]
    [InlineData("<ul style=\"text-align: center\"><li style=\"margin-left: 5mm\"><p>x</p></li></ul>", "<ul><li><p>x</p></li></ul>")]
    // Out of range.
    [InlineData("<p style=\"text-indent: 60mm; margin-left: -1mm; line-height: 4; margin-top: 80pt\">x</p>", "<p>x</p>")]
    [InlineData("<p style=\"text-indent: 142pt; margin-left: 150mm; line-height: 0.8; margin-bottom: 72pt\">x</p>",
        "<p style=\"margin-left: 150mm; line-height: 0.8; margin-bottom: 72pt\">x</p>")]
    [InlineData("<p><span style=\"font-size: 41pt\">a</span><span style=\"font-size: 6pt\">b</span></p>",
        "<p>a<span style=\"font-size: 6pt\">b</span></p>")]
    // Bad units and values.
    [InlineData("<p style=\"text-indent: 1em; margin-left: 12.7; line-height: 1.5em; text-align: middle\">x</p>", "<p>x</p>")]
    [InlineData("<p><span style=\"font-size: 12px\">a</span></p>", "<p>a</p>")]
    [InlineData("<table style=\"width: 50%; min-width: 2001px\"><tr><td>x</td></tr></table>",
        "<table><tbody><tr><td><p>x</p></td></tr></tbody></table>")]
    [InlineData("<p style=\"text-align: center !important; margin-top: expression(alert(1))\">x</p>", "<p>x</p>")]
    // Case is normalised; the last of a repeated property wins in its place; an empty style goes.
    [InlineData("<p style=\"TEXT-ALIGN: Center; text-align: right;\">x</p>", "<p style=\"text-align: right\">x</p>")]
    [InlineData("<p style=\"\">x</p><p style=\";;\">y</p>", "<p>x</p><p>y</p>")]
    public void Styles_are_kept_to_their_element_and_range(string input, string expected)
        => Assert.Equal(expected, MinutesHtml.Sanitize(input));

    [Theory]
    [InlineData("color: rgb(192, 0, 0)", "color: #c00000")]
    [InlineData("color: #C00000", "color: #c00000")]
    [InlineData("color:rgb(0,112,192)", "color: #0070c0")]
    [InlineData("color: #000", "color: #000000")]
    [InlineData("background-color: #FFFF00", "background-color: #ffff00")]
    [InlineData("background-color: rgb(217, 217, 217)", "background-color: #d9d9d9")]
    public void Colours_are_normalised_to_the_palette(string style, string expected)
        => Assert.Equal($"<p><span style=\"{expected}\">x</span></p>", MinutesHtml.Sanitize($"<p><span style=\"{style}\">x</span></p>"));

    [Theory]
    [InlineData("<p><span style=\"color: #123456\">x</span></p>")]
    [InlineData("<p><span style=\"background-color: #c00000\">x</span></p>")]
    [InlineData("<p><span style=\"color: rgb(300, 0, 0)\">x</span></p>")]
    [InlineData("<p><span style=\"color: red\">x</span></p>")]
    [InlineData("<p><span>x</span></p>")]
    [InlineData("<p><span class=\"mark\">x</span></p>")]
    public void A_span_left_without_a_style_is_unwrapped(string html)
        => Assert.Equal("<p>x</p>", MinutesHtml.Sanitize(html));

    [Theory]
    [InlineData("<ol start=\"0\"><li><p>x</p></li></ol>", "<ol><li><p>x</p></li></ol>")]
    [InlineData("<ol start=\"10000\"><li><p>x</p></li></ol>", "<ol><li><p>x</p></li></ol>")]
    [InlineData("<ol start=\"007\"><li><p>x</p></li></ol>", "<ol start=\"7\"><li><p>x</p></li></ol>")]
    [InlineData("<ul start=\"3\"><li><p>x</p></li></ul>", "<ul><li><p>x</p></li></ul>")]
    [InlineData("<hr data-page-break=\"yes\">", "<hr>")]
    [InlineData("<table data-layout=\"grid\"><tr><td><p>x</p></td></tr></table>", "<table><tbody><tr><td><p>x</p></td></tr></tbody></table>")]
    [InlineData("<table><tr><td colspan=\"51\" rowspan=\"0\" colwidth=\"2001\"><p>x</p></td></tr></table>",
        "<table><tbody><tr><td><p>x</p></td></tr></tbody></table>")]
    [InlineData("<table><tr><td colspan=\"50\" rowspan=\"200\" colwidth=\"1, 2000\"><p>x</p></td></tr></table>",
        "<table><tbody><tr><td colspan=\"50\" rowspan=\"200\" colwidth=\"1,2000\"><p>x</p></td></tr></tbody></table>")]
    [InlineData("<table><tr><td colwidth=\"100,abc\"><p>x</p></td></tr></table>", "<table><tbody><tr><td><p>x</p></td></tr></tbody></table>")]
    public void Attribute_values_are_checked(string input, string expected) => Assert.Equal(expected, MinutesHtml.Sanitize(input));

    [Fact]
    public void Empty_lists_and_tables_go()
        => Assert.Equal("<p>x</p>", MinutesHtml.Sanitize("<ul></ul><table><tbody></tbody></table><ol>  </ol><p>x</p>"));

    [Theory]
    [InlineData("<p>a<script>x</script>b</p>")]
    [InlineData("chữ <b>rời</b><div>khối <i>nghiêng</i><ul><li>mục</li></ul></div> &amp; &nbsp; <em>x</em>")]
    [InlineData("<span><p>đoạn trong span</p><table><tr><td>ô</td></tr></table></span>")]
    [InlineData("<ul>chữ<li>mục</li><ul><li>con</li></ul></ul>")]
    [InlineData("<table><caption>tựa</caption><tr><td>a</td><th>b</th></tr><tfoot><tr><td>c</td></tr></tfoot></table>")]
    [InlineData("<p>a</p><!-- ghi chú --><p>b<!-- c -->d</p>")]
    [InlineData("<li>lạc</li><td>ô lạc</td><tr><td>dòng lạc</td></tr>")]
    [InlineData("<p><span style=\"color: rgb(192,0,0)\"><span style=\"font-size: 12pt\">lồng</span></span></p>")]
    [InlineData("<math><mi>x</mi></math><p>a\u0001b​c</p>")]
    [InlineData("<h1><p>x</p></h1><p><hr></p>")]
    public void Sanitizing_twice_changes_nothing(string html)
    {
        var once = MinutesHtml.Sanitize(html);
        Assert.Equal(once, MinutesHtml.Sanitize(once));
        Assert.DoesNotContain("<script", once, StringComparison.Ordinal);
        Assert.False(once.Contains('\u0001'), once);
    }

    [Theory]
    [InlineData("# BIÊN BẢN", "<h1>BIÊN BẢN</h1>")]
    [InlineData("## II. Nội dung", "<h2>II. Nội dung</h2>")]
    [InlineData("### 1. Báo cáo", "<h3>1. Báo cáo</h3>")]
    [InlineData("^ **Kết quả**", "<p style=\"text-align: center; text-indent: 0mm; margin-bottom: 12pt\"><strong>Kết quả</strong></p>")]
    [InlineData("~ - Lưu: VT.", "<p style=\"text-align: left; text-indent: 0mm; margin-bottom: 0pt\"><span style=\"font-size: 11pt\">- Lưu: VT.</span></p>")]
    [InlineData("**Địa điểm:** Phòng họp số 1.", "<p><strong>Địa điểm:</strong> Phòng họp số 1.</p>")]
    [InlineData("a **đậm __cả hai** nghiêng__ b", "<p>a <strong>đậm </strong><strong><em>cả hai</em></strong><em> nghiêng</em> b</p>")]
    [InlineData("1 < 2 & \"3\" > 0", "<p>1 &lt; 2 &amp; \"3\" &gt; 0</p>")]
    [InlineData("<script>alert(1)</script>", "<p>&lt;script&gt;alert(1)&lt;/script&gt;</p>")]
    [InlineData("#không cách\n####x", "<p>#không cách</p><p>####x</p>")]
    [InlineData("​# không phải tiêu đề\nA*​*b", "<p># không phải tiêu đề</p><p>A**b</p>")]
    [InlineData("dòng một\r\n\r\n\r\ndòng hai\n", "<p>dòng một</p><p>dòng hai</p>")]
    public void Lines_become_html(string lines, string expected)
    {
        var html = MinutesHtml.FromLines(lines);
        Assert.Equal(expected, html);
        Assert.Equal(html, MinutesHtml.Sanitize(html));
        if (!MinutesHtml.IsHtml(lines)) Assert.Equal(html, MinutesHtml.Normalize(lines));
    }

    [Fact]
    public void Two_column_rows_become_one_frame_table_cut_at_the_first_bar()
    {
        var html = MinutesHtml.FromLines("@@ TÊN CƠ QUAN | **CỘNG HÒA** | thêm\n@@ | \n@@ chỉ trái\nsau");
        const string cell = "<p style=\"text-align: center\">";
        Assert.Equal(
            "<table data-layout=\"frame\"><colgroup><col style=\"width: 250px\"><col style=\"width: 374px\"></colgroup><tbody>"
            + $"<tr><td colwidth=\"250\">{cell}TÊN CƠ QUAN</p></td><td colwidth=\"374\">{cell}<strong>CỘNG HÒA</strong> | thêm</p></td></tr>"
            + $"<tr><td colwidth=\"250\">{cell}</p></td><td colwidth=\"374\">{cell}</p></td></tr>"
            + $"<tr><td colwidth=\"250\">{cell}chỉ trái</p></td><td colwidth=\"374\">{cell}</p></td></tr>"
            + "</tbody></table><p>sau</p>", html);
        Assert.Equal(html, MinutesHtml.Sanitize(html));
    }

    [Fact]
    public void The_whole_template_converts_to_sanitized_html()
    {
        var lines = MinutesTemplate.Build(new MinutesRecords("Họp <giao ban> & tổng kết", DateTimeOffset.UnixEpoch, null, "Phòng 1",
            "Trần Chủ Trì", "Nguyễn Thư Ký", [new("Trần Chủ Trì", "Chủ trì", false)], [], []), null, SummaryWriter.FindHanoi());
        var html = MinutesHtml.FromLines(lines);
        Assert.Equal(html, MinutesHtml.Sanitize(html));
        Assert.Contains("<h1>BIÊN BẢN</h1>", html);
        Assert.Contains("<strong>Kết quả cuộc họp Họp &lt;giao ban&gt; &amp; tổng kết</strong>", html);
        Assert.Contains("<strong>Nguyễn Thư Ký</strong>", html);
        Assert.Contains("<span style=\"font-size: 11pt\"><strong><em>Nơi nhận:</em></strong></span>", html);
        Assert.Equal(2, html.Split("<table").Length - 1);
    }

    [Theory]
    [InlineData("<p>x</p>", true)]
    [InlineData("<p>……</p>", true)]
    [InlineData("<table><tr><td><p>chữ</p></td></tr></table>", true)]
    [InlineData("<ul><li><p><strong>a</strong></p></li></ul>", true)]
    [InlineData("<p></p><p> </p><p>&nbsp;</p><p><br></p><hr><hr data-page-break=\"true\">", false)]
    [InlineData("<p>​</p>", false)]
    [InlineData("<table><tr><td><p></p></td></tr></table>", false)]
    [InlineData("<script>chữ</script>", false)]
    [InlineData("", false)]
    public void Has_text_only_with_something_to_read(string html, bool expected)
        => Assert.Equal(expected, MinutesHtml.HasText(MinutesHtml.Sanitize(html)));

    [Theory]
    [InlineData("<p>x</p>", true)]
    [InlineData("  \n<h1>x</h1>", true)]
    [InlineData("# BIÊN BẢN", false)]
    [InlineData("@@ a | b", false)]
    public void Html_is_told_from_line_text_by_its_first_character(string content, bool html)
        => Assert.Equal(html, MinutesHtml.IsHtml(content));
}
