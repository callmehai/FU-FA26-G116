using TrustCabinet.Domain.Entities;
using TrustCabinet.Domain.Summaries;

namespace TrustCabinet.Application.Summaries;

/// <summary>
/// Everything said in a meeting as one timeline, for the summary (SCRUM-36): the sentences of each person's mic
/// (SCRUM-109) and the segments of the room mic or an uploaded file (SCRUM-31). Excluded sentences stay out (UC-54b).
/// Numbers read aloud arrive as digits (<see cref="SpokenNumbers"/>). In a hybrid meeting the room mic's copy of an
/// online attendee's words, heard through the room's speakers, is left out: their own mic has them (ADR-0061). Called
/// inside the caller's unit of work, which translates a storage failure.
/// </summary>
public sealed class MeetingTranscriptReader(IMeetingTranscriptQuery query)
{
    public async Task<IReadOnlyList<TranscriptLine>> ReadAsync(Guid meetingId, CancellationToken ct)
    {
        var lines = new List<TranscriptLine>();
        var hybrid = await query.IsHybridAsync(meetingId, ct);

        var clips = await query.ListClipsAsync(meetingId, ct);
        lines.AddRange(clips.Select(c => new TranscriptLine(TranscriptLine.ClipRef(c.Id), c.StartedAt, c.Speaker ?? "Người nói",
            SpokenNumbers.Normalise(c.Text.Trim()))));
        RoomEcho.Span[] online = hybrid
            ? [.. clips.Select(c => new RoomEcho.Span(c.StartedAt, c.StartedAt.AddMilliseconds(c.DurationMs), c.Text))]
            : [];

        var recordings = await query.ListRecordingsAsync(meetingId, ct);
        foreach (var recording in recordings)
        {
            var speakers = await query.ListSpeakerNamesAsync(recording.TranscriptId, ct);
            var segments = await query.ListSegmentsAsync(recording.TranscriptId, ct);
            foreach (var segment in segments)
            {
                var at = WallClock(recording.Takes, recording.CreatedAt, segment.StartMs);
                if (online.Length > 0
                    && RoomEcho.IsEcho(new(at, at.AddMilliseconds(Math.Max(0, segment.EndMs - segment.StartMs)), segment.Text), online))
                    continue;
                var name = speakers.GetValueOrDefault(segment.SpeakerLabel) ?? LabelName(segment.SpeakerLabel);
                lines.Add(new(TranscriptLine.SegmentRef(segment.Id), at, name, SpokenNumbers.Normalise(segment.Text.Trim())));
            }
        }

        return [.. lines.OrderBy(l => l.At)];
    }

    /// <summary>
    /// A room take knows when it started (ADR-0041); an uploaded file does not, so its segments count from when it was
    /// uploaded, which keeps their order but not their place against the agenda.
    /// </summary>
    private static DateTimeOffset WallClock(List<RecordingTake>? takes, DateTimeOffset uploadedAt, long ms)
    {
        if (takes is not { Count: > 0 }) return uploadedAt.AddMilliseconds(ms);
        var take = takes.LastOrDefault(t => t.OffsetMs <= ms) ?? takes[0];
        return take.StartedAt.AddMilliseconds(ms - take.OffsetMs);
    }

    /// <summary>"SPEAKER_00" reads as "Người nói 1", as on screen.</summary>
    private static string LabelName(string label)
        => label.StartsWith("SPEAKER_", StringComparison.Ordinal) && int.TryParse(label[8..], out var n)
            ? $"Người nói {n + 1}" : label.Length > 0 ? label : "Người nói";
}
