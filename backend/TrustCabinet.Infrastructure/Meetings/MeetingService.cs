using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Npgsql;
using TrustCabinet.Domain.Access;
using TrustCabinet.Domain.Entities;
using TrustCabinet.Domain.Enums;
using TrustCabinet.Domain.Meetings;
using TrustCabinet.Domain.Participants;

namespace TrustCabinet.Infrastructure.Meetings;

/// <summary>Meeting rules and persistence orchestration across creation, listing and lifecycle workflows.</summary>
public sealed class MeetingService(
    IMeetingRepository meetings,
    ILogger<MeetingService> logger,
    TimeProvider? timeProvider = null) : IMeetingService
{
    private DateTimeOffset UtcNow => (timeProvider ?? TimeProvider.System).GetUtcNow();

    public async Task<MeetingResult> CreateAsync(
        MeetingInput input, Guid actorId, IReadOnlyList<ParticipantSeed>? participants = null, CancellationToken ct = default)
    {
        var now = UtcNow;
        if (!MeetingRules.TryValidate(input, startsAfter: now, out var valid, out var errors))
        {
            return MeetingResult.Invalid(errors);
        }

        var meeting = new Meeting
        {
            Title = valid!.Title,
            PlannedStartTime = valid.PlannedStartTime,
            PlannedEndTime = valid.PlannedEndTime,
            Mode = valid.Mode,
            Location = valid.Location,
            OnlineLink = valid.OnlineLink,
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
            return await meetings.InTransactionAsync(async token =>
            {
                var rosterErrors = new Dictionary<string, string>();
                if (participants is null || participants.Count is < 1 or > 100)
                    rosterErrors["participants"] = "Chọn từ 1 đến 100 thành phần tham dự, có đúng một Chủ tọa.";
                else
                {
                    var seen = new HashSet<Guid>();
                    for (var i = 0; i < participants.Count; i++)
                    {
                        var seed = participants[i];
                        if (seed is null || seed.UserId == Guid.Empty || !seen.Add(seed.UserId))
                            rosterErrors[$"participants[{i}].userId"] = "Người dùng không hợp lệ hoặc bị trùng.";
                        if (seed is null || !ParticipantRules.TryRole(seed.Role, out _))
                            rosterErrors[$"participants[{i}].role"] = "Chọn Chủ tọa, Thư ký hoặc Thành viên.";
                    }
                    if (participants.Count(p => p?.Role == nameof(MeetingRole.Chair)) != 1)
                        rosterErrors["participants"] = "Cuộc họp phải có đúng một Chủ tọa.";
                }
                if (rosterErrors.Count > 0)
                    return new MeetingResult(MeetingOutcome.InvalidParticipants, Errors: rosterErrors);
                var users = (await meetings.LockParticipantUsersAsync(participants!.Select(p => p.UserId).ToArray(), token))
                    .ToDictionary(u => u.Id);
                for (var i = 0; i < participants!.Count; i++)
                    if (!users.TryGetValue(participants[i].UserId, out var user) || !user.IsActive)
                        rosterErrors[$"participants[{i}].userId"] = "Người dùng không tồn tại hoặc đã ngừng hoạt động.";
                if (rosterErrors.Count > 0)
                    return new MeetingResult(MeetingOutcome.InvalidParticipants, Errors: rosterErrors);
                foreach (var seed in participants)
                {
                    var participant = new MeetingParticipant
                    {
                        MeetingId = meeting.Id, UserId = seed.UserId, User = users[seed.UserId],
                        Role = Enum.Parse<MeetingRole>(seed.Role!), Response = InvitationResponse.Pending,
                        CreatedAt = now, UpdatedAt = now
                    };
                    participant.InitialInvitationDeliveryKey = $"participant:{participant.Id}:invited";
                    meeting.Participants.Add(participant);
                }
                await meetings.AddAsync(meeting, token);
                await meetings.SaveChangesAsync(token);
                return MeetingResult.Success(MeetingDetails.From(meeting, actorId));
            }, ct);
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
                : MeetingResult.Success(MeetingDetails.From(meeting, actorId));
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

            return MeetingRules.IsEditable(meeting.Status) || MeetingRules.IsReschedulable(meeting.Status)
                ? MeetingResult.Success(MeetingDetails.From(meeting, actorId))
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

                // Match UC-15: check the current, locked state before validating submitted fields.
                var reschedules = MeetingRules.IsReschedulable(meeting.Status);
                if (!reschedules && !MeetingRules.IsEditable(meeting.Status))
                {
                    return MeetingResult.Failure(MeetingOutcome.NotEditable);
                }

                var input = await inputFactory(token);
                var now = UtcNow;
                if (!MeetingRules.TryValidate(input, reschedules ? now : null, out var valid, out var errors))
                {
                    return MeetingResult.Invalid(errors);
                }

                var normalized = valid!;
                if (reschedules)
                {
                    return await RescheduleAsync(meeting, normalized, actorId, now, token);
                }

                if (HasChanges(meeting, normalized))
                {
                    Apply(meeting, normalized, now);
                    await meetings.SaveChangesAsync(token);
                }

                return MeetingResult.Success(MeetingDetails.From(meeting, actorId));
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

    /// <summary>
    /// UC-15 AF-1. Only the times, place and link may move (BR-158); the rest must come back as it
    /// is. The change is written to history and every current invitee is told in-app (BR-76).
    /// </summary>
    private async Task<MeetingResult> RescheduleAsync(
        Meeting meeting, ValidatedMeetingInput input, Guid actorId, DateTimeOffset now, CancellationToken ct)
    {
        var locked = new Dictionary<string, string>(StringComparer.Ordinal);
        const string lockedMessage = "Cuộc họp đã công bố, chỉ được dời thời gian, địa điểm và link họp.";
        if (!string.Equals(meeting.Title, input.Title, StringComparison.Ordinal))
        {
            locked["title"] = lockedMessage;
        }

        if (meeting.Mode != input.Mode)
        {
            locked["mode"] = lockedMessage;
        }

        if (meeting.Sensitivity != input.Sensitivity)
        {
            locked["sensitivity"] = lockedMessage;
        }

        if (!string.Equals(meeting.Description, input.Description, StringComparison.Ordinal))
        {
            locked["description"] = lockedMessage;
        }

        if (locked.Count > 0)
        {
            return MeetingResult.Invalid(locked);
        }

        if (!HasChanges(meeting, input))
        {
            return MeetingResult.Success(MeetingDetails.From(meeting, actorId));
        }

        Apply(meeting, input, now);
        var history = new MeetingStatusHistory
        {
            Id = Guid.CreateVersion7(),
            MeetingId = meeting.Id,
            FromStatus = MeetingStatus.Published,
            ToStatus = MeetingStatus.Published,
            Action = MeetingHistoryAction.Reschedule,
            ActorId = actorId,
            Reason = string.Empty,
            OccurredAt = now,
            CreatedAt = now,
            UpdatedAt = now,
        };
        meeting.StatusHistory.Add(history);
        await meetings.AddHistoryAsync(history, ct);
        await meetings.StageGuestRescheduleInvitationsAsync(meeting, history.Id, now, ct);
        await meetings.AddNotificationsAsync(
            MeetingInvitationRecipientPolicy.SelectSnapshot(meeting.Participants)
                .Select(participant => CreateNotification(
                    participant.UserId,
                    meeting,
                    NotificationType.MeetingRescheduled,
                    "Cuộc họp đã dời lịch",
                    $"Cuộc họp “{meeting.Title}” đã thay đổi thời gian hoặc địa điểm. Mở cuộc họp để xem lịch mới.",
                    now,
                    history.Id))
                .ToArray(),
            ct);
        await meetings.SaveChangesAsync(ct);
        return MeetingResult.Success(MeetingDetails.From(meeting, actorId), rescheduled: true);
    }

    private static void Apply(Meeting meeting, ValidatedMeetingInput input, DateTimeOffset now)
    {
        meeting.Title = input.Title;
        meeting.PlannedStartTime = input.PlannedStartTime;
        meeting.PlannedEndTime = input.PlannedEndTime;
        meeting.Mode = input.Mode;
        meeting.Location = input.Location;
        meeting.OnlineLink = input.OnlineLink;
        meeting.Sensitivity = input.Sensitivity;
        meeting.Description = input.Description;
        meeting.UpdatedAt = now;
    }

    private static bool HasChanges(Meeting meeting, ValidatedMeetingInput input)
        => !string.Equals(meeting.Title, input.Title, StringComparison.Ordinal)
            || meeting.PlannedStartTime != input.PlannedStartTime
            || meeting.PlannedEndTime != input.PlannedEndTime
            || meeting.Mode != input.Mode
            || !string.Equals(PlaceOf(meeting.Mode, meeting.Location), PlaceOf(input.Mode, input.Location), StringComparison.Ordinal)
            || !string.Equals(meeting.OnlineLink, input.OnlineLink, StringComparison.Ordinal)
            || meeting.Sensitivity != input.Sensitivity
            || !string.Equals(meeting.Description, input.Description, StringComparison.Ordinal);

    /// <summary>
    /// An online meeting has no room: the form hides and clears the field, so a location stored on one
    /// (older rows say "Trực tuyến") must not count as a change, or an untouched reschedule would notify
    /// every invitee.
    /// </summary>
    private static string PlaceOf(MeetingMode mode, string location)
        => mode == MeetingMode.Online ? string.Empty : location;

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

            if (meeting.Status is not (MeetingStatus.Draft or MeetingStatus.Rejected))
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
            var result = await meetings.InTransactionAsync(async token =>
            {
                var transfersOrganizer = command.Action == MeetingTransitionAction.TransferOrganizer;
                // Recovery authorization is independent of ordinary meeting visibility. Check the
                // current database permission before looking up a resource the administrator cannot read.
                if (transfersOrganizer
                    && !await meetings.IsActiveUserWithCapabilityAsync(actorId, Capability.AdminUsers, token))
                {
                    return MeetingWorkflowResult.Failure(MeetingWorkflowOutcome.NotFound);
                }

                var meeting = await meetings.FindWorkflowAsync(id, forUpdate: true, token);
                if (meeting is null || (!transfersOrganizer && !MeetingReadPolicy.CanRead(meeting, actorId)))
                {
                    return MeetingWorkflowResult.Failure(MeetingWorkflowOutcome.NotFound);
                }

                var requiredCapability = command.Action is MeetingTransitionAction.Approve or MeetingTransitionAction.Reject
                    ? Capability.MeetingApprover
                    : Capability.MeetingCreator;
                var isExpectedActor = command.Action is MeetingTransitionAction.Approve or MeetingTransitionAction.Reject
                    ? meeting.AssignedApproverId == actorId
                    : meeting.CreatedById == actorId;
                if (!transfersOrganizer && (!isExpectedActor
                    || (command.Action is MeetingTransitionAction.Approve or MeetingTransitionAction.Reject
                        && meeting.CreatedById == actorId)
                    || !await meetings.IsActiveUserWithCapabilityAsync(actorId, requiredCapability, token)))
                {
                    return MeetingWorkflowResult.Failure(MeetingWorkflowOutcome.Forbidden);
                }

                var (fromStatus, toStatus, historyAction) = command.Action switch
                {
                    MeetingTransitionAction.SubmitForApproval when meeting.Status == MeetingStatus.Rejected =>
                        (MeetingStatus.Rejected, MeetingStatus.WaitingApproval, MeetingHistoryAction.SubmitForApproval),
                    MeetingTransitionAction.SubmitForApproval =>
                        (MeetingStatus.Draft, MeetingStatus.WaitingApproval, MeetingHistoryAction.SubmitForApproval),
                    MeetingTransitionAction.Approve =>
                        (MeetingStatus.WaitingApproval, MeetingStatus.Approved, MeetingHistoryAction.Approve),
                    MeetingTransitionAction.Reject =>
                        (MeetingStatus.WaitingApproval, MeetingStatus.Rejected, MeetingHistoryAction.Reject),
                    MeetingTransitionAction.ReturnToDraft when meeting.Status == MeetingStatus.WaitingApproval =>
                        (MeetingStatus.WaitingApproval, MeetingStatus.Draft, MeetingHistoryAction.ReturnToDraft),
                    MeetingTransitionAction.ReturnToDraft =>
                        (MeetingStatus.Rejected, MeetingStatus.Draft, MeetingHistoryAction.ReturnToDraft),
                    MeetingTransitionAction.Publish =>
                        (MeetingStatus.Approved, MeetingStatus.Published, MeetingHistoryAction.Publish),
                    MeetingTransitionAction.TransferOrganizer =>
                        (MeetingStatus.Approved, MeetingStatus.Approved, MeetingHistoryAction.TransferOrganizer),
                    _ => throw new InvalidOperationException("Unknown meeting transition action."),
                };

                if (meeting.Status != fromStatus)
                {
                    return MeetingWorkflowResult.Failure(MeetingWorkflowOutcome.StatusConflict);
                }

                if (command.Action == MeetingTransitionAction.Publish)
                {
                    var activeChairCount = meeting.Participants.Count(participant =>
                        participant.Role == MeetingRole.Chair && Domain.Participants.ParticipantRules.Active(participant));
                    var hasActiveSecretary = meeting.Participants.Any(participant =>
                        participant.Role == MeetingRole.Secretary && Domain.Participants.ParticipantRules.Active(participant));
                    if (activeChairCount != 1 || !hasActiveSecretary)
                    {
                        return MeetingWorkflowResult.Failure(MeetingWorkflowOutcome.PublicationRosterNotReady);
                    }
                }

                if (transfersOrganizer)
                {
                    if (await meetings.IsUserActiveAsync(meeting.CreatedById, token))
                    {
                        return MeetingWorkflowResult.Failure(MeetingWorkflowOutcome.StatusConflict);
                    }

                    if (command.OrganizerId == meeting.CreatedById
                        || command.OrganizerId == meeting.AssignedApproverId
                        || !await meetings.IsActiveUserWithCapabilityAsync(
                            command.OrganizerId!.Value, Capability.MeetingCreator, token))
                    {
                        return MeetingWorkflowResult.Failure(MeetingWorkflowOutcome.InvalidOrganizer);
                    }
                }

                // Recover blocked or legacy invalid assignments under the meeting lock.
                // A different, active and capable approver keeps the current approval cycle.
                if (command.Action == MeetingTransitionAction.ReturnToDraft
                    && fromStatus == MeetingStatus.WaitingApproval)
                {
                    if (meeting.AssignedApproverId is { } assignedApproverId
                        && assignedApproverId != meeting.CreatedById
                        && await meetings.IsActiveUserWithCapabilityAsync(
                            assignedApproverId, Capability.MeetingApprover, token))
                    {
                        return MeetingWorkflowResult.Failure(MeetingWorkflowOutcome.StatusConflict);
                    }
                }

                if (command.Action == MeetingTransitionAction.SubmitForApproval)
                {
                    var readinessErrors = MeetingSubmissionReadinessRules.Validate(meeting);
                    if (readinessErrors.Count > 0)
                    {
                        return MeetingWorkflowResult.Invalid(MeetingWorkflowOutcome.SubmissionNotReady, readinessErrors);
                    }

                    if (command.AssignedApproverId == meeting.CreatedById
                        || !await meetings.IsEligibleApproverAsync(command.AssignedApproverId!.Value, token))
                    {
                        return MeetingWorkflowResult.Failure(MeetingWorkflowOutcome.InvalidApprover);
                    }
                }

                var now = UtcNow;
                var previousOrganizerId = transfersOrganizer ? meeting.CreatedById : (Guid?)null;
                meeting.Status = toStatus;
                meeting.UpdatedAt = now;
                if (transfersOrganizer)
                {
                    meeting.CreatedById = command.OrganizerId!.Value;
                }
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
                    PreviousOrganizerId = previousOrganizerId,
                    NewOrganizerId = transfersOrganizer ? command.OrganizerId : null,
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
                else if (command.Action == MeetingTransitionAction.Approve)
                {
                    await meetings.AddNotificationsAsync(
                    [CreateNotification(
                        meeting.CreatedById,
                        meeting,
                        NotificationType.MeetingApproved,
                        "Cuộc họp đã được duyệt",
                        $"Cuộc họp “{meeting.Title}” đã được duyệt. Bạn có thể công bố cuộc họp.",
                        now,
                        history.Id)], token);
                }
                else if (command.Action == MeetingTransitionAction.Reject)
                {
                    // Only the organizer receives this, and they may read the rejection reason.
                    await meetings.AddNotificationsAsync(
                    [CreateNotification(
                        meeting.CreatedById,
                        meeting,
                        NotificationType.MeetingRejected,
                        "Cuộc họp bị từ chối",
                        $"Cuộc họp “{meeting.Title}” bị từ chối. Lý do: {command.Reason}",
                        now,
                        history.Id)], token);
                }
                else if (command.Action == MeetingTransitionAction.Publish)
                {
                    // The publisher just acted, so no in-app invitation for them; email still goes
                    // to every active participant, the publisher included (SCRUM-15 producer).
                    var recipients = MeetingInvitationRecipientPolicy.SelectSnapshot(meeting.Participants)
                        .Where(participant => participant.UserId != actorId);

                    var notifications = recipients.Select(participant => CreateNotification(
                        participant.UserId,
                        meeting,
                        NotificationType.MeetingInvited,
                        "Cuộc họp đã được công bố",
                        $"Bạn được mời tham dự cuộc họp “{meeting.Title}”.",
                        now,
                        history.Id)).ToArray();
                    await meetings.AddNotificationsAsync(notifications, token);
                    await meetings.StagePublicationInvitationsAsync(meeting, now, token);
                }

                await meetings.SaveChangesAsync(token);
                var actorName = await meetings.FindUserDisplayNameAsync(actorId, token) ?? string.Empty;
                var historyRecord = ToHistoryRecord(history, actorName);
                // Recovery permission does not grant access to meeting contents.
                return transfersOrganizer
                    ? MeetingWorkflowResult.OrganizerTransferred(historyRecord)
                    : MeetingWorkflowResult.Success(MeetingDetails.From(meeting, actorId), historyRecord);
            }, ct);

            return result;
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
                var meeting = await meetings.FindWorkflowAsync(id, forUpdate: false, token);
                if (meeting is null || !MeetingReadPolicy.CanReadHistory(meeting, actorId))
                {
                    return MeetingHistoryResult.Failure(MeetingWorkflowOutcome.NotFound);
                }

                var history = await meetings.FindHistoryPageAsync(id, page, pageSize, token);
                if (history is null)
                {
                    return MeetingHistoryResult.Failure(MeetingWorkflowOutcome.InvalidRequest);
                }

                var canReadFullHistory = MeetingReadPolicy.CanReadFullHistory(meeting, actorId);
                var canReadRejectionReason = MeetingReadPolicy.CanReadRejectionReason(meeting, actorId);
                if (!canReadFullHistory)
                {
                    history = history with
                    {
                        Items = history.Items
                            .Select(item => item with
                            {
                                RejectionReason = canReadRejectionReason ? item.RejectionReason : null,
                                PreviousOrganizerId = null,
                                NewOrganizerId = null,
                            })
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
                command.AssignedApproverId is { } id && id != Guid.Empty && command.Reason is null
                    && command.OrganizerId is null,
            MeetingTransitionAction.Reject =>
                command.AssignedApproverId is null && command.OrganizerId is null,
            MeetingTransitionAction.TransferOrganizer =>
                command.OrganizerId is { } organizerId && organizerId != Guid.Empty
                    && command.AssignedApproverId is null && command.Reason is null,
            _ => command.AssignedApproverId is null && command.Reason is null && command.OrganizerId is null,
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
    {
        if (value.Length <= maximum)
        {
            return value;
        }

        // Do not turn a valid supplementary character into an unencodable UTF-16 fragment.
        var length = maximum;
        if (length > 0 && char.IsHighSurrogate(value[length - 1]) && char.IsLowSurrogate(value[length]))
        {
            length--;
        }

        return value[..length];
    }

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
            history.AssignedApproverId,
            history.PreviousOrganizerId,
            history.NewOrganizerId);

}
