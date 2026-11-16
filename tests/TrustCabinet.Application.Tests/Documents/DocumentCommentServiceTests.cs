using Microsoft.Extensions.Logging.Abstractions;
using TrustCabinet.Application.Abstractions;
using TrustCabinet.Application.Documents;
using TrustCabinet.Application.Participants;
using TrustCabinet.Application.Tests.Fakes;
using TrustCabinet.Domain.Enums;
using TrustCabinet.Domain.Participants;
using TrustCabinet.Domain.Documents;
using TrustCabinet.Domain.Entities;

namespace TrustCabinet.Application.Tests.Documents;

public sealed class DocumentCommentServiceTests
{
    private static readonly DateTimeOffset Now = new(2026, 11, 4, 2, 0, 0, TimeSpan.Zero);

    private readonly FakeMeetingRepository _meetings = new();
    private readonly FakeDocumentCommentRepository _comments = new();
    private readonly FakeGuestSessionRepository _sessions = new();
    private readonly FakeAgendaEffects _agenda;
    private readonly FakeUnitOfWork _unitOfWork = new();
    private readonly Guid _secretary = Guid.CreateVersion7();
    private readonly Guid _meeting = Guid.CreateVersion7();
    private readonly Guid _document = Guid.CreateVersion7();

    public DocumentCommentServiceTests()
    {
        _agenda = new FakeAgendaEffects(_unitOfWork);
    }

    private DocumentCommentService Service() => new(_meetings, _comments, _sessions, _agenda, _unitOfWork,
        new FixedClock(Now), NullLogger<DocumentCommentService>.Instance);

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
        _comments.PageInfo = new DocumentPageInfo(DocumentFileRules.PdfContentType, 4);
    }

    [Fact]
    public async Task Listing_threads_top_level_comments_with_their_replies()
    {
        SeedManagerMeeting();
        var top = Guid.CreateVersion7();
        _comments.Rows.Add(new DocumentCommentFlatRow(top, null, "Ý đầu tiên", null, _secretary, null,
            "Thư ký", Now));
        _comments.Rows.Add(new DocumentCommentFlatRow(Guid.CreateVersion7(), top, "Đồng ý", null, _secretary,
            null, "Thư ký", Now.AddMinutes(1)));

        var outcome = await Service().ListAsync(_meeting, _document, new ParticipantActor(_secretary));

        Assert.Equal(CommentFailure.None, outcome.Failure);
        var threads = Assert.Single(outcome.Value!.Comments);
        Assert.Equal("Ý đầu tiên", threads.Body);
        Assert.Single(threads.Replies);
        Assert.True(outcome.Value.CanComment);
        Assert.Equal([TransactionMode.None], _unitOfWork.Modes);
    }

    [Fact]
    public async Task Adding_a_comment_locks_writes_and_saves_in_one_transaction()
    {
        SeedManagerMeeting();

        var outcome = await Service().AddAsync(_meeting, _document, new ParticipantActor(_secretary),
            "Đề nghị bổ sung", null, null);

        Assert.Equal(CommentFailure.None, outcome.Failure);
        Assert.Equal("Đề nghị bổ sung", outcome.Value!.Body);
        var saved = Assert.Single(_comments.Added);
        Assert.Equal(_document, saved.MeetingDocumentId);
        Assert.Equal([_meeting], _comments.MeetingLocks);
        Assert.Equal(1, _unitOfWork.Saves);
        Assert.Equal(1, _unitOfWork.Commits);
        Assert.Equal([TransactionMode.ReadCommitted], _unitOfWork.Modes);
    }

    [Fact]
    public async Task Adding_a_blank_comment_is_refused_before_any_read()
    {
        SeedManagerMeeting();

        var outcome = await Service().AddAsync(_meeting, _document, new ParticipantActor(_secretary), "  ", null, null);

        Assert.Equal(CommentFailure.Blank, outcome.Failure);
        Assert.Empty(_unitOfWork.Modes);
    }

    [Fact]
    public async Task Adding_maps_a_database_failure_to_save_failed()
    {
        SeedManagerMeeting();
        _comments.Fail = new PersistenceUnavailableException();

        var outcome = await Service().AddAsync(_meeting, _document, new ParticipantActor(_secretary), "Ý kiến", null, null);

        Assert.Equal(CommentFailure.SaveFailed, outcome.Failure);
        Assert.Equal(1, _unitOfWork.Rollbacks);
    }
}
