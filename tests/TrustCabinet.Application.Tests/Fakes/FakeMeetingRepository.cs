using TrustCabinet.Application.Meetings;
using TrustCabinet.Domain.Entities;
using TrustCabinet.Domain.Enums;
using TrustCabinet.Domain.Meetings;

namespace TrustCabinet.Application.Tests.Fakes;

/// <summary>
/// Meetings held in <see cref="Meetings"/>; who may open one by <see cref="FindAccessibleAsync"/> from
/// <see cref="Accessible"/>. What the services stage is recorded; a service moved to Application saves and opens
/// transactions through <see cref="FakeUnitOfWork"/>. Every other member throws until a test needs it: fill it in
/// then, rather than returning a guess.
/// </summary>
public sealed class FakeMeetingRepository : IMeetingRepository
{
    /// <summary>(meeting, user) pairs the read policy lets through.</summary>
    public HashSet<(Guid MeetingId, Guid UserId)> Accessible { get; } = [];

    public List<Meeting> Meetings { get; } = [];

    /// <summary>(user, capability) pairs held by an active user.</summary>
    public HashSet<(Guid UserId, Capability Capability)> Capabilities { get; } = [];

    /// <summary>Meetings read under their row lock, in order.</summary>
    public List<Guid> Locked { get; } = [];

    public List<MeetingStatusHistory> History { get; } = [];

    public List<Notification> Notifications { get; } = [];

    public List<Meeting> Added { get; } = [];

    /// <summary>Users read under a shared lock, in order.</summary>
    public List<Guid> LockedUsers { get; } = [];

    public Task<Meeting?> FindAccessibleAsync(Guid id, Guid actorId, CancellationToken ct = default)
        => Task.FromResult(Accessible.Contains((id, actorId)) ? new Meeting { Id = id, Title = "Họp" } : null);

    public Task<Meeting?> FindWorkflowAsync(Guid id, bool forUpdate = false, CancellationToken ct = default)
    {
        if (forUpdate) Locked.Add(id);
        return Task.FromResult(Meetings.SingleOrDefault(m => m.Id == id));
    }

    public Task<Meeting?> LockOwnedAsync(Guid id, Guid creatorId, CancellationToken ct = default)
    {
        Locked.Add(id);
        return Task.FromResult(Meetings.SingleOrDefault(m => m.Id == id && m.CreatedById == creatorId));
    }

    /// <summary>
    /// What the overdue and overrun queries answer, in place of the ids worked out from <see cref="Meetings"/>: a
    /// meeting that changed between the query and its row lock.
    /// </summary>
    public IReadOnlyList<Guid>? Listed { get; set; }

    public Task<IReadOnlyList<Guid>> ListOverdueAsync(DateTimeOffset cutoff, int take, CancellationToken ct = default)
        => Task.FromResult<IReadOnlyList<Guid>>(Listed ?? Meetings
            .Where(m => MeetingRules.IsCancellable(m.Status) && m.PlannedStartTime <= cutoff)
            .OrderBy(m => m.PlannedStartTime).Select(m => m.Id).Take(take).ToArray());

    public Task<IReadOnlyList<Guid>> ListOverrunAsync(DateTimeOffset cutoff, int take, CancellationToken ct = default)
        => Task.FromResult<IReadOnlyList<Guid>>(Listed ?? Meetings
            .Where(m => MeetingRules.IsOverrun(m, cutoff))
            .OrderBy(m => m.PlannedEndTime).Select(m => m.Id).Take(take).ToArray());

    public Task<bool> IsActiveUserWithCapabilityAsync(Guid userId, Capability capability, CancellationToken ct = default)
        => Task.FromResult(Capabilities.Contains((userId, capability)));

    /// <summary>Users whose account is active.</summary>
    public HashSet<Guid> ActiveUsers { get; } = [];

    public Task<bool> IsUserActiveAsync(Guid userId, CancellationToken ct = default)
        => Task.FromResult(ActiveUsers.Contains(userId));

    public Task<string?> FindUserDisplayNameAsync(Guid userId, CancellationToken ct = default)
        => Task.FromResult<string?>("Người thực hiện");

    public Task AddNotificationsAsync(IReadOnlyCollection<Notification> notifications, CancellationToken ct = default)
    {
        Notifications.AddRange(notifications);
        return Task.CompletedTask;
    }

    public Task AddHistoryAsync(MeetingStatusHistory history, CancellationToken ct = default)
    {
        History.Add(history);
        return Task.CompletedTask;
    }

    public Task<IReadOnlyList<User>> LockParticipantUsersAsync(Guid[] userIds, CancellationToken ct = default)
    {
        LockedUsers.AddRange(userIds);
        return Task.FromResult<IReadOnlyList<User>>(userIds
            .Select(id => new User { Id = id, FullName = "Thành viên", Email = $"{id}@trustcabinet.local", IsActive = true })
            .ToArray());
    }

    public Task AddAsync(Meeting meeting, CancellationToken ct = default)
    {
        Added.Add(meeting);
        return Task.CompletedTask;
    }

    public Task<MeetingHistoryPage?> FindHistoryPageAsync(Guid meetingId, int page, int pageSize, CancellationToken ct = default)
        => Task.FromResult<MeetingHistoryPage?>(new MeetingHistoryPage([], page, pageSize, 0));

    public Task<MeetingListPage> FindAuthorizedPageAsync(Guid actorId, MeetingListQuery query, CancellationToken ct = default) => throw new NotSupportedException();
    public Task<Meeting?> FindOwnedAsync(Guid id, Guid creatorId, CancellationToken ct = default) => throw new NotSupportedException();
    public Task<IReadOnlyList<MeetingApproverOption>> ListEligibleApproversAsync(CancellationToken ct = default) => throw new NotSupportedException();
    public Task<bool> IsEligibleApproverAsync(Guid id, CancellationToken ct = default) => throw new NotSupportedException();
    public Task StageCancellationAsync(Meeting meeting, Guid historyId, Guid actorId, string reason, DateTimeOffset now, CancellationToken ct = default) => throw new NotSupportedException();
}
