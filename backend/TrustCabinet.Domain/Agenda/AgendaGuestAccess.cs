using TrustCabinet.Domain.Meetings;
namespace TrustCabinet.Domain.Agenda;

/// <summary>
/// UC-04 adapter: re-read invitation and session verification, expiry and revocation on every call.
/// No cached authorization from JWT claims alone. Readers must be current sessions for this meeting.
/// Implementations propagate cancellation and persistence errors; never authorize on failure.
/// </summary>
public interface IAgendaGuestAccess
{
    Task<bool> CanReadAsync(MeetingGuestSession session, CancellationToken ct);
    Task<IReadOnlyList<MeetingGuestSession>> ListReadersAsync(Guid meetingId, CancellationToken ct);
}
