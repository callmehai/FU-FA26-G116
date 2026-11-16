using TrustCabinet.Application.Participants;
using TrustCabinet.Domain.Documents;
using TrustCabinet.Domain.Entities;

namespace TrustCabinet.Application.Documents;

/// <summary>
/// The object store behind <see cref="Domain.Entities.StoredFile"/>: Cloudflare R2 on the test server,
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

/// <summary><see cref="FileMissing"/>: the row is there but its object is gone from the store, so trying again cannot help.</summary>
public enum DocumentReadFailure { None, NotFound, DownloadForbidden, ReadFailed, FileMissing }

/// <summary>One row of the Documents tab (UC-32a). <see cref="Id"/> is the MeetingDocument ID.</summary>
public sealed record MeetingDocumentRow(Guid Id, string FileName, string ContentType, long ByteSize, int? PageCount,
    int CommentCount, Meetings.MeetingPerson AddedBy, DateTimeOffset AddedAt);

/// <summary>An agenda item block, in agenda order, with every document linked to it (BR-277).</summary>
public sealed record MeetingDocumentAgendaGroup(Guid Id, int Position, string Title, string Status,
    IReadOnlyList<MeetingDocumentRow> Documents);

/// <summary>A vote's document (UC-32a AF-1): the row of the Documents tab plus when the reader confirmed reading it (UC-74).</summary>
public sealed record MeetingVoteDocumentRow(Guid Id, string FileName, string ContentType, long ByteSize, int? PageCount,
    int CommentCount, Meetings.MeetingPerson AddedBy, DateTimeOffset AddedAt, DateTimeOffset? ReadConfirmedAt);

/// <summary>
/// A vote the reader may see, in the order votes are created, with its documents still in the meeting.
/// <see cref="CanConfirmRead"/>: the reader is on the vote's voter list and the vote is Open in an InProgress meeting,
/// the only case in which UC-74 accepts a confirmation, so the only one in which "not read yet" means anything.
/// </summary>
public sealed record MeetingDocumentVoteGroup(Guid Id, string Title, string Status, bool CanConfirmRead,
    IReadOnlyList<MeetingVoteDocumentRow> Documents);

/// <summary>
/// UC-32a: the current documents grouped by agenda item, plus those linked to no item (BR-280), plus (AF-1) the
/// documents of each vote the reader may see — a vote's document still counts as general when no item links it.
/// <see cref="CurrentAgendaItemId"/> lets the room panel put the item under discussion first (BR-316).
/// </summary>
public sealed record MeetingDocumentList(Guid MeetingId, Guid? CurrentAgendaItemId,
    IReadOnlyList<MeetingDocumentAgendaGroup> AgendaItems, IReadOnlyList<MeetingDocumentRow> General,
    IReadOnlyList<MeetingDocumentVoteGroup> Votes);

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
    /// <summary>
    /// UC-32a, for anyone who can currently view the meeting (BR-181, BR-275), or a guest of it whose UC-04
    /// session is current (SCRUM-123).
    /// </summary>
    Task<MeetingDocumentListOutcome> ListAsync(Guid meetingId, ParticipantActor reader, CancellationToken ct = default);

    /// <summary>
    /// UC-32c: opens a document still attached to the meeting. Downloading a Secret meeting's
    /// document is refused (BR-188); viewing one is audited (BR-318).
    /// </summary>
    Task<MeetingDocumentContentOutcome> OpenContentAsync(Guid meetingId, Guid meetingDocumentId, ParticipantActor reader,
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
}

/// <summary>Where UC-33b takes the documents from: the meeting itself, or the actor's library (AF-2).</summary>
public enum DocumentAttachSource { Meeting, Library }

public interface IMeetingDocumentRepository
{
    /// <summary>The owner's upload folder for the meeting, wherever it has been moved or renamed (BR-227).</summary>
    Task<Domain.Entities.Folder?> FindUploadFolderAsync(Guid ownerId, Guid meetingId, CancellationToken ct);

    void AddFolder(Domain.Entities.Folder folder);

    void AddStoredFiles(IEnumerable<Domain.Entities.StoredFile> files);

    /// <summary>Stages the new library row and its meeting row; written by the next <c>IUnitOfWork.SaveChangesAsync</c>.</summary>
    void AddUpload(Domain.Entities.Document document, Domain.Entities.MeetingDocument meetingDocument);

