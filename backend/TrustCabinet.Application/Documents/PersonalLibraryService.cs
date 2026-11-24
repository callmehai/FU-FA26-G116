using Microsoft.Extensions.Logging;
using TrustCabinet.Application.Abstractions;
using TrustCabinet.Application.Audit;
using TrustCabinet.Domain.Audit;
using TrustCabinet.Domain.Documents;
using TrustCabinet.Domain.Entities;

namespace TrustCabinet.Application.Documents;

/// <summary>
/// The personal library (ADR-0006, ADR-0018): folders (UC-41a–d, SCRUM-110) and documents (UC-37, UC-39,
/// UC-40a–c, SCRUM-111). Only the owner sees it; another person's folder or document is answered as missing
/// (BR-083). Every change runs under a per-owner lock, so two moves at once cannot build a cycle or a sixth
/// level, and two uploads at once cannot both slip under the quota.
/// </summary>
public sealed class PersonalLibraryService(IPersonalLibraryRepository library, StoredFileWriter writer,
    IFileStorage storage, IAuditService audit, IUnitOfWork unitOfWork, TimeProvider clock,
    ILogger<PersonalLibraryService> logger) : IPersonalLibraryService
{
    public async Task<LibraryOutcome<LibraryView>> ViewAsync(Guid readerId, Guid ownerId, Guid? folderId,
        string? search, string? fileType, Guid? meetingId = null, CancellationToken ct = default)
    {
        try
        {
            return await unitOfWork.ExecuteAsync(async token =>
            {
                if (!await MayReadAsync(readerId, ownerId, token)) return new LibraryOutcome<LibraryView>(LibraryFailure.NotFound);
                if (folderId is { } id && !await library.FolderExistsAsync(ownerId, id, token))
                    return new LibraryOutcome<LibraryView>(LibraryFailure.NotFound);
                // SCRUM-124: only who may add to the meeting (its current Chair or Secretary, BR-070, BR-071) learns what is in
                // it. A Member must not, since a document on a draft vote is hidden from them (BR-086, BR-087).
                if (meetingId is { } asked && !await library.IsMeetingManagerAsync(asked, readerId, token))
                    meetingId = null;
                HashSet<Guid>? hidden = readerId == ownerId ? null : await HiddenFromAsync(ownerId, token);
                if (folderId is { } at && hidden is not null && hidden.Contains(at)) return new LibraryOutcome<LibraryView>(LibraryFailure.NotFound);
                if (readerId != ownerId)
                    await audit.RecordAsync(AuditActions.PersonalLibraryRead, AuditActions.UserObjectType, ownerId, readerId,
                        new { folderId }, token);

                var folders = (await library.ListFoldersAsync(ownerId, token))
                    .Where(f => hidden is null || !hidden.Contains(f.Id))
                    .Select(f => new LibraryFolder(f.Id, f.Name, f.ParentId, f.MeetingId, f.CreatedAt)).ToList();
                var documents = await library.ListDocumentsAsync(readerId, ownerId, folderId, search,
                    ContentTypeOf(fileType), meetingId, readerId != ownerId, token);
                return new LibraryOutcome<LibraryView>(Value: new(folderId, folders, documents,
                    await writer.UsedBytesAsync(token), writer.LimitBytes));
            }, TransactionMode.None, ct);
        }
        catch (PersistenceException ex)
        {
            logger.LogError(ex.InnerException ?? ex, "Failed to load the personal library of {OwnerId}", ownerId);
            return new(LibraryFailure.SaveFailed);
        }
    }

    public async Task<MeetingDocumentContentOutcome> OpenContentAsync(Guid readerId, Guid ownerId, Guid documentId,
        CancellationToken ct = default)
    {
        AttachedDocumentFile? file;
        try
        {
            file = await unitOfWork.ExecuteAsync(async token =>
            {
                if (!await MayReadAsync(readerId, ownerId, token)) return (AttachedDocumentFile?)null;
                HashSet<Guid>? hidden = readerId == ownerId ? null : await HiddenFromAsync(ownerId, token);
                return await library.FindLibraryFileAsync(ownerId, documentId, hidden, token);
            }, TransactionMode.None, ct);
        }
        catch (PersistenceException ex)
        {
            logger.LogError(ex.InnerException ?? ex, "Failed to look up library document {DocumentId}", documentId);
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
            return new(ex is FileNotFoundException ? DocumentReadFailure.FileMissing : DocumentReadFailure.ReadFailed);
        }

        if (readerId != ownerId)
        {
            // As for a Secret meeting document: no audit row, no content.
            try
            {
                await audit.RecordAsync(AuditActions.PersonalLibraryRead, AuditActions.UserObjectType, ownerId, readerId,
                    new { documentId, fileName = file.FileName }, ct);
            }
            catch (Exception ex)
            {
                await content.DisposeAsync();
                if (ex is not PersistenceException persistence) throw;
                logger.LogError(persistence.InnerException ?? ex, "Failed to audit the read of library document {DocumentId}", documentId);
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
            // Duplicate names are allowed (UC-41a, UC-41b).
            var now = clock.GetUtcNow();
            var folder = new Folder { OwnerId = ownerId, ParentId = parentId, Name = value!, CreatedAt = now, UpdatedAt = now };
            library.AddFolder(folder);
            await unitOfWork.SaveChangesAsync(token);
            return new(Value: ToRow(folder));
        }, ct);

    public Task<LibraryOutcome<LibraryFolder>> RenameFolderAsync(Guid ownerId, Guid folderId, string? name,
        CancellationToken ct = default)
        => InLibraryLockAsync<LibraryFolder>(ownerId, "rename a folder", async (folders, token) =>
        {
            if (!folders.ContainsKey(folderId)) return new(LibraryFailure.NotFound);
            var (failure, value) = FolderName(name);
            if (failure != LibraryFailure.None) return new(failure);
            var folder = await library.FindFolderAsync(folderId, token);
            folder.Name = value!;
            folder.UpdatedAt = clock.GetUtcNow();
            await unitOfWork.SaveChangesAsync(token);
            return new(Value: ToRow(folder));
        }, ct);

    public Task<LibraryOutcome<LibraryFolder>> MoveFolderAsync(Guid ownerId, Guid folderId, Guid? parentId,
        CancellationToken ct = default)
        => InLibraryLockAsync<LibraryFolder>(ownerId, "move a folder", async (folders, token) =>
        {
            if (!folders.ContainsKey(folderId)) return new(LibraryFailure.NotFound);
            if (parentId is { } parent && !folders.ContainsKey(parent)) return new(LibraryFailure.DestinationNotFound);
            // The destination's own chain reaching the folder means it is the folder or one of its subfolders (BR-084).
            for (var at = parentId; at is { } id; at = folders[id])
                if (id == folderId) return new(LibraryFailure.IntoItself);
            // The whole subtree moves along, so its deepest folder must stay within level five (UC-37, UC-40b, UC-40c, UC-41a, UC-41b, UC-41c, UC-41d).
            if (Depth(folders, parentId) + Height(folders, folderId) > DocumentFileRules.MaxFolderDepth)
                return new(LibraryFailure.TooDeep);
            var folder = await library.FindFolderAsync(folderId, token);
            folder.ParentId = parentId;
            folder.UpdatedAt = clock.GetUtcNow();
            await unitOfWork.SaveChangesAsync(token);
            return new(Value: ToRow(folder));
        }, ct);

    public async Task<LibraryFailure> DeleteFolderAsync(Guid ownerId, Guid folderId, CancellationToken ct = default)
        => (await InLibraryLockAsync<bool>(ownerId, "delete a folder", async (folders, token) =>
        {
            if (!folders.ContainsKey(folderId)) return new(LibraryFailure.NotFound);
            // Only an empty folder (BR-084); the Restrict foreign keys are the backstop.
            if (folders.ContainsValue(folderId) || await library.HasDocumentsAsync(folderId, token))
                return new(LibraryFailure.NotEmpty);
            // A meeting's upload folder too: the next upload to that meeting makes a new one (UC-36a).
            await library.DeleteFolderAsync(folderId, token);
            return new(Value: true);
        }, ct)).Failure;

    public async Task<LibraryFailure> CheckUploadAsync(Guid ownerId, Guid? folderId, CancellationToken ct = default)
    {
        try
        {
            if (folderId is { } id && !await unitOfWork.ExecuteAsync(token => library.FolderExistsAsync(ownerId, id, token),
                TransactionMode.None, ct))
                return LibraryFailure.DestinationNotFound;
        }
        catch (PersistenceException ex)
        {
            logger.LogError(ex.InnerException ?? ex, "Failed to check the upload destination of {OwnerId}", ownerId);
            return LibraryFailure.SaveFailed;
        }
        if (writer.IsConfigured) return LibraryFailure.None;
        logger.LogError("Library upload refused: FileStorage is not configured.");
        return LibraryFailure.SaveFailed;
    }

    /// <summary>UC-39. The system's storage limit is the writer's to check (ADR-0034).</summary>
    public async Task<LibraryOutcome<IReadOnlyList<LibraryDocument>>> UploadAsync(Guid ownerId, Guid? folderId,
        IReadOnlyList<DocumentUploadFile> files, CancellationToken ct = default)
        => await writer.WriteAsync<LibraryOutcome<IReadOnlyList<LibraryDocument>>>(ownerId, files,
            async (stored, now, token) =>
            {
                await library.LockLibraryAsync(ownerId, token);
                if (folderId is { } id && !await library.FolderExistsAsync(ownerId, id, token))
                    return new(LibraryFailure.DestinationNotFound);
                // Original names; the same name or bytes twice is still two documents (BR-068).
                var added = stored.Select(file => new Document { OwnerId = ownerId, FolderId = folderId,
                    StoredFileId = file.Id, FileName = file.OriginalFileName, CreatedAt = now, UpdatedAt = now }).ToList();
                library.AddDocuments(added);
                await unitOfWork.SaveChangesAsync(token);
                return new(Value: added.Zip(stored, (d, f) => new LibraryDocument(d.Id, d.FileName, f.ContentType,
                    f.ByteSize, f.PageCount, d.FolderId, d.CreatedAt, 0)).ToList());
            }, outcome => outcome.Failure == LibraryFailure.None, new(LibraryFailure.SaveFailed),
            new(LibraryFailure.StorageFull), ct);

    public Task<LibraryOutcome<LibraryDocument>> RenameDocumentAsync(Guid ownerId, Guid documentId, string? fileName,
        CancellationToken ct = default)
        => ChangeDocumentAsync(ownerId, documentId, "rename", (document, _) =>
        {
            // The name keeps the file's own extension: the file is downloaded under it.
            var name = DocumentFileRules.NormalizeFileName(fileName);
            if (name is null || Path.GetFileNameWithoutExtension(name).Trim().Length == 0
                || !DocumentFileRules.MatchesContentType(name, document.StoredFile.ContentType))
                return Task.FromResult(LibraryFailure.NameInvalid);
            // The meeting copies keep their own name (UC-40b).
            document.FileName = name;
            return Task.FromResult(LibraryFailure.None);
        }, ct);

    public Task<LibraryOutcome<LibraryDocument>> MoveDocumentAsync(Guid ownerId, Guid documentId, Guid? folderId,
        CancellationToken ct = default)
        => ChangeDocumentAsync(ownerId, documentId, "move", async (document, token) =>
        {
            if (folderId is { } id && !await library.FolderExistsAsync(ownerId, id, token))
                return LibraryFailure.DestinationNotFound;
            // Only the library row moves; meetings using the file are untouched (UC-40c).
            document.FolderId = folderId;
            return LibraryFailure.None;
        }, ct);

    public async Task<LibraryFailure> DeleteDocumentAsync(Guid ownerId, Guid documentId, CancellationToken ct = default)
        => (await InLibraryLockAsync<bool>(ownerId, "delete a document", async (_, token) =>
        {
            // Only the library row: the meeting copies keep pointing at the stored file (their DocumentId goes
            // null), and the purge deletes the bytes once nothing uses them (BR-074–BR-077).
            var deleted = await library.DeleteDocumentAsync(ownerId, documentId, token);
            return deleted == 0 ? new(LibraryFailure.NotFound) : new(Value: true);
        }, ct)).Failure;

    /// <summary>The owner, or an administrator once the owner's account is deactivated (PO 27/10/2026).</summary>
    private async Task<bool> MayReadAsync(Guid readerId, Guid ownerId, CancellationToken ct)
        => readerId == ownerId || await library.IsDeactivatedAsync(ownerId, ct);

    /// <summary>
    /// What an administrator reading someone else's library must not see: the files of any Secret
    /// meeting, attached now or earlier, and that meeting's upload folder — named after it — with its subfolders.
    /// Even when the administrator may view the meeting: there it opens under the meeting's own rules (BR-081 no
    /// download, BR-074–BR-076 removed, the audit row), and the library must not become a way round them.
    /// </summary>
    private async Task<HashSet<Guid>> HiddenFromAsync(Guid ownerId, CancellationToken ct)
    {
        var folders = await library.ListFolderFlagsAsync(ownerId, ct);
        var hidden = folders.Where(f => f.Hidden).Select(f => f.Id).ToHashSet();
        // A subfolder goes with its hidden parent, however deep.
        bool grew;
        do
        {
            grew = false;
            foreach (var folder in folders)
                if (folder.ParentId is { } parent && hidden.Contains(parent)) grew |= hidden.Add(folder.Id);
        } while (grew);
        return hidden;
    }

    private async Task<LibraryOutcome<LibraryDocument>> ChangeDocumentAsync(Guid ownerId, Guid documentId, string what,
        Func<Document, CancellationToken, Task<LibraryFailure>> change, CancellationToken ct)
        => await InLibraryLockAsync<LibraryDocument>(ownerId, $"{what} a document", async (_, token) =>
        {
            var document = await library.FindDocumentAsync(ownerId, documentId, token);
            if (document is null) return new(LibraryFailure.NotFound);
            var failure = await change(document, token);
            if (failure != LibraryFailure.None) return new(failure);
            document.UpdatedAt = clock.GetUtcNow();
            await unitOfWork.SaveChangesAsync(token);
            return new(Value: await library.GetDocumentRowAsync(documentId, ownerId, token));
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
            return await unitOfWork.ExecuteAsync(async token =>
            {
                await library.LockLibraryAsync(ownerId, token);
                // ponytail: the whole folder list per change; a library has tens of folders, not thousands.
                var folders = await library.ListFolderParentsAsync(ownerId, token);
                return await change(folders, token);
            }, TransactionMode.ReadCommitted, ct);
        }
        catch (PersistenceException ex)
        {
            logger.LogError(ex.InnerException ?? ex, "Failed to {What} in the personal library of {OwnerId}", what, ownerId);
            return new(LibraryFailure.SaveFailed);
        }
    }

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
        // Not unique (UC-41a, UC-41b), only not blank (UC-41a, UC-41b).
        if (string.IsNullOrEmpty(value)) return (LibraryFailure.NameRequired, null);
        return value.Length > DocumentFileRules.MaxFolderNameLength ? (LibraryFailure.NameTooLong, null) : (LibraryFailure.None, value);
    }

    private static string? ContentTypeOf(string? fileType) => fileType?.Trim().ToLowerInvariant() switch
    {
        "pdf" => DocumentFileRules.PdfContentType,
        "docx" => DocumentFileRules.DocxContentType,
        _ => null,
    };

    private static LibraryFolder ToRow(Folder f) => new(f.Id, f.Name, f.ParentId, f.MeetingId, f.CreatedAt);
}
