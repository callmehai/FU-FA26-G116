using Microsoft.EntityFrameworkCore;
using TrustCabinet.Application.Participants;
using TrustCabinet.Application.Speaking;
using TrustCabinet.Domain.Entities;
using TrustCabinet.Domain.Enums;

namespace TrustCabinet.Infrastructure.Speaking;

/// <summary>The speaking queue's rows (SCRUM-27, UC-79), and closing the open ones (BR-337).</summary>
public sealed class SpeakingRequestRepository(TrustCabinetDbContext db) : ISpeakingRequestRepository
{
    public Task<bool> IsActiveUserAsync(Guid userId, CancellationToken ct)
        => db.Users.AnyAsync(u => u.Id == userId && u.IsActive, ct);

    public Task<SpeakingRequest?> FindAsync(Guid meetingId, Guid requestId, CancellationToken ct)
        => db.SpeakingRequests.SingleOrDefaultAsync(r => r.Id == requestId && r.MeetingId == meetingId, ct);

    public Task<SpeakingRequest?> FindOpenAsync(Guid meetingId, SpeakingViewer viewer, CancellationToken ct)
        => db.SpeakingRequests.Where(r => r.MeetingId == meetingId
                && SpeakingRules.OpenStatuses.Contains(r.Status)
                && (viewer.UserId != null ? r.UserId == viewer.UserId : r.GuestInvitationId == viewer.GuestInvitationId))
            .SingleOrDefaultAsync(ct);

    public async Task<int> LastQueuePositionAsync(Guid meetingId, CancellationToken ct)
        => await db.SpeakingRequests.Where(r => r.MeetingId == meetingId)
            .MaxAsync(r => (int?)r.QueuePosition, ct) ?? 0;

    public Task<Guid?> FindCurrentAgendaItemIdAsync(Guid meetingId, CancellationToken ct)
        => db.AgendaItems
            .Where(i => i.MeetingId == meetingId && i.Status == AgendaItemStatus.InProgress)
            .Select(i => (Guid?)i.Id).SingleOrDefaultAsync(ct);

    public Task<bool> IsFloorHeldAsync(Guid meetingId, CancellationToken ct)
        => db.SpeakingRequests.AnyAsync(r => r.MeetingId == meetingId
            && (r.Status == SpeakingRequestStatus.Invited || r.Status == SpeakingRequestStatus.Speaking), ct);

    public Task<SpeakingRequest?> FindQueuedNeighbourAsync(Guid meetingId, int queuePosition, bool up, CancellationToken ct)
        => (up
                ? db.SpeakingRequests.Where(r => r.MeetingId == meetingId && r.Status == SpeakingRequestStatus.Queued
                    && r.QueuePosition < queuePosition).OrderByDescending(r => r.QueuePosition)
                : db.SpeakingRequests.Where(r => r.MeetingId == meetingId && r.Status == SpeakingRequestStatus.Queued
                    && r.QueuePosition > queuePosition).OrderBy(r => r.QueuePosition))
            .FirstOrDefaultAsync(ct);

    public Task<bool> IsInRoomAsync(Guid meetingId, ParticipantActor actor, CancellationToken ct)
    {
        var open = db.CallParticipants.Where(p => p.LeftAt == null && p.CallSession.MeetingId == meetingId
            && p.CallSession.Kind == CallKind.MeetingRoom && p.CallSession.Status == CallStatus.Active);
        return actor.Guest is { } guest
            ? open.AnyAsync(p => p.GuestSessionId == guest.SessionId, ct)
            : open.AnyAsync(p => p.UserId == actor.UserId, ct);
    }

    public async Task<IReadOnlyList<SpeakingRequestRow>> ListOpenAsync(Guid meetingId, CancellationToken ct)
    {
        var rows = await db.SpeakingRequests.AsNoTracking()
            .Where(r => r.MeetingId == meetingId && SpeakingRules.OpenStatuses.Contains(r.Status))
            .Select(r => new
            {
                r.Id, r.UserId, r.GuestInvitationId, r.Status, r.QueuePosition, r.RequestedAt, r.InvitedAt, r.StartedAt,
                r.AgendaItemId, AgendaItemTitle = r.AgendaItem == null ? null : r.AgendaItem.Title,
                UserName = r.User == null ? null : r.User.FullName,
                GuestName = r.GuestInvitation == null ? null
                    : r.GuestInvitation.SelfDeclaredDisplayName ?? r.GuestInvitation.InvitedName,
            })
            .ToListAsync(ct);
        return rows.Select(r => new SpeakingRequestRow(r.Id, r.UserId, r.GuestInvitationId, r.Status, r.QueuePosition,
            r.RequestedAt, r.InvitedAt, r.StartedAt, r.AgendaItemId, r.AgendaItemTitle, r.UserName, r.GuestName)).ToArray();
    }

    /// <summary>
    /// Closes the open requests of people who are no longer there: the meeting ended, they left the room, or they
    /// were removed from the meeting (BR-337). Runs inside the caller's transaction; the caller saves.
    /// </summary>
    public async Task<int> CloseOpenAsync(Guid meetingId, Guid? userId, IReadOnlyCollection<Guid>? guestInvitationIds,
        DateTimeOffset endedAt, DateTimeOffset now, CancellationToken ct)
    {
        var query = db.SpeakingRequests.Where(r => r.MeetingId == meetingId && SpeakingRules.OpenStatuses.Contains(r.Status));
        if (userId is not null || guestInvitationIds is not null)
            query = query.Where(r => (userId != null && r.UserId == userId)
                || (guestInvitationIds != null && r.GuestInvitationId != null && guestInvitationIds.Contains(r.GuestInvitationId.Value)));
        var open = await query.ToListAsync(ct);
        return open.Count(r => SpeakingRules.Close(r, endedAt, now));
    }

    public async Task CloseAllOpenAsync(Guid meetingId, DateTimeOffset endedAt, CancellationToken ct)
    {
        if (db.Database.CurrentTransaction is null)
            throw new InvalidOperationException("Closing speaking requests requires the caller's meeting transaction and lock.");
        await CloseOpenAsync(meetingId, null, null, endedAt, endedAt, ct);
    }

    public void Add(SpeakingRequest request) => db.SpeakingRequests.Add(request);
}
