using Microsoft.Extensions.Logging.Abstractions;
using Npgsql;
using TrustCabinet.Domain.Entities;
using TrustCabinet.Domain.Enums;
using TrustCabinet.Domain.Meetings;
using TrustCabinet.Infrastructure.Meetings;

namespace TrustCabinet.Web.Tests.Meetings;

public sealed class MeetingWorkflowServiceTests
{
    private readonly FakeMeetingRepository _repository = new();
    private readonly FakeMeetingInvitationQueue _invitations = new();
    private readonly Guid _organizerId = Guid.CreateVersion7();
    private readonly Guid _approverId = Guid.CreateVersion7();

    private MeetingService NewService() => new(_repository, NullLogger<MeetingService>.Instance, invitations: _invitations);

    private Meeting GivenMeeting(MeetingStatus status = MeetingStatus.Draft)
    {
        Grant(_organizerId, Capability.MeetingCreator);
        Grant(_approverId, Capability.MeetingApprover);
        var meeting = new Meeting
        {
            Title = "Giao ban tuần",
            Description = "Nội dung",
            Status = status,
            Sensitivity = Sensitivity.Internal,
            Mode = MeetingMode.InPerson,
            Location = "Phòng A",
            StartsAt = new DateTimeOffset(2026, 10, 1, 2, 0, 0, TimeSpan.Zero),
            CreatedById = _organizerId,
            AssignedApproverId = status == MeetingStatus.Draft ? null : _approverId,
        };
        if (status == MeetingStatus.Approved)
        {
            meeting.Sessions.Add(PlannedSession(meeting.Id));
        }
        _repository.Meetings.Add(meeting);
        return meeting;
    }

    private void Grant(Guid userId, Capability capability)
    {
        if (!_repository.UserCapabilities.TryGetValue(userId, out var capabilities))
        {
            capabilities = [];
            _repository.UserCapabilities.Add(userId, capabilities);
        }
        capabilities.Add(capability);
    }

    private static AgendaItem Agenda(Guid meetingId, int position = 1)
        => new()
        {
            MeetingId = meetingId,
            Position = position,
            Title = "Báo cáo",
            PlannedDurationMinutes = 30,
            PresenterId = Guid.CreateVersion7(),
        };

    private static MeetingSession PlannedSession(Guid meetingId)
        => new()
        {
            MeetingId = meetingId,
            Title = "Phiên họp kế hoạch",
            PlannedStartsAt = new DateTimeOffset(2026, 10, 1, 2, 0, 0, TimeSpan.Zero),
            PlannedEndsAt = new DateTimeOffset(2026, 10, 1, 3, 0, 0, TimeSpan.Zero),
            Status = MeetingSessionStatus.Planned,
        };

    [Fact]
    public async Task Submit_records_assignment_history_and_approver_notification()
    {
        var meeting = GivenMeeting();
        meeting.AgendaItems.Add(Agenda(meeting.Id));

        var result = await NewService().TransitionAsync(
            meeting.Id,
            new MeetingTransitionCommand(MeetingTransitionAction.SubmitForApproval, _approverId),
            _organizerId);

        Assert.Equal(MeetingWorkflowOutcome.Success, result.Outcome);
        Assert.Equal(MeetingStatus.WaitingApproval, meeting.Status);
        Assert.Equal(_approverId, meeting.AssignedApproverId);
        var history = Assert.Single(meeting.StatusHistory);
        Assert.Equal(MeetingHistoryAction.SubmitForApproval, history.Action);
        Assert.Equal(_approverId, history.AssignedApproverId);
        var notification = Assert.Single(_repository.Notifications);
        Assert.Equal(_approverId, notification.UserId);
        Assert.Equal(NotificationType.MeetingWaitingApproval, notification.Type);
        Assert.Equal("Meeting", notification.TargetType);
        Assert.Equal(meeting.Id, notification.TargetId);
        Assert.Empty(_repository.EmailMessages);
    }

    [Fact]
    public async Task Rejected_meeting_returns_to_draft_with_a_new_history_without_losing_reason()
    {
        var meeting = GivenMeeting(MeetingStatus.Rejected);
        meeting.StatusHistory.Add(new MeetingStatusHistory
        {
            MeetingId = meeting.Id,
            FromStatus = MeetingStatus.WaitingApproval,
            ToStatus = MeetingStatus.Rejected,
            Action = MeetingHistoryAction.Reject,
            ActorId = _approverId,
            Reason = "Cần bổ sung số liệu",
            AssignedApproverId = _approverId,
            OccurredAt = DateTimeOffset.UtcNow.AddMinutes(-1),
        });

        var result = await NewService().TransitionAsync(
            meeting.Id,
            new MeetingTransitionCommand(MeetingTransitionAction.ReturnToDraft),
            _organizerId);

        Assert.Equal(MeetingWorkflowOutcome.Success, result.Outcome);
        Assert.Equal(MeetingStatus.Draft, meeting.Status);
        Assert.Null(meeting.AssignedApproverId);
        Assert.Equal(2, meeting.StatusHistory.Count);
        Assert.Equal("Cần bổ sung số liệu", meeting.StatusHistory.First().Reason);
        Assert.Equal(MeetingHistoryAction.ReturnToDraft, meeting.StatusHistory.Last().Action);
        Assert.Equal(_approverId, meeting.StatusHistory.First().AssignedApproverId);
        Assert.Empty(_repository.EmailMessages);
        Assert.Empty(_repository.Notifications);
        Assert.Equal(MeetingWorkflowOutcome.NotFound,
            (await NewService().GetHistoryAsync(meeting.Id, _approverId, 1, 20)).Outcome);
        Assert.Equal(MeetingWorkflowOutcome.Success,
            (await NewService().GetHistoryAsync(meeting.Id, _organizerId, 1, 20)).Outcome);
    }

