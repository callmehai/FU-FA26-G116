using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using TrustCabinet.Application.Abstractions;
using TrustCabinet.Application.Documents;
using TrustCabinet.Application.Participants;
using TrustCabinet.Application.Tests.Fakes;
using TrustCabinet.Application.Votes;
using TrustCabinet.Domain.Entities;
using TrustCabinet.Domain.Enums;
using TrustCabinet.Domain.Meetings;
using TrustCabinet.Domain.Participants;
using TrustCabinet.Domain.Votes;

namespace TrustCabinet.Application.Tests.Unit.Round1;

/// <summary>Round 1 unit tests for FE-11 (Voting): one test per kept UTC of the Vote.* sheets.</summary>
public sealed class VoteUnitRound1Tests
{
    private static readonly DateTimeOffset Now = new(2026, 11, 20, 3, 0, 0, TimeSpan.Zero);

    private readonly FakeVoteRepository _votes = new();
    private readonly FakeParticipantRepository _participants = new();
    private readonly FakeUnitOfWork _unitOfWork = new();
    private readonly FakeParticipantEffects _guests;
    private readonly FakeVoteChangePublisher _publisher;
    private readonly FakeRound1Logger<VoteService> _logger = new();
    private readonly Guid _meeting = Guid.CreateVersion7();
    private readonly Guid _chair = Guid.CreateVersion7();
    private readonly Guid _member = Guid.CreateVersion7();

    public VoteUnitRound1Tests()
    {
        _guests = new FakeParticipantEffects(_unitOfWork);
        _publisher = new FakeVoteChangePublisher(_unitOfWork);
        var meeting = new Meeting
        {
            Id = _meeting, Title = "Họp tổng kết", Status = MeetingStatus.InProgress, Sensitivity = Sensitivity.Normal,
            PlannedStartTime = Now.AddHours(-1), PlannedEndTime = Now.AddHours(1), CreatedAt = Now, UpdatedAt = Now,
        };
        meeting.Participants.Add(Participant(_chair, MeetingRole.Chair));
        meeting.Participants.Add(Participant(_member, MeetingRole.Member));
        _participants.Snapshots[_meeting] = new ParticipantSnapshot(meeting, []);
        _votes.ActiveUsers.UnionWith([_chair, _member]);
    }

    private VoteService Service() => new(_votes, _participants, _guests, new FakeMeetingDocumentAttacher(),
        new StoredFileWriter(new FakeStoredFileRepository(), new FakeFileStorage(), _unitOfWork, new FixedClock(Now),
            Options.Create(new FileStorageOptions()), NullLogger<StoredFileWriter>.Instance),
        _publisher, _unitOfWork, new FixedClock(Now), _logger);

    private MeetingParticipant Participant(Guid userId, MeetingRole role, bool active = true) => new()
    {
        MeetingId = _meeting, UserId = userId, Role = role, AssignmentStatus = AssignmentStatus.Current,
        Response = InvitationResponse.Accepted,
        User = new User { Id = userId, FullName = role.ToString(), Email = $"{userId}@trustcabinet.local", IsActive = active },
    };

    private VotingSession SeedVote(VotingStatus status, bool isAnonymous = false)
    {
        var vote = new VotingSession
        {
            MeetingId = _meeting, Title = "Chọn địa điểm họp", Status = status, IsAnonymous = isAnonymous,
            EligibleRoles = [MeetingRole.Chair, MeetingRole.Member], CreatedById = _chair, CreatedAt = Now, UpdatedAt = Now,
        };
        vote.Options.Add(new VotingOption { VotingSessionId = vote.Id, Position = 1, Label = "Hà Nội" });
        vote.Options.Add(new VotingOption { VotingSessionId = vote.Id, Position = 2, Label = "Đà Nẵng" });
        _votes.Votes[vote.Id] = vote;
        return vote;
    }

    private static VoteInput Draft(bool openImmediately = false) => new(Title: "Chọn địa điểm họp", Description: "",
        Options: ["Hà Nội", "Đà Nẵng"], IsAnonymous: false, EligibleRoles: ["Chair", "Member"],
        OpenImmediately: openImmediately);

