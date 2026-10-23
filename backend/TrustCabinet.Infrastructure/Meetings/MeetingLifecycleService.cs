using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Npgsql;
using TrustCabinet.Domain.Agenda;
using TrustCabinet.Domain.Calls;
using TrustCabinet.Domain.Entities;
using TrustCabinet.Domain.Enums;
using TrustCabinet.Domain.Meetings;
using TrustCabinet.Domain.Notifications;
using TrustCabinet.Domain.Participants;

namespace TrustCabinet.Infrastructure.Meetings;

/// <summary>Serializes Chair lifecycle commands with roster, scheduling and agenda mutations.</summary>
public sealed class MeetingLifecycleService(
    IMeetingRepository meetings,
    IAgendaMeetingEnd agendaEnd,
    IAgendaChangePublisher publisher,
    INotificationService notifications,
    ICallRoomService rooms,
    TimeProvider clock,
    ILogger<MeetingLifecycleService> logger) : IMeetingLifecycleService
{
    public async Task<MeetingWorkflowResult> TransitionAsync(
        Guid meetingId, MeetingTransitionCommand command, Guid actorId, CancellationToken ct = default)
    {
        if (command.Action is not (MeetingTransitionAction.Start or MeetingTransitionAction.End)
            || command.Reason is not null || command.AssignedApproverId is not null || command.OrganizerId is not null)
            return MeetingWorkflowResult.Failure(MeetingWorkflowOutcome.InvalidRequest);

        var starting = command.Action == MeetingTransitionAction.Start;
        AgendaChange? change = null;
        Guid[] recipients = [];
        var closeRoom = false;
        MeetingWorkflowResult result;
        try
        {
            result = await meetings.InTransactionAsync(async token =>
            {
                var meeting = await meetings.FindWorkflowAsync(meetingId, forUpdate: true, token);
                if (meeting is null || !MeetingReadPolicy.CanRead(meeting, actorId))
                    return MeetingWorkflowResult.Failure(MeetingWorkflowOutcome.NotFound);

                var active = meeting.Participants.Where(ParticipantRules.Active).ToArray();
                if (!active.Any(p => p.UserId == actorId && p.Role == MeetingRole.Chair))
                    return MeetingWorkflowResult.Failure(MeetingWorkflowOutcome.Forbidden);

                var from = starting ? MeetingStatus.Published : MeetingStatus.InProgress;
                if (meeting.Status != from || (starting &&
                    (active.Count(p => p.Role == MeetingRole.Chair) != 1 || !active.Any(p => p.Role == MeetingRole.Secretary))))
                    return MeetingWorkflowResult.Failure(MeetingWorkflowOutcome.StatusConflict);

                var now = AgendaExecution.UtcMicroseconds(clock.GetUtcNow());
                if (starting)
                {
                    meeting.ActualStartedAt = now;
                    recipients = MeetingInvitationRecipientPolicy.SelectSnapshot(meeting.Participants)
                        .Select(p => p.UserId).ToArray();
                }
                else
                {
                    change = await agendaEnd.PrepareAsync(meetingId, now, token)
                        ?? new AgendaChange(Guid.CreateVersion7(), meetingId, "MeetingEnded", null, null, now);
                    meeting.ActualEndedAt = now;
                    closeRoom = meeting.Mode is MeetingMode.Online or MeetingMode.Hybrid;
                }

                meeting.Status = starting ? MeetingStatus.InProgress : MeetingStatus.Ended;
                meeting.UpdatedAt = now;
                var history = new MeetingStatusHistory
                {
                    Id = Guid.CreateVersion7(), MeetingId = meetingId, ActorId = actorId,
                    Action = starting ? MeetingHistoryAction.Start : MeetingHistoryAction.End,
                    FromStatus = from, ToStatus = meeting.Status, Reason = string.Empty,
                    OccurredAt = now, CreatedAt = now, UpdatedAt = now,
                };
                meeting.StatusHistory.Add(history);
                await meetings.AddHistoryAsync(history, token);
                await meetings.SaveChangesAsync(token);
                var actorName = active.First(p => p.UserId == actorId && p.Role == MeetingRole.Chair).User.FullName;
                return MeetingWorkflowResult.Success(MeetingDetails.From(meeting, actorId),
                    new MeetingHistoryRecord(history.Id, meetingId, history.Action, from, meeting.Status,
                        actorId, actorName, now, null, null));
            }, ct);
        }
        catch (OperationCanceledException) when (ct.IsCancellationRequested) { throw; }
        catch (Exception error) when (error is DbUpdateException or NpgsqlException or TimeoutException or InvalidAgendaExecutionException)
        {
            logger.LogError(error, "Could not perform {Action} for meeting {MeetingId}.", command.Action, meetingId);
            return MeetingWorkflowResult.Failure(MeetingWorkflowOutcome.SaveFailed);
        }

        // Commit is complete. Each awaited effect gets its own budget, independent of RequestAborted.
        // Effect failures are logged without changing the committed transition result.
        if (!result.Succeeded) return result;
        var record = result.History!;
        if (starting)
        {
            await AfterCommitAsync("notification", meetingId, record.Id, token =>
                notifications.QueueMeetingStartedAsync(meetingId, result.Meeting!.Title,
                    record.Id, record.OccurredAt, recipients, token));
        }
        else
        {
            await AfterCommitAsync("agenda event", meetingId, record.Id, token => publisher.PublishAsync(change!, token));
            if (closeRoom)
                await AfterCommitAsync("room closure", meetingId, record.Id,
                    token => rooms.CloseMeetingRoomAsync(meetingId, token));
        }
        return result;
    }

    private async Task AfterCommitAsync(string effect, Guid meetingId, Guid historyId, Func<CancellationToken, Task> operation)
    {
        using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(2));
        try { await operation(timeout.Token); }
        catch (Exception error)
        {
            logger.LogWarning(error, "Post-commit {Effect} failed for meeting {MeetingId}, history {HistoryId}.",
                effect, meetingId, historyId);
        }
    }
}
