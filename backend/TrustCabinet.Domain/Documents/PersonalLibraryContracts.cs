namespace TrustCabinet.Domain.Documents;

/// <summary>Why a personal-library operation was refused (SCRUM-110, SCRUM-111). Each endpoint maps these to its own codes.</summary>
public enum LibraryFailure
{
    None,
    /// <summary>The folder or document is missing or someone else's: the same answer for both (BR-242).</summary>
    NotFound,
    /// <summary>The destination folder is missing or someone else's.</summary>
    DestinationNotFound,
    NameRequired,
    NameTooLong,
    /// <summary>A document name that is blank, unusable, or no longer ends in the file's own extension.</summary>
    NameInvalid,
    /// <summary>The folder would be deeper than level five (BR-191).</summary>
    TooDeep,
    /// <summary>A folder moved into itself or one of its subfolders (BR-222).</summary>
    IntoItself,
    /// <summary>A folder that still holds a document or a subfolder (BR-223).</summary>
    NotEmpty,
    /// <summary>The upload would take the library past its quota (PO 27/10/2026).</summary>
    QuotaExceeded,
    SaveFailed,
}

public sealed record LibraryFolder(Guid Id, string Name, Guid? ParentId, Guid? MeetingId, DateTimeOffset CreatedAt);

/// <summary>
/// A library document. <see cref="MeetingCount"/> counts the meetings that still use it, among those the
/// reader may view (BR-251).
/// </summary>
public sealed record LibraryDocument(Guid Id, string FileName, string ContentType, long ByteSize, int? PageCount,
    Guid? FolderId, DateTimeOffset CreatedAt, int MeetingCount);

/// <summary>
/// UC-34: every folder of the library, so the screen can draw the tree and the move dialog, and the documents
/// at one location. <see cref="UsedBytes"/> covers the whole library.
/// </summary>
public sealed record LibraryView(Guid? FolderId, IReadOnlyList<LibraryFolder> Folders,
    IReadOnlyList<LibraryDocument> Documents, long UsedBytes, long QuotaBytes);

public sealed record LibraryOutcome<T>(LibraryFailure Failure = LibraryFailure.None, T? Value = default);

public interface IPersonalLibraryService
{
    /// <summary>
    /// UC-34. <paramref name="readerId"/> is the owner, or an administrator reading a deactivated owner's library
    /// (PO 27/10/2026), which is audited; anyone else gets <see cref="LibraryFailure.NotFound"/>. The caller has
    /// already checked the administrator's capability.
    /// </summary>
    Task<LibraryOutcome<LibraryView>> ViewAsync(Guid readerId, Guid ownerId, Guid? folderId, string? search,
        string? fileType, CancellationToken ct = default);

    /// <summary>The same reader rule as <see cref="ViewAsync"/>; an administrator's read is audited.</summary>
    Task<MeetingDocumentContentOutcome> OpenContentAsync(Guid readerId, Guid ownerId, Guid documentId,
        CancellationToken ct = default);

    /// <summary>UC-72a.</summary>
    Task<LibraryOutcome<LibraryFolder>> CreateFolderAsync(Guid ownerId, Guid? parentId, string? name,
        CancellationToken ct = default);

    /// <summary>UC-72b.</summary>
    Task<LibraryOutcome<LibraryFolder>> RenameFolderAsync(Guid ownerId, Guid folderId, string? name,
        CancellationToken ct = default);

    /// <summary>UC-72c: <paramref name="parentId"/> null moves it to the library root.</summary>
    Task<LibraryOutcome<LibraryFolder>> MoveFolderAsync(Guid ownerId, Guid folderId, Guid? parentId,
        CancellationToken ct = default);

    /// <summary>UC-72d.</summary>
    Task<LibraryFailure> DeleteFolderAsync(Guid ownerId, Guid folderId, CancellationToken ct = default);

    /// <summary>UC-36: the destination and store check alone, so a bad destination is refused before the body is read.</summary>
    Task<LibraryFailure> CheckUploadAsync(Guid ownerId, Guid? folderId, CancellationToken ct = default);

    /// <summary>UC-36: all files or none (BR-286).</summary>
    Task<LibraryOutcome<IReadOnlyList<LibraryDocument>>> UploadAsync(Guid ownerId, Guid? folderId,
        IReadOnlyList<DocumentUploadFile> files, CancellationToken ct = default);

    /// <summary>UC-71b: the meeting copies keep the name they were added with (BR-276).</summary>
    Task<LibraryOutcome<LibraryDocument>> RenameDocumentAsync(Guid ownerId, Guid documentId, string? fileName,
        CancellationToken ct = default);

    /// <summary>UC-71c: <paramref name="folderId"/> null moves it to the library root.</summary>
    Task<LibraryOutcome<LibraryDocument>> MoveDocumentAsync(Guid ownerId, Guid documentId, Guid? folderId,
        CancellationToken ct = default);

    /// <summary>UC-71a: removes the library row only; meetings using the file keep it (BR-186, BR-232).</summary>
    Task<LibraryFailure> DeleteDocumentAsync(Guid ownerId, Guid documentId, CancellationToken ct = default);
}