    [Fact]
    public async Task Draft_approver_selected_by_legacy_data_cannot_read_detail_or_history()
    {
        var meeting = GivenMeeting();
        meeting.AssignedApproverId = _approverId;

        var detail = await NewService().GetAsync(meeting.Id, _approverId);
        var organizerDetail = await NewService().GetAsync(meeting.Id, _organizerId);
        var history = await NewService().GetHistoryAsync(meeting.Id, _approverId, 1, 20);
        var action = await NewService().TransitionAsync(
            meeting.Id,
            new MeetingTransitionCommand(MeetingTransitionAction.Approve),
            _approverId);

        Assert.Equal(MeetingOutcome.NotFound, detail.Outcome);
        Assert.Null(organizerDetail.Meeting!.AssignedApproverId);
        Assert.Equal(MeetingWorkflowOutcome.NotFound, history.Outcome);
        Assert.Equal(MeetingWorkflowOutcome.NotFound, action.Outcome);
    }

    [Fact]
    public async Task Assigned_approver_with_capability_can_approve_but_an_unassigned_approver_cannot()
    {
        var meeting = GivenMeeting(MeetingStatus.WaitingApproval);
        var otherApprover = Guid.CreateVersion7();
        Grant(otherApprover, Capability.MeetingApprover);

        var denied = await NewService().TransitionAsync(
            meeting.Id,
            new MeetingTransitionCommand(MeetingTransitionAction.Approve),
            otherApprover);
        var approved = await NewService().TransitionAsync(
            meeting.Id,
            new MeetingTransitionCommand(MeetingTransitionAction.Approve),
            _approverId);

        Assert.Equal(MeetingWorkflowOutcome.NotFound, denied.Outcome);
        Assert.Equal(MeetingWorkflowOutcome.Success, approved.Outcome);
        Assert.Equal(MeetingStatus.Approved, meeting.Status);
    }

    [Fact]
    public async Task Assigned_approver_without_current_capability_is_forbidden()
    {
        var meeting = GivenMeeting(MeetingStatus.WaitingApproval);
        _repository.UserCapabilities.Remove(_approverId);

        var result = await NewService().TransitionAsync(
            meeting.Id,
            new MeetingTransitionCommand(MeetingTransitionAction.Approve),
            _approverId);

        Assert.Equal(MeetingWorkflowOutcome.Forbidden, result.Outcome);
        Assert.Equal(MeetingStatus.WaitingApproval, meeting.Status);
        Assert.Empty(meeting.StatusHistory);
    }

    [Fact]
    public async Task Readiness_get_and_submit_use_the_same_meeting_information_validation()
    {
        var meeting = GivenMeeting();
        meeting.Title = " ";

        var readiness = await NewService().GetSubmissionReadinessAsync(meeting.Id, _organizerId);
        var submit = await NewService().TransitionAsync(
            meeting.Id,
            new MeetingTransitionCommand(MeetingTransitionAction.SubmitForApproval, _approverId),
            _organizerId);

        Assert.False(readiness.Ready);
        Assert.Contains("title", readiness.Errors.Keys);
        Assert.Equal(MeetingWorkflowOutcome.SubmissionNotReady, submit.Outcome);
        Assert.Equal(readiness.Errors, submit.Errors);
        Assert.Equal(MeetingStatus.Draft, meeting.Status);
        Assert.Empty(_repository.Notifications);
    }

    [Theory]
    [InlineData(null, MeetingWorkflowOutcome.RejectionReasonRequired)]
    [InlineData("   ", MeetingWorkflowOutcome.RejectionReasonRequired)]
    [InlineData("valid", MeetingWorkflowOutcome.Success)]
    public async Task Reject_requires_nonblank_reason_and_stores_trimmed_value(
        string? reason, MeetingWorkflowOutcome expected)
    {
        var meeting = GivenMeeting(MeetingStatus.WaitingApproval);

        var result = await NewService().TransitionAsync(
            meeting.Id,
            new MeetingTransitionCommand(MeetingTransitionAction.Reject, Reason: reason),
            _approverId);

        Assert.Equal(expected, result.Outcome);
        Assert.Equal(expected == MeetingWorkflowOutcome.Success ? MeetingStatus.Rejected : MeetingStatus.WaitingApproval,
            meeting.Status);
        if (expected == MeetingWorkflowOutcome.Success)
        {
            Assert.Equal("valid", Assert.Single(meeting.StatusHistory).Reason);
        }
    }

    [Fact]
    public async Task Reject_accepts_exactly_1000_characters_and_rejects_1001()
    {
        var acceptedMeeting = GivenMeeting(MeetingStatus.WaitingApproval);
        var accepted = await NewService().TransitionAsync(
            acceptedMeeting.Id,
            new MeetingTransitionCommand(MeetingTransitionAction.Reject, Reason: new string('x', 1000)),
            _approverId);
        var rejectedMeeting = GivenMeeting(MeetingStatus.WaitingApproval);
        var rejected = await NewService().TransitionAsync(
            rejectedMeeting.Id,
            new MeetingTransitionCommand(MeetingTransitionAction.Reject, Reason: new string('x', 1001)),
            _approverId);

        Assert.Equal(MeetingWorkflowOutcome.Success, accepted.Outcome);
        Assert.Equal(1000, Assert.Single(acceptedMeeting.StatusHistory).Reason.Length);
        Assert.Equal(MeetingWorkflowOutcome.RejectionReasonTooLong, rejected.Outcome);
        Assert.Equal(MeetingStatus.WaitingApproval, rejectedMeeting.Status);
    }

