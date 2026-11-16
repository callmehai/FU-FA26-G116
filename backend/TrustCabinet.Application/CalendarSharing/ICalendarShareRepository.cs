using TrustCabinet.Domain.Entities;

namespace TrustCabinet.Application.CalendarSharing;

/// <summary>An invitee of the meeting-time search (UC-85a): who, and whether the account is still active.</summary>
public sealed record Invitee(Guid Id, string FullName, string Email, bool IsActive);

/// <summary>
/// Calendar shares and the people on them (SCRUM-142, SCRUM-137). A share returned by a <c>Lock…Async</c> method is
/// tracked, so a change to it is written by <c>IUnitOfWork.SaveChangesAsync</c>; every other read is untracked.
/// </summary>
public interface ICalendarShareRepository
{
    /// <summary>BR-430: an active account whose role holds MeetingCreator.</summary>
    Task<bool> IsOrganizerAsync(Guid userId, CancellationToken ct = default);

    /// <summary>
    /// UC-84f: the shares the user owns and, when <paramref name="includeViewed"/>, the ones they view, not past their end
    /// date (BR-425), newest first, with both people loaded.
    /// </summary>
    Task<List<CalendarShare>> FindListedAsync(Guid userId, bool includeViewed, DateOnly today,
        CancellationToken ct = default);

    /// <summary>Which of these users hold MeetingCreator through their role, active or not.</summary>
    Task<List<Guid>> FindMeetingCreatorIdsAsync(Guid[] userIds, CancellationToken ct = default);

    /// <summary>
    /// UC-84a/UC-84b step 1: one page of the active accounts other than the user, searched by name or email. For a direct
    /// share only the ones holding MeetingCreator (BR-430). Each says whether the pair already has an open share in the
    /// direction about to be sent, not past its end date (BR-425).
    /// </summary>
    Task<ShareCandidatePage> FindCandidatesAsync(Guid userId, CandidateQuery query, DateOnly today,
        CancellationToken ct = default);

    /// <summary>
    /// Each of these people as a share sees them now (BR-424, BR-430), read fresh from the database: inside a transaction,
    /// after a change was saved, it sees that change. An unknown id is missing from the result.
    /// </summary>
    Task<Dictionary<Guid, PersonState>> LoadPeopleAsync(IEnumerable<Guid> userIds, CancellationToken ct = default);

    /// <summary>
    /// <see cref="LoadPeopleAsync"/> after taking a shared lock on these people's <c>users</c> rows, in id order, until the
    /// transaction ends. A deactivation or role change updates that row, so it waits for this transaction; one that
    /// committed first is read. Call it inside a transaction.
    /// </summary>
    Task<Dictionary<Guid, PersonState>> LockPeopleAsync(IEnumerable<Guid> userIds, CancellationToken ct = default);

    /// <summary>
    /// The share, tracked, with its row under an exclusive lock until the transaction ends, so two answers to it cannot
    /// both win. Null when there is no such share. Call it inside a transaction.
    /// </summary>
    Task<CalendarShare?> LockAsync(Guid shareId, CancellationToken ct = default);

    /// <summary>
    /// The pair's open shares (Pending or Effective) past their end date (BR-425), tracked, each row under an exclusive
    /// lock until the transaction ends, taken in no particular order (the pair's rows are only locked here and by
    /// <see cref="LockAsync"/>, one at a time). Call it inside a transaction.
    /// </summary>
    Task<List<CalendarShare>> LockLapsedOpenAsync(Guid ownerId, Guid viewerId, DateOnly today,
        CancellationToken ct = default);

    /// <summary>
    /// BR-424: the Effective shares not past their end date where any of these people is the owner or the viewer, tracked,
    /// each row under an exclusive lock until the transaction ends, taken in id order (the order every other writer of
    /// these rows meets them in, so two sweeps never deadlock). Call it inside a transaction.
    /// </summary>
    Task<List<CalendarShare>> LockEffectiveOfPeopleAsync(Guid[] userIds, DateOnly today, CancellationToken ct = default);

    /// <summary>BR-423: whether the pair, in this direction, has a Pending or Effective share, whatever its end date.</summary>
    Task<bool> HasOpenShareAsync(Guid ownerId, Guid viewerId, CancellationToken ct = default);

    /// <summary>
    /// UC-85c: the shares viewed by the user that are Effective or Stopped and not past their end date (BR-425), with the
    /// owner loaded.
    /// </summary>
    Task<List<CalendarShare>> FindSharedWithAsync(Guid viewerId, DateOnly today, CancellationToken ct = default);

    /// <summary>
    /// UC-85a: the Effective shares from these owners to the viewer, not past their end date and with an active owner
    /// (BR-423, BR-424, BR-425).
    /// </summary>
    Task<List<CalendarShare>> FindEffectiveFromAsync(Guid viewerId, Guid[] ownerIds, DateOnly today,
        CancellationToken ct = default);

    /// <summary>UC-85a: these people by id, active or not. An unknown id is missing from the result.</summary>
    Task<Dictionary<Guid, Invitee>> FindInviteesAsync(Guid[] userIds, CancellationToken ct = default);

    /// <summary>Stages a new share; it is written by <c>IUnitOfWork.SaveChangesAsync</c>.</summary>
    void Add(CalendarShare share);
}
