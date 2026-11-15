using System.Globalization;
using System.Text;

namespace TrustCabinet.Infrastructure.Email;

/// <summary>One row of the facts table: "Thời gian" → "09:00 – 10:30, Thứ Hai, 16/11/2026".</summary>
/// <param name="Struck">The old value of something that changed, shown struck through.</param>
internal sealed record EmailFact(string Label, string Value, bool Strong = false, bool Struck = false);

internal sealed record EmailButton(string Label, string Url);

internal enum EmailNoticeTone { Info, Warning }

internal sealed record EmailNotice(string Text, EmailNoticeTone Tone = EmailNoticeTone.Info);

/// <summary>
/// What one email says, in reading order. <see cref="EmailLayout"/> turns it into the HTML part; the plain-text part is
/// written by each sender and stays the source of truth for clients that show no HTML.
/// </summary>
/// <param name="Preheader">The line a mail client shows after the subject in the inbox list; never shown in the body.</param>
/// <param name="Kicker">What kind of email this is ("Lời mời tham dự cuộc họp"), above the heading.</param>
/// <param name="Heading">The meeting title, or the action ("Đặt lại mật khẩu").</param>
/// <param name="Code">A one-time code, shown large so it can be read and typed.</param>
/// <param name="After">Paragraphs under the button.</param>
internal sealed record EmailContent(
    string Preheader,
    string Kicker,
    string Heading,
    string Greeting,
    IReadOnlyList<string> Lead,
    string? FactsTitle = null,
    IReadOnlyList<EmailFact>? Facts = null,
    EmailNotice? Notice = null,
    string? Code = null,
    EmailButton? Button = null,
    IReadOnlyList<string>? After = null);

/// <summary>
/// The designed HTML of every TrustCabinet email: product header, kind and heading, greeting, a facts table, a call to
/// action and a footer. Tables and inline styles only, because Gmail and Outlook drop &lt;style&gt; blocks, flexbox
/// and SVG; colours are the app's tokens (global.css). Every value is escaped: titles, names, descriptions and reasons
/// are typed by people.
/// </summary>
internal static class EmailLayout
{
    // global.css tokens, written out because an email cannot read CSS variables.
    private const string Brand = "#2a4d94";
    private const string BrandWash = "#e8f0fa";
    private const string BrandWashLine = "#bbd0ea";
    private const string Gold = "#c99a2e";
    private const string Ink = "#1a2433";
    private const string MutedInk = "#4b5563";
    private const string Faint = "#6b7280";
    private const string Line = "#d9dee6";
    private const string SoftLine = "#e8ecf2";
    private const string Page = "#f3f5f8";
    private const string Panel = "#f6f8fb";
    private const string Font = "Arial,Helvetica,sans-serif";

    public static string Render(EmailContent content)
    {
        var h = new StringBuilder(6000);
        h.Append("<!doctype html><html lang=\"vi\"><head><meta charset=\"utf-8\">")
            .Append("<meta name=\"viewport\" content=\"width=device-width, initial-scale=1\">")
            .Append("<meta name=\"color-scheme\" content=\"light\"><meta name=\"supported-color-schemes\" content=\"light\">")
            .Append("<title>").Append(EmailHtml.Escape(content.Kicker)).Append("</title></head>")
            .Append($"<body style=\"margin:0;padding:0;background-color:{Page};font-family:{Font};color:{Ink};\">");

        // The inbox preview line, hidden in the body; the padding keeps the client from pulling body text after it.
        h.Append("<div style=\"display:none;max-height:0;overflow:hidden;mso-hide:all;font-size:1px;line-height:1px;color:")
            .Append(Page).Append(";\">").Append(EmailHtml.Escape(content.Preheader))
            .Append(string.Concat(Enumerable.Repeat("&#8199;&#65279;&#847; ", 40))).Append("</div>");

        h.Append(Table("width=\"100%\"", $"background-color:{Page};"))
            .Append("<tr><td align=\"center\" style=\"padding:24px 12px;\">")
            .Append(Table("width=\"600\"", $"width:100%;max-width:600px;background-color:#ffffff;border:1px solid {Line};border-radius:10px;border-collapse:separate;"));

        Header(h);
        Opening(h, content);
        if (content.Facts is { Count: > 0 } facts) FactsTable(h, content.FactsTitle, facts);
        if (content.Code is { } code) CodeBox(h, code);
        if (content.Notice is { } notice) NoticeBox(h, notice);
        if (content.Button is { } button) Button(h, button);
        foreach (var paragraph in content.After ?? [])
            Row(h, "8px 32px 0 32px", $"<p style=\"margin:0;font-size:13px;line-height:21px;color:{MutedInk};\">{EmailHtml.Inline(paragraph)}</p>");
        Footer(h);

        h.Append("</table></td></tr></table></body></html>");
        return h.ToString();
    }

