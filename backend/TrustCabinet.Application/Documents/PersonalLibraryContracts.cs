using TrustCabinet.Domain.Documents;
using TrustCabinet.Domain.Entities;

namespace TrustCabinet.Application.Documents;

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
    /// <summary>The files do not fit the system's storage limit (ADR-0034).</summary>
    StorageFull,
    SaveFailed,
}

public sealed record LibraryFolder(Guid Id, string Name, Guid? ParentId, Guid? MeetingId, DateTimeOffset CreatedAt);

/// <summary>
/// A library document. <see cref="MeetingCount"/> counts the meetings that still use it, among those the
/// reader may view (BR-251). <see cref="InMeeting"/>: its file is already in the meeting the reader asked about
/// and may add documents to (SCRUM-124); false when none was asked about.
/// </summary>
public sealed record LibraryDocument(Guid Id, string FileName, string ContentType, long ByteSize, int? PageCount,
    Guid? FolderId, DateTimeOffset CreatedAt, int MeetingCount, bool InMeeting = false);

/// <summary>
/// UC-34: every folder of the library, so the screen can draw the tree and the move dialog, and the documents
/// at one location. <see cref="StorageUsedBytes"/> and <see cref="StorageLimitBytes"/> are the whole system's
/// (ADR-0034): there is no limit per person.
/// </summary>
public sealed record LibraryView(Guid? FolderId, IReadOnlyList<LibraryFolder> Folders,
    IReadOnlyList<LibraryDocument> Documents, long StorageUsedBytes, long StorageLimitBytes);

public sealed record LibraryOutcome<T>(LibraryFailure Failure = LibraryFailure.None, T? Value = default);

public interface IPersonalLibraryService
{
    /// <summary>
    /// UC-34. <paramref name="readerId"/> is the owner, or an administrator reading a deactivated owner's library
    /// (PO 27/10/2026), which is audited; anyone else gets <see cref="LibraryFailure.NotFound"/>. The caller has
    /// already checked the administrator's capability. <paramref name="meetingId"/> marks the documents whose file
    /// is already in that meeting (SCRUM-124), only when the reader is its current Chair or Secretary — otherwise none
    /// are marked.
    /// </summary>
    Task<LibraryOutcome<LibraryView>> ViewAsync(Guid readerId, Guid ownerId, Guid? folderId, string? search,
        string? fileType, Guid? meetingId = null, CancellationToken ct = default);
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

/// <summary>One folder with whether it hides inside a Secret meeting (BR-188).</summary>
public sealed record LibraryFolderFlag(Guid Id, Guid? ParentId, bool Hidden);

/// <summary>The folders and documents of one library as the use case reads and writes them.</summary>
public interface IPersonalLibraryRepository
{
    /// <summary>
    /// Serialises the owner's library changes until the transaction ends (an advisory lock, no row).
    /// An upload into a meeting takes it after the meeting lock; nothing takes the meeting lock after this one.
    /// </summary>
    Task LockLibraryAsync(Guid ownerId, CancellationToken ct);

    /// <summary>Whether the owner's account exists and is deactivated (PO 27/10/2026).</summary>
    Task<bool> IsDeactivatedAsync(Guid ownerId, CancellationToken ct);

    /// <summary>Whether the folder is the owner's.</summary>
    Task<bool> FolderExistsAsync(Guid ownerId, Guid folderId, CancellationToken ct);

    /// <summary>Whether the reader is the meeting's current Chair or Secretary (BR-184).</summary>
    Task<bool> IsMeetingManagerAsync(Guid meetingId, Guid readerId, CancellationToken ct);

    /// <summary>Every folder of the owner (id to parent), read under the owner's lock.</summary>
    Task<Dictionary<Guid, Guid?>> ListFolderParentsAsync(Guid ownerId, CancellationToken ct);

    /// <summary>The folder, tracked, with nothing else; throws when it does not exist.</summary>
    Task<Folder> FindFolderAsync(Guid folderId, CancellationToken ct);

    /// <summary>Stages the folder; written by the next <c>IUnitOfWork.SaveChangesAsync</c>.</summary>
    void AddFolder(Folder folder);

    /// <summary>Deletes the folder. Runs immediately, not staged.</summary>
    Task<int> DeleteFolderAsync(Guid folderId, CancellationToken ct);

    /// <summary>Whether the folder still holds a document (BR-223).</summary>
    Task<bool> HasDocumentsAsync(Guid folderId, CancellationToken ct);

    /// <summary>Deletes the library row. Runs immediately, not staged.</summary>
    Task<int> DeleteDocumentAsync(Guid ownerId, Guid documentId, CancellationToken ct);

    /// <summary>The library document, tracked with its stored file; null when missing or someone else's.</summary>
    Task<Document?> FindDocumentAsync(Guid ownerId, Guid documentId, CancellationToken ct);

    /// <summary>Stages the documents; written by the next <c>IUnitOfWork.SaveChangesAsync</c>.</summary>
    void AddDocuments(IEnumerable<Document> documents);

    /// <summary>Every folder of the owner with whether it hides inside a Secret meeting.</summary>
    Task<IReadOnlyList<LibraryFolderFlag>> ListFolderFlagsAsync(Guid ownerId, CancellationToken ct);

    /// <summary>Every folder of the owner, untracked, ordered by name then id.</summary>
    Task<IReadOnlyList<Folder>> ListFoldersAsync(Guid ownerId, CancellationToken ct);

    /// <summary>
    /// The documents at the location, oldest name first, each with its meeting count among the meetings the
    /// reader may view (BR-251) and whether its file is already in the asked meeting (BR-193, SCRUM-124).
    /// Another reader than the owner is not shown the files of Secret meetings (BR-188).
    /// </summary>
    Task<IReadOnlyList<LibraryDocument>> ListDocumentsAsync(Guid readerId, Guid ownerId, Guid? folderId,
        string? search, string? contentType, Guid? meetingId, CancellationToken ct);

    /// <summary>
    /// The document's stored file, enough to stream it. Null when missing, someone else's, or hidden from
    /// an administrator (the files of Secret meetings, BR-188).
    /// </summary>
    Task<AttachedDocumentFile?> FindLibraryFileAsync(Guid ownerId, Guid documentId,
        HashSet<Guid>? hiddenFolderIds, CancellationToken ct);

    /// <summary>The changed document as the reader views it; throws when it does not exist.</summary>
    Task<LibraryDocument> GetDocumentRowAsync(Guid documentId, Guid readerId, CancellationToken ct);
}