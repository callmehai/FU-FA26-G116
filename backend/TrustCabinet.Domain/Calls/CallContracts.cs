using TrustCabinet.Domain.Enums;
using TrustCabinet.Domain.Meetings;

namespace TrustCabinet.Domain.Calls;

/// <summary>
/// LiveKit room names for meeting rooms (ADR-0010). The name is derived from the meeting id on the
/// server, so a caller can never ask for a token to a room of their own choosing.
/// </summary>
public static class CallRoomNames
{
    private const string MeetingPrefix = "meeting-";

    public static string ForMeeting(Guid meetingId) => MeetingPrefix + meetingId.ToString("D");

    public static bool TryParseMeeting(string? roomName, out Guid meetingId)
    {
        meetingId = Guid.Empty;
        return roomName is not null
            && roomName.StartsWith(MeetingPrefix, StringComparison.Ordinal)
            && Guid.TryParseExact(roomName[MeetingPrefix.Length..], "D", out meetingId);
    }
}

/// <summary>Whether a meeting's room can be entered, as the pre-join screen explains it.</summary>
public enum CallRoomStatus
{
    Open,
    NotStarted,
    Ended,
    InPersonOnly,
    Cancelled,
}

public static class CallRoomRules
{
    /// <summary>
    /// The room exists only for an online or hybrid meeting that is in progress (SCRUM-41). An
    /// in-person meeting never has one, whatever its status.
    /// </summary>
    public static CallRoomStatus StatusOf(MeetingStatus status, MeetingMode mode)
        => mode == MeetingMode.InPerson ? CallRoomStatus.InPersonOnly
            : status switch
            {
                MeetingStatus.InProgress => CallRoomStatus.Open,
                MeetingStatus.Ended => CallRoomStatus.Ended,
                MeetingStatus.Cancelled => CallRoomStatus.Cancelled,
                _ => CallRoomStatus.NotStarted,
            };
}

/// <summary>
/// Who a LiveKit participant is: an internal user or one verified guest session (SCRUM-59). Exactly one id is set.
/// The identity string is derived here on the server, so nobody can pick their own; a user's is the bare user id
/// (unchanged since SCRUM-41), a guest's is prefixed so it can never be read as a user id.
/// </summary>
public readonly record struct CallMember(Guid? UserId, Guid? GuestSessionId)
{
    private const string GuestPrefix = "guest-";

    public static CallMember User(Guid userId) => new(userId, null);

    public static CallMember Guest(Guid guestSessionId) => new(null, guestSessionId);

    public bool IsGuest => GuestSessionId is not null;

    public string Identity => GuestSessionId is { } session ? GuestPrefix + session.ToString("D") : UserId!.Value.ToString("D");

    public static bool TryParse(string? identity, out CallMember member)
    {
        member = default;
        if (identity is null) return false;
        if (identity.StartsWith(GuestPrefix, StringComparison.Ordinal))
        {
            if (!Guid.TryParseExact(identity[GuestPrefix.Length..], "D", out var session)) return false;
            member = Guest(session);
            return true;
        }

        if (!Guid.TryParseExact(identity, "D", out var user)) return false;
        member = User(user);
        return true;
    }
}

/// <summary>The caller of a room endpoint: a signed-in user, or a guest token (UC-04) for one meeting.</summary>
public sealed record CallCaller(Guid? UserId = null, MeetingGuestSession? Guest = null)
{
    public static implicit operator CallCaller(Guid userId) => new(userId);
}

public enum CallOutcome
{
    Success,
    /// <summary>No access, or not an invited participant — answered as 404 so existence stays hidden.</summary>
    NotFound,
    RoomNotOpen,
    InPersonMeeting,
    NotChair,
    TargetNotInRoom,
    NotConfigured,
    ProviderFailed,
    ReadFailed,
    /// <summary>A guest asked for a token without confirming the recording notice (SCRUM-59).</summary>
    RecordingNoticeRequired,
}

/// <summary>The caller's standing in one meeting's room. A guest's role is <see cref="MeetingRole.Guest"/> and the name is self-declared.</summary>
public sealed record CallRoomAccess(
    Guid MeetingId, MeetingStatus Status, MeetingMode Mode, MeetingRole Role, string FullName);

/// <summary><c>RetentionDays</c>: how long what is recorded in the room is kept, as the pre-join notice states it.</summary>
public sealed record CallRoomView(
    Guid MeetingId, CallRoomStatus Status, MeetingRole MyRole, bool Available, int? ParticipantCount, int RetentionDays);

public sealed record CallJoinTicket(
    string ServerUrl, string Token, string RoomName, string Identity, DateTimeOffset ExpiresAt);

public sealed record CallResult<T>(CallOutcome Outcome, T? Value = default)
{
    public static CallResult<T> Ok(T value) => new(CallOutcome.Success, value);

    public static CallResult<T> Fail(CallOutcome outcome) => new(outcome);
}

// SDK-neutral views of LiveKit state, so the domain never references the LiveKit SDK.
public enum CallTrackSource
{
    Unknown,
    Camera,
    Microphone,
    ScreenShare,
    ScreenShareAudio,
}

public sealed record RoomTrack(string Sid, CallTrackSource Source, bool Muted);

public sealed record RoomParticipant(
    string Identity, string Name, DateTimeOffset JoinedAt, IReadOnlyList<RoomTrack> Tracks);

/// <summary>A verified LiveKit webhook, reduced to the fields the call room acts on.</summary>
public sealed record CallWebhookEvent(
    string Id,
    string Event,
    string? RoomName,
    string? ParticipantIdentity,
    DateTimeOffset? ParticipantJoinedAt,
    string? TrackSid,
    CallTrackSource TrackSource,
    DateTimeOffset CreatedAt);

/// <summary>An active meeting-room session as the reconciliation job sees it.</summary>
public sealed record ActiveRoomSession(
    Guid SessionId,
    Guid MeetingId,
    MeetingStatus MeetingStatus,
    MeetingMode MeetingMode,
    IReadOnlySet<CallMember> OpenMembers);

/// <summary>LiveKit could not be reached or refused a server API call.</summary>
public sealed class LiveKitUnavailableException(string message, Exception? inner = null)
    : Exception(message, inner);
