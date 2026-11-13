using System.Text;
using System.Text.RegularExpressions;

namespace TrustCabinet.Infrastructure.Email;

/// <summary>
/// The HTML part sent beside every plain-text email: the same text under the product name, each web address a link.
/// A mail client that does not detect links in plain text left a guest copying a long invitation URL by hand. The
/// address stays written out, so it can still be copied. Built from the text at send time, so nothing new is stored.
/// </summary>
internal static partial class EmailHtml
{
    [GeneratedRegex(@"https?://[^\s<>""]+")]
    private static partial Regex WebAddress();

    public static string FromText(string text)
    {
        var html = new StringBuilder(
            "<div style=\"font-family:Arial,Helvetica,sans-serif;font-size:14px;line-height:1.6;color:#1a2433\">"
            + "<p style=\"margin:0 0 16px;font-size:18px;font-weight:700;color:#2a4d94\">TrustCabinet</p><p style=\"margin:0\">");
        var last = 0;
        foreach (Match match in WebAddress().Matches(text))
        {
            html.Append(Encode(text[last..match.Index]));
            var url = Escape(match.Value);
            html.Append("<a href=\"").Append(url).Append("\" style=\"color:#2a4d94;word-break:break-all\">").Append(url).Append("</a>");
            last = match.Index + match.Length;
        }
        html.Append(Encode(text[last..])).Append("</p></div>");
        return html.ToString();
    }

    private static string Encode(string text) => Escape(text).Replace("\r\n", "\n").Replace("\n", "<br>");

    /// <summary>The four characters HTML needs escaped; Vietnamese letters stay as they are (UTF-8), not as entities.</summary>
    private static string Escape(string text)
        => text.Replace("&", "&amp;").Replace("<", "&lt;").Replace(">", "&gt;").Replace("\"", "&quot;");
}
