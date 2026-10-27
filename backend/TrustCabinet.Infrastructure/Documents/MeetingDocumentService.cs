using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using TrustCabinet.Domain.Audit;
using TrustCabinet.Domain.Documents;
using TrustCabinet.Domain.Entities;
using TrustCabinet.Domain.Enums;
using TrustCabinet.Domain.Meetings;

namespace TrustCabinet.Infrastructure.Documents;

public sealed class MeetingDocumentRepository(TrustCabinetDbContext db) : IMeetingDocumentRepository
{
    public Task<Folder?> FindUploadFolderAsync(Guid ownerId, Guid meetingId, CancellationToken ct)
        => db.Folders.SingleOrDefaultAsync(f => f.OwnerId == ownerId && f.MeetingId == meetingId, ct);

    public void AddFolder(Folder folder) => db.Folders.Add(folder);

    public void AddStoredFiles(IEnumerable<StoredFile> files) => db.StoredFiles.AddRange(files);

    public void AddUpload(Document document, MeetingDocument meetingDocument)
    {
        db.Documents.Add(document);
        db.MeetingDocuments.Add(meetingDocument);
    }

    public async Task<MeetingDocumentList> ListAsync(Guid meetingId, CancellationToken ct)
    {
        var items = await db.AgendaItems.AsNoTracking().Where(i => i.MeetingId == meetingId)
            .OrderBy(i => i.Position).ThenBy(i => i.Id).Select(i => new { i.Id, i.Position, i.Title, i.Status }).ToListAsync(ct);
        var rows = await db.MeetingDocuments.AsNoTracking()
            .Where(d => d.MeetingId == meetingId && d.RemovedAt == null)
            .OrderBy(d => d.AddedAt).ThenBy(d => d.Id)
            .Select(d => new
            {
                Row = new MeetingDocumentRow(d.Id, d.FileName, d.StoredFile.ContentType, d.StoredFile.ByteSize,
                    d.StoredFile.PageCount, d.Comments.Count, new MeetingPerson(d.AddedById, d.AddedBy.FullName), d.AddedAt),
                ItemIds = d.AgendaItemLinks.Select(l => l.AgendaItemId).ToList(),
            }).ToListAsync(ct);

        // A document linked to several items shows under each (BR-277); one linked to none is general (BR-280).
        var groups = items.Select(i => new MeetingDocumentAgendaGroup(i.Id, i.Position, i.Title, i.Status.ToString(),
            rows.Where(r => r.ItemIds.Contains(i.Id)).Select(r => r.Row).ToList())).ToList();
        var general = rows.Where(r => r.ItemIds.Count == 0).Select(r => r.Row).ToList();
        var current = items.FirstOrDefault(i => i.Status == AgendaItemStatus.InProgress)?.Id;
        return new(meetingId, current, groups, general);
    }

    public Task<AttachedDocumentFile?> FindAttachedFileAsync(Guid meetingId, Guid meetingDocumentId, CancellationToken ct)
        => db.MeetingDocuments.AsNoTracking()
            .Where(d => d.Id == meetingDocumentId && d.MeetingId == meetingId && d.RemovedAt == null)
            .Select(d => new AttachedDocumentFile(d.FileName, d.StoredFile.StorageKey, d.StoredFile.ContentType,
                d.StoredFile.ByteSize))
            .SingleOrDefaultAsync(ct);
}

