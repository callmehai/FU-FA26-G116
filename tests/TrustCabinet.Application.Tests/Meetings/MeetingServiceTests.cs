using Microsoft.Extensions.Logging.Abstractions;
using TrustCabinet.Application.Abstractions;
using TrustCabinet.Application.Meetings;
using TrustCabinet.Application.Tests.Fakes;
using TrustCabinet.Domain.Entities;
using TrustCabinet.Domain.Enums;
using TrustCabinet.Domain.Meetings;

namespace TrustCabinet.Application.Tests.Meetings;

public sealed class MeetingServiceTests
{
    private static readonly DateTimeOffset Now = new(2026, 11, 16, 8, 0, 0, TimeSpan.Zero);

    private readonly FakeMeetingRepository _meetings = new();
    private readonly FakeUnitOfWork _unitOfWork = new();
    private readonly Guid _organizer = Guid.CreateVersion7();
    private readonly Guid _approver = Guid.CreateVersion7();

    private readonly FakeMeetingInvitationStager _invitations = new();

    private MeetingService Service() => new(_meetings, _invitations, _unitOfWork, NullLogger<MeetingService>.Instance, new FixedClock(Now));

    private Meeting GivenWaitingApproval()
    {
        var meeting = new Meeting
        {
            Title = "Họp giao ban",
            Status = MeetingStatus.WaitingApproval,
            CreatedById = _organizer,
            AssignedApproverId = _approver,
            PlannedStartTime = Now.AddDays(1),
            PlannedEndTime = Now.AddDays(1).AddHours(1),
        };
        _meetings.Meetings.Add(meeting);
        _meetings.Capabilities.Add((_approver, Capability.MeetingApprover));
        return meeting;
    }

    private static MeetingTransitionCommand Approve() => new(MeetingTransitionAction.Approve);

    [Fact]
    public async Task Rescheduling_a_published_meeting_emails_the_time_it_had_before_the_change()
    {
        var oldStart = Now.AddDays(1);
        var meeting = new Meeting
        {
            Title = "Họp giao ban", Status = MeetingStatus.Published, CreatedById = _organizer, Mode = MeetingMode.InPerson,
            Location = "Phòng 1", Sensitivity = Sensitivity.Normal, Description = "",
            PlannedStartTime = oldStart, PlannedEndTime = oldStart.AddHours(1),
        };
        _meetings.Meetings.Add(meeting);
        var newStart = Now.AddDays(2);
        var input = new MeetingInput("Họp giao ban", newStart.ToString("O"), newStart.AddHours(1).ToString("O"), "InPerson",
            "Phòng 1", null, "Normal", "");

        var result = await Service().UpdateAsync(meeting.Id, _ => Task.FromResult(input), _organizer);

        Assert.Equal(MeetingOutcome.Success, result.Outcome);
        Assert.Equal(newStart, meeting.PlannedStartTime);
        var staged = Assert.Single(_invitations.Rescheduled);
        Assert.Equal((meeting.Id, Assert.Single(_meetings.History).Id), (staged.MeetingId, staged.HistoryId));
        Assert.Equal((oldStart, oldStart.AddHours(1)), staged.Previous);
    }

    [Fact]
    public async Task Approve_locks_the_meeting_and_saves_history_and_notice_in_one_committed_transaction()
    {
        var meeting = GivenWaitingApproval();

        var result = await Service().TransitionAsync(meeting.Id, Approve(), _approver);

        Assert.Equal(MeetingWorkflowOutcome.Success, result.Outcome);
        Assert.Equal(MeetingStatus.Approved, meeting.Status);
        Assert.Equal([meeting.Id], _meetings.Locked);
        var history = Assert.Single(_meetings.History);
        Assert.Equal(MeetingHistoryAction.Approve, history.Action);
        Assert.Equal(_organizer, Assert.Single(_meetings.Notifications).UserId);
        Assert.Equal([TransactionMode.ReadCommitted], _unitOfWork.Modes);
        Assert.Equal((1, 1, 0), (_unitOfWork.Saves, _unitOfWork.Commits, _unitOfWork.Rollbacks));
    }