    private Meeting ActiveMeeting(MeetingStatus status, Guid chairId, Guid memberId, bool memberActive = true)
    {
        var meeting = new Meeting
        {
            Id = _meeting, Title = "Họp tổng kết", Status = status, Sensitivity = Sensitivity.Normal,
            PlannedStartTime = Now.AddHours(-1), PlannedEndTime = Now.AddHours(1), CreatedAt = Now, UpdatedAt = Now,
        };
        meeting.Participants.Add(Participant(chairId, MeetingRole.Chair));
        meeting.Participants.Add(Participant(memberId, MeetingRole.Member, memberActive));
        return meeting;
    }

    // VoteRules.Validate (Vote.Validate/UTC001-UTC006)

    [Fact(DisplayName = "VoteRules.Validate/UTC001 valid draft passes with parsed roles")]
    [Trait("UTC", "VoteRules.Validate/UTC001")]
    [Trait("Trait", "UTC")]
    public void Validate_ValidDraftPasses()
    {
        var errors = VoteRules.Validate(Draft(), out var roles);

        Assert.Empty(errors);
        Assert.Equal([MeetingRole.Chair, MeetingRole.Member], roles);
    }

    [Fact(DisplayName = "VoteRules.Validate/UTC002 blank title is refused")]
    [Trait("UTC", "VoteRules.Validate/UTC002")]
    [Trait("Trait", "UTC")]
    public void Validate_BlankTitleRefused()
    {
        var errors = VoteRules.Validate(Draft() with { Title = "" }, out _);

        Assert.Equal(["Nội dung biểu quyết phải có từ 1 đến 300 ký tự."], errors["title"]);
    }

    [Fact(DisplayName = "VoteRules.Validate/UTC003 description over 4000 characters is refused")]
    [Trait("UTC", "VoteRules.Validate/UTC003")]
    [Trait("Trait", "UTC")]
    public void Validate_OverlongDescriptionRefused()
    {
        var errors = VoteRules.Validate(Draft() with { Description = new string('m', 4001) }, out _);

        Assert.Equal(["Mô tả không quá 4000 ký tự."], errors["description"]);
        Assert.DoesNotContain("description", VoteRules.Validate(Draft() with { Description = new string('m', 4000) }, out _));
    }

    [Fact(DisplayName = "VoteRules.Validate/UTC004 title over 300 characters is refused")]
    [Trait("UTC", "VoteRules.Validate/UTC004")]
    [Trait("Trait", "UTC")]
    public void Validate_OverlongTitleRefused()
    {
        var errors = VoteRules.Validate(Draft() with { Title = new string('t', 301) }, out _);

        Assert.Equal(["Nội dung biểu quyết phải có từ 1 đến 300 ký tự."], errors["title"]);
        Assert.DoesNotContain("title", VoteRules.Validate(Draft() with { Title = new string('t', 300) }, out _));
    }

    // VoteRules.EligibleUsers (Vote.EligibleUsers/UTC001-UTC004)

    [Fact(DisplayName = "VoteRules.EligibleUsers/UTC001 active role holders are eligible in order")]
    [Trait("UTC", "VoteRules.EligibleUsers/UTC001")]
    [Trait("Trait", "UTC")]
    public void EligibleUsers_ActiveHoldersEligible()
    {
        var meeting = ActiveMeeting(MeetingStatus.InProgress, _chair, _member);

        var eligible = VoteRules.EligibleUsers(meeting, [MeetingRole.Chair, MeetingRole.Member]);

        Assert.Equal(new[] { _chair, _member }.Order(), eligible);
    }

    [Fact(DisplayName = "VoteRules.EligibleUsers/UTC002 deactivated holder is excluded")]
    [Trait("UTC", "VoteRules.EligibleUsers/UTC002")]
    [Trait("Trait", "UTC")]
    public void EligibleUsers_DeactivatedExcluded()
    {
        var meeting = ActiveMeeting(MeetingStatus.InProgress, _chair, _member, memberActive: false);

        var eligible = VoteRules.EligibleUsers(meeting, [MeetingRole.Chair, MeetingRole.Member]);

        Assert.Equal([_chair], eligible);
    }

    [Fact(DisplayName = "VoteRules.EligibleUsers/UTC003 no holder means an empty list")]
    [Trait("UTC", "VoteRules.EligibleUsers/UTC003")]
    [Trait("Trait", "UTC")]
    public void EligibleUsers_NoHolderIsEmpty()
    {
        var meeting = ActiveMeeting(MeetingStatus.InProgress, _chair, _member);

        Assert.Empty(VoteRules.EligibleUsers(meeting, [MeetingRole.Guest]));
    }

