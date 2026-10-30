namespace TrustCabinet.Domain.Documents;

/// <summary>
/// The object store behind <see cref="Entities.StoredFile"/>: Cloudflare R2 on the test server,
/// SeaweedFS in development, an in-memory double in tests (ADR-0024). Bytes always pass through
/// the backend; nothing here hands the browser a URL.
/// </summary>
public interface IFileStorage
{
    /// <summary>False when no store is configured; uploads then fail with MSG166 instead of the app refusing to start.</summary>
    bool IsConfigured { get; }

    Task WriteAsync(string key, Stream content, long length, string contentType, CancellationToken ct);

    /// <summary>
    /// Streams an object back for preview and download (SCRUM-21). The caller disposes the stream.
    /// Throws <see cref="FileNotFoundException"/> when the object does not exist.
    /// </summary>
    Task<Stream> OpenReadAsync(string key, CancellationToken ct);

    /// <summary>Idempotent: deleting a missing object succeeds.</summary>
    Task DeleteAsync(string key, CancellationToken ct);
}

/// <summary>
/// Why a change to a meeting's documents was refused: an upload (UC-33a AF-1), an addition from the
/// library (UC-33a), a removal (UC-33c), or a link to an agenda item (UC-33b, UC-33d) — SCRUM-107, SCRUM-58.
/// Each endpoint maps these to its own message codes.
/// </summary>
public enum DocumentUploadFailure
{
    None, NotFound, Inactive, Forbidden, Ended, Cancelled, Invalid, TooMany, StoreFailed,
    /// <summary>A selected library document is missing or not the actor's (BR-209).</summary>
    LibraryUnavailable,
    /// <summary>A selected library document's file is already in the meeting (BR-193).</summary>
    AlreadyInMeeting,
    /// <summary>The meeting document was removed or belongs to another meeting (BR-211).</summary>
    NotInMeeting,
    /// <summary>The agenda item does not exist in this meeting (BR-278).</summary>
    AgendaItemNotFound,
    /// <summary>A selected document is already linked to the agenda item (BR-277).</summary>
    AlreadyLinked,
    /// <summary>The document is not linked to the agenda item.</summary>
    NotLinked,
    /// <summary>The files do not fit the system's storage limit (ADR-0034).</summary>
    StorageFull,
    /// <summary>The document is already out of the general group (ADR-0043).</summary>
    NotGeneral,
    /// <summary>The document is linked to no agenda item, so the general group is its only place (BR-280).</summary>
    GeneralOnly,
}

/// <summary>A file part as the request delivered it, before anything about it is trusted.</summary>
public sealed record DocumentUploadCandidate(string? FileName, long Length, Func<Stream> OpenRead, string? PageCount);

/// <summary>A file that passed <see cref="DocumentFileRules.ValidateAsync"/>.</summary>
public sealed record DocumentUploadFile(string FileName, string ContentType, long Length, string ContentSha256,
    int? PageCount, Func<Stream> OpenRead);

public sealed record UploadedMeetingDocument(Guid Id, string FileName, string ContentType, long ByteSize, int? PageCount);

public sealed record DocumentUploadOutcome(DocumentUploadFailure Failure = DocumentUploadFailure.None,
    IReadOnlyList<UploadedMeetingDocument>? Documents = null, IReadOnlyList<DocumentUploadFile>? Files = null,
    IReadOnlyDictionary<string, string[]>? Errors = null);

public enum DocumentReadFailure { None, NotFound, DownloadForbidden, ReadFailed }

/// <summary>One row of the Documents tab (UC-32a). <see cref="Id"/> is the MeetingDocument ID.</summary>
public sealed record MeetingDocumentRow(Guid Id, string FileName, string ContentType, long ByteSize, int? PageCount,
    int CommentCount, Meetings.MeetingPerson AddedBy, DateTimeOffset AddedAt);

/// <summary>An agenda item block, in agenda order, with every document linked to it (BR-277).</summary>
public sealed record MeetingDocumentAgendaGroup(Guid Id, int Position, string Title, string Status,
    IReadOnlyList<MeetingDocumentRow> Documents);

/// <summary>
/// UC-32a: the current documents grouped by agenda item, plus those linked to no item (BR-280).
/// <see cref="CurrentAgendaItemId"/> lets the room panel put the item under discussion first (BR-316).
/// </summary>
public sealed record MeetingDocumentList(Guid MeetingId, Guid? CurrentAgendaItemId,
    IReadOnlyList<MeetingDocumentAgendaGroup> AgendaItems, IReadOnlyList<MeetingDocumentRow> General);

public sealed record MeetingDocumentListOutcome(DocumentReadFailure Failure = DocumentReadFailure.None,
    MeetingDocumentList? List = null);

/// <summary>An attached document's stored file, enough to stream it.</summary>
public sealed record AttachedDocumentFile(string FileName, string StorageKey, string ContentType, long ByteSize);

