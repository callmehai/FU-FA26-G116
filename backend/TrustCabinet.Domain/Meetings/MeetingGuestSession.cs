namespace TrustCabinet.Domain.Meetings;

/// <summary>A verified token's guest identity shared by meeting features.</summary>
public sealed record MeetingGuestSession(Guid MeetingId, Guid InvitationId, Guid SessionId)
{
    public bool IsValid => MeetingId != Guid.Empty && InvitationId != Guid.Empty && SessionId != Guid.Empty;
}
