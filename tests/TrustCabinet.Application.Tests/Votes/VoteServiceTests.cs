using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using TrustCabinet.Application.Abstractions;
using TrustCabinet.Application.Documents;
using TrustCabinet.Application.Participants;
using TrustCabinet.Application.Tests.Fakes;
using TrustCabinet.Application.Votes;
using TrustCabinet.Domain.Entities;
using TrustCabinet.Domain.Enums;
using TrustCabinet.Domain.Participants;
using TrustCabinet.Domain.Votes;

namespace TrustCabinet.Application.Tests.Votes;

/// <summary>SCRUM-177 (CA-12): the vote use cases on the ports, without a database.</summary>
public sealed class VoteServiceTests
{
    private static readonly DateTimeOffset Now = new(2026, 11, 16, 2, 0, 0, TimeSpan.Zero);

    private readonly FakeVoteRepository _votes = new();
    private readonly FakeParticipantRepository _participants = new();
    private readonly FakeUnitOfWork _unitOfWork = new();
    private readonly FakeParticipantEffects _guests;
    private readonly FakeVoteChangePublisher _publisher;
    private readonly Guid _meeting = Guid.CreateVersion7();
    private readonly Guid _chair = Guid.CreateVersion7();
    private readonly Guid _member = Guid.CreateVersion7();

    public VoteServiceTests()
    {
        _guests = new FakeParticipantEffects(_unitOfWork);
        _publisher = new FakeVoteChangePublisher(_unitOfWork);
        SeedMeeting(MeetingStatus.InProgress);
    }

    private VoteService Service() => new(_votes, _participants, _guests, new FakeMeetingDocumentAttacher(),
        new StoredFileWriter(new FakeStoredFileRepository(), new FakeFileStorage(), _unitOfWork, new FixedClock(Now),
            Options.Create(new FileStorageOptions()), NullLogger<StoredFileWriter>.Instance),
        _publisher, _unitOfWork, new FixedClock(Now), NullLogger<VoteService>.Instance);

    private void SeedMeeting(MeetingStatus status)
    {
        var meeting = new Meeting
        {
            Id = _meeting, Title = "Họp giao ban", Status = status, Sensitivity = Sensitivity.Normal,
            PlannedStartTime = Now.AddHours(-1), PlannedEndTime = Now.AddHours(1), CreatedAt = Now, UpdatedAt = Now,
        };
        meeting.Participants.Add(Participant(_chair, MeetingRole.Chair));
        meeting.Participants.Add(Participant(_member, MeetingRole.Member));
        _participants.Snapshots[_meeting] = new ParticipantSnapshot(meeting, []);
        _votes.ActiveUsers.UnionWith([_chair, _member]);
    }

    private MeetingParticipant Participant(Guid userId, MeetingRole role) => new()
    {
        MeetingId = _meeting, UserId = userId, Role = role, AssignmentStatus = AssignmentStatus.Current,
        Response = InvitationResponse.Accepted,
        User = new User { Id = userId, FullName = role.ToString(), Email = $"{userId}@trustcabinet.local", IsActive = true },
    };

    private VotingSession SeedVote(VotingStatus status, bool isAnonymous = false)
    {
        var vote = new VotingSession
        {
            MeetingId = _meeting, Title = "Thông qua kế hoạch", Status = status, IsAnonymous = isAnonymous,
            EligibleRoles = [MeetingRole.Chair, MeetingRole.Member], CreatedById = _chair, CreatedAt = Now, UpdatedAt = Now,
        };
        vote.Options.Add(new VotingOption { VotingSessionId = vote.Id, Position = 1, Label = "Đồng ý" });
        vote.Options.Add(new VotingOption { VotingSessionId = vote.Id, Position = 2, Label = "Không đồng ý" });
        _votes.Votes[vote.Id] = vote;
        return vote;
    }

