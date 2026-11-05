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
/// The minutes as a PDF file (SCRUM-32), drawn on the server with QuestPDF from the same marks the page and
/// <see cref="MinutesDocx"/> read (MinutesContracts.cs), on the same sheet: A4, margins 20/15/20/30 mm (Decree
/// 30/2020/NĐ-CP), a Times-like serif at 13 pt (the title 14 pt), justified paragraphs with the first line indented
/// 12.7 mm, the header and the signature blocks as borderless two-column tables split 3740:5615 like the Word file.
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
    private const float Body = 13;
    private const float Title = 14;
    private const float Small = 11;
    // Word's "multiple 1.2" over the font's own single spacing (about 1.15 em for this font), as the Word file sets.
    private const float LineHeight = 1.38f;
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
        var blocks = new List<Action<ColumnDescriptor>>();
        var rows = new List<string>();
        void Flush()
        {
            if (rows.Count == 0) return;
            var table = rows.ToArray();
            blocks.Add(column => Table(column, table));
            rows.Clear();
        }
        foreach (var raw in Clean(content).Split('\n'))
        {
            var line = raw.TrimEnd();
            if (line.StartsWith("@@", StringComparison.Ordinal))
            {
                rows.Add(line[2..].Trim());
                continue;
            }
            Flush();
            if (line.Length == 0) continue;
            if (line.StartsWith("# ", StringComparison.Ordinal))
                blocks.Add(c => Paragraph(c, line[2..], Align.Center, size: Title, bold: true, before: 12));
            else if (line.StartsWith("^ ", StringComparison.Ordinal))
                blocks.Add(c => Paragraph(c, line[2..], Align.Center, after: 12));
            else if (line.StartsWith("### ", StringComparison.Ordinal))
                blocks.Add(c => Paragraph(c, line[4..], Align.Justify, indent: true, bold: true));
            else if (line.StartsWith("## ", StringComparison.Ordinal))
                blocks.Add(c => Paragraph(c, line[3..], Align.Justify, indent: true, bold: true, before: 6));
            else if (line.StartsWith("~ ", StringComparison.Ordinal))
                blocks.Add(c => Paragraph(c, line[2..], Align.Left, size: Small, after: 0));
            else blocks.Add(c => Paragraph(c, line, Align.Justify, indent: true));
        }
        Flush();

        return Document.Create(document => document.Page(page =>
        {
            page.Size(PageSizes.A4);
            page.MarginTop(20, Unit.Millimetre);
            page.MarginBottom(20, Unit.Millimetre);
            page.MarginLeft(30, Unit.Millimetre);
            page.MarginRight(15, Unit.Millimetre);
            page.PageColor(Colors.White);
            page.DefaultTextStyle(style => style.FontFamily(Family).FontSize(Body).LineHeight(LineHeight).FontColor(Colors.Black));
            page.Content().Column(column =>
            {
                foreach (var block in blocks) block(column);
            });
        })).WithMetadata(new DocumentMetadata { Title = "Biên bản", Creator = "TrustCabinet", Producer = "TrustCabinet" })
            .GeneratePdf();
    }

    private enum Align { Left, Center, Justify }

    private static void Paragraph(ColumnDescriptor column, string text, Align align, float size = Body, bool bold = false,
        float before = 0, float after = 3, bool indent = false)
    {
        var item = column.Item().PaddingTop(before).PaddingBottom(after);
        item.Text(block =>
        {
            if (align == Align.Center) block.AlignCenter();
            else if (align == Align.Justify) block.Justify();
            else block.AlignLeft();
            if (indent) block.ParagraphFirstLineIndentation(12.7f, Unit.Millimetre);
            Runs(block, text, size, bold);
        });
    }

    /// <summary>A line's runs, cut at <c>**</c> (bold) and <c>__</c> (italic), as in the Word file.</summary>
    private static void Runs(TextDescriptor block, string text, float size, bool bold)
    {
        bool strong = bold, italic = false;
        var at = 0;
        var any = false;
        while (at < text.Length)
        {
            var star = text.IndexOf("**", at, StringComparison.Ordinal);
            var under = text.IndexOf("__", at, StringComparison.Ordinal);
            var next = star < 0 ? under : under < 0 ? star : Math.Min(star, under);
            var end = next < 0 ? text.Length : next;
            if (end > at)
            {
                var span = block.Span(text[at..end]).FontSize(size);
                if (strong) span.Bold();
                if (italic) span.Italic();
                any = true;
            }
            if (next < 0) break;
            if (next == star) strong = !strong;
            else italic = !italic;
            at = next + 2;
        }
        // An empty cell still takes a line, as an empty Word paragraph does: the gap left for a signature.
        if (!any) block.Span(" ").FontSize(size);
    }

    /// <summary>
    /// Two-column rows as one borderless table, each cell centred, the 40/60 split of the Word file across the whole text
    /// width (9355 twips = 165 mm), with Word's default 1.9 mm cell margins and the page's small gap above and below.
    /// </summary>
    private static void Table(ColumnDescriptor column, IReadOnlyList<string> rows)
        => column.Item().PaddingTop(3).PaddingBottom(6).Table(table =>
        {
            table.ColumnsDefinition(columns =>
            {
                columns.RelativeColumn(3740);
                columns.RelativeColumn(5615);
            });
            foreach (var row in rows)
            {
                var halves = row.Split('|', 2);
                foreach (var half in new[] { halves[0], halves.Length > 1 ? halves[1] : "" })
                {
                    table.Cell().PaddingHorizontal(1.9f, Unit.Millimetre).Text(block =>
                    {
                        block.AlignCenter();
                        Runs(block, half.Trim(), Body, bold: false);
                    });
                }
            }
        });

    /// <summary>
    /// The text as the font can draw it: composed (NFC) so a decomposed "ế" is one glyph, tabs and other controls as
    /// spaces, joiners and variation selectors dropped, and anything the font lacks as "?".
    /// </summary>
    private static string Clean(string content)
    {
        var text = content.Replace("\r", "", StringComparison.Ordinal).Normalize(NormalizationForm.FormC);
        var clean = new StringBuilder(text.Length);
        foreach (var rune in text.EnumerateRunes())
        {
            if (rune.Value == '\n') clean.Append('\n');
            else if (Rune.IsControl(rune)) clean.Append(' ');
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
