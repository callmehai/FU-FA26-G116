using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Npgsql;
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
            .OrderBy(i => i.Position).Select(i => new { i.Id, i.Position, i.Title, i.Status }).ToListAsync(ct);
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
    IFileStorage storage, IAuditService audit, TimeProvider clock, ILogger<MeetingDocumentService> logger)
    : IMeetingDocumentService
{
    private static bool PersistenceError(Exception ex) => ex is DbUpdateException or NpgsqlException or TimeoutException;

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
            if (access == DocumentUploadFailure.None && !storage.IsConfigured)
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
    {
        var now = clock.GetUtcNow();
        var stored = files.Select(file =>
        {
            var id = Guid.CreateVersion7();
            return new StoredFile
            {
                Id = id, StorageKey = DocumentFileRules.StorageKey(id), OriginalFileName = file.FileName,
                ContentType = file.ContentType, ByteSize = file.Length, ContentSha256 = file.ContentSha256,
                PageCount = file.PageCount, UploadedById = actorId, UploadedAt = now, CreatedAt = now, UpdatedAt = now,
            };
        }).ToArray();

        // The StoredFile rows go in first, on their own, so that an object never exists without a
        // row: if the process dies mid-upload (a deploy restarts the container), the rows have no
        // Document and the orphan purge deletes the objects once the grace period is over.
        // Nothing points at these rows until the transaction below commits (ADR-0024 §4).
        try
        {
            documents.AddStoredFiles(stored);
            await meetings.SaveChangesAsync(ct);
        }
        catch (Exception ex) when (PersistenceError(ex))
        {
            logger.LogError(ex, "Failed to record uploaded documents for meeting {MeetingId}", meetingId);
            return new(DocumentUploadFailure.StoreFailed);
        }

        var written = new List<string>();
        try
        {
            for (var i = 0; i < files.Count; i++)
            {
                await using var content = files[i].OpenRead();
                await storage.WriteAsync(stored[i].StorageKey, content, files[i].Length, files[i].ContentType, ct);
                written.Add(stored[i].StorageKey);
            }
        }
        catch (Exception ex)
        {
            await DeleteAsync(written);
            if (ex is OperationCanceledException && ct.IsCancellationRequested)
            {
                logger.LogInformation("Document upload for meeting {MeetingId} was abandoned by the client", meetingId);
                throw;
            }
            logger.LogError(ex, "Failed to store uploaded documents for meeting {MeetingId}", meetingId);
            return new(DocumentUploadFailure.StoreFailed);
        }

        DocumentUploadOutcome outcome;
        try
        {
            // Not cancellable: the objects are already up, and an abandoned request must still end
            // in either committed rows or deleted objects, never in between.
            outcome = await meetings.InTransactionAsync(async token =>
            {
                // Re-checked under the meeting lock: the meeting may have ended, or the actor lost
                // the role, while the files were uploading (EX-1, EX-6).
                var meeting = await meetings.FindWorkflowAsync(meetingId, true, token);
                var access = DocumentFileRules.UploadAccess(meeting, actorId, await meetings.IsUserActiveAsync(actorId, token));
                if (access != DocumentUploadFailure.None) return new DocumentUploadOutcome(access);

                var folder = await documents.FindUploadFolderAsync(actorId, meetingId, token);
                if (folder is null)
                {
                    // BR-226 first upload, BR-228 recreated at the root once the old one is deleted.
                    folder = new Folder { OwnerId = actorId, MeetingId = meetingId,
                        Name = DocumentFileRules.UploadFolderName(meeting!), CreatedAt = now, UpdatedAt = now };
                    documents.AddFolder(folder);
                }

                var added = new List<UploadedMeetingDocument>();
                foreach (var file in stored)
                {
                    // Two uploads of the same name or bytes are still two documents (BR-229).
                    var document = new Document { OwnerId = actorId, FolderId = folder.Id, StoredFileId = file.Id,
                        FileName = file.OriginalFileName, CreatedAt = now, UpdatedAt = now };
                    var meetingDocument = new MeetingDocument { MeetingId = meetingId, StoredFileId = file.Id,
                        DocumentId = document.Id, FileName = file.OriginalFileName, AddedById = actorId, AddedAt = now,
                        CreatedAt = now, UpdatedAt = now };
                    documents.AddUpload(document, meetingDocument);
                    added.Add(new(meetingDocument.Id, file.OriginalFileName, file.ContentType, file.ByteSize, file.PageCount));
                }
                await meetings.SaveChangesAsync(token);
                return new DocumentUploadOutcome(Documents: added);
            }, CancellationToken.None);
        }
        catch (Exception ex)
        {
            // Any failure here, not only a database one: the purge would get them, but not for a day.
            await DeleteAsync(written);
            if (!PersistenceError(ex)) throw;
            logger.LogError(ex, "Failed to save uploaded documents for meeting {MeetingId}", meetingId);
            return new(DocumentUploadFailure.StoreFailed);
        }

        if (outcome.Failure != DocumentUploadFailure.None) await DeleteAsync(written);
        return outcome;
    }

    /// <summary>
    /// Best effort and early: the rows stay without a Document, so the orphan purge deletes
    /// whatever is left after the grace period anyway, and stamps them.
    /// </summary>
    private async Task DeleteAsync(IEnumerable<string> keys)
    {
        foreach (var key in keys)
        {
            try { await storage.DeleteAsync(key, CancellationToken.None); }
            catch (Exception ex) { logger.LogWarning(ex, "Could not delete stored object {StorageKey} after a failed upload", key); }
        }
    }
}
