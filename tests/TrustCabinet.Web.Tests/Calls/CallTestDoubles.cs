using TrustCabinet.Domain.Calls;
using TrustCabinet.Domain.Entities;
using TrustCabinet.Domain.Enums;

namespace TrustCabinet.Web.Tests.Calls;

/// <summary>In-memory call history and room access, for service and endpoint tests.</summary>
internal sealed class FakeCallRepository : ICallRepository
{
    public Dictionary<(Guid MeetingId, Guid UserId), CallRoomAccess> Access { get; } = [];

    public Dictionary<Guid, HashSet<Guid>> Allowed { get; } = [];

    /// <summary>Guest sessions with room access, by session id.</summary>
    public Dictionary<Guid, CallRoomAccess> GuestAccess { get; } = [];

    public List<CallSession> Sessions { get; } = [];

    public List<CallParticipant> Stints { get; } = [];

    public Exception? ReadThrows { get; set; }

    public FakeCallRepository Invite(
        Guid meetingId, Guid userId, MeetingRole role = MeetingRole.Member,
        MeetingStatus status = MeetingStatus.InProgress, MeetingMode mode = MeetingMode.Online)
    {
        Access[(meetingId, userId)] = new CallRoomAccess(meetingId, status, mode, role, $"User {userId:N}");
        if (status == MeetingStatus.InProgress && mode != MeetingMode.InPerson)
        {
            (Allowed.TryGetValue(meetingId, out var set) ? set : Allowed[meetingId] = []).Add(userId);
        }

        return this;
    }

    public Task<CallRoomAccess?> FindRoomAccessAsync(Guid meetingId, Guid userId, CancellationToken ct = default)
    {
        if (ReadThrows is { } error)
        {
            throw error;
        }

        return Task.FromResult(Access.GetValueOrDefault((meetingId, userId)));
    }

    public Task<IReadOnlySet<Guid>> ListAllowedUserIdsAsync(Guid meetingId, CancellationToken ct = default)
        => Task.FromResult<IReadOnlySet<Guid>>(Allowed.GetValueOrDefault(meetingId) ?? []);

    /// <summary>A verified, named guest session of the meeting (UC-04); revoke by removing it.</summary>
    public FakeCallRepository AdmitGuest(
        Guid meetingId, Guid guestSessionId, MeetingStatus status = MeetingStatus.InProgress, MeetingMode mode = MeetingMode.Online)
    {
        GuestAccess[guestSessionId] = new CallRoomAccess(meetingId, status, mode, MeetingRole.Guest, "Khách tự khai");
        return this;
    }

    public Task<CallRoomAccess?> FindGuestRoomAccessAsync(Guid meetingId, Guid guestSessionId, Guid? invitationId,
        CancellationToken ct = default)
    {
        if (ReadThrows is { } error)
        {
            throw error;
        }

        return Task.FromResult(GuestAccess.GetValueOrDefault(guestSessionId) is { } access && access.MeetingId == meetingId ? access : null);
    }

    public Task<IReadOnlySet<Guid>> ListAllowedGuestSessionIdsAsync(Guid meetingId, CancellationToken ct = default)
        => Task.FromResult<IReadOnlySet<Guid>>(GuestAccess
            .Where(g => g.Value.MeetingId == meetingId && g.Value.Status == MeetingStatus.InProgress && g.Value.Mode != MeetingMode.InPerson)
            .Select(g => g.Key)
            .ToHashSet());

    public Task<CallSession?> FindActiveRoomSessionAsync(Guid meetingId, CancellationToken ct = default)
        => Task.FromResult(Sessions.SingleOrDefault(s => s.MeetingId == meetingId && s.Status == CallStatus.Active));

    public async Task<CallSession> EnsureActiveRoomSessionAsync(
        Guid meetingId, Guid? startedById, DateTimeOffset startedAt, CancellationToken ct = default)
    {
        if (await FindActiveRoomSessionAsync(meetingId, ct) is { } existing)
        {
            return existing;
        }

        var session = new CallSession
        {
            Kind = CallKind.MeetingRoom,
            MeetingId = meetingId,
            Status = CallStatus.Active,
            StartedById = startedById,
            StartedAt = startedAt,
        };
        Sessions.Add(session);
        return session;
    }

    public Task RecordJoinAsync(Guid sessionId, CallMember member, DateTimeOffset joinedAt, CancellationToken ct = default)
    {
        if (!Stints.Any(p => p.CallSessionId == sessionId && Of(p) == member && p.LeftAt == null))
        {
            Stints.Add(new CallParticipant
            {
                CallSessionId = sessionId, UserId = member.UserId, GuestSessionId = member.GuestSessionId, JoinedAt = joinedAt,
            });
        }

        return Task.CompletedTask;
    }

    private static CallMember Of(CallParticipant stint) => new(stint.UserId, stint.GuestSessionId);

