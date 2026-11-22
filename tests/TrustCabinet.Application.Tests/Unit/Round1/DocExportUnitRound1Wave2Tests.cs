using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using TrustCabinet.Application.Abstractions;
using TrustCabinet.Application.Documents;
using TrustCabinet.Application.Email;
using TrustCabinet.Application.Participants;
using TrustCabinet.Application.Summaries;
using TrustCabinet.Application.Tests.Fakes;
using TrustCabinet.Domain.Entities;
using TrustCabinet.Domain.Enums;
using TrustCabinet.Domain.Participants;

namespace TrustCabinet.Application.Tests.Unit.Round1;

// Round 1 unit tests, wave 2 (đợt 2) for FE-07 (Documents) and FE-12 (Minutes export):
// DocExportUnitRound1Wave2. Tester: Phạm Gia Khánh. Covers the 2 missing keys:
// DocumentComment.Add UTC005 and Minutes.Export UTC006.
// Every test carries [Trait("UTC", ...)] + [Trait("Trait", "UTC")] and the
// "<Class.Method>/<UTCnnn>" string in its DisplayName, so the trx (which has
// no traits) is matched back to the UTC rows by name.
// Note: the JSON rows for both keys carry file-validation inputs from another
// sheet; the tests below follow each row's note instead (Comment UTC005 note
// cites MSG156 = documents unavailable → NotFound; Export UTC006 expects a
// read failure → ReadFailed with an error log).
public sealed class DocExportUnitRound1Wave2Tests
{
    private static readonly DateTimeOffset Now = new(2026, 11, 4, 2, 0, 0, TimeSpan.Zero);

    private readonly FakeMeetingRepository _meetings = new();
    private readonly FakeUnitOfWork _unitOfWork = new();
    private readonly FakeAgendaEffects _agenda;
    private readonly FakeDocumentCommentRepository _comments = new();
    private readonly FakeMeetingMinutesRepository _minutes = new();
    private readonly FakeMinutesSourceQuery _sources = new();
    private readonly FakeSummarySourceQuery _summarySources = new();
    private readonly FakeMeetingSummaryRepository _summaries = new();
    private readonly FakeNotificationRepository _notifications = new();
    private readonly FakeAuditLog _audit = new();
    private readonly FakeEmailMessageRepository _emails = new();
    private readonly CapturingMinutesLog _logger = new();
    private readonly Guid _secretary = Guid.CreateVersion7();
    private readonly Guid _meeting = Guid.CreateVersion7();

    public DocExportUnitRound1Wave2Tests() => _agenda = new FakeAgendaEffects(_unitOfWork);

    private sealed class CapturingMinutesLog : ILogger<MinutesService>
    {
        public List<(LogLevel Level, string Message)> Records { get; } = [];
        IDisposable ILogger.BeginScope<TState>(TState state) => NoScope.Instance;
        public bool IsEnabled(LogLevel level) => true;
        public void Log<TState>(LogLevel logLevel, EventId eventId, TState state, Exception? exception,
            Func<TState, Exception?, string> formatter) => Records.Add((logLevel, formatter(state, exception)));
        private sealed class NoScope : IDisposable
        {
            public static readonly NoScope Instance = new();
            public void Dispose() { }
        }
    }

    // ---- DocumentCommentService.AddAsync ----

    private void SeedManagerMeeting(MeetingStatus status = MeetingStatus.Published)
    {
        _meetings.Meetings.Add(new Meeting
        {
            Id = _meeting, Title = "Họp giao ban", Status = status,
            PlannedStartTime = Now.AddHours(-1), PlannedEndTime = Now.AddHours(1),
            CreatedAt = Now, UpdatedAt = Now,
            Participants =
            [
                new MeetingParticipant
                {
                    MeetingId = _meeting, UserId = _secretary, Role = MeetingRole.Secretary,
                    AssignmentStatus = AssignmentStatus.Current, Response = InvitationResponse.Accepted,
                    User = new User { Id = _secretary, FullName = "Thư ký", Email = "tk@trustcabinet.local", IsActive = true },
                },
            ],
        });
        _meetings.ActiveUsers.Add(_secretary);
    }

    private DocumentCommentService CommentService() => new(_meetings, _comments,
        new FakeGuestSessionRepository(), _agenda, _unitOfWork, new FixedClock(Now),
        Microsoft.Extensions.Logging.Abstractions.NullLogger<DocumentCommentService>.Instance);

