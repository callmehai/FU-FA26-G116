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

public enum DocumentUploadFailure { None, NotFound, Inactive, Forbidden, Ended, Invalid, TooMany, StoreFailed }

/// <summary>A file part as the request delivered it, before anything about it is trusted.</summary>
public sealed record DocumentUploadCandidate(string? FileName, long Length, Func<Stream> OpenRead, string? PageCount);

/// <summary>A file that passed <see cref="DocumentFileRules.ValidateAsync"/>.</summary>
public sealed record DocumentUploadFile(string FileName, string ContentType, long Length, string ContentSha256,
    int? PageCount, Func<Stream> OpenRead);

public sealed record UploadedMeetingDocument(Guid Id, string FileName, string ContentType, long ByteSize, int? PageCount);

public sealed record DocumentUploadOutcome(DocumentUploadFailure Failure = DocumentUploadFailure.None,
    IReadOnlyList<UploadedMeetingDocument>? Documents = null, IReadOnlyList<DocumentUploadFile>? Files = null,
    IReadOnlyDictionary<string, string[]>? Errors = null);

public interface IMeetingDocumentService
{
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
}
