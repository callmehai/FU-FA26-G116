using System.Globalization;
using TrustCabinet.Domain.Entities;
using TrustCabinet.Domain.Enums;

namespace TrustCabinet.Infrastructure.CalendarSharing;

/// <summary>
/// The in-app notification of each calendar-share step (BR-426). Only names, the level and the dates — never anything
/// about a meeting (BR-427). Saved in the step's transaction; <c>NotificationWorker</c> pushes it once committed.
/// </summary>
internal static class CalendarShareNotifications
{
    internal const string TargetType = "CalendarShare";

    /// <summary>The level as the Chia sẻ lịch screen names it (frontend <c>shareFormat.ts</c>), so a notice and the screen agree.</summary>
    internal static string LevelName(CalendarShareLevel level)
        => level == CalendarShareLevel.Level1 ? "Họp Thường" : "Thường và Nội bộ";

    internal static string Day(DateOnly date) => date.ToString("dd/MM/yyyy", CultureInfo.InvariantCulture);

    private static string Range(CalendarShare share) => $"{Day(share.StartDate)}–{Day(share.EndDate)}";

    internal static Notification Requested(CalendarShare share, string viewerName, DateTimeOffset now)
        => Create(share.OwnerId, share, NotificationType.CalendarShareRequested, "Yêu cầu xem lịch",
            $"{viewerName} xin xem lịch bận/rảnh của bạn ({LevelName(share.Level)}, {Range(share)}).", now);

    internal static Notification Received(CalendarShare share, string ownerName, DateTimeOffset now)
        => Create(share.ViewerId, share, NotificationType.CalendarShareReceived, "Được chia sẻ lịch",
            $"{ownerName} chia sẻ lịch bận/rảnh với bạn ({LevelName(share.Level)}, {Range(share)}). Hãy đồng ý hoặc từ chối.", now);

    internal static Notification Accepted(CalendarShare share, Guid senderId, string answererName, DateTimeOffset now)
        => Create(senderId, share, NotificationType.CalendarShareAccepted, "Chia sẻ lịch đã được chấp nhận",
            $"{answererName} đã chấp nhận chia sẻ lịch ({LevelName(share.Level)}, {Range(share)}).", now);

    internal static Notification Declined(CalendarShare share, Guid senderId, string answererName, DateTimeOffset now)
        => Create(senderId, share, NotificationType.CalendarShareDeclined, "Chia sẻ lịch bị từ chối",
            $"{answererName} đã từ chối chia sẻ lịch.", now);

    internal static Notification ChangeRequested(CalendarShare share, string viewerName, DateTimeOffset now)
        => Create(share.OwnerId, share, NotificationType.CalendarShareChangeRequested, "Yêu cầu thay đổi chia sẻ lịch",
            $"{viewerName} xin đổi chia sẻ lịch thành {LevelName(share.PendingLevel!.Value)}, đến {Day(share.PendingEndDate!.Value)}.",
            now);

    internal static Notification Updated(CalendarShare share, string ownerName, DateTimeOffset now)
        => Create(share.ViewerId, share, NotificationType.CalendarShareUpdated, "Chia sẻ lịch đã thay đổi",
            $"{ownerName} đã cập nhật chia sẻ lịch: {LevelName(share.Level)}, {Range(share)}.", now);

    internal static Notification ChangeDeclined(CalendarShare share, string ownerName, DateTimeOffset now)
        => Create(share.ViewerId, share, NotificationType.CalendarShareChangeDeclined, "Thay đổi chia sẻ lịch bị từ chối",
            $"{ownerName} đã từ chối thay đổi chia sẻ lịch. Chia sẻ giữ như cũ.", now);

    internal static Notification Withdrawn(CalendarShare share, Guid recipientId, string actorName, DateTimeOffset now)
        => Create(recipientId, share, NotificationType.CalendarShareWithdrawn, "Yêu cầu chia sẻ lịch đã được rút",
            $"{actorName} đã rút yêu cầu chia sẻ lịch.", now);

    internal static Notification Ended(CalendarShare share, Guid recipientId, string actorName, DateTimeOffset now)
        => Create(recipientId, share, NotificationType.CalendarShareEnded, "Chia sẻ lịch đã ngừng",
            $"{actorName} đã ngừng chia sẻ lịch.", now);

    /// <summary>BR-424/BR-426: a stop tells both sides, once per share.</summary>
    internal static IEnumerable<Notification> Stopped(CalendarShare share, string ownerName, string viewerName,
        DateTimeOffset now)
        => new[] { share.OwnerId, share.ViewerId }.Select(userId => Create(userId, share, NotificationType.CalendarShareStopped,
            "Chia sẻ lịch đã dừng",
            $"Chia sẻ lịch giữa {ownerName} và {viewerName} đã dừng vì người xem không còn quyền tạo cuộc họp hoặc tài khoản bị vô hiệu hoá.",
            now, $"calendar-share-stopped:{share.Id:D}:{userId:D}"));


    private static Notification Create(Guid userId, CalendarShare share, NotificationType type, string title, string body,
        DateTimeOffset now, string? deduplicationKey = null)
        => new()
        {
            Id = Guid.CreateVersion7(),
            UserId = userId,
            Type = type,
            Title = title,
            Body = body.Length <= 2000 ? body : body[..(char.IsHighSurrogate(body[1999]) ? 1999 : 2000)],
            TargetType = TargetType,
            TargetId = share.Id,
            DeduplicationKey = deduplicationKey,
            CreatedAt = now,
            UpdatedAt = now,
        };
}
