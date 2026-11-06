using System.Globalization;
using System.IO.Compression;
using System.Security;
using System.Text;
using System.Xml;

namespace TrustCabinet.Infrastructure.Summaries;

/// <summary>
/// The minutes as a Word file (SCRUM-32), written by hand as the three parts a .docx needs, so no library comes in for a
/// few pages of text. It draws the sanitized HTML (<see cref="MinutesHtml"/>, ADR-0055) as <see cref="MinutesLayout"/> lays
/// it out, the same page as <see cref="MinutesPdf"/>: Times New Roman 13 pt, A4, margins 20/15/20/30 mm (Decree
/// 30/2020/NĐ-CP), lists as paragraphs with a hanging marker, tables with a fixed grid. Older rows of line-marked text are
/// converted first.
/// </summary>
public static class MinutesDocx
{
    private const string Font = """<w:rFonts w:ascii="Times New Roman" w:hAnsi="Times New Roman" w:cs="Times New Roman"/>""";

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
        LayoutBlock? previous = null;
        foreach (var block in MinutesLayout.Read(content))
        {
            // Two tables in a row would merge into one in Word: a paragraph keeps them apart, as the editor shows them.
            if (block is LayoutTable && previous is LayoutTable) body.Append(Empty());
            Block(body, block);
            previous = block;
        }
        return $"""
            <?xml version="1.0" encoding="UTF-8" standalone="yes"?>
            <w:document xmlns:w="http://schemas.openxmlformats.org/wordprocessingml/2006/main">
            <w:body>{body}<w:sectPr><w:pgSz w:w="11906" w:h="16838"/><w:pgMar w:top="1134" w:right="850" w:bottom="1134" w:left="1701" w:header="720" w:footer="720" w:gutter="0"/></w:sectPr></w:body>
            </w:document>
            """;
    }

    private static void Block(StringBuilder body, LayoutBlock block)
    {
        switch (block)
        {
            case LayoutParagraph paragraph:
                body.Append(Paragraph(paragraph));
                break;
            case LayoutRule rule:
                // An empty paragraph whose bottom border is the rule.
                body.Append($"""<w:p><w:pPr><w:pBdr><w:bottom w:val="single" w:sz="4" w:space="1" w:color="000000"/></w:pBdr><w:spacing w:before="0" w:after="120" w:line="240" w:lineRule="auto"/>{Indent(rule.LeftMm, 0)}<w:rPr><w:sz w:val="8"/></w:rPr></w:pPr></w:p>""");
                break;
            case LayoutPageBreak:
                body.Append("""<w:p><w:r><w:br w:type="page"/></w:r></w:p>""");
                break;
            case LayoutTable table:
                body.Append(Table(table));
                break;
        }
    }

    private static string Paragraph(LayoutParagraph p)
    {
        var xml = new StringBuilder("<w:p><w:pPr>");
        xml.Append(CultureInfo.InvariantCulture,
            $"""<w:spacing w:before="{Math.Round(p.BeforePt * 20)}" w:after="{Math.Round(p.AfterPt * 20)}" w:line="{Math.Round(p.LineHeight * 240)}" w:lineRule="auto"/>""");
        xml.Append(p.Marker is null ? Indent(p.LeftMm, p.FirstLineMm) : Hanging(p.LeftMm));
        xml.Append($"""<w:jc w:val="{Justification(p.Align)}"/>""");
        xml.Append($"<w:rPr>{Font}{(p.Bold ? "<w:b/>" : "")}{Size(p.Size)}</w:rPr></w:pPr>");
        if (p.Marker is not null)
            xml.Append($"<w:r><w:rPr>{Font}{(p.Bold ? "<w:b/>" : "")}{Size(p.Size)}</w:rPr><w:t>{Text(p.Marker)}</w:t><w:tab/></w:r>");
        foreach (var run in p.Runs) xml.Append(Run(run));
        return xml.Append("</w:p>").ToString();
    }

    private static string Indent(float leftMm, float firstLineMm)
    {
        var left = MinutesLayout.Twips(leftMm);
        var first = MinutesLayout.Twips(firstLineMm);
        if (left == 0 && first == 0) return "";
        var line = first > 0 ? $" w:firstLine=\"{first}\"" : first < 0 ? $" w:hanging=\"{-first}\"" : "";
        return $"""<w:ind w:left="{left}"{line}/>""";
    }

    /// <summary>A list item: the text starts at the left indent, the marker hangs before it and a tab reaches the text.</summary>
    private static string Hanging(float leftMm)
        => $"""<w:ind w:left="{MinutesLayout.Twips(leftMm)}" w:hanging="{MinutesLayout.Twips(MinutesLayout.ListIndent)}"/>""";

    private static string Run(LayoutRun run)
    {
        if (run.Break) return "<w:r><w:br/></w:r>";
        var props = new StringBuilder(Font);
        if (run.Bold) props.Append("<w:b/>");
        if (run.Italic) props.Append("<w:i/>");
        if (run.Strike) props.Append("<w:strike/>");
        if (run.Color is { } color) props.Append($"""<w:color w:val="{color[1..].ToUpperInvariant()}"/>""");
        props.Append(Size(run.Size));
        if (run.Underline) props.Append("""<w:u w:val="single"/>""");
        if (run.Background is { } fill) props.Append($"""<w:shd w:val="clear" w:color="auto" w:fill="{fill[1..].ToUpperInvariant()}"/>""");
        if (run.Sub) props.Append("""<w:vertAlign w:val="subscript"/>""");
        else if (run.Sup) props.Append("""<w:vertAlign w:val="superscript"/>""");
        return $"""<w:r><w:rPr>{props}</w:rPr><w:t xml:space="preserve">{Text(run.Text)}</w:t></w:r>""";
    }

    private static string Size(float points)
    {
        var half = (int)Math.Round(points * 2);
        return $"""<w:sz w:val="{half}"/><w:szCs w:val="{half}"/>""";
    }

    private static string Justification(Align align) => align switch
    {
        Align.Center => "center",
        Align.Right => "right",
        Align.Justify => "both",
        _ => "left",
    };

    /// <summary>
    /// A table on a fixed grid, so a reader keeps the widths rather than sizing columns to their text. Borders are 0.5 pt
    /// black on every cell, none for a frame (the header and the signature blocks); cells keep Word's 1.9 mm margins.
    /// </summary>
    private static string Table(LayoutTable table)
    {
        var border = table.Frame ? "w:val=\"nil\"" : "w:val=\"single\" w:sz=\"4\" w:space=\"0\" w:color=\"000000\"";
        var borders = string.Concat(new[] { "top", "left", "bottom", "right", "insideH", "insideV" }
            .Select(side => $"<w:{side} {border}/>"));
        var xml = new StringBuilder("<w:tbl><w:tblPr>");
        xml.Append($"""<w:tblW w:w="{table.Columns.Sum()}" w:type="dxa"/>""");
        if (MinutesLayout.Twips(table.LeftMm) is var indent and > 0) xml.Append($"""<w:tblInd w:w="{indent}" w:type="dxa"/>""");
        xml.Append($"<w:tblBorders>{borders}</w:tblBorders>")
            .Append("""<w:tblLayout w:type="fixed"/>""")
            .Append($"""<w:tblCellMar><w:left w:w="{MinutesLayout.CellPaddingTwips}" w:type="dxa"/><w:right w:w="{MinutesLayout.CellPaddingTwips}" w:type="dxa"/></w:tblCellMar>""")
            .Append("</w:tblPr><w:tblGrid>");
        foreach (var width in table.Columns) xml.Append($"""<w:gridCol w:w="{width}"/>""");
        xml.Append("</w:tblGrid>");
        foreach (var row in table.Rows)
        {
            // A frame row (the header, the signature blocks) never splits across pages, as the PDF draws it.
            xml.Append(table.Frame ? "<w:tr><w:trPr><w:cantSplit/></w:trPr>" : "<w:tr>");
            foreach (var cell in row.Cells)
            {
                var width = table.Columns.Skip(cell.Column).Take(cell.Span).Sum();
                xml.Append($"""<w:tc><w:tcPr><w:tcW w:w="{width}" w:type="dxa"/>""");
                if (cell.Span > 1) xml.Append($"""<w:gridSpan w:val="{cell.Span}"/>""");
                if (cell.Continues) xml.Append("<w:vMerge/>");
                else if (cell.RowSpan > 1) xml.Append("""<w:vMerge w:val="restart"/>""");
                xml.Append($"""<w:vAlign w:val="{(cell.VerticalAlign == "middle" ? "center" : cell.VerticalAlign)}"/></w:tcPr>""");
                var inner = new StringBuilder();
                foreach (var block in cell.Blocks) Block(inner, block);
                // A cell must end with a paragraph: empty, or after a table nested in it.
                if (cell.Blocks.Count == 0 || cell.Blocks[^1] is LayoutTable) inner.Append(Empty());
                xml.Append(inner).Append("</w:tc>");
            }
            xml.Append("</w:tr>");
        }
        return xml.Append("</w:tbl>").ToString();
    }

    private static string Empty()
        => $"""<w:p><w:pPr><w:spacing w:before="0" w:after="0" w:line="{Math.Round(MinutesLayout.LineHeight * 240)}" w:lineRule="auto"/><w:rPr>{Font}{Size(MinutesLayout.BodySize)}</w:rPr></w:pPr></w:p>""";

    /// <summary>Text as XML: escaped, and without the few characters XML cannot carry at all.</summary>
    private static string Text(string text)
    {
        var kept = new StringBuilder(text.Length);
        for (var i = 0; i < text.Length; i++)
        {
            if (i + 1 < text.Length && XmlConvert.IsXmlSurrogatePair(text[i + 1], text[i]))
            {
                kept.Append(text, i, 2);
                i++;
            }
            else if (XmlConvert.IsXmlChar(text[i])) kept.Append(text[i]);
        }
        return SecurityElement.Escape(kept.ToString());
    }

    private static void Add(ZipArchive zip, string name, string xml)
    {
        using var writer = new StreamWriter(zip.CreateEntry(name, CompressionLevel.Optimal).Open(), new UTF8Encoding(false));
        writer.Write(xml.Trim());
    }
}
