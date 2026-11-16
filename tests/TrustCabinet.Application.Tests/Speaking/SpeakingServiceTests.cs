using Microsoft.Extensions.Logging.Abstractions;
using TrustCabinet.Application.Abstractions;
using TrustCabinet.Application.Participants;
using TrustCabinet.Application.Speaking;
using TrustCabinet.Application.Tests.Fakes;
using TrustCabinet.Domain.Entities;
using TrustCabinet.Domain.Enums;
using TrustCabinet.Domain.Participants;

namespace TrustCabinet.Application.Tests.Speaking;

/// <summary>SCRUM-178 (CA-13): the speaking queue (UC-79) over the unit of work, with every storage call faked.</summary>
public sealed class SpeakingServiceTests
{
    private static readonly DateTimeOffset Now = new(2026, 11, 16, 8, 0, 0, TimeSpan.Zero);

    private readonly FakeSpeakingRequestRepository _requests = new();
    private readonly FakeParticipantRepository _participants = new();
    private readonly FakeUnitOfWork _unitOfWork = new();
    private readonly FakeParticipantEffects _guests;
    private readonly FakeSpeakingChangePublisher _publisher;
    private readonly Meeting _meeting;
    private readonly Guid _chair = Guid.CreateVersion7();
    private readonly Guid _member = Guid.CreateVersion7();
    private readonly Guid _other = Guid.CreateVersion7();

    public SpeakingServiceTests()
    {
        _guests = new(_unitOfWork);
        _publisher = new(_unitOfWork);
        _meeting = new Meeting
        {
            Title = "Họp giao ban", Status = MeetingStatus.InProgress, CreatedById = Guid.CreateVersion7(),
            PlannedStartTime = Now.AddHours(-1), PlannedEndTime = Now.AddHours(1),
        };
        foreach (var (userId, role) in new[] { (_chair, MeetingRole.Chair), (_member, MeetingRole.Member), (_other, MeetingRole.Member) })
        {
            _meeting.Participants.Add(new MeetingParticipant
            {
                MeetingId = _meeting.Id, UserId = userId, Role = role, AssignmentStatus = AssignmentStatus.Current,
                Response = InvitationResponse.Accepted,
                User = new User { Id = userId, FullName = $"{role}", Email = $"{userId}@trustcabinet.local", IsActive = true },
            });
            _requests.ActiveUsers.Add(userId);
        }
        _participants.Snapshots[_meeting.Id] = new(_meeting, []);
    }

    private SpeakingService Service() => new(_requests, _participants, _guests, _publisher, _unitOfWork,
        new FixedClock(Now), NullLogger<SpeakingService>.Instance);

    private static ParticipantActor User(Guid userId) => new(UserId: userId);

    private SpeakingRequest Request(Guid userId, SpeakingRequestStatus status, int position)
    {
        var request = new SpeakingRequest
        {
            Id = Guid.CreateVersion7(), MeetingId = _meeting.Id, UserId = userId, Status = status,
            QueuePosition = position, RequestedAt = Now.AddMinutes(-10), CreatedAt = Now.AddMinutes(-10),
            UpdatedAt = Now.AddMinutes(-10),
        };
        _requests.Requests.Add(request);
        return request;
    }

    private Task<SpeakingOutcome> Mutate(Guid? requestId, Guid userId, SpeakingOperation operation)
        => Service().MutateAsync(_meeting.Id, requestId, User(userId), operation);

    [Fact]
    public async Task The_queue_is_read_in_one_repeatable_read_transaction()
    {
        Request(_member, SpeakingRequestStatus.Queued, 1);

        var outcome = await Service().ListAsync(_meeting.Id, User(_chair));

        Assert.Equal(SpeakingFailure.None, outcome.Failure);
        Assert.True(outcome.Data!.CanManage);
        Assert.Single(outcome.Data.Requests);
        Assert.Equal([TransactionMode.RepeatableRead], _unitOfWork.Modes);
        Assert.Equal([(_meeting.Id, false)], _participants.Loads);
    }

