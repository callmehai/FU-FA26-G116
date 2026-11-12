using System.Globalization;
using System.Text;

namespace TrustCabinet.Domain.Summaries;

/// <summary>
/// ADR-0061 — in a hybrid meeting the online attendees are heard in the room through its speakers, so the room mic
/// records their words a second time. Their own mic already has them, named by account; the room's copy is an echo.
/// A room segment is an echo when it is said at the same time as an online sentence (give or take <see cref="Slack"/>)
/// and most of its words are that sentence's words. Anything said in the room over an online speaker stays.
/// </summary>
public static class RoomEcho
{
    /// <summary>The room's speakers and the transcription's timing are not exact.</summary>
    public static readonly TimeSpan Slack = TimeSpan.FromSeconds(2);

    /// <summary>Share of the room segment's words that must be in the online sentence.</summary>
    public const double Overlap = 0.6;

    public sealed record Span(DateTimeOffset Start, DateTimeOffset End, string Text);

    public static bool IsEcho(Span room, IEnumerable<Span> online)
    {
        var words = Words(room.Text);
        if (words.Count == 0) return false;
        foreach (var sentence in online)
        {
            if (sentence.End + Slack < room.Start || room.End + Slack < sentence.Start) continue;
            var theirs = Words(sentence.Text);
            var shared = words.Count(theirs.Contains);
            if (shared >= Math.Min(2, words.Count) && shared >= Overlap * words.Count) return true;
        }
        return false;
    }

    /// <summary>Lower-case words without accents or punctuation: two transcriptions of one voice rarely agree on tones.</summary>
    public static HashSet<string> Words(string text)
    {
        var plain = new StringBuilder(text.Length);
        foreach (var c in text.Normalize(NormalizationForm.FormD))
        {
            if (CharUnicodeInfo.GetUnicodeCategory(c) == UnicodeCategory.NonSpacingMark) continue;
            plain.Append(c == 'đ' || c == 'Đ' ? 'd' : char.IsLetterOrDigit(c) ? char.ToLowerInvariant(c) : ' ');
        }
        return [.. plain.ToString().Split(' ', StringSplitOptions.RemoveEmptyEntries)];
    }
}