    [Fact]
    public async Task Publish_notifies_active_non_delegated_participants_and_passes_meeting_id_to_the_email_owner()
    {
        var meeting = GivenMeeting(MeetingStatus.Approved);
        var active = Participant(meeting, active: true, InvitationResponse.Accepted);
        var inactive = Participant(meeting, active: false, InvitationResponse.Pending);
        var delegated = Participant(meeting, active: true, InvitationResponse.Delegated);
        meeting.Participants.Add(active);
        meeting.Participants.Add(inactive);
        meeting.Participants.Add(delegated);

        var result = await NewService().TransitionAsync(
            meeting.Id,
            new MeetingTransitionCommand(MeetingTransitionAction.Publish),
            _organizerId);

        Assert.Equal(MeetingWorkflowOutcome.Success, result.Outcome);
        Assert.Equal(MeetingStatus.Published, meeting.Status);
        var notification = Assert.Single(_repository.Notifications);
        Assert.Equal(active.UserId, notification.UserId);
        Assert.Equal(NotificationType.MeetingInvited, notification.Type);
        Assert.Equal(meeting.StatusHistory.Single().Id, notification.OriginHistoryId);
        Assert.Equal(meeting.Id, Assert.Single(_invitations.MeetingIds));
        Assert.Equal(MeetingHistoryAction.Publish, meeting.StatusHistory.Single().Action);
    }

    [Fact]
    public async Task Publish_keeps_the_full_unicode_title_available_to_the_email_owner()
    {
        var meeting = GivenMeeting(MeetingStatus.Approved);
        meeting.Title = new string('a', 290) + string.Concat(Enumerable.Repeat("📋", 5));
        Assert.Equal(300, meeting.Title.Length);
        meeting.Participants.Add(Participant(meeting, active: true, InvitationResponse.Pending));

        var result = await NewService().TransitionAsync(
            meeting.Id, new MeetingTransitionCommand(MeetingTransitionAction.Publish), _organizerId);

        Assert.Equal(MeetingWorkflowOutcome.Success, result.Outcome);
        Assert.Equal(meeting.Id, Assert.Single(_invitations.MeetingIds));
        Assert.Equal(new string('a', 290) + string.Concat(Enumerable.Repeat("📋", 5)),
            _repository.Meetings.Single().Title);
    }

    [Theory]
    [InlineData(Sensitivity.Normal)]
    [InlineData(Sensitivity.Internal)]
    [InlineData(Sensitivity.Confidential)]
    [InlineData(Sensitivity.Secret)]
    public async Task Organizer_cannot_submit_to_self_even_with_both_capabilities(Sensitivity sensitivity)
    {
        var meeting = GivenMeeting();
        meeting.Sensitivity = sensitivity;
        meeting.Participants.Add(new MeetingParticipant { MeetingId = meeting.Id, UserId = _organizerId, Role = MeetingRole.Chair });
        meeting.AgendaItems.Add(Agenda(meeting.Id));
        Grant(_organizerId, Capability.MeetingApprover);

        var result = await NewService().TransitionAsync(meeting.Id,
            new MeetingTransitionCommand(MeetingTransitionAction.SubmitForApproval, _organizerId), _organizerId);

        Assert.Equal(MeetingWorkflowOutcome.InvalidApprover, result.Outcome);
        Assert.Equal(MeetingStatus.Draft, meeting.Status);
        Assert.Null(meeting.AssignedApproverId);
        Assert.Empty(meeting.StatusHistory);
        Assert.Empty(_repository.Notifications);
        Assert.Empty(_repository.EmailMessages);
    }

