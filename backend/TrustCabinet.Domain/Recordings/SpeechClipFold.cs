using System.Globalization;
using System.Text;

namespace TrustCabinet.Domain.Recordings;

/// <summary>
/// One stretch of speech the worker found in a sentence: when, from the sentence's start, how many words of its text,
/// and how loud it reached the mic before any levelling. Kept so a sentence one mic overheard from another can be told
/// apart from the speaker's own (<see cref="SpeechClipFold"/>); never the audio.
/// </summary>
public sealed record SpeechClipSegment(long StartMs, long EndMs, int Words, double? LevelDbfs);

/// <summary>What <see cref="SpeechClipFold"/> reads of one sentence. <c>OffsetMs</c> is its start on the meeting clock.</summary>
public sealed record FoldClip(Guid Id, Guid SpeakerId, long OffsetMs, long DurationMs, string Text,
    IReadOnlyList<SpeechClipSegment> Segments, bool Excluded, bool Edited);

/// <summary>A stretch of a sentence, in ms from its start, that probably is <c>Of</c>'s speaker overheard.</summary>
public sealed record LeakSpan(long StartMs, long EndMs, Guid Of);

/// <summary>The verdict on one sentence: <c>LeakOf</c> is set when all of it reads as another attendee's sentence.</summary>
public sealed record FoldMark(Guid? LeakOf, IReadOnlyList<LeakSpan> Spans);

/// <summary>
/// SCRUM-109 — marks what one person's mic probably caught from another's, as AI-eCabinet's per-segment Fold does.
/// In an in-person meeting every laptop hears the people next to it; the same words then arrive twice, once loud from
/// the speaker's own mic and once quiet from the neighbour's.
///
/// A segment of a sentence is marked when another attendee's sentence, at the clock offset within ±2 s that lines the
/// two up best (laptop clocks disagree), was speaking through at least 60 % of it, holds at least half of its words
/// with at most three words that copy never said, and was at least 6 dB louder there. Measured on synthetic
/// meetings built from VIVOS (workers/speech/eval/bleed.py): duplicated words 114 % → 36 %, no word of a mic's owner
/// marked. It only marks: nothing is hidden or deleted until the Chair or the Secretary excludes it.
///
/// workers/speech/eval/fold.py is the same rule in Python for that measurement; both run the cases in
/// tests/TrustCabinet.Web.Tests/Recordings/speech-fold-cases.json.
/// </summary>
public static class SpeechClipFold
{
    public const long LagMs = 2000;
    public const long CoverPercent = 60;
    public const double LevelGapDb = 6.0;
    public const double Containment = 0.5;
    public const int StrayWords = 3;
    public const long WordSlackMs = 1000;

    public static IReadOnlyDictionary<Guid, FoldMark> Fold(IReadOnlyList<FoldClip> clips)
    {
        var byStart = clips.OrderBy(c => c.OffsetMs).ThenBy(c => c.Id).ToList();
        var marks = new Dictionary<Guid, FoldMark>();
        foreach (var q in byStart)
        {
            if (q.Excluded || !Measured(q)) continue;
            var words = Words(q.Text);
            var spans = new List<LeakSpan>();
            var at = 0;
            foreach (var segment in q.Segments)
            {
                var said = Tokens(string.Join(' ', words.Skip(at).Take(segment.Words)));
                at += segment.Words;
                if (segment.Words == 0) continue;
                if (LeakOf(q, segment, said, byStart) is { } of) spans.Add(new(segment.StartMs, segment.EndMs, of));
            }
            if (spans.Count == 0) continue;
            Guid? whole = null;
            if (spans.Count == q.Segments.Count(s => s.Words > 0))
                whole = spans.GroupBy(s => s.Of).OrderByDescending(g => g.Sum(s => s.EndMs - s.StartMs))
                    .ThenBy(g => g.Key).First().Key;
            marks[q.Id] = new(whole, spans);
        }
        return marks;
    }

    /// <summary>Segments that still account for every word of the text: not edited by hand, as the worker left them.</summary>
    public static bool Measured(FoldClip clip)
        => !clip.Edited && clip.Segments.Count > 0
            && clip.Segments.All(s => s.Words >= 0 && s.EndMs >= s.StartMs)
            && clip.Segments.Sum(s => s.Words) == Words(clip.Text).Length;

