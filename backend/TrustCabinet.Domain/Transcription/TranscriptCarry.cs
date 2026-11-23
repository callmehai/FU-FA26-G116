using TrustCabinet.Domain.Entities;

namespace TrustCabinet.Domain.Transcription;

/// <summary>
/// ADR-0077 — what the Secretary corrected by hand in a transcript survives the next run of the same recording. A room
/// recording is transcribed again as a whole after every take (ADR-0041), every five minutes since the room mic saves a
/// take that often, and the diarizer numbers its voices afresh each time: "SPEAKER_00" of one run may be "SPEAKER_02" of
/// the next. Lines and voices are therefore matched by time, on the part of the audio both runs heard; never by voice,
/// so nothing biometric is read here.
/// </summary>
public static class TranscriptCarry
{
    /// <summary>A new voice takes an old voice's name when at least this share of its speech in the old run's audio was that voice's.</summary>
    public const double VoiceShare = 0.6;

    /// <summary>…and at least this much of it: a second of agreement is a coincidence, not the same person.</summary>
    public const long MinVoiceOverlapMs = 2000;

    /// <summary>A new line is the old one when both its ends are within this of the old one's…</summary>
    public const long LineToleranceMs = 500;

    /// <summary>…or when they share at least this much of the longer of the two.</summary>
    public const double LineOverlap = 0.8;

    /// <summary>A stretch of a transcript said in a voice. A null label is a line the Secretary moved: not the diarizer's word.</summary>
    public readonly record struct Span(long StartMs, long EndMs, string? Label);

    /// <summary>
    /// For each voice of the new run, the voice of the old run it is: the old voice that holds at least
    /// <see cref="VoiceShare"/> (and <see cref="MinVoiceOverlapMs"/>) of the new voice's speech within the old run's audio,
    /// lines moved by hand left out on both sides of the sum. A new voice with no such old voice is not in the result.
    /// Several new voices may be the same old one: the diarizer split a person it heard as one before.
    /// </summary>
    public static IReadOnlyDictionary<string, string> Voices(IReadOnlyList<Span> before, IReadOnlyList<Span> after)
    {
        var result = new Dictionary<string, string>(StringComparer.Ordinal);
        if (before.Count == 0 || after.Count == 0) return result;
        var heard = before.Max(s => s.EndMs);
        var old = before.OrderBy(s => s.StartMs).ToList();
        var shared = new Dictionary<(string New, string Old), long>();
        var within = new Dictionary<string, long>(StringComparer.Ordinal);
        foreach (var line in after)
        {
            if (string.IsNullOrEmpty(line.Label)) continue;
            var speech = Math.Max(0, Math.Min(line.EndMs, heard) - line.StartMs);
            foreach (var o in old)
            {
                if (o.StartMs >= line.EndMs) break;
                var overlap = Overlap(line, o);
                if (overlap <= 0) continue;
                // A moved line says nothing of how the diarizer heard that stretch: it counts for neither voice.
                if (o.Label is null) speech -= overlap;
                else if (o.Label.Length > 0)
                    shared[(line.Label, o.Label)] = shared.GetValueOrDefault((line.Label, o.Label)) + overlap;
            }
            within[line.Label] = within.GetValueOrDefault(line.Label) + Math.Max(0, speech);
        }
        foreach (var group in shared.GroupBy(p => p.Key.New))
        {
            var best = group.OrderByDescending(p => p.Value).ThenBy(p => p.Key.Old, StringComparer.Ordinal).First();
            var total = within.GetValueOrDefault(group.Key);
            if (best.Value >= MinVoiceOverlapMs && total > 0 && best.Value >= VoiceShare * total)
                result[group.Key] = best.Key.Old;
        }
        return result;
    }

    /// <summary>
    /// Pairs old lines with new ones, one to one: in the same take, both ends within <see cref="LineToleranceMs"/>, or
    /// sharing <see cref="LineOverlap"/> of the longer line. The closest pairs are taken first. Returns, for each index
    /// of <paramref name="before"/> that found a line, the index in <paramref name="after"/>.
    /// </summary>
    public static IReadOnlyDictionary<int, int> Lines(IReadOnlyList<Span> before, IReadOnlyList<Span> after,
        IReadOnlyList<RecordingTake>? takes)
    {
        var pairs = new List<(int Old, int New, long Overlap, long Distance)>();
        for (var i = 0; i < before.Count; i++)
        {
            var o = before[i];
            for (var j = 0; j < after.Count; j++)
            {
                var n = after[j];
                if (Take(o, takes) != Take(n, takes)) continue;
                var overlap = Overlap(o, n);
                var distance = Math.Abs(o.StartMs - n.StartMs) + Math.Abs(o.EndMs - n.EndMs);
                var close = Math.Abs(o.StartMs - n.StartMs) <= LineToleranceMs && Math.Abs(o.EndMs - n.EndMs) <= LineToleranceMs;
                var longer = Math.Max(o.EndMs - o.StartMs, n.EndMs - n.StartMs);
                if (close || (longer > 0 && overlap >= LineOverlap * longer)) pairs.Add((i, j, overlap, distance));
            }
        }
        var result = new Dictionary<int, int>();
        var taken = new HashSet<int>();
        foreach (var pair in pairs.OrderByDescending(p => p.Overlap).ThenBy(p => p.Distance).ThenBy(p => p.Old).ThenBy(p => p.New))
        {
            if (result.ContainsKey(pair.Old) || taken.Contains(pair.New)) continue;
            result[pair.Old] = pair.New;
            taken.Add(pair.New);
        }
        return result;
    }

    private static long Overlap(Span a, Span b) => Math.Max(0, Math.Min(a.EndMs, b.EndMs) - Math.Max(a.StartMs, b.StartMs));

    /// <summary>The take a line was said in, by its middle; an uploaded file is one take.</summary>
    private static int Take(Span line, IReadOnlyList<RecordingTake>? takes)
    {
        if (takes is not { Count: > 1 }) return 0;
        var middle = (line.StartMs + line.EndMs) / 2;
        for (var i = takes.Count - 1; i >= 0; i--)
            if (middle >= takes[i].OffsetMs) return i;
        return 0;
    }
}
