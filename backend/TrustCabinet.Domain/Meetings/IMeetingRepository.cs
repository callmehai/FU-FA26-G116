using TrustCabinet.Domain.Entities;

namespace TrustCabinet.Domain.Meetings;

public interface IMeetingRepository
{
    Task<Meeting?> FindOwnedAsync(Guid id, Guid creatorId, CancellationToken ct = default);

    Task<Meeting?> FindOwnedForUpdateAsync(Guid id, Guid creatorId, CancellationToken ct = default);

    Task<IReadOnlyList<MeetingApproverOption>> ListEligibleApproversAsync(CancellationToken ct = default);

    Task<bool> IsEligibleApproverAsync(Guid id, CancellationToken ct = default);

    Task AddAsync(Meeting meeting, CancellationToken ct = default);

    Task SaveChangesAsync(CancellationToken ct = default);

    Task<T> InTransactionAsync<T>(
        Func<CancellationToken, Task<T>> operation, CancellationToken ct = default);
}
