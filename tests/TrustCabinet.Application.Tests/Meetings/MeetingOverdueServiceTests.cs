using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using TrustCabinet.Application.Abstractions;
using TrustCabinet.Application.Meetings;
using TrustCabinet.Application.Tests.Fakes;
using TrustCabinet.Domain.Entities;
using TrustCabinet.Domain.Enums;

namespace TrustCabinet.Application.Tests.Meetings;

public sealed class MeetingOverdueServiceTests
{
    private static readonly DateTimeOffset Now = new(2026, 11, 16, 8, 0, 0, TimeSpan.Zero);

    private readonly FakeMeetingRepository _meetings = new();
    private readonly FakeUnitOfWork _unitOfWork = new();
    private readonly FakeMeetingEffects _effects = new();

    private MeetingOverdueService Service()
    {
        var clock = new FixedClock(Now);
        var lifecycle = new MeetingLifecycleService(
            _meetings, _unitOfWork, _effects, _effects, _effects, _effects, _effects, _effects, _effects,
            clock, NullLogger<MeetingLifecycleService>.Instance);
        return new MeetingOverdueService(_meetings, _unitOfWork, lifecycle, Options.Create(new MeetingOverdueOptions()),
            clock, NullLogger<MeetingOverdueService>.Instance);
    }

    private Meeting GivenMeeting(MeetingStatus status, DateTimeOffset plannedStart)
    {
        var meeting = new Meeting
        {
            Title = "Họp giao ban",
            Status = status,
            CreatedById = Guid.CreateVersion7(),
            PlannedStartTime = plannedStart,
            PlannedEndTime = plannedStart.AddHours(1),
        };
        _meetings.Meetings.Add(meeting);
        return meeting;
    }

    [Fact]
    public async Task An_unstarted_meeting_a_day_past_its_start_is_cancelled_by_the_system_and_its_organizer_told()
    {
        var meeting = GivenMeeting(MeetingStatus.Published, Now.AddHours(-25));

        Assert.Equal(1, await Service().CancelOverdueAsync());

        Assert.Equal(MeetingStatus.Cancelled, meeting.Status);
        Assert.Null(meeting.CancelledById);
        Assert.Equal(MeetingStatus.Published, meeting.CancelledFromStatus);
        var history = Assert.Single(_meetings.History);
        Assert.Equal((MeetingHistoryAction.AutoCancel, (Guid?)null), (history.Action, history.ActorId));
        Assert.Equal(meeting.CreatedById, Assert.Single(_meetings.Notifications).UserId);
        Assert.Equal([meeting.Id], _meetings.Locked);
        Assert.Equal([TransactionMode.None, TransactionMode.ReadCommitted], _unitOfWork.Modes);
        Assert.Equal((1, 1), (_unitOfWork.Saves, _unitOfWork.Commits));
    }

    [Fact]
    public async Task A_meeting_rescheduled_before_its_lock_is_left_alone()
    {
        var meeting = GivenMeeting(MeetingStatus.Published, Now.AddHours(2));
        _meetings.Listed = [meeting.Id];

        Assert.Equal(0, await Service().CancelOverdueAsync());

        Assert.Equal(MeetingStatus.Published, meeting.Status);
        Assert.Empty(_meetings.History);
        Assert.Equal(0, _unitOfWork.Saves);
    }

    [Fact]
    public async Task A_store_failure_on_one_meeting_leaves_the_others_to_be_cancelled()
    {
        var first = GivenMeeting(MeetingStatus.Draft, Now.AddHours(-30));
        var second = GivenMeeting(MeetingStatus.Approved, Now.AddHours(-26));
        _unitOfWork.FailNextSave = new PersistenceUnavailableException();

        Assert.Equal(1, await Service().CancelOverdueAsync());

        Assert.Equal([first.Id, second.Id], _meetings.Locked);
        Assert.Equal(MeetingStatus.Cancelled, second.Status);
        Assert.Equal((1, 1, 1), (_unitOfWork.Saves, _unitOfWork.Commits, _unitOfWork.Rollbacks));
    }

    [Fact]
    public async Task A_meeting_left_running_a_day_past_its_end_is_ended_by_the_system()
    {
        var meeting = GivenMeeting(MeetingStatus.InProgress, Now.AddHours(-26));

        Assert.Equal(1, await Service().EndOverrunAsync());

        Assert.Equal(MeetingStatus.Ended, meeting.Status);
        Assert.Equal(MeetingOverdueService.EndReason(24), Assert.Single(_meetings.History).Reason);
        Assert.Equal([TransactionMode.None, TransactionMode.ReadCommitted], _unitOfWork.Modes);
    }
}
