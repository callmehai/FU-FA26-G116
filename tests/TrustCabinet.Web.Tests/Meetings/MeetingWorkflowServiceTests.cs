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
    private readonly Guid _organizerId = Guid.CreateVersion7();
    private readonly Guid _approverId = Guid.CreateVersion7();

    private MeetingService NewService() => new(_repository, NullLogger<MeetingService>.Instance);

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
    public async Task Readiness_get_and_submit_use_the_same_agenda_validation()
    {
        var meeting = GivenMeeting();

        var readiness = await NewService().GetSubmissionReadinessAsync(meeting.Id, _organizerId);
        var submit = await NewService().TransitionAsync(
            meeting.Id,
            new MeetingTransitionCommand(MeetingTransitionAction.SubmitForApproval, _approverId),
            _organizerId);

        Assert.False(readiness.Ready);
        Assert.Contains("agendaItems", readiness.Errors.Keys);
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
    public async Task Publish_snapshots_only_active_non_delegated_participants_into_notifications_and_email_jobs()
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
        var email = Assert.Single(_repository.EmailMessages);
        Assert.Equal(active.UserId, email.RecipientUserId);
        Assert.Equal(meeting.Id, email.MeetingId);
        Assert.Equal(EmailStatus.Queued, email.Status);
        Assert.Null(email.SentAt);
        Assert.Equal(MeetingHistoryAction.Publish, meeting.StatusHistory.Single().Action);
        Assert.Equal(meeting.StatusHistory.Single().Id, email.OriginHistoryId);
        using var payload = System.Text.Json.JsonDocument.Parse(email.PayloadSnapshot!);
        Assert.Equal("Giao ban tuần", payload.RootElement.GetProperty("Title").GetString());
    }

    [Fact]
    public async Task Publish_truncates_a_long_invitation_subject_without_splitting_a_surrogate_pair()
    {
        var meeting = GivenMeeting(MeetingStatus.Approved);
        meeting.Title = new string('a', 290) + string.Concat(Enumerable.Repeat("📋", 5));
        Assert.Equal(300, meeting.Title.Length);
        meeting.Participants.Add(Participant(meeting, active: true, InvitationResponse.Pending));

        var result = await NewService().TransitionAsync(
            meeting.Id, new MeetingTransitionCommand(MeetingTransitionAction.Publish), _organizerId);

        Assert.Equal(MeetingWorkflowOutcome.Success, result.Outcome);
        var subject = Assert.Single(_repository.EmailMessages).Subject;
        Assert.StartsWith("Thư mời: ", subject);
        Assert.True(subject.Length <= 300);
        var utf8 = new System.Text.UTF8Encoding(encoderShouldEmitUTF8Identifier: false, throwOnInvalidBytes: true);
        Assert.True(utf8.GetByteCount(subject) > 0);
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
