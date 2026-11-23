using System.Text;
using System.Text.RegularExpressions;

namespace TrustCabinet.Application.Summaries;

/// <summary>
/// Keeps a task's deadline in the draft only when the meeting record says it. The 7B model wrote "trước thứ Tư" for
/// every task of the 20/11 meeting although nobody said "thứ Tư": it copied the prompt's own example (Hải, 24/11/2026).
/// Changing the prompt lowers the odds; this check makes it hold. A deadline is the last " — " part of a task line
/// "- &lt;task&gt; — &lt;owner&gt; — &lt;deadline&gt;"; every time anchor in it (a weekday, a date, an hour, "mai", "tuần", "tháng")
/// must appear in the transcript, the agenda or the meeting title, or the deadline is dropped and the task kept. A last
/// part with no time anchor is the owner, or a deadline that names no time, and is left as written.
/// </summary>
public static partial class DeadlineGrounding
{
    private const string Dash = " — ";

    public static string Apply(string tasks, MeetingFacts facts)
    {
        if (string.IsNullOrWhiteSpace(tasks)) return tasks;
        var record = new StringBuilder(facts.Title).Append('\n');
        foreach (var item in facts.Agenda) record.Append(item.Title).Append('\n').Append(item.Conclusion).Append('\n');
        foreach (var line in facts.Lines) record.Append(line.Text).Append('\n');
        var anchorsSaid = Anchors(Normalise(SpokenNumbers.Normalise(record.ToString())));
        return string.Join('\n', tasks.Split('\n').Select(line => Ground(line, anchorsSaid)));
    }

    internal static string Ground(string line, IReadOnlySet<string> anchorsSaid)
    {
        if (!line.TrimStart().StartsWith('-')) return line;
        var at = line.LastIndexOf(Dash, StringComparison.Ordinal);
        if (at < 0) return line;
        var anchors = Anchors(Normalise(line[(at + Dash.Length)..]));
        return anchors.Count == 0 || anchors.IsSubsetOf(anchorsSaid) ? line : line[..at].TrimEnd();
    }

    /// <summary>The time anchors of a text, in one spelling: "thứ Tư" and "thứ 4" are both "thứ 4", "25/11/2026" is "25/11".</summary>
    internal static HashSet<string> Anchors(string normalised)
    {
        var found = new HashSet<string>(StringComparer.Ordinal);
        foreach (Match m in Weekday().Matches(normalised)) found.Add("thứ " + m.Groups[1].Value);
        if (normalised.Contains("chủ nhật", StringComparison.Ordinal)) found.Add("chủ nhật");
        foreach (Match m in Date().Matches(normalised))
            found.Add($"{int.Parse(m.Groups[1].Value)}/{int.Parse(m.Groups[2].Value)}");
        foreach (Match m in Hour().Matches(normalised)) found.Add($"{int.Parse(m.Groups[1].Value)} giờ");
        if (Tomorrow().IsMatch(normalised)) found.Add("mai");
        if (normalised.Contains("hôm nay", StringComparison.Ordinal)) found.Add("hôm nay");
        if (Week().IsMatch(normalised)) found.Add("tuần");
        if (MonthWord().IsMatch(normalised)) found.Add("tháng");
        return found;
    }

    /// <summary>Lower case, weekdays and spoken dates in digits: "Thứ Tư" → "thứ 4", "ngày 25 tháng 11" → "25/11".</summary>
    internal static string Normalise(string text)
    {
        var lower = text.Normalize(NormalizationForm.FormC).ToLowerInvariant();
        lower = WeekdayWord().Replace(lower, m => "thứ " + (m.Groups[1].Value switch
        {
            "hai" => "2", "ba" => "3", "tư" => "4", "năm" => "5", "sáu" => "6", _ => "7",
        }));
        return SpokenDate().Replace(lower, "$1/$2");
    }

    [GeneratedRegex(@"\bthứ\s+(hai|ba|tư|năm|sáu|bảy|bẩy)\b")]
    private static partial Regex WeekdayWord();

    [GeneratedRegex(@"\bthứ\s*([2-7])\b")]
    private static partial Regex Weekday();

    [GeneratedRegex(@"\bngày\s+(\d{1,2})\s+tháng\s+(\d{1,2})\b")]
    private static partial Regex SpokenDate();

    [GeneratedRegex(@"\b(\d{1,2})\s*/\s*(\d{1,2})(?:\s*/\s*\d{2,4})?\b")]
    private static partial Regex Date();

    [GeneratedRegex(@"\b(\d{1,2})\s*(?:giờ|h)\b")]
    private static partial Regex Hour();

    [GeneratedRegex(@"\b(?:ngày\s+)?mai\b")]
    private static partial Regex Tomorrow();

    [GeneratedRegex(@"\btuần\b")]
    private static partial Regex Week();

    [GeneratedRegex(@"\b(?:cuối|đầu|giữa)\s+tháng\b|\btháng\s+(?:này|sau|tới)\b")]
    private static partial Regex MonthWord();
}