    [Fact(DisplayName = "DocumentCommentService.AddAsync/UTC005: unknown document is not found")]
    [Trait("UTC", "DocumentCommentService.AddAsync/UTC005")]
    [Trait("Trait", "UTC")]
    public async Task Add_UnknownDocumentIsNotFound()
    {
        SeedManagerMeeting();

        var outcome = await CommentService().AddAsync(_meeting, Guid.CreateVersion7(),
            new ParticipantActor(_secretary), "Ý kiến cho tài liệu.", null, null);

        Assert.Equal(CommentFailure.NotFound, outcome.Failure);
        Assert.Null(outcome.Value);
        Assert.Empty(_comments.Added);
    }

    // ---- MinutesService.ExportAsync ----

    private MinutesService MinutesService(IMeetingMinutesRepository? minutes = null) => new(minutes ?? _minutes,
        _sources, _summarySources, _summaries, new FakeMinutesRenderer(),
        _notifications, _audit, _unitOfWork, _meetings, new FixedClock(Now), _logger, _emails,
        Options.Create(new EmailDeliveryOptions { ApplicationBaseUrl = "https://hop.example.vn" }));

    /// <summary>A store whose reads fail, standing in for a database that cannot be
    /// reached mid-read; declared here so the shared Fakes/ stay untouched.</summary>
    private sealed class UnreachableMinutesRepository(IMeetingMinutesRepository inner) : IMeetingMinutesRepository
    {
        public Task<MeetingMinutes?> FindAsync(Guid minutesId, CancellationToken ct)
            => Task.FromException<MeetingMinutes?>(new PersistenceUnavailableException());
        public Task<MeetingMinutes?> FindLatestAsync(Guid meetingId, CancellationToken ct)
            => inner.FindLatestAsync(meetingId, ct);
        public Task<MeetingMinutes> GetAsync(Guid minutesId, CancellationToken ct)
            => inner.GetAsync(minutesId, ct);
        public Task<MeetingMinutes?> FindWithPeopleAsync(Guid minutesId, CancellationToken ct)
            => inner.FindWithPeopleAsync(minutesId, ct);
        public Task<Guid?> FindMeetingIdAsync(Guid minutesId, CancellationToken ct)
            => inner.FindMeetingIdAsync(minutesId, ct);
        public Task<MeetingMinutes?> LockAsync(Guid minutesId, CancellationToken ct)
            => inner.LockAsync(minutesId, ct);
        public Task<bool> HasLaterRevisionAsync(Guid meetingId, int revisionNumber, CancellationToken ct)
            => inner.HasLaterRevisionAsync(meetingId, revisionNumber, ct);
        public Task<IReadOnlyList<MinutesRevisionRow>> ListRevisionsAsync(Guid meetingId, bool manages, CancellationToken ct)
            => inner.ListRevisionsAsync(meetingId, manages, ct);
        public Task<int> SaveContentAsync(Guid minutesId, string content, DateTimeOffset from, DateTimeOffset to,
            DateTimeOffset now, CancellationToken ct)
            => inner.SaveContentAsync(minutesId, content, from, to, now, ct);
        public void Add(MeetingMinutes minutes) => inner.Add(minutes);
    }

    [Fact(DisplayName = "MinutesService.ExportAsync/UTC006: a failing read answers read failed")]
    [Trait("UTC", "MinutesService.ExportAsync/UTC006")]
    [Trait("Trait", "UTC")]
    public async Task Export_FailingReadAnswersReadFailed()
    {
        SeedManagerMeeting(MeetingStatus.Ended);
        var row = new MeetingMinutes
        {
            MeetingId = _meeting, RevisionNumber = 1, Status = MinutesStatus.Issued,
            Content = "<p>Biên bản</p>", CreatedById = _secretary,
            CreatedAt = Now.AddMinutes(-10), UpdatedAt = Now.AddMinutes(-10),
        };
        _minutes.Rows.Add(row);

        var outcome = await MinutesService(new UnreachableMinutesRepository(_minutes))
            .ExportAsync(row.Id, _secretary, MinutesExportFormat.Docx);

        Assert.Equal(MinutesFailure.ReadFailed, outcome.Failure);
        Assert.Null(outcome.Data);
        Assert.Contains("Could not export minutes", Assert.Single(_logger.Records).Message);
    }
}
