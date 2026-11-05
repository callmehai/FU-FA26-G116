using System.Buffers.Binary;
using System.Globalization;
using System.Text;
using QuestPDF;
using QuestPDF.Drawing;
using QuestPDF.Fluent;
using QuestPDF.Helpers;
using QuestPDF.Infrastructure;

namespace TrustCabinet.Infrastructure.Summaries;

/// <summary>
/// The minutes as a PDF file (SCRUM-32), drawn on the server with QuestPDF from the sanitized HTML
/// (<see cref="MinutesHtml"/>, ADR-0055) as <see cref="MinutesLayout"/> lays it out, the same page as
/// <see cref="MinutesDocx"/>: A4, margins 20/15/20/30 mm (Decree 30/2020/NĐ-CP), a Times-like serif at 13 pt, justified
/// paragraphs with the first line indented 12.7 mm, tables across the text width. Older rows of line-marked text are
/// converted first, and each run is cleaned for the font only after the marks are read.
/// <para>
/// The runtime image has no Times New Roman, so the file carries Liberation Serif (SIL OFL 1.1, Fonts/LICENSE), which is
/// metric-compatible with it and covers Vietnamese. System fonts are off, so a server and a laptop draw the same file and
/// no letter is ever drawn from a font the reader did not choose. QuestPDF throws on a glyph the font lacks; a character
/// outside the font (an emoji the AI wrote) is replaced by "?" before drawing, so one odd character never fails the export.
/// </para>
/// </summary>
public static class MinutesPdf
{
    private const string Family = "Liberation Serif";
    // The font's own single line spacing, about 1.15 em: Word's "multiple 1.2" is 1.2 of that, 1.38 em.
    private const float SingleLine = 1.15f;
    private static readonly string[] Faces = ["Regular", "Bold", "Italic", "BoldItalic"];
    private static readonly HashSet<int> Covered;

    static MinutesPdf()
    {
        // A public student capstone: QuestPDF's Community license (learning and evaluation).
        Settings.License = LicenseType.Community;
        Settings.UseSystemFonts = false;
        Settings.FontDiscoveryPath = null;
        Settings.ThrowOnMissingTextGlyphs = true;
        HashSet<int>? covered = null;
        var assembly = typeof(MinutesPdf).Assembly;
        foreach (var face in Faces)
        {
            using var stream = assembly.GetManifestResourceStream($"TrustCabinet.Fonts.LiberationSerif-{face}.ttf")
                ?? throw new InvalidOperationException($"The font LiberationSerif-{face}.ttf is not embedded.");
            using var copy = new MemoryStream();
            stream.CopyTo(copy);
            var bytes = copy.ToArray();
            FontManager.RegisterFontFromBinaryData(bytes);
            // Only what every face draws, so a bold or an italic run never meets a missing glyph either.
            var faceCovers = FontCoverage.Read(bytes);
            if (covered is null) covered = faceCovers;
            else covered.IntersectWith(faceCovers);
        }
        Covered = covered!;
    }

    public static byte[] Build(string content)
    {
        var blocks = MinutesLayout.Read(content);
        return Document.Create(document => document.Page(page =>
        {
            page.Size(PageSizes.A4);
            page.MarginTop(20, Unit.Millimetre);
            page.MarginBottom(20, Unit.Millimetre);
            page.MarginLeft(30, Unit.Millimetre);
            page.MarginRight(15, Unit.Millimetre);
            page.PageColor(Colors.White);
            page.DefaultTextStyle(style => style.FontFamily(Family).FontSize(MinutesLayout.BodySize)
                .LineHeight(MinutesLayout.LineHeight * SingleLine).FontColor(Colors.Black));
            page.Content().Column(column =>
            {
                foreach (var block in blocks) Block(column, block);
            });
        })).WithMetadata(new DocumentMetadata { Title = "Biên bản", Creator = "TrustCabinet", Producer = "TrustCabinet" })
            .GeneratePdf();
    }

