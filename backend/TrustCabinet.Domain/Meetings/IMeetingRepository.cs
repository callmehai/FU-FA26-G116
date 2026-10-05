using TrustCabinet.Domain.Entities;
using TrustCabinet.Domain.Enums;

namespace TrustCabinet.Domain.Meetings;

public interface IMeetingRepository
{
    Task<Meeting?> FindAccessibleAsync(Guid id, Guid actorId, CancellationToken ct = default);

    Task<MeetingListPage> FindAuthorizedPageAsync(
        Guid actorId, MeetingListQuery query, CancellationToken ct = default);

    Task<Meeting?> FindOwnedAsync(Guid id, Guid creatorId, CancellationToken ct = default);

    Task<Meeting?> FindOwnedForUpdateAsync(Guid id, Guid creatorId, CancellationToken ct = default);

    Task<IReadOnlyList<MeetingApproverOption>> ListEligibleApproversAsync(CancellationToken ct = default);

    Task<bool> IsEligibleApproverAsync(Guid id, CancellationToken ct = default);

    Task<Meeting?> FindWorkflowAsync(Guid id, bool forUpdate = false, CancellationToken ct = default);

    Task<MeetingHistoryPage?> FindHistoryPageAsync(
        Guid meetingId, int page, int pageSize, CancellationToken ct = default);

    Task<bool> IsActiveUserWithCapabilityAsync(
        Guid userId, Capability capability, CancellationToken ct = default);

    Task<bool> IsUserActiveAsync(Guid userId, CancellationToken ct = default);

    Task<string?> FindUserDisplayNameAsync(Guid userId, CancellationToken ct = default);

    Task AddNotificationsAsync(IReadOnlyCollection<Notification> notifications, CancellationToken ct = default);

    Task AddHistoryAsync(MeetingStatusHistory history, CancellationToken ct = default);

    Task AddAsync(Meeting meeting, CancellationToken ct = default);

    Task SaveChangesAsync(CancellationToken ct = default);

    Task<T> InTransactionAsync<T>(
        Func<CancellationToken, Task<T>> operation, CancellationToken ct = default);
}
