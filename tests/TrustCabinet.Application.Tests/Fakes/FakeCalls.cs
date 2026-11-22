using TrustCabinet.Application.Calls;
using TrustCabinet.Domain.Entities;
using TrustCabinet.Domain.Enums;

namespace TrustCabinet.Application.Tests.Fakes;

/// <summary>
/// Room access and call history as the call room sees them: fixed answers, and every write recorded in
/// <see cref="Calls"/>. The repository keeps its own transactions, so nothing here goes through a unit of work.
/// </summary>
public sealed class FakeCallRepository : ICallRepository
{
    public Dictionary<Guid, CallRoomAccess> Access { get; } = [];
    public CallSession? ActiveSession { get; set; }

    /// <summary>The active meeting-room sessions the reconciliation job lists.</summary>
    public List<ActiveRoomSession> Sessions { get; } = [];

    /// <summary>How many raised hands a leave or the end of the session closes (BR-337).</summary>
    public int ClosedHands { get; set; }

    public List<string> Calls { get; } = [];

    /// <summary>Thrown by every call in place of answering: a store that cannot be reached.</summary>
    public Exception? Fail { get; set; }

    public Task<CallRoomAccess?> FindRoomAccessAsync(Guid meetingId, Guid userId, CancellationToken ct = default)
        => Answer("find-access", Access.GetValueOrDefault(userId));

    public Task<IReadOnlySet<Guid>> ListAllowedUserIdsAsync(Guid meetingId, CancellationToken ct = default)
        => Answer<IReadOnlySet<Guid>>("list-allowed-users", Access.Keys.ToHashSet());

    public Task<CallRoomAccess?> FindGuestRoomAccessAsync(Guid meetingId, Guid guestSessionId, Guid? invitationId,
        CancellationToken ct = default)
        => Answer("find-guest-access", Access.GetValueOrDefault(guestSessionId));

    public Task<IReadOnlySet<Guid>> ListAllowedGuestSessionIdsAsync(Guid meetingId, CancellationToken ct = default)
        => Answer<IReadOnlySet<Guid>>("list-allowed-guests", new HashSet<Guid>());

    public Task<CallSession?> FindActiveRoomSessionAsync(Guid meetingId, CancellationToken ct = default)
        => Answer("find-session", ActiveSession);

    public Task<CallSession> EnsureActiveRoomSessionAsync(Guid meetingId, Guid? startedById, DateTimeOffset startedAt,
        CancellationToken ct = default)
        => Answer("ensure-session", ActiveSession ??= new CallSession
        {
            MeetingId = meetingId, Kind = CallKind.MeetingRoom, Status = CallStatus.Active, StartedAt = startedAt,
        });

    public Task RecordJoinAsync(Guid sessionId, CallMember member, DateTimeOffset joinedAt, CancellationToken ct = default)
        => Answer("join", true);

    public Task<int> RecordLeaveAsync(Guid sessionId, CallMember member, DateTimeOffset leftAt, CancellationToken ct = default)
        => Answer("leave", ClosedHands);

    public Task<int> EndSessionAsync(Guid sessionId, DateTimeOffset endedAt, CancellationToken ct = default)
        => Answer("end-session", ClosedHands);

    public Task<IReadOnlyList<ActiveRoomSession>> ListActiveRoomSessionsAsync(CancellationToken ct = default)
        => Answer<IReadOnlyList<ActiveRoomSession>>("list-sessions", Sessions.ToArray());

    private Task<T> Answer<T>(string name, T answer)
    {
        if (Fail is { } failure) return Task.FromException<T>(failure);
        Calls.Add(name);
        return Task.FromResult(answer);
    }
}

/// <summary>A LiveKit server in memory: rooms by name, and what was asked of it.</summary>
public sealed class FakeLiveKitGateway : ILiveKitGateway
{
    public bool IsConfigured { get; set; } = true;
    public Dictionary<string, List<RoomParticipant>> Rooms { get; } = [];
    public List<string> Calls { get; } = [];

    /// <summary>Thrown by every server API call: a provider that cannot be reached.</summary>
    public LiveKitUnavailableException? Unavailable { get; set; }

    public CallJoinTicket CreateJoinTicket(string roomName, string identity, string displayName, string metadataJson, TimeSpan ttl)
    {
        Calls.Add($"ticket:{identity}");
        return new("wss://livekit.test", "token", roomName, identity, DateTimeOffset.UnixEpoch.Add(ttl));
    }

    public Task<IReadOnlyList<RoomParticipant>> ListParticipantsAsync(string roomName, CancellationToken ct)
        => Call<IReadOnlyList<RoomParticipant>>("list", Rooms.GetValueOrDefault(roomName) ?? []);

    public Task<IReadOnlyList<string>> ListRoomNamesAsync(CancellationToken ct)
        => Call<IReadOnlyList<string>>("rooms", Rooms.Keys.ToArray());

    public Task MuteTrackAsync(string roomName, string identity, string trackSid, CancellationToken ct)
        => Call($"mute:{identity}:{trackSid}", true);

    public Task RemoveParticipantAsync(string roomName, string identity, CancellationToken ct)
        => Call($"remove:{identity}", true);

    public Task DeleteRoomAsync(string roomName, CancellationToken ct) => Call($"delete:{roomName}", true);

    public CallWebhookEvent? ReadWebhook(byte[] body, string? authorizationHeader) => null;

    private Task<T> Call<T>(string name, T answer)
    {
        if (Unavailable is { } failure) return Task.FromException<T>(failure);
        Calls.Add(name);
        return Task.FromResult(answer);
    }
}
