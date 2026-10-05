using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Npgsql;
using TrustCabinet.Domain.Entities;
using TrustCabinet.Domain.Enums;
using TrustCabinet.Domain.Meetings;

namespace TrustCabinet.Infrastructure.Meetings;

/// <summary>
/// UC-53 and UC-26 to UC-30. Every change runs under the parent meeting row lock, so a concurrent
/// lifecycle transition or session change cannot interleave, and never touches the meeting itself (BR-92).
/// </summary>
public sealed class MeetingSessionService(
    IMeetingRepository meetings,
    IMeetingSessionRepository sessions,
    ILogger<MeetingSessionService> logger,
    TimeProvider? timeProvider = null) : IMeetingSessionService
{
    private DateTimeOffset UtcNow => (timeProvider ?? TimeProvider.System).GetUtcNow();

    public async Task<MeetingSessionListResult> ListAsync(
        Guid meetingId, Guid actorId, CancellationToken ct = default)
    {
        try
        {
            if (await meetings.FindAccessibleAsync(meetingId, actorId, ct) is null)
            {
                return new MeetingSessionListResult(MeetingSessionOutcome.NotFound);
            }

            return new MeetingSessionListResult(
                MeetingSessionOutcome.Success, await sessions.ListByMeetingAsync(meetingId, ct));
        }
        catch (OperationCanceledException) when (ct.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception exception) when (IsPersistenceFailure(exception))
        {
            logger.LogError(exception, "Could not list sessions of meeting {MeetingId}.", meetingId);
            return new MeetingSessionListResult(MeetingSessionOutcome.ReadFailed);
        }
    }

    public Task<MeetingSessionResult> CreateAsync(
        Guid meetingId, MeetingSessionInput input, Guid actorId, CancellationToken ct = default)
        => PlanAsync(meetingId, actorId, "create a session", async (meeting, now, token) =>
        {
            var mustStartAfter = MeetingSessionRules.RequiresFutureStart(meeting.Status) ? now : (DateTimeOffset?)null;
            if (!MeetingSessionRules.TryValidate(input, mustStartAfter, out var valid, out var errors))
            {
                return MeetingSessionResult.Invalid(errors);
            }

            var session = new MeetingSession
            {
                MeetingId = meeting.Id,
                Title = valid!.Title,
                PlannedStartsAt = valid.PlannedStartsAt,
                PlannedEndsAt = valid.PlannedEndsAt,
                Status = MeetingSessionStatus.Planned,
                CreatedAt = now,
                UpdatedAt = now,
            };
            await sessions.AddAsync(session, token);
            await NotifyAsync(meeting, actorId, $"Phiên “{session.Title}” đã được thêm vào cuộc họp “{meeting.Title}”.", now, token);
            await meetings.SaveChangesAsync(token);
            return MeetingSessionResult.Success(MeetingSessionDetails.From(session));
        }, ct);

    public Task<MeetingSessionResult> UpdateAsync(
        Guid meetingId, Guid sessionId, MeetingSessionInput input, Guid actorId, CancellationToken ct = default)
        => PlanAsync(meetingId, actorId, "update a session", async (meeting, now, token) =>
        {
            var session = await sessions.FindAsync(meetingId, sessionId, token);
            if (session is null)
            {
                return MeetingSessionResult.Failure(MeetingSessionOutcome.NotFound);
            }

            if (session.Status != MeetingSessionStatus.Planned)
            {
                return MeetingSessionResult.Failure(MeetingSessionOutcome.StatusConflict);
            }

            // BR-75 guards rescheduling only: a planned session keeping its start may still be renamed.
            var mustStartAfter = MeetingSessionRules.RequiresFutureStart(meeting.Status) ? now : (DateTimeOffset?)null;
            if (!MeetingSessionRules.TryValidate(
                    input, mustStartAfter, out var valid, out var errors, unchangedStart: session.PlannedStartsAt))
            {
                return MeetingSessionResult.Invalid(errors);
            }

            if (session.Title != valid!.Title
                || session.PlannedStartsAt != valid.PlannedStartsAt
                || session.PlannedEndsAt != valid.PlannedEndsAt)
            {
                session.Title = valid.Title;
                session.PlannedStartsAt = valid.PlannedStartsAt;
                session.PlannedEndsAt = valid.PlannedEndsAt;
                session.UpdatedAt = now;
                await NotifyAsync(meeting, actorId, $"Phiên “{session.Title}” của cuộc họp “{meeting.Title}” đã được cập nhật.", now, token);
                await meetings.SaveChangesAsync(token);
            }

            return MeetingSessionResult.Success(MeetingSessionDetails.From(session));
        }, ct);

    public Task<MeetingSessionResult> CancelAsync(
        Guid meetingId, Guid sessionId, Guid actorId, CancellationToken ct = default)
        => PlanAsync(meetingId, actorId, "cancel a session", async (meeting, now, token) =>
        {
            var session = await sessions.FindAsync(meetingId, sessionId, token);
            if (session is null)
            {
                return MeetingSessionResult.Failure(MeetingSessionOutcome.NotFound);
            }

            if (session.Status != MeetingSessionStatus.Planned)
            {
                return MeetingSessionResult.Failure(MeetingSessionOutcome.StatusConflict);
            }

            // BR-77: the last planned session may be cancelled; the meeting is ended only via UC-44.
            session.Status = MeetingSessionStatus.Cancelled;
            session.UpdatedAt = now;
            await NotifyAsync(meeting, actorId, $"Phiên “{session.Title}” của cuộc họp “{meeting.Title}” đã bị hủy.", now, token);
            await meetings.SaveChangesAsync(token);
            return MeetingSessionResult.Success(MeetingSessionDetails.From(session));
        }, ct);

    public Task<MeetingSessionResult> StartAsync(
        Guid meetingId, Guid sessionId, Guid actorId, CancellationToken ct = default)
        => RunAsync(meetingId, actorId, "start a session", async (meeting, now, token) =>
        {
            var session = await sessions.FindAsync(meetingId, sessionId, token);
            if (session is null)
            {
                return MeetingSessionResult.Failure(MeetingSessionOutcome.NotFound);
            }

            // BR-58 and BR-89: only a planned session, and only while an active Secretary remains.
            if (session.Status != MeetingSessionStatus.Planned || !MeetingReadPolicy.HasActiveSecretary(meeting))
            {
                return MeetingSessionResult.Failure(MeetingSessionOutcome.StatusConflict);
            }

            if (await sessions.HasSessionInProgressAsync(meetingId, token))
            {
                return MeetingSessionResult.Failure(MeetingSessionOutcome.AnotherSessionInProgress);
            }

            // BR-60/BR-95: the meeting and its online room keep their state.
            session.Status = MeetingSessionStatus.InProgress;
            session.ActualStartedAt = MeetingSessionRules.ToStoredPrecision(now);
            session.UpdatedAt = now;
            await meetings.SaveChangesAsync(token);
            return MeetingSessionResult.Success(MeetingSessionDetails.From(session));
        }, ct);

    public Task<MeetingSessionResult> EndAsync(
        Guid meetingId, Guid sessionId, Guid actorId, CancellationToken ct = default)
        => RunAsync(meetingId, actorId, "end a session", async (meeting, now, token) =>
        {
            var session = await sessions.FindAsync(meetingId, sessionId, token);
            if (session is null)
            {
                return MeetingSessionResult.Failure(MeetingSessionOutcome.NotFound);
            }

            if (session.Status != MeetingSessionStatus.InProgress)
            {
                return MeetingSessionResult.Failure(MeetingSessionOutcome.StatusConflict);
            }

            // BR-61: ending the last session leaves the meeting InProgress; the Chair ends it via UC-44.
            session.Status = MeetingSessionStatus.Completed;
            session.ActualEndedAt = MeetingSessionRules.ToStoredPrecision(now);
            session.UpdatedAt = now;
            await meetings.SaveChangesAsync(token);
            return MeetingSessionResult.Success(MeetingSessionDetails.From(session));
        }, ct);

    /// <summary>UC-26 to UC-28: BR-54 allows only the assigned Secretary, while the meeting is open for planning.</summary>
    private Task<MeetingSessionResult> PlanAsync(
        Guid meetingId,
        Guid actorId,
        string action,
        Func<Meeting, DateTimeOffset, CancellationToken, Task<MeetingSessionResult>> apply,
        CancellationToken ct)
        => MutateAsync(
            meetingId,
            actorId,
            action,
            MeetingReadPolicy.IsAssignedSecretary,
            MeetingSessionRules.AllowsPlanning,
            apply,
            ct);

    /// <summary>UC-29/UC-30: BR-63 allows only the assigned Chair, while the meeting is InProgress.</summary>
    private Task<MeetingSessionResult> RunAsync(
        Guid meetingId,
        Guid actorId,
        string action,
        Func<Meeting, DateTimeOffset, CancellationToken, Task<MeetingSessionResult>> apply,
        CancellationToken ct)
        => MutateAsync(
            meetingId,
            actorId,
            action,
            MeetingReadPolicy.IsAssignedChair,
            status => status == MeetingStatus.InProgress,
            apply,
            ct);

    /// <summary>Locks the meeting, then applies BR-29 (404), the actor's role (403) and the meeting status (409).</summary>
    private async Task<MeetingSessionResult> MutateAsync(
        Guid meetingId,
        Guid actorId,
        string action,
        Func<Meeting, Guid, bool> isAllowedActor,
        Func<MeetingStatus, bool> allowsAction,
        Func<Meeting, DateTimeOffset, CancellationToken, Task<MeetingSessionResult>> apply,
        CancellationToken ct)
    {
        try
        {
            return await meetings.InTransactionAsync(async token =>
            {
                var meeting = await meetings.FindWorkflowAsync(meetingId, forUpdate: true, token);
                if (meeting is null || !MeetingReadPolicy.CanRead(meeting, actorId))
                {
                    return MeetingSessionResult.Failure(MeetingSessionOutcome.NotFound);
                }

                if (!isAllowedActor(meeting, actorId))
                {
                    return MeetingSessionResult.Failure(MeetingSessionOutcome.Forbidden);
                }

                if (!allowsAction(meeting.Status))
                {
                    return MeetingSessionResult.Failure(MeetingSessionOutcome.StatusConflict);
                }

                return await apply(meeting, UtcNow, token);
            }, ct);
        }
        catch (OperationCanceledException) when (ct.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception exception) when (IsPersistenceFailure(exception))
        {
            logger.LogError(exception, "Could not {Action} for meeting {MeetingId}.", action, meetingId);
            return MeetingSessionResult.Failure(MeetingSessionOutcome.SaveFailed);
        }
    }

    /// <summary>BR-76: after publication, tell the other invited participants about the schedule change.</summary>
    private Task NotifyAsync(Meeting meeting, Guid actorId, string body, DateTimeOffset now, CancellationToken ct)
    {
        if (!MeetingSessionRules.RequiresFutureStart(meeting.Status))
        {
            return Task.CompletedTask;
        }

        var notifications = MeetingInvitationRecipientPolicy.SelectSnapshot(meeting.Participants)
            .Where(participant => participant.UserId != actorId)
            .Select(participant => new Notification
            {
                UserId = participant.UserId,
                MeetingId = meeting.Id,
                TargetType = "Meeting",
                TargetId = meeting.Id,
                Type = NotificationType.MeetingSessionChanged,
                Title = "Lịch cuộc họp thay đổi",
                Body = body,
                CreatedAt = now,
                UpdatedAt = now,
            })
            .ToArray();
        return notifications.Length == 0
            ? Task.CompletedTask
            : meetings.AddNotificationsAsync(notifications, ct);
    }

    private static bool IsPersistenceFailure(Exception exception)
        => exception is DbUpdateException or NpgsqlException or TimeoutException;
}