    private static VoteInput Draft(bool openImmediately = false) => new(Title: "Thông qua kế hoạch", Description: "",
        Options: ["Đồng ý", "Không đồng ý"], IsAnonymous: false, EligibleRoles: ["Chair", "Member"],
        OpenImmediately: openImmediately);

    [Fact]
    public async Task Listing_reads_in_one_repeatable_read_transaction()
    {
        _votes.Rows.Add(new VoteRow(Guid.CreateVersion7(), "Biểu quyết", "", VotingStatus.Open, false, [MeetingRole.Member],
            Now, null, 2, 0, 0, true, false, null, "", null, null, null));

        var outcome = await Service().ListAsync(_meeting, new ParticipantActor(_member));

        Assert.Equal(VoteFailure.None, outcome.Failure);
        Assert.Single(Assert.IsType<VoteListSnapshot>(outcome.Data).Votes);
        Assert.Equal([TransactionMode.RepeatableRead], _unitOfWork.Modes);
    }

    [Fact]
    public async Task Listing_maps_a_storage_failure_to_list_read_failed()
    {
        _votes.Fail = new PersistenceUnavailableException();

        var outcome = await Service().ListAsync(_meeting, new ParticipantActor(_member));

        Assert.Equal(VoteFailure.ListReadFailed, outcome.Failure);
        Assert.Equal(1, _unitOfWork.Rollbacks);
    }

    [Fact]
    public async Task Creating_a_draft_stages_it_saves_commits_then_tells_the_managers()
    {
        var outcome = await Service().MutateAsync(_meeting, null, new ParticipantActor(_chair), VoteOperation.Create, Draft());

        Assert.Equal(VoteFailure.None, outcome.Failure);
        var vote = Assert.Single(_votes.Added.OfType<VotingSession>());
        Assert.Equal(outcome.VoteId, vote.Id);
        Assert.Equal(["Đồng ý", "Không đồng ý"], vote.Options.OrderBy(o => o.Position).Select(o => o.Label));
        Assert.Equal(["lock-meeting:exclusive", "lock-user"], _votes.Calls);
        Assert.Equal([TransactionMode.ReadCommitted], _unitOfWork.Modes);
        Assert.Equal((1, 1, 0), (_unitOfWork.Saves, _unitOfWork.Commits, _unitOfWork.Rollbacks));
        var (change, managersOnly, commits) = Assert.Single(_publisher.Published);
        Assert.Equal(("DraftCreated", true, 1), (change.ChangeType, managersOnly, commits));
    }

    [Fact]
    public async Task A_member_may_not_create_a_draft_and_nothing_is_saved()
    {
        var outcome = await Service().MutateAsync(_meeting, null, new ParticipantActor(_member), VoteOperation.Create, Draft());

        Assert.Equal(VoteFailure.DraftDenied, outcome.Failure);
        Assert.Empty(_votes.Added);
        Assert.Equal((0, 0, 1), (_unitOfWork.Saves, _unitOfWork.Commits, _unitOfWork.Rollbacks));
        Assert.Empty(_publisher.Published);
    }

    [Fact]
    public async Task Opening_from_the_form_that_fails_after_staging_rolls_back_without_saving()
    {
        // Guests only, and the meeting has none: the draft is staged, then nobody is eligible (UC-63 AF-3).
        var outcome = await Service().MutateAsync(_meeting, null, new ParticipantActor(_chair), VoteOperation.Create,
            Draft(openImmediately: true) with { EligibleRoles = ["Guest"] });

        Assert.Equal(VoteFailure.NoEligibleVoters, outcome.Failure);
        Assert.Single(_votes.Added.OfType<VotingSession>());
        Assert.Equal((0, 0, 1), (_unitOfWork.Saves, _unitOfWork.Commits, _unitOfWork.Rollbacks));
    }

