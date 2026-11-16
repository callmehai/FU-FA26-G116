using Microsoft.EntityFrameworkCore;
using TrustCabinet.Application.Recordings;
using TrustCabinet.Domain.Audit;
using TrustCabinet.Domain.Entities;
using TrustCabinet.Infrastructure.GuestAccess;

namespace TrustCabinet.Infrastructure.Recordings;

public sealed class SpeechClipRepository(TrustCabinetDbContext db) : ISpeechClipRepository
{
    public async Task<IReadOnlyList<ClipRow>> ListRowsAsync(Guid meetingId, CancellationToken ct)
        => await RowsAsync(db.SpeechClips.AsNoTracking().Where(c => c.MeetingId == meetingId), ct);

    public async Task<IReadOnlyList<ClipRow>> ListRowsChangedSinceAsync(Guid meetingId, DateTimeOffset after, CancellationToken ct)
        => await RowsAsync(db.SpeechClips.AsNoTracking().Where(c => c.MeetingId == meetingId).Where(c => c.UpdatedAt > after), ct);

    public async Task<IReadOnlyList<ClipRow>> ListRowsStartedBetweenAsync(Guid meetingId, DateTimeOffset from, DateTimeOffset to,
        CancellationToken ct)
        => await RowsAsync(db.SpeechClips.AsNoTracking().Where(c => c.MeetingId == meetingId)
            .Where(c => c.StartedAt >= from && c.StartedAt <= to), ct);

    public async Task<IReadOnlyList<ClipRow>> ListGuestRowsAsync(Guid meetingId, Guid invitationId, DateTimeOffset? since,
        CancellationToken ct)
    {
        var clips = db.SpeechClips.AsNoTracking().Where(c => c.MeetingId == meetingId && c.GuestInvitationId == invitationId);
        if (since is { } after) clips = clips.Where(c => c.UpdatedAt > after);
        return await RowsAsync(clips, ct);
    }

    public async Task<ClipRow?> FindRowByClientIdAsync(Guid meetingId, Guid? speakerId, Guid? guestInvitationId, Guid clientClipId,
        CancellationToken ct)
    {
        var clips = db.SpeechClips.AsNoTracking().Where(c => c.MeetingId == meetingId && c.ClientClipId == clientClipId);
        clips = speakerId is { } user ? clips.Where(c => c.SpeakerId == user)
            : clips.Where(c => c.GuestInvitationId == guestInvitationId);
        return (await RowsAsync(clips, ct)).SingleOrDefault();
    }

    public Task<SpeechClip?> FindAsync(Guid clipId, CancellationToken ct)
        => db.SpeechClips.SingleOrDefaultAsync(c => c.Id == clipId, ct);

    public Task<SpeechClip?> FindGuestClipAsync(Guid clipId, Guid meetingId, Guid invitationId, CancellationToken ct)
        => db.SpeechClips.SingleOrDefaultAsync(c => c.Id == clipId && c.MeetingId == meetingId
            && c.GuestInvitationId == invitationId, ct);

    public Task<bool> HasGuestAudioAsync(Guid clipId, Guid meetingId, Guid invitationId, CancellationToken ct)
        => db.SpeechClips.AsNoTracking().AnyAsync(c => c.Id == clipId && c.MeetingId == meetingId
            && c.GuestInvitationId == invitationId && c.AudioPath != null && c.ExcludedAt == null, ct);

    public Task<ClipPlayback?> FindPlaybackAsync(Guid clipId, CancellationToken ct)
        => db.SpeechClips.AsNoTracking().Where(c => c.Id == clipId)
            .Select(c => new ClipPlayback(c.MeetingId, c.AudioPath, c.ExcludedAt)).SingleOrDefaultAsync(ct);

    public Task<SpeechClipAudio?> FindAudioAsync(Guid clipId, CancellationToken ct)
        => db.SpeechClips.AsNoTracking().Where(c => c.Id == clipId && c.AudioPath != null)
            .Select(c => new SpeechClipAudio(c.AudioPath!, c.AudioContentType ?? "audio/wav")).SingleOrDefaultAsync(ct);

    public Task<bool> IsGuestSessionAdmittedAsync(Guid sessionId, Guid invitationId, Guid meetingId, DateTimeOffset now,
        CancellationToken ct)
        => GuestSessionRepository.CurrentSessions(db, now)
            .AnyAsync(x => x.Id == sessionId && x.InvitationId == invitationId
                && x.Invitation.MeetingId == meetingId && x.Invitation.SelfDeclaredDisplayName != null, ct);

    public Task<bool> HasGuestConfirmedNoticeAsync(Guid sessionId, CancellationToken ct)
        => db.AuditLog.AsNoTracking().AnyAsync(a => a.ObjectType == AuditActions.GuestSessionObjectType
            && a.ObjectId == sessionId && a.Action == AuditActions.CallGuestAdmitted, ct);

    public async Task<IReadOnlyList<string>> ListAudioCreatedBeforeAsync(DateTimeOffset cutoff, CancellationToken ct)
        => await db.SpeechClips.Where(c => c.CreatedAt < cutoff).Where(c => c.AudioPath != null).Select(c => c.AudioPath!)
            .ToListAsync(ct);

    public Task<int> DeleteCreatedBeforeAsync(DateTimeOffset cutoff, CancellationToken ct)
        => db.SpeechClips.Where(c => c.CreatedAt < cutoff).ExecuteDeleteAsync(ct);

    public async Task<IReadOnlyList<CombinedClip>> ListMixableAsync(Guid meetingId, CancellationToken ct)
        => await db.SpeechClips.AsNoTracking()
            .Where(c => c.MeetingId == meetingId && c.AudioPath != null && c.ExcludedAt == null && c.DurationMs > 0)
            .OrderBy(c => c.StartedAt)
            .Select(c => new CombinedClip(c.StartedAt, c.DurationMs, c.AudioPath!))
            .ToListAsync(ct);

    public Task<DateTimeOffset?> FindLastChangeAsync(Guid meetingId, CancellationToken ct)
        => db.SpeechClips.Where(c => c.MeetingId == meetingId).MaxAsync(c => (DateTimeOffset?)c.UpdatedAt, ct);

    public void Add(SpeechClip clip) => db.SpeechClips.Add(clip);

    public void Remove(SpeechClip clip) => db.SpeechClips.Remove(clip);

    private static async Task<List<ClipRow>> RowsAsync(IQueryable<SpeechClip> clips, CancellationToken ct)
        => await clips.OrderBy(c => c.StartedAt).ThenBy(c => c.CreatedAt)
            .Select(c => new ClipRow(c.Id, c.SpeakerId ?? c.GuestInvitationId ?? Guid.Empty,
                c.Speaker != null ? c.Speaker.FullName
                    : c.GuestInvitation!.SelfDeclaredDisplayName ?? c.GuestInvitation.InvitedName,
                c.StartedAt, c.DurationMs, c.Text, c.UpdatedAt,
                c.EditedAt, c.EditedBy == null ? null : c.EditedBy.FullName, c.Segments, c.ExcludedAt, c.ExclusionReason,
                c.ExcludedBy == null ? null : c.ExcludedBy.FullName, c.AudioPath != null, c.Peaks, c.GuestInvitationId != null))
            .ToListAsync(ct);
}