    private static void Header(StringBuilder h)
    {
        // The app's mark: a gold star in a ring. A character, not an image, so it shows with images blocked.
        h.Append($"<tr><td style=\"height:4px;background-color:{Brand};border-radius:10px 10px 0 0;font-size:0;line-height:0;\">&nbsp;</td></tr>")
            .Append($"<tr><td style=\"padding:20px 32px 18px 32px;border-bottom:1px solid {SoftLine};\">")
            .Append(Table("", ""))
            .Append("<tr><td width=\"36\" valign=\"middle\">")
            .Append(Table("width=\"34\" height=\"34\"", $"width:34px;height:34px;border:2px solid {Brand};border-radius:50%;"))
            .Append($"<tr><td align=\"center\" valign=\"middle\" style=\"font-size:18px;line-height:30px;color:{Gold};font-weight:bold;\">&#9733;</td></tr></table>")
            .Append("</td><td valign=\"middle\" style=\"padding-left:12px;\">")
            .Append($"<div style=\"font-size:18px;line-height:22px;font-weight:bold;color:{Brand};\">TrustCabinet</div>")
            .Append($"<div style=\"font-size:12px;line-height:18px;color:{Faint};\">Phòng họp không giấy</div>")
            .Append("</td></tr></table></td></tr>");
    }

    private static void Opening(StringBuilder h, EmailContent content)
    {
        var b = new StringBuilder()
            .Append($"<p style=\"margin:0;font-size:13px;line-height:20px;font-weight:bold;color:{Brand};\">{EmailHtml.Escape(content.Kicker)}</p>")
            .Append($"<h1 style=\"margin:6px 0 0 0;font-size:22px;line-height:30px;font-weight:bold;color:{Ink};\">{EmailHtml.Escape(content.Heading)}</h1>")
            .Append($"<p style=\"margin:18px 0 0 0;font-size:14px;line-height:22px;color:{Ink};\">{EmailHtml.Escape(content.Greeting)}</p>");
        foreach (var paragraph in content.Lead)
            b.Append($"<p style=\"margin:8px 0 0 0;font-size:14px;line-height:22px;color:{MutedInk};\">{EmailHtml.Inline(paragraph)}</p>");
        Row(h, "26px 32px 4px 32px", b.ToString());
    }

    private static void FactsTable(StringBuilder h, string? title, IReadOnlyList<EmailFact> facts)
    {
        var b = new StringBuilder(Table("width=\"100%\"", $"border:1px solid {Line};border-radius:8px;border-collapse:separate;"));
        if (title is not null)
            b.Append($"<tr><td colspan=\"2\" style=\"padding:10px 14px;background-color:{Panel};border-bottom:1px solid {Line};border-radius:8px 8px 0 0;")
                .Append($"font-size:13px;line-height:18px;font-weight:bold;color:{Ink};\">{EmailHtml.Escape(title)}</td></tr>");
        for (var i = 0; i < facts.Count; i++)
        {
            var fact = facts[i];
            var border = i < facts.Count - 1 ? $"border-bottom:1px solid {SoftLine};" : "";
            var weight = fact.Strong ? "font-weight:bold;" : "";
            var value = fact.Struck
                ? $"<span style=\"color:{Faint};text-decoration:line-through;\">{EmailHtml.Inline(fact.Value)}</span>"
                : EmailHtml.Inline(fact.Value);
            b.Append($"<tr><td width=\"32%\" valign=\"top\" style=\"padding:10px 14px;{border}font-size:13px;line-height:20px;color:{Faint};\">")
                .Append(EmailHtml.Escape(fact.Label)).Append("</td>")
                .Append($"<td valign=\"top\" style=\"padding:10px 14px;{border}font-size:13px;line-height:20px;color:{Ink};{weight}word-break:break-word;\">")
                .Append(value).Append("</td></tr>");
        }
        b.Append("</table>");
        Row(h, "18px 32px 4px 32px", b.ToString());
    }

    private static void CodeBox(StringBuilder h, string code)
        => Row(h, "18px 32px 4px 32px",
            Table("width=\"100%\"", $"background-color:{BrandWash};border:1px solid {BrandWashLine};border-radius:8px;border-collapse:separate;")
            + $"<tr><td align=\"center\" style=\"padding:18px 12px;font-family:'Courier New',Courier,monospace;font-size:30px;line-height:36px;"
            + $"font-weight:bold;letter-spacing:8px;color:{Ink};\">{EmailHtml.Escape(code)}</td></tr></table>");