    [Fact]
    public async Task A_storage_failure_reading_the_queue_is_read_failed()
    {
        _requests.Fail = new PersistenceUnavailableException();

        var outcome = await Service().ListAsync(_meeting.Id, User(_chair));

        Assert.Equal(SpeakingFailure.ReadFailed, outcome.Failure);
    }

    [Fact]
    public async Task An_inactive_account_is_refused()
    {
        _requests.ActiveUsers.Remove(_member);

        Assert.Equal(SpeakingFailure.Inactive, (await Service().ListAsync(_meeting.Id, User(_member))).Failure);
    }

    [Fact]
    public async Task Raising_a_hand_queues_it_last_saves_commits_then_tells_the_panels()
    {
        Request(_other, SpeakingRequestStatus.Cancelled, 3);
        _requests.InRoom.Add(_member);
        _requests.CurrentAgendaItemId = Guid.CreateVersion7();

        var outcome = await Mutate(null, _member, SpeakingOperation.Raise);

        Assert.Equal(SpeakingFailure.None, outcome.Failure);
        Assert.True(outcome.Created);
        var raised = Assert.Single(_requests.Added);
        Assert.Equal((4, SpeakingRequestStatus.Queued, _requests.CurrentAgendaItemId), (raised.QueuePosition, raised.Status, raised.AgendaItemId));
        // The meeting row first, then the caller's account, as before.
        Assert.Equal([(_meeting.Id, true)], _participants.Loads);
        Assert.Equal([_member], _participants.LockedUsers);
        Assert.Equal([TransactionMode.ReadCommitted], _unitOfWork.Modes);
        Assert.Equal((1, 1, 0), (_unitOfWork.Saves, _unitOfWork.Commits, _unitOfWork.Rollbacks));
        var (change, invited, commits) = Assert.Single(_publisher.Published);
        Assert.Equal(("Raise", raised.Id, 1), (change.ChangeType, change.RequestId, commits));
        Assert.Null(invited);
    }

    [Fact]
    public async Task A_hand_already_up_stays_and_sends_no_event()
    {
        Request(_member, SpeakingRequestStatus.Queued, 1);
        _requests.InRoom.Add(_member);

        var outcome = await Mutate(null, _member, SpeakingOperation.Raise);

        Assert.Equal((SpeakingFailure.None, false), (outcome.Failure, outcome.Created));
        Assert.Empty(_requests.Added);
        Assert.Equal(1, _unitOfWork.Commits);
        Assert.Empty(_publisher.Published);
    }

    [Fact]
    public async Task Raising_a_hand_outside_the_room_is_refused_and_rolled_back()
    {
        var outcome = await Mutate(null, _member, SpeakingOperation.Raise);

        Assert.Equal(SpeakingFailure.NotInRoom, outcome.Failure);
        Assert.Equal((0, 0, 1), (_unitOfWork.Saves, _unitOfWork.Commits, _unitOfWork.Rollbacks));
        Assert.Empty(_publisher.Published);
    }

    [Fact]
    public async Task Only_the_chair_invites()
    {
        var queued = Request(_other, SpeakingRequestStatus.Queued, 1);

        Assert.Equal(SpeakingFailure.NotChair, (await Mutate(queued.Id, _member, SpeakingOperation.Invite)).Failure);
        Assert.Equal(SpeakingRequestStatus.Queued, queued.Status);
    }

    [Fact]
    public async Task Inviting_while_someone_holds_the_floor_is_floor_taken()
    {
        Request(_other, SpeakingRequestStatus.Speaking, 1);
        var queued = Request(_member, SpeakingRequestStatus.Queued, 2);

        var outcome = await Mutate(queued.Id, _chair, SpeakingOperation.Invite);

        Assert.Equal(SpeakingFailure.FloorTaken, outcome.Failure);
        Assert.Equal(SpeakingRequestStatus.Queued, queued.Status);
        Assert.Equal(1, _unitOfWork.Rollbacks);
    }

