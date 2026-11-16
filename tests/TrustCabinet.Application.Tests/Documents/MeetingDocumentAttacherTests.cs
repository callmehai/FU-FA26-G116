using TrustCabinet.Application.Documents;
using TrustCabinet.Application.Tests.Fakes;
using TrustCabinet.Domain.Documents;
using TrustCabinet.Domain.Entities;

namespace TrustCabinet.Application.Tests.Documents;

public sealed class MeetingDocumentAttacherTests
{
    private static readonly DateTimeOffset Now = new(2026, 11, 4, 2, 0, 0, TimeSpan.Zero);

    private readonly FakeMeetingDocumentRepository _documents = new();
    private readonly FakePersonalLibraryRepository _library = new();
    private readonly Guid _actor = Guid.CreateVersion7();
    private readonly Guid _meetingId = Guid.CreateVersion7();
    private readonly Guid _itemId = Guid.CreateVersion7();

    private MeetingDocumentAttacher Attacher() => new(_documents, _library);

    private static Meeting Meeting(Guid id) => new()
    {
        Id = id,
        Title = "Họp giao ban",
        PlannedStartTime = new DateTimeOffset(2026, 10, 23, 18, 30, 0, TimeSpan.Zero),
    };

    [Fact]
    public async Task Adding_library_documents_links_a_file_already_in_the_meeting()
    {
        var storedFile = Guid.CreateVersion7();
        var libraryId = Guid.CreateVersion7();
        _documents.LibraryDocuments.Add(new Document
        {
            Id = libraryId,
            OwnerId = _actor,
            StoredFileId = storedFile,
            FileName = "a.pdf",
            StoredFile = new StoredFile { Id = storedFile, StorageKey = "stored-files/x", OriginalFileName = "a.pdf", ContentType = "application/pdf", ContentSha256 = new string('a', 64), ByteSize = 3 },
        });
        var meetingDocumentId = Guid.CreateVersion7();
        _documents.MeetingDocuments.Add(new MeetingDocument
        {
            Id = meetingDocumentId,
            MeetingId = _meetingId,
            StoredFileId = storedFile,
            FileName = "a.pdf",
        });

        var (failure, added) = await Attacher().AddLibraryDocumentsAsync(Meeting(_meetingId), _actor,
            [libraryId], Now, _itemId, null, default);

        Assert.Equal(DocumentUploadFailure.None, failure);
        Assert.Empty(added);
        var link = Assert.Single(_documents.AgendaLinks);
        Assert.Equal(meetingDocumentId, link.MeetingDocumentId);
        Assert.Equal(_itemId, link.AgendaItemId);
        Assert.Empty(_documents.VoteLinks);
    }

    [Fact]
    public async Task Adding_a_missing_library_document_answers_LibraryUnavailable()
    {
        var (failure, added) = await Attacher().AddLibraryDocumentsAsync(Meeting(_meetingId), _actor,
            [Guid.CreateVersion7()], Now, null, null, default);

        Assert.Equal(DocumentUploadFailure.LibraryUnavailable, failure);
        Assert.Empty(added);
    }

    [Fact]
    public async Task Adding_uploads_creates_the_upload_folder_on_first_use()
    {
        var storedFile = new StoredFile
        {
            Id = Guid.CreateVersion7(),
            StorageKey = "stored-files/a",
            ContentSha256 = new string('a', 64),
            ContentType = "application/pdf",
            ByteSize = 3,
            PageCount = 1,
            OriginalFileName = "a.pdf",
        };

        var added = await Attacher().AddUploadsAsync(Meeting(_meetingId), _actor, [storedFile], Now,
            null, null, default);

        var file = Assert.Single(added);
        Assert.Equal("a.pdf", file.FileName);
        var folder = Assert.Single(_documents.Folders);
        Assert.Equal(_meetingId, folder.MeetingId);
        var document = Assert.Single(_documents.LibraryDocuments);
        Assert.Equal(folder.Id, document.FolderId);
        var meetingDocument = Assert.Single(_documents.MeetingDocuments);
        Assert.Equal(document.Id, meetingDocument.DocumentId);
        Assert.Equal([_actor], _library.Locks);
    }
}
