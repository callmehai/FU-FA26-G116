using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Npgsql;
using TrustCabinet.Domain.Documents;
using TrustCabinet.Domain.Entities;
using TrustCabinet.Domain.Meetings;

namespace TrustCabinet.Infrastructure.Documents;

public sealed class MeetingDocumentRepository(TrustCabinetDbContext db) : IMeetingDocumentRepository
{
    public Task<Folder?> FindUploadFolderAsync(Guid ownerId, Guid meetingId, CancellationToken ct)
        => db.Folders.SingleOrDefaultAsync(f => f.OwnerId == ownerId && f.MeetingId == meetingId, ct);

    public void AddFolder(Folder folder) => db.Folders.Add(folder);

    public void AddUpload(StoredFile file, Document document, MeetingDocument meetingDocument)
    {
        db.StoredFiles.Add(file);
        db.Documents.Add(document);
        db.MeetingDocuments.Add(meetingDocument);
    }
}

public sealed class MeetingDocumentService(IMeetingRepository meetings, IMeetingDocumentRepository documents,
    IFileStorage storage, TimeProvider clock, ILogger<MeetingDocumentService> logger) : IMeetingDocumentService
{
    private static bool PersistenceError(Exception ex) => ex is DbUpdateException or NpgsqlException or TimeoutException;

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

        // Objects first, rows second (ADR-0024 §4): a row never points at bytes that are not there.
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
            logger.LogError(ex, "Failed to store uploaded documents for meeting {MeetingId}", meetingId);
            await DeleteAsync(written);
            if (ex is OperationCanceledException && ct.IsCancellationRequested) throw;
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
                    documents.AddUpload(file, document, meetingDocument);
                    added.Add(new(meetingDocument.Id, file.OriginalFileName, file.ContentType, file.ByteSize, file.PageCount));
                }
                await meetings.SaveChangesAsync(token);
                return new DocumentUploadOutcome(Documents: added);
            }, CancellationToken.None);
        }
        catch (Exception ex) when (PersistenceError(ex))
        {
            logger.LogError(ex, "Failed to save uploaded documents for meeting {MeetingId}", meetingId);
            outcome = new(DocumentUploadFailure.StoreFailed);
        }

        if (outcome.Failure != DocumentUploadFailure.None) await DeleteAsync(written);
        return outcome;
    }

    /// <summary>
    /// Best effort. An object left behind has no row, so the orphan purge cannot see it; the
    /// warning carries the key so it can be removed by hand (ADR-0024).
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
