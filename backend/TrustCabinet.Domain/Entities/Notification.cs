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

    /// <summary>The kind and identifier of the resource opened from this notification.</summary>
    public string? TargetType { get; set; }
    public Guid? TargetId { get; set; }

    /// <summary>Null until read. The unread count is the number of rows where this is null.</summary>
    public DateTimeOffset? ReadAt { get; set; }

    public bool IsRead => ReadAt is not null;

    public void MarkAsRead(DateTimeOffset now)
    {
        if (IsRead)
        {
            return;
        }

        ReadAt = now;
        UpdatedAt = now;
    }

    /// <summary>Stable event key so retries never create the same notification twice.</summary>
    public string? DeduplicationKey { get; set; }

    /// <summary>Null until the realtime dispatcher has attempted to push this persisted row.</summary>
    public DateTimeOffset? PushedAt { get; set; }
}