    private static void NoticeBox(StringBuilder h, EmailNotice notice)
    {
        var (background, border, ink) = notice.Tone == EmailNoticeTone.Warning
            ? ("#fdf6e3", "#f1d9a0", "#7a5410")
            : (BrandWash, BrandWashLine, "#1d3b78");
        Row(h, "16px 32px 4px 32px",
            Table("width=\"100%\"", $"background-color:{background};border:1px solid {border};border-radius:8px;border-collapse:separate;")
            + $"<tr><td style=\"padding:12px 14px;font-size:13px;line-height:21px;color:{ink};\">{EmailHtml.Inline(notice.Text)}</td></tr></table>");
    }

    private static void Button(StringBuilder h, EmailButton button)
    {
        var url = EmailHtml.Escape(button.Url);
        Row(h, "22px 32px 4px 32px",
            Table("", "")
            + $"<tr><td bgcolor=\"{Brand}\" style=\"border-radius:8px;\">"
            + $"<a href=\"{url}\" style=\"display:inline-block;padding:12px 22px;font-size:14px;line-height:20px;font-weight:bold;color:#ffffff;"
            + $"text-decoration:none;border-radius:8px;\">{EmailHtml.Escape(button.Label)}</a></td></tr></table>"
            // The address written out too: a client that blocks the button still lets it be copied.
            + $"<p style=\"margin:12px 0 0 0;font-size:12px;line-height:18px;color:{Faint};\">Nếu nút không mở được, chép đường dẫn này vào trình duyệt:<br>"
            + $"<a href=\"{url}\" style=\"color:{Brand};word-break:break-all;\">{url}</a></p>");
    }

    private static void Footer(StringBuilder h)
        => h.Append($"<tr><td style=\"padding:24px 32px 0 32px;font-size:0;line-height:0;\">&nbsp;</td></tr>")
            .Append($"<tr><td style=\"padding:16px 32px;background-color:{Panel};border-top:1px solid {SoftLine};border-radius:0 0 10px 10px;")
            .Append($"font-size:12px;line-height:18px;color:{Faint};\">")
            .Append("Thư gửi tự động từ TrustCabinet, vui lòng không trả lời thư này.</td></tr>");

    private static void Row(StringBuilder h, string padding, string inner)
        => h.Append("<tr><td style=\"padding:").Append(padding).Append(";\">").Append(inner).Append("</td></tr>");

    private static string Table(string attributes, string style)
        => $"<table role=\"presentation\" cellpadding=\"0\" cellspacing=\"0\" border=\"0\" {attributes} style=\"{style}\">";

    // Written out rather than read from vi-VN, which a server image without ICU data does not have.
    private static readonly string[] DayNames = ["Chủ Nhật", "Thứ Hai", "Thứ Ba", "Thứ Tư", "Thứ Năm", "Thứ Sáu", "Thứ Bảy"];
    private static readonly TimeZoneInfo Zone = TimeZoneInfo.FindSystemTimeZoneById("Asia/Ho_Chi_Minh");

    /// <summary>"09:00 – 10:30, Thứ Hai, 16/11/2026"; a meeting across midnight names both days.</summary>
    public static string When(DateTimeOffset start, DateTimeOffset end)
    {
        var s = TimeZoneInfo.ConvertTime(start, Zone);
        var e = TimeZoneInfo.ConvertTime(end, Zone);
        return s.Date == e.Date
            ? $"{Clock(s)} – {Clock(e)}, {Day(s)}"
            : $"{Clock(s)}, {Day(s)} – {Clock(e)}, {Day(e)}";
    }

    /// <summary>"10:00, Thứ Hai, 16/11/2026".</summary>
    public static string At(DateTimeOffset at)
    {
        var local = TimeZoneInfo.ConvertTime(at, Zone);
        return $"{Clock(local)}, {Day(local)}";
    }

    private static string Clock(DateTimeOffset local) => local.ToString("HH:mm", CultureInfo.InvariantCulture);

    private static string Day(DateTimeOffset local)
        => $"{DayNames[(int)local.DayOfWeek]}, {local.ToString("dd/MM/yyyy", CultureInfo.InvariantCulture)}";

    public static string ModeLabel(string mode) => mode switch
    {
        "Online" => "Trực tuyến",
        "Hybrid" => "Kết hợp",
        _ => "Trực tiếp",
    };
}
