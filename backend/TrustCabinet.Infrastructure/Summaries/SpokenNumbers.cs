using System.Globalization;
using System.Text;

namespace TrustCabinet.Infrastructure.Summaries;

/// <summary>
/// Turns numbers read aloud in a transcript into digits before the model sees them: "mười tám trên hai mươi" becomes
/// "18 trên 20", "chín mươi phần trăm" "90%", "một phẩy hai tỷ" "1,2 tỷ", "hai không hai bảy" "2027". AI-eCabinet asks its
/// 70B model to do this in the prompt (<c>_DIGITS</c>); a 7B model ignores the rule and copies the words into the minutes,
/// so the transcript arrives already written the way the minutes must read.
///
/// Only what is surely a number changes: a run of two number words or more, or one next to a unit ("hai máy", "ba
/// tuần", "tháng tư"). A lone "một", "năm" or "tư" stays as it is ("một số ý kiến", "năm nay", "thứ tư").
/// </summary>
public static class SpokenNumbers
{
    private static readonly Dictionary<string, int> Digits = new(StringComparer.Ordinal)
    {
        ["không"] = 0, ["một"] = 1, ["mốt"] = 1, ["hai"] = 2, ["ba"] = 3, ["bốn"] = 4, ["tư"] = 4, ["năm"] = 5,
        ["lăm"] = 5, ["sáu"] = 6, ["bảy"] = 7, ["bẩy"] = 7, ["tám"] = 8, ["chín"] = 9,
    };

    /// <summary>Words a count stands before. "năm" (year) and "lần" are left out: "hai năm" reads fine either way.</summary>
    private static readonly HashSet<string> Units = new(StringComparer.Ordinal)
    {
        "phần", "triệu", "tỷ", "tỉ", "nghìn", "ngàn", "trăm", "đồng", "người", "máy", "phòng", "tuần", "ngày", "tháng",
        "nhiệm", "phiếu", "giờ", "phút", "đơn", "cán", "chiếc", "bộ", "hồ", "văn", "trường", "xã", "huyện", "tỉnh",
        "mục", "điểm", "km", "mét", "tấn", "suất", "lớp", "đợt", "quý", "trên",
    };

    /// <summary>Words a lone number stands after: "quý một", "tháng tư", "ngày năm", "số hai".</summary>
    private static readonly HashSet<string> Before = new(StringComparer.Ordinal) { "quý", "tháng", "ngày", "số" };

    public static string Normalise(string text)
    {
        var words = text.Split(' ');
        var output = new List<string>(words.Length);
        var i = 0;
        while (i < words.Length)
        {
            var end = i;
            while (end < words.Length && IsNumberWord(Bare(words[end]), end > i) && !IsYear(words, end)) end++;
            // A run may not end on "phẩy", "lẻ" or "linh", nor on a word with punctuation inside it.
            while (end > i && Bare(words[end - 1]) is "phẩy" or "lẻ" or "linh") end--;
            var length = end - i;
            var next = end < words.Length ? Bare(words[end]) : "";
            var after = i > 0 ? Bare(words[i - 1]) : "";
            if (length > 0 && (length >= 2 || Units.Contains(next) || Before.Contains(after)) && Parse(words, i, end) is { } number)
            {
                var tail = Trailing(words[end - 1]);
                output.Add(Lead(words[i]) + number + tail);
                i = end;
                // "90 phần trăm" reads "90%".
                if (tail.Length == 0 && i + 1 < words.Length && Bare(words[i]) == "phần" && Bare(words[i + 1]) == "trăm")
                {
                    output[^1] += "%" + Trailing(words[i + 1]);
                    i += 2;
                }
                continue;
            }
            output.Add(words[i]);
            i++;
        }
        return string.Join(' ', output);
    }

    /// <summary>
    /// "năm" before a digit read aloud is the year, not five: "quý một năm hai không hai bảy" is "quý 1 năm 2027", and
    /// "ba năm hai tháng" is three years and two months. "năm mươi", "năm trăm" and "năm nghìn" stay numbers.
    /// </summary>
    private static bool IsYear(string[] words, int at) => Bare(words[at]) == "năm" && at + 1 < words.Length
        && Bare(words[at + 1]) is var following && Digits.ContainsKey(following) && following is not ("lăm" or "mốt");

    private static bool IsNumberWord(string word, bool inRun) => word.Length > 0 && (Digits.ContainsKey(word)
        || word is "mười" or "mươi" or "trăm" or "nghìn" or "ngàn" || (inRun && word is "phẩy" or "lẻ" or "linh"));

    /// <summary>The run's value as written in Vietnamese administrative style, or null when it does not parse.</summary>
    private static string? Parse(string[] words, int from, int to)
    {
        var tokens = Enumerable.Range(from, to - from).Select(k => Bare(words[k])).ToList();
        // "hai không hai bảy": digits read one by one, as years and codes are.
        if (tokens.Count >= 3 && tokens.All(t => Digits.ContainsKey(t) && t is not ("lăm" or "mốt" or "tư")))
            return string.Concat(tokens.Select(t => Digits[t].ToString(CultureInfo.InvariantCulture)));
        var comma = tokens.IndexOf("phẩy");
        if (comma >= 0)
        {
            var whole = Value(tokens[..comma]);
            var fraction = tokens[(comma + 1)..];
            if (whole is null || fraction.Count == 0 || !fraction.All(Digits.ContainsKey)) return null;
            return $"{whole},{string.Concat(fraction.Select(t => Digits[t]))}";
        }
        return Value(tokens)?.ToString(CultureInfo.InvariantCulture);
    }

    /// <summary>Reads "bốn mươi lăm", "một trăm lẻ năm", "hai nghìn không trăm hai mươi bảy".</summary>
    private static long? Value(IReadOnlyList<string> tokens)
    {
        if (tokens.Count == 0) return null;
        long total = 0, group = 0, current = 0;
        var seenAny = false;
        foreach (var token in tokens)
        {
            switch (token)
            {
                case "mười":
                    current += 10;
                    break;
                case "mươi":
                    current = (current == 0 ? 1 : current) * 10;
                    break;
                case "trăm":
                    group += (current == 0 ? 1 : current) * 100;
                    current = 0;
                    break;
                case "nghìn" or "ngàn":
                    total += (group + current == 0 ? 1 : group + current) * 1000;
                    group = 0;
                    current = 0;
                    break;
                case "lẻ" or "linh":
                    break;
                default:
                    if (!Digits.TryGetValue(token, out var digit)) return null;
                    current += digit;
                    break;
            }
            seenAny = true;
        }
        return seenAny ? total + group + current : null;
    }

    private static string Bare(string word)
    {
        var start = 0;
        var end = word.Length;
        while (start < end && !char.IsLetterOrDigit(word[start])) start++;
        while (end > start && !char.IsLetterOrDigit(word[end - 1])) end--;
        return word[start..end].ToLower(CultureInfo.GetCultureInfo("vi-VN")).Normalize(NormalizationForm.FormC);
    }

    private static string Lead(string word)
    {
        var start = 0;
        while (start < word.Length && !char.IsLetterOrDigit(word[start])) start++;
        return word[..start];
    }

    private static string Trailing(string word)
    {
        var end = word.Length;
        while (end > 0 && !char.IsLetterOrDigit(word[end - 1])) end--;
        return word[end..];
    }
}
