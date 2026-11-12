using Microsoft.EntityFrameworkCore;
using TrustCabinet.Domain.Entities;
using TrustCabinet.Domain.Enums;
using TrustCabinet.Domain.Summaries;

namespace TrustCabinet.Infrastructure.Summaries;

/// <summary>
/// Everything said in a meeting as one timeline, for the summary (SCRUM-36): the sentences of each person's mic
/// (SCRUM-109) and the segments of the room mic or an uploaded file (SCRUM-31). Excluded sentences stay out (BR-358).
/// Numbers read aloud arrive as digits (<see cref="SpokenNumbers"/>). In a hybrid meeting the room mic's copy of an
/// online attendee's words, heard through the room's speakers, is left out: their own mic has them (ADR-0061).
/// </summary>
public sealed class MeetingTranscriptReader(TrustCabinetDbContext db)
{
    public async Task<IReadOnlyList<TranscriptLine>> ReadAsync(Guid meetingId, CancellationToken ct)
    {
        var lines = new List<TranscriptLine>();
        var hybrid = await db.Meetings.AnyAsync(m => m.Id == meetingId && m.Mode == MeetingMode.Hybrid, ct);

        var clips = await db.SpeechClips.AsNoTracking()
            .Where(c => c.MeetingId == meetingId && c.ExcludedAt == null && c.Text != "")
            .Select(c => new
            {
                c.Id, c.StartedAt, c.DurationMs, c.Text,
                Speaker = c.Speaker != null ? c.Speaker.FullName
                    : c.GuestInvitation != null ? c.GuestInvitation.SelfDeclaredDisplayName ?? c.GuestInvitation.InvitedName : null,
            })
            .ToListAsync(ct);
        lines.AddRange(clips.Select(c => new TranscriptLine(TranscriptLine.ClipRef(c.Id), c.StartedAt, c.Speaker ?? "Người nói",
            SpokenNumbers.Normalise(c.Text.Trim()))));
        RoomEcho.Span[] online = hybrid
            ? [.. clips.Select(c => new RoomEcho.Span(c.StartedAt, c.StartedAt.AddMilliseconds(c.DurationMs), c.Text))]
            : [];

        var recordings = await db.Recordings.AsNoTracking()
            .Where(r => r.MeetingId == meetingId && r.Status == RecordingStatus.Available && r.DeletedAt == null
                && r.Transcript != null && r.Transcript.Status == TranscriptStatus.Completed)
            .Select(r => new { r.Id, r.Takes, r.CreatedAt, TranscriptId = r.Transcript!.Id })
            .ToListAsync(ct);
        foreach (var recording in recordings)
        {
            var speakers = await db.TranscriptSpeakers.AsNoTracking()
                .Where(s => s.TranscriptId == recording.TranscriptId)
                .Select(s => new
                {
                    s.Label,
                    Name = s.AssignedUser != null ? s.AssignedUser.FullName
                        : s.AssignedGuest != null ? s.AssignedGuest.SelfDeclaredDisplayName ?? s.AssignedGuest.InvitedName : null,
                })
                .ToDictionaryAsync(s => s.Label, s => s.Name, ct);
            var segments = await db.TranscriptSegments.AsNoTracking()
                .Where(s => s.TranscriptId == recording.TranscriptId && s.Text != "")
                .OrderBy(s => s.Position)
                .Select(s => new { s.Id, s.StartMs, s.EndMs, s.Text, s.SpeakerLabel })
                .ToListAsync(ct);
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
