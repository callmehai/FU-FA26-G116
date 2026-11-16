using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using TrustCabinet.Application.Abstractions;
using TrustCabinet.Application.Documents;
using TrustCabinet.Application.Participants;
using TrustCabinet.Application.Tests.Fakes;
using TrustCabinet.Domain.Enums;
using TrustCabinet.Domain.Participants;
using TrustCabinet.Domain.Documents;
using TrustCabinet.Domain.Entities;

namespace TrustCabinet.Application.Tests.Documents;

public sealed class MeetingDocumentServiceTests
{
    private static readonly DateTimeOffset Now = new(2026, 11, 4, 2, 0, 0, TimeSpan.Zero);

    private readonly FakeMeetingRepository _meetings = new();
    private readonly FakeMeetingDocumentRepository _documents = new();
    private readonly FakeMeetingDocumentAttacher _attacher = new();
    private readonly FakeStoredFileRepository _storedFiles = new();
    private readonly FakeFileStorage _storage = new();
    private readonly FakeUnitOfWork _unitOfWork = new();
    private readonly FakeAuditService _audit = new();
    private readonly FakeAgendaEffects _agenda;
    private readonly Guid _secretary = Guid.CreateVersion7();
    private readonly Guid _meeting = Guid.CreateVersion7();

    public MeetingDocumentServiceTests()
    {
        _agenda = new FakeAgendaEffects(_unitOfWork);
    }

    private MeetingDocumentService Service() => new(_meetings, _documents, _attacher,
        new StoredFileWriter(_storedFiles, _storage, _unitOfWork, new FixedClock(Now),
            Options.Create(new FileStorageOptions()), NullLogger<StoredFileWriter>.Instance),
        _storage, _audit, _agenda, _unitOfWork, new FixedClock(Now),
        NullLogger<MeetingDocumentService>.Instance);

    private void SeedManagerMeeting(MeetingStatus status = MeetingStatus.InProgress)
    {
        _meetings.Meetings.Add(new Meeting
        {
            Id = _meeting,
            Title = "Họp giao ban",
            Status = status,
            Sensitivity = Sensitivity.Normal,
            PlannedStartTime = Now.AddHours(-1),
            PlannedEndTime = Now.AddHours(1),
            CreatedAt = Now,
            UpdatedAt = Now,
            Participants =
            [
                new MeetingParticipant
                {
                    MeetingId = _meeting, UserId = _secretary, Role = MeetingRole.Secretary,
                    AssignmentStatus = AssignmentStatus.Current, Response = InvitationResponse.Accepted,
                    User = new User { Id = _secretary, FullName = "Thư ký", Email = "Thư ký@trustcabinet.local", IsActive = true },
                },
            ],
        });
        _meetings.ActiveUsers.Add(_secretary);
    }

    [Fact]
    public async Task Listing_groups_documents_and_votes_in_one_read()
    {
        SeedManagerMeeting();
        var groups = new MeetingDocumentList(_meeting, null, [], [], []);
        _documents.List = groups;
        _documents.VoteGroups.Add(new MeetingDocumentVoteGroup(Guid.CreateVersion7(), "Biểu quyết 1", "Open", true, []));

        var outcome = await Service().ListAsync(_meeting, new ParticipantActor(_secretary));

        Assert.Equal(DocumentReadFailure.None, outcome.Failure);
        Assert.NotNull(outcome.List);
        Assert.Single(outcome.List.Votes);
        Assert.Equal([TransactionMode.None], _unitOfWork.Modes);
        Assert.Equal(["list", "vote-groups"], _documents.Calls);
        Assert.Equal(0, _unitOfWork.Commits);
    }

    [Fact]
    public async Task Listing_answers_NotFound_for_a_meeting_the_reader_cannot_see()
    {
        SeedManagerMeeting();
        var outsider = Guid.CreateVersion7();

        var outcome = await Service().ListAsync(_meeting, new ParticipantActor(outsider));

        Assert.Equal(DocumentReadFailure.NotFound, outcome.Failure);
        Assert.Null(outcome.List);
        Assert.Empty(_documents.Calls);
    }

    [Fact]
    public async Task Listing_maps_a_database_failure_to_read_failed()
    {
        SeedManagerMeeting();
        _documents.Fail = new PersistenceUnavailableException();

        var outcome = await Service().ListAsync(_meeting, new ParticipantActor(_secretary));

        Assert.Equal(DocumentReadFailure.ReadFailed, outcome.Failure);
        Assert.Null(outcome.List);
    }

    [Fact]
    public async Task Uploading_stores_bytes_then_rows_in_one_transaction()
    {
        SeedManagerMeeting();
        _attacher.UploadResult.Add(new UploadedMeetingDocument(Guid.CreateVersion7(), "a.pdf",
            "application/pdf", 3, 1));
        var file = new DocumentUploadFile("a.pdf", "application/pdf", 3, new string('a', 64), 1,
            () => new MemoryStream([1, 2, 3]));

        var outcome = await Service().UploadAsync(_meeting, _secretary, [file]);

        Assert.Equal(DocumentUploadFailure.None, outcome.Failure);
        Assert.Single(outcome.Documents!);
        Assert.Single(_storedFiles.Added);
        Assert.Equal(["add-uploads"], _attacher.Calls);
        Assert.Equal(2, _unitOfWork.Commits);
        Assert.Equal([TransactionMode.ReadCommitted, TransactionMode.ReadCommitted], _unitOfWork.Modes);
        Assert.Equal(2, _unitOfWork.Saves);
    }

    [Fact]
    public async Task Removing_a_missing_document_answers_NotInMeeting_without_saving()
    {
        SeedManagerMeeting();

        var outcome = await Service().RemoveAsync(_meeting, Guid.CreateVersion7(), _secretary);

        Assert.Equal(DocumentUploadFailure.NotInMeeting, outcome.Failure);
        Assert.Equal(0, _unitOfWork.Saves);
        Assert.Equal(1, _unitOfWork.Rollbacks);
    }
}
