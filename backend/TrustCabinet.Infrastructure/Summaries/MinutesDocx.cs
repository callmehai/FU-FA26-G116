using System.IO.Compression;
using System.Security;
using System.Text;

namespace TrustCabinet.Infrastructure.Summaries;

/// <summary>
/// The minutes as a Word file (SCRUM-32), written by hand as the three parts a .docx needs, so no library comes in for a
/// few pages of text. It follows Decree 30/2020/NĐ-CP as eCab's template does: Times New Roman 13 pt (the title 14 pt),
/// A4, margins 20/15/20/30 mm, the first line of each paragraph indented 12.7 mm, the header and the signature blocks as
/// borderless two-column tables. It reads the same marks as the page (MinutesContracts.cs).
/// </summary>
public static class MinutesDocx
{
    private const int Body = 26; // half-points: 13 pt

    public static byte[] Build(string content)
    {
        using var buffer = new MemoryStream();
        using (var zip = new ZipArchive(buffer, ZipArchiveMode.Create, leaveOpen: true))
        {
            Add(zip, "[Content_Types].xml", """
                <?xml version="1.0" encoding="UTF-8" standalone="yes"?>
                <Types xmlns="http://schemas.openxmlformats.org/package/2006/content-types">
                <Default Extension="rels" ContentType="application/vnd.openxmlformats-package.relationships+xml"/>
                <Default Extension="xml" ContentType="application/xml"/>
                <Override PartName="/word/document.xml" ContentType="application/vnd.openxmlformats-officedocument.wordprocessingml.document.main+xml"/>
                </Types>
                """);
            Add(zip, "_rels/.rels", """
                <?xml version="1.0" encoding="UTF-8" standalone="yes"?>
                <Relationships xmlns="http://schemas.openxmlformats.org/package/2006/relationships">
                <Relationship Id="rId1" Type="http://schemas.openxmlformats.org/officeDocument/2006/relationships/officeDocument" Target="word/document.xml"/>
                </Relationships>
                """);
            Add(zip, "word/document.xml", Document(content));
        }
        return buffer.ToArray();
    }

    private static string Document(string content)
    {
        var body = new StringBuilder();
        var rows = new List<string>();
        void Flush()
        {
            if (rows.Count == 0) return;
            body.Append(Table(rows));
            rows.Clear();
        }
        foreach (var raw in content.Replace("\r", "", StringComparison.Ordinal).Split('\n'))
        {
            var line = raw.TrimEnd();
            if (line.StartsWith("@@", StringComparison.Ordinal))
            {
                rows.Add(line[2..].Trim());
                continue;
            }
            Flush();
            if (line.Length == 0) continue;
            if (line.StartsWith("# ", StringComparison.Ordinal)) body.Append(Paragraph(line[2..], "center", size: 28, bold: true, before: 240));
            else if (line.StartsWith("^ ", StringComparison.Ordinal)) body.Append(Paragraph(line[2..], "center", after: 240));
            else if (line.StartsWith("### ", StringComparison.Ordinal)) body.Append(Paragraph(line[4..], "both", indent: true, bold: true));
            else if (line.StartsWith("## ", StringComparison.Ordinal)) body.Append(Paragraph(line[3..], "both", indent: true, bold: true, before: 120));
            else if (line.StartsWith("~ ", StringComparison.Ordinal)) body.Append(Paragraph(line[2..], "left", size: 22, after: 0));
            else body.Append(Paragraph(line, "both", indent: true));
        }
        Flush();
        return $"""
            <?xml version="1.0" encoding="UTF-8" standalone="yes"?>
            <w:document xmlns:w="http://schemas.openxmlformats.org/wordprocessingml/2006/main">
            <w:body>{body}<w:sectPr><w:pgSz w:w="11906" w:h="16838"/><w:pgMar w:top="1134" w:right="850" w:bottom="1134" w:left="1701" w:header="720" w:footer="720" w:gutter="0"/></w:sectPr></w:body>
            </w:document>
            """;
    }

    /// <summary>A line's runs, cut at <c>**</c> (bold) and <c>__</c> (italic).</summary>
    private static string Runs(string text, int size, bool bold)
    {
        var runs = new StringBuilder();
        bool strong = bold, italic = false;
        var at = 0;
        while (at < text.Length)
        {
            var star = text.IndexOf("**", at, StringComparison.Ordinal);
            var under = text.IndexOf("__", at, StringComparison.Ordinal);
            var next = star < 0 ? under : under < 0 ? star : Math.Min(star, under);
            var end = next < 0 ? text.Length : next;
            if (end > at) runs.Append(Run(text[at..end], size, strong, italic));
            if (next < 0) break;
            if (next == star) strong = !strong;
            else italic = !italic;
            at = next + 2;
        }
        return runs.ToString();
    }

    private static string Run(string text, int size, bool bold, bool italic)
        => $"""<w:r><w:rPr><w:rFonts w:ascii="Times New Roman" w:hAnsi="Times New Roman" w:cs="Times New Roman"/>{(bold ? "<w:b/>" : "")}{(italic ? "<w:i/>" : "")}<w:sz w:val="{size}"/></w:rPr><w:t xml:space="preserve">{SecurityElement.Escape(text)}</w:t></w:r>""";

    private static string Paragraph(string text, string align, int size = Body, bool bold = false, int before = 0, int after = 60,
        bool indent = false)
        => $"""<w:p><w:pPr><w:spacing w:before="{before}" w:after="{after}" w:line="288" w:lineRule="auto"/><w:jc w:val="{align}"/>{(indent ? "<w:ind w:firstLine=\"720\"/>" : "")}</w:pPr>{Runs(text, size, bold)}</w:p>""";

    /// <summary>
    /// Two-column rows as one borderless table, each cell centred, as the template's header and signatures. The grid and the
    /// fixed layout keep the 40/60 split: without them a reader sizes the columns to their text and the two run together.
    /// </summary>
    private static string Table(IReadOnlyList<string> rows)
    {
        string Cell(string text, int width) => $"""<w:tc><w:tcPr><w:tcW w:w="{width}" w:type="dxa"/></w:tcPr>{Paragraph(text.Trim(), "center", after: 0)}</w:tc>""";
        var table = new StringBuilder("""<w:tbl><w:tblPr><w:tblW w:w="9355" w:type="dxa"/><w:tblBorders><w:top w:val="nil"/><w:left w:val="nil"/><w:bottom w:val="nil"/><w:right w:val="nil"/><w:insideH w:val="nil"/><w:insideV w:val="nil"/></w:tblBorders><w:tblLayout w:type="fixed"/></w:tblPr><w:tblGrid><w:gridCol w:w="3740"/><w:gridCol w:w="5615"/></w:tblGrid>""");
        foreach (var row in rows)
        {
            var halves = row.Split('|', 2);
            table.Append("<w:tr>").Append(Cell(halves[0], 3740)).Append(Cell(halves.Length > 1 ? halves[1] : "", 5615)).Append("</w:tr>");
        }
        return table.Append("</w:tbl>").ToString();
    }

    private static void Add(ZipArchive zip, string name, string xml)
    {
        using var writer = new StreamWriter(zip.CreateEntry(name, CompressionLevel.Optimal).Open(), new UTF8Encoding(false));
        writer.Write(xml.Trim());
    }
}
