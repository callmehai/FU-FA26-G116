using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Npgsql;
using TrustCabinet.Domain.Entities;
using TrustCabinet.Domain.Enums;
using TrustCabinet.Domain.Meetings;

namespace TrustCabinet.Infrastructure.Meetings;

public sealed class MeetingOverdueOptions
{
    public const string SectionName = "Meetings";

    /// <summary>
    /// Hours after the planned start before an unstarted meeting is cancelled (BR-301; PO 27/10/2026: 24, counted from
    /// the start, not the end).
    /// </summary>
    public int AutoCancelAfterHours { get; set; } = 24;
}

/// <summary>
/// One meeting per transaction, each re-checked under its row lock: an organizer who reschedules or starts the meeting
/// at the same moment wins, and a failure on one meeting leaves the others to the next sweep.
/// </summary>
public sealed class MeetingOverdueService(
    IMeetingRepository meetings,
    IOptions<MeetingOverdueOptions> options,
    TimeProvider clock,
    ILogger<MeetingOverdueService> logger) : IMeetingOverdueService
{
    /// <summary>Shown on the meeting and in its history; the actor is the system (BR-301).</summary>
    internal static string Reason(int hours) => $"Không diễn ra theo lịch: quá {hours} giờ sau giờ bắt đầu dự kiến mà chưa bắt đầu.";

    private const int BatchSize = 50;

    public async Task<int> CancelOverdueAsync(CancellationToken ct = default)
    {
        var delay = TimeSpan.FromHours(options.Value.AutoCancelAfterHours);
        var cutoff = clock.GetUtcNow() - delay;
        var cancelled = 0;
        foreach (var id in await meetings.ListOverdueAsync(cutoff, BatchSize, ct))
        {
            try
            {
                if (await CancelAsync(id, delay, ct)) cancelled++;
            }
            catch (Exception error) when (error is DbUpdateException or NpgsqlException or TimeoutException)
            {
                logger.LogError(error, "Could not cancel overdue meeting {MeetingId}.", id);
            }
        }

        return cancelled;
    }

    private Task<bool> CancelAsync(Guid id, TimeSpan delay, CancellationToken ct)
        => meetings.InTransactionAsync(async token =>
        {
            var meeting = await meetings.FindWorkflowAsync(id, forUpdate: true, token);
            var now = clock.GetUtcNow();
            if (meeting is null || !MeetingRules.IsCancellable(meeting.Status) || meeting.PlannedStartTime > now - delay)
            {
                return false;
            }

            var fromStatus = meeting.Status;
            var reason = Reason(options.Value.AutoCancelAfterHours);
            meeting.Status = MeetingStatus.Cancelled;
            meeting.CancelledAt = now;
            meeting.CancelledById = null;
            meeting.CancellationReason = reason;
            meeting.CancelledFromStatus = fromStatus;
            meeting.UpdatedAt = now;
            var history = new MeetingStatusHistory
            {
                Id = Guid.CreateVersion7(),
                MeetingId = meeting.Id,
                FromStatus = fromStatus,
                ToStatus = MeetingStatus.Cancelled,
                Action = MeetingHistoryAction.AutoCancel,
                ActorId = null,
                Reason = reason,
                OccurredAt = now,
                CreatedAt = now,
                UpdatedAt = now,
            };
            meeting.StatusHistory.Add(history);
            await meetings.AddHistoryAsync(history, token);

            // Only the organizer is told: the time has already passed, so a notice to every invitee would arrive
            // about a meeting they already know did not happen.
            await meetings.AddNotificationsAsync(
            [new Notification
            {
                Id = Guid.CreateVersion7(),
                UserId = meeting.CreatedById,
                MeetingId = meeting.Id,
                TargetType = "Meeting",
                TargetId = meeting.Id,
                OriginHistoryId = history.Id,
                Type = NotificationType.MeetingCancelled,
                Title = "Cuộc họp đã tự huỷ",
                Body = Limit($"Cuộc họp “{meeting.Title}” đã tự huỷ. {reason}"),
                CreatedAt = now,
                UpdatedAt = now,
            }], token);
            await meetings.SaveChangesAsync(token);
            logger.LogInformation("Cancelled overdue meeting {MeetingId} (was {Status}).", meeting.Id, fromStatus);
            return true;
        }, ct);

    private static string Limit(string value)
        => value.Length <= 2000 ? value : value[..(char.IsHighSurrogate(value[1999]) ? 1999 : 2000)];
}