/// <summary>The opened file, unchanged (BR-183, BR-216). The caller disposes <see cref="Content"/>.</summary>
public sealed record MeetingDocumentContent(Stream Content, string FileName, string ContentType, long ByteSize);

public sealed record MeetingDocumentContentOutcome(DocumentReadFailure Failure = DocumentReadFailure.None,
    MeetingDocumentContent? Content = null);

public interface IMeetingDocumentService
{
    /// <summary>UC-32a, for anyone who can currently view the meeting (BR-181, BR-275).</summary>
    Task<MeetingDocumentListOutcome> ListAsync(Guid meetingId, Guid actorId, CancellationToken ct = default);

    /// <summary>
    /// UC-32c: opens a document still attached to the meeting. Downloading a Secret meeting's
    /// document is refused (BR-188); a Confidential or Secret one is audited (BR-318).
    /// </summary>
    Task<MeetingDocumentContentOutcome> OpenContentAsync(Guid meetingId, Guid meetingDocumentId, Guid actorId,
        bool download, CancellationToken ct = default);

    /// <summary>The access check alone, so a caller who may not upload is refused before the body is read.</summary>
    Task<DocumentUploadOutcome> CheckUploadAsync(Guid meetingId, Guid actorId, CancellationToken ct = default);

    /// <summary>UC-33a AF-1: all files or none (BR-194, BR-286).</summary>
    Task<DocumentUploadOutcome> UploadAsync(Guid meetingId, Guid actorId, IReadOnlyList<DocumentUploadFile> files,
        CancellationToken ct = default);

    /// <summary>UC-33a: adds the actor's own library documents as general meeting documents, all or none (BR-193, BR-209, BR-276).</summary>
    Task<DocumentUploadOutcome> AddFromLibraryAsync(Guid meetingId, Guid actorId, IReadOnlyList<Guid> documentIds,
        CancellationToken ct = default);

    /// <summary>UC-33c: marks the document removed and drops its agenda links; its comments stay as history (BR-281, BR-284).</summary>
    Task<DocumentUploadOutcome> RemoveAsync(Guid meetingId, Guid meetingDocumentId, Guid actorId, CancellationToken ct = default);

    /// <summary>
    /// UC-33b: links documents to an agenda item, all or none (BR-304) — documents already in the meeting, or
    /// (AF-2) the actor's library documents, which are added to the meeting first.
    /// </summary>
    Task<DocumentUploadOutcome> AttachAsync(Guid meetingId, Guid agendaItemId, Guid actorId, DocumentAttachSource source,
        IReadOnlyList<Guid> documentIds, CancellationToken ct = default);

    /// <summary>UC-33b AF-1: the access and agenda item check alone, before the body is read.</summary>
    Task<DocumentUploadOutcome> CheckAttachUploadAsync(Guid meetingId, Guid agendaItemId, Guid actorId,
        CancellationToken ct = default);

    /// <summary>UC-33b AF-1: uploads files into the meeting and links them to the agenda item, all or none.</summary>
    Task<DocumentUploadOutcome> AttachUploadAsync(Guid meetingId, Guid agendaItemId, Guid actorId,
        IReadOnlyList<DocumentUploadFile> files, CancellationToken ct = default);

    /// <summary>UC-33d: drops one link; the document stays in the meeting (BR-280).</summary>
    Task<DocumentUploadOutcome> DetachAsync(Guid meetingId, Guid agendaItemId, Guid meetingDocumentId, Guid actorId,
        CancellationToken ct = default);

    /// <summary>ADR-0043: takes a document linked to an agenda item out of the general group; it stays in the meeting.</summary>
    Task<DocumentUploadOutcome> RemoveFromGeneralAsync(Guid meetingId, Guid meetingDocumentId, Guid actorId,
        CancellationToken ct = default);
}

/// <summary>Where UC-33b takes the documents from: the meeting itself, or the actor's library (AF-2).</summary>
public enum DocumentAttachSource { Meeting, Library }

public interface IMeetingDocumentRepository
{
    /// <summary>The owner's upload folder for the meeting, wherever it has been moved or renamed (BR-227).</summary>
    Task<Entities.Folder?> FindUploadFolderAsync(Guid ownerId, Guid meetingId, CancellationToken ct);

    void AddFolder(Entities.Folder folder);

    void AddStoredFiles(IEnumerable<Entities.StoredFile> files);

    void AddUpload(Entities.Document document, Entities.MeetingDocument meetingDocument);

    /// <summary>Removed documents excluded (BR-211).</summary>
    Task<MeetingDocumentList> ListAsync(Guid meetingId, CancellationToken ct);

    /// <summary>Null when the document is not, or no longer, attached to this meeting.</summary>
    Task<AttachedDocumentFile?> FindAttachedFileAsync(Guid meetingId, Guid meetingDocumentId, CancellationToken ct);
}
