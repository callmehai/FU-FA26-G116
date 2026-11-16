using TrustCabinet.Domain.Entities;
using TrustCabinet.Domain.Enums;

namespace TrustCabinet.Application.Email;

/// <summary>
/// The roster events waiting to become invitation emails (<see cref="MeetingInvitationDispatch"/>), and the facts about
/// the meeting and the invitee that decide whether one still should.
/// </summary>
public interface IMeetingInvitationDispatchRepository
{
    /// <summary>Stages the dispatch; it is written by the next <c>SaveChangesAsync</c>.</summary>
    void Add(MeetingInvitationDispatch dispatch);

    /// <summary>The Guests of the meeting whose invitation is not revoked, tracked (no row lock).</summary>
    Task<MeetingGuestInvitation[]> ListActiveGuestInvitationsAsync(Guid meetingId, CancellationToken ct = default);

    /// <summary>The ids of up to <paramref name="take"/> pending or blocked dispatches that are due, oldest first.</summary>
    Task<Guid[]> ListDueIdsAsync(DateTimeOffset now, int take, CancellationToken ct = default);

    /// <summary>The dispatch as stored, not tracked; null when there is none.</summary>
    Task<MeetingInvitationDispatch?> FindAsync(Guid id, CancellationToken ct = default);

    /// <summary>
    /// Takes an exclusive lock on the meeting row, then on the dispatch row, skipping the dispatch if another worker
    /// holds it, and returns the dispatch reloaded from the store, for update. Null when it was skipped. Needs the
    /// caller's transaction: the locks last until it ends.
    /// </summary>
    Task<MeetingInvitationDispatch?> LockAsync(Guid meetingId, Guid id, CancellationToken ct = default);

    /// <summary>Whether the Guest invitation exists and is not revoked.</summary>
    Task<bool> IsGuestInvitationActiveAsync(Guid? guestInvitationId, CancellationToken ct = default);

    /// <summary>Whether the participant is still on the roster.</summary>
    Task<bool> IsParticipantActiveAsync(Guid participantId, CancellationToken ct = default);

    /// <summary>Whether the meeting has ended or been cancelled.</summary>
    Task<bool> IsMeetingClosedAsync(Guid? meetingId, CancellationToken ct = default);

    /// <summary>The meeting's status; the meeting must exist.</summary>
    Task<MeetingStatus> GetMeetingStatusAsync(Guid? meetingId, CancellationToken ct = default);

    /// <summary>The delivery key the participant's first invitation went out under, if recorded.</summary>
    Task<string?> GetParticipantInitialKeyAsync(Guid participantId, CancellationToken ct = default);

    /// <summary>The delivery key the Guest's first invitation went out under, if recorded.</summary>
    Task<string?> GetGuestInitialKeyAsync(Guid? guestInvitationId, CancellationToken ct = default);

    /// <summary>Whether this invitee's invitation for the meeting already became an email.</summary>
    Task<bool> HasMaterializedInvitationAsync(Guid? meetingId, Guid? participantId, Guid? guestInvitationId,
        CancellationToken ct = default);

    /// <summary>
    /// Suppresses the dispatch if it is still pending or blocked. Runs immediately, not staged; returns the number of
    /// rows updated.
    /// </summary>
    Task<int> SuppressAsync(Guid id, DateTimeOffset at, CancellationToken ct = default);

    /// <summary>
    /// Blocks the dispatch until <paramref name="nextAttemptAt"/> because an outside dependency is unavailable, if it
    /// is still pending or blocked and due at <paramref name="at"/>. Runs immediately, not staged; returns the number
    /// of rows updated.
    /// </summary>
    Task<int> BlockOnDependencyAsync(Guid id, DateTimeOffset nextAttemptAt, DateTimeOffset at,
        CancellationToken ct = default);

    /// <summary>
    /// Counts a failed attempt of the dispatch, if it is still pending or blocked: pending again until
    /// <paramref name="retryAt"/>, or failed once it reaches <paramref name="maxAttempts"/>. Runs immediately, not
    /// staged; returns the number of rows updated.
    /// </summary>
    Task<int> RecordFailedAttemptAsync(Guid id, int maxAttempts, DateTimeOffset retryAt, string error,
        DateTimeOffset at, CancellationToken ct = default);
}
