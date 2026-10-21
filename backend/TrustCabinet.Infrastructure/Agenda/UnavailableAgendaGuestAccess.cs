using TrustCabinet.Domain.Meetings;
using TrustCabinet.Domain.Agenda;

namespace TrustCabinet.Infrastructure.Agenda;

/// <summary>Fail closed until UC-04 registers its authoritative invitation/session adapter.</summary>
public sealed class UnavailableAgendaGuestAccess : IAgendaGuestAccess
{
    public Task<bool> CanReadAsync(MeetingGuestSession session, CancellationToken ct)
    {
        ct.ThrowIfCancellationRequested();
        return Task.FromResult(false);
    }

    public Task<IReadOnlyList<MeetingGuestSession>> ListReadersAsync(Guid meetingId, CancellationToken ct)
    {
        ct.ThrowIfCancellationRequested();
        return Task.FromResult<IReadOnlyList<MeetingGuestSession>>([]);
    }
}
