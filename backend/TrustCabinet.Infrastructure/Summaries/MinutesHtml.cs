using System.Globalization;
using System.Text;
using System.Text.RegularExpressions;
using AngleSharp.Dom;
using AngleSharp.Html.Parser;

namespace TrustCabinet.Infrastructure.Summaries;

/// <summary>
/// The draft minutes are HTML from a Word-like editor (TipTap, as AI-eCabinet), kept to a fixed allow-list (ADR-0055) so
/// nothing in them can run in a browser and everything in them can be drawn by the page, <see cref="MinutesDocx"/> and
/// <see cref="MinutesPdf"/> alike. The server sanitizes every write; rows saved before as line-marked text
/// (<see cref="MinutesTemplate"/>'s output) are converted on read by <see cref="FromLines"/>.
/// <para>
/// Allowed: the blocks <c>p h1 h2 h3 ul ol li hr table colgroup col thead tbody tr th td</c> and the inline
/// <c>strong em u s sub sup br span</c>, each with only the attributes and style properties it can carry. b, i, strike,
/// del, ins and h4–h6 are renamed; script, style, iframe and their kind are dropped with their content; any other element
/// is unwrapped, keeping its text (a block-like one such as <c>div</c> still ends the paragraph). Inline content sitting
/// among blocks is wrapped in a <c>p</c>.
/// </para>
/// </summary>
public static partial class MinutesHtml
{
    private static readonly HashSet<string> Blocks = ["p", "h1", "h2", "h3", "ul", "ol", "hr", "table"];
    private static readonly HashSet<string> Inlines = ["strong", "em", "u", "s", "sub", "sup", "br", "span"];
    private static readonly HashSet<string> Structure = ["li", "colgroup", "col", "thead", "tbody", "tr", "th", "td"];
    private static readonly HashSet<string> Voids = ["br", "hr", "col"];

    private static readonly HashSet<string> Dropped =
    [
        "script", "style", "iframe", "object", "embed", "template", "noscript", "head", "title", "svg", "math", "textarea",
        "select", "button", "form", "img", "video", "audio", "canvas", "link", "meta", "base",
    ];

    /// <summary>Unknown elements that still end a paragraph when unwrapped: pasted text keeps its line breaks.</summary>
    private static readonly HashSet<string> Breaking =
    [
        "div", "section", "article", "header", "footer", "main", "nav", "aside", "blockquote", "pre", "address", "figure",
        "figcaption", "dl", "dt", "dd", "details", "summary", "center", "fieldset", "legend", "caption", "tfoot", "body",
        "html",
    ];

    private static readonly Dictionary<string, string> Renamed = new()
    {
        ["b"] = "strong", ["i"] = "em", ["strike"] = "s", ["del"] = "s", ["ins"] = "u",
        ["h4"] = "h3", ["h5"] = "h3", ["h6"] = "h3",
    };

    private static readonly string[] Paragraphs = ["p", "h1", "h2", "h3"];
    private static readonly string[] Cells = ["td", "th"];

    /// <summary>Which style properties each element may carry.</summary>
    private static readonly Dictionary<string, HashSet<string>> StyleProperties = Build(
        (["text-align"], [.. Paragraphs, "li", .. Cells]),
        (["text-indent", "margin-top", "margin-bottom"], Paragraphs),
        (["margin-left"], [.. Paragraphs, "ul", "ol"]),
        (["line-height"], [.. Paragraphs, "li"]),
        (["font-size", "color", "background-color"], ["span"]),
        (["vertical-align"], Cells),
        (["width", "min-width"], ["table", "col"]));

    /// <summary>The text colours the editor offers (Word's standard colours and black).</summary>
    public static readonly IReadOnlySet<string> TextColors = new HashSet<string>
        { "#000000", "#c00000", "#ff0000", "#ed7d31", "#00b050", "#0070c0", "#002060", "#7030a0" };