    [Fact]
    public async Task An_invitation_tells_the_invited_person()
    {
        var queued = Request(_member, SpeakingRequestStatus.Queued, 1);

        var outcome = await Mutate(queued.Id, _chair, SpeakingOperation.Invite);

        Assert.Equal(SpeakingFailure.None, outcome.Failure);
        Assert.Equal(SpeakingRequestStatus.Invited, queued.Status);
        var (change, invited, commits) = Assert.Single(_publisher.Published);
        Assert.Equal(("Invite", new SpeakingViewer(_member, null), 1), (change.ChangeType, invited, commits));
    }

    [Fact]
    public async Task Moving_swaps_with_the_neighbour_and_at_the_end_of_the_queue_sends_nothing()
    {
        var first = Request(_member, SpeakingRequestStatus.Queued, 1);
        var second = Request(_other, SpeakingRequestStatus.Queued, 2);

        Assert.Equal(SpeakingFailure.None, (await Mutate(first.Id, _chair, SpeakingOperation.MoveUp)).Failure);
        Assert.Empty(_publisher.Published);

        Assert.Equal(SpeakingFailure.None, (await Mutate(second.Id, _chair, SpeakingOperation.MoveUp)).Failure);
        Assert.Equal((2, 1), (first.QueuePosition, second.QueuePosition));
        Assert.Equal("MoveUp", Assert.Single(_publisher.Published).Change.ChangeType);
    }

    [Fact]
    public async Task The_floor_taken_by_a_concurrent_invitation_is_floor_taken()
    {
        var queued = Request(_member, SpeakingRequestStatus.Queued, 1);
        _unitOfWork.FailNextSave = new UniqueConflictException(UniqueConflict.SpeakingFloorTaken);

        var outcome = await Mutate(queued.Id, _chair, SpeakingOperation.Invite);

        Assert.Equal(SpeakingFailure.FloorTaken, outcome.Failure);
        Assert.Equal((1, 0, 1), (_unitOfWork.Discards, _unitOfWork.Commits, _unitOfWork.Rollbacks));
        Assert.Empty(_publisher.Published);
    }

    [Fact]
    public async Task Two_presses_at_once_answer_with_the_queue_as_the_first_left_it()
    {
        _requests.InRoom.Add(_member);
        _unitOfWork.FailNextSave = new UniqueConflictException(UniqueConflict.SpeakingRequestAlreadyOpen);

        var outcome = await Mutate(null, _member, SpeakingOperation.Raise);

        // UC-79 EX-2: the hand is raised either way, and the queue is read again in a transaction of its own.
        Assert.Equal(SpeakingFailure.None, outcome.Failure);
        Assert.NotNull(outcome.Data);
        Assert.Equal([TransactionMode.ReadCommitted, TransactionMode.RepeatableRead], _unitOfWork.Modes);
        Assert.Equal(1, _unitOfWork.Discards);
        Assert.Empty(_publisher.Published);
    }

    [Fact]
    public async Task Any_other_duplicate_is_save_failed()
    {
        _requests.InRoom.Add(_member);
        _unitOfWork.FailNextSave = new UniqueConflictException(UniqueConflict.Unknown);

        Assert.Equal(SpeakingFailure.SaveFailed, (await Mutate(null, _member, SpeakingOperation.Raise)).Failure);
        Assert.Equal(1, _unitOfWork.Discards);
    }

    [Fact]
    public async Task A_storage_failure_while_saving_is_save_failed_and_nothing_is_kept()
    {
        _requests.InRoom.Add(_member);
        _unitOfWork.FailNextSave = new PersistenceUnavailableException();

        var outcome = await Mutate(null, _member, SpeakingOperation.Raise);

        Assert.Equal(SpeakingFailure.SaveFailed, outcome.Failure);
        Assert.Equal((1, 0, 1), (_unitOfWork.Discards, _unitOfWork.Commits, _unitOfWork.Rollbacks));
        Assert.Empty(_publisher.Published);
    }
}
