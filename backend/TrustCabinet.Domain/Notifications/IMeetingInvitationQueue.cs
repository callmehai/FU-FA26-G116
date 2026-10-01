namespace TrustCabinet.Domain.Notifications;

/// <summary>
/// Creates the durable email jobs for one successful meeting publication.
///
/// The publication service must call this while its database transaction is still open. That
/// keeps the Published state, its immutable history row and the invitation jobs atomic: either
/// all of them commit, or none of them do.
/// </summary>
public interface IMeetingInvitationQueue
{
    Task<int> QueueForPublishedMeetingAsync(Guid meetingId, CancellationToken ct = default);
}
