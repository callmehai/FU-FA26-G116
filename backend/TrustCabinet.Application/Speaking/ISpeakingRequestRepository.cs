using TrustCabinet.Application.Participants;
using TrustCabinet.Domain.Entities;
using TrustCabinet.Domain.Enums;

namespace TrustCabinet.Application.Speaking;

/// <summary>An open request as the queue shows it, with the names it is listed under.</summary>
public sealed record SpeakingRequestRow(Guid Id, Guid? UserId, Guid? GuestInvitationId, SpeakingRequestStatus Status,
    int QueuePosition, DateTimeOffset RequestedAt, DateTimeOffset? InvitedAt, DateTimeOffset? StartedAt,
    Guid? AgendaItemId, string? AgendaItemTitle, string? UserName, string? GuestName);

/// <summary>
/// Speaking requests for the queue use cases (SCRUM-27, UC-79). Reads of requests return tracked rows, which the
/// service changes and saves through <c>IUnitOfWork.SaveChangesAsync</c>. It also closes open requests for the
/// Participants module, through <see cref="ISpeakingRequestCloser"/>.
/// </summary>
public interface ISpeakingRequestRepository : ISpeakingRequestCloser
{
    /// <summary>Whether the account exists and is active. One query.</summary>
    Task<bool> IsActiveUserAsync(Guid userId, CancellationToken ct);

    /// <summary>The meeting's request with that id, tracked; null when there is none. One query.</summary>
    Task<SpeakingRequest?> FindAsync(Guid meetingId, Guid requestId, CancellationToken ct);

    /// <summary>The viewer's open request in the meeting (queued, invited or speaking), tracked. One query.</summary>
    Task<SpeakingRequest?> FindOpenAsync(Guid meetingId, SpeakingViewer viewer, CancellationToken ct);

    /// <summary>The highest queue position the meeting has handed out, over every status; 0 when none. One query.</summary>
    Task<int> LastQueuePositionAsync(Guid meetingId, CancellationToken ct);

    /// <summary>The meeting's agenda item in progress, if any. One query.</summary>
    Task<Guid?> FindCurrentAgendaItemIdAsync(Guid meetingId, CancellationToken ct);

    /// <summary>Whether someone in the meeting is invited to speak or speaking. One query.</summary>
    Task<bool> IsFloorHeldAsync(Guid meetingId, CancellationToken ct);

    /// <summary>
    /// The queued request just before (<paramref name="up"/>) or just after <paramref name="queuePosition"/>, tracked;
    /// null at the start or the end of the queue. One query.
    /// </summary>
    Task<SpeakingRequest?> FindQueuedNeighbourAsync(Guid meetingId, int queuePosition, bool up, CancellationToken ct);

    /// <summary>
    /// Whether the caller is connected to the meeting's active room right now, as LiveKit last reported: a guest by
    /// guest session, a member by account. One query.
    /// </summary>
    Task<bool> IsInRoomAsync(Guid meetingId, ParticipantActor actor, CancellationToken ct);

    /// <summary>The meeting's open requests with their names, untracked and unordered. One query.</summary>
    Task<IReadOnlyList<SpeakingRequestRow>> ListOpenAsync(Guid meetingId, CancellationToken ct);

    /// <summary>
    /// Closes every open request of the meeting as it ends (BR-337), at <paramref name="endedAt"/>. Requires the
    /// caller's transaction, which holds the meeting lock: throws <see cref="InvalidOperationException"/> without
    /// one. Staged; the caller saves.
    /// </summary>
    Task CloseAllOpenAsync(Guid meetingId, DateTimeOffset endedAt, CancellationToken ct);

    /// <summary>Stages the request; it is written by the next <c>IUnitOfWork.SaveChangesAsync</c>.</summary>
    void Add(SpeakingRequest request);
}
