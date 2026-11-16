using TrustCabinet.Domain.Entities;
using TrustCabinet.Domain.Meetings;

namespace TrustCabinet.Application.GuestAccess;

/// <summary>
/// UC-04's guest sessions, challenges and invitations as the use case reads and writes them.
/// Every query keeps the shape the moved code had: the same includes, tracking, filters and order.
/// </summary>
public interface IGuestSessionRepository
{
    /// <summary>The invitation with its meeting, untracked, while it still stands and its meeting is open to guests.</summary>
    Task<MeetingGuestInvitation?> FindOpenAsync(Guid invitationId, CancellationToken ct);

    /// <summary>
    /// Takes an exclusive lock on the one invitation row until the current transaction ends, and returns that
    /// invitation with its meeting, untracked; null, and no lock, when it is not open. Locks no other row. Throws
    /// <see cref="InvalidOperationException"/> when no transaction is open: the lock would be released at once, so
    /// the caller has not taken the serialization it relies on.
    /// </summary>
    Task<MeetingGuestInvitation?> LockOpenAsync(Guid invitationId, CancellationToken ct);

    /// <summary>The sending times of the invitation's challenges newer than <paramref name="since"/>, newest first, untracked. One query.</summary>
    Task<IReadOnlyList<DateTimeOffset>> ListRecentChallengeTimesAsync(
        Guid invitationId, DateTimeOffset since, int take, CancellationToken ct);

    /// <summary>Deletes the invitation's challenges created at or before <paramref name="since"/>. Runs immediately, not staged.</summary>
    Task<int> DeleteChallengesNotNewerThanAsync(Guid invitationId, DateTimeOffset since, CancellationToken ct);

    /// <summary>Stages the challenge; it is written by the next <c>IUnitOfWork.SaveChangesAsync</c>.</summary>
    void AddChallenge(GuestVerificationChallenge challenge);

    /// <summary>Deletes the one challenge by id. Runs immediately, not staged.</summary>
    Task<int> DeleteChallengeAsync(Guid challengeId, CancellationToken ct);

    /// <summary>The invitation's two newest challenges, newest first, tracked for consuming and counting. One query.</summary>
    Task<IReadOnlyList<GuestVerificationChallenge>> ListLatestChallengesAsync(Guid invitationId, CancellationToken ct);

    /// <summary>Stages the session; it is written by the next <c>IUnitOfWork.SaveChangesAsync</c>.</summary>
    void AddSession(GuestSession session);

    /// <summary>
    /// The caller's own session while it still grants access, with its invitation and meeting; null when the token's
    /// claims disagree or the session ended. One query.
    /// </summary>
    Task<GuestSession?> FindSessionAsync(MeetingGuestSession session, bool tracked, CancellationToken ct);

    /// <summary>Ends the one session (revocation and touch), when it is still live. Runs immediately, not staged.</summary>
    Task<int> RevokeSessionAsync(Guid sessionId, Guid invitationId, DateTimeOffset now, CancellationToken ct);

    /// <summary>
    /// Takes a shared lock on the one session row until the current transaction ends, so a write racing the session's
    /// end lands before it or not at all, while sessions do not queue behind one another. Locks no other row. Runs
    /// immediately, not staged. Throws <see cref="InvalidOperationException"/> when no transaction is open.
    /// Declared here for CA-11 (SCRUM-176), whose comment writes take this lock.
    /// </summary>
    Task LockSessionSharedAsync(Guid sessionId, CancellationToken ct);
}
