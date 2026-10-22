using TrustCabinet.Domain.Entities;

namespace TrustCabinet.Domain.Calls;

public interface ICallRepository
{
    /// <summary>
    /// The caller's standing in the meeting's room: an invited participant (any response except
    /// Delegated) with an active account, of a meeting in a participant-visible status. Null
    /// otherwise, which the endpoints turn into the same 404 as an unknown meeting.
    /// </summary>
    Task<CallRoomAccess?> FindRoomAccessAsync(Guid meetingId, Guid userId, CancellationToken ct = default);

    /// <summary>
    /// Users allowed in the room right now — the reconciliation job's roster. Empty once the
    /// meeting is no longer an in-progress online or hybrid meeting.
    /// </summary>
    Task<IReadOnlySet<Guid>> ListAllowedUserIdsAsync(Guid meetingId, CancellationToken ct = default);

    Task<CallSession?> FindActiveRoomSessionAsync(Guid meetingId, CancellationToken ct = default);

    /// <summary>
    /// The meeting's Active room session, created if there is none. Safe under concurrent
    /// webhooks: a partial unique index rejects a second Active row and the loser re-reads.
    /// </summary>
    Task<CallSession> EnsureActiveRoomSessionAsync(
        Guid meetingId, Guid startedById, DateTimeOffset startedAt, CancellationToken ct = default);

    /// <summary>Opens a stint unless one is already open for this user — webhooks repeat.</summary>
    Task RecordJoinAsync(Guid sessionId, Guid userId, DateTimeOffset joinedAt, CancellationToken ct = default);

    Task RecordLeaveAsync(Guid sessionId, Guid userId, DateTimeOffset leftAt, CancellationToken ct = default);

    /// <summary>Ends the session and closes every stint still open.</summary>
    Task EndSessionAsync(Guid sessionId, DateTimeOffset endedAt, CancellationToken ct = default);

    Task<IReadOnlyList<ActiveRoomSession>> ListActiveRoomSessionsAsync(CancellationToken ct = default);
}

/// <summary>
/// Everything the call room needs from LiveKit. The only place allowed to reference the SDK is
/// its implementation in Infrastructure.
/// </summary>
public interface ILiveKitGateway
{
    bool IsConfigured { get; }

    CallJoinTicket CreateJoinTicket(
        string roomName, Guid userId, string displayName, string metadataJson, TimeSpan ttl);

    /// <summary>Empty when the room does not exist.</summary>
    Task<IReadOnlyList<RoomParticipant>> ListParticipantsAsync(string roomName, CancellationToken ct);

    Task<IReadOnlyList<string>> ListRoomNamesAsync(CancellationToken ct);

    Task MuteTrackAsync(string roomName, string identity, string trackSid, CancellationToken ct);

    Task RemoveParticipantAsync(string roomName, string identity, CancellationToken ct);

    Task DeleteRoomAsync(string roomName, CancellationToken ct);

    /// <summary>Verifies the signature and parses; null when it is missing, stale or wrong.</summary>
    CallWebhookEvent? ReadWebhook(byte[] body, string? authorizationHeader);
}

public interface ICallRoomService
{
    Task<CallResult<CallRoomView>> GetRoomAsync(Guid meetingId, Guid actorId, CancellationToken ct = default);

    Task<CallResult<CallJoinTicket>> JoinAsync(Guid meetingId, Guid actorId, CancellationToken ct = default);

    Task<CallOutcome> MuteAsync(Guid meetingId, Guid actorId, Guid targetUserId, CancellationToken ct = default);

    Task HandleWebhookAsync(CallWebhookEvent webhook, CancellationToken ct = default);

    Task ReconcileAsync(CancellationToken ct = default);
}
