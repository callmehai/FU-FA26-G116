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

    /// <summary>
    /// A guest session's standing in the meeting's room (SCRUM-59): the session is current (not signed out or
    /// expired, invitation not revoked, meeting Published or InProgress), belongs to this meeting and has a display
    /// name (UC-04 step 5). Null otherwise — guests lose the meeting once it ends, unlike members.
    /// </summary>
    Task<CallRoomAccess?> FindGuestRoomAccessAsync(Guid meetingId, Guid guestSessionId, CancellationToken ct = default);

    /// <summary>Guest sessions allowed in the room right now, by the same rule; empty unless InProgress online/hybrid.</summary>
    Task<IReadOnlySet<Guid>> ListAllowedGuestSessionIdsAsync(Guid meetingId, CancellationToken ct = default);

    Task<CallSession?> FindActiveRoomSessionAsync(Guid meetingId, CancellationToken ct = default);

    /// <summary>
    /// The meeting's Active room session, created if there is none. Safe under concurrent
    /// webhooks: a partial unique index rejects a second Active row and the loser re-reads.
    /// </summary>
    /// <param name="startedById">Null when a guest was the first one in.</param>
    Task<CallSession> EnsureActiveRoomSessionAsync(
        Guid meetingId, Guid? startedById, DateTimeOffset startedAt, CancellationToken ct = default);

    /// <summary>Opens a stint unless one is already open for this member — webhooks repeat.</summary>
    Task RecordJoinAsync(Guid sessionId, CallMember member, DateTimeOffset joinedAt, CancellationToken ct = default);

    Task RecordLeaveAsync(Guid sessionId, CallMember member, DateTimeOffset leftAt, CancellationToken ct = default);

    /// <summary>
    /// Ends the session and closes every stint still open. The effective end cannot precede
    /// the session start or any already recorded participant join/leave timestamp.
    /// </summary>
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
        string roomName, string identity, string displayName, string metadataJson, TimeSpan ttl);

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
    /// <summary>Closes the provider room, then ends call history using the closure confirmation time.</summary>
    Task CloseMeetingRoomAsync(Guid meetingId, CancellationToken ct = default);

    Task<CallResult<CallRoomView>> GetRoomAsync(Guid meetingId, CallCaller caller, CancellationToken ct = default);

    /// <param name="recordingNoticeAcknowledged">
    /// A guest must confirm the recording notice before a token is issued; the confirmation is audited (SCRUM-59).
    /// </param>
    Task<CallResult<CallJoinTicket>> JoinAsync(
        Guid meetingId, CallCaller caller, bool recordingNoticeAcknowledged = false, CancellationToken ct = default);

    Task<CallOutcome> MuteAsync(Guid meetingId, Guid actorId, Guid targetUserId, CancellationToken ct = default);

    Task HandleWebhookAsync(CallWebhookEvent webhook, CancellationToken ct = default);

    Task ReconcileAsync(CancellationToken ct = default);

    /// <summary>
    /// Reconciles one meeting's room now rather than at the next 30-second run: whoever lost access (a revoked
    /// guest invitation, a removed participant) is taken out at once.
    /// </summary>
    Task EnforceRoomAccessAsync(Guid meetingId, CancellationToken ct = default);
}