    [Fact]
    public async Task Approve_by_someone_other_than_the_assigned_approver_is_refused_and_saves_nothing()
    {
        var meeting = GivenWaitingApproval();
        var other = Guid.CreateVersion7();
        _meetings.Capabilities.Add((other, Capability.MeetingApprover));
        meeting.Participants.Add(new MeetingParticipant
        {
            MeetingId = meeting.Id, UserId = other, Role = MeetingRole.Chair,
            User = new User { Id = other, FullName = "Chủ toạ", Email = "chutoa@trustcabinet.local", IsActive = true },
        });

        var result = await Service().TransitionAsync(meeting.Id, Approve(), other);

        Assert.Equal(MeetingWorkflowOutcome.Forbidden, result.Outcome);
        Assert.Equal(MeetingStatus.WaitingApproval, meeting.Status);
        Assert.Empty(_meetings.History);
        Assert.Equal(0, _unitOfWork.Saves);
    }

    [Fact]
    public async Task A_store_failure_on_save_answers_save_failed_and_rolls_back()
    {
        var meeting = GivenWaitingApproval();
        _unitOfWork.FailNextSave = new PersistenceUnavailableException();

        var result = await Service().TransitionAsync(meeting.Id, Approve(), _approver);

        Assert.Equal(MeetingWorkflowOutcome.SaveFailed, result.Outcome);
        Assert.Equal((0, 0, 1), (_unitOfWork.Saves, _unitOfWork.Commits, _unitOfWork.Rollbacks));
    }

    private static MeetingInput Input(string title) => new(
        title, Now.AddDays(2).ToString("O"), Now.AddDays(2).AddHours(1).ToString("O"), null, null, null, null, null);

    [Fact]
    public async Task Create_locks_the_invitees_and_adds_the_meeting_in_one_committed_transaction()
    {
        var chair = Guid.CreateVersion7();
        var member = Guid.CreateVersion7();

        var result = await Service().CreateAsync(Input("Họp giao ban"), _organizer,
            [new ParticipantSeed(chair, nameof(MeetingRole.Chair)), new ParticipantSeed(member, nameof(MeetingRole.Member))]);

        Assert.Equal(MeetingOutcome.Success, result.Outcome);
        Assert.Equal([chair, member], _meetings.LockedUsers);
        Assert.Equal(2, Assert.Single(_meetings.Added).Participants.Count);
        Assert.Equal([TransactionMode.ReadCommitted], _unitOfWork.Modes);
        Assert.Equal((1, 1), (_unitOfWork.Saves, _unitOfWork.Commits));
    }

    [Fact]
    public async Task Update_locks_the_meeting_and_saves_in_one_committed_transaction()
    {
        var meeting = new Meeting
        {
            Title = "Họp cũ", Status = MeetingStatus.Draft, CreatedById = _organizer,
            PlannedStartTime = Now.AddDays(1), PlannedEndTime = Now.AddDays(1).AddHours(1),
        };
        _meetings.Meetings.Add(meeting);

        var result = await Service().UpdateAsync(meeting.Id, Input("Họp mới"), _organizer);

        Assert.Equal(MeetingOutcome.Success, result.Outcome);
        Assert.Equal("Họp mới", meeting.Title);
        Assert.Equal([meeting.Id], _meetings.Locked);
        Assert.Equal([TransactionMode.ReadCommitted], _unitOfWork.Modes);
        Assert.Equal((1, 1), (_unitOfWork.Saves, _unitOfWork.Commits));
    }

    [Fact]
    public async Task History_is_read_inside_a_transaction_as_before()
    {
        var meeting = GivenWaitingApproval();

        var result = await Service().GetHistoryAsync(meeting.Id, _organizer, page: 1, pageSize: 20);

        Assert.Equal(MeetingWorkflowOutcome.Success, result.Outcome);
        Assert.Equal([TransactionMode.ReadCommitted], _unitOfWork.Modes);
        Assert.Equal(0, _unitOfWork.Saves);
    }

    [Fact]
    public async Task Reading_a_meeting_opens_no_transaction()
    {
        var meetingId = Guid.CreateVersion7();
        _meetings.Accessible.Add((meetingId, _organizer));

        var result = await Service().GetAsync(meetingId, _organizer);

        Assert.Equal(MeetingOutcome.Success, result.Outcome);
        Assert.Equal([TransactionMode.None], _unitOfWork.Modes);
        Assert.Equal((0, 0), (_unitOfWork.Commits, _unitOfWork.Rollbacks));
    }
}
