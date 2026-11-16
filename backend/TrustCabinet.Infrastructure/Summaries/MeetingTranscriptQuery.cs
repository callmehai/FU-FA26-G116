using Microsoft.EntityFrameworkCore;
using TrustCabinet.Application.Summaries;
using TrustCabinet.Domain.Enums;

namespace TrustCabinet.Infrastructure.Summaries;

public sealed class MeetingTranscriptQuery(TrustCabinetDbContext db) : IMeetingTranscriptQuery
{
    public Task<bool> IsHybridAsync(Guid meetingId, CancellationToken ct)
        => db.Meetings.AnyAsync(m => m.Id == meetingId && m.Mode == MeetingMode.Hybrid, ct);

    public async Task<IReadOnlyList<SpokenClip>> ListClipsAsync(Guid meetingId, CancellationToken ct)
        => await db.SpeechClips.AsNoTracking()
            .Where(c => c.MeetingId == meetingId && c.ExcludedAt == null && c.Text != "")
            .Select(c => new SpokenClip(c.Id, c.StartedAt, c.DurationMs, c.Text,
                c.Speaker != null ? c.Speaker.FullName
                    : c.GuestInvitation != null ? c.GuestInvitation.SelfDeclaredDisplayName ?? c.GuestInvitation.InvitedName : null))
            .ToListAsync(ct);

    public async Task<IReadOnlyList<TranscribedRecording>> ListRecordingsAsync(Guid meetingId, CancellationToken ct)
        => await db.Recordings.AsNoTracking()
            .Where(r => r.MeetingId == meetingId && r.Status == RecordingStatus.Available && r.DeletedAt == null
                && r.Transcript != null && r.Transcript.Status == TranscriptStatus.Completed)
            .Select(r => new TranscribedRecording(r.Id, r.Takes, r.CreatedAt, r.Transcript!.Id))
            .ToListAsync(ct);

    public async Task<IReadOnlyDictionary<string, string?>> ListSpeakerNamesAsync(Guid transcriptId, CancellationToken ct)
        => await db.TranscriptSpeakers.AsNoTracking()
            .Where(s => s.TranscriptId == transcriptId)
            .Select(s => new
            {
                s.Label,
                Name = s.AssignedUser != null ? s.AssignedUser.FullName
                    : s.AssignedGuest != null ? s.AssignedGuest.SelfDeclaredDisplayName ?? s.AssignedGuest.InvitedName : null,
            })
            .ToDictionaryAsync(s => s.Label, s => s.Name, ct);

    public async Task<IReadOnlyList<SpokenSegment>> ListSegmentsAsync(Guid transcriptId, CancellationToken ct)
        => await db.TranscriptSegments.AsNoTracking()
            .Where(s => s.TranscriptId == transcriptId && s.Text != "")
            .OrderBy(s => s.Position)
            .Select(s => new SpokenSegment(s.Id, s.StartMs, s.EndMs, s.Text, s.SpeakerLabel))
            .ToListAsync(ct);
}
