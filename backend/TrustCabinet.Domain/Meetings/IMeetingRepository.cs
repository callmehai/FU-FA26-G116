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

    /// <summary>Not-yet-started meetings whose planned start is at or before <paramref name="cutoff"/>, oldest first.</summary>
    Task<IReadOnlyList<Guid>> ListOverdueAsync(DateTimeOffset cutoff, int take, CancellationToken ct = default);

    Task<MeetingHistoryPage?> FindHistoryPageAsync(
        Guid meetingId, int page, int pageSize, CancellationToken ct = default);

    Task<bool> IsActiveUserWithCapabilityAsync(
        Guid userId, Capability capability, CancellationToken ct = default);

    Task<bool> IsUserActiveAsync(Guid userId, CancellationToken ct = default);

    Task<string?> FindUserDisplayNameAsync(Guid userId, CancellationToken ct = default);

    Task AddNotificationsAsync(IReadOnlyCollection<Notification> notifications, CancellationToken ct = default);

    Task AddHistoryAsync(MeetingStatusHistory history, CancellationToken ct = default);

    Task<IReadOnlyList<User>> LockParticipantUsersAsync(Guid[] userIds, CancellationToken ct = default);

    Task AddAsync(Meeting meeting, CancellationToken ct = default);

    Task SaveChangesAsync(CancellationToken ct = default);

    Task StagePublicationInvitationsAsync(Meeting meeting, DateTimeOffset now, CancellationToken ct = default)
        => Task.CompletedTask;

    /// <summary>Emails every invitee and Guest of a Published meeting that it was cancelled, with the reason (BR-300).</summary>
    Task StageCancellationAsync(
        Meeting meeting, Guid historyId, Guid actorId, string reason, DateTimeOffset now, CancellationToken ct = default);

    Task StageGuestRescheduleInvitationsAsync(Meeting meeting, Guid historyId, DateTimeOffset now, CancellationToken ct = default)
        => Task.CompletedTask;

    Task<T> InTransactionAsync<T>(
        Func<CancellationToken, Task<T>> operation, CancellationToken ct = default);
}
