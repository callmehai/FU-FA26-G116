using System.Text;
using System.Text.RegularExpressions;

namespace TrustCabinet.Infrastructure.Email;

/// <summary>
/// The fallback HTML part for an email queued without one (<see cref="Domain.Entities.EmailMessage.HtmlBody"/> null,
/// e.g. rows queued before the designed templates): the same text under the product name, each web address a link.
/// A mail client that does not detect links in plain text left a guest copying a long invitation URL by hand. The
/// address stays written out, so it can still be copied.
/// </summary>
internal static partial class EmailHtml
{
    // Never ends on punctuation: "…/tb." or "(https://x.vn/a)" in a user's text keeps its "." or ")" outside the link.
    [GeneratedRegex(@"https?://[^\s<>""]*[^\s<>"".,;:!?)\]}”’']")]
    private static partial Regex WebAddress();

    public static string FromText(string text)
        => "<div style=\"font-family:Arial,Helvetica,sans-serif;font-size:14px;line-height:1.6;color:#1a2433\">"
            + "<p style=\"margin:0 0 16px;font-size:18px;font-weight:700;color:#2a4d94\">TrustCabinet</p><p style=\"margin:0\">"
            + Inline(text) + "</p></div>";

    /// <summary>
    /// A person's text as HTML: escaped, line breaks kept, each web address a link that still shows the address.
    /// </summary>
    public static string Inline(string text)
    {
        var html = new StringBuilder();
        var last = 0;
        foreach (Match match in WebAddress().Matches(text))
        {
            html.Append(Encode(text[last..match.Index]));
            var url = Escape(match.Value);
            html.Append("<a href=\"").Append(url).Append("\" style=\"color:#2a4d94;word-break:break-all\">").Append(url).Append("</a>");
            last = match.Index + match.Length;
        }
        return html.Append(Encode(text[last..])).ToString();
    }

    private static string Encode(string text) => Escape(text).Replace("\r\n", "\n").Replace("\n", "<br>");

    /// <summary>The four characters HTML needs escaped; Vietnamese letters stay as they are (UTF-8), not as entities.</summary>
    public static string Escape(string text)
        => text.Replace("&", "&amp;").Replace("<", "&lt;").Replace(">", "&gt;").Replace("\"", "&quot;");
}