    private static void Block(ColumnDescriptor column, LayoutBlock block)
    {
        switch (block)
        {
            case LayoutParagraph paragraph:
                Paragraph(column.Item(), paragraph);
                break;
            case LayoutRule rule:
                column.Item().PaddingLeft(rule.LeftMm, Unit.Millimetre).PaddingTop(3).PaddingBottom(6).LineHorizontal(0.5f)
                    .LineColor(Colors.Black);
                break;
            case LayoutPageBreak:
                column.Item().PageBreak();
                break;
            case LayoutTable table:
                Table(column.Item(), table);
                break;
        }
    }

    /// <summary>
    /// A paragraph: its spacing as padding, its left indent as padding, and a list item's marker in a 6 mm column before the
    /// text, which is how a hanging indent looks. A negative first line (a hanging indent without a marker) is drawn flush.
    /// </summary>
    private static void Paragraph(IContainer item, LayoutParagraph p)
    {
        item = item.PaddingTop(p.BeforePt).PaddingBottom(p.AfterPt)
            .DefaultTextStyle(style => style.FontSize(p.Size).LineHeight(p.LineHeight * SingleLine));
        if (p.Marker is { } marker)
        {
            item.PaddingLeft(Math.Max(0, p.LeftMm - MinutesLayout.ListIndent), Unit.Millimetre).Row(row =>
            {
                row.ConstantItem(MinutesLayout.ListIndent, Unit.Millimetre).Text(text =>
                {
                    var span = text.Span(Clean(marker));
                    if (p.Bold) span.Bold();
                });
                row.RelativeItem().Text(text => Runs(text, p));
            });
            return;
        }
        item.PaddingLeft(Math.Max(0, p.LeftMm), Unit.Millimetre).Text(text =>
        {
            if (p.FirstLineMm > 0) text.ParagraphFirstLineIndentation(p.FirstLineMm, Unit.Millimetre);
            Runs(text, p);
        });
    }

    private static void Runs(TextDescriptor text, LayoutParagraph p)
    {
        switch (p.Align)
        {
            case Align.Center: text.AlignCenter(); break;
            case Align.Right: text.AlignRight(); break;
            case Align.Justify: text.Justify(); break;
            default: text.AlignLeft(); break;
        }
        var any = false;
        foreach (var run in p.Runs)
        {
            if (run.Break)
            {
                text.Span("\n").FontSize(run.Size);
                continue;
            }
            var clean = Clean(run.Text);
            if (clean.Length == 0) continue;
            var span = text.Span(clean).FontSize(run.Size);
            if (run.Bold) span.Bold();
            if (run.Italic) span.Italic();
            if (run.Underline) span.Underline();
            if (run.Strike) span.Strikethrough();
            if (run.Sub) span.Subscript();
            else if (run.Sup) span.Superscript();
            if (run.Color is { } color) span.FontColor(Color.FromHex(color));
            if (run.Background is { } background) span.BackgroundColor(Color.FromHex(background));
            any = true;
        }
        // An empty paragraph still takes a line, as an empty Word paragraph does: the gap left for a signature.
        if (!any) text.Span(" ").FontSize(p.Size);
    }

    /// <summary>
    /// A table across the width it has, its columns in the Word file's proportions, 0.5 pt black borders on every cell or none for a frame, the
    /// cells padded 1.9 mm left and right as Word's default cell margins.
    /// </summary>
    private static void Table(IContainer item, LayoutTable table)
    {
        item.PaddingLeft(table.LeftMm, Unit.Millimetre).Table(grid =>
        {
            grid.ColumnsDefinition(columns =>
            {
                foreach (var width in table.Columns) columns.RelativeColumn(width);
            });
            for (var r = 0; r < table.Rows.Count; r++)
                foreach (var cell in table.Rows[r].Cells.Where(c => !c.Continues))
                {
                    IContainer box = grid.Cell().Row((uint)r + 1).Column((uint)cell.Column + 1)
                        .ColumnSpan((uint)cell.Span).RowSpan((uint)cell.RowSpan);
                    if (!table.Frame) box = box.Border(0.5f).BorderColor(Colors.Black);
                    box = box.PaddingHorizontal(MinutesLayout.CellPadding, Unit.Millimetre);
                    box = cell.VerticalAlign switch
                    {
                        "middle" => box.AlignMiddle(),
                        "bottom" => box.AlignBottom(),
                        _ => box.AlignTop(),
                    };
                    box.Column(column =>
                    {
                        foreach (var block in cell.Blocks) Block(column, block);
                        if (cell.Blocks.Count == 0) column.Item().Text(" ");
                    });
                }
        });
    }

