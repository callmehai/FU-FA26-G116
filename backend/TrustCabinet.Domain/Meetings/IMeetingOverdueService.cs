namespace TrustCabinet.Domain.Meetings;

/// <summary>
/// BR-301 and ADR-0062 — the system closes what people forgot. A meeting nobody started a set time after its planned start is
/// cancelled, so it does not stay "Published" on everyone's calendar; the organizer keeps one by rescheduling it before
/// then. A meeting nobody ended a set time after its planned end is ended, so it does not stay "Đang diễn ra" for days.
/// </summary>
public interface IMeetingOverdueService
{
    /// <returns>How many meetings were cancelled.</returns>
    Task<int> CancelOverdueAsync(CancellationToken ct = default);

    /// <returns>How many meetings were ended.</returns>
    Task<int> EndOverrunAsync(CancellationToken ct = default);
}