    [Theory]
    [InlineData(MeetingTransitionAction.Approve)]
    [InlineData(MeetingTransitionAction.Reject)]
    public async Task Legacy_self_assignment_cannot_approve_or_reject(MeetingTransitionAction action)
    {
        var meeting = GivenMeeting(MeetingStatus.WaitingApproval);
        meeting.AssignedApproverId = _organizerId;
        Grant(_organizerId, Capability.MeetingApprover);

        var result = await NewService().TransitionAsync(meeting.Id,
            new MeetingTransitionCommand(action, Reason: action == MeetingTransitionAction.Reject ? "Lý do" : null),
            _organizerId);

        Assert.Equal(MeetingWorkflowOutcome.Forbidden, result.Outcome);
        Assert.Equal(MeetingStatus.WaitingApproval, meeting.Status);
        Assert.Empty(meeting.StatusHistory);
        Assert.Empty(_repository.Notifications);
        Assert.Empty(_repository.EmailMessages);
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public async Task Waiting_approval_can_be_withdrawn_only_when_approver_is_ineligible(bool approverEligible)
    {
        var meeting = GivenMeeting(MeetingStatus.WaitingApproval);
        if (!approverEligible)
        {
            _repository.UserCapabilities.Remove(_approverId);
        }

        var result = await NewService().TransitionAsync(meeting.Id,
            new MeetingTransitionCommand(MeetingTransitionAction.ReturnToDraft), _organizerId);

        Assert.Equal(approverEligible ? MeetingWorkflowOutcome.StatusConflict : MeetingWorkflowOutcome.Success,
            result.Outcome);
        Assert.Equal(approverEligible ? MeetingStatus.WaitingApproval : MeetingStatus.Draft, meeting.Status);
        Assert.Equal(approverEligible ? _approverId : (Guid?)null, meeting.AssignedApproverId);
        if (approverEligible)
        {
            Assert.Empty(meeting.StatusHistory);
        }
        else
        {
            var history = Assert.Single(meeting.StatusHistory);
            Assert.Equal(MeetingHistoryAction.ReturnToDraft, history.Action);
            Assert.Equal(MeetingStatus.WaitingApproval, history.FromStatus);
            Assert.Equal(MeetingStatus.Draft, history.ToStatus);
            Assert.Equal(_organizerId, history.ActorId);
        }
        Assert.True(_repository.LastWorkflowForUpdate);
        Assert.Empty(_repository.Notifications);
        Assert.Empty(_repository.EmailMessages);
        Assert.Empty(_invitations.MeetingIds);
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public async Task Legacy_missing_or_self_assignment_can_be_withdrawn_and_resubmitted(bool selfAssignment)
    {
        var meeting = GivenMeeting(MeetingStatus.WaitingApproval);
        meeting.AssignedApproverId = selfAssignment ? _organizerId : null;
        meeting.AgendaItems.Add(Agenda(meeting.Id));
        Grant(_organizerId, Capability.MeetingApprover);

        var result = await NewService().TransitionAsync(meeting.Id,
            new MeetingTransitionCommand(MeetingTransitionAction.ReturnToDraft), _organizerId);

        Assert.Equal(MeetingWorkflowOutcome.Success, result.Outcome);
        Assert.Equal(MeetingStatus.Draft, meeting.Status);
        Assert.Null(meeting.AssignedApproverId);
        var withdrawal = Assert.Single(meeting.StatusHistory);
        Assert.Equal(MeetingHistoryAction.ReturnToDraft, withdrawal.Action);
        Assert.Equal(MeetingStatus.WaitingApproval, withdrawal.FromStatus);
        Assert.Equal(MeetingStatus.Draft, withdrawal.ToStatus);
        Assert.Equal(_organizerId, withdrawal.ActorId);
        Assert.True(_repository.LastWorkflowForUpdate);
        Assert.Empty(_repository.Notifications);
        Assert.Empty(_repository.EmailMessages);
        Assert.Empty(_invitations.MeetingIds);

        var resubmit = await NewService().TransitionAsync(meeting.Id,
            new MeetingTransitionCommand(MeetingTransitionAction.SubmitForApproval, _approverId), _organizerId);

        Assert.Equal(MeetingWorkflowOutcome.Success, resubmit.Outcome);
        Assert.Equal(MeetingStatus.WaitingApproval, meeting.Status);
        Assert.Equal(_approverId, meeting.AssignedApproverId);
        Assert.Equal(2, meeting.StatusHistory.Count);
        Assert.Same(withdrawal, meeting.StatusHistory.First());
        Assert.Equal(MeetingHistoryAction.ReturnToDraft, withdrawal.Action);
        Assert.Equal(MeetingStatus.Draft, withdrawal.ToStatus);
        Assert.Equal(_approverId, Assert.Single(_repository.Notifications).UserId);
        Assert.Empty(_invitations.MeetingIds);
    }

    [Fact]
    public async Task Withdrawal_save_failure_rolls_back_the_blocked_approval_cycle()
    {
        var meeting = GivenMeeting(MeetingStatus.WaitingApproval);
        _repository.UserCapabilities.Remove(_approverId);
        _repository.NextSaveThrows = new NpgsqlException("database unavailable");

        var result = await NewService().TransitionAsync(meeting.Id,
            new MeetingTransitionCommand(MeetingTransitionAction.ReturnToDraft), _organizerId);

        Assert.Equal(MeetingWorkflowOutcome.SaveFailed, result.Outcome);
        var persisted = Assert.Single(_repository.Meetings);
        Assert.Equal(MeetingStatus.WaitingApproval, persisted.Status);
        Assert.Equal(_approverId, persisted.AssignedApproverId);
        Assert.Empty(persisted.StatusHistory);
        Assert.Empty(_repository.Notifications);
        Assert.Empty(_invitations.MeetingIds);
    }

    [Theory]
    [InlineData(MeetingStatus.Draft)]
    [InlineData(MeetingStatus.Approved)]
    [InlineData(MeetingStatus.Published)]
    public async Task Ineligible_approver_does_not_allow_withdrawal_from_other_states(MeetingStatus status)
    {
        var meeting = GivenMeeting(status);
        _repository.UserCapabilities.Remove(_approverId);

        var result = await NewService().TransitionAsync(meeting.Id,
            new MeetingTransitionCommand(MeetingTransitionAction.ReturnToDraft), _organizerId);

        Assert.Equal(MeetingWorkflowOutcome.StatusConflict, result.Outcome);
        Assert.Equal(status, meeting.Status);
        Assert.Empty(meeting.StatusHistory);
        Assert.Empty(_repository.Notifications);
        Assert.Empty(_invitations.MeetingIds);
    }

    [Fact]
    public async Task Invitation_producer_failure_keeps_the_successful_publication_for_worker_retry()
    {
        var meeting = GivenMeeting(MeetingStatus.Approved);
        meeting.Participants.Add(Participant(meeting, active: true, InvitationResponse.Pending));
        _invitations.NextQueueThrows = new InvalidOperationException("Queue unavailable");

        var result = await NewService().TransitionAsync(meeting.Id,
            new MeetingTransitionCommand(MeetingTransitionAction.Publish), _organizerId);

        Assert.Equal(MeetingWorkflowOutcome.Success, result.Outcome);
        Assert.Equal(MeetingStatus.Published, Assert.Single(_repository.Meetings).Status);
        Assert.Equal(MeetingHistoryAction.Publish, Assert.Single(meeting.StatusHistory).Action);
        Assert.Single(_repository.Notifications);
        Assert.Empty(_invitations.MeetingIds);

        await _invitations.QueueForPublishedMeetingAsync(meeting.Id);

        Assert.Equal([meeting.Id], _invitations.MeetingIds);
        Assert.Single(meeting.StatusHistory);
    }

    [Fact]
    public async Task Publish_persistence_failure_rolls_back_status_history_notifications_and_jobs()
    {
        var meeting = GivenMeeting(MeetingStatus.Approved);
        meeting.Participants.Add(Participant(meeting, active: true, InvitationResponse.Pending));
        _repository.NextSaveThrows = new NpgsqlException("database unavailable");

        var result = await NewService().TransitionAsync(
            meeting.Id,
            new MeetingTransitionCommand(MeetingTransitionAction.Publish),
            _organizerId);

        Assert.Equal(MeetingWorkflowOutcome.SaveFailed, result.Outcome);
        var persisted = Assert.Single(_repository.Meetings);
        Assert.Equal(MeetingStatus.Approved, persisted.Status);
        Assert.Empty(persisted.StatusHistory);
        Assert.Empty(_repository.Notifications);
        Assert.Empty(_repository.EmailMessages);
    }

    [Fact]
    public async Task Organizer_and_current_approver_get_reasons_and_published_participant_gets_redacted_timeline()
    {
        var meeting = GivenMeeting(MeetingStatus.Draft);
        var previousApprover = Guid.CreateVersion7();
        var currentApprover = Guid.CreateVersion7();
        var publishedParticipant = Guid.CreateVersion7();
        meeting.AssignedApproverId = currentApprover;
        meeting.Status = MeetingStatus.Published;
        meeting.Participants.Add(new MeetingParticipant
        {
            MeetingId = meeting.Id,
            UserId = publishedParticipant,
            Response = InvitationResponse.Accepted,
        });
        meeting.StatusHistory.Add(new MeetingStatusHistory
        {
            MeetingId = meeting.Id,
            FromStatus = MeetingStatus.WaitingApproval,
            ToStatus = MeetingStatus.Rejected,
            Action = MeetingHistoryAction.Reject,
            ActorId = previousApprover,
            Reason = "Cần bổ sung số liệu",
            AssignedApproverId = previousApprover,
            OccurredAt = DateTimeOffset.UtcNow,
        });

        var current = await NewService().GetHistoryAsync(meeting.Id, currentApprover, 1, 20);
        var organizer = await NewService().GetHistoryAsync(meeting.Id, _organizerId, 1, 20);
        var former = await NewService().GetHistoryAsync(meeting.Id, previousApprover, 1, 20);
        var participant = await NewService().GetHistoryAsync(meeting.Id, publishedParticipant, 1, 20);

        Assert.Equal(MeetingWorkflowOutcome.Success, current.Outcome);
        Assert.Equal(MeetingWorkflowOutcome.Success, organizer.Outcome);
        Assert.Equal(MeetingWorkflowOutcome.NotFound, former.Outcome);
        Assert.Equal(MeetingWorkflowOutcome.Success, participant.Outcome);
        Assert.Equal("Cần bổ sung số liệu", Assert.Single(current.Page!.Items).RejectionReason);
        Assert.Null(Assert.Single(participant.Page!.Items).RejectionReason);
        Assert.Equal("Cần bổ sung số liệu", Assert.Single(organizer.Page!.Items).RejectionReason);
        Assert.False(_repository.LastWorkflowForUpdate);
    }

    [Fact]
    public async Task Administrative_transfer_recovers_an_approved_meeting_and_records_typed_organizer_ids()
    {
        var meeting = GivenMeeting(MeetingStatus.Approved);
        var administrator = Guid.CreateVersion7();
        var replacement = Guid.CreateVersion7();
        Grant(administrator, Capability.AdminUsers);
        Grant(replacement, Capability.MeetingCreator);
        _repository.InactiveUserIds.Add(_organizerId);
        var approval = new MeetingStatusHistory
        {
            MeetingId = meeting.Id,
            FromStatus = MeetingStatus.WaitingApproval,
            ToStatus = MeetingStatus.Approved,
            Action = MeetingHistoryAction.Approve,
            ActorId = _approverId,
            OccurredAt = DateTimeOffset.UtcNow.AddMinutes(-1),
        };
        meeting.StatusHistory.Add(approval);

        var result = await NewService().TransitionAsync(meeting.Id,
            new MeetingTransitionCommand(MeetingTransitionAction.TransferOrganizer, OrganizerId: replacement),
            administrator);

        Assert.Equal(MeetingWorkflowOutcome.Success, result.Outcome);
        Assert.Null(result.Meeting);
        Assert.Equal(MeetingStatus.Approved, meeting.Status);
        Assert.Equal(replacement, meeting.CreatedById);
        Assert.Equal(_approverId, meeting.AssignedApproverId);
        Assert.Equal(2, meeting.StatusHistory.Count);
        Assert.Same(approval, meeting.StatusHistory.First());
        var transfer = meeting.StatusHistory.Last();
        Assert.Equal(MeetingHistoryAction.TransferOrganizer, transfer.Action);
        Assert.Equal(MeetingStatus.Approved, transfer.FromStatus);
        Assert.Equal(MeetingStatus.Approved, transfer.ToStatus);
        Assert.Equal(administrator, transfer.ActorId);
        Assert.Equal(_organizerId, transfer.PreviousOrganizerId);
        Assert.Equal(replacement, transfer.NewOrganizerId);
        Assert.Equal(_organizerId, result.History!.PreviousOrganizerId);
        Assert.Equal(replacement, result.History.NewOrganizerId);
        Assert.Empty(_repository.Notifications);
        Assert.Empty(_invitations.MeetingIds);
        Assert.True(_repository.LastWorkflowForUpdate);

        Assert.Equal(MeetingOutcome.NotFound, (await NewService().GetAsync(meeting.Id, administrator)).Outcome);
        Assert.Equal(MeetingWorkflowOutcome.NotFound,
            (await NewService().GetHistoryAsync(meeting.Id, administrator, 1, 20)).Outcome);
        var retry = await NewService().TransitionAsync(meeting.Id,
            new MeetingTransitionCommand(MeetingTransitionAction.TransferOrganizer, OrganizerId: Guid.CreateVersion7()),
            administrator);
        Assert.Equal(MeetingWorkflowOutcome.StatusConflict, retry.Outcome);
        Assert.Equal(2, meeting.StatusHistory.Count);

        var publish = await NewService().TransitionAsync(meeting.Id,
            new MeetingTransitionCommand(MeetingTransitionAction.Publish), replacement);
        Assert.Equal(MeetingWorkflowOutcome.Success, publish.Outcome);
        Assert.Equal(MeetingStatus.Published, meeting.Status);
        Assert.Equal(meeting.Id, Assert.Single(_invitations.MeetingIds));
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task Transfer_requires_current_active_administrator_before_reading_the_meeting(bool inactive)
    {
        var meeting = GivenMeeting(MeetingStatus.Approved);
        var administrator = Guid.CreateVersion7();
        Grant(administrator, inactive ? Capability.AdminUsers : Capability.MeetingCreator);
        if (inactive) _repository.InactiveUserIds.Add(administrator);
        _repository.InactiveUserIds.Add(_organizerId);
        _repository.NextWorkflowReadThrows = new InvalidOperationException("Resource must not be loaded");

        var result = await NewService().TransitionAsync(meeting.Id,
            new MeetingTransitionCommand(MeetingTransitionAction.TransferOrganizer, OrganizerId: Guid.CreateVersion7()),
            administrator);

        Assert.Equal(MeetingWorkflowOutcome.NotFound, result.Outcome);
        Assert.NotNull(_repository.NextWorkflowReadThrows);
        Assert.Equal(_organizerId, meeting.CreatedById);
        Assert.Empty(meeting.StatusHistory);
        Assert.Empty(_repository.Notifications);
    }

    [Fact]
    public async Task Transfer_cannot_replace_an_active_organizer_even_if_their_creator_capability_was_removed()
    {
        var meeting = GivenMeeting(MeetingStatus.Approved);
        _repository.UserCapabilities[_organizerId].Clear();
        var administrator = Guid.CreateVersion7();
        var replacement = Guid.CreateVersion7();
        Grant(administrator, Capability.AdminUsers);
        Grant(replacement, Capability.MeetingCreator);

        var result = await NewService().TransitionAsync(meeting.Id,
            new MeetingTransitionCommand(MeetingTransitionAction.TransferOrganizer, OrganizerId: replacement),
            administrator);

        Assert.Equal(MeetingWorkflowOutcome.StatusConflict, result.Outcome);
        Assert.Equal(_organizerId, meeting.CreatedById);
        Assert.Empty(meeting.StatusHistory);
    }

    [Theory]
    [InlineData("missing")]
    [InlineData("inactive")]
    [InlineData("withoutCapability")]
    [InlineData("previousOrganizer")]
    [InlineData("assignedApprover")]
    public async Task Transfer_rejects_an_invalid_organizer_without_recording_history(string target)
    {
        var meeting = GivenMeeting(MeetingStatus.Approved);
        var administrator = Guid.CreateVersion7();
        var replacement = target switch
        {
            "previousOrganizer" => _organizerId,
            "assignedApprover" => _approverId,
            _ => Guid.CreateVersion7(),
        };
        Grant(administrator, Capability.AdminUsers);
        _repository.InactiveUserIds.Add(_organizerId);
        if (target != "missing")
        {
            Grant(replacement, target == "withoutCapability" ? Capability.MeetingApprover : Capability.MeetingCreator);
        }
        if (target == "inactive") _repository.InactiveUserIds.Add(replacement);

        var result = await NewService().TransitionAsync(meeting.Id,
            new MeetingTransitionCommand(MeetingTransitionAction.TransferOrganizer, OrganizerId: replacement),
            administrator);

        Assert.Equal(MeetingWorkflowOutcome.InvalidOrganizer, result.Outcome);
        Assert.Equal(_organizerId, meeting.CreatedById);
        Assert.Equal(_approverId, meeting.AssignedApproverId);
        Assert.Equal(MeetingStatus.Approved, meeting.Status);
        Assert.Empty(meeting.StatusHistory);
        Assert.Empty(_repository.Notifications);
        Assert.Empty(_invitations.MeetingIds);
    }

    [Theory]
    [InlineData(MeetingStatus.Draft)]
    [InlineData(MeetingStatus.WaitingApproval)]
    [InlineData(MeetingStatus.Rejected)]
    [InlineData(MeetingStatus.Published)]
    public async Task Transfer_requires_an_approved_meeting(MeetingStatus status)
    {
        var meeting = GivenMeeting(status);
        var administrator = Guid.CreateVersion7();
        var replacement = Guid.CreateVersion7();
        Grant(administrator, Capability.AdminUsers);
        Grant(replacement, Capability.MeetingCreator);
        _repository.InactiveUserIds.Add(_organizerId);

        var result = await NewService().TransitionAsync(meeting.Id,
            new MeetingTransitionCommand(MeetingTransitionAction.TransferOrganizer, OrganizerId: replacement),
            administrator);

        Assert.Equal(MeetingWorkflowOutcome.StatusConflict, result.Outcome);
        Assert.Equal(status, meeting.Status);
        Assert.Equal(_organizerId, meeting.CreatedById);
        Assert.Empty(meeting.StatusHistory);
    }

    [Fact]
    public async Task Transfer_save_failure_rolls_back_the_organizer_and_history()
    {
        var meeting = GivenMeeting(MeetingStatus.Approved);
        var administrator = Guid.CreateVersion7();
        var replacement = Guid.CreateVersion7();
        Grant(administrator, Capability.AdminUsers);
        Grant(replacement, Capability.MeetingCreator);
        _repository.InactiveUserIds.Add(_organizerId);
        _repository.NextSaveThrows = new TimeoutException("Persistence unavailable");

        var result = await NewService().TransitionAsync(meeting.Id,
            new MeetingTransitionCommand(MeetingTransitionAction.TransferOrganizer, OrganizerId: replacement),
            administrator);

        Assert.Equal(MeetingWorkflowOutcome.SaveFailed, result.Outcome);
        var persisted = Assert.Single(_repository.Meetings);
        Assert.Equal(_organizerId, persisted.CreatedById);
        Assert.Equal(_approverId, persisted.AssignedApproverId);
        Assert.Equal(MeetingStatus.Approved, persisted.Status);
        Assert.Empty(persisted.StatusHistory);
        Assert.Empty(_repository.Notifications);
        Assert.Empty(_invitations.MeetingIds);
    }

    [Fact]
    public async Task Transfer_organizer_ids_are_visible_only_in_full_history()
    {
        var meeting = GivenMeeting(MeetingStatus.Published);
        var member = Participant(meeting, active: true, InvitationResponse.Accepted);
        meeting.Participants.Add(member);
        meeting.StatusHistory.Add(new MeetingStatusHistory
        {
            MeetingId = meeting.Id,
            FromStatus = MeetingStatus.Approved,
            ToStatus = MeetingStatus.Approved,
            Action = MeetingHistoryAction.TransferOrganizer,
            ActorId = Guid.CreateVersion7(),
            PreviousOrganizerId = Guid.CreateVersion7(),
            NewOrganizerId = _organizerId,
            OccurredAt = DateTimeOffset.UtcNow,
        });

        var full = await NewService().GetHistoryAsync(meeting.Id, _organizerId, 1, 20);
        var limited = await NewService().GetHistoryAsync(meeting.Id, member.UserId, 1, 20);

        Assert.NotNull(Assert.Single(full.Page!.Items).PreviousOrganizerId);
        Assert.Equal(_organizerId, full.Page.Items.Single().NewOrganizerId);
        Assert.Null(Assert.Single(limited.Page!.Items).PreviousOrganizerId);
        Assert.Null(limited.Page.Items.Single().NewOrganizerId);
        Assert.Equal(MeetingHistoryAction.TransferOrganizer, limited.Page.Items.Single().Action);
    }

    [Theory]
    [InlineData(MeetingStatus.Draft)]
    [InlineData(MeetingStatus.Rejected)]
    public async Task Submit_accepts_draft_or_rejected_without_agenda_or_sessions(MeetingStatus status)
    {
        var meeting = GivenMeeting(status);
        var previousApproverId = _approverId;
        var replacement = Guid.CreateVersion7();
        Grant(replacement, Capability.MeetingApprover);
        MeetingStatusHistory? rejection = null;
        if (status == MeetingStatus.Rejected)
        {
            rejection = new MeetingStatusHistory
            {
                MeetingId = meeting.Id,
                Action = MeetingHistoryAction.Reject,
                FromStatus = MeetingStatus.WaitingApproval,
                ToStatus = MeetingStatus.Rejected,
                ActorId = previousApproverId,
                Reason = "Bổ sung tài liệu",
                AssignedApproverId = previousApproverId,
            };
            meeting.StatusHistory.Add(rejection);
        }

        var readiness = await NewService().GetSubmissionReadinessAsync(meeting.Id, _organizerId);
        var result = await NewService().TransitionAsync(meeting.Id,
            new MeetingTransitionCommand(MeetingTransitionAction.SubmitForApproval, replacement), _organizerId);

        Assert.True(readiness.Ready);
        Assert.Equal(status, readiness.Status);
        Assert.Equal(MeetingWorkflowOutcome.Success, result.Outcome);
        Assert.Equal(MeetingStatus.WaitingApproval, meeting.Status);
        Assert.Equal(replacement, meeting.AssignedApproverId);
        var submission = meeting.StatusHistory.Last();
        Assert.Equal(status, submission.FromStatus);
        Assert.Equal(MeetingHistoryAction.SubmitForApproval, submission.Action);
        Assert.Equal(replacement, submission.AssignedApproverId);
        if (rejection is not null)
        {
            Assert.Same(rejection, meeting.StatusHistory.First());
            Assert.Equal("Bổ sung tài liệu", rejection.Reason);
            Assert.Equal(previousApproverId, rejection.AssignedApproverId);
        }
        Assert.Equal(status == MeetingStatus.Rejected ? 2 : 1, meeting.StatusHistory.Count);
        Assert.Equal(replacement, Assert.Single(_repository.Notifications).UserId);
        Assert.Empty(_invitations.MeetingIds);
    }

    [Theory]
    [InlineData("invalidApprover", MeetingWorkflowOutcome.InvalidApprover)]
    [InlineData("invalidInformation", MeetingWorkflowOutcome.SubmissionNotReady)]
    [InlineData("saveFailure", MeetingWorkflowOutcome.SaveFailed)]
    public async Task Failed_rejected_resubmission_keeps_assignment_and_rejection_evidence(
        string failure, MeetingWorkflowOutcome expected)
    {
        var meeting = GivenMeeting(MeetingStatus.Rejected);
        var replacement = Guid.CreateVersion7();
        Grant(replacement, Capability.MeetingApprover);
        var rejection = new MeetingStatusHistory
        {
            MeetingId = meeting.Id,
            Action = MeetingHistoryAction.Reject,
            FromStatus = MeetingStatus.WaitingApproval,
            ToStatus = MeetingStatus.Rejected,
            ActorId = _approverId,
            Reason = "Giữ nguyên bằng chứng từ chối",
            AssignedApproverId = _approverId,
        };
        meeting.StatusHistory.Add(rejection);
        switch (failure)
        {
            case "invalidApprover": _repository.IneligibleApproverIds.Add(replacement); break;
            case "invalidInformation": meeting.Title = " "; break;
            case "saveFailure": _repository.NextSaveThrows = new TimeoutException("Persistence unavailable"); break;
        }

        var result = await NewService().TransitionAsync(meeting.Id,
            new MeetingTransitionCommand(MeetingTransitionAction.SubmitForApproval, replacement), _organizerId);

        Assert.Equal(expected, result.Outcome);
        var persisted = Assert.Single(_repository.Meetings);
        Assert.Equal(MeetingStatus.Rejected, persisted.Status);
        Assert.Equal(_approverId, persisted.AssignedApproverId);
        Assert.Same(rejection, Assert.Single(persisted.StatusHistory));
        Assert.Equal("Giữ nguyên bằng chứng từ chối", rejection.Reason);
        Assert.Equal(_approverId, rejection.AssignedApproverId);
        Assert.Empty(_repository.Notifications);
        Assert.Empty(_repository.EmailMessages);
        Assert.Empty(_invitations.MeetingIds);
    }

    [Theory]
    [InlineData("none")]
    [InlineData("cancelled")]
    [InlineData("completed")]
    [InlineData("inProgress")]
    [InlineData("otherMeeting")]
    [InlineData("blankTitle")]
    [InlineData("longTitle")]
    [InlineData("defaultStart")]
    [InlineData("defaultEnd")]
    [InlineData("equalTimes")]
    [InlineData("reversedTimes")]
    [InlineData("emptyId")]
    public async Task Publish_requires_a_valid_planned_session_before_any_side_effect(string invalid)
    {
        var meeting = GivenMeeting(MeetingStatus.Approved);
        meeting.Sessions.Clear();
        var session = PlannedSession(meeting.Id);
        switch (invalid)
        {
            case "cancelled": session.Status = MeetingSessionStatus.Cancelled; break;
            case "completed": session.Status = MeetingSessionStatus.Completed; break;
            case "inProgress": session.Status = MeetingSessionStatus.InProgress; break;
            case "otherMeeting": session.MeetingId = Guid.CreateVersion7(); break;
            case "blankTitle": session.Title = " "; break;
            case "longTitle": session.Title = new string('x', 301); break;
            case "defaultStart": session.PlannedStartsAt = default; break;
            case "defaultEnd": session.PlannedEndsAt = default; break;
            case "equalTimes": session.PlannedEndsAt = session.PlannedStartsAt; break;
            case "reversedTimes": session.PlannedEndsAt = session.PlannedStartsAt.AddMinutes(-1); break;
            case "emptyId": session.Id = Guid.Empty; break;
        }
        if (invalid != "none") meeting.Sessions.Add(session);
        meeting.Participants.Add(Participant(meeting, active: true, InvitationResponse.Accepted));
        var updatedAt = meeting.UpdatedAt;

        var result = await NewService().TransitionAsync(meeting.Id,
            new MeetingTransitionCommand(MeetingTransitionAction.Publish), _organizerId);

        Assert.Equal(MeetingWorkflowOutcome.PublicationNotReady, result.Outcome);
        Assert.Contains("meetingSessions", result.Errors!.Keys);
        Assert.Equal(MeetingStatus.Approved, meeting.Status);
        Assert.Equal(updatedAt, meeting.UpdatedAt);
        Assert.Empty(meeting.StatusHistory);
        Assert.Empty(_repository.Notifications);
        Assert.Empty(_repository.EmailMessages);
        Assert.Empty(_invitations.MeetingIds);
        Assert.Equal(0, _repository.SaveCount);
        Assert.True(_repository.LastWorkflowForUpdate);
    }

    [Theory]
    [InlineData(MeetingStatus.Draft)]
    [InlineData(MeetingStatus.WaitingApproval)]
    [InlineData(MeetingStatus.Rejected)]
    [InlineData(MeetingStatus.Approved)]
    [InlineData(MeetingStatus.Published)]
    public async Task Assigned_secretary_reads_detail_and_rejection_reason_without_transfer_ids(MeetingStatus status)
    {
        var meeting = GivenMeeting(status);
        var secretary = Participant(meeting, active: true, InvitationResponse.Accepted);
        secretary.Role = MeetingRole.Secretary;
        meeting.Participants.Add(secretary);
        meeting.StatusHistory.Add(new MeetingStatusHistory
        {
            MeetingId = meeting.Id,
            Action = MeetingHistoryAction.Reject,
            FromStatus = MeetingStatus.WaitingApproval,
            ToStatus = MeetingStatus.Rejected,
            ActorId = _approverId,
            Reason = "Lý do dành cho thư ký",
        });
        meeting.StatusHistory.Add(new MeetingStatusHistory
        {
            MeetingId = meeting.Id,
            Action = MeetingHistoryAction.TransferOrganizer,
            FromStatus = MeetingStatus.Approved,
            ToStatus = MeetingStatus.Approved,
            ActorId = Guid.CreateVersion7(),
            PreviousOrganizerId = Guid.CreateVersion7(),
            NewOrganizerId = _organizerId,
        });

        var history = await NewService().GetHistoryAsync(meeting.Id, secretary.UserId, 1, 20);
        var details = await NewService().GetAsync(meeting.Id, secretary.UserId);

        Assert.Equal(MeetingWorkflowOutcome.Success, history.Outcome);
        Assert.Equal("Lý do dành cho thư ký", Assert.Single(history.Page!.Items,
            item => item.Action == MeetingHistoryAction.Reject).RejectionReason);
        Assert.All(history.Page.Items, item =>
        {
            Assert.Null(item.PreviousOrganizerId);
            Assert.Null(item.NewOrganizerId);
        });
        Assert.Equal(MeetingOutcome.Success, details.Outcome);
    }

    [Theory]
    [InlineData("member")]
    [InlineData("globalSecretary")]
    [InlineData("otherMeeting")]
    [InlineData("delegated")]
    [InlineData("delegationTarget")]
    public async Task Invalid_secretary_relationship_does_not_grant_prepublication_history(string invalid)
    {
        var meeting = GivenMeeting(MeetingStatus.Rejected);
        var secretary = Participant(meeting, active: true, InvitationResponse.Accepted);
        secretary.Role = MeetingRole.Secretary;
        switch (invalid)
        {
            case "member": secretary.Role = MeetingRole.Member; break;
            case "globalSecretary":
                secretary.Role = MeetingRole.Member;
                secretary.User.RoleId = TrustCabinet.Domain.Access.BuiltInRoles.Secretary;
                break;
            case "otherMeeting": secretary.MeetingId = Guid.CreateVersion7(); break;
            case "delegated": secretary.Response = InvitationResponse.Delegated; break;
            case "delegationTarget": secretary.DelegatedToUserId = Guid.CreateVersion7(); break;
        }
        meeting.Participants.Add(secretary);

        var result = await NewService().GetHistoryAsync(meeting.Id, secretary.UserId, 1, 20);

        Assert.Equal(MeetingWorkflowOutcome.NotFound, result.Outcome);
        Assert.Null(result.Page);
        Assert.Empty(_repository.Notifications);
        Assert.Empty(_invitations.MeetingIds);
    }

    private static MeetingParticipant Participant(
        Meeting meeting, bool active, InvitationResponse response)
    {
        var user = new User
        {
            FullName = active ? "Participant Active" : "Participant Inactive",
            Email = $"{Guid.CreateVersion7():N}@trustcabinet.local",
            IsActive = active,
        };
        return new MeetingParticipant
        {
            MeetingId = meeting.Id,
            UserId = user.Id,
            User = user,
            Response = response,
        };
    }
}