    /// <summary>
    /// The text as the font can draw it: composed (NFC) so a decomposed "ế" is one glyph, tabs and other controls as
    /// spaces, joiners and variation selectors dropped, and anything the font lacks as "?".
    /// </summary>
    private static string Clean(string run)
    {
        var text = run.Normalize(NormalizationForm.FormC);
        var clean = new StringBuilder(text.Length);
        foreach (var rune in text.EnumerateRunes())
        {
            if (Rune.IsControl(rune)) clean.Append(' ');
            else if (Rune.GetUnicodeCategory(rune) is UnicodeCategory.Format || rune.Value is >= 0xFE00 and <= 0xFE0F)
                continue;
            else if (Covered.Contains(rune.Value)) clean.Append(rune.ToString());
            else clean.Append('?');
        }
        return clean.ToString();
    }

    /// <summary>The code points a TrueType font maps to a glyph, read from its <c>cmap</c> table (format 4 or 12).</summary>
    private static class FontCoverage
    {
        public static HashSet<int> Read(byte[] font)
        {
            var span = font.AsSpan();
            int tables = U16(span, 4), cmap = -1;
            for (var i = 0; i < tables; i++)
            {
                var record = 12 + 16 * i;
                if (Encoding.ASCII.GetString(span.Slice(record, 4)) == "cmap") cmap = (int)U32(span, record + 8);
            }
            if (cmap < 0) throw new InvalidOperationException("The font has no cmap table.");
            int best = -1, bestFormat = 0;
            for (var i = 0; i < U16(span, cmap + 2); i++)
            {
                var record = cmap + 4 + 8 * i;
                int platform = U16(span, record), encoding = U16(span, record + 2);
                var offset = cmap + (int)U32(span, record + 4);
                var format = U16(span, offset);
                var unicode = platform == 0 || (platform == 3 && encoding is 1 or 10);
                if (unicode && (format == 12 || (format == 4 && bestFormat != 12)))
                {
                    best = offset;
                    bestFormat = format;
                }
            }
            if (best < 0) throw new InvalidOperationException("The font has no Unicode cmap subtable.");
            return bestFormat == 12 ? Format12(span, best) : Format4(span, best);
        }

        private static HashSet<int> Format4(ReadOnlySpan<byte> span, int table)
        {
            var covered = new HashSet<int>();
            var segments = U16(span, table + 6) / 2;
            int ends = table + 14, starts = ends + 2 * segments + 2, deltas = starts + 2 * segments, ranges = deltas + 2 * segments;
            for (var s = 0; s < segments; s++)
            {
                int end = U16(span, ends + 2 * s), start = U16(span, starts + 2 * s), delta = U16(span, deltas + 2 * s);
                int rangeAt = ranges + 2 * s, range = U16(span, rangeAt);
                for (var code = start; code <= end && code != 0xFFFF; code++)
                {
                    var glyph = range == 0 ? (code + delta) & 0xFFFF : U16(span, rangeAt + range + 2 * (code - start));
                    if (range != 0 && glyph != 0) glyph = (glyph + delta) & 0xFFFF;
                    if (glyph != 0) covered.Add(code);
                }
            }
            return covered;
        }

        private static HashSet<int> Format12(ReadOnlySpan<byte> span, int table)
        {
            var covered = new HashSet<int>();
            var groups = U32(span, table + 12);
            for (var g = 0; g < groups; g++)
            {
                var group = table + 16 + 12 * g;
                uint start = U32(span, group), end = U32(span, group + 4), glyph = U32(span, group + 8);
                for (var code = start; code <= end; code++, glyph++)
                    if (glyph != 0) covered.Add((int)code);
            }
            return covered;
        }

        private static int U16(ReadOnlySpan<byte> span, int at) => BinaryPrimitives.ReadUInt16BigEndian(span[at..]);
        private static uint U32(ReadOnlySpan<byte> span, int at) => BinaryPrimitives.ReadUInt32BigEndian(span[at..]);
    }
}
