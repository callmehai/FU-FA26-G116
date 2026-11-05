using System.Globalization;
using System.Text;
using System.Text.RegularExpressions;

namespace TrustCabinet.Infrastructure.Summaries;

/// <summary>
/// The sanitized minutes (<see cref="MinutesHtml"/>) laid out once, so <see cref="MinutesDocx"/> and
/// <see cref="MinutesPdf"/> draw the same page: every default of Decree 30/2020/NĐ-CP (ADR-0055) is settled here, and the
/// two renderers only translate paragraphs, runs, rules and tables into their own format.
/// <list type="bullet">
/// <item>p: justified, first line 12.7 mm, 3 pt after; inside a cell or a list item: flush left, no indent, nothing after.</item>
/// <item>h1: centred, bold, 14 pt, 12 pt before, 3 pt after. h2: justified, bold, first line 12.7 mm, 6 pt before, 3 pt
/// after. h3: as h2 with nothing before.</item>
/// <item>A list item: a hanging indent of 6 mm from the list's margin (6 mm more per level), "•" or "n.".</item>
/// <item>A table: the whole text width (165 mm), 0.5 pt borders (none when <c>data-layout="frame"</c>), 1.9 mm cell
/// padding, the columns sharing the width in the ratio of the first row's <c>colwidth</c> pixels.</item>
/// </list>
/// Explicit styles override the defaults. <c>line-height</c> is Word's "multiple" line spacing (1.2 by default).
/// </summary>
internal static partial class MinutesLayout
{
    public const float BodySize = 13;
    public const float TitleSize = 14;
    public const float LineHeight = 1.2f;
    public const float FirstLine = 12.7f;
    public const float ListIndent = 6;
    public const float CellPadding = 1.9f;
    /// <summary>The text width of the A4 page between the 30 mm and 15 mm margins: 165 mm.</summary>
    public const int TextWidth = 9355;
    public const int CellPaddingTwips = 108;
    /// <summary>Word's limit on a table's columns.</summary>
    public const int MaxColumns = 63;

    public static IReadOnlyList<LayoutBlock> Read(string content)
    {
        var output = new List<LayoutBlock>();
        Blocks(MinutesHtml.Read(content), new Scope(), output);
        return output;
    }

    /// <summary>Where a block sits: the document, a list item or a table cell.</summary>
    private sealed record Scope(bool Compact = false, float LeftMm = 0, Align? Align = null, float? LineHeight = null,
        bool Bold = false, int Available = TextWidth, bool TopLevel = true);

    private static void Blocks(IEnumerable<HtmlElement> elements, Scope scope, List<LayoutBlock> output)
    {
        foreach (var element in elements) Block(element, scope, output);
    }

    private static void Block(HtmlElement element, Scope scope, List<LayoutBlock> output)
    {
        switch (element.Name)
        {
            case "hr":
                if (element.Attribute("data-page-break") == "true")
                {
                    // A page break only means something between the document's own blocks.
                    if (scope.TopLevel) output.Add(new LayoutPageBreak());
                }
                else output.Add(new LayoutRule(scope.LeftMm));
                break;
            case "ul" or "ol":
                List(element, scope, output);
                break;
            case "table":
                output.Add(Table(element, scope));
                break;
            default:
                output.Add(Paragraph(element, scope, marker: null));
                break;
        }
    }

