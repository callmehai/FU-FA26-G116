using Microsoft.Extensions.Logging.Abstractions;
using TrustCabinet.Application.Abstractions;
using TrustCabinet.Application.Meetings;
using TrustCabinet.Application.Tests.Fakes;
using TrustCabinet.Domain.Entities;
using TrustCabinet.Domain.Enums;

namespace TrustCabinet.Application.Tests.Meetings;

public sealed class MeetingLifecycleServiceTests
{
    private static readonly DateTimeOffset Now = new(2026, 11, 16, 8, 0, 0, TimeSpan.Zero);

    private readonly FakeMeetingRepository _meetings = new();
    private readonly FakeUnitOfWork _unitOfWork = new();
    private readonly FakeMeetingEffects _effects = new();
    private readonly Guid _chair = Guid.CreateVersion7();
    private readonly Guid _secretary = Guid.CreateVersion7();
    private readonly Guid _member = Guid.CreateVersion7();

    private MeetingLifecycleService Service() => new(
        _meetings, _unitOfWork, _effects, _effects, _effects, _effects, _effects, _effects, _effects,
        new FixedClock(Now), NullLogger<MeetingLifecycleService>.Instance);

    private Meeting GivenMeeting(MeetingStatus status)
    {
        var meeting = new Meeting
        {
            Title = "Họp giao ban",
            Status = status,
            Mode = MeetingMode.Online,
            CreatedById = Guid.CreateVersion7(),
            PlannedStartTime = Now.AddHours(-1),
            PlannedEndTime = Now.AddHours(1),
        };
        foreach (var (userId, role) in new[] { (_chair, MeetingRole.Chair), (_secretary, MeetingRole.Secretary), (_member, MeetingRole.Member) })
        {
            meeting.Participants.Add(new MeetingParticipant
            {
                MeetingId = meeting.Id, UserId = userId, Role = role,
                User = new User { Id = userId, FullName = $"{role}", Email = $"{userId}@trustcabinet.local", IsActive = true },
            });
        }
        _meetings.Meetings.Add(meeting);
        return meeting;
    }

    private static MeetingTransitionCommand Command(MeetingTransitionAction action) => new(action);

    [Fact]
    public async Task Start_commits_then_tells_everyone_but_the_chair()
    {
        var meeting = GivenMeeting(MeetingStatus.Published);

        var result = await Service().TransitionAsync(meeting.Id, Command(MeetingTransitionAction.Start), _chair);

        Assert.Equal(MeetingWorkflowOutcome.Success, result.Outcome);
        Assert.Equal(MeetingStatus.InProgress, meeting.Status);
        Assert.Equal([meeting.Id], _meetings.Locked);
        Assert.Equal(MeetingHistoryAction.Start, Assert.Single(_meetings.History).Action);
        Assert.Equal([TransactionMode.ReadCommitted], _unitOfWork.Modes);
        Assert.Equal((1, 1), (_unitOfWork.Saves, _unitOfWork.Commits));
        Assert.Equal(["notifications.started"], _effects.Calls);
        Assert.DoesNotContain(_chair, _effects.StartedRecipients!);
    }

    [Fact]
    public async Task End_prepares_the_modules_inside_the_transaction_and_publishes_after_the_commit()
    {
        var meeting = GivenMeeting(MeetingStatus.InProgress);

        var result = await Service().TransitionAsync(meeting.Id, Command(MeetingTransitionAction.End), _chair);

        Assert.Equal(MeetingWorkflowOutcome.Success, result.Outcome);
        Assert.Equal(MeetingStatus.Ended, meeting.Status);
        Assert.NotNull(meeting.ActualEndedAt);
        Assert.Equal(
            ["agenda.prepare", "speaking.prepare", "votes.deleteDrafts", "agenda.publish:MeetingEnded", "room.close"],
            _effects.Calls);
        Assert.Equal((1, 1), (_unitOfWork.Saves, _unitOfWork.Commits));
    }

    [Fact]
    public async Task Someone_other_than_the_chair_is_refused()
    {
        var meeting = GivenMeeting(MeetingStatus.Published);

        var result = await Service().TransitionAsync(meeting.Id, Command(MeetingTransitionAction.Start), _member);

        Assert.Equal(MeetingWorkflowOutcome.Forbidden, result.Outcome);
        Assert.Equal(MeetingStatus.Published, meeting.Status);
        Assert.Equal(0, _unitOfWork.Saves);
        Assert.Empty(_effects.Calls);
    }

    [Theory]
    [InlineData(MeetingStatus.InProgress, MeetingTransitionAction.Start)]
    [InlineData(MeetingStatus.Ended, MeetingTransitionAction.Start)]
    [InlineData(MeetingStatus.Published, MeetingTransitionAction.End)]
    [InlineData(MeetingStatus.Ended, MeetingTransitionAction.End)]
    public async Task A_transition_from_the_wrong_status_is_a_conflict(MeetingStatus status, MeetingTransitionAction action)
    {
        var meeting = GivenMeeting(status);

        var result = await Service().TransitionAsync(meeting.Id, Command(action), _chair);

        Assert.Equal(MeetingWorkflowOutcome.StatusConflict, result.Outcome);
        Assert.Equal(status, meeting.Status);
        Assert.Empty(_meetings.History);
        Assert.Equal(0, _unitOfWork.Saves);
    }

    [Fact]
    public async Task A_store_failure_answers_save_failed_and_runs_no_after_commit_effect()
    {
        var meeting = GivenMeeting(MeetingStatus.InProgress);
        _unitOfWork.FailNextSave = new PersistenceUnavailableException();

        var result = await Service().TransitionAsync(meeting.Id, Command(MeetingTransitionAction.End), _chair);

        Assert.Equal(MeetingWorkflowOutcome.SaveFailed, result.Outcome);
        Assert.Equal((0, 1), (_unitOfWork.Commits, _unitOfWork.Rollbacks));
        Assert.DoesNotContain("agenda.publish:MeetingEnded", _effects.Calls);
        Assert.DoesNotContain("room.close", _effects.Calls);
    }

    [Fact]
    public async Task The_system_ends_an_overrunning_meeting_with_no_actor()
    {
        var meeting = GivenMeeting(MeetingStatus.InProgress);

        Assert.True(await Service().EndOverrunAsync(meeting.Id, cutoff: Now.AddHours(2), "Quá giờ"));

        Assert.Equal(MeetingStatus.Ended, meeting.Status);
        var history = Assert.Single(_meetings.History);
        Assert.Equal((MeetingHistoryAction.AutoEnd, (Guid?)null, "Quá giờ"), (history.Action, history.ActorId, history.Reason));
        Assert.Equal([TransactionMode.ReadCommitted], _unitOfWork.Modes);
    }
}
