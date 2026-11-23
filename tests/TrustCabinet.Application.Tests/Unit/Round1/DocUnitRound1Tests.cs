using System.Security.Cryptography;
using System.Text;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using TrustCabinet.Application.Abstractions;
using TrustCabinet.Application.Documents;
using TrustCabinet.Application.Participants;
using TrustCabinet.Application.Tests.Fakes;
using TrustCabinet.Domain.Documents;
using TrustCabinet.Domain.Entities;
using TrustCabinet.Domain.Enums;
using TrustCabinet.Domain.Participants;
using TrustCabinet.Application.Email;

namespace TrustCabinet.Application.Tests.Unit.Round1;

// Round 1 unit tests for FE-07 (Documents): DocUnitRound1.
// Every test carries [Trait("UTC", ...)] + [Trait("Trait", "UTC")] and the
// "<Class.Method>/<UTCnnn>" string in its DisplayName, so the trx (which has
// no traits) is matched back to the UTC rows by name.
public sealed class DocUnitRound1Tests
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
    private readonly FakePersonalLibraryRepository _library = new();
    private readonly FakeDocumentCommentRepository _comments = new();
    private readonly Guid _secretary = Guid.CreateVersion7();
    private readonly Guid _member = Guid.CreateVersion7();
    private readonly Guid _meeting = Guid.CreateVersion7();
    private readonly Guid _owner = Guid.CreateVersion7();

    public DocUnitRound1Tests() => _agenda = new FakeAgendaEffects(_unitOfWork);

    private sealed class CapturingLog<T> : ILogger<T>
    {
        public List<string> Messages { get; } = [];
        IDisposable ILogger.BeginScope<TState>(TState state) => NullScope.Instance;
        public bool IsEnabled(LogLevel level) => true;
        public void Log<TState>(LogLevel logLevel, EventId eventId, TState state, Exception? exception,
            Func<TState, Exception?, string> formatter) => Messages.Add(formatter(state, exception));
        private sealed class NullScope : IDisposable
        {
            public static readonly NullScope Instance = new();
            public void Dispose() { }
        }
    }

    private MeetingDocumentService DocService(
        ILogger<MeetingDocumentService>? logger = null,
        ILogger<StoredFileWriter>? writerLogger = null,
        IMeetingDocumentAttacher? attacher = null,
        FileStorageOptions? options = null) => new(_meetings, _documents, attacher ?? _attacher,
            new StoredFileWriter(_storedFiles, _storage, _unitOfWork, new FixedClock(Now),
                Options.Create(options ?? new FileStorageOptions()),
                (ILogger<StoredFileWriter>)(writerLogger ?? Microsoft.Extensions.Logging.Abstractions.NullLogger<StoredFileWriter>.Instance)),
            _storage, _audit, _agenda, _unitOfWork, new FixedClock(Now),
            (ILogger<MeetingDocumentService>)(logger ?? Microsoft.Extensions.Logging.Abstractions.NullLogger<MeetingDocumentService>.Instance));

    private PersonalLibraryService LibraryService(
        ILogger<PersonalLibraryService>? logger = null,
        ILogger<StoredFileWriter>? writerLogger = null,
        FileStorageOptions? options = null) => new(_library,
            new StoredFileWriter(_storedFiles, _storage, _unitOfWork, new FixedClock(Now),
                Options.Create(options ?? new FileStorageOptions()),
                (ILogger<StoredFileWriter>)(writerLogger ?? Microsoft.Extensions.Logging.Abstractions.NullLogger<StoredFileWriter>.Instance)),
            _storage, _audit, _unitOfWork, new FixedClock(Now),
            (ILogger<PersonalLibraryService>)(logger ?? Microsoft.Extensions.Logging.Abstractions.NullLogger<PersonalLibraryService>.Instance));

    private DocumentCommentService CommentService() => new(_meetings, _comments,
        new FakeGuestSessionRepository(), _agenda, _unitOfWork, new FixedClock(Now),
        Microsoft.Extensions.Logging.Abstractions.NullLogger<DocumentCommentService>.Instance);

    private void SeedManagerMeeting(MeetingStatus status = MeetingStatus.InProgress,
        Sensitivity sensitivity = Sensitivity.Normal, bool withMember = false)
    {
        var participants = new List<MeetingParticipant>
        {
            new()
            {
                MeetingId = _meeting, UserId = _secretary, Role = MeetingRole.Secretary,
                AssignmentStatus = AssignmentStatus.Current, Response = InvitationResponse.Accepted,
                User = new User { Id = _secretary, FullName = "Thư ký", Email = "Thư ký@trustcabinet.local", IsActive = true },
            },
        };
        if (withMember)
            participants.Add(new()
            {
                MeetingId = _meeting, UserId = _member, Role = MeetingRole.Member,
                AssignmentStatus = AssignmentStatus.Current, Response = InvitationResponse.Accepted,
                User = new User { Id = _member, FullName = "Thành viên", Email = "tv@trustcabinet.local", IsActive = true },
            });
        _meetings.Meetings.Add(new Meeting
        {
            Id = _meeting, Title = "Họp giao ban", Status = status, Sensitivity = sensitivity,
            PlannedStartTime = Now.AddHours(-1), PlannedEndTime = Now.AddHours(1),
            CreatedAt = Now, UpdatedAt = Now, Participants = participants,
        });
        _meetings.ActiveUsers.Add(_secretary);
        if (withMember) _meetings.ActiveUsers.Add(_member);
    }

    private static DocumentUploadFile ValidatedFile(string name = "a.pdf", long length = 3, int? pages = 1,
        string contentType = "application/pdf") => new(name, contentType, length, new string('a', 64), pages,
            () => new MemoryStream([1, 2, 3]));

    private void SeedManagedUpload(out Guid storedId)
    {
        storedId = Guid.CreateVersion7();
        _attacher.UploadResult.Add(new UploadedMeetingDocument(Guid.CreateVersion7(), "Bao-cao.pdf",
            "application/pdf", 2097152, 12));
    }

    // ---- DocumentFileRules.ValidateAsync ----

    private static DocumentUploadCandidate Candidate(string name, long length, byte[] bytes, bool mustNotRead = false) =>
        mustNotRead
            ? new(name, length, () => throw new InvalidOperationException("bytes must not be read"))
            : new(name, length, () => new MemoryStream(bytes));

    private static readonly byte[] PdfBytes = "%PDF-1.4\n1 0 obj\n%%EOF\nstartxref\n0\n"u8.ToArray();

    [Fact(DisplayName = "DocumentFileRules.ValidateAsync/UTC001: valid 2 MB PDF passes with 12 pages")]
    [Trait("UTC", "DocumentFileRules.ValidateAsync/UTC001")]
    [Trait("Trait", "UTC")]
    public async Task Validate_ValidPdfPassesWithPageCount()
    {
        var outcome = await DocumentFileRules.ValidateAsync(
            [Candidate("Bao-cao-Q3.pdf", 2097152, PdfBytes)],
            (_, _) => Task.FromResult<int?>(12), default);

        Assert.Equal(DocumentUploadFailure.None, outcome.Failure);
        var file = Assert.Single(outcome.Files!);
        Assert.Equal(("Bao-cao-Q3.pdf", "application/pdf"), (file.FileName, file.ContentType));
        Assert.True(file.Length == 2097152 && file.PageCount == 12);
        Assert.Equal(Convert.ToHexStringLower(SHA256.HashData(PdfBytes)), file.ContentSha256);
    }

    [Fact(DisplayName = "DocumentFileRules.ValidateAsync/UTC002: eleven files answer too many")]
    [Trait("UTC", "DocumentFileRules.ValidateAsync/UTC002")]
    [Trait("Trait", "UTC")]
    public async Task Validate_ElevenFilesAnswerTooMany()
    {
        var outcome = await DocumentFileRules.ValidateAsync(
            Enumerable.Range(0, 11).Select(i => Candidate($"f{i}.pdf", 1048576, PdfBytes, mustNotRead: true)).ToList(),
            (_, _) => Task.FromResult<int?>(1), default);

        Assert.Equal(DocumentUploadFailure.TooMany, outcome.Failure);
        Assert.Null(outcome.Files);
        Assert.Null(outcome.Errors);
    }

    [Fact(DisplayName = "DocumentFileRules.ValidateAsync/UTC003: total over 100 MB answers too many")]
    [Trait("UTC", "DocumentFileRules.ValidateAsync/UTC003")]
    [Trait("Trait", "UTC")]
    public async Task Validate_TotalOver100MBAnswersTooMany()
    {
        var outcome = await DocumentFileRules.ValidateAsync(
            Enumerable.Range(0, 10).Select(i => Candidate($"g{i}.pdf", 11534336, PdfBytes, mustNotRead: true)).ToList(),
            (_, _) => Task.FromResult<int?>(1), default);

        Assert.Equal(DocumentUploadFailure.TooMany, outcome.Failure);
        Assert.Null(outcome.Files);
        Assert.Null(outcome.Errors);
    }

    [Fact(DisplayName = "DocumentFileRules.ValidateAsync/UTC004: 10 MB plus one byte is invalid")]
    [Trait("UTC", "DocumentFileRules.ValidateAsync/UTC004")]
    [Trait("Trait", "UTC")]
    public async Task Validate_10MBPlusOneByteIsInvalid()
    {
        var outcome = await DocumentFileRules.ValidateAsync(
            [Candidate("lon.pdf", 10485761, PdfBytes, mustNotRead: true)],
            (_, _) => Task.FromResult<int?>(1), default);

        Assert.Equal(DocumentUploadFailure.Invalid, outcome.Failure);
        Assert.Equal(["lon.pdf: tệp lớn hơn 10 MB."], outcome.Errors!["files[0]"]);
        Assert.Null(outcome.Files);
    }

    // ---- MeetingDocumentService.UploadAsync ----

    [Fact(DisplayName = "MeetingDocumentService.UploadAsync/UTC001: chair upload stores rows and bytes")]
    [Trait("UTC", "MeetingDocumentService.UploadAsync/UTC001")]
    [Trait("Trait", "UTC")]
    public async Task Upload_ChairUploadStoresRowsAndBytes()
    {
        SeedManagerMeeting(MeetingStatus.Published);
        var realAttacher = new MeetingDocumentAttacher(_documents, _library);
        var outcome = await DocService(attacher: realAttacher).UploadAsync(_meeting, _secretary,
            [ValidatedFile("Bao-cao.pdf", 2097152, 12)]);

        Assert.Equal(DocumentUploadFailure.None, outcome.Failure);
        Assert.Single(outcome.Documents!);
        Assert.Single(_storedFiles.Added);
        Assert.Single(_storage.Objects);
    }

    [Fact(DisplayName = "MeetingDocumentService.UploadAsync/UTC002: ended meeting releases the upload")]
    [Trait("UTC", "MeetingDocumentService.UploadAsync/UTC002")]
    [Trait("Trait", "UTC")]
    public async Task Upload_EndedMeetingReleasesTheUpload()
    {
        SeedManagerMeeting(MeetingStatus.Ended);
        SeedManagedUpload(out _);

        var outcome = await DocService().UploadAsync(_meeting, _secretary, [ValidatedFile()]);

        Assert.Equal(DocumentUploadFailure.Ended, outcome.Failure);
        Assert.Empty(_storage.Objects);
        Assert.NotEmpty(_storedFiles.Stamped);
    }

    [Fact(DisplayName = "MeetingDocumentService.UploadAsync/UTC003: member without a manager role is forbidden")]
    [Trait("UTC", "MeetingDocumentService.UploadAsync/UTC003")]
    [Trait("Trait", "UTC")]
    public async Task Upload_MemberWithoutManagerRoleIsForbidden()
    {
        SeedManagerMeeting(MeetingStatus.InProgress, withMember: true);
        SeedManagedUpload(out _);

        var outcome = await DocService().UploadAsync(_meeting, _member, [ValidatedFile()]);

        Assert.Equal(DocumentUploadFailure.Forbidden, outcome.Failure);
        Assert.Empty(_storage.Objects);
        Assert.NotEmpty(_storedFiles.Stamped);
    }

    [Fact(DisplayName = "MeetingDocumentService.UploadAsync/UTC004: used plus new exactly at the limit")]
    [Trait("UTC", "MeetingDocumentService.UploadAsync/UTC004")]
    [Trait("Trait", "UTC")]
    public async Task Upload_UsedPlusNewExactlyAtTheLimit()
    {
        SeedManagerMeeting(MeetingStatus.Published);
        SeedManagedUpload(out _);
        const long fileSize = 1048576;
        _storedFiles.UsedBytes = new FileStorageOptions().MaxTotalBytes - fileSize;

        var outcome = await DocService().UploadAsync(_meeting, _secretary, [ValidatedFile("a.pdf", fileSize)]);

        Assert.Equal(DocumentUploadFailure.None, outcome.Failure);
        Assert.Single(outcome.Documents!);
        Assert.Single(_storedFiles.Added);
    }

    [Fact(DisplayName = "MeetingDocumentService.UploadAsync/UTC005: one byte over the limit is storage full")]
    [Trait("UTC", "MeetingDocumentService.UploadAsync/UTC005")]
    [Trait("Trait", "UTC")]
    public async Task Upload_OneByteOverTheLimitIsStorageFull()
    {
        SeedManagerMeeting(MeetingStatus.Published);
        SeedManagedUpload(out _);
        const long fileSize = 1048576;
        _storedFiles.UsedBytes = new FileStorageOptions().MaxTotalBytes - fileSize + 1;

        var outcome = await DocService().UploadAsync(_meeting, _secretary, [ValidatedFile("a.pdf", fileSize)]);

        Assert.Equal(DocumentUploadFailure.StorageFull, outcome.Failure);
        Assert.Empty(_storedFiles.Added);
        Assert.Empty(_storage.Objects);
    }

    // ---- MeetingDocumentService.AddFromLibraryAsync ----

    private void SeedLibraryDocs(Guid owner, int count, out List<Guid> ids)
    {
        ids = [];
        for (var i = 0; i < count; i++)
        {
            var stored = Guid.CreateVersion7();
            var id = Guid.CreateVersion7();
            _documents.LibraryDocuments.Add(new Document
            {
                Id = id, OwnerId = owner, StoredFileId = stored, FileName = $"t{i}.pdf",
                StoredFile = new StoredFile { Id = stored, StorageKey = $"stored-files/{stored:D}", OriginalFileName = $"t{i}.pdf", ContentType = "application/pdf", ContentSha256 = new string('a', 64), ByteSize = 3 },
            });
            ids.Add(id);
        }
    }

    [Fact(DisplayName = "MeetingDocumentService.AddFromLibraryAsync/UTC001: chair adds two library documents")]
    [Trait("UTC", "MeetingDocumentService.AddFromLibraryAsync/UTC001")]
    [Trait("Trait", "UTC")]
    public async Task AddFromLibrary_ChairAddsTwoLibraryDocuments()
    {
        SeedManagerMeeting(MeetingStatus.Published);
        SeedLibraryDocs(_secretary, 2, out var ids);
        _attacher.LibraryResult = (DocumentUploadFailure.None,
            ids.Select(id => new UploadedMeetingDocument(Guid.CreateVersion7(), "t.pdf", "application/pdf", 3, 1)).ToList());

        var outcome = await DocService().AddFromLibraryAsync(_meeting, _secretary, ids);

        Assert.Equal(DocumentUploadFailure.None, outcome.Failure);
        Assert.Equal(2, outcome.Documents!.Count);
        Assert.Equal(1, _unitOfWork.Saves);
    }

    [Fact(DisplayName = "MeetingDocumentService.AddFromLibraryAsync/UTC002: file already in meeting is refused")]
    [Trait("UTC", "MeetingDocumentService.AddFromLibraryAsync/UTC002")]
    [Trait("Trait", "UTC")]
    public async Task AddFromLibrary_FileAlreadyInMeetingIsRefused()
    {
        SeedManagerMeeting(MeetingStatus.Published);
        SeedLibraryDocs(_secretary, 1, out var ids);
        _attacher.LibraryResult = (DocumentUploadFailure.AlreadyInMeeting, []);

        var outcome = await DocService().AddFromLibraryAsync(_meeting, _secretary, ids);

        Assert.Equal(DocumentUploadFailure.AlreadyInMeeting, outcome.Failure);
        Assert.Null(outcome.Documents);
        Assert.Equal(0, _unitOfWork.Saves);
    }

    [Fact(DisplayName = "MeetingDocumentService.AddFromLibraryAsync/UTC003: member is forbidden")]
    [Trait("UTC", "MeetingDocumentService.AddFromLibraryAsync/UTC003")]
    [Trait("Trait", "UTC")]
    public async Task AddFromLibrary_MemberIsForbidden()
    {
        SeedManagerMeeting(MeetingStatus.Published, withMember: true);
        SeedLibraryDocs(_member, 1, out var ids);

        var outcome = await DocService().AddFromLibraryAsync(_meeting, _member, ids);

        Assert.Equal(DocumentUploadFailure.Forbidden, outcome.Failure);
        Assert.Empty(_attacher.Calls);
        Assert.Equal(0, _unitOfWork.Saves);
    }

    [Fact(DisplayName = "MeetingDocumentService.AddFromLibraryAsync/UTC004: ended meeting refuses")]
    [Trait("UTC", "MeetingDocumentService.AddFromLibraryAsync/UTC004")]
    [Trait("Trait", "UTC")]
    public async Task AddFromLibrary_EndedMeetingRefuses()
    {
        SeedManagerMeeting(MeetingStatus.Ended);
        SeedLibraryDocs(_secretary, 1, out var ids);

        var outcome = await DocService().AddFromLibraryAsync(_meeting, _secretary, ids);

        Assert.Equal(DocumentUploadFailure.Ended, outcome.Failure);
        Assert.Equal(0, _unitOfWork.Saves);
        Assert.Equal(1, _unitOfWork.Commits);
    }

    // ---- MeetingDocumentService.RemoveAsync ----

    private Guid SeedMeetingDocument(bool removed = false)
    {
        var id = Guid.CreateVersion7();
        _documents.MeetingDocuments.Add(new MeetingDocument
        {
            Id = id, MeetingId = _meeting, FileName = "a.pdf",
            RemovedAt = removed ? Now.AddMinutes(-1) : null,
            AgendaItemLinks = removed ? [] : [new MeetingDocumentAgendaItem { MeetingDocumentId = id, AgendaItemId = Guid.CreateVersion7() }],
        });
        return id;
    }

    [Fact(DisplayName = "MeetingDocumentService.RemoveAsync/UTC001: chair removes a linked document")]
    [Trait("UTC", "MeetingDocumentService.RemoveAsync/UTC001")]
    [Trait("Trait", "UTC")]
    public async Task Remove_ChairRemovesALinkedDocument()
    {
        SeedManagerMeeting(MeetingStatus.Published);
        var id = SeedMeetingDocument();

        var outcome = await DocService().RemoveAsync(_meeting, id, _secretary);

        Assert.Equal(DocumentUploadFailure.None, outcome.Failure);
        var row = _documents.MeetingDocuments.Single(d => d.Id == id);
        Assert.True(row.RemovedAt == Now);
        Assert.True(row.RemovedById == _secretary);
        Assert.Empty(_documents.AgendaLinks);
    }

    [Fact(DisplayName = "MeetingDocumentService.RemoveAsync/UTC002: already removed document is not in meeting")]
    [Trait("UTC", "MeetingDocumentService.RemoveAsync/UTC002")]
    [Trait("Trait", "UTC")]
    public async Task Remove_AlreadyRemovedDocumentIsNotInMeeting()
    {
        SeedManagerMeeting(MeetingStatus.Published);
        var id = SeedMeetingDocument(removed: true);

        var outcome = await DocService().RemoveAsync(_meeting, id, _secretary);

        Assert.Equal(DocumentUploadFailure.NotInMeeting, outcome.Failure);
        Assert.Equal(0, _unitOfWork.Saves);
        Assert.Equal(1, _unitOfWork.Commits);
    }

    [Fact(DisplayName = "MeetingDocumentService.RemoveAsync/UTC003: member is forbidden")]
    [Trait("UTC", "MeetingDocumentService.RemoveAsync/UTC003")]
    [Trait("Trait", "UTC")]
    public async Task Remove_MemberIsForbidden()
    {
        SeedManagerMeeting(MeetingStatus.Published, withMember: true);
        var id = SeedMeetingDocument();

        var outcome = await DocService().RemoveAsync(_meeting, id, _member);

        Assert.Equal(DocumentUploadFailure.Forbidden, outcome.Failure);
        Assert.Null(_documents.MeetingDocuments.Single(d => d.Id == id).RemovedAt);
        Assert.Equal(0, _unitOfWork.Saves);
    }

    [Fact(DisplayName = "MeetingDocumentService.RemoveAsync/UTC004: ended meeting refuses")]
    [Trait("UTC", "MeetingDocumentService.RemoveAsync/UTC004")]
    [Trait("Trait", "UTC")]
    public async Task Remove_EndedMeetingRefuses()
    {
        SeedManagerMeeting(MeetingStatus.Ended);
        var id = SeedMeetingDocument();

        var outcome = await DocService().RemoveAsync(_meeting, id, _secretary);

        Assert.Equal(DocumentUploadFailure.Ended, outcome.Failure);
        Assert.Null(_documents.MeetingDocuments.Single(d => d.Id == id).RemovedAt);
        Assert.Equal(1, _unitOfWork.Commits);
    }

    // ---- MeetingDocumentService.AttachAsync ----

    private Guid SeedLinkableDocument(out Guid itemId)
    {
        itemId = Guid.CreateVersion7();
        _documents.AgendaItems.Add((_meeting, itemId));
        var id = Guid.CreateVersion7();
        _documents.MeetingDocuments.Add(new MeetingDocument { Id = id, MeetingId = _meeting, FileName = "a.pdf" });
        return id;
    }

    [Fact(DisplayName = "MeetingDocumentService.AttachAsync/UTC001: chair links two meeting documents")]
    [Trait("UTC", "MeetingDocumentService.AttachAsync/UTC001")]
    [Trait("Trait", "UTC")]
    public async Task Attach_ChairLinksTwoMeetingDocuments()
    {
        SeedManagerMeeting(MeetingStatus.Published);
        var m1 = SeedLinkableDocument(out var a1);
        var m2 = SeedLinkableDocument(out _);

        var outcome = await DocService().AttachAsync(_meeting, a1, _secretary, DocumentAttachSource.Meeting, [m1, m2]);

        Assert.Equal(DocumentUploadFailure.None, outcome.Failure);
        Assert.Equal(2, _documents.AgendaLinks.Count);
        Assert.Equal(1, _unitOfWork.Saves);
    }

    [Fact(DisplayName = "MeetingDocumentService.AttachAsync/UTC002: already linked document is refused")]
    [Trait("UTC", "MeetingDocumentService.AttachAsync/UTC002")]
    [Trait("Trait", "UTC")]
    public async Task Attach_AlreadyLinkedDocumentIsRefused()
    {
        SeedManagerMeeting(MeetingStatus.Published);
        var m1 = SeedLinkableDocument(out var a1);
        _documents.AgendaLinks.Add(new MeetingDocumentAgendaItem { MeetingDocumentId = m1, AgendaItemId = a1 });

        var outcome = await DocService().AttachAsync(_meeting, a1, _secretary, DocumentAttachSource.Meeting, [m1]);

        Assert.Equal(DocumentUploadFailure.AlreadyLinked, outcome.Failure);
        Assert.Single(_documents.AgendaLinks);
        Assert.Equal(0, _unitOfWork.Saves);
    }

    [Fact(DisplayName = "MeetingDocumentService.AttachAsync/UTC003: unknown agenda item is not found")]
    [Trait("UTC", "MeetingDocumentService.AttachAsync/UTC003")]
    [Trait("Trait", "UTC")]
    public async Task Attach_UnknownAgendaItemIsNotFound()
    {
        SeedManagerMeeting(MeetingStatus.Published);
        var m1 = SeedLinkableDocument(out _);

        var outcome = await DocService().AttachAsync(_meeting, Guid.CreateVersion7(), _secretary,
            DocumentAttachSource.Meeting, [m1]);

        Assert.Equal(DocumentUploadFailure.AgendaItemNotFound, outcome.Failure);
        Assert.Empty(_documents.AgendaLinks);
        Assert.Equal(0, _unitOfWork.Saves);
    }

    [Fact(DisplayName = "MeetingDocumentService.AttachAsync/UTC004: member is forbidden")]
    [Trait("UTC", "MeetingDocumentService.AttachAsync/UTC004")]
    [Trait("Trait", "UTC")]
    public async Task Attach_MemberIsForbidden()
    {
        SeedManagerMeeting(MeetingStatus.Published, withMember: true);
        var m1 = SeedLinkableDocument(out var a1);

        var outcome = await DocService().AttachAsync(_meeting, a1, _member, DocumentAttachSource.Meeting, [m1]);

        Assert.Equal(DocumentUploadFailure.Forbidden, outcome.Failure);
        Assert.Empty(_documents.AgendaLinks);
        Assert.Equal(0, _unitOfWork.Saves);
    }

    // ---- MeetingDocumentService.OpenContentAsync ----

    private void SeedAttachedFile(string key = "stored-files/m1", byte[]? bytes = null)
    {
        _documents.AttachedFile = new AttachedDocumentFile("M1.pdf", key, "application/pdf", 7);
        _storage.Objects[key] = bytes ?? [1, 2, 3, 4, 5, 6, 7];
    }

    [Fact(DisplayName = "MeetingDocumentService.OpenContentAsync/UTC001: member views a normal document")]
    [Trait("UTC", "MeetingDocumentService.OpenContentAsync/UTC001")]
    [Trait("Trait", "UTC")]
    public async Task OpenContent_MemberViewsANormalDocument()
    {
        SeedManagerMeeting(MeetingStatus.Published, withMember: true);
        SeedAttachedFile();
        var docId = Guid.CreateVersion7();

        var outcome = await DocService().OpenContentAsync(_meeting, docId, new ParticipantActor(_member), false);

        Assert.Equal(DocumentReadFailure.None, outcome.Failure);
        Assert.Equal(("M1.pdf", "application/pdf"), (outcome.Content!.FileName, outcome.Content.ContentType));
        Assert.Empty(_audit.Actions);
    }

    [Fact(DisplayName = "MeetingDocumentService.OpenContentAsync/UTC002: downloading a secret document is refused")]
    [Trait("UTC", "MeetingDocumentService.OpenContentAsync/UTC002")]
    [Trait("Trait", "UTC")]
    public async Task OpenContent_DownloadingASecretDocumentIsRefused()
    {
        SeedManagerMeeting(MeetingStatus.Published, Sensitivity.Secret, withMember: true);
        SeedAttachedFile();

        var outcome = await DocService().OpenContentAsync(_meeting, Guid.CreateVersion7(),
            new ParticipantActor(_member), true);

        Assert.Equal(DocumentReadFailure.DownloadForbidden, outcome.Failure);
        Assert.Null(outcome.Content);
        Assert.Empty(_audit.Actions);
    }

    [Fact(DisplayName = "MeetingDocumentService.OpenContentAsync/UTC003: outsider learns nothing")]
    [Trait("UTC", "MeetingDocumentService.OpenContentAsync/UTC003")]
    [Trait("Trait", "UTC")]
    public async Task OpenContent_OutsiderLearnsNothing()
    {
        SeedManagerMeeting(MeetingStatus.Published);
        SeedAttachedFile();

        var outcome = await DocService().OpenContentAsync(_meeting, Guid.CreateVersion7(),
            new ParticipantActor(Guid.CreateVersion7()), false);

        Assert.Equal(DocumentReadFailure.NotFound, outcome.Failure);
        Assert.Null(outcome.Content);
        Assert.Empty(_documents.Calls);
    }

    [Fact(DisplayName = "MeetingDocumentService.OpenContentAsync/UTC004: chair sees a draft-vote document")]
    [Trait("UTC", "MeetingDocumentService.OpenContentAsync/UTC004")]
    [Trait("Trait", "UTC")]
    public async Task OpenContent_ChairSeesADraftVoteDocument()
    {
        SeedManagerMeeting(MeetingStatus.InProgress);
        SeedAttachedFile();

        var outcome = await DocService().OpenContentAsync(_meeting, Guid.CreateVersion7(),
            new ParticipantActor(_secretary), false);

        Assert.Equal(DocumentReadFailure.None, outcome.Failure);
        Assert.NotNull(outcome.Content);
        Assert.Equal(7, outcome.Content!.ByteSize);
    }

    [Fact(DisplayName = "MeetingDocumentService.OpenContentAsync/UTC005: gone object answers file missing")]
    [Trait("UTC", "MeetingDocumentService.OpenContentAsync/UTC005")]
    [Trait("Trait", "UTC")]
    public async Task OpenContent_GoneObjectAnswersFileMissing()
    {
        var log = new CapturingLog<MeetingDocumentService>();
        SeedManagerMeeting(MeetingStatus.Published, withMember: true);
        _documents.AttachedFile = new AttachedDocumentFile("M1.pdf", "stored-files/gone", "application/pdf", 7);

        var outcome = await DocService(logger: log).OpenContentAsync(_meeting, Guid.CreateVersion7(),
            new ParticipantActor(_member), false);

        Assert.Equal(DocumentReadFailure.FileMissing, outcome.Failure);
        Assert.Null(outcome.Content);
        Assert.Equal(1, log.Messages.Count(m => m.Contains("Failed to open stored object")));
    }

    // ---- PersonalLibraryService.CreateFolderAsync ----

    [Fact(DisplayName = "PersonalLibraryService.CreateFolderAsync/UTC001: owner creates a root folder")]
    [Trait("UTC", "PersonalLibraryService.CreateFolderAsync/UTC001")]
    [Trait("Trait", "UTC")]
    public async Task CreateFolder_OwnerCreatesARootFolder()
    {
        var outcome = await LibraryService().CreateFolderAsync(_owner, null, "Hồ sơ 2026");

        Assert.Equal(LibraryFailure.None, outcome.Failure);
        Assert.Equal(("Hồ sơ 2026", null), (outcome.Value!.Name, outcome.Value.ParentId));
        Assert.Equal([_owner], _library.Locks);
    }

    [Fact(DisplayName = "PersonalLibraryService.CreateFolderAsync/UTC002: missing parent is not found")]
    [Trait("UTC", "PersonalLibraryService.CreateFolderAsync/UTC002")]
    [Trait("Trait", "UTC")]
    public async Task CreateFolder_MissingParentIsNotFound()
    {
        var outcome = await LibraryService().CreateFolderAsync(_owner, Guid.CreateVersion7(), "A");

        Assert.Equal(LibraryFailure.DestinationNotFound, outcome.Failure);
        Assert.Null(outcome.Value);
        Assert.Empty(_library.Folders);
    }

    [Fact(DisplayName = "PersonalLibraryService.CreateFolderAsync/UTC003: blank name is required")]
    [Trait("UTC", "PersonalLibraryService.CreateFolderAsync/UTC003")]
    [Trait("Trait", "UTC")]
    public async Task CreateFolder_BlankNameIsRequired()
    {
        var outcome = await LibraryService().CreateFolderAsync(_owner, null, null);

        Assert.Equal(LibraryFailure.NameRequired, outcome.Failure);
        Assert.Null(outcome.Value);
        Assert.Empty(_library.Folders);
    }

    [Fact(DisplayName = "PersonalLibraryService.CreateFolderAsync/UTC004: fifth level is allowed")]
    [Trait("UTC", "PersonalLibraryService.CreateFolderAsync/UTC004")]
    [Trait("Trait", "UTC")]
    public async Task CreateFolder_FifthLevelIsAllowed()
    {
        Guid? at = null;
        for (var depth = 0; depth < 4; depth++)
        {
            var folder = new Folder { OwnerId = _owner, ParentId = at, Name = $"Cấp {depth}", CreatedAt = Now, UpdatedAt = Now };
            _library.Folders[folder.Id] = folder;
            at = folder.Id;
        }

        var outcome = await LibraryService().CreateFolderAsync(_owner, at, "Cấp 5");

        Assert.Equal(LibraryFailure.None, outcome.Failure);
        Assert.Equal("Cấp 5", outcome.Value!.Name);
        Assert.Equal(1, _unitOfWork.Saves);
    }

    [Fact(DisplayName = "PersonalLibraryService.CreateFolderAsync/UTC005: sixth level is too deep")]
    [Trait("UTC", "PersonalLibraryService.CreateFolderAsync/UTC005")]
    [Trait("Trait", "UTC")]
    public async Task CreateFolder_SixthLevelIsTooDeep()
    {
        Guid? at = null;
        for (var depth = 0; depth < 5; depth++)
        {
            var folder = new Folder { OwnerId = _owner, ParentId = at, Name = $"Cấp {depth}", CreatedAt = Now, UpdatedAt = Now };
            _library.Folders[folder.Id] = folder;
            at = folder.Id;
        }

        var outcome = await LibraryService().CreateFolderAsync(_owner, at, "Cấp 6");

        Assert.Equal(LibraryFailure.TooDeep, outcome.Failure);
        Assert.Null(outcome.Value);
        Assert.Equal(0, _unitOfWork.Saves);
    }

    // ---- PersonalLibraryService.DeleteDocumentAsync ----

    private Guid SeedLibraryDocument()
    {
        var id = Guid.CreateVersion7();
        _library.Documents.Add(new Document
        {
            Id = id, OwnerId = _owner, StoredFileId = Guid.CreateVersion7(), FileName = "a.pdf",
            StoredFile = new StoredFile { Id = Guid.CreateVersion7(), StorageKey = "stored-files/a", OriginalFileName = "a.pdf", ContentType = "application/pdf", ContentSha256 = new string('a', 64), ByteSize = 3 },
        });
        return id;
    }

    [Fact(DisplayName = "PersonalLibraryService.DeleteDocumentAsync/UTC001: owner deletes a shared document row")]
    [Trait("UTC", "PersonalLibraryService.DeleteDocumentAsync/UTC001")]
    [Trait("Trait", "UTC")]
    public async Task DeleteDocument_OwnerDeletesASharedDocumentRow()
    {
        var id = SeedLibraryDocument();

        var failure = await LibraryService().DeleteDocumentAsync(_owner, id);

        Assert.Equal(LibraryFailure.None, failure);
        Assert.Empty(_library.Documents);
        Assert.Equal([_owner], _library.Locks);
    }

    [Fact(DisplayName = "PersonalLibraryService.DeleteDocumentAsync/UTC002: missing document is not found")]
    [Trait("UTC", "PersonalLibraryService.DeleteDocumentAsync/UTC002")]
    [Trait("Trait", "UTC")]
    public async Task DeleteDocument_MissingDocumentIsNotFound()
    {
        var failure = await LibraryService().DeleteDocumentAsync(_owner, Guid.CreateVersion7());

        Assert.Equal(LibraryFailure.NotFound, failure);
        Assert.Equal(0, _library.DeletedDocuments);
        Assert.Equal([_owner], _library.Locks);
    }

    [Fact(DisplayName = "PersonalLibraryService.DeleteDocumentAsync/UTC003: already deleted document is not found")]
    [Trait("UTC", "PersonalLibraryService.DeleteDocumentAsync/UTC003")]
    [Trait("Trait", "UTC")]
    public async Task DeleteDocument_AlreadyDeletedDocumentIsNotFound()
    {
        var id = SeedLibraryDocument();
        Assert.Equal(LibraryFailure.None, await LibraryService().DeleteDocumentAsync(_owner, id));

        var failure = await LibraryService().DeleteDocumentAsync(_owner, id);

        Assert.Equal(LibraryFailure.NotFound, failure);
        Assert.Equal(1, _library.DeletedDocuments);
        Assert.Equal([_owner, _owner], _library.Locks);
    }

    // ---- PersonalLibraryService.DeleteFolderAsync ----

    private Guid SeedFolder(Guid? parent = null, Guid? meetingId = null)
    {
        var folder = new Folder { OwnerId = _owner, ParentId = parent, Name = "F", MeetingId = meetingId, CreatedAt = Now, UpdatedAt = Now };
        _library.Folders[folder.Id] = folder;
        return folder.Id;
    }

    [Fact(DisplayName = "PersonalLibraryService.DeleteFolderAsync/UTC001: owner deletes an empty folder")]
    [Trait("UTC", "PersonalLibraryService.DeleteFolderAsync/UTC001")]
    [Trait("Trait", "UTC")]
    public async Task DeleteFolder_OwnerDeletesAnEmptyFolder()
    {
        var id = SeedFolder();

        var failure = await LibraryService().DeleteFolderAsync(_owner, id);

        Assert.Equal(LibraryFailure.None, failure);
        Assert.Empty(_library.Folders);
        Assert.Equal(1, _library.DeletedFolders);
    }

    [Fact(DisplayName = "PersonalLibraryService.DeleteFolderAsync/UTC002: missing folder is not found")]
    [Trait("UTC", "PersonalLibraryService.DeleteFolderAsync/UTC002")]
    [Trait("Trait", "UTC")]
    public async Task DeleteFolder_MissingFolderIsNotFound()
    {
        var failure = await LibraryService().DeleteFolderAsync(_owner, Guid.CreateVersion7());

        Assert.Equal(LibraryFailure.NotFound, failure);
        Assert.Equal(0, _library.DeletedFolders);
        Assert.Equal([_owner], _library.Locks);
    }

    [Fact(DisplayName = "PersonalLibraryService.DeleteFolderAsync/UTC003: folder with a subfolder is not empty")]
    [Trait("UTC", "PersonalLibraryService.DeleteFolderAsync/UTC003")]
    [Trait("Trait", "UTC")]
    public async Task DeleteFolder_FolderWithASubfolderIsNotEmpty()
    {
        var parent = SeedFolder();
        SeedFolder(parent);

        var failure = await LibraryService().DeleteFolderAsync(_owner, parent);

        Assert.Equal(LibraryFailure.NotEmpty, failure);
        Assert.Equal(2, _library.Folders.Count);
        Assert.Equal(0, _library.DeletedFolders);
    }

    [Fact(DisplayName = "PersonalLibraryService.DeleteFolderAsync/UTC004: empty upload folder is deleted")]
    [Trait("UTC", "PersonalLibraryService.DeleteFolderAsync/UTC004")]
    [Trait("Trait", "UTC")]
    public async Task DeleteFolder_EmptyUploadFolderIsDeleted()
    {
        var id = SeedFolder(meetingId: Guid.CreateVersion7());

        var failure = await LibraryService().DeleteFolderAsync(_owner, id);

        Assert.Equal(LibraryFailure.None, failure);
        Assert.Empty(_library.Folders);
        Assert.Equal(1, _library.DeletedFolders);
    }

    // ---- PersonalLibraryService.RenameDocumentAsync ----

    private Guid SeedRenamableDocument()
    {
        var id = Guid.CreateVersion7();
        _library.Documents.Add(new Document
        {
            Id = id, OwnerId = _owner, StoredFileId = Guid.CreateVersion7(), FileName = "bao-cao.pdf",
            CreatedAt = Now.AddHours(-1), UpdatedAt = Now.AddHours(-1),
            StoredFile = new StoredFile { Id = Guid.CreateVersion7(), StorageKey = "stored-files/a", OriginalFileName = "bao-cao.pdf", ContentType = "application/pdf", ContentSha256 = new string('a', 64), ByteSize = 3 },
        });
        _library.DocumentRow = new LibraryDocument(id, "bao-cao.pdf", "application/pdf", 3, 1, null, Now.AddHours(-1), 0);
        return id;
    }

    [Fact(DisplayName = "PersonalLibraryService.RenameDocumentAsync/UTC001: owner renames a document")]
    [Trait("UTC", "PersonalLibraryService.RenameDocumentAsync/UTC001")]
    [Trait("Trait", "UTC")]
    public async Task RenameDocument_OwnerRenamesADocument()
    {
        var id = SeedRenamableDocument();

        var outcome = await LibraryService().RenameDocumentAsync(_owner, id, "Bao cao Q3.pdf");

        Assert.Equal(LibraryFailure.None, outcome.Failure);
        Assert.Equal("Bao cao Q3.pdf", _library.Documents.Single(d => d.Id == id).FileName);
        Assert.Equal(Now, _library.Documents.Single(d => d.Id == id).UpdatedAt);
    }

    [Fact(DisplayName = "PersonalLibraryService.RenameDocumentAsync/UTC002: name is trimmed, extension case kept")]
    [Trait("UTC", "PersonalLibraryService.RenameDocumentAsync/UTC002")]
    [Trait("Trait", "UTC")]
    public async Task RenameDocument_NameIsTrimmedExtensionCaseKept()
    {
        var id = SeedRenamableDocument();

        var outcome = await LibraryService().RenameDocumentAsync(_owner, id, "  Bao-cao.PDF  ");

        Assert.Equal(LibraryFailure.None, outcome.Failure);
        Assert.NotNull(outcome.Value);
        Assert.Equal("Bao-cao.PDF", _library.Documents.Single(d => d.Id == id).FileName);
    }

    [Fact(DisplayName = "PersonalLibraryService.RenameDocumentAsync/UTC003: missing document is not found")]
    [Trait("UTC", "PersonalLibraryService.RenameDocumentAsync/UTC003")]
    [Trait("Trait", "UTC")]
    public async Task RenameDocument_MissingDocumentIsNotFound()
    {
        var outcome = await LibraryService().RenameDocumentAsync(_owner, Guid.CreateVersion7(), "a.pdf");

        Assert.Equal(LibraryFailure.NotFound, outcome.Failure);
        Assert.Null(outcome.Value);
        Assert.Equal(0, _unitOfWork.Saves);
    }

    [Fact(DisplayName = "PersonalLibraryService.RenameDocumentAsync/UTC004: pdf renamed to html is invalid")]
    [Trait("UTC", "PersonalLibraryService.RenameDocumentAsync/UTC004")]
    [Trait("Trait", "UTC")]
    public async Task RenameDocument_PdfRenamedToHtmlIsInvalid()
    {
        var id = SeedRenamableDocument();

        var outcome = await LibraryService().RenameDocumentAsync(_owner, id, "bao-cao.html");

        Assert.Equal(LibraryFailure.NameInvalid, outcome.Failure);
        Assert.Equal("bao-cao.pdf", _library.Documents.Single(d => d.Id == id).FileName);
        Assert.Equal(0, _unitOfWork.Saves);
    }

    [Fact(DisplayName = "PersonalLibraryService.RenameDocumentAsync/UTC005: 255 characters is accepted")]
    [Trait("UTC", "PersonalLibraryService.RenameDocumentAsync/UTC005")]
    [Trait("Trait", "UTC")]
    public async Task RenameDocument_255CharactersIsAccepted()
    {
        var id = SeedRenamableDocument();
        var name = new string('n', 251) + ".pdf";

        var outcome = await LibraryService().RenameDocumentAsync(_owner, id, name);

        Assert.Equal(LibraryFailure.None, outcome.Failure);
        Assert.NotNull(outcome.Value);
        Assert.Equal(name, _library.Documents.Single(d => d.Id == id).FileName);
        Assert.Equal(255, _library.Documents.Single(d => d.Id == id).FileName.Length);
    }

    // ---- PersonalLibraryService.UploadAsync ----

    [Fact(DisplayName = "PersonalLibraryService.UploadAsync/UTC001: owner uploads two files to a folder")]
    [Trait("UTC", "PersonalLibraryService.UploadAsync/UTC001")]
    [Trait("Trait", "UTC")]
    public async Task LibraryUpload_OwnerUploadsTwoFilesToAFolder()
    {
        var folder = new Folder { OwnerId = _owner, Name = "F1", CreatedAt = Now, UpdatedAt = Now };
        _library.Folders[folder.Id] = folder;

        var outcome = await LibraryService().UploadAsync(_owner, folder.Id,
            [ValidatedFile("Bao-cao.pdf", 2097152, 12), ValidatedFile("Nghi-quyet.docx", 40960, null, "application/vnd.openxmlformats-officedocument.wordprocessingml.document")]);

        Assert.Equal(LibraryFailure.None, outcome.Failure);
        Assert.Equal(2, outcome.Value!.Count);
        Assert.True(outcome.Value.All(d => d.FolderId == folder.Id));
    }

    [Fact(DisplayName = "PersonalLibraryService.UploadAsync/UTC002: deleted folder answers not found")]
    [Trait("UTC", "PersonalLibraryService.UploadAsync/UTC002")]
    [Trait("Trait", "UTC")]
    public async Task LibraryUpload_DeletedFolderAnswersNotFound()
    {
        var outcome = await LibraryService().UploadAsync(_owner, Guid.CreateVersion7(), [ValidatedFile()]);

        Assert.Equal(LibraryFailure.DestinationNotFound, outcome.Failure);
        Assert.Empty(_storage.Objects);
        Assert.Single(_storedFiles.Stamped);
    }

    [Fact(DisplayName = "PersonalLibraryService.UploadAsync/UTC003: one byte over the limit is storage full")]
    [Trait("UTC", "PersonalLibraryService.UploadAsync/UTC003")]
    [Trait("Trait", "UTC")]
    public async Task LibraryUpload_OneByteOverTheLimitIsStorageFull()
    {
        const long fileSize = 1048576;
        _storedFiles.UsedBytes = new FileStorageOptions().MaxTotalBytes - fileSize + 1;

        var outcome = await LibraryService().UploadAsync(_owner, null, [ValidatedFile("a.pdf", fileSize)]);

        Assert.Equal(LibraryFailure.StorageFull, outcome.Failure);
        Assert.Empty(_storedFiles.Added);
        Assert.Empty(_storage.Objects);
    }

    [Fact(DisplayName = "PersonalLibraryService.UploadAsync/UTC004: storage failure answers save failed")]
    [Trait("UTC", "PersonalLibraryService.UploadAsync/UTC004")]
    [Trait("Trait", "UTC")]
    public async Task LibraryUpload_StorageFailureAnswersSaveFailed()
    {
        _storage.FailWrite = new IOException("down");

        var outcome = await LibraryService().UploadAsync(_owner, null, [ValidatedFile()]);

        Assert.Equal(LibraryFailure.SaveFailed, outcome.Failure);
        Assert.Empty(_storage.Objects);
        Assert.Single(_storedFiles.Stamped);
    }

    // ---- DocumentCommentService.AddAsync ----

    private void SeedCommentMeeting(MeetingStatus status = MeetingStatus.Published, bool withMember = false)
    {
        SeedManagerMeeting(status, withMember: withMember);
        _comments.PageInfo = new DocumentPageInfo(DocumentFileRules.PdfContentType, 10);
    }

    [Fact(DisplayName = "DocumentCommentService.AddAsync/UTC001: member comments on page 4")]
    [Trait("UTC", "DocumentCommentService.AddAsync/UTC001")]
    [Trait("Trait", "UTC")]
    public async Task Add_MemberCommentsOnPage4()
    {
        SeedCommentMeeting(withMember: true);
        var docId = Guid.CreateVersion7();

        var outcome = await CommentService().AddAsync(_meeting, docId, new ParticipantActor(_member),
            "Đề nghị bổ sung số liệu quý 3.", 4, null);

        Assert.Equal(CommentFailure.None, outcome.Failure);
        Assert.Equal("Đề nghị bổ sung số liệu quý 3.", outcome.Value!.Body);
        Assert.True(outcome.Value.PageNumber == 4);
        var saved = Assert.Single(_comments.Added);
        Assert.True(saved.AuthorId == _member);
        Assert.True(saved.PageNumber == 4);
    }

    [Fact(DisplayName = "DocumentCommentService.AddAsync/UTC002: blank body is refused before any read")]
    [Trait("UTC", "DocumentCommentService.AddAsync/UTC002")]
    [Trait("Trait", "UTC")]
    public async Task Add_BlankBodyIsRefusedBeforeAnyRead()
    {
        SeedCommentMeeting();

        var outcome = await CommentService().AddAsync(_meeting, Guid.CreateVersion7(),
            new ParticipantActor(_secretary), "   ", null, null);

        Assert.Equal(CommentFailure.Blank, outcome.Failure);
        Assert.Null(outcome.Value);
        Assert.Empty(_unitOfWork.Modes);
    }

    [Fact(DisplayName = "DocumentCommentService.AddAsync/UTC003: 4000 characters are saved")]
    [Trait("UTC", "DocumentCommentService.AddAsync/UTC003")]
    [Trait("Trait", "UTC")]
    public async Task Add_4000CharactersAreSaved()
    {
        SeedCommentMeeting();
        var body = new string('a', 4000);

        var outcome = await CommentService().AddAsync(_meeting, Guid.CreateVersion7(),
            new ParticipantActor(_secretary), body, null, null);

        Assert.Equal(CommentFailure.None, outcome.Failure);
        Assert.Equal(4000, outcome.Value!.Body.Length);
        Assert.Single(_comments.Added);
    }

    [Fact(DisplayName = "DocumentCommentService.AddAsync/UTC004: 4001 characters is too long")]
    [Trait("UTC", "DocumentCommentService.AddAsync/UTC004")]
    [Trait("Trait", "UTC")]
    public async Task Add_4001CharactersIsTooLong()
    {
        SeedCommentMeeting();

        var outcome = await CommentService().AddAsync(_meeting, Guid.CreateVersion7(),
            new ParticipantActor(_secretary), new string('a', 4001), null, null);

        Assert.Equal(CommentFailure.TooLong, outcome.Failure);
        Assert.Null(outcome.Value);
        Assert.Empty(_comments.Added);
    }

    [Fact(DisplayName = "DocumentCommentService.AddAsync/UTC005: ended meeting refuses new comments")]
    [Trait("UTC", "DocumentCommentService.AddAsync/UTC005")]
    [Trait("Trait", "UTC")]
    public async Task Add_EndedMeetingRefusesNewComments()
    {
        SeedCommentMeeting(MeetingStatus.Ended);

        var outcome = await CommentService().AddAsync(_meeting, Guid.CreateVersion7(),
            new ParticipantActor(_secretary), "Thêm ý kiến", null, null);

        Assert.Equal(CommentFailure.Ended, outcome.Failure);
        Assert.Null(outcome.Value);
        Assert.Empty(_comments.Added);
    }
}
