namespace TrustCabinet.Domain.Notifications;

/// <summary>
/// Creates durable email jobs for successful meeting publications.
/// </summary>
public interface IMeetingInvitationQueue
{
    Task<int> QueueForPublishedMeetingAsync(Guid meetingId, CancellationToken ct = default);

    /// <summary>
    /// Reconciles one published meeting whose invitation jobs were not initialized yet.
    /// </summary>
    Task<Guid?> QueueNextUninitializedPublishedMeetingAsync(CancellationToken ct = default);
}