    [Fact]
    public async Task Opening_fixes_the_eligible_voters_and_publishes_the_title()
    {
        var vote = SeedVote(VotingStatus.Draft);

        var outcome = await Service().MutateAsync(_meeting, vote.Id, new ParticipantActor(_chair), VoteOperation.Open, new());

        Assert.Equal(VoteFailure.None, outcome.Failure);
        Assert.Equal((VotingStatus.Open, 2), (vote.Status, vote.EligibleCount));
        Assert.Equal(new[] { _chair, _member }.Order(),
            _votes.Added.OfType<VoteEligibleVoter>().Select(e => e.UserId!.Value).Order());
        Assert.Equal(["lock-meeting:exclusive", "lock-user", "lock-vote:exclusive"], _votes.Calls);
        var (change, managersOnly, _) = Assert.Single(_publisher.Published);
        Assert.Equal(("Opened", vote.Title, false), (change.ChangeType, change.Title, managersOnly));
    }

    [Fact]
    public async Task A_secret_ballot_stores_a_receipt_and_a_count_but_no_choice()
    {
        var vote = SeedVote(VotingStatus.Open, isAnonymous: true);
        _votes.EligibleIn.Add(vote.Id);
        var option = vote.Options.First().Id;

        var outcome = await Service().MutateAsync(_meeting, vote.Id, new ParticipantActor(_member), VoteOperation.Cast,
            new VoteInput(OptionId: option));

        Assert.Equal(VoteFailure.None, outcome.Failure);
        Assert.Equal(_member, Assert.Single(_votes.Added.OfType<VoteReceipt>()).VoterId);
        Assert.Empty(_votes.Added.OfType<VoteChoice>());
        Assert.Equal((vote.Id, option), Assert.Single(_votes.CountedBallots));
        Assert.Equal(["lock-meeting:shared", "lock-user", "lock-vote:shared"], _votes.Calls);
        Assert.Equal("BallotCast", Assert.Single(_publisher.Published).Change.ChangeType);
    }

    [Fact]
    public async Task Closing_counts_the_result_before_the_commit()
    {
        var vote = SeedVote(VotingStatus.Open);
        _votes.Result = new VoteResult(1, [], []);
        int? commitsAtRead = null;
        _votes.OnResult = () => commitsAtRead = _unitOfWork.Commits;

        var outcome = await Service().MutateAsync(_meeting, vote.Id, new ParticipantActor(_chair), VoteOperation.Close, new());

        Assert.Equal(VoteFailure.None, outcome.Failure);
        Assert.Same(_votes.Result, outcome.Data);
        Assert.Equal(0, commitsAtRead);
        Assert.Equal((VotingStatus.Closed, Now), (vote.Status, vote.ClosedAt!.Value));
        Assert.Equal(1, _unitOfWork.Commits);
    }

    [Fact]
    public async Task A_storage_failure_on_save_discards_the_changes_and_answers_save_failed()
    {
        var vote = SeedVote(VotingStatus.Open);
        _unitOfWork.FailNextSave = new PersistenceUnavailableException();

        var outcome = await Service().MutateAsync(_meeting, vote.Id, new ParticipantActor(_chair), VoteOperation.Close, new());

        Assert.Equal(VoteFailure.SaveFailed, outcome.Failure);
        Assert.Equal((1, 1, 0), (_unitOfWork.Discards, _unitOfWork.Rollbacks, _unitOfWork.Commits));
        Assert.Empty(_publisher.Published);
    }

    [Fact]
    public async Task Another_open_vote_found_by_the_index_answers_another_open()
    {
        var vote = SeedVote(VotingStatus.Draft);
        _unitOfWork.FailNextSave = new UniqueConflictException(UniqueConflict.OpenVoteInMeeting);

        var outcome = await Service().MutateAsync(_meeting, vote.Id, new ParticipantActor(_chair), VoteOperation.Open, new());

        Assert.Equal(VoteFailure.AnotherOpen, outcome.Failure);
        Assert.Equal(1, _unitOfWork.Discards);
        Assert.Empty(_publisher.Published);
    }

