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

public enum DocumentUploadFailure { None, NotFound, Inactive, Forbidden, Ended, Cancelled, Invalid, TooMany, StoreFailed }

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
}

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