    /// <summary>
    /// Stages the new meeting row for an existing library document (no byte is copied); written by the next
    /// <c>IUnitOfWork.SaveChangesAsync</c>.
    /// </summary>
    void AddMeetingDocument(Domain.Entities.MeetingDocument meetingDocument);

    /// <summary>Removed documents excluded (BR-211); <see cref="MeetingDocumentList.Votes"/> left empty.</summary>
    Task<MeetingDocumentList> ListAsync(Guid meetingId, CancellationToken ct);

    /// <summary>
    /// UC-32a AF-1: the votes that link a document still in the meeting — drafts only with
    /// <paramref name="includeDrafts"/> (BR-253) — each with the reader's own read confirmations (BR-291).
    /// </summary>
    Task<IReadOnlyList<MeetingDocumentVoteGroup>> ListVoteGroupsAsync(Guid meetingId, bool includeDrafts, Guid? userId,
        Guid? guestInvitationId, CancellationToken ct);

    /// <summary>
    /// Null when the document is not, or no longer, attached to this meeting, or, without
    /// <paramref name="includeDrafts"/>, reached only through draft votes (BR-253).
    /// </summary>
    Task<AttachedDocumentFile?> FindAttachedFileAsync(Guid meetingId, Guid meetingDocumentId, bool includeDrafts,
        CancellationToken ct);

    /// <summary>
    /// The meeting document still in the meeting, tracked with its agenda links, for a removal that keeps the
    /// row as history (BR-197, BR-281, BR-284); null when removed or of another meeting (BR-211).
    /// </summary>
    Task<MeetingDocument?> FindForRemoveAsync(Guid meetingId, Guid meetingDocumentId, CancellationToken ct);

    /// <summary>Stages the removal of agenda links; written by the next <c>IUnitOfWork.SaveChangesAsync</c>.</summary>
    void RemoveAgendaItemLinks(IEnumerable<MeetingDocumentAgendaItem> links);

    /// <summary>Whether the document is still attached to the meeting (BR-211).</summary>
    Task<bool> ExistsAsync(Guid meetingId, Guid meetingDocumentId, CancellationToken ct);

    /// <summary>Whether the agenda item is part of the meeting (BR-278).</summary>
    Task<bool> AgendaItemExistsAsync(Guid meetingId, Guid agendaItemId, CancellationToken ct);

    /// <summary>Each requested document still in the meeting, with whether it already links the agenda item.</summary>
    Task<IReadOnlyList<MeetingDocumentLinkState>> FindLinkStatesAsync(Guid meetingId, Guid agendaItemId,
        Guid[] documentIds, CancellationToken ct);

    /// <summary>Stages agenda links; written by the next <c>IUnitOfWork.SaveChangesAsync</c>.</summary>
    void AddAgendaItemLinks(IEnumerable<MeetingDocumentAgendaItem> links);

    /// <summary>The one link to drop (UC-33d); null when the document was never linked to the item.</summary>
    Task<MeetingDocumentAgendaItem?> FindAgendaItemLinkAsync(Guid meetingDocumentId, Guid agendaItemId,
        CancellationToken ct);

    /// <summary>Stages the removal of the link; written by the next <c>IUnitOfWork.SaveChangesAsync</c>.</summary>
    void RemoveAgendaItemLink(MeetingDocumentAgendaItem link);

    /// <summary>The actor's library documents, tracked with their stored file (BR-209).</summary>
    Task<IReadOnlyList<Document>> FindLibraryDocumentsAsync(Guid ownerId, Guid[] documentIds, CancellationToken ct);

    /// <summary>Every unremoved meeting document that points at one of the files.</summary>
    Task<IReadOnlyList<MeetingDocumentFileState>> FindMeetingFilesAsync(Guid meetingId, Guid[] storedFileIds,
        Guid? agendaItemId, Guid? voteId, CancellationToken ct);

    /// <summary>Stages vote links; written by the next <c>IUnitOfWork.SaveChangesAsync</c>.</summary>
    void AddVoteDocuments(IEnumerable<VoteDocument> links);
}

/// <summary>One meeting document of an attach request, with whether it already links the agenda item.</summary>
public sealed record MeetingDocumentLinkState(Guid Id, bool Linked);