    /// <summary>The highlight colours the editor offers.</summary>
    public static readonly IReadOnlySet<string> HighlightColors = new HashSet<string>
        { "#ffff00", "#00ff00", "#00ffff", "#ff00ff", "#d9d9d9" };

    private static readonly HtmlParser Parser = new();

    /// <summary>True when the content is HTML rather than the line-marked text of older rows.</summary>
    public static bool IsHtml(string content) => content.Trim().StartsWith('<');

    /// <summary>Any content as sanitized HTML: HTML is sanitized, line-marked text converted.</summary>
    public static string Normalize(string content) => IsHtml(content) ? Sanitize(content) : FromLines(content);

    /// <summary>The HTML kept to the allow-list. Idempotent: sanitizing the output again gives the same string.</summary>
    public static string Sanitize(string html) => Serialize(Parse(html));

    /// <summary>Whether the minutes say anything: a document of empty paragraphs and rules does not.</summary>
    public static bool HasText(string html) => Read(html).Any(HasText);

    /// <summary>The sanitized tree of any content, HTML or line-marked text; what the Word and PDF files are drawn from.</summary>
    internal static IReadOnlyList<HtmlElement> Read(string content) => Parse(IsHtml(content) ? content : FromLines(content));

    /// <summary>
    /// How deep elements may nest. The walker below recurses once per level and .NET cannot catch a stack overflow: a few
    /// kilobytes of nested tags would take the whole process down. Nothing the editor writes comes near it (a list in a
    /// cell in a table is under ten levels).
    /// </summary>
    internal const int MaxDepth = 64;

    /// <summary>True when the HTML nests deeper than <see cref="MaxDepth"/>; counted without recursion.</summary>
    public static bool TooDeep(string html) => TooDeep(Parser.ParseFragment(html, Parser.ParseDocument(string.Empty).Body!));

    private static bool TooDeep(IEnumerable<INode> nodes)
    {
        var stack = new Stack<(INode Node, int Depth)>(nodes.Select(n => (n, 1)));
        while (stack.Count > 0)
        {
            var (node, depth) = stack.Pop();
            if (depth > MaxDepth) return true;
            foreach (var child in node.ChildNodes) if (child is IElement) stack.Push((child, depth + 1));
        }
        return false;
    }

    /// <summary>All the text under the nodes, in reading order, gathered without recursion.</summary>
    private static string FlatText(IEnumerable<INode> nodes)
    {
        var text = new StringBuilder();
        var stack = new Stack<INode>(nodes.Reverse());
        while (stack.Count > 0)
        {
            var node = stack.Pop();
            if (node is IText t) text.Append(t.Data);
            else for (var i = node.ChildNodes.Length - 1; i >= 0; i--) stack.Push(node.ChildNodes[i]);
        }
        return text.ToString();
    }

    internal static IReadOnlyList<HtmlElement> Parse(string html)
    {
        var context = Parser.ParseDocument(string.Empty).Body!;
        var nodes = Parser.ParseFragment(html, context);
        // Stored content passed this check when it was saved; anything deeper is cut to its text rather than walked.
        if (TooDeep(nodes)) nodes = Parser.ParseFragment($"<p>{Encode(FlatText(nodes), attribute: false)}</p>", context);
        var output = new List<HtmlNode>();
        Flow(Expand(nodes), output);
        return output.Cast<HtmlElement>().ToList();
    }

    // ---- Walking the parsed DOM ------------------------------------------------------------------------------------

    /// <summary>
    /// A parsed node as the walker sees it: text (Name null), an allowed element under its canonical name, or the edge of an
    /// unwrapped block-like element (Name ""), where a paragraph ends.
    /// </summary>
    private readonly record struct Item(INode Node, string? Name)
    {
        public bool IsBlank => Name == "" || (Name is null && Node.TextContent.All(IsHtmlSpace));
    }

