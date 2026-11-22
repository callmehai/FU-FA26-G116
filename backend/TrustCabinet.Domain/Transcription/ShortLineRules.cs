namespace TrustCabinet.Domain.Transcription;

/// <summary>
/// Which very short lines the machine leaves out (team test 21/11/2026). A room recording put a "Người nói N" of its
/// own on a breath or a café neighbour's "Ừ.", and an online member's own mic stored fifteen lone "Ừ." sentences: lines
/// nobody can use in the minutes, and voices the Secretary cannot name. A long line is always kept whole.
/// </summary>
public static class ShortLineRules
{
    /// <summary>A line with fewer characters than this is short.</summary>
    public const int MinChars = 10;

    /// <summary>A line said in less time than this is short, when it also has fewer than <see cref="QuickMaxChars"/>
    /// characters.</summary>
    public const long MinDurationMs = 3000;

    /// <summary>
    /// A line said quickly yet this long is a real sentence ("Nó có còn phải test là có nền không?", 36 characters in
    /// 2.2 s), not a breath or a "Ừ. OK." (PO 22/11/2026): only lines shorter than this are short for their time.
    /// </summary>
    public const int QuickMaxChars = 20;

    /// <summary>
    /// Interjections that carry no content on their own: a nod said aloud, a hesitation. "Vâng", "Dạ", "Có", "Không",
    /// "Ok", "Đồng ý" are answers and stay.
    /// </summary>
    private static readonly HashSet<string> Fillers = new(StringComparer.Ordinal)
    {
        "ừ", "ừm", "ừa", "ờ", "ờm", "ơ", "à", "ạ", "ồ", "ô", "ê", "ư", "hả", "hử", "hừ", "ủa",
    };

    private static readonly char[] Marks = ['.', ',', '?', '!', ':', ';', '…', '"', '\'', '(', ')', '-'];

    public static bool IsShort(string text, long durationMs)
    {
        var chars = text.Trim().Length;
        return chars < MinChars || (durationMs < MinDurationMs && chars < QuickMaxChars);
    }

    /// <summary>True when every word of the line is an interjection ("Ừ.", "À. Ừ.", "Ờ ờ"); false for an empty line.</summary>
    public static bool IsFillerOnly(string text)
    {
        var words = text.Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries)
            .Select(w => w.Trim(Marks).ToLowerInvariant()).Where(w => w.Length > 0).ToList();
        return words.Count > 0 && words.All(Fillers.Contains);
    }

    /// <summary>
    /// A line of a room recording stays unless it is short and either nobody's enrolled voice matched its voice
    /// (<paramref name="attributed"/> false: it would only become a "Người nói N" of a breath or a stranger) or it is
    /// nothing but interjections.
    /// </summary>
    public static bool KeepsRoomLine(string text, long durationMs, bool attributed)
        => !IsShort(text, durationMs) || (attributed && !IsFillerOnly(text));

    /// <summary>
    /// A sentence from someone's own mic is theirs, so only one that is nothing but interjections is left out.
    /// </summary>
    public static bool KeepsClip(string text) => !IsFillerOnly(text);

    /// <summary>
    /// The voices that still say a line, renamed "SPEAKER_00", "SPEAKER_01"… in the order they first speak, as the
    /// worker numbers them: a voice left with no line is gone, and "Người nói N" on screen has no gaps.
    /// </summary>
    public static IReadOnlyDictionary<string, string> Renumber(IEnumerable<string?> labelsInOrder)
    {
        var renamed = new Dictionary<string, string>(StringComparer.Ordinal);
        foreach (var label in labelsInOrder)
            if (!string.IsNullOrEmpty(label) && !renamed.ContainsKey(label)) renamed[label] = $"SPEAKER_{renamed.Count:00}";
        return renamed;
    }
}
