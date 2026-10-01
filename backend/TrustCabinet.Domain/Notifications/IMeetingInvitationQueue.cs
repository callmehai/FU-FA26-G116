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
    Task<int> QueueForPublishedMeetingAsync(MeetingPublicationInvitation publication, CancellationToken ct = default);
}

/// <summary>Immutable values selected by Publish while the meeting row is locked.</summary>
public sealed record MeetingPublicationInvitation(
    Guid MeetingId,
    Guid OriginHistoryId,
    string Title,
    string Description,
    string Location,
    DateTimeOffset StartsAt,
    DateTimeOffset? EndsAt,
    DateTimeOffset PublishedAt,
    IReadOnlyList<MeetingInvitationRecipient> Recipients);

public sealed record MeetingInvitationRecipient(Guid UserId, string FullName, string Email);

/// <summary>A publication cannot create usable invitation jobs with the current configuration.</summary>
public sealed class MeetingInvitationQueueException(string message) : Exception(message);