    /// <summary>The children, with comments and dropped elements gone and unknown elements replaced by their children.</summary>
    private static IEnumerable<Item> Expand(IEnumerable<INode> nodes)
    {
        foreach (var node in nodes)
        {
            if (node is IText) yield return new(node, null);
            else if (node is IElement element)
            {
                var name = element.LocalName.ToLowerInvariant();
                if (element.NamespaceUri is { } ns && ns != NamespaceNames.HtmlUri) continue;
                if (Dropped.Contains(name)) continue;
                name = Renamed.GetValueOrDefault(name, name);
                if (Blocks.Contains(name) || Inlines.Contains(name) || Structure.Contains(name)) yield return new(element, name);
                else
                {
                    var breaking = Breaking.Contains(name);
                    if (breaking) yield return new(element, "");
                    foreach (var inner in Expand(element.ChildNodes)) yield return inner;
                    if (breaking) yield return new(element, "");
                }
            }
        }
    }

    private static IEnumerable<Item> Expand(Item item) => Expand(item.Node.ChildNodes);

    /// <summary>Content where only blocks belong (the document, a list item, a cell): loose inline content becomes a p.</summary>
    private static void Flow(IEnumerable<Item> items, List<HtmlNode> output)
    {
        var pending = new List<Item>();
        foreach (var item in items) FlowItem(item, pending, output);
        Wrap(pending, output);
    }

    private static void FlowItem(Item item, List<Item> pending, List<HtmlNode> output)
    {
        if (item.Name == "")
        {
            Wrap(pending, output);
            return;
        }
        if (item.Name is null || Inlines.Contains(item.Name))
        {
            pending.Add(item);
            return;
        }
        if (item.Name is "col" or "colgroup") return;
        Wrap(pending, output);
        if (Blocks.Contains(item.Name))
        {
            if (Block(item) is { } block) output.Add(block);
            return;
        }
        // A list item, a row or a cell out of place: its content joins the flow as blocks of its own.
        foreach (var inner in Expand(item)) FlowItem(inner, pending, output);
        Wrap(pending, output);
    }

    private static void Wrap(List<Item> pending, List<HtmlNode> output)
    {
        var start = pending.FindIndex(i => !i.IsBlank);
        if (start >= 0)
        {
            var end = pending.FindLastIndex(i => !i.IsBlank);
            var p = new HtmlElement("p");
            Inline(pending.Skip(start).Take(end - start + 1), p.Children);
            // Loose text keeps no source indentation at its edges, as a browser draws none.
            if (p.Children is [HtmlText head, ..]) p.Children[0] = new HtmlText(head.Text.TrimStart(HtmlSpaces));
            if (p.Children is [.., HtmlText tail]) p.Children[^1] = new HtmlText(tail.Text.TrimEnd(HtmlSpaces));
            p.Children.RemoveAll(c => c is HtmlText { Text: "" });
            output.Add(p);
        }
        pending.Clear();
    }

    /// <summary>Content of a paragraph or an inline element: anything block-like is unwrapped into it.</summary>
    private static void Inline(IEnumerable<Item> items, List<HtmlNode> output)
    {
        foreach (var item in items)
        {
            if (item.Name == "") continue;
            if (item.Name is null)
            {
                var text = Clean(item.Node.TextContent);
                if (text.Length > 0) output.Add(new HtmlText(text));
            }
            else if (Inlines.Contains(item.Name))
            {
                var element = new HtmlElement(item.Name);
                Copy((IElement)item.Node, element);
                if (item.Name != "br") Inline(Expand(item), element.Children);
                if (item.Name == "span" && element.Attributes.Count == 0) output.AddRange(element.Children);
                else output.Add(element);
            }
            else if (item.Name is not ("hr" or "col" or "colgroup")) Inline(Expand(item), output);
        }
    }

    private static HtmlElement? Block(Item item)
    {
        var element = new HtmlElement(item.Name!);
        Copy((IElement)item.Node, element);
        switch (item.Name)
        {
            case "hr":
                return element;
            case "ul" or "ol":
                List(Expand(item), element.Children);
                return element.Children.Count > 0 ? element : null;
            case "table":
                Table(Expand(item), element.Children);
                return element.Children.OfType<HtmlElement>().Any(e => e.Name is "thead" or "tbody") ? element : null;
            case "li" or "td" or "th":
                Flow(Expand(item), element.Children);
                return element;
            default:
                Inline(Expand(item), element.Children);
                return element;
        }
    }