    // VoteRules.Percentage (Vote.Percentage/UTC001-UTC004)

    [Fact(DisplayName = "VoteRules.Percentage/UTC001 one of three ballots is 33.33")]
    [Trait("UTC", "VoteRules.Percentage/UTC001")]
    [Trait("Trait", "UTC")]
    public void Percentage_PartialShare()
    {
        Assert.Equal(33.33, VoteRules.Percentage(1, 3));
    }

    [Fact(DisplayName = "VoteRules.Percentage/UTC002 no ballots shows nothing")]
    [Trait("UTC", "VoteRules.Percentage/UTC002")]
    [Trait("Trait", "UTC")]
    public void Percentage_NoBallotsIsNull()
    {
        Assert.Null(VoteRules.Percentage(0, 0));
    }

    [Fact(DisplayName = "VoteRules.Percentage/UTC003 rounding goes away from zero to 2 decimals")]
    [Trait("UTC", "VoteRules.Percentage/UTC003")]
    [Trait("Trait", "UTC")]
    public void Percentage_RoundsAwayFromZero()
    {
        Assert.Equal(16.67, VoteRules.Percentage(1, 6));
    }

    // VoteRules.Permissions (Vote.Permissions/UTC001-UTC005)

    [Fact(DisplayName = "VoteRules.Permissions/UTC001 chair after the end may only close and conclude")]
    [Trait("UTC", "VoteRules.Permissions/UTC001")]
    [Trait("Trait", "UTC")]
    public void Permissions_ChairAfterEnd()
    {
        var meeting = ActiveMeeting(MeetingStatus.Ended, _chair, _member);

        Assert.Equal(new VotePermissions(false, false, true, true),
            VoteRules.Permissions(meeting, new VoteViewer(_chair, null)));
    }

    [Fact(DisplayName = "VoteRules.Permissions/UTC002 secretary manages drafts but never opens")]
    [Trait("UTC", "VoteRules.Permissions/UTC002")]
    [Trait("Trait", "UTC")]
    public void Permissions_SecretaryManagesDrafts()
    {
        var meeting = ActiveMeeting(MeetingStatus.InProgress, _chair, _member);
        meeting.Participants.Add(Participant(Guid.CreateVersion7(), MeetingRole.Secretary));
        var secretary = meeting.Participants.Single(p => p.Role == MeetingRole.Secretary).UserId;

        Assert.Equal(new VotePermissions(true, false, false, false),
            VoteRules.Permissions(meeting, new VoteViewer(secretary, null)));
    }

    [Fact(DisplayName = "VoteRules.Permissions/UTC003 chair before the start only manages drafts")]
    [Trait("UTC", "VoteRules.Permissions/UTC003")]
    [Trait("Trait", "UTC")]
    public void Permissions_ChairBeforeStart()
    {
        var meeting = ActiveMeeting(MeetingStatus.Published, _chair, _member);

        Assert.Equal(new VotePermissions(true, false, false, false),
            VoteRules.Permissions(meeting, new VoteViewer(_chair, null)));
    }

    [Fact(DisplayName = "VoteRules.Permissions/UTC004 member may do nothing")]
    [Trait("UTC", "VoteRules.Permissions/UTC004")]
    [Trait("Trait", "UTC")]
    public void Permissions_MemberHasNothing()
    {
        var meeting = ActiveMeeting(MeetingStatus.InProgress, _chair, _member);

        Assert.Equal(new VotePermissions(false, false, false, false),
            VoteRules.Permissions(meeting, new VoteViewer(_member, null)));
    }

    // VoteService.MutateAsync (Vote.Mutate/UTC001-UTC008)

    [Fact(DisplayName = "VoteService.MutateAsync/UTC001 secretary creates a draft with positioned options")]
    [Trait("UTC", "VoteService.MutateAsync/UTC001")]
    [Trait("Trait", "UTC")]
    public async Task Mutate_CreateDraftSucceeds()
    {
        var outcome = await Service().MutateAsync(_meeting, null, new ParticipantActor(_chair),
            VoteOperation.Create, Draft());

        Assert.Equal(VoteFailure.None, outcome.Failure);
        var vote = Assert.Single(_votes.Added.OfType<VotingSession>());
        Assert.Equal(outcome.VoteId, vote.Id);
        Assert.Equal(["Hà Nội", "Đà Nẵng"], vote.Options.OrderBy(o => o.Position).Select(o => o.Label));
        Assert.Equal((1, 1, 0), (_unitOfWork.Saves, _unitOfWork.Commits, _unitOfWork.Rollbacks));
        var (change, managersOnly, _) = Assert.Single(_publisher.Published);
        Assert.Equal(("DraftCreated", true), (change.ChangeType, managersOnly));
        Assert.Empty(_logger.Records);
    }

