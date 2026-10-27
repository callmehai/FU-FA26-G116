using System.Data;
using Microsoft.EntityFrameworkCore;
using TrustCabinet.Domain.Entities;
using TrustCabinet.Domain.Enums;
using TrustCabinet.Domain.Meetings;
using TrustCabinet.Domain.Participants;

namespace TrustCabinet.Infrastructure.Meetings;

/// <summary>Queries meetings with a shared row-visibility predicate and persists changes.</summary>
public sealed class MeetingRepository(TrustCabinetDbContext db) : IMeetingRepository
{
    public Task<Meeting?> FindAccessibleAsync(
        Guid id, Guid actorId, CancellationToken ct = default)
        => AccessibleTo(actorId).Include(m => m.CancelledBy).Include(m => m.Participants).ThenInclude(p => p.User)
            .SingleOrDefaultAsync(m => m.Id == id, ct);

    public async Task<MeetingListPage> FindAuthorizedPageAsync(
        Guid actorId, MeetingListQuery query, CancellationToken ct = default)
    {
        var meetings = AccessibleTo(actorId);
        if (query.Status is { } status)
        {
            meetings = meetings.Where(m => m.Status == status);
        }

        if (query.Sensitivity is { } sensitivity)
        {
            meetings = meetings.Where(m => m.Sensitivity == sensitivity);
        }

        // BR-30: a meeting matches when its own planned interval overlaps the range.
        if (query.From is { } from)
        {
            meetings = meetings.Where(m => m.PlannedEndTime >= from);
        }

        if (query.To is { } to)
        {
            meetings = meetings.Where(m => m.PlannedStartTime <= to);
        }

        if (query.Timing is { } timing)
        {
            var now = query.Now ?? DateTimeOffset.UtcNow;
            meetings = timing == MeetingTiming.Upcoming
                ? meetings.Where(m => m.PlannedStartTime > now)
                : meetings.Where(m => m.PlannedStartTime <= now);
        }

        if (query.ExcludeDeclined)
        {
            // The Chair is never asked, so an answer left from before they took the chair does not hide the meeting.
            meetings = meetings.Where(m => !m.Participants.AsQueryable().Where(ParticipantRules.CurrentExpression)
                .Any(p => p.UserId == actorId && p.Role != MeetingRole.Chair && p.Response == InvitationResponse.Declined));
        }

        if (query.Query is { Length: > 0 } search)
        {
            var pattern = $"%{search.Replace("\\", "\\\\", StringComparison.Ordinal)
                .Replace("%", "\\%", StringComparison.Ordinal)
                .Replace("_", "\\_", StringComparison.Ordinal)}%";
            meetings = meetings.Where(m => EF.Functions.ILike(m.Title, pattern, "\\"));
        }

        var totalCount = await meetings.LongCountAsync(ct);
        var offset = ((long)query.Page - 1) * query.PageSize;
        if (offset >= totalCount)
        {
            return new MeetingListPage([], query.Page, query.PageSize, totalCount);
        }

        var ordered = (query.SortBy, query.SortDirection) switch
        {
            (MeetingSortBy.Title, MeetingSortDirection.Asc) => meetings.OrderBy(m => m.Title),
            (MeetingSortBy.Title, MeetingSortDirection.Desc) => meetings.OrderByDescending(m => m.Title),
            (MeetingSortBy.CreatedAt, MeetingSortDirection.Asc) => meetings.OrderBy(m => m.CreatedAt),
            (MeetingSortBy.CreatedAt, MeetingSortDirection.Desc) => meetings.OrderByDescending(m => m.CreatedAt),
            (MeetingSortBy.PlannedStartTime, MeetingSortDirection.Asc) => meetings.OrderBy(m => m.PlannedStartTime),
            _ => meetings.OrderByDescending(m => m.PlannedStartTime),
        };