    private static void List(IEnumerable<Item> items, List<HtmlNode> output)
    {
        var pending = new List<Item>();
        void Flush()
        {
            if (pending.Any(i => !i.IsBlank))
            {
                var li = new HtmlElement("li");
                Flow(pending, li.Children);
                if (li.Children.Count > 0) output.Add(li);
            }
            pending.Clear();
        }
        foreach (var item in items)
        {
            if (item.Name == "li")
            {
                Flush();
                output.Add(Block(item)!);
            }
            else pending.Add(item);
        }
        Flush();
    }

    private static void Table(IEnumerable<Item> items, List<HtmlNode> output)
    {
        var loose = new List<Item>();
        void Flush()
        {
            if (loose.Count > 0 && Section("tbody", loose) is { } body) output.Add(body);
            loose.Clear();
        }
        foreach (var item in items)
        {
            switch (item.Name)
            {
                case "colgroup":
                    Flush();
                    var group = new HtmlElement("colgroup");
                    foreach (var col in Expand(item).Where(i => i.Name == "col"))
                    {
                        var element = new HtmlElement("col");
                        Copy((IElement)col.Node, element);
                        group.Children.Add(element);
                    }
                    if (group.Children.Count > 0) output.Add(group);
                    break;
                case "thead" or "tbody":
                    Flush();
                    if (Section(item.Name, Expand(item)) is { } section) output.Add(section);
                    break;
                case "tr" or "td" or "th":
                    loose.Add(item);
                    break;
            }
        }
        Flush();
    }

    private static HtmlElement? Section(string name, IEnumerable<Item> items)
    {
        var section = new HtmlElement(name);
        var cells = new List<Item>();
        void Flush()
        {
            if (cells.Count > 0) section.Children.Add(Row(cells));
            cells.Clear();
        }
        foreach (var item in items)
        {
            if (item.Name is "td" or "th") cells.Add(item);
            else if (item.Name == "tr")
            {
                Flush();
                var row = Row(Expand(item).Where(i => i.Name is "td" or "th"));
                if (row.Children.Count > 0) section.Children.Add(row);
            }
        }
        Flush();
        return section.Children.Count > 0 ? section : null;
    }

    private static HtmlElement Row(IEnumerable<Item> cells)
    {
        var row = new HtmlElement("tr");
        foreach (var cell in cells) row.Children.Add(Block(cell)!);
        return row;
    }

    // ---- Attributes and styles -------------------------------------------------------------------------------------

    private static void Copy(IElement source, HtmlElement target)
    {
        foreach (var attribute in source.Attributes)
        {
            var name = attribute.Name.ToLowerInvariant();
            var value = attribute.Value.Trim();
            string? kept = (target.Name, name) switch
            {
                (_, "style") => FilterStyle(target, value),
                ("ol", "start") => Integer(value, 1, 9999),
                ("hr", "data-page-break") => value == "true" ? value : null,
                ("table", "data-layout") => value == "frame" ? value : null,
                ("td" or "th", "colspan") => Integer(value, 1, 50),
                ("td" or "th", "rowspan") => Integer(value, 1, 200),
                ("td" or "th", "colwidth") => Widths(value),
                _ => null,
            };
            if (kept is not null && target.Attributes.All(a => a.Key != name)) target.Attributes.Add(new(name, kept));
        }
    }