    public Task RecordLeaveAsync(Guid sessionId, CallMember member, DateTimeOffset leftAt, CancellationToken ct = default)
    {
        foreach (var stint in Stints.Where(p => p.CallSessionId == sessionId && Of(p) == member && p.LeftAt == null))
        {
            stint.LeftAt = leftAt;
        }

        return Task.CompletedTask;
    }

    public Task EndSessionAsync(Guid sessionId, DateTimeOffset endedAt, CancellationToken ct = default)
    {
        var session = Sessions.Single(s => s.Id == sessionId);
        session.Status = CallStatus.Ended;
        session.EndedAt = endedAt;
        foreach (var stint in Stints.Where(p => p.CallSessionId == sessionId && p.LeftAt == null))
        {
            stint.LeftAt = endedAt;
        }

        return Task.CompletedTask;
    }

    public Task<IReadOnlyList<ActiveRoomSession>> ListActiveRoomSessionsAsync(CancellationToken ct = default)
        => Task.FromResult<IReadOnlyList<ActiveRoomSession>>(Sessions
            .Where(s => s.Status == CallStatus.Active)
            .Select(s =>
            {
                var status = Access.Values.FirstOrDefault(a => a.MeetingId == s.MeetingId);
                return new ActiveRoomSession(
                    s.Id,
                    s.MeetingId!.Value,
                    MeetingStatusOf(s.MeetingId!.Value),
                    status?.Mode ?? MeetingMode.Online,
                    Stints.Where(p => p.CallSessionId == s.Id && p.LeftAt == null).Select(Of).ToHashSet());
            })
            .ToArray());

    /// <summary>Lets a test move a meeting on (e.g. to Ended) after its room opened.</summary>
    public Dictionary<Guid, MeetingStatus> MeetingStatusOverride { get; } = [];

    private MeetingStatus MeetingStatusOf(Guid meetingId)
        => MeetingStatusOverride.TryGetValue(meetingId, out var status)
            ? status
            : Access.Values.FirstOrDefault(a => a.MeetingId == meetingId)?.Status ?? MeetingStatus.InProgress;
}

/// <summary>A LiveKit that records every server call instead of making it.</summary>
internal sealed class FakeLiveKitGateway : ILiveKitGateway
{
    public bool IsConfigured { get; set; } = true;

    public Dictionary<string, List<RoomParticipant>> Rooms { get; } = [];

    public List<(string Room, string Identity, string TrackSid)> Muted { get; } = [];

    public List<(string Room, string Identity)> Removed { get; } = [];

    public List<string> Deleted { get; } = [];

    public LiveKitUnavailableException? Unavailable { get; set; }

    public CallWebhookEvent? NextWebhook { get; set; }

    public (string Room, string Identity, string Name, string Metadata, TimeSpan Ttl)? LastTicket { get; private set; }

    public RoomParticipant Join(string room, Guid userId, params RoomTrack[] tracks)
        => Join(room, CallMember.User(userId), tracks);

    public RoomParticipant Join(string room, CallMember member, params RoomTrack[] tracks)
        => Join(room, new RoomParticipant(member.Identity, "name", DateTimeOffset.UtcNow, tracks));

    public RoomParticipant Join(string room, RoomParticipant participant)
    {
        (Rooms.TryGetValue(room, out var list) ? list : Rooms[room] = []).Add(participant);
        return participant;
    }

    public CallJoinTicket CreateJoinTicket(string roomName, string identity, string displayName, string metadataJson, TimeSpan ttl)
    {
        LastTicket = (roomName, identity, displayName, metadataJson, ttl);
        return new CallJoinTicket("ws://livekit.test", "token", roomName, identity, DateTimeOffset.UtcNow.Add(ttl));
    }

    public Task<IReadOnlyList<RoomParticipant>> ListParticipantsAsync(string roomName, CancellationToken ct)
    {
        ThrowIfUnavailable();
        return Task.FromResult<IReadOnlyList<RoomParticipant>>(Rooms.GetValueOrDefault(roomName)?.ToArray() ?? []);
    }

    public Task<IReadOnlyList<string>> ListRoomNamesAsync(CancellationToken ct)
        => Task.FromResult<IReadOnlyList<string>>(Rooms.Keys.ToArray());

    public Task MuteTrackAsync(string roomName, string identity, string trackSid, CancellationToken ct)
    {
        ThrowIfUnavailable();
        Muted.Add((roomName, identity, trackSid));
        return Task.CompletedTask;
    }

    public Task RemoveParticipantAsync(string roomName, string identity, CancellationToken ct)
    {
        Removed.Add((roomName, identity));
        Rooms.GetValueOrDefault(roomName)?.RemoveAll(p => p.Identity == identity);
        return Task.CompletedTask;
    }

    public Task DeleteRoomAsync(string roomName, CancellationToken ct)
    {
        Deleted.Add(roomName);
        Rooms.Remove(roomName);
        return Task.CompletedTask;
    }

    public CallWebhookEvent? ReadWebhook(byte[] body, string? authorizationHeader) => NextWebhook;

    private void ThrowIfUnavailable()
    {
        if (Unavailable is { } error)
        {
            throw error;
        }
    }
}
