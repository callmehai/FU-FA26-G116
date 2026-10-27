namespace TrustCabinet.Domain.Meetings;

/// <summary>
/// Cancels meetings nobody started a set time after their planned start (BR-301), so a forgotten meeting does not stay
/// "Published" on everyone's calendar. The organizer keeps one by rescheduling it before then.
/// </summary>
public interface IMeetingOverdueService
{
    /// <returns>How many meetings were cancelled.</returns>
    Task<int> CancelOverdueAsync(CancellationToken ct = default);
}