    [Fact(DisplayName = "VoteService.MutateAsync/UTC002 eligible voter casts a public ballot")]
    [Trait("UTC", "VoteService.MutateAsync/UTC002")]
    [Trait("Trait", "UTC")]
    public async Task Mutate_CastPublicBallotSucceeds()
    {
        var vote = SeedVote(VotingStatus.Open);
        _votes.EligibleIn.Add(vote.Id);
        var option = vote.Options.First().Id;

        var outcome = await Service().MutateAsync(_meeting, vote.Id, new ParticipantActor(_member),
            VoteOperation.Cast, new VoteInput(OptionId: option));

        Assert.Equal(VoteFailure.None, outcome.Failure);
        Assert.Equal(_member, Assert.Single(_votes.Added.OfType<VoteReceipt>()).VoterId);
        Assert.Single(_votes.Added.OfType<VoteChoice>());
        Assert.Equal([(vote.Id, option)], _votes.CountedBallots);
        Assert.Equal("BallotCast", Assert.Single(_publisher.Published).Change.ChangeType);
        Assert.Empty(_logger.Records);
    }

    [Fact(DisplayName = "VoteService.MutateAsync/UTC003 late joiner is not eligible to cast")]
    [Trait("UTC", "VoteService.MutateAsync/UTC003")]
    [Trait("Trait", "UTC")]
    public async Task Mutate_LateJoinerNotEligible()
    {
        var vote = SeedVote(VotingStatus.Open);

        var outcome = await Service().MutateAsync(_meeting, vote.Id, new ParticipantActor(_member),
            VoteOperation.Cast, new VoteInput(OptionId: vote.Options.First().Id));

        Assert.Equal(VoteFailure.NotEligible, outcome.Failure);
        Assert.Empty(_votes.Added);
        Assert.Empty(_publisher.Published);
        Assert.Empty(_logger.Records);
    }

    [Fact(DisplayName = "VoteService.MutateAsync/UTC004 chair closes an open vote with its result")]
    [Trait("UTC", "VoteService.MutateAsync/UTC004")]
    [Trait("Trait", "UTC")]
    public async Task Mutate_CloseReturnsResult()
    {
        var vote = SeedVote(VotingStatus.Open);
        _votes.Result = new VoteResult(3, [], null);

        var outcome = await Service().MutateAsync(_meeting, vote.Id, new ParticipantActor(_chair),
            VoteOperation.Close, new());

        Assert.Equal(VoteFailure.None, outcome.Failure);
        Assert.Equal((VotingStatus.Closed, Now), (vote.Status, vote.ClosedAt!.Value));
        Assert.Same(_votes.Result, outcome.Data);
        Assert.Equal("Closed", Assert.Single(_publisher.Published).Change.ChangeType);
        Assert.Empty(_logger.Records);
    }

    [Fact(DisplayName = "VoteService.MutateAsync/UTC005 chair records the conclusion of a closed vote")]
    [Trait("UTC", "VoteService.MutateAsync/UTC005")]
    [Trait("Trait", "UTC")]
    public async Task Mutate_ConcludeSucceeds()
    {
        _participants.Snapshots[_meeting]!.Meeting.Status = MeetingStatus.Ended;
        var vote = SeedVote(VotingStatus.Closed);

        var outcome = await Service().MutateAsync(_meeting, vote.Id, new ParticipantActor(_chair),
            VoteOperation.RecordConclusion, new VoteInput(Conclusion: "Passed", Note: "  Thống nhất  "));

        Assert.Equal(VoteFailure.None, outcome.Failure);
        Assert.Equal(VoteConclusion.Passed, vote.Conclusion);
        Assert.Equal(("Thống nhất", _chair, Now),
            (vote.ConclusionNote, vote.ConcludedById, vote.ConcludedAt!.Value));
        Assert.Equal("ConclusionRecorded", Assert.Single(_publisher.Published).Change.ChangeType);
        Assert.Empty(_logger.Records);
    }

