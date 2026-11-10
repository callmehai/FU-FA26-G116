using TrustCabinet.Domain.Audit;
using TrustCabinet.Domain.Entities;
using TrustCabinet.Domain.Enums;

namespace TrustCabinet.Infrastructure.CalendarSharing;

/// <summary>
/// The ways the system closes a share without anyone asking for it. The caller owns the transaction;
/// <see cref="IAuditService"/> saves, so the share, its notifications and the audit row are written together.
/// </summary>
public sealed class CalendarShareCloser(TrustCabinetDbContext db, IAuditService audit)
{
    /// <summary>BR-424: an Effective share stops for good; both sides are told (BR-426).</summary>
    public async Task StopAsync(CalendarShare share, string ownerName, string viewerName, Guid? actorId,
        CalendarShareStopReason reason, DateTimeOffset now, CancellationToken ct)
    {
        Close(share, CalendarShareStatus.Stopped, actorId, now);
        db.Notifications.AddRange(CalendarShareNotifications.Stopped(share, ownerName, viewerName, now));
        await audit.RecordAsync(AuditActions.CalendarShareStopped, AuditActions.CalendarShareObjectType, share.Id, actorId,
            new
            {
                share.OwnerId, share.ViewerId, Level = share.Level.ToString(), share.StartDate, share.EndDate,
                Reason = reason == CalendarShareStopReason.AccountDeactivated ? "accountDeactivated" : "viewerLostMeetingCreator",
            }, ct);
    }

    /// <summary>
    /// BR-425: a share past its end date has already ended, but its row still holds the pair's one open place (BR-423).
    /// A new share of the pair closes it first — as Ended, by nobody, without a notification (PO 09/11/2026, Q-B: no
    /// state for it, nobody told). The audit row keeps it traceable.
    /// </summary>
    public async Task CloseAfterEndDateAsync(CalendarShare share, DateTimeOffset now, CancellationToken ct)
    {
        var status = share.Status;
        Close(share, CalendarShareStatus.Ended, null, now);
        await audit.RecordAsync(AuditActions.CalendarShareClosedAfterEndDate, AuditActions.CalendarShareObjectType,
            share.Id, null, new
            {
                share.OwnerId, share.ViewerId, Level = share.Level.ToString(), share.StartDate, share.EndDate,
                Was = status.ToString(),
            }, ct);
    }

    internal static void Close(CalendarShare share, CalendarShareStatus status, Guid? actorId, DateTimeOffset now)
    {
        share.Status = status;
        share.ClosedAt = now;
        share.ClosedById = actorId;
        share.ClearPendingChange();
        share.UpdatedAt = now;
    }
}

internal static class CalendarShareAudit
{
    /// <summary>What every share audit row carries: the two people, the level and the range (never a meeting).</summary>
    internal static object Details(CalendarShare share) => new
    {
        share.OwnerId, share.ViewerId, Level = share.Level.ToString(), share.StartDate, share.EndDate,
    };

    internal static object Snapshot(CalendarShare share) => new
    {
        Level = share.Level.ToString(), share.StartDate, share.EndDate,
    };
}
