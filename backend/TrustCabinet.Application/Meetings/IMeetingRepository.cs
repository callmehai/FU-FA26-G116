using TrustCabinet.Domain.Entities;
using TrustCabinet.Domain.Enums;

namespace TrustCabinet.Application.Meetings;

public interface IMeetingRepository
{
    Task<Meeting?> FindAccessibleAsync(Guid id, Guid actorId, CancellationToken ct = default);

    Task<MeetingListPage> FindAuthorizedPageAsync(
        Guid actorId, MeetingListQuery query, CancellationToken ct = default);

    Task<Meeting?> FindOwnedAsync(Guid id, Guid creatorId, CancellationToken ct = default);

    /// <summary>
    /// Takes an exclusive lock on the one meeting row, when <paramref name="creatorId"/> created it, until the
    /// current transaction ends; then loads that meeting tracked, with its participants and their accounts. Takes
    /// no lock on the participant or user rows. Null, and no lock, when no such meeting exists.
    /// </summary>
    Task<Meeting?> LockOwnedAsync(Guid id, Guid creatorId, CancellationToken ct = default);

    Task<IReadOnlyList<MeetingApproverOption>> ListEligibleApproversAsync(CancellationToken ct = default);

    Task<bool> IsEligibleApproverAsync(Guid id, CancellationToken ct = default);

    Task<Meeting?> FindWorkflowAsync(Guid id, bool forUpdate = false, CancellationToken ct = default);

    /// <summary>Not-yet-started meetings whose planned start is at or before <paramref name="cutoff"/>, oldest first.</summary>
    Task<IReadOnlyList<Guid>> ListOverdueAsync(DateTimeOffset cutoff, int take, CancellationToken ct = default);

    /// <summary>Meetings still InProgress whose planned end and actual start are both at or before the cutoff.</summary>
    Task<IReadOnlyList<Guid>> ListOverrunAsync(DateTimeOffset cutoff, int take, CancellationToken ct = default);

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
}