    private static LayoutParagraph Paragraph(HtmlElement element, Scope scope, string? marker)
    {
        var heading = element.Name is "h1" or "h2" or "h3";
        Align align;
        float first = 0, before = 0, after = 3, size = BodySize;
        switch (element.Name)
        {
            case "h1":
                align = Align.Center;
                size = TitleSize;
                before = 12;
                break;
            case "h2":
                align = Align.Justify;
                first = FirstLine;
                before = 6;
                break;
            case "h3":
                align = Align.Justify;
                first = FirstLine;
                break;
            default:
                align = scope.Compact ? Align.Left : Align.Justify;
                first = scope.Compact ? 0 : FirstLine;
                after = scope.Compact ? 0 : 3;
                break;
        }
        if (scope.Align is { } inherited) align = inherited;
        var style = element.Style;
        if (style.TryGetValue("text-align", out var a)) align = ToAlign(a);
        if (style.TryGetValue("text-indent", out var indent)) first = Millimetres(indent);
        // A left indent never leaves less than a centimetre for the text: in a narrow cell the writer's indent steps would
        // otherwise push the text past the cell's edge, which the PDF cannot lay out at all.
        var room = Math.Max(0, scope.Available * 25.4f / 1440 - 10);
        var left = scope.LeftMm + Math.Min(room, style.TryGetValue("margin-left", out var margin) ? Millimetres(margin) : 0);
        first = Math.Min(first, Math.Max(0, room - (left - scope.LeftMm)));
        if (style.TryGetValue("margin-top", out var top)) before = Points(top);
        if (style.TryGetValue("margin-bottom", out var bottom)) after = Points(bottom);
        var lineHeight = style.TryGetValue("line-height", out var lh) ? Number(lh) : scope.LineHeight ?? LineHeight;
        if (marker is not null) first = 0;
        var runs = new List<LayoutRun>();
        Runs(element.Children, new RunStyle(heading || scope.Bold, false, false, false, false, false, null, null, size), runs);
        return new LayoutParagraph(align, left, first, before, after, lineHeight, size, heading || scope.Bold, marker, Trim(runs));
    }

    private static void List(HtmlElement list, Scope scope, List<LayoutBlock> output)
    {
        // Nested lists step right only while a centimetre of text is left beside the marker.
        var room = Math.Max(0, scope.Available * 25.4f / 1440 - 10 - ListIndent);
        var markerAt = scope.LeftMm + Math.Min(room, list.Style.TryGetValue("margin-left", out var margin) ? Millimetres(margin) : 0);
        var step = room - (markerAt - scope.LeftMm) >= ListIndent ? ListIndent : 0;
        var number = list.Attribute("start") is { } start ? int.Parse(start, CultureInfo.InvariantCulture) : 1;
        foreach (var item in list.Elements.Where(e => e.Name == "li"))
        {
            var marker = list.Name == "ol" ? $"{number++}." : "•";
            var inner = scope with
            {
                Compact = true,
                LeftMm = markerAt + step,
                Align = item.Style.TryGetValue("text-align", out var a) ? ToAlign(a) : scope.Align,
                LineHeight = item.Style.TryGetValue("line-height", out var lh) ? Number(lh) : scope.LineHeight,
                Available = Math.Max(567, scope.Available - Twips(markerAt + step - scope.LeftMm)),
                TopLevel = false,
            };
            var blocks = item.Elements.ToList();
            if (blocks.FirstOrDefault() is { Name: "p" or "h1" or "h2" or "h3" } first)
            {
                output.Add(Paragraph(first, inner, marker));
                blocks.RemoveAt(0);
            }
            else output.Add(new LayoutParagraph(inner.Align ?? Align.Left, inner.LeftMm, 0, 0, 0, inner.LineHeight ?? LineHeight,
                BodySize, scope.Bold, marker, []));
            Blocks(blocks, inner, output);
        }
    }

    private static LayoutTable Table(HtmlElement table, Scope scope)
    {
        var rows = table.Elements.Where(e => e.Name is "thead" or "tbody")
            .SelectMany(s => s.Elements.Where(r => r.Name == "tr")).ToList();

        // Place every cell on the grid, skipping the places a row span from above still holds. Word stops at 63 columns;
        // cells past that are left out, so a hostile colspan cannot grow the grid (or the memory) without bound.
        var taken = new Dictionary<(int Row, int Column), (int Row, int Column, int Span, int RowSpan)>();
        var placed = new List<List<(HtmlElement Cell, int Column, int Span, int RowSpan)>>();
        var columns = 0;
        for (var r = 0; r < rows.Count; r++)
        {
            var line = new List<(HtmlElement, int, int, int)>();
            var c = 0;
            foreach (var cell in rows[r].Elements)
            {
                while (taken.ContainsKey((r, c))) c++;
                if (c >= MaxColumns) break;
                var span = Math.Min(Int(cell.Attribute("colspan")), MaxColumns - c);
                var rowSpan = Math.Min(Int(cell.Attribute("rowspan")), rows.Count - r);
                for (var rr = r; rr < r + rowSpan; rr++)
                    for (var cc = c; cc < c + span; cc++) taken.TryAdd((rr, cc), (r, c, span, rowSpan));
                line.Add((cell, c, span, rowSpan));
                c += span;
            }
            placed.Add(line);
            columns = Math.Max(columns, c);
        }
        if (taken.Count > 0) columns = Math.Max(columns, taken.Keys.Max(t => t.Column) + 1);

        var widths = Widths(table, placed.FirstOrDefault() ?? [], columns, scope.Available);
        var frame = table.Attribute("data-layout") == "frame";
        var layoutRows = new List<LayoutRow>();
        for (var r = 0; r < placed.Count; r++)
        {
            var cells = new List<LayoutCell>();
            var starts = placed[r].ToDictionary(p => p.Column);
            for (var c = 0; c < columns;)
            {
                if (starts.TryGetValue(c, out var start))
                {
                    var width = widths.Skip(c).Take(start.Span).Sum();
                    cells.Add(Cell(start.Cell, c, start.Span, start.RowSpan, width));
                    c += start.Span;
                }
                else if (taken.TryGetValue((r, c), out var above) && above.Row < r)
                {
                    // Held by a cell spanning down from a row above: Word needs a continuation cell here.
                    cells.Add(new LayoutCell(above.Column, above.Span, 1, true, "top", []));
                    c = above.Column + above.Span;
                }
                else
                {
                    cells.Add(new LayoutCell(c, 1, 1, false, "top", []));
                    c++;
                }
            }
            layoutRows.Add(new LayoutRow(cells));
        }
        return new LayoutTable(scope.LeftMm, widths, frame, layoutRows);
    }

