using System.Globalization;
using System.Text;

namespace TrustCabinet.Infrastructure.Email;

internal static class CalendarInvitationBuilder
{
    public static string Build(
        Guid meetingId,
        string title,
        string description,
        string location,
        DateTimeOffset startsAt,
        DateTimeOffset? endsAt,
        string attendeeName,
        string attendeeEmail,
        string meetingUrl,
        string organizerName,
        string organizerEmail,
        DateTimeOffset createdAt,
        bool cancelled = false)
    {
        // A cancellation reuses the invitation's UID with a higher SEQUENCE, so calendar apps that
        // accepted the invitation remove the event instead of adding a second one (RFC 5546 §3.2.5).
        var lines = new List<string>
        {
            "BEGIN:VCALENDAR",
            "PRODID:-//TrustCabinet//Meeting Invitation//VI",
            "VERSION:2.0",
            "CALSCALE:GREGORIAN",
            cancelled ? "METHOD:CANCEL" : "METHOD:REQUEST",
            "BEGIN:VEVENT",
            $"UID:{meetingId:D}@trustcabinet",
            $"DTSTAMP:{Utc(createdAt)}",
            $"DTSTART:{Utc(startsAt)}",
        };

        if (endsAt is not null)
        {
            lines.Add($"DTEND:{Utc(endsAt.Value)}");
        }

        lines.Add($"SUMMARY:{Escape(title)}");
        lines.Add($"DESCRIPTION:{Escape(JoinDescription(description, meetingUrl))}");
        lines.Add($"LOCATION:{Escape(location)}");
        lines.Add($"URL:{Escape(meetingUrl)}");
        lines.Add($"ORGANIZER;CN=\"{EncodeParameter(organizerName)}\":mailto:{organizerEmail}");
        lines.Add($"ATTENDEE;CN=\"{EncodeParameter(attendeeName)}\";RSVP=TRUE:mailto:{attendeeEmail}");
        lines.Add(cancelled ? "STATUS:CANCELLED" : "STATUS:CONFIRMED");
        lines.Add(cancelled ? "SEQUENCE:1" : "SEQUENCE:0");
        lines.Add("END:VEVENT");
        lines.Add("END:VCALENDAR");

        return string.Join("\r\n", lines.Select(FoldLine)) + "\r\n";
    }

    /// <summary>The iTIP method of a built calendar, which the MIME part must repeat for mail clients to act on it.</summary>
    public static string MethodOf(string calendar)
        => calendar.Contains("\r\nMETHOD:CANCEL\r\n", StringComparison.Ordinal) ? "CANCEL" : "REQUEST";

    private static string Utc(DateTimeOffset value)
        => value.UtcDateTime.ToString("yyyyMMdd'T'HHmmss'Z'", CultureInfo.InvariantCulture);

    private static string JoinDescription(string description, string meetingUrl)
        => string.IsNullOrWhiteSpace(description)
            ? $"Chi tiết cuộc họp: {meetingUrl}"
            : $"{description.Trim()}\n\nChi tiết cuộc họp: {meetingUrl}";

    private static string Escape(string value)
        => value
            .Replace("\\", "\\\\", StringComparison.Ordinal)
            .Replace(";", "\\;", StringComparison.Ordinal)
            .Replace(",", "\\,", StringComparison.Ordinal)
            .Replace("\r\n", "\\n", StringComparison.Ordinal)
            .Replace("\n", "\\n", StringComparison.Ordinal)
            .Replace("\r", "\\n", StringComparison.Ordinal);

    // RFC 5545 requires parameter values containing ',', ';' or ':' to be quoted. RFC 6868
    // supplies the encoding for characters (notably a quote) that cannot appear literally there.
    private static string EncodeParameter(string value)
        => value
            .Replace("^", "^^", StringComparison.Ordinal)
            .Replace("\"", "^'", StringComparison.Ordinal)
            .Replace("\r\n", "^n", StringComparison.Ordinal)
            .Replace("\n", "^n", StringComparison.Ordinal)
            .Replace("\r", "^n", StringComparison.Ordinal);

    // RFC 5545 limits a content line to 75 octets. UTF-8 characters may span multiple bytes, so
    // fold by rune rather than by char and account for the continuation space on following lines.
    private static string FoldLine(string line)
    {
        var result = new StringBuilder(line.Length + 8);
        var octets = 0;
        foreach (var rune in line.EnumerateRunes())
        {
            var runeLength = rune.Utf8SequenceLength;
            var limit = result.Length == 0 ? 75 : 74;
            if (octets + runeLength > limit)
            {
                result.Append("\r\n ");
                octets = 0;
            }

            result.Append(rune.ToString());
            octets += runeLength;
        }

        return result.ToString();
    }
}