    private static string? FilterStyle(HtmlElement target, string style)
    {
        if (!StyleProperties.TryGetValue(target.Name, out var allowed)) return null;
        var kept = new List<KeyValuePair<string, string>>();
        foreach (var declaration in style.Split(';'))
        {
            var colon = declaration.IndexOf(':');
            if (colon < 0) continue;
            var property = declaration[..colon].Trim().ToLowerInvariant();
            if (!allowed.Contains(property)) continue;
            var value = StyleValue(property, declaration[(colon + 1)..].Trim().ToLowerInvariant());
            if (value is null) continue;
            var at = kept.FindIndex(k => k.Key == property);
            if (at >= 0) kept[at] = new(property, value);
            else kept.Add(new(property, value));
        }
        foreach (var (property, value) in kept) target.Style[property] = value;
        return kept.Count == 0 ? null : string.Join("; ", kept.Select(k => $"{k.Key}: {k.Value}"));
    }

    private static string? StyleValue(string property, string value) => property switch
    {
        "text-align" => value is "left" or "center" or "right" or "justify" ? value : null,
        "vertical-align" => value is "top" or "middle" or "bottom" ? value : null,
        "text-indent" => Length(value, -50, 50),
        "margin-left" => Length(value, 0, 150),
        "line-height" => Number(value) is { } n && n >= 0.8m && n <= 3m ? value : null,
        "margin-top" or "margin-bottom" => Unit(value, "pt") is { } pt && pt >= 0 && pt <= 72 ? value : null,
        "font-size" => Unit(value, "pt") is { } size && size >= 6 && size <= 40 ? value : null,
        "width" or "min-width" => Unit(value, "px") is { } px && px >= 0 && px <= 2000 ? value : null,
        "color" => Color(value) is { } c && TextColors.Contains(c) ? c : null,
        "background-color" => Color(value) is { } b && HighlightColors.Contains(b) ? b : null,
        _ => null,
    };

    /// <summary>A length in mm or pt, within the range in millimetres.</summary>
    private static string? Length(string value, decimal minMm, decimal maxMm)
    {
        var mm = Unit(value, "mm") ?? Unit(value, "pt") * 25.4m / 72m;
        return mm is { } v && v >= minMm && v <= maxMm ? value : null;
    }

    private static decimal? Unit(string value, string unit)
        => value.EndsWith(unit, StringComparison.Ordinal) ? Number(value[..^unit.Length]) : null;

    private static decimal? Number(string value)
        => NumberPattern().IsMatch(value) && decimal.TryParse(value, NumberStyles.AllowLeadingSign | NumberStyles.AllowDecimalPoint,
            CultureInfo.InvariantCulture, out var number) ? number : null;

    private static string? Integer(string value, int min, int max)
        => DigitsPattern().IsMatch(value) && int.TryParse(value, NumberStyles.None, CultureInfo.InvariantCulture, out var n)
           && n >= min && n <= max ? n.ToString(CultureInfo.InvariantCulture) : null;

    private static string? Widths(string value)
    {
        var parts = value.Split(',').Select(p => Integer(p.Trim(), 1, 2000)).ToList();
        return parts.Count > 0 && parts.All(p => p is not null) ? string.Join(',', parts) : null;
    }

    /// <summary>A colour as lower-case <c>#rrggbb</c>, from <c>#rgb</c>, <c>#rrggbb</c> or <c>rgb(r, g, b)</c>.</summary>
    internal static string? Color(string value)
    {
        if (HexPattern().IsMatch(value))
            return value.Length == 4 ? $"#{value[1]}{value[1]}{value[2]}{value[2]}{value[3]}{value[3]}" : value;
        var rgb = RgbPattern().Match(value);
        if (!rgb.Success) return null;
        var channels = new int[3];
        for (var i = 0; i < 3; i++)
            if (!int.TryParse(rgb.Groups[i + 1].Value, CultureInfo.InvariantCulture, out channels[i]) || channels[i] > 255) return null;
        return $"#{channels[0]:x2}{channels[1]:x2}{channels[2]:x2}";
    }

    // ---- Line-marked text (rows saved before ADR-0055) -------------------------------------------------------------