    private static LayoutCell Cell(HtmlElement cell, int column, int span, int rowSpan, int width)
    {
        var header = cell.Name == "th";
        var scope = new Scope(Compact: true, Align: cell.Style.TryGetValue("text-align", out var a) ? ToAlign(a) : header ? Align.Center : null,
            Bold: header, Available: Math.Max(width - 2 * CellPaddingTwips, 567), TopLevel: false);
        var blocks = new List<LayoutBlock>();
        Blocks(cell.Elements, scope, blocks);
        var vertical = cell.Style.GetValueOrDefault("vertical-align", "top");
        return new LayoutCell(column, span, rowSpan, false, vertical, blocks);
    }

    /// <summary>
    /// Column widths in twips. The table always spans the width it has (the whole 165 mm text width on the page); the
    /// first row's <c>colwidth</c> (or the colgroup's <c>width</c>) only gives each column's share, since the editor
    /// rescales its pixels to its own sheet. A column without a width gets the average of the others, all equal when none
    /// has one.
    /// </summary>
    private static int[] Widths(HtmlElement table, List<(HtmlElement Cell, int Column, int Span, int RowSpan)> first, int columns,
        int available)
    {
        var px = new double?[columns];
        foreach (var (cell, column, span, _) in first)
        {
            var given = cell.Attribute("colwidth")?.Split(',');
            for (var i = 0; i < span && given is not null && i < given.Length && column + i < columns; i++)
                px[column + i] = int.Parse(given[i], CultureInfo.InvariantCulture);
        }
        var cols = table.Elements.Where(e => e.Name == "colgroup").SelectMany(g => g.Elements).ToList();
        for (var i = 0; i < columns && i < cols.Count; i++)
            if (px[i] is null && cols[i].Style.TryGetValue("width", out var w) && Number(w[..^2]) is var n and > 0)
                px[i] = n;

        var known = px.Where(p => p is > 0).Select(p => p!.Value).ToList();
        var average = known.Count > 0 ? known.Average() : 1;
        var shares = px.Select(p => p is > 0 ? p.Value : average).ToArray();
        var total = shares.Sum();
        // Each column's share of the width, the last one taking the rounding so the table is exactly as wide as it may be.
        var widths = shares.Select(share => Math.Max(1, (int)Math.Round(share * available / total))).ToArray();
        if (widths.Length > 0) widths[^1] = Math.Max(1, widths[^1] + available - widths.Sum());
        return widths;
    }

    // ---- Runs ------------------------------------------------------------------------------------------------------

    private sealed record RunStyle(bool Bold, bool Italic, bool Underline, bool Strike, bool Sub, bool Sup, string? Color,
        string? Background, float Size);

