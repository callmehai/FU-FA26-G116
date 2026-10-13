namespace TrustCabinet.Domain.Agenda;

/// <summary>A verified token's guest identity, never a UserId or a client-supplied display name.</summary>
public sealed record AgendaGuestSession(Guid MeetingId, Guid InvitationId, Guid SessionId)
{
    public bool IsValid => MeetingId != Guid.Empty && InvitationId != Guid.Empty && SessionId != Guid.Empty;
}

/// <summary>
/// UC-04 adapter: re-read invitation and session verification, expiry and revocation on every call.
/// No cached authorization from JWT claims alone. Readers must be current sessions for this meeting.
/// Implementations propagate cancellation and persistence errors; never authorize on failure.
/// </summary>
public interface IAgendaGuestAccess
{
    Task<bool> CanReadAsync(AgendaGuestSession session, CancellationToken ct);
    Task<IReadOnlyList<AgendaGuestSession>> ListReadersAsync(Guid meetingId, CancellationToken ct);
}
