using TrustCabinet.Domain.Recordings;

namespace TrustCabinet.Domain.Transcription;

/// <summary>
/// How sure the recogniser was of each word of a line, 0–100 (per cent), as the speech worker measured it (team test 21/11/2026: mark
/// the words the machine was not sure of, so the Secretary knows where to listen again). Like <see cref="WordTimings"/>,
/// a score is only ever the recogniser's own and only while the words still match it one for one: a corrected line has
/// none.
/// </summary>
public static class WordScores
{
    private static int WordCount(string text) => text.Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries).Length;

    /// <summary>The worker's scores for a line it wrote, kept only while they match its words.</summary>
    public static int[]? Of(string text, IReadOnlyList<int>? scores)
        => scores is { Count: > 0 } && scores.Count == WordCount(text) ? [.. scores] : null;

    /// <summary>A transcript line as it is read: no scores once a person changed its words.</summary>
    public static IReadOnlyList<int>? ForLine(string text, IReadOnlyList<int>? scores, bool edited)
        => edited ? null : Of(text, scores);

    /// <summary>A per-mic sentence's scores: its segments' scores one after the other, as its text joins their words.</summary>
    public static IReadOnlyList<int>? ForClip(string text, IReadOnlyList<SpeechClipSegment>? segments, bool edited)
    {
        if (edited || segments is not { Count: > 0 }) return null;
        var all = new List<int>();
        foreach (var segment in segments)
        {
            if (segment.WordConfidence is not { } scores || scores.Count != segment.Words) return null;
            all.AddRange(scores);
        }
        return Of(text, all);
    }
}