public sealed class MeetingDocumentService(IMeetingRepository meetings, IMeetingDocumentRepository documents,
    TrustCabinetDbContext db, StoredFileWriter writer, IFileStorage storage, IAuditService audit, TimeProvider clock,
    ILogger<MeetingDocumentService> logger) : IMeetingDocumentService
{
    private static bool PersistenceError(Exception ex) => StoredFileWriter.PersistenceError(ex);

    public async Task<MeetingDocumentListOutcome> ListAsync(Guid meetingId, Guid actorId, CancellationToken ct = default)
    {
        try
        {
            var meeting = await meetings.FindWorkflowAsync(meetingId, ct: ct);
            // Same answer for a meeting that does not exist and one the caller cannot see (SCRUM-22).
            if (meeting is null || !MeetingReadPolicy.CanRead(meeting, actorId)) return new(DocumentReadFailure.NotFound);
            return new(List: await documents.ListAsync(meetingId, ct));
        }
        catch (Exception ex) when (PersistenceError(ex))
        {
            logger.LogError(ex, "Failed to list documents of meeting {MeetingId}", meetingId);
            return new(DocumentReadFailure.ReadFailed);
        }
    }

    public async Task<MeetingDocumentContentOutcome> OpenContentAsync(Guid meetingId, Guid meetingDocumentId,
        Guid actorId, bool download, CancellationToken ct = default)
    {
        Meeting? meeting;
        AttachedDocumentFile? file;
        try
        {
            meeting = await meetings.FindWorkflowAsync(meetingId, ct: ct);
            if (meeting is null || !MeetingReadPolicy.CanRead(meeting, actorId)) return new(DocumentReadFailure.NotFound);
            file = await documents.FindAttachedFileAsync(meetingId, meetingDocumentId, ct);
        }
        catch (Exception ex) when (PersistenceError(ex))
        {
            logger.LogError(ex, "Failed to look up document {DocumentId} of meeting {MeetingId}", meetingDocumentId, meetingId);
            return new(DocumentReadFailure.ReadFailed);
        }
        // Removed, or never in this meeting: the same answer, so an ID from another meeting reveals nothing (BR-211).
        if (file is null) return new(DocumentReadFailure.NotFound);
        if (download && meeting.Sensitivity == Sensitivity.Secret) return new(DocumentReadFailure.DownloadForbidden);

        Stream content;
        try
        {
            content = await storage.OpenReadAsync(file.StorageKey, ct);
        }
        catch (Exception ex) when (ex is not OperationCanceledException || !ct.IsCancellationRequested)
        {
            // A missing object, an unconfigured store or the store being down all read as "try again".
            logger.LogError(ex, "Failed to open stored object {StorageKey} for document {DocumentId}", file.StorageKey,
                meetingDocumentId);
            return new(DocumentReadFailure.ReadFailed);
        }

        if (meeting.Sensitivity is Sensitivity.Confidential or Sensitivity.Secret)
        {
            // Recorded only once the file is actually there to hand over, and before it is: no
            // audit row, no content (BR-318).
            try
            {
                await audit.RecordAsync(download ? AuditActions.DocumentDownloaded : AuditActions.DocumentViewed,
                    AuditActions.MeetingDocumentObjectType, meetingDocumentId, actorId,
                    new { meetingId, fileName = file.FileName, sensitivity = meeting.Sensitivity.ToString() }, ct);
            }
            catch (Exception ex)
            {
                await content.DisposeAsync();
                if (!PersistenceError(ex)) throw;
                logger.LogError(ex, "Failed to audit access to document {DocumentId}", meetingDocumentId);
                return new(DocumentReadFailure.ReadFailed);
            }
        }

        return new(Content: new(content, file.FileName, file.ContentType, file.ByteSize));
    }

    public async Task<DocumentUploadOutcome> CheckUploadAsync(Guid meetingId, Guid actorId, CancellationToken ct = default)
    {
        try
        {
            var meeting = await meetings.FindWorkflowAsync(meetingId, ct: ct);
            var access = DocumentFileRules.UploadAccess(meeting, actorId, await meetings.IsUserActiveAsync(actorId, ct));
            if (access == DocumentUploadFailure.None && !writer.IsConfigured)
            {
                logger.LogError("Document upload refused: FileStorage is not configured.");
                return new(DocumentUploadFailure.StoreFailed);
            }
            return new(access);
        }
        catch (Exception ex) when (PersistenceError(ex))
        {
            logger.LogError(ex, "Failed to check document upload access for meeting {MeetingId}", meetingId);
            return new(DocumentUploadFailure.StoreFailed);
        }
    }

    public async Task<DocumentUploadOutcome> UploadAsync(Guid meetingId, Guid actorId,
        IReadOnlyList<DocumentUploadFile> files, CancellationToken ct = default)
        => await writer.WriteAsync(actorId, files, async (stored, now, token) =>
        {
            // Re-checked under the meeting lock: the meeting may have ended, or the actor lost
            // the role, while the files were uploading (EX-1, EX-6).
            var (meeting, access) = await LockAsync(meetingId, actorId, token);
            if (access != DocumentUploadFailure.None) return new DocumentUploadOutcome(access);
            var added = await AddUploadsAsync(meeting!, actorId, stored, now, null, token);
            await meetings.SaveChangesAsync(token);
            return new DocumentUploadOutcome(Documents: added);
        }, outcome => outcome.Failure == DocumentUploadFailure.None, new(DocumentUploadFailure.StoreFailed), ct);

    public Task<DocumentUploadOutcome> AddFromLibraryAsync(Guid meetingId, Guid actorId, IReadOnlyList<Guid> documentIds,
        CancellationToken ct = default)
        => InMeetingLockAsync(meetingId, actorId, "add library documents to",
            (meeting, now, token) => AddLibraryDocumentsAsync(meeting, actorId, documentIds, now, null, token), ct);

    public Task<DocumentUploadOutcome> RemoveAsync(Guid meetingId, Guid meetingDocumentId, Guid actorId,
        CancellationToken ct = default)
        => InMeetingLockAsync(meetingId, actorId, "remove a document from", async (meeting, now, token) =>
        {
            var document = await db.MeetingDocuments.Include(d => d.AgendaItemLinks)
                .SingleOrDefaultAsync(d => d.Id == meetingDocumentId && d.MeetingId == meeting.Id && d.RemovedAt == null, token);
            // Removed, or never in this meeting: the same answer (BR-211).
            if (document is null) return DocumentUploadFailure.NotInMeeting;
            // The row and its comments stay as history, out of every list (BR-197, BR-281, BR-284).
            document.RemovedAt = now;
            document.RemovedById = actorId;
            document.UpdatedAt = now;
            db.MeetingDocumentAgendaItems.RemoveRange(document.AgendaItemLinks);
            return DocumentUploadFailure.None;
        }, ct);

    public Task<DocumentUploadOutcome> AttachAsync(Guid meetingId, Guid agendaItemId, Guid actorId,
        DocumentAttachSource source, IReadOnlyList<Guid> documentIds, CancellationToken ct = default)
        => InMeetingLockAsync(meetingId, actorId, "link documents in", async (meeting, now, token) =>
        {
            if (!await ItemExistsAsync(meeting.Id, agendaItemId, token)) return DocumentUploadFailure.AgendaItemNotFound;
            if (source == DocumentAttachSource.Library)
                return await AddLibraryDocumentsAsync(meeting, actorId, documentIds, now, agendaItemId, token);

            var ids = documentIds.Distinct().ToArray();
            var found = await db.MeetingDocuments
                .Where(d => ids.Contains(d.Id) && d.MeetingId == meeting.Id && d.RemovedAt == null)
                .Select(d => new { d.Id, Linked = d.AgendaItemLinks.Any(l => l.AgendaItemId == agendaItemId) })
                .ToListAsync(token);
            if (ids.Length == 0 || found.Count != ids.Length) return DocumentUploadFailure.NotInMeeting;
            if (found.Any(d => d.Linked)) return DocumentUploadFailure.AlreadyLinked;
            // Links to other items stay as they are (BR-277).
            db.MeetingDocumentAgendaItems.AddRange(ids.Select(id => new MeetingDocumentAgendaItem
                { MeetingDocumentId = id, AgendaItemId = agendaItemId }));
            return DocumentUploadFailure.None;
        }, ct);

    public async Task<DocumentUploadOutcome> CheckAttachUploadAsync(Guid meetingId, Guid agendaItemId, Guid actorId,
        CancellationToken ct = default)
    {
        var access = await CheckUploadAsync(meetingId, actorId, ct);
        if (access.Failure != DocumentUploadFailure.None) return access;
        try
        {
            return await ItemExistsAsync(meetingId, agendaItemId, ct) ? access : new(DocumentUploadFailure.AgendaItemNotFound);
        }
        catch (Exception ex) when (PersistenceError(ex))
        {
            logger.LogError(ex, "Failed to check agenda item {AgendaItemId} of meeting {MeetingId}", agendaItemId, meetingId);
            return new(DocumentUploadFailure.StoreFailed);
        }
    }

    public async Task<DocumentUploadOutcome> AttachUploadAsync(Guid meetingId, Guid agendaItemId, Guid actorId,
        IReadOnlyList<DocumentUploadFile> files, CancellationToken ct = default)
        => await writer.WriteAsync(actorId, files, async (stored, now, token) =>
        {
            var (meeting, access) = await LockAsync(meetingId, actorId, token);
            if (access != DocumentUploadFailure.None) return new DocumentUploadOutcome(access);
            if (!await ItemExistsAsync(meetingId, agendaItemId, token))
                return new DocumentUploadOutcome(DocumentUploadFailure.AgendaItemNotFound);
            var added = await AddUploadsAsync(meeting!, actorId, stored, now, agendaItemId, token);
            await meetings.SaveChangesAsync(token);
            return new DocumentUploadOutcome(Documents: added);
        }, outcome => outcome.Failure == DocumentUploadFailure.None, new(DocumentUploadFailure.StoreFailed), ct);

    public Task<DocumentUploadOutcome> DetachAsync(Guid meetingId, Guid agendaItemId, Guid meetingDocumentId,
        Guid actorId, CancellationToken ct = default)
        => InMeetingLockAsync(meetingId, actorId, "unlink a document in", async (meeting, _, token) =>
        {
            if (!await db.MeetingDocuments.AnyAsync(d => d.Id == meetingDocumentId && d.MeetingId == meeting.Id
                    && d.RemovedAt == null, token))
                return DocumentUploadFailure.NotInMeeting;
            if (!await ItemExistsAsync(meeting.Id, agendaItemId, token)) return DocumentUploadFailure.AgendaItemNotFound;
            var link = await db.MeetingDocumentAgendaItems.SingleOrDefaultAsync(l => l.MeetingDocumentId == meetingDocumentId
                && l.AgendaItemId == agendaItemId, token);
            if (link is null) return DocumentUploadFailure.NotLinked;
            // Only this link: with none left the document shows as a general one (BR-280).
            db.MeetingDocumentAgendaItems.Remove(link);
            return DocumentUploadFailure.None;
        }, ct);

    /// <summary>
    /// Locks the meeting and checks who may change its documents now: its current Chair or Secretary until it
    /// ends (BR-184, BR-185, BR-195, BR-279).
    /// </summary>
    private async Task<(Meeting? Meeting, DocumentUploadFailure Access)> LockAsync(Guid meetingId, Guid actorId,
        CancellationToken ct)
    {
        var meeting = await meetings.FindWorkflowAsync(meetingId, true, ct);
        return (meeting, DocumentFileRules.UploadAccess(meeting, actorId, await meetings.IsUserActiveAsync(actorId, ct)));
    }

    private Task<bool> ItemExistsAsync(Guid meetingId, Guid agendaItemId, CancellationToken ct)
        => db.AgendaItems.AnyAsync(i => i.Id == agendaItemId && i.MeetingId == meetingId, ct);

    /// <summary>
    /// Runs a change of the meeting's documents in one transaction under the meeting lock, after the access
    /// check, and saves it only when <paramref name="change"/> succeeds. The outcome lists the meeting
    /// documents the change added.
    /// </summary>
    private async Task<DocumentUploadOutcome> InMeetingLockAsync(Guid meetingId, Guid actorId, string what,
        Func<Meeting, DateTimeOffset, CancellationToken, Task<DocumentUploadFailure>> change, CancellationToken ct)
    {
        try
        {
            return await meetings.InTransactionAsync(async token =>
            {
                var (meeting, access) = await LockAsync(meetingId, actorId, token);
                if (access != DocumentUploadFailure.None) return new DocumentUploadOutcome(access);
                var failure = await change(meeting!, clock.GetUtcNow(), token);
                if (failure != DocumentUploadFailure.None) return new DocumentUploadOutcome(failure);
                var added = db.ChangeTracker.Entries<MeetingDocument>().Where(e => e.State == EntityState.Added)
                    .Select(e => e.Entity).ToList();
                await meetings.SaveChangesAsync(token);
                return new DocumentUploadOutcome(Documents: added.Select(d => new UploadedMeetingDocument(d.Id,
                    d.FileName, d.StoredFile.ContentType, d.StoredFile.ByteSize, d.StoredFile.PageCount)).ToList());
            }, ct);
        }
        catch (Exception ex) when (PersistenceError(ex))
        {
            logger.LogError(ex, "Failed to {What} meeting {MeetingId}", what, meetingId);
            return new(DocumentUploadFailure.StoreFailed);
        }
    }

    /// <summary>
    /// UC-33a and UC-33b AF-2: the actor's own library documents, none of them already in the meeting (BR-193,
    /// BR-209), become general meeting documents under their current name (BR-276) — linked to
    /// <paramref name="agendaItemId"/> when given. No byte is copied (ADR-0018).
    /// </summary>
    private async Task<DocumentUploadFailure> AddLibraryDocumentsAsync(Meeting meeting, Guid actorId,
        IReadOnlyList<Guid> documentIds, DateTimeOffset now, Guid? agendaItemId, CancellationToken ct)
    {
        var ids = documentIds.Distinct().ToArray();
        var library = await db.Documents.Include(d => d.StoredFile)
            .Where(d => ids.Contains(d.Id) && d.OwnerId == actorId).ToListAsync(ct);
        if (ids.Length == 0 || library.Count != ids.Length) return DocumentUploadFailure.LibraryUnavailable;
        var files = library.Select(d => d.StoredFileId).ToArray();
        if (await db.MeetingDocuments.AnyAsync(d => d.MeetingId == meeting.Id && d.RemovedAt == null
                && files.Contains(d.StoredFileId), ct))
            return DocumentUploadFailure.AlreadyInMeeting;

        foreach (var document in library)
        {
            // A new row even for a file removed earlier: its old comments do not come back (BR-212, BR-284).
            var meetingDocument = new MeetingDocument { MeetingId = meeting.Id, StoredFileId = document.StoredFileId,
                StoredFile = document.StoredFile, DocumentId = document.Id, FileName = document.FileName,
                AddedById = actorId, AddedAt = now, CreatedAt = now, UpdatedAt = now };
            if (agendaItemId is { } item) meetingDocument.AgendaItemLinks.Add(new() { AgendaItemId = item });
            db.MeetingDocuments.Add(meetingDocument);
        }
        return DocumentUploadFailure.None;
    }

    /// <summary>
    /// UC-33a AF-1 and UC-33b AF-1: a library document in the meeting's upload folder and a meeting document per
    /// stored file, both pointing at it (BR-210) — linked to <paramref name="agendaItemId"/> when given.
    /// </summary>
    private async Task<List<UploadedMeetingDocument>> AddUploadsAsync(Meeting meeting, Guid actorId,
        StoredFile[] stored, DateTimeOffset now, Guid? agendaItemId, CancellationToken ct)
    {
        // The upload folder must not be deleted or moved away while it is being filled.
        // ponytail: not counted against the 1 GB library quota (deviation 3, awaiting the PO).
        await PersonalLibraryService.LockLibraryAsync(db, actorId, ct);
        var folder = await documents.FindUploadFolderAsync(actorId, meeting.Id, ct);
        if (folder is null)
        {
            // BR-226 first upload, BR-228 recreated at the root once the old one is deleted.
            folder = new Folder { OwnerId = actorId, MeetingId = meeting.Id,
                Name = DocumentFileRules.UploadFolderName(meeting), CreatedAt = now, UpdatedAt = now };
            documents.AddFolder(folder);
        }

        var added = new List<UploadedMeetingDocument>();
        foreach (var file in stored)
        {
            // Two uploads of the same name or bytes are still two documents (BR-229).
            var document = new Document { OwnerId = actorId, FolderId = folder.Id, StoredFileId = file.Id,
                FileName = file.OriginalFileName, CreatedAt = now, UpdatedAt = now };
            var meetingDocument = new MeetingDocument { MeetingId = meeting.Id, StoredFileId = file.Id,
                DocumentId = document.Id, FileName = file.OriginalFileName, AddedById = actorId, AddedAt = now,
                CreatedAt = now, UpdatedAt = now };
            if (agendaItemId is { } item) meetingDocument.AgendaItemLinks.Add(new() { AgendaItemId = item });
            documents.AddUpload(document, meetingDocument);
            added.Add(new(meetingDocument.Id, file.OriginalFileName, file.ContentType, file.ByteSize, file.PageCount));
        }
        return added;
    }
}
