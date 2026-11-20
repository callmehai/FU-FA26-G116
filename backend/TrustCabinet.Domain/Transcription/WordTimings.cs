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

    /// <summary>A transcript line as it is read: no timing once a person changed its words.</summary>
    public static IReadOnlyList<long>? ForLine(string text, IReadOnlyList<long>? starts, bool edited)
        => edited ? null : Of(text, starts);

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