        var rows = await ordered
            .ThenBy(m => m.Id)
            .Skip(checked((int)offset))
            .Take(query.PageSize)
            .Select(m => new
            {
                Item = new MeetingListItem(
                    m.Id, m.Title, m.PlannedStartTime, m.PlannedEndTime, m.Mode, m.Location,
                    m.CreatedAt, m.Status, m.Sensitivity, null, false),
                Mine = m.Participants.AsQueryable().Where(ParticipantRules.ActiveExpression)
                    .Where(p => p.UserId == actorId)
                    .Select(p => new { p.Role, p.Response })
                    .FirstOrDefault(),
            })
            .ToListAsync(ct);

        // The caller's own invitation, so the calendar can ask "Xác nhận tham dự?" without a request per meeting.
        var items = rows.Select(row => row.Mine is null ? row.Item : row.Item with
        {
            MyResponse = MeetingDetails.MyResponseOf(row.Mine.Role, row.Mine.Response),
            CanRespond = ParticipantRules.CanRespond(row.Item.Status, row.Mine.Role, row.Mine.Response),
        }).ToList();

        return new MeetingListPage(items, query.Page, query.PageSize, totalCount);
    }

    private IQueryable<Meeting> AccessibleTo(Guid actorId)
        => db.Meetings.AsNoTracking().Where(MeetingReadPolicy.ForUser(actorId));

    public Task<Meeting?> FindOwnedAsync(
        Guid id, Guid creatorId, CancellationToken ct = default)
        => db.Meetings
            .AsNoTracking()
            .SingleOrDefaultAsync(m => m.Id == id && m.CreatedById == creatorId, ct);

    public async Task<Meeting?> FindOwnedForUpdateAsync(
        Guid id, Guid creatorId, CancellationToken ct = default)
    {
        // Issue the lock as a parameterized PostgreSQL query. Materialize it without composing
        // another query over SELECT ... FOR UPDATE, then load the tracked row under the same lock.
        var lockedRows = await db.Meetings
            .FromSqlInterpolated($"SELECT * FROM meetings WHERE id = {id} AND created_by_id = {creatorId} FOR UPDATE")
            .AsNoTracking()
            .ToListAsync(ct);

        if (lockedRows.Count == 0)
        {
            return null;
        }

        // Participants are the invitees a reschedule notifies (BR-76).
        return await db.Meetings
            .AsSplitQuery()
            .Include(m => m.Participants)
            .ThenInclude(participant => participant.User)
            .SingleOrDefaultAsync(m => m.Id == id && m.CreatedById == creatorId, ct);
    }

    public async Task<IReadOnlyList<MeetingApproverOption>> ListEligibleApproversAsync(
        CancellationToken ct = default)
        => await db.Users
            .AsNoTracking()
            .Where(user => user.IsActive
                && db.RoleCapabilities.Any(capability =>
                    capability.RoleId == user.RoleId
                    && capability.Capability == Capability.MeetingApprover))
            .OrderBy(user => user.FullName)
            .Select(user => new MeetingApproverOption(user.Id, user.FullName))
            .ToListAsync(ct);

    public Task<bool> IsEligibleApproverAsync(Guid id, CancellationToken ct = default)
        => db.Users.AnyAsync(user => user.Id == id
            && user.IsActive
            && db.RoleCapabilities.Any(capability =>
                capability.RoleId == user.RoleId
                && capability.Capability == Capability.MeetingApprover), ct);

    public async Task<Meeting?> FindWorkflowAsync(
        Guid id, bool forUpdate = false, CancellationToken ct = default)
    {
        if (forUpdate)
        {
            var lockedRows = await db.Meetings
                .FromSqlInterpolated($"SELECT * FROM meetings WHERE id = {id} FOR UPDATE")
                .AsNoTracking()
                .ToListAsync(ct);
            if (lockedRows.Count == 0)
            {
                return null;
            }
            // Publication readiness must not race account deactivation after the meeting lock.
            var lockedUsers = await db.Users.FromSqlInterpolated($"SELECT * FROM users WHERE id IN (SELECT user_id FROM meeting_participants WHERE meeting_id = {id}) ORDER BY id FOR SHARE")
                .AsNoTracking().ToArrayAsync(ct);
            // The active-user HTTP gate may already track the actor before this lock was acquired.
            // EF identity resolution would otherwise reuse that stale account in the roster query.
            foreach (var user in lockedUsers)
            {
                var entry = db.ChangeTracker.Entries<User>()
                    .SingleOrDefault(e => e.Entity.Id == user.Id && e.State == EntityState.Unchanged);
                if (entry is null) continue;
                entry.CurrentValues.SetValues(user);
                entry.OriginalValues.SetValues(user);
            }
        }

        IQueryable<Meeting> query = db.Meetings
            .AsSplitQuery()
            .Include(meeting => meeting.CancelledBy)
            .Include(meeting => meeting.Participants)
            .ThenInclude(participant => participant.User);

        if (!forUpdate)
        {
            query = query.AsNoTracking();
        }

        return await query.SingleOrDefaultAsync(meeting => meeting.Id == id, ct);
    }

    public async Task<IReadOnlyList<Guid>> ListOverdueAsync(DateTimeOffset cutoff, int take, CancellationToken ct = default)
        => await db.Meetings.AsNoTracking()
            .Where(m => (m.Status == MeetingStatus.Draft || m.Status == MeetingStatus.WaitingApproval
                    || m.Status == MeetingStatus.Rejected || m.Status == MeetingStatus.Approved
                    || m.Status == MeetingStatus.Published)
                && m.PlannedStartTime <= cutoff)
            .OrderBy(m => m.PlannedStartTime)
            .ThenBy(m => m.Id)
            .Select(m => m.Id)
            .Take(take)
            .ToListAsync(ct);

    public async Task<MeetingHistoryPage?> FindHistoryPageAsync(
        Guid meetingId, int page, int pageSize, CancellationToken ct = default)
    {
        var history = db.MeetingStatusHistory.AsNoTracking()
            .Where(item => item.MeetingId == meetingId);
        var total = await history.LongCountAsync(ct);
        var offset = ((long)page - 1) * pageSize;
        if (offset >= total)
        {
            return new MeetingHistoryPage([], page, pageSize, total);
        }

        if (offset > int.MaxValue)
        {
            return null;
        }

        var items = await history
            .OrderBy(item => item.OccurredAt)
            .ThenBy(item => item.Id)
            .Skip(checked((int)offset))
            .Take(pageSize)
            .Select(item => new MeetingHistoryRecord(
                item.Id,
                item.MeetingId,
                item.Action,
                item.FromStatus,
                item.ToStatus,
                item.ActorId,
                item.Actor != null ? item.Actor.FullName : MeetingHistoryRecord.SystemActorName,
                item.OccurredAt,
                item.Action == MeetingHistoryAction.Reject ? item.Reason : null,
                item.AssignedApproverId,
                item.PreviousOrganizerId,
                item.NewOrganizerId,
                item.Action == MeetingHistoryAction.Cancel || item.Action == MeetingHistoryAction.AutoCancel
                    ? item.Reason : null))
            .ToListAsync(ct);

        return new MeetingHistoryPage(items, page, pageSize, total);
    }

    public Task<bool> IsActiveUserWithCapabilityAsync(
        Guid userId, Capability capability, CancellationToken ct = default)
        => db.Users.AnyAsync(user => user.Id == userId && user.IsActive
            && db.RoleCapabilities.Any(roleCapability =>
                roleCapability.RoleId == user.RoleId
                && roleCapability.Capability == capability), ct);

    public Task<bool> IsUserActiveAsync(Guid userId, CancellationToken ct = default)
        => db.Users.AnyAsync(user => user.Id == userId && user.IsActive, ct);

    public Task<string?> FindUserDisplayNameAsync(Guid userId, CancellationToken ct = default)
        => db.Users.AsNoTracking()
            .Where(user => user.Id == userId)
            .Select(user => user.FullName)
            .SingleOrDefaultAsync(ct);

    public Task AddNotificationsAsync(
        IReadOnlyCollection<Notification> notifications, CancellationToken ct = default)
    {
        ct.ThrowIfCancellationRequested();
        db.Notifications.AddRange(notifications);
        return Task.CompletedTask;
    }

    public Task AddHistoryAsync(MeetingStatusHistory history, CancellationToken ct = default)
    {
        ct.ThrowIfCancellationRequested();
        db.MeetingStatusHistory.Add(history);
        return Task.CompletedTask;
    }

    public async Task<IReadOnlyList<User>> LockParticipantUsersAsync(Guid[] userIds, CancellationToken ct = default)
        => await db.Users.FromSqlInterpolated($"SELECT * FROM users WHERE id = ANY({userIds.Order().ToArray()}) ORDER BY id FOR SHARE")
            .ToArrayAsync(ct);

    public Task AddAsync(Meeting meeting, CancellationToken ct = default)
    {
        ct.ThrowIfCancellationRequested();
        db.Meetings.Add(meeting);
        return Task.CompletedTask;
    }

    public Task SaveChangesAsync(CancellationToken ct = default) => db.SaveChangesAsync(ct);

    public async Task StagePublicationInvitationsAsync(Meeting meeting, DateTimeOffset now, CancellationToken ct = default)
    {
        foreach (var participant in MeetingInvitationRecipientPolicy.SelectSnapshot(meeting.Participants))
            Email.MeetingInvitationEvents.Stage(db, meeting, participant, null, InvitationEventKind.Invitation, now, notify: false);
        foreach (var guest in await db.MeetingGuestInvitations.Where(g => g.MeetingId == meeting.Id && g.RevokedAt == null).ToArrayAsync(ct))
            Email.MeetingInvitationEvents.Stage(db, meeting, null, guest, InvitationEventKind.Invitation, now, notify: false);
    }

    public async Task StageCancellationAsync(
        Meeting meeting, Guid historyId, Guid actorId, string reason, DateTimeOffset now, CancellationToken ct = default)
    {
        // The organizer who cancelled needs no bell for it, but still gets the email like every invitee.
        foreach (var participant in MeetingInvitationRecipientPolicy.SelectSnapshot(meeting.Participants))
            Email.MeetingInvitationEvents.Stage(db, meeting, participant, null, InvitationEventKind.Cancelled, now,
                notify: participant.UserId != actorId, historyId, reason);
        foreach (var guest in await db.MeetingGuestInvitations.Where(g => g.MeetingId == meeting.Id && g.RevokedAt == null).ToArrayAsync(ct))
            Email.MeetingInvitationEvents.Stage(db, meeting, null, guest, InvitationEventKind.Cancelled, now,
                notify: false, historyId, reason);
    }

    public async Task StageGuestRescheduleInvitationsAsync(Meeting meeting, Guid historyId, DateTimeOffset now, CancellationToken ct = default)
    {
        foreach (var guest in await db.MeetingGuestInvitations.Where(g => g.MeetingId == meeting.Id && g.RevokedAt == null).ToArrayAsync(ct))
            Email.MeetingInvitationEvents.Stage(db, meeting, null, guest, InvitationEventKind.Rescheduled, now, notify: false, historyId);
    }

    public async Task<T> InTransactionAsync<T>(
        Func<CancellationToken, Task<T>> operation, CancellationToken ct = default)
    {
        await using var transaction = await db.Database.BeginTransactionAsync(
            IsolationLevel.ReadCommitted, ct);
        var result = await operation(ct);
        await transaction.CommitAsync(ct);
        return result;
    }
}
