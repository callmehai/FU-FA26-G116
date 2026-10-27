using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using TrustCabinet.Domain.Audit;
using TrustCabinet.Domain.Documents;
using TrustCabinet.Domain.Entities;
using TrustCabinet.Domain.Meetings;

namespace TrustCabinet.Infrastructure.Documents;

/// <summary>
/// The personal library (ADR-0006, ADR-0018): folders (UC-72a–d, SCRUM-110) and documents (UC-34, UC-36,
/// UC-71a–c, SCRUM-111). Only the owner sees it; another person's folder or document is answered as missing
/// (BR-242). Every change runs under a per-owner lock, so two moves at once cannot build a cycle or a sixth
/// level, and two uploads at once cannot both slip under the quota.
/// </summary>
public sealed class PersonalLibraryService(TrustCabinetDbContext db, StoredFileWriter writer, IFileStorage storage,
    IAuditService audit, TimeProvider clock, ILogger<PersonalLibraryService> logger) : IPersonalLibraryService
{
    private static bool PersistenceError(Exception ex) => StoredFileWriter.PersistenceError(ex);

    public async Task<LibraryOutcome<LibraryView>> ViewAsync(Guid readerId, Guid ownerId, Guid? folderId,
        string? search, string? fileType, CancellationToken ct = default)
    {
        try
        {
            if (!await MayReadAsync(readerId, ownerId, ct)) return new(LibraryFailure.NotFound);
            if (folderId is { } id && !await db.Folders.AnyAsync(f => f.Id == id && f.OwnerId == ownerId, ct))
                return new(LibraryFailure.NotFound);
            if (readerId != ownerId)
                await audit.RecordAsync(AuditActions.PersonalLibraryRead, AuditActions.UserObjectType, ownerId, readerId,
                    new { folderId }, ct);

            var folders = await db.Folders.AsNoTracking().Where(f => f.OwnerId == ownerId)
                .OrderBy(f => f.Name).ThenBy(f => f.Id)
                .Select(f => new LibraryFolder(f.Id, f.Name, f.ParentId, f.MeetingId, f.CreatedAt)).ToListAsync(ct);
            var query = db.Documents.AsNoTracking().Where(d => d.OwnerId == ownerId && d.FolderId == folderId);
            // AF-1: a name search and a file-type filter within the location.
            if (!string.IsNullOrWhiteSpace(search))
                query = query.Where(d => EF.Functions.ILike(d.FileName, "%" + EscapeLike(search.Trim()) + "%", "\\"));
            if (ContentTypeOf(fileType) is { } contentType) query = query.Where(d => d.StoredFile.ContentType == contentType);
            var documents = await Project(query.OrderBy(d => d.FileName).ThenBy(d => d.Id), readerId).ToListAsync(ct);
            return new(Value: new(folderId, folders, documents, await UsedBytesAsync(ownerId, ct), DocumentFileRules.MaxLibraryBytes));
        }
        catch (Exception ex) when (PersistenceError(ex))
        {
            logger.LogError(ex, "Failed to load the personal library of {OwnerId}", ownerId);
            return new(LibraryFailure.SaveFailed);
        }
    }

    public async Task<MeetingDocumentContentOutcome> OpenContentAsync(Guid readerId, Guid ownerId, Guid documentId,
        CancellationToken ct = default)
    {
        AttachedDocumentFile? file;
        try
        {
            if (!await MayReadAsync(readerId, ownerId, ct)) return new(DocumentReadFailure.NotFound);
            file = await db.Documents.AsNoTracking().Where(d => d.Id == documentId && d.OwnerId == ownerId)
                .Select(d => new AttachedDocumentFile(d.FileName, d.StoredFile.StorageKey, d.StoredFile.ContentType,
                    d.StoredFile.ByteSize)).SingleOrDefaultAsync(ct);
        }
        catch (Exception ex) when (PersistenceError(ex))
        {
            logger.LogError(ex, "Failed to look up library document {DocumentId}", documentId);
            return new(DocumentReadFailure.ReadFailed);
        }
        if (file is null) return new(DocumentReadFailure.NotFound);

        Stream content;
        try
        {
            content = await storage.OpenReadAsync(file.StorageKey, ct);
        }
        catch (Exception ex) when (ex is not OperationCanceledException || !ct.IsCancellationRequested)
        {
            logger.LogError(ex, "Failed to open stored object {StorageKey} for library document {DocumentId}",
                file.StorageKey, documentId);
            return new(DocumentReadFailure.ReadFailed);
        }

        if (readerId != ownerId)
        {
            // As for a Confidential meeting document: no audit row, no content.
            try
            {
                await audit.RecordAsync(AuditActions.PersonalLibraryRead, AuditActions.UserObjectType, ownerId, readerId,
                    new { documentId, fileName = file.FileName }, ct);
            }
            catch (Exception ex)
            {
                await content.DisposeAsync();
                if (!PersistenceError(ex)) throw;
                logger.LogError(ex, "Failed to audit the read of library document {DocumentId}", documentId);
                return new(DocumentReadFailure.ReadFailed);
            }
        }
        return new(Content: new(content, file.FileName, file.ContentType, file.ByteSize));
    }

    public Task<LibraryOutcome<LibraryFolder>> CreateFolderAsync(Guid ownerId, Guid? parentId, string? name,
        CancellationToken ct = default)
        => InLibraryLockAsync<LibraryFolder>(ownerId, "create a folder", async (folders, token) =>
        {
            if (parentId is { } parent && !folders.ContainsKey(parent)) return new(LibraryFailure.DestinationNotFound);
            var (failure, value) = FolderName(name);
            if (failure != LibraryFailure.None) return new(failure);
            if (Depth(folders, parentId) + 1 > DocumentFileRules.MaxFolderDepth) return new(LibraryFailure.TooDeep);
            // Duplicate names are allowed (BR-221).
            var now = clock.GetUtcNow();
            var folder = new Folder { OwnerId = ownerId, ParentId = parentId, Name = value!, CreatedAt = now, UpdatedAt = now };
            db.Folders.Add(folder);
            await db.SaveChangesAsync(token);
            return new(Value: ToRow(folder));
        }, ct);

    public Task<LibraryOutcome<LibraryFolder>> RenameFolderAsync(Guid ownerId, Guid folderId, string? name,
        CancellationToken ct = default)
        => InLibraryLockAsync<LibraryFolder>(ownerId, "rename a folder", async (folders, token) =>
        {
            if (!folders.ContainsKey(folderId)) return new(LibraryFailure.NotFound);
            var (failure, value) = FolderName(name);
            if (failure != LibraryFailure.None) return new(failure);
            var folder = await db.Folders.SingleAsync(f => f.Id == folderId, token);
            folder.Name = value!;
            folder.UpdatedAt = clock.GetUtcNow();
            await db.SaveChangesAsync(token);
            return new(Value: ToRow(folder));
        }, ct);

    public Task<LibraryOutcome<LibraryFolder>> MoveFolderAsync(Guid ownerId, Guid folderId, Guid? parentId,
        CancellationToken ct = default)
        => InLibraryLockAsync<LibraryFolder>(ownerId, "move a folder", async (folders, token) =>
        {
            if (!folders.ContainsKey(folderId)) return new(LibraryFailure.NotFound);
            if (parentId is { } parent && !folders.ContainsKey(parent)) return new(LibraryFailure.DestinationNotFound);
            // The destination's own chain reaching the folder means it is the folder or one of its subfolders (BR-222).
            for (var at = parentId; at is { } id; at = folders[id])
                if (id == folderId) return new(LibraryFailure.IntoItself);
            // The whole subtree moves along, so its deepest folder must stay within level five (BR-191).
            if (Depth(folders, parentId) + Height(folders, folderId) > DocumentFileRules.MaxFolderDepth)
                return new(LibraryFailure.TooDeep);
            var folder = await db.Folders.SingleAsync(f => f.Id == folderId, token);
            folder.ParentId = parentId;
            folder.UpdatedAt = clock.GetUtcNow();
            await db.SaveChangesAsync(token);
            return new(Value: ToRow(folder));
        }, ct);

    public async Task<LibraryFailure> DeleteFolderAsync(Guid ownerId, Guid folderId, CancellationToken ct = default)
        => (await InLibraryLockAsync<bool>(ownerId, "delete a folder", async (folders, token) =>
        {
            if (!folders.ContainsKey(folderId)) return new(LibraryFailure.NotFound);
            // Only an empty folder (BR-223); the Restrict foreign keys are the backstop.
            if (folders.ContainsValue(folderId) || await db.Documents.AnyAsync(d => d.FolderId == folderId, token))
                return new(LibraryFailure.NotEmpty);
            // A meeting's upload folder too: the next upload to that meeting makes a new one (BR-228).
            await db.Folders.Where(f => f.Id == folderId).ExecuteDeleteAsync(token);
            return new(Value: true);
        }, ct)).Failure;

    public async Task<LibraryFailure> CheckUploadAsync(Guid ownerId, Guid? folderId, CancellationToken ct = default)
    {
        try
        {
            if (folderId is { } id && !await db.Folders.AnyAsync(f => f.Id == id && f.OwnerId == ownerId, ct))
                return LibraryFailure.DestinationNotFound;
        }
        catch (Exception ex) when (PersistenceError(ex))
        {
            logger.LogError(ex, "Failed to check the upload destination of {OwnerId}", ownerId);
            return LibraryFailure.SaveFailed;
        }
        if (writer.IsConfigured) return LibraryFailure.None;
        logger.LogError("Library upload refused: FileStorage is not configured.");
        return LibraryFailure.SaveFailed;
    }

    public async Task<LibraryOutcome<IReadOnlyList<LibraryDocument>>> UploadAsync(Guid ownerId, Guid? folderId,
        IReadOnlyList<DocumentUploadFile> files, CancellationToken ct = default)
    {
        try
        {
            // Checked before any byte goes up, and again under the lock below.
            if (await UsedBytesAsync(ownerId, ct) + files.Sum(f => f.Length) > DocumentFileRules.MaxLibraryBytes)
                return new(LibraryFailure.QuotaExceeded);
        }
        catch (Exception ex) when (PersistenceError(ex))
        {
            logger.LogError(ex, "Failed to read the library usage of {OwnerId}", ownerId);
            return new(LibraryFailure.SaveFailed);
        }

        return await writer.WriteAsync<LibraryOutcome<IReadOnlyList<LibraryDocument>>>(ownerId, files,
            async (stored, now, token) =>
            {
                await LockLibraryAsync(ownerId, token);
                if (folderId is { } id && !await db.Folders.AnyAsync(f => f.Id == id && f.OwnerId == ownerId, token))
                    return new(LibraryFailure.DestinationNotFound);
                if (await UsedBytesAsync(ownerId, token) + stored.Sum(f => f.ByteSize) > DocumentFileRules.MaxLibraryBytes)
                    return new(LibraryFailure.QuotaExceeded);
                // Original names; the same name or bytes twice is still two documents (BR-229).
                var added = stored.Select(file => new Document { OwnerId = ownerId, FolderId = folderId,
                    StoredFileId = file.Id, FileName = file.OriginalFileName, CreatedAt = now, UpdatedAt = now }).ToList();
                db.Documents.AddRange(added);
                await db.SaveChangesAsync(token);
                return new(Value: added.Zip(stored, (d, f) => new LibraryDocument(d.Id, d.FileName, f.ContentType,
                    f.ByteSize, f.PageCount, d.FolderId, d.CreatedAt, 0)).ToList());
            }, outcome => outcome.Failure == LibraryFailure.None, new(LibraryFailure.SaveFailed), ct);
    }

    public Task<LibraryOutcome<LibraryDocument>> RenameDocumentAsync(Guid ownerId, Guid documentId, string? fileName,
        CancellationToken ct = default)
        => ChangeDocumentAsync(ownerId, documentId, "rename", (document, _) =>
        {
            // The name keeps the file's own extension: the file is downloaded under it.
            var name = DocumentFileRules.NormalizeFileName(fileName);
            if (name is null || !DocumentFileRules.MatchesContentType(name, document.StoredFile.ContentType))
                return Task.FromResult(LibraryFailure.NameInvalid);
            // The meeting copies keep their own name (BR-276).
            document.FileName = name;
            return Task.FromResult(LibraryFailure.None);
        }, ct);

    public Task<LibraryOutcome<LibraryDocument>> MoveDocumentAsync(Guid ownerId, Guid documentId, Guid? folderId,
        CancellationToken ct = default)
        => ChangeDocumentAsync(ownerId, documentId, "move", async (document, token) =>
        {
            if (folderId is { } id && !await db.Folders.AnyAsync(f => f.Id == id && f.OwnerId == ownerId, token))
                return LibraryFailure.DestinationNotFound;
            // Only the library row moves; meetings using the file are untouched (BR-245).
            document.FolderId = folderId;
            return LibraryFailure.None;
        }, ct);

    public async Task<LibraryFailure> DeleteDocumentAsync(Guid ownerId, Guid documentId, CancellationToken ct = default)
    {
        try
        {
            // Only the library row: the meeting copies keep pointing at the stored file (their DocumentId goes
            // null), and the purge deletes the bytes once nothing uses them (BR-186, BR-213, BR-232).
            var deleted = await db.Documents.Where(d => d.Id == documentId && d.OwnerId == ownerId).ExecuteDeleteAsync(ct);
            return deleted == 0 ? LibraryFailure.NotFound : LibraryFailure.None;
        }
        catch (Exception ex) when (PersistenceError(ex))
        {
            logger.LogError(ex, "Failed to delete library document {DocumentId}", documentId);
            return LibraryFailure.SaveFailed;
        }
    }

    /// <summary>The owner, or an administrator once the owner's account is deactivated (PO 27/10/2026).</summary>
    private async Task<bool> MayReadAsync(Guid readerId, Guid ownerId, CancellationToken ct)
        => readerId == ownerId || await db.Users.AnyAsync(u => u.Id == ownerId && !u.IsActive, ct);

    private Task<long> UsedBytesAsync(Guid ownerId, CancellationToken ct)
        => db.Documents.Where(d => d.OwnerId == ownerId).SumAsync(d => d.StoredFile.ByteSize, ct);

    private IQueryable<LibraryDocument> Project(IQueryable<Document> query, Guid readerId)
    {
        var readable = db.Meetings.Where(MeetingReadPolicy.ForUser(readerId));
        return query.Select(d => new LibraryDocument(d.Id, d.FileName, d.StoredFile.ContentType, d.StoredFile.ByteSize,
            d.StoredFile.PageCount, d.FolderId, d.CreatedAt,
            // "Used in n meetings", counting only meetings the reader may view (BR-251).
            readable.Count(m => m.Documents.Any(md => md.DocumentId == d.Id && md.RemovedAt == null))));
    }

    private async Task<LibraryOutcome<LibraryDocument>> ChangeDocumentAsync(Guid ownerId, Guid documentId, string what,
        Func<Document, CancellationToken, Task<LibraryFailure>> change, CancellationToken ct)
        => await InLibraryLockAsync<LibraryDocument>(ownerId, $"{what} a document", async (_, token) =>
        {
            var document = await db.Documents.Include(d => d.StoredFile)
                .SingleOrDefaultAsync(d => d.Id == documentId && d.OwnerId == ownerId, token);
            if (document is null) return new(LibraryFailure.NotFound);
            var failure = await change(document, token);
            if (failure != LibraryFailure.None) return new(failure);
            document.UpdatedAt = clock.GetUtcNow();
            await db.SaveChangesAsync(token);
            return new(Value: await Project(db.Documents.Where(d => d.Id == documentId), ownerId).SingleAsync(token));
        }, ct);

    /// <summary>
    /// One library change in a transaction under the owner's lock, with every folder of the owner (id → parent)
    /// read under it. A database failure is <see cref="LibraryFailure.SaveFailed"/>.
    /// </summary>
    private async Task<LibraryOutcome<T>> InLibraryLockAsync<T>(Guid ownerId, string what,
        Func<Dictionary<Guid, Guid?>, CancellationToken, Task<LibraryOutcome<T>>> change, CancellationToken ct)
    {
        try
        {
            await using var transaction = await db.Database.BeginTransactionAsync(ct);
            await LockLibraryAsync(ownerId, ct);
            // ponytail: the whole folder list per change; a library has tens of folders, not thousands.
            var folders = await db.Folders.Where(f => f.OwnerId == ownerId)
                .ToDictionaryAsync(f => f.Id, f => f.ParentId, ct);
            var outcome = await change(folders, ct);
            if (outcome.Failure == LibraryFailure.None) await transaction.CommitAsync(ct);
            return outcome;
        }
        catch (Exception ex) when (PersistenceError(ex))
        {
            logger.LogError(ex, "Failed to {What} in the personal library of {OwnerId}", what, ownerId);
            return new(LibraryFailure.SaveFailed);
        }
    }

    /// <summary>Serialises the owner's library changes until the transaction ends.</summary>
    private Task LockLibraryAsync(Guid ownerId, CancellationToken ct)
        => db.Database.ExecuteSqlInterpolatedAsync(
            $"SELECT pg_advisory_xact_lock(hashtextextended({"library:" + ownerId}, 0))", ct);

    /// <summary>Level of a folder: 1 at the library root, 0 for the root itself.</summary>
    private static int Depth(Dictionary<Guid, Guid?> folders, Guid? folderId)
    {
        var depth = 0;
        for (var at = folderId; at is { } id; at = folders[id]) depth++;
        return depth;
    }

    /// <summary>Levels in the subtree under and including a folder: 1 for a folder with no subfolder.</summary>
    private static int Height(Dictionary<Guid, Guid?> folders, Guid folderId)
        => 1 + folders.Where(f => f.Value == folderId).Select(f => Height(folders, f.Key)).DefaultIfEmpty(0).Max();

    private static (LibraryFailure Failure, string? Value) FolderName(string? name)
    {
        var value = name?.Trim();
        // Not unique (BR-221), only not blank (BR-220).
        if (string.IsNullOrEmpty(value)) return (LibraryFailure.NameRequired, null);
        return value.Length > DocumentFileRules.MaxFolderNameLength ? (LibraryFailure.NameTooLong, null) : (LibraryFailure.None, value);
    }

    private static string? ContentTypeOf(string? fileType) => fileType?.Trim().ToLowerInvariant() switch
    {
        "pdf" => DocumentFileRules.PdfContentType,
        "docx" => DocumentFileRules.DocxContentType,
        _ => null,
    };

    private static string EscapeLike(string value)
        => value.Replace("\\", "\\\\").Replace("%", "\\%").Replace("_", "\\_");

    private static LibraryFolder ToRow(Folder f) => new(f.Id, f.Name, f.ParentId, f.MeetingId, f.CreatedAt);
}
