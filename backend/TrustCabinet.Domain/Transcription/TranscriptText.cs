using System.Globalization;
using System.Text;
using TrustCabinet.Domain.Entities;

namespace TrustCabinet.Domain.Transcription;

/// <summary>One line of an exported transcript: when, who, what.</summary>
public sealed record TranscriptLine(string Time, string Speaker, string Text);

/// <summary>
/// A transcript as a plain text file (UTF-8), as AI-eCabinet exports one: a header that says what it is and that a
/// machine wrote it, then one <c>[time] speaker: text</c> line per segment, ready to paste into the minutes.
/// </summary>
public static class TranscriptText
{
    /// <summary>Times in the file are the meeting's own: the organisation is in Vietnam (UTC+7, no summer time).</summary>
    public static readonly TimeSpan Vietnam = TimeSpan.FromHours(7);

    public static string Render(string title, string source, DateTimeOffset? startedAt, DateTimeOffset exportedAt,
        IEnumerable<TranscriptLine> lines)
    {
        var text = new StringBuilder();
        text.Append(title).Append('\n');
        text.Append("Bản gỡ băng — ").Append(source).Append('\n');
        if (startedAt is { } started) text.Append("Cuộc họp bắt đầu: ").Append(Format(started)).Append('\n');
        text.Append("Xuất lúc: ").Append(Format(exportedAt))
            .Append(". Chữ do máy nhận dạng giọng nói viết, có thể còn sai; nghe lại bản ghi khi cần.\n\n");
        foreach (var line in lines) text.Append('[').Append(line.Time).Append("] ").Append(line.Speaker).Append(": ").Append(line.Text).Append('\n');
        return text.ToString();
    }

    /// <summary>Offset into a recording, as <c>HH:MM:SS</c>.</summary>
    public static string Offset(long ms)
    {
        var at = TimeSpan.FromMilliseconds(Math.Max(0, ms));
        return $"{(int)at.TotalHours:00}:{at.Minutes:00}:{at.Seconds:00}";
    }

    /// <summary>
    /// When a line of a recording was said: the wall clock for the room mic, whose takes know when each was spoken
    /// (ADR-0041) — the take's start plus how far into it the line begins — and the offset into the file for an upload,
    /// which knows no clock. Read as an offset, the room recording drifted from the clock by every pause between takes:
    /// a line at 01:01:11 into the audio of the meeting of 22/11/2026 was said at 15:31, 70 minutes after a 14:20 start.
    /// </summary>
    public static string LineTime(long ms, IReadOnlyList<RecordingTake>? takes)
    {
        if (takes is not { Count: > 0 }) return Offset(ms);
        var take = takes.LastOrDefault(t => t.OffsetMs <= ms) ?? takes[0];
        return Clock(take.StartedAt.AddMilliseconds(Math.Max(0, ms - take.OffsetMs)));
    }

    /// <summary>A moment of the meeting, as the wall clock in the room showed it.</summary>
    public static string Clock(DateTimeOffset at) => at.ToOffset(Vietnam).ToString("HH:mm:ss", CultureInfo.InvariantCulture);

    public static string FileName(DateTimeOffset startedAt)
        => $"ban-go-bang-{startedAt.ToOffset(Vietnam).ToString("dd-MM-yyyy-HHmm", CultureInfo.InvariantCulture)}.txt";

    private static string Format(DateTimeOffset at) => at.ToOffset(Vietnam).ToString("dd/MM/yyyy HH:mm", CultureInfo.InvariantCulture);
}

/// <summary>A text file for the browser to save.</summary>
public sealed record ExportedText(string FileName, string Text);
