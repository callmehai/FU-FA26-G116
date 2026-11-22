using TrustCabinet.Domain.Recordings;

namespace TrustCabinet.Domain.Transcription;

/// <summary>
/// When each word of a line was said, as the speech worker measured it, so a transcript can follow the voice word by
/// word while it plays. A time is only ever the recogniser's own: when the words no longer match the times one for one
/// (the Secretary corrected the line, the text was cut to its limit, an older worker sent none) there is no timing at
/// all, and the line is followed as a whole. Times are never spread over the words to fake one.
/// </summary>
public static class WordTimings
{
    private static int WordCount(string text) => text.Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries).Length;

    /// <summary>The worker's times for a line it wrote, kept only while they match its words.</summary>
    public static long[]? Of(string text, IReadOnlyList<long>? starts)
        => starts is { Count: > 0 } && starts.Count == WordCount(text) ? [.. starts] : null;

    /// <summary>A transcript line as it is read: no timing once a person changed its words, and none outside the line.</summary>
    public static IReadOnlyList<long>? ForLine(string text, IReadOnlyList<long>? starts, bool edited, long startMs, long endMs)
        => edited ? null : Within(Of(text, starts), startMs, endMs);

    /// <summary>
    /// Word starts kept inside their line, in order. A room recording's line is cut to the take holding most of it
    /// (<see cref="TranscriptRules.FitToTake"/>) or to where an erased take began, while the worker timed its words on
    /// the whole stretch it heard: the words said across the cut started before the line or after it ended (6 of 664
    /// lines of the meeting of 22/11/2026), and the highlight then left the line it was following. A word across the
    /// cut now starts at the line's edge.
    /// </summary>
    public static long[]? Within(IReadOnlyList<long>? starts, long startMs, long endMs)
    {
        if (starts is null) return null;
        var high = Math.Max(startMs, endMs);
        var within = new long[starts.Count];
        for (var i = 0; i < starts.Count; i++)
        {
            var at = Math.Clamp(starts[i], startMs, high);
            within[i] = i > 0 ? Math.Max(at, within[i - 1]) : at;
        }
        return within;
    }

    /// <summary>
    /// A per-mic sentence's word times, in ms from its start: its segments' times one after the other, as its text
    /// joins their words. Null when it was edited, or when any segment has none.
    /// </summary>
    public static IReadOnlyList<long>? ForClip(string text, IReadOnlyList<SpeechClipSegment>? segments, bool edited)
    {
        if (edited || segments is not { Count: > 0 }) return null;
        var all = new List<long>();
        foreach (var segment in segments)
        {
            if (segment.WordStartsMs is not { } starts || starts.Count != segment.Words) return null;
            all.AddRange(starts);
        }
        return Of(text, all);
    }
}