/// <summary>One file already in the meeting, with whether it already links the item or the vote.</summary>
public sealed record MeetingDocumentFileState(Guid Id, Guid StoredFileId, bool Linked);

/// <summary>The stored files as the uploads and the orphan purge read and stamp them (ADR-0024 §4, ADR-0034, BR-213).</summary>
public interface IStoredFileRepository
{
    /// <summary>
    /// What every stored document together takes: every file not purged yet, whether anything still points at it
    /// (an unreferenced file takes room until the orphan purge deletes it).
    /// </summary>
    Task<long> UsedBytesAsync(CancellationToken ct);

    /// <summary>Stages the reserved rows; written by the next <c>IUnitOfWork.SaveChangesAsync</c>.</summary>
    void AddStoredFiles(IEnumerable<Domain.Entities.StoredFile> files);

    /// <summary>
    /// Takes the advisory lock for the whole system until the current transaction ends, so two uploads cannot
    /// both take the last room of the storage limit (ADR-0034). Locks no row.
    /// </summary>
    Task LockStorageAsync(CancellationToken ct);

    /// <summary>
    /// Stamps the rows purged (BR-213, BR-286): they stop taking room in the storage limit at once.
    /// Runs immediately, not staged.
    /// </summary>
    Task<int> StampPurgedAsync(Guid[] ids, DateTimeOffset now, CancellationToken ct);

    /// <summary>
    /// Stamps the one orphan purged, when it is still unstamped. Runs immediately, not staged.
    /// </summary>
    Task<int> TryStampPurgedAsync(Guid id, DateTimeOffset now, CancellationToken ct);

    /// <summary>
    /// The oldest unreferenced files past <paramref name="cutoff"/>, skipping <paramref name="exclude"/>
    /// (whose delete failed earlier in this run), at most <paramref name="take"/>: a file no library row and
    /// no active meeting document points at any more.
    /// </summary>
    Task<IReadOnlyList<StoredFileRef>> ListOrphansAsync(DateTimeOffset cutoff, IReadOnlyList<Guid> exclude, int take,
        CancellationToken ct);
}

/// <summary>One unreferenced file past its grace period (BR-213).</summary>
public sealed record StoredFileRef(Guid Id, string StorageKey);

/// <summary>BR-213: deletes the object of every unreferenced stored file past its grace period, then stamps it.</summary>
public interface IOrphanFilePurger
{
    Task<int> PurgeAsync(CancellationToken ct);
}

/// <summary>
/// The two ways files become meeting documents, shared by meetings (UC-33a, UC-33b AF-1, UC-76a AF-1) and votes
/// (UC-76a AF-2): the library documents (no byte is copied, ADR-0018) and the uploads (a library document in
/// the meeting's upload folder and a meeting document per stored file, BR-210). Runs inside the caller's
/// transaction: the caller saves and commits.
/// </summary>
public interface IMeetingDocumentAttacher
{
    /// <summary>
    /// UC-33a, UC-33b AF-2 and UC-76a AF-2: the actor's own library documents (BR-209) become meeting documents
    /// under their current name (BR-276) — linked to <paramref name="agendaItemId"/> or attached to
    /// <paramref name="voteId"/> when given. A file already in the meeting is refused for the general group
    /// (BR-193); for an item or a vote its meeting document is linked instead, unless it already is.
    /// </summary>
    Task<(DocumentUploadFailure Failure, IReadOnlyList<UploadedMeetingDocument> Added)> AddLibraryDocumentsAsync(
        Domain.Entities.Meeting meeting, Guid actorId, IReadOnlyList<Guid> documentIds, DateTimeOffset now,
        Guid? agendaItemId, Guid? voteId, CancellationToken ct);

    /// <summary>
    /// UC-33a AF-1, UC-33b AF-1 and UC-76a AF-1: a library document in the meeting's upload folder and a meeting
    /// document per stored file, both pointing at it (BR-210) — linked to <paramref name="agendaItemId"/> or
    /// attached to <paramref name="voteId"/> when given.
    /// </summary>
    Task<IReadOnlyList<UploadedMeetingDocument>> AddUploadsAsync(Domain.Entities.Meeting meeting, Guid actorId,
        Domain.Entities.StoredFile[] stored, DateTimeOffset now, Guid? agendaItemId, Guid? voteId,
        CancellationToken ct);
}
