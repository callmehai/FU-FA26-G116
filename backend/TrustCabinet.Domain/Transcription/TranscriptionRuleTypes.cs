using TrustCabinet.Domain.Entities;

namespace TrustCabinet.Domain.Transcription;

public static class TranscriptRules
{
    public const int MaxSegmentText = 8000;

    /// <summary>Gipformer, run in-house; the version pins which model wrote a given transcript.</summary>
    public static string Provider(string asrModel) => $"self-hosted:{asrModel}";

    /// <summary>
    /// A segment of a room recording (ADR-0041) placed in the take that holds most of it, and cut to that take. The
    /// worker lays segments end to end, each starting where the one before ended, so a segment can start in the take
    /// before the one its words were said in; read by its start, the words of one take showed under the previous one.
    /// </summary>
    public static (long StartMs, long EndMs) FitToTake(long startMs, long endMs, IReadOnlyList<RecordingTake>? takes)
    {
        if (takes is not { Count: > 1 }) return (startMs, endMs);
        RecordingTake? best = null;
        var most = 0L;
        foreach (var take in takes)
        {
            var overlap = Math.Min(endMs, take.OffsetMs + take.DurationMs) - Math.Max(startMs, take.OffsetMs);
            // A tie goes to the later take: the worker stretches a segment back, never forward.
            if (overlap > 0 && overlap >= most) (best, most) = (take, overlap);
        }
        return best is null ? (startMs, endMs)
            : (Math.Max(startMs, best.OffsetMs), Math.Min(endMs, best.OffsetMs + best.DurationMs));
    }
}