    // VoteService.ConfirmReadAsync (Vote.ConfirmRead/UTC001-UTC006)

    [Fact(DisplayName = "VoteService.ConfirmReadAsync/UTC001 eligible voter confirms reading")]
    [Trait("UTC", "VoteService.ConfirmReadAsync/UTC001")]
    [Trait("Trait", "UTC")]
    public async Task ConfirmRead_FirstConfirmationSaved()
    {
        var document = Guid.CreateVersion7();

        var outcome = await Service().ConfirmReadAsync(_meeting, document, new ParticipantActor(_member),
            new VoteInput(LastPageReached: true));

        Assert.Equal(VoteFailure.None, outcome.Failure);
        var view = Assert.IsType<DocumentReadView>(outcome.Data);
        Assert.Equal((document, Now, false), (view.MeetingDocumentId, view.ConfirmedAt, view.AlreadyConfirmed));
        Assert.Equal(_member, Assert.Single(_votes.Added.OfType<DocumentReadConfirmation>()).UserId);
        Assert.Equal((1, 1), (_unitOfWork.Saves, _unitOfWork.Commits));
        Assert.Empty(_logger.Records);
    }

    [Fact(DisplayName = "VoteService.ConfirmReadAsync/UTC002 repeated confirmation keeps the first time")]
    [Trait("UTC", "VoteService.ConfirmReadAsync/UTC002")]
    [Trait("Trait", "UTC")]
    public async Task ConfirmRead_RepeatIsIdempotent()
    {
        var document = Guid.CreateVersion7();
        _votes.ReadConfirmations.Enqueue(new DocumentReadConfirmation
        {
            MeetingDocumentId = document, UserId = _member, ConfirmedAt = Now.AddSeconds(-1),
        });

        var outcome = await Service().ConfirmReadAsync(_meeting, document, new ParticipantActor(_member),
            new VoteInput(LastPageReached: true));

        Assert.Equal(VoteFailure.None, outcome.Failure);
        var view = Assert.IsType<DocumentReadView>(outcome.Data);
        Assert.Equal((Now.AddSeconds(-1), true), (view.ConfirmedAt, view.AlreadyConfirmed));
        Assert.Empty(_votes.Added);
        Assert.Empty(_logger.Records);
    }

    [Fact(DisplayName = "VoteService.ConfirmReadAsync/UTC003 voter off the fixed list is refused")]
    [Trait("UTC", "VoteService.ConfirmReadAsync/UTC003")]
    [Trait("Trait", "UTC")]
    public async Task ConfirmRead_NotEligibleRefused()
    {
        _votes.ReadRequirement = (false, true);

        var outcome = await Service().ConfirmReadAsync(_meeting, Guid.CreateVersion7(),
            new ParticipantActor(_member), new VoteInput(LastPageReached: true));

        Assert.Equal(VoteFailure.NotEligible, outcome.Failure);
        Assert.Empty(_votes.Added);
        Assert.Empty(_logger.Records);
    }

    [Fact(DisplayName = "VoteService.ConfirmReadAsync/UTC004 reader short of the last page is refused")]
    [Trait("UTC", "VoteService.ConfirmReadAsync/UTC004")]
    [Trait("Trait", "UTC")]
    public async Task ConfirmRead_LastPageNotReachedRefused()
    {
        var outcome = await Service().ConfirmReadAsync(_meeting, Guid.CreateVersion7(),
            new ParticipantActor(_member), new VoteInput(LastPageReached: false));

        Assert.Equal(VoteFailure.LastPageNotReached, outcome.Failure);
        Assert.Empty(_votes.Added);
        Assert.Empty(_logger.Records);
    }

    /// <summary>Captures what the service logs, so a test can assert the log line of its UTC.</summary>
    private sealed class FakeRound1Logger<T> : ILogger<T>
    {
        public List<(LogLevel Level, string Message)> Records { get; } = [];

        public IDisposable BeginScope<TState>(TState state) where TState : notnull => NoScope.Instance;

        private sealed class NoScope : IDisposable
        {
            public static readonly NoScope Instance = new();

            public void Dispose()
            {
            }
        }

        public bool IsEnabled(LogLevel logLevel) => true;

        public void Log<TState>(LogLevel logLevel, EventId eventId, TState state, Exception? exception,
            Func<TState, Exception?, string> formatter)
            => Records.Add((logLevel, formatter(state, exception)));
    }
}