    /// <summary>
    /// The line-marked text <see cref="MinutesTemplate"/> writes (and older rows hold) as sanitized HTML: <c># </c>,
    /// <c>## </c>, <c>### </c> headings, <c>^ </c> a centred line, <c>~ </c> a small line flush left, consecutive
    /// <c>@@ a | b</c> rows a borderless two-column table, anything else a paragraph; <c>**</c> and <c>__</c> toggle bold
    /// and italic. A zero-width space kept a typed mark from reading as one; it goes once the marks are read.
    /// </summary>
    public static string FromLines(string lineText)
    {
        var html = new StringBuilder();
        var rows = new List<string>();
        void Flush()
        {
            if (rows.Count == 0) return;
            html.Append("""<table data-layout="frame"><colgroup><col style="width: 250px"><col style="width: 374px"></colgroup><tbody>""");
            foreach (var row in rows)
            {
                var bar = row.IndexOf('|');
                var (left, right) = bar < 0 ? (row, "") : (row[..bar], row[(bar + 1)..]);
                html.Append("<tr>")
                    .Append("""<td colwidth="250"><p style="text-align: center">""").Append(Runs(left.Trim())).Append("</p></td>")
                    .Append("""<td colwidth="374"><p style="text-align: center">""").Append(Runs(right.Trim())).Append("</p></td>")
                    .Append("</tr>");
            }
            html.Append("</tbody></table>");
            rows.Clear();
        }
        foreach (var raw in lineText.Replace("\r", "", StringComparison.Ordinal).Split('\n'))
        {
            var line = raw.TrimEnd();
            if (line.StartsWith("@@", StringComparison.Ordinal))
            {
                rows.Add(line[2..].Trim());
                continue;
            }
            Flush();
            if (line.Length == 0) continue;
            if (line.StartsWith("### ", StringComparison.Ordinal)) html.Append("<h3>").Append(Runs(line[4..])).Append("</h3>");
            else if (line.StartsWith("## ", StringComparison.Ordinal)) html.Append("<h2>").Append(Runs(line[3..])).Append("</h2>");
            else if (line.StartsWith("# ", StringComparison.Ordinal)) html.Append("<h1>").Append(Runs(line[2..])).Append("</h1>");
            else if (line.StartsWith("^ ", StringComparison.Ordinal))
                html.Append("""<p style="text-align: center; text-indent: 0mm; margin-bottom: 12pt">""").Append(Runs(line[2..])).Append("</p>");
            else if (line.StartsWith("~ ", StringComparison.Ordinal))
                html.Append("""<p style="text-align: left; text-indent: 0mm; margin-bottom: 0pt"><span style="font-size: 11pt">""")
                    .Append(Runs(line[2..])).Append("</span></p>");
            else html.Append("<p>").Append(Runs(line)).Append("</p>");
        }
        Flush();
        return html.ToString();
    }

    /// <summary>A line's runs, cut at <c>**</c> (bold) and <c>__</c> (italic), as HTML.</summary>
    private static string Runs(string text)
    {
        var runs = new StringBuilder();
        bool strong = false, italic = false;
        var at = 0;
        while (at < text.Length)
        {
            var star = text.IndexOf("**", at, StringComparison.Ordinal);
            var under = text.IndexOf("__", at, StringComparison.Ordinal);
            var next = star < 0 ? under : under < 0 ? star : Math.Min(star, under);
            var end = next < 0 ? text.Length : next;
            var run = Clean(text[at..end].Replace("​", "", StringComparison.Ordinal));
            if (run.Length > 0)
            {
                var encoded = Encode(run, attribute: false);
                if (italic) encoded = $"<em>{encoded}</em>";
                if (strong) encoded = $"<strong>{encoded}</strong>";
                runs.Append(encoded);
            }
            if (next < 0) break;
            if (next == star) strong = !strong;
            else italic = !italic;
            at = next + 2;
        }
        return runs.ToString();
    }

    // ---- Output ----------------------------------------------------------------------------------------------------

