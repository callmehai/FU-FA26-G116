using TrustCabinet.Domain.Entities;

namespace TrustCabinet.Domain.Notifications;

public sealed record NotificationPage(
    IReadOnlyList<Notification> Items,
    int UnreadCount,
    int TotalCount,
    int Page,
    int PageSize);

public sealed record NotificationTarget(string TargetType, Guid TargetId);

public sealed record UpcomingMeetingRecipient(
    Guid MeetingId,
    string MeetingTitle,
    Guid SessionId,
    string SessionTitle,
    Guid UserId,
    DateTimeOffset PlannedStartsAt);
