using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Npgsql;
using System.Text.Json;
using TrustCabinet.Domain.Access;
using TrustCabinet.Domain.Entities;
using TrustCabinet.Domain.Enums;
using TrustCabinet.Domain.Meetings;

namespace TrustCabinet.Infrastructure.Meetings;

/// <summary>Meeting rules and persistence orchestration across creation, listing and lifecycle workflows.</summary>
public sealed class MeetingService(
    IMeetingRepository meetings,
    ILogger<MeetingService> logger,
    TimeProvider? timeProvider = null) : IMeetingService
{
    private DateTimeOffset UtcNow => (timeProvider ?? TimeProvider.System).GetUtcNow();

    public async Task<MeetingResult> CreateAsync(
        MeetingInput input, Guid actorId, CancellationToken ct = default)
    {
        if (!MeetingRules.TryValidate(input, out var valid, out var errors))
        {
            return MeetingResult.Invalid(errors);
        }

        var now = UtcNow;
        var meeting = new Meeting
        {
            Title = valid!.Title,
            StartsAt = valid.StartsAt,
            EndsAt = valid.EndsAt,
            Mode = valid.Mode,
            Location = valid.Location,
            Sensitivity = valid.Sensitivity,
            Description = valid.Description,
            Status = MeetingStatus.Draft,
            CreatedById = actorId,
            AssignedApproverId = null,
            CreatedAt = now,
            UpdatedAt = now,
        };

        meeting.StatusHistory.Add(new MeetingStatusHistory
        {
            Meeting = meeting,
            FromStatus = null,
            ToStatus = MeetingStatus.Draft,
            ActorId = actorId,
            Action = MeetingHistoryAction.Create,
            Reason = string.Empty,
            OccurredAt = now,
            CreatedAt = now,
            UpdatedAt = now,
        });

        try
        {
            await meetings.AddAsync(meeting, ct);
            await meetings.SaveChangesAsync(ct);
            return MeetingResult.Success(MeetingDetails.From(meeting));
        }
        catch (OperationCanceledException) when (ct.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception exception) when (IsPersistenceFailure(exception))
        {
            logger.LogError(exception, "Could not create a meeting for actor {ActorId}.", actorId);
            return MeetingResult.Failure(MeetingOutcome.SaveFailed);
        }
    }

    public async Task<MeetingResult> GetAsync(
        Guid id, Guid actorId, CancellationToken ct = default)
    {
        try
        {
            var meeting = await meetings.FindAccessibleAsync(id, actorId, ct);
            return meeting is null
                ? MeetingResult.Failure(MeetingOutcome.NotFound)
                : MeetingResult.Success(MeetingDetails.From(meeting));
        }
        catch (OperationCanceledException) when (ct.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception exception) when (IsPersistenceFailure(exception))
        {
            logger.LogError(exception, "Could not read meeting {MeetingId} for actor {ActorId}.", id, actorId);
            return MeetingResult.Failure(MeetingOutcome.ReadFailed);
        }
    }

    public async Task<MeetingListResult> ListAsync(
        MeetingListQuery query, Guid actorId, CancellationToken ct = default)
    {
        try
        {
            return MeetingListResult.Success(await meetings.FindAuthorizedPageAsync(actorId, query, ct));
        }
        catch (OperationCanceledException) when (ct.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception exception) when (IsPersistenceFailure(exception))
        {
            logger.LogError(exception, "Could not list meetings for actor {ActorId}.", actorId);
            return MeetingListResult.ReadFailed();
        }
    }

    public async Task<MeetingApproverListResult> ListEligibleApproversAsync(
        CancellationToken ct = default)
    {
        try
        {
            var approvers = await meetings.ListEligibleApproversAsync(ct);
            return MeetingApproverListResult.Success(approvers);
        }
        catch (OperationCanceledException) when (ct.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception exception) when (IsPersistenceFailure(exception))
        {
            logger.LogError(exception, "Could not list eligible meeting approvers.");
            return MeetingApproverListResult.ReadFailed();
        }
    }

    public async Task<MeetingResult> CheckUpdateAsync(
        Guid id, Guid actorId, CancellationToken ct = default)
    {
        try
        {
            var meeting = await meetings.FindOwnedAsync(id, actorId, ct);
            if (meeting is null)
            {
                return MeetingResult.Failure(MeetingOutcome.NotFound);
            }

            return MeetingRules.IsEditable(meeting.Status)
                ? MeetingResult.Success(MeetingDetails.From(meeting))
                : MeetingResult.Failure(MeetingOutcome.NotEditable);
        }
        catch (OperationCanceledException) when (ct.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception exception) when (IsPersistenceFailure(exception))
        {
            logger.LogError(exception, "Could not check whether meeting {MeetingId} can be updated by actor {ActorId}.", id, actorId);
            return MeetingResult.Failure(MeetingOutcome.ReadFailed);
        }
    }

    public Task<MeetingResult> UpdateAsync(
        Guid id, MeetingInput input, Guid actorId, CancellationToken ct = default)
        => UpdateAsync(id, _ => Task.FromResult(input), actorId, ct);

    public async Task<MeetingResult> UpdateAsync(
        Guid id,
        Func<CancellationToken, Task<MeetingInput>> inputFactory,
        Guid actorId,
        CancellationToken ct = default)
    {
        try
        {
            return await meetings.InTransactionAsync(async token =>
            {
                var meeting = await meetings.FindOwnedForUpdateAsync(id, actorId, token);
                if (meeting is null)
                {
                    return MeetingResult.Failure(MeetingOutcome.NotFound);
                }

                // Match UC-12: check the current, locked state before validating submitted fields.
                if (!MeetingRules.IsEditable(meeting.Status))
                {
                    return MeetingResult.Failure(MeetingOutcome.NotEditable);
                }

                var input = await inputFactory(token);
                if (!MeetingRules.TryValidate(input, out var valid, out var errors))
                {
                    return MeetingResult.Invalid(errors);
                }

                var normalized = valid!;
                if (HasChanges(meeting, normalized))
                {
                    meeting.Title = normalized.Title;
                    meeting.StartsAt = normalized.StartsAt;
                    meeting.EndsAt = normalized.EndsAt;
                    meeting.Mode = normalized.Mode;
                    meeting.Location = normalized.Location;
                    meeting.Sensitivity = normalized.Sensitivity;
                    meeting.Description = normalized.Description;
                    if (meeting.Status == MeetingStatus.Draft)
                    {
                        meeting.AssignedApproverId = null;
                    }

                    meeting.UpdatedAt = UtcNow;
                    await meetings.SaveChangesAsync(token);
                }

                return MeetingResult.Success(MeetingDetails.From(meeting));
            }, ct);
        }
        catch (OperationCanceledException) when (ct.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception exception) when (IsPersistenceFailure(exception))
        {
            logger.LogError(exception, "Could not update meeting {MeetingId} for actor {ActorId}.", id, actorId);
            return MeetingResult.Failure(MeetingOutcome.SaveFailed);
        }
    }

    private static bool HasChanges(Meeting meeting, ValidatedMeetingInput input)
        => !string.Equals(meeting.Title, input.Title, StringComparison.Ordinal)
            || meeting.StartsAt != input.StartsAt
            || meeting.EndsAt != input.EndsAt
            || meeting.Mode != input.Mode
            || !string.Equals(meeting.Location, input.Location, StringComparison.Ordinal)
            || meeting.Sensitivity != input.Sensitivity
            || !string.Equals(meeting.Description, input.Description, StringComparison.Ordinal)
            || meeting.AssignedApproverId is not null;

    private static bool IsPersistenceFailure(Exception exception)
        => exception is DbUpdateException or NpgsqlException or TimeoutException;

    public async Task<MeetingSubmissionReadiness> GetSubmissionReadinessAsync(
        Guid id, Guid actorId, CancellationToken ct = default)
    {
        try
        {
            var meeting = await meetings.FindWorkflowAsync(id, ct: ct);
            if (meeting is null)
            {
                return ReadinessFailure(MeetingWorkflowOutcome.NotFound, id);
            }

            if (!MeetingReadPolicy.CanRead(meeting, actorId))
            {
                return ReadinessFailure(MeetingWorkflowOutcome.NotFound, id);
            }

            if (meeting.CreatedById != actorId
                || !await meetings.IsActiveUserWithCapabilityAsync(actorId, Capability.MeetingCreator, ct))
            {
                return ReadinessFailure(MeetingWorkflowOutcome.Forbidden, id, meeting.Status);
            }

            if (meeting.Status != MeetingStatus.Draft)
            {
                return ReadinessFailure(MeetingWorkflowOutcome.StatusConflict, id, meeting.Status);
            }

            var errors = MeetingSubmissionReadinessRules.Validate(meeting);
            return new MeetingSubmissionReadiness(
                MeetingWorkflowOutcome.Success, id, meeting.Status, errors);
        }
        catch (OperationCanceledException) when (ct.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception exception) when (IsPersistenceFailure(exception))
        {
            logger.LogError(exception, "Could not read submission readiness for meeting {MeetingId}.", id);
            return ReadinessFailure(MeetingWorkflowOutcome.ReadFailed, id);
        }
    }

    public async Task<MeetingWorkflowResult> TransitionAsync(
        Guid id, MeetingTransitionCommand command, Guid actorId, CancellationToken ct = default)
    {
        if (!IsCommandShapeValid(command))
        {
            return MeetingWorkflowResult.Failure(MeetingWorkflowOutcome.InvalidRequest);
        }

        if (command.Action == MeetingTransitionAction.Reject)
        {
            var reason = command.Reason?.Trim() ?? string.Empty;
            if (reason.Length == 0)
            {
                return MeetingWorkflowResult.Failure(MeetingWorkflowOutcome.RejectionReasonRequired);
            }

            if (reason.Length > 1000)
            {
                return MeetingWorkflowResult.Failure(MeetingWorkflowOutcome.RejectionReasonTooLong);
            }

            command = command with { Reason = reason };
        }

        try
        {
            return await meetings.InTransactionAsync(async token =>
            {
                var meeting = await meetings.FindWorkflowAsync(id, forUpdate: true, token);
                if (meeting is null || !MeetingReadPolicy.CanRead(meeting, actorId))
                {
                    return MeetingWorkflowResult.Failure(MeetingWorkflowOutcome.NotFound);
                }

                var requiredCapability = command.Action is MeetingTransitionAction.Approve or MeetingTransitionAction.Reject
                    ? Capability.MeetingApprover
                    : Capability.MeetingCreator;
                var isExpectedActor = command.Action is MeetingTransitionAction.Approve or MeetingTransitionAction.Reject
                    ? meeting.AssignedApproverId == actorId
                    : meeting.CreatedById == actorId;
                if (!isExpectedActor
                    || !await meetings.IsActiveUserWithCapabilityAsync(actorId, requiredCapability, token))
                {
                    return MeetingWorkflowResult.Failure(MeetingWorkflowOutcome.Forbidden);
                }

                var (fromStatus, toStatus, historyAction) = command.Action switch
                {
                    MeetingTransitionAction.SubmitForApproval =>
                        (MeetingStatus.Draft, MeetingStatus.WaitingApproval, MeetingHistoryAction.SubmitForApproval),
                    MeetingTransitionAction.Approve =>
                        (MeetingStatus.WaitingApproval, MeetingStatus.Approved, MeetingHistoryAction.Approve),
                    MeetingTransitionAction.Reject =>
                        (MeetingStatus.WaitingApproval, MeetingStatus.Rejected, MeetingHistoryAction.Reject),
                    MeetingTransitionAction.ReturnToDraft =>
                        (MeetingStatus.Rejected, MeetingStatus.Draft, MeetingHistoryAction.ReturnToDraft),
                    MeetingTransitionAction.Publish =>
                        (MeetingStatus.Approved, MeetingStatus.Published, MeetingHistoryAction.Publish),
                    _ => throw new InvalidOperationException("Unknown meeting transition action."),
                };

                if (meeting.Status != fromStatus)
                {
                    return MeetingWorkflowResult.Failure(MeetingWorkflowOutcome.StatusConflict);
                }

                if (command.Action == MeetingTransitionAction.SubmitForApproval)
                {
                    var readinessErrors = MeetingSubmissionReadinessRules.Validate(meeting);
                    if (readinessErrors.Count > 0)
                    {
                        return MeetingWorkflowResult.Invalid(MeetingWorkflowOutcome.SubmissionNotReady, readinessErrors);
                    }

                    if (!await meetings.IsEligibleApproverAsync(command.AssignedApproverId!.Value, token))
                    {
                        return MeetingWorkflowResult.Failure(MeetingWorkflowOutcome.InvalidApprover);
                    }
                }

                var now = UtcNow;
                meeting.Status = toStatus;
                meeting.UpdatedAt = now;
                if (command.Action == MeetingTransitionAction.SubmitForApproval)
                {
                    meeting.AssignedApproverId = command.AssignedApproverId;
                }
                else if (command.Action == MeetingTransitionAction.ReturnToDraft)
                {
                    meeting.AssignedApproverId = null;
                }

                var history = new MeetingStatusHistory
                {
                    Id = Guid.CreateVersion7(),
                    MeetingId = meeting.Id,
                    FromStatus = fromStatus,
                    ToStatus = toStatus,
                    Action = historyAction,
                    ActorId = actorId,
                    Reason = command.Action == MeetingTransitionAction.Reject ? command.Reason! : string.Empty,
                    AssignedApproverId = command.Action == MeetingTransitionAction.SubmitForApproval
                        ? command.AssignedApproverId
                        : null,
                    OccurredAt = now,
                    CreatedAt = now,
                    UpdatedAt = now,
                };
                meeting.StatusHistory.Add(history);
                await meetings.AddHistoryAsync(history, token);

                if (command.Action == MeetingTransitionAction.SubmitForApproval)
                {
                    await meetings.AddNotificationsAsync(
                    [CreateNotification(
                        command.AssignedApproverId!.Value,
                        meeting,
                        NotificationType.MeetingWaitingApproval,
                        "Cuộc họp cần được duyệt",
                        $"Cuộc họp “{meeting.Title}” đang chờ bạn xem xét.",
                        now,
                        history.Id)], token);
                }
                else if (command.Action == MeetingTransitionAction.Publish)
                {
                    var recipients = MeetingInvitationRecipientPolicy.SelectSnapshot(meeting.Participants);

                    var notifications = recipients.Select(participant => CreateNotification(
                        participant.UserId,
                        meeting,
                        NotificationType.MeetingInvited,
                        "Cuộc họp đã được công bố",
                        $"Bạn được mời tham dự cuộc họp “{meeting.Title}”.",
                        now,
                        history.Id)).ToArray();
                    await meetings.AddNotificationsAsync(notifications, token);

                    var snapshot = JsonSerializer.Serialize(new MeetingInvitationSnapshot(
                        meeting.Id,
                        meeting.Title,
                        meeting.Description,
                        meeting.StartsAt,
                        meeting.EndsAt,
                        meeting.Location,
                        meeting.Sensitivity.ToString(),
                        $"/mgmt/meetings/{meeting.Id}"));
                    var emailMessages = recipients.Select(participant => new EmailMessage
                    {
                        Id = Guid.CreateVersion7(),
                        ToAddress = participant.User!.Email,
                        Subject = LimitText($"Thư mời: {meeting.Title}", 300),
                        Body = $"Bạn được mời tham dự cuộc họp “{meeting.Title}”.",
                        Status = EmailStatus.Queued,
                        MeetingId = meeting.Id,
                        OriginHistoryId = history.Id,
                        RecipientUserId = participant.UserId,
                        MessageType = EmailMessageType.MeetingInvitation,
                        PayloadSnapshot = snapshot,
                        CreatedAt = now,
                        UpdatedAt = now,
                    }).ToArray();
                    await meetings.AddEmailMessagesAsync(emailMessages, token);
                }

                await meetings.SaveChangesAsync(token);
                var actorName = await meetings.FindUserDisplayNameAsync(actorId, token) ?? string.Empty;
                return MeetingWorkflowResult.Success(
                    MeetingDetails.From(meeting), ToHistoryRecord(history, actorName));
            }, ct);
        }
        catch (OperationCanceledException) when (ct.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception exception) when (IsPersistenceFailure(exception))
        {
            logger.LogError(exception, "Could not perform {Action} for meeting {MeetingId}.", command.Action, id);
            return MeetingWorkflowResult.Failure(MeetingWorkflowOutcome.SaveFailed);
        }
    }

    public async Task<MeetingHistoryResult> GetHistoryAsync(
        Guid id, Guid actorId, int page, int pageSize, CancellationToken ct = default)
    {
        if (page < 1 || pageSize is < 1 or > 100)
        {
            return MeetingHistoryResult.Failure(MeetingWorkflowOutcome.InvalidRequest);
        }

        try
        {
            return await meetings.InTransactionAsync(async token =>
            {
                var meeting = await meetings.FindWorkflowAsync(id, forUpdate: true, token);
                if (meeting is null || !MeetingReadPolicy.CanRead(meeting, actorId))
                {
                    return MeetingHistoryResult.Failure(MeetingWorkflowOutcome.NotFound);
                }

                var history = await meetings.FindHistoryPageAsync(id, page, pageSize, token);
                if (history is null)
                {
                    return MeetingHistoryResult.Failure(MeetingWorkflowOutcome.InvalidRequest);
                }

                if (!MeetingReadPolicy.CanReadFullHistory(meeting, actorId))
                {
                    history = history with
                    {
                        Items = history.Items
                            .Select(item => item with { RejectionReason = null })
                            .ToArray(),
                    };
                }

                return MeetingHistoryResult.Success(history);
            }, ct);
        }
        catch (OperationCanceledException) when (ct.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception exception) when (IsPersistenceFailure(exception))
        {
            logger.LogError(exception, "Could not read lifecycle history for meeting {MeetingId}.", id);
            return MeetingHistoryResult.Failure(MeetingWorkflowOutcome.ReadFailed);
        }
    }

    private static MeetingSubmissionReadiness ReadinessFailure(
        MeetingWorkflowOutcome outcome, Guid id, MeetingStatus status = MeetingStatus.Draft)
        => new(outcome, id, status, new Dictionary<string, string>());

    private static bool IsCommandShapeValid(MeetingTransitionCommand command)
    {
        if (!Enum.IsDefined(command.Action))
        {
            return false;
        }

        return command.Action switch
        {
            MeetingTransitionAction.SubmitForApproval =>
                command.AssignedApproverId is { } id && id != Guid.Empty && command.Reason is null,
            MeetingTransitionAction.Reject =>
                command.AssignedApproverId is null,
            _ => command.AssignedApproverId is null && command.Reason is null,
        };
    }

    private static Notification CreateNotification(
        Guid userId,
        Meeting meeting,
        NotificationType type,
        string title,
        string body,
        DateTimeOffset now,
        Guid originHistoryId)
        => new()
        {
            Id = Guid.CreateVersion7(),
            UserId = userId,
            MeetingId = meeting.Id,
            TargetType = "Meeting",
            TargetId = meeting.Id,
            OriginHistoryId = originHistoryId,
            Type = type,
            Title = LimitText(title, 300),
            Body = LimitText(body, 2000),
            CreatedAt = now,
            UpdatedAt = now,
        };

    private static string LimitText(string value, int maximum)
        => value.Length <= maximum ? value : value[..maximum];

    private static MeetingHistoryRecord ToHistoryRecord(MeetingStatusHistory history, string actorName)
        => new(
            history.Id,
            history.MeetingId,
            history.Action,
            history.FromStatus,
            history.ToStatus,
            history.ActorId,
            actorName,
            history.OccurredAt,
            history.Action == MeetingHistoryAction.Reject ? history.Reason : null,
            history.AssignedApproverId);

    private sealed record MeetingInvitationSnapshot(
        Guid MeetingId,
        string Title,
        string Description,
        DateTimeOffset StartsAt,
        DateTimeOffset? EndsAt,
        string Location,
        string Sensitivity,
        string DetailPath);
}
