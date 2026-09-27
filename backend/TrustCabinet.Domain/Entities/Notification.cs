using TrustCabinet.Domain.Entities.Common;
using TrustCabinet.Domain.Enums;

namespace TrustCabinet.Domain.Entities;

/// <summary>
/// One in-app notification for one person (SCRUM-14). Pushed over SignalR when created, and read
/// back on sign-in for whatever arrived while they were away.
/// </summary>
public class Notification : Entity
{
    public Guid UserId { get; set; }
    public User User { get; set; } = null!;

    public NotificationType Type { get; set; }

    public required string Title { get; set; }

    public string Body { get; set; } = string.Empty;

    /// <summary>The meeting it is about, so clicking the notification opens the right screen.</summary>
    public Guid? MeetingId { get; set; }
    public Meeting? Meeting { get; set; }

    /// <summary>The lifecycle history row that produced this notification, when applicable.</summary>
    public Guid? OriginHistoryId { get; set; }

    /// <summary>Null until read. The unread count is the number of rows where this is null.</summary>
    public DateTimeOffset? ReadAt { get; set; }
}
