using TrustCabinet.Application.Meetings;
using TrustCabinet.Domain.Entities;
using TrustCabinet.Domain.Enums;

namespace TrustCabinet.Application.Tests.Fakes;

/// <summary>
/// Answers who may open a meeting from <see cref="Accessible"/>. Every other member throws until a test of the
/// Meetings module needs it: fill it in then, rather than returning a guess.
/// </summary>
public sealed class FakeMeetingRepository : IMeetingRepository
{
    /// <summary>(meeting, user) pairs the read policy lets through.</summary>
    public HashSet<(Guid MeetingId, Guid UserId)> Accessible { get; } = [];

    public Task<Meeting?> FindAccessibleAsync(Guid id, Guid actorId, CancellationToken ct = default)
        => Task.FromResult(Accessible.Contains((id, actorId)) ? new Meeting { Id = id, Title = "Họp" } : null);

    public Task<MeetingListPage> FindAuthorizedPageAsync(Guid actorId, MeetingListQuery query, CancellationToken ct = default) => throw new NotSupportedException();
    public Task<Meeting?> FindOwnedAsync(Guid id, Guid creatorId, CancellationToken ct = default) => throw new NotSupportedException();
    public Task<Meeting?> FindOwnedForUpdateAsync(Guid id, Guid creatorId, CancellationToken ct = default) => throw new NotSupportedException();
    public Task<IReadOnlyList<MeetingApproverOption>> ListEligibleApproversAsync(CancellationToken ct = default) => throw new NotSupportedException();
    public Task<bool> IsEligibleApproverAsync(Guid id, CancellationToken ct = default) => throw new NotSupportedException();
    public Task<Meeting?> FindWorkflowAsync(Guid id, bool forUpdate = false, CancellationToken ct = default) => throw new NotSupportedException();
    public Task<IReadOnlyList<Guid>> ListOverdueAsync(DateTimeOffset cutoff, int take, CancellationToken ct = default) => throw new NotSupportedException();
    public Task<IReadOnlyList<Guid>> ListOverrunAsync(DateTimeOffset cutoff, int take, CancellationToken ct = default) => throw new NotSupportedException();
    public Task<MeetingHistoryPage?> FindHistoryPageAsync(Guid meetingId, int page, int pageSize, CancellationToken ct = default) => throw new NotSupportedException();
    public Task<bool> IsActiveUserWithCapabilityAsync(Guid userId, Capability capability, CancellationToken ct = default) => throw new NotSupportedException();
    public Task<bool> IsUserActiveAsync(Guid userId, CancellationToken ct = default) => throw new NotSupportedException();
    public Task<string?> FindUserDisplayNameAsync(Guid userId, CancellationToken ct = default) => throw new NotSupportedException();
    public Task AddNotificationsAsync(IReadOnlyCollection<Notification> notifications, CancellationToken ct = default) => throw new NotSupportedException();
    public Task AddHistoryAsync(MeetingStatusHistory history, CancellationToken ct = default) => throw new NotSupportedException();
    public Task<IReadOnlyList<User>> LockParticipantUsersAsync(Guid[] userIds, CancellationToken ct = default) => throw new NotSupportedException();
    public Task AddAsync(Meeting meeting, CancellationToken ct = default) => throw new NotSupportedException();
    public Task SaveChangesAsync(CancellationToken ct = default) => throw new NotSupportedException();
    public Task StageCancellationAsync(Meeting meeting, Guid historyId, Guid actorId, string reason, DateTimeOffset now, CancellationToken ct = default) => throw new NotSupportedException();
    public Task<T> InTransactionAsync<T>(Func<CancellationToken, Task<T>> operation, CancellationToken ct = default) => throw new NotSupportedException();
}