    private static Guid? LeakOf(FoldClip q, SpeechClipSegment s, Dictionary<string, int> said, List<FoldClip> byStart)
    {
        if (s.LevelDbfs is not { } level) return null;
        long q0 = q.OffsetMs + s.StartMs, q1 = q.OffsetMs + s.EndMs;
        Guid? best = null;
        var bestGap = double.NegativeInfinity;
        foreach (var l in byStart)
        {
            if (l.OffsetMs > q1 + LagMs) break;
            if (l.SpeakerId == q.SpeakerId || l.Excluded || !Measured(l) || l.OffsetMs + l.DurationMs < q0 - LagMs) continue;
            var (lag, covered) = BestLag(q0, q1, l);
            if (covered * 100 < CoverPercent * (q1 - q0)) continue;
            var copy = WordsBetween(l, q0 + lag - WordSlackMs, q1 + lag + WordSlackMs);
            if (ContainmentOf(said, copy) < Containment || Strays(said, copy) > StrayWords) continue;
            var gap = LevelBetween(l, q0 + lag, q1 + lag) - level;
            if (gap < LevelGapDb || gap <= bestGap) continue;
            best = l.Id;
            bestGap = gap;
        }
        return best;
    }

    /// <summary>The shift of [q0, q1) within ±LagMs that l's speech covers most; the smallest shift wins a tie.</summary>
    private static (long Lag, long Covered) BestLag(long q0, long q1, FoldClip l)
    {
        var candidates = new List<long> { 0 };
        foreach (var t in l.Segments)
        {
            candidates.Add(l.OffsetMs + t.StartMs - q0);
            candidates.Add(l.OffsetMs + t.EndMs - q1);
        }
        long lag = 0, covered = -1;
        foreach (var candidate in candidates)
        {
            var c = Math.Clamp(candidate, -LagMs, LagMs);
            long got = 0;
            foreach (var t in l.Segments)
                got += Math.Max(0, Math.Min(q1 + c, l.OffsetMs + t.EndMs) - Math.Max(q0 + c, l.OffsetMs + t.StartMs));
            if (got > covered || (got == covered && Math.Abs(c) < Math.Abs(lag))) (lag, covered) = (c, got);
        }
        return (lag, covered);
    }

    /// <summary>l's level over [from, to): the power mean of its segments there, weighted by length.</summary>
    private static double LevelBetween(FoldClip l, long from, long to)
    {
        double power = 0, weight = 0;
        foreach (var t in l.Segments)
        {
            if (t.LevelDbfs is not { } db || l.OffsetMs + t.EndMs <= from || l.OffsetMs + t.StartMs >= to) continue;
            var length = t.EndMs - t.StartMs;
            power += length * Math.Pow(10, db / 10);
            weight += length;
        }
        return weight == 0 ? double.NegativeInfinity : 10 * Math.Log10(power / weight);
    }

    private static Dictionary<string, int> WordsBetween(FoldClip l, long from, long to)
    {
        var words = Words(l.Text);
        var said = new List<string>();
        var at = 0;
        foreach (var t in l.Segments)
        {
            if (l.OffsetMs + t.EndMs > from && l.OffsetMs + t.StartMs < to) said.AddRange(words.Skip(at).Take(t.Words));
            at += t.Words;
        }
        return Tokens(string.Join(' ', said));
    }

    private static string[] Words(string text) => text.Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries);

    /// <summary>Lower-case words without punctuation, counted with repeats.</summary>
    public static Dictionary<string, int> Tokens(string text)
    {
        var counts = new Dictionary<string, int>(StringComparer.Ordinal);
        var lower = text.Normalize(NormalizationForm.FormC).ToLower(CultureInfo.InvariantCulture);
        var start = -1;
        for (var i = 0; i <= lower.Length; i++)
        {
            if (i < lower.Length && IsWordChar(lower[i]))
            {
                if (start < 0) start = i;
                continue;
            }
            if (start < 0) continue;
            var word = lower[start..i];
            counts[word] = counts.GetValueOrDefault(word) + 1;
            start = -1;
        }
        return counts;
    }

    private static bool IsWordChar(char ch) => char.IsLetterOrDigit(ch)
        || CharUnicodeInfo.GetUnicodeCategory(ch) is UnicodeCategory.NonSpacingMark or UnicodeCategory.SpacingCombiningMark;

    private static double ContainmentOf(Dictionary<string, int> q, Dictionary<string, int> l)
    {
        var total = q.Values.Sum();
        return total == 0 ? 0 : (double)q.Sum(p => Math.Min(p.Value, l.GetValueOrDefault(p.Key))) / total;
    }

    private static int Strays(Dictionary<string, int> q, Dictionary<string, int> l)
        => q.Sum(p => Math.Max(0, p.Value - l.GetValueOrDefault(p.Key)));
}
