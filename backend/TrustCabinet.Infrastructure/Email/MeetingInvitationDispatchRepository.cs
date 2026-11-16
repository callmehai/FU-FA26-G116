using Microsoft.EntityFrameworkCore;
using TrustCabinet.Application.Email;
using TrustCabinet.Domain.Entities;
using TrustCabinet.Domain.Enums;
using TrustCabinet.Domain.Participants;

namespace TrustCabinet.Infrastructure.Email;

public sealed class MeetingInvitationDispatchRepository(TrustCabinetDbContext db) : IMeetingInvitationDispatchRepository
{
    public void Add(MeetingInvitationDispatch dispatch) => db.MeetingInvitationDispatches.Add(dispatch);

    public Task<MeetingGuestInvitation[]> ListActiveGuestInvitationsAsync(Guid meetingId, CancellationToken ct = default)
        => db.MeetingGuestInvitations.Where(g => g.MeetingId == meetingId && g.RevokedAt == null).ToArrayAsync(ct);

    public Task<Guid[]> ListDueIdsAsync(DateTimeOffset now, int take, CancellationToken ct = default)
        => db
            .MeetingInvitationDispatches.AsNoTracking()
            .Where(d =>
                (
                    d.State == InvitationDispatchState.Pending
                    || d.State == InvitationDispatchState.BlockedDependency
                ) && (d.NextAttemptAt == null || d.NextAttemptAt <= now)
            )
            .OrderBy(d => d.CreatedAt)
            .ThenBy(d => d.Id)
            .Select(d => d.Id)
            .Take(take)
            .ToArrayAsync(ct);

    public Task<MeetingInvitationDispatch?> FindAsync(Guid id, CancellationToken ct = default)
        => db
            .MeetingInvitationDispatches.AsNoTracking()
            .SingleOrDefaultAsync(d => d.Id == id, ct);

    /// <summary>
    /// <c>FOR UPDATE</c> on the <c>meetings</c> row first, then <c>FOR UPDATE SKIP LOCKED</c> on the
    /// <c>meeting_invitation_dispatches</c> row; the dispatch is then loaded and reloaded so it reflects the locked row.
    /// </summary>
    public async Task<MeetingInvitationDispatch?> LockAsync(Guid meetingId, Guid id, CancellationToken ct = default)
    {
        await db
            .Meetings.FromSqlInterpolated(
                $"SELECT * FROM meetings WHERE id = {meetingId} FOR UPDATE"
            )
            .AsNoTracking()
            .ToArrayAsync(ct);
        var locked = await db
            .MeetingInvitationDispatches.FromSqlInterpolated(
                $"SELECT * FROM meeting_invitation_dispatches WHERE id = {id} FOR UPDATE SKIP LOCKED"
            )
            .AsNoTracking()
            .ToArrayAsync(ct);
        if (locked.Length == 0)
            return null;
        var dispatch = await db.MeetingInvitationDispatches.SingleAsync(d => d.Id == id, ct);
        await db.Entry(dispatch).ReloadAsync(ct);
        return dispatch;
    }

    public Task<bool> IsGuestInvitationActiveAsync(Guid? guestInvitationId, CancellationToken ct = default)
        => db.MeetingGuestInvitations.AnyAsync(
            g => g.Id == guestInvitationId && g.RevokedAt == null,
            ct
        );

    public Task<bool> IsParticipantActiveAsync(Guid participantId, CancellationToken ct = default)
        => db
            .MeetingParticipants.Where(ParticipantRules.ActiveExpression)
            .AnyAsync(p => p.Id == participantId, ct);

    public Task<bool> IsMeetingClosedAsync(Guid? meetingId, CancellationToken ct = default)
        => db.Meetings.AnyAsync(
            m =>
                m.Id == meetingId
                && (m.Status == MeetingStatus.Ended || m.Status == MeetingStatus.Cancelled),
            ct
        );

    public Task<MeetingStatus> GetMeetingStatusAsync(Guid? meetingId, CancellationToken ct = default)
        => db
            .Meetings.Where(m => m.Id == meetingId)
            .Select(m => m.Status)
            .SingleAsync(ct);

    public Task<string?> GetParticipantInitialKeyAsync(Guid participantId, CancellationToken ct = default)
        => db
            .MeetingParticipants.Where(p => p.Id == participantId)
            .Select(p => p.InitialInvitationDeliveryKey)
            .SingleAsync(ct);

    public async Task<string?> GetGuestInitialKeyAsync(Guid? guestInvitationId, CancellationToken ct = default)
        => await db
            .MeetingGuestInvitations.Where(g => g.Id == guestInvitationId)
            .Select(g => g.InitialInvitationDeliveryKey)
            .SingleAsync(ct);

    public Task<bool> HasMaterializedInvitationAsync(Guid? meetingId, Guid? participantId, Guid? guestInvitationId,
        CancellationToken ct = default)
        => db.MeetingInvitationDispatches.AnyAsync(
            d =>
                d.MeetingId == meetingId
                && d.ParticipantId == participantId
                && d.GuestInvitationId == guestInvitationId
                && d.EventKind == InvitationEventKind.Invitation
                && d.State == InvitationDispatchState.Materialized,
            ct
        );

    public Task<int> SuppressAsync(Guid id, DateTimeOffset at, CancellationToken ct = default)
        => db
            .MeetingInvitationDispatches.Where(d =>
                d.Id == id
                && (
                    d.State == InvitationDispatchState.Pending
                    || d.State == InvitationDispatchState.BlockedDependency
                )
            )
            .ExecuteUpdateAsync(
                set =>
                    set.SetProperty(d => d.State, InvitationDispatchState.Suppressed)
                        .SetProperty(d => d.UpdatedAt, at),
                ct
            );

    public Task<int> BlockOnDependencyAsync(Guid id, DateTimeOffset nextAttemptAt, DateTimeOffset at,
        CancellationToken ct = default)
        => db
            .MeetingInvitationDispatches.Where(d =>
                d.Id == id
                && (
                    d.State == InvitationDispatchState.Pending
                    || d.State == InvitationDispatchState.BlockedDependency
                )
                && (d.NextAttemptAt == null || d.NextAttemptAt <= at)
            )
            .ExecuteUpdateAsync(
                set =>
                    set.SetProperty(d => d.State, InvitationDispatchState.BlockedDependency)
                        .SetProperty(
                            d => d.DependencyAttemptCount,
                            d => d.DependencyAttemptCount + 1
                        )
                        .SetProperty(d => d.NextAttemptAt, nextAttemptAt)
                        .SetProperty(d => d.LastError, "Unavailable")
                        .SetProperty(d => d.UpdatedAt, at),
                ct
            );

    public Task<int> RecordFailedAttemptAsync(Guid id, int maxAttempts, DateTimeOffset retryAt, string error,
        DateTimeOffset at, CancellationToken ct = default)
        => db
            .MeetingInvitationDispatches.Where(d =>
                d.Id == id
                && (
                    d.State == InvitationDispatchState.Pending
                    || d.State == InvitationDispatchState.BlockedDependency
                )
            )
            .ExecuteUpdateAsync(
                set =>
                    set.SetProperty(d => d.AttemptCount, d => d.AttemptCount + 1)
                        .SetProperty(
                            d => d.State,
                            d =>
                                d.AttemptCount + 1 >= maxAttempts
                                    ? InvitationDispatchState.Failed
                                    : InvitationDispatchState.Pending
                        )
                        .SetProperty(d => d.NextAttemptAt, retryAt)
                        .SetProperty(d => d.LastError, error)
                        .SetProperty(d => d.UpdatedAt, at),
                ct
            );
}
