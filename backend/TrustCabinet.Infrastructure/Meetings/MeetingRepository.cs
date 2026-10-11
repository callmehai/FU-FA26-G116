using System.Data;
using Microsoft.EntityFrameworkCore;
using TrustCabinet.Domain.Entities;
using TrustCabinet.Domain.Enums;
using TrustCabinet.Domain.Meetings;

namespace TrustCabinet.Infrastructure.Meetings;

/// <summary>Queries meetings with a shared row-visibility predicate and persists changes.</summary>
public sealed class MeetingRepository(TrustCabinetDbContext db) : IMeetingRepository
{
    public Task<Meeting?> FindAccessibleAsync(
        Guid id, Guid actorId, CancellationToken ct = default)
        => AccessibleTo(actorId).SingleOrDefaultAsync(m => m.Id == id, ct);

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

        var items = await ordered
            .ThenBy(m => m.Id)
            .Skip(checked((int)offset))
            .Take(query.PageSize)
            .Select(m => new MeetingListItem(
                m.Id, m.Title, m.PlannedStartTime, m.PlannedEndTime, m.Mode, m.Location,
                m.CreatedAt, m.Status, m.Sensitivity))
            .ToListAsync(ct);

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
        }

        IQueryable<Meeting> query = db.Meetings
            .AsSplitQuery()
            .Include(meeting => meeting.Participants)
            .ThenInclude(participant => participant.User);

        if (!forUpdate)
        {
            query = query.AsNoTracking();
        }

        return await query.SingleOrDefaultAsync(meeting => meeting.Id == id, ct);
    }

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
                item.Actor.FullName,
                item.OccurredAt,
                item.Action == MeetingHistoryAction.Reject ? item.Reason : null,
                item.AssignedApproverId,
                item.PreviousOrganizerId,
                item.NewOrganizerId))
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

    public Task AddAsync(Meeting meeting, CancellationToken ct = default)
    {
        ct.ThrowIfCancellationRequested();
        db.Meetings.Add(meeting);
        return Task.CompletedTask;
    }

    public Task SaveChangesAsync(CancellationToken ct = default) => db.SaveChangesAsync(ct);

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