    private static string Serialize(IEnumerable<HtmlNode> nodes)
    {
        var html = new StringBuilder();
        foreach (var node in nodes) Write(html, node);
        return html.ToString();
    }

    private static void Write(StringBuilder html, HtmlNode node)
    {
        if (node is HtmlText text)
        {
            html.Append(Encode(text.Text, attribute: false));
            return;
        }
        var element = (HtmlElement)node;
        html.Append('<').Append(element.Name);
        foreach (var (name, value) in element.Attributes)
            html.Append(' ').Append(name).Append("=\"").Append(Encode(value, attribute: true)).Append('"');
        html.Append('>');
        if (Voids.Contains(element.Name)) return;
        foreach (var child in element.Children) Write(html, child);
        html.Append("</").Append(element.Name).Append('>');
    }

    /// <summary>Text escaped as a browser's <c>innerHTML</c> escapes it, so the editor's own output comes back unchanged.</summary>
    private static string Encode(string text, bool attribute)
    {
        var encoded = new StringBuilder(text.Length);
        foreach (var c in text)
        {
            encoded.Append(c switch
            {
                '&' => "&amp;",
                ' ' => "&nbsp;",
                '"' when attribute => "&quot;",
                '<' when !attribute => "&lt;",
                '>' when !attribute => "&gt;",
                _ => c.ToString(),
            });
        }
        return encoded.ToString();
    }

    /// <summary>Control characters other than tab and line feed go: no reader shows them and XML cannot carry them.</summary>
    private static string Clean(string text)
    {
        if (!text.Any(c => char.IsControl(c) && c is not ('\t' or '\n'))) return text;
        return string.Concat(text.Where(c => !char.IsControl(c) || c is '\t' or '\n'));
    }

    private static bool HasText(HtmlNode node) => node switch
    {
        HtmlText text => text.Text.Any(c => !char.IsWhiteSpace(c) && c != '​'),
        HtmlElement element => element.Children.Any(HasText),
        _ => false,
    };

    private static readonly char[] HtmlSpaces = [' ', '\t', '\n', '\f', '\r'];

    private static bool IsHtmlSpace(char c) => HtmlSpaces.Contains(c);

    private static Dictionary<string, HashSet<string>> Build(params (string[] Properties, string[] Elements)[] rules)
    {
        var map = new Dictionary<string, HashSet<string>>();
        foreach (var (properties, elements) in rules)
            foreach (var element in elements)
            {
                if (!map.TryGetValue(element, out var set)) map[element] = set = [];
                set.UnionWith(properties);
            }
        return map;
    }

    [GeneratedRegex(@"^-?(\d+(\.\d+)?|\.\d+)$")]
    private static partial Regex NumberPattern();

    [GeneratedRegex(@"^\d+$")]
    private static partial Regex DigitsPattern();

    [GeneratedRegex(@"^#([0-9a-f]{3}|[0-9a-f]{6})$")]
    private static partial Regex HexPattern();

    [GeneratedRegex(@"^rgb\(\s*(\d{1,3})\s*,\s*(\d{1,3})\s*,\s*(\d{1,3})\s*\)$")]
    private static partial Regex RgbPattern();
}

/// <summary>A node of the sanitized minutes, as the renderers read it.</summary>
internal abstract class HtmlNode;

internal sealed class HtmlText(string text) : HtmlNode
{
    public string Text { get; } = text;
}

internal sealed class HtmlElement(string name) : HtmlNode
{
    public string Name { get; } = name;
    /// <summary>The kept attributes in their input order; <c>style</c> holds the filtered declarations.</summary>
    public List<KeyValuePair<string, string>> Attributes { get; } = [];
    /// <summary>The kept style declarations, by property.</summary>
    public Dictionary<string, string> Style { get; } = [];
    public List<HtmlNode> Children { get; } = [];

    public string? Attribute(string name) => Attributes.FirstOrDefault(a => a.Key == name).Value;
    public IEnumerable<HtmlElement> Elements => Children.OfType<HtmlElement>();
}