    private static void Runs(IEnumerable<HtmlNode> nodes, RunStyle style, List<LayoutRun> output)
    {
        foreach (var node in nodes)
        {
            if (node is HtmlText text)
            {
                var value = Spaces().Replace(text.Text, " ");
                if (value.Length > 0)
                    output.Add(new LayoutRun(value, style.Bold, style.Italic, style.Underline, style.Strike, style.Sub, style.Sup,
                        style.Color, style.Background, style.Size, false));
                continue;
            }
            var element = (HtmlElement)node;
            var inner = element.Name switch
            {
                "strong" => style with { Bold = true },
                "em" => style with { Italic = true },
                "u" => style with { Underline = true },
                "s" => style with { Strike = true },
                "sub" => style with { Sub = true, Sup = false },
                "sup" => style with { Sup = true, Sub = false },
                "span" => style with
                {
                    Color = element.Style.GetValueOrDefault("color", style.Color!),
                    Background = element.Style.GetValueOrDefault("background-color", style.Background!),
                    Size = element.Style.TryGetValue("font-size", out var size) ? Points(size) : style.Size,
                },
                _ => style,
            };
            if (element.Name == "br")
                output.Add(new LayoutRun("", style.Bold, style.Italic, false, false, false, false, null, null, style.Size, true));
            else Runs(element.Children, inner, output);
        }
    }

    /// <summary>Spaces at the start and the end of a paragraph are not drawn, as a browser does not draw them.</summary>
    private static List<LayoutRun> Trim(List<LayoutRun> runs)
    {
        var first = runs.FindIndex(r => !r.Break);
        if (first >= 0) runs[first] = runs[first] with { Text = runs[first].Text.TrimStart(' ') };
        var last = runs.FindLastIndex(r => !r.Break);
        if (last >= 0) runs[last] = runs[last] with { Text = runs[last].Text.TrimEnd(' ') };
        runs.RemoveAll(r => !r.Break && r.Text.Length == 0);
        return runs;
    }

    // ---- Units -----------------------------------------------------------------------------------------------------

    public static int Twips(float mm) => (int)Math.Round(mm * 1440 / 25.4);

    private static float Millimetres(string length) => length.EndsWith("pt", StringComparison.Ordinal)
        ? Number(length[..^2]) * 25.4f / 72
        : Number(length[..^2]);

    private static float Points(string length) => Number(length[..^2]);

    private static float Number(string value) => float.Parse(value, NumberStyles.Float, CultureInfo.InvariantCulture);

    private static int Int(string? value) => value is null ? 1 : int.Parse(value, CultureInfo.InvariantCulture);

    private static Align ToAlign(string value) => value switch
    {
        "center" => Align.Center,
        "right" => Align.Right,
        "justify" => Align.Justify,
        _ => Align.Left,
    };

    [GeneratedRegex(@"[ \t\r\n\f]+")]
    private static partial Regex Spaces();
}

internal enum Align { Left, Center, Right, Justify }

internal abstract record LayoutBlock;

/// <summary>
/// One paragraph. <see cref="LeftMm"/> is where its text starts; a list item's <see cref="Marker"/> hangs
/// <see cref="MinutesLayout.ListIndent"/> mm to the left of it. <see cref="FirstLineMm"/> is negative for a hanging indent.
/// </summary>
internal sealed record LayoutParagraph(Align Align, float LeftMm, float FirstLineMm, float BeforePt, float AfterPt,
    float LineHeight, float Size, bool Bold, string? Marker, IReadOnlyList<LayoutRun> Runs) : LayoutBlock;

internal sealed record LayoutRun(string Text, bool Bold, bool Italic, bool Underline, bool Strike, bool Sub, bool Sup,
    string? Color, string? Background, float Size, bool Break);

internal sealed record LayoutRule(float LeftMm) : LayoutBlock;

internal sealed record LayoutPageBreak : LayoutBlock;

/// <summary>A table: column widths in twips, rows of cells placed on the grid.</summary>
internal sealed record LayoutTable(float LeftMm, IReadOnlyList<int> Columns, bool Frame, IReadOnlyList<LayoutRow> Rows) : LayoutBlock;

internal sealed record LayoutRow(IReadOnlyList<LayoutCell> Cells);

/// <summary>
/// A cell at <see cref="Column"/> (0-based) spanning <see cref="Span"/> columns and <see cref="RowSpan"/> rows.
/// <see cref="Continues"/> marks a place held by a cell spanning down from above (Word's vMerge continuation); it has no
/// content of its own.
/// </summary>
internal sealed record LayoutCell(int Column, int Span, int RowSpan, bool Continues, string VerticalAlign,
    IReadOnlyList<LayoutBlock> Blocks);
