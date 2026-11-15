namespace TrustCabinet.Domain.Notifications;

/// <summary>
/// The email beside the in-app "starting soon" reminder (PO 15/11/2026): every invited active user and every current
/// Guest of a Published meeting, once per planned start, in the 15 minutes before it.
/// </summary>
public interface IMeetingReminderEmails
{
    /// <returns>How many emails were queued.</returns>
    Task<int> QueueDueAsync(CancellationToken ct = default);
}
