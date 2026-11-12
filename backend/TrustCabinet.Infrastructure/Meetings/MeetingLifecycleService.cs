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
using TrustCabinet.Domain.Speaking;
using TrustCabinet.Domain.Votes;

namespace TrustCabinet.Infrastructure.Meetings;

/// <summary>
/// Serializes Chair lifecycle commands with roster, scheduling and agenda mutations, and ends meetings left running as the
/// system (ADR-0062).
/// </summary>
public sealed class MeetingLifecycleService(
    IMeetingRepository meetings,
    IAgendaMeetingEnd agendaEnd,
    ISpeakingMeetingEnd speakingEnd,
    IAgendaChangePublisher publisher,
    IVoteMeetingEnd voteEnd,
    IVoteChangePublisher votePublisher,
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
        EndEffects? end = null;
        Guid[] recipients = [];
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
                    // The Chair just started it, so only the others are told (as for Publish).
                    recipients = MeetingInvitationRecipientPolicy.SelectSnapshot(meeting.Participants)
                        .Where(p => p.UserId != actorId)
                        .Select(p => p.UserId).ToArray();
                }
                else
                {
                    end = await PrepareEndAsync(meeting, actorId, now, token);
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
            await AfterCommitAsync("notification", meetingId, record.Id, NotificationBudget, token =>
                notifications.QueueMeetingStartedAsync(meetingId, result.Meeting!.Title,
                    record.Id, record.OccurredAt, recipients, token));
        }
        else
        {
            await AfterEndAsync(meetingId, record.Id, end!);
        }
        return result;
    }

    public async Task<bool> EndOverrunAsync(Guid meetingId, DateTimeOffset cutoff, string reason, CancellationToken ct = default)
    {
        EndEffects? end = null;
        var historyId = Guid.Empty;
        try
        {
            var ended = await meetings.InTransactionAsync(async token =>
            {
                var meeting = await meetings.FindWorkflowAsync(meetingId, forUpdate: true, token);
                if (meeting is null || !MeetingRules.IsOverrun(meeting, cutoff)) return false;

                var now = AgendaExecution.UtcMicroseconds(clock.GetUtcNow());
                end = await PrepareEndAsync(meeting, null, now, token);
                meeting.Status = MeetingStatus.Ended;
                meeting.UpdatedAt = now;
                var history = new MeetingStatusHistory
                {
                    Id = Guid.CreateVersion7(), MeetingId = meetingId, ActorId = null,
                    Action = MeetingHistoryAction.AutoEnd, FromStatus = MeetingStatus.InProgress, ToStatus = MeetingStatus.Ended,
                    Reason = reason, OccurredAt = now, CreatedAt = now, UpdatedAt = now,
                };
                meeting.StatusHistory.Add(history);
                await meetings.AddHistoryAsync(history, token);
                await meetings.SaveChangesAsync(token);
                historyId = history.Id;
                return true;
            }, ct);
            if (!ended) return false;
        }
        catch (OperationCanceledException) when (ct.IsCancellationRequested) { throw; }
        catch (Exception error) when (error is DbUpdateException or NpgsqlException or TimeoutException or InvalidAgendaExecutionException)
        {
            logger.LogError(error, "Could not end overrunning meeting {MeetingId}.", meetingId);
            return false;
        }

        logger.LogInformation("Ended meeting {MeetingId} left running past its planned end.", meetingId);
        await AfterEndAsync(meetingId, historyId, end!);
        return true;
    }

    /// <summary>What ending does inside the meeting's transaction; the Chair's End and the system's share it.</summary>
    private sealed record EndEffects(AgendaChange Change, IReadOnlyList<VoteChange> DeletedDrafts, bool CloseRoom);

    private async Task<EndEffects> PrepareEndAsync(Meeting meeting, Guid? actorId, DateTimeOffset now, CancellationToken token)
    {
        var change = await agendaEnd.PrepareAsync(meeting.Id, now, token)
            ?? new AgendaChange(Guid.CreateVersion7(), meeting.Id, "MeetingEnded", null, null, now);
        // Readers refetch the queue on "MeetingEnded", so no speaking event of its own is needed.
        await speakingEnd.PrepareAsync(meeting.Id, now, token);
        meeting.ActualEndedAt = now;
        // A draft can only be opened while InProgress (BR-160), so none survives the end (PO 08/11/2026).
        var deletedDrafts = await voteEnd.DeleteDraftsAsync(meeting.Id, actorId, now, token);
        return new(change, deletedDrafts, meeting.Mode is MeetingMode.Online or MeetingMode.Hybrid);
    }

    private async Task AfterEndAsync(Guid meetingId, Guid historyId, EndEffects end)
    {
        await AfterCommitAsync("agenda event", meetingId, historyId, EffectBudget,
            token => publisher.PublishAsync(end.Change, token));
        // Drafts were seen by the Chair and Secretary only (BR-253), so only they are told.
        foreach (var deleted in end.DeletedDrafts)
            await AfterCommitAsync("vote draft deletion", meetingId, historyId, EffectBudget,
                token => votePublisher.PublishAsync(deleted, managersOnly: true, token));
        if (end.CloseRoom)
            await AfterCommitAsync("room closure", meetingId, historyId, EffectBudget,
                token => rooms.CloseMeetingRoomAsync(meetingId, token));
    }

    /// <summary>A hub push or a LiveKit call: a stalled one must not hold the Chair's response for long.</summary>
    private static readonly TimeSpan EffectBudget = TimeSpan.FromSeconds(2);

    /// <summary>
    /// One insert into our own database. Under load (twenty Start requests released at once on a CI runner) it took
    /// past two seconds and was cancelled, so the attendees silently got no "Cuộc họp đã bắt đầu"; it gets more room.
    /// </summary>
    private static readonly TimeSpan NotificationBudget = TimeSpan.FromSeconds(10);

    private async Task AfterCommitAsync(string effect, Guid meetingId, Guid historyId, TimeSpan budget,
        Func<CancellationToken, Task> operation)
    {
        using var timeout = new CancellationTokenSource(budget);
        try { await operation(timeout.Token); }
        catch (Exception error)
        {
            logger.LogWarning(error, "Post-commit {Effect} failed for meeting {MeetingId}, history {HistoryId}.",
                effect, meetingId, historyId);
        }
    }
}