    [Fact]
    public async Task A_second_ballot_found_by_the_index_answers_already_cast()
    {
        var vote = SeedVote(VotingStatus.Open);
        _votes.EligibleIn.Add(vote.Id);
        _unitOfWork.FailNextSave = new UniqueConflictException(UniqueConflict.BallotAlreadyCast);

        var outcome = await Service().MutateAsync(_meeting, vote.Id, new ParticipantActor(_member), VoteOperation.Cast,
            new VoteInput(OptionId: vote.Options.First().Id));

        Assert.Equal(VoteFailure.AlreadyCast, outcome.Failure);
        // The tally was bumped at once, before the save: only the rollback takes it back.
        Assert.Single(_votes.CountedBallots);
        Assert.Equal((1, 0, 1), (_unitOfWork.Discards, _unitOfWork.Commits, _unitOfWork.Rollbacks));
        Assert.Empty(_publisher.Published);
    }

    [Fact]
    public async Task Another_duplicate_is_a_save_failure()
    {
        var vote = SeedVote(VotingStatus.Draft);
        _unitOfWork.FailNextSave = new UniqueConflictException(UniqueConflict.Unknown);

        var outcome = await Service().MutateAsync(_meeting, vote.Id, new ParticipantActor(_chair), VoteOperation.Open, new());

        Assert.Equal(VoteFailure.SaveFailed, outcome.Failure);
    }

    [Fact]
    public async Task Confirming_a_read_records_it_under_the_shared_meeting_lock()
    {
        var document = Guid.CreateVersion7();

        var outcome = await Service().ConfirmReadAsync(_meeting, document, new ParticipantActor(_member),
            new VoteInput(LastPageReached: true));

        var view = Assert.IsType<DocumentReadView>(outcome.Data);
        Assert.Equal((document, Now, false), (view.MeetingDocumentId, view.ConfirmedAt, view.AlreadyConfirmed));
        Assert.Equal(_member, Assert.Single(_votes.Added.OfType<DocumentReadConfirmation>()).UserId);
        Assert.Equal(["lock-meeting:shared", "lock-user", "find-read-confirmation"], _votes.Calls);
        Assert.Equal((1, 1), (_unitOfWork.Saves, _unitOfWork.Commits));
    }

    [Fact]
    public async Task A_confirmation_raced_by_the_same_reader_answers_with_the_first_one()
    {
        var first = new DocumentReadConfirmation { MeetingDocumentId = Guid.CreateVersion7(), UserId = _member, ConfirmedAt = Now.AddSeconds(-1) };
        _votes.ReadConfirmations.Enqueue(null);
        _votes.ReadConfirmations.Enqueue(first);
        _unitOfWork.FailNextSave = new UniqueConflictException(UniqueConflict.DocumentAlreadyConfirmed);

        var outcome = await Service().ConfirmReadAsync(_meeting, first.MeetingDocumentId, new ParticipantActor(_member),
            new VoteInput(LastPageReached: true));

        var view = Assert.IsType<DocumentReadView>(outcome.Data);
        Assert.Equal((first.ConfirmedAt, true), (view.ConfirmedAt, view.AlreadyConfirmed));
        Assert.Equal(1, _unitOfWork.Discards);
        Assert.Equal([TransactionMode.ReadCommitted, TransactionMode.None], _unitOfWork.Modes);
    }

    [Fact]
    public async Task An_inactive_account_is_refused_before_anything_else()
    {
        _votes.ActiveUsers.Remove(_member);

        var outcome = await Service().ConfirmReadAsync(_meeting, Guid.CreateVersion7(), new ParticipantActor(_member),
            new VoteInput(LastPageReached: true));

        Assert.Equal(VoteFailure.Inactive, outcome.Failure);
        Assert.Empty(_votes.Added);
    }
}
