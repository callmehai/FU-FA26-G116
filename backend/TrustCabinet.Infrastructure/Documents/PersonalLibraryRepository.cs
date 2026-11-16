using Microsoft.EntityFrameworkCore;
using TrustCabinet.Application.Documents;
using TrustCabinet.Domain.Entities;
using TrustCabinet.Domain.Enums;
using TrustCabinet.Domain.Meetings;
using TrustCabinet.Domain.Participants;

namespace TrustCabinet.Infrastructure.Documents;

public sealed class PersonalLibraryRepository(TrustCabinetDbContext db) : IPersonalLibraryRepository
{
    public Task LockLibraryAsync(Guid ownerId, CancellationToken ct)
        => db.Database.ExecuteSqlInterpolatedAsync(
            $"SELECT pg_advisory_xact_lock(hashtextextended({"library:" + ownerId}, 0))", ct);

    public Task<bool> IsDeactivatedAsync(Guid ownerId, CancellationToken ct)
        => db.Users.AnyAsync(u => u.Id == ownerId && !u.IsActive, ct);

    public Task<bool> FolderExistsAsync(Guid ownerId, Guid folderId, CancellationToken ct)
        => db.Folders.AnyAsync(f => f.Id == folderId && f.OwnerId == ownerId, ct);

    public Task<bool> IsMeetingManagerAsync(Guid meetingId, Guid readerId, CancellationToken ct)
        => db.MeetingParticipants.Where(ParticipantRules.ActiveExpression)
            .AnyAsync(p => p.MeetingId == meetingId && p.UserId == readerId
                && (p.Role == MeetingRole.Chair || p.Role == MeetingRole.Secretary), ct);

    public Task<Dictionary<Guid, Guid?>> ListFolderParentsAsync(Guid ownerId, CancellationToken ct)
        => db.Folders.Where(f => f.OwnerId == ownerId).ToDictionaryAsync(f => f.Id, f => f.ParentId, ct);

    public Task<Folder> FindFolderAsync(Guid folderId, CancellationToken ct)
        => db.Folders.SingleAsync(f => f.Id == folderId, ct);

    public void AddFolder(Folder folder) => db.Folders.Add(folder);

    public Task<int> DeleteFolderAsync(Guid folderId, CancellationToken ct)
        => db.Folders.Where(f => f.Id == folderId).ExecuteDeleteAsync(ct);

    public Task<bool> HasDocumentsAsync(Guid folderId, CancellationToken ct)
        => db.Documents.AnyAsync(d => d.FolderId == folderId, ct);

    public Task<int> DeleteDocumentAsync(Guid ownerId, Guid documentId, CancellationToken ct)
        => db.Documents.Where(d => d.Id == documentId && d.OwnerId == ownerId).ExecuteDeleteAsync(ct);

    public Task<Document?> FindDocumentAsync(Guid ownerId, Guid documentId, CancellationToken ct)
        => db.Documents.Include(d => d.StoredFile)
            .SingleOrDefaultAsync(d => d.Id == documentId && d.OwnerId == ownerId, ct);

    public void AddDocuments(IEnumerable<Document> documents) => db.Documents.AddRange(documents);

    public async Task<IReadOnlyList<LibraryFolderFlag>> ListFolderFlagsAsync(Guid ownerId, CancellationToken ct)
    {
        var restricted = db.Meetings.Where(m => m.Sensitivity == Sensitivity.Secret).Select(m => m.Id);
        return await db.Folders.Where(f => f.OwnerId == ownerId).Select(f => new LibraryFolderFlag(
            f.Id, f.ParentId, f.MeetingId != null && restricted.Contains(f.MeetingId.Value))).ToListAsync(ct);
    }

    public async Task<IReadOnlyList<Folder>> ListFoldersAsync(Guid ownerId, CancellationToken ct)
        => await db.Folders.AsNoTracking().Where(f => f.OwnerId == ownerId)
            .OrderBy(f => f.Name).ThenBy(f => f.Id).ToListAsync(ct);

    public async Task<IReadOnlyList<LibraryDocument>> ListDocumentsAsync(Guid readerId, Guid ownerId,
        Guid? folderId, string? search, string? contentType, Guid? meetingId, CancellationToken ct)
    {
        var query = db.Documents.AsNoTracking().Where(d => d.OwnerId == ownerId && d.FolderId == folderId);
        if (readerId != ownerId)
        {
            var restricted = db.Meetings.Where(m => m.Sensitivity == Sensitivity.Secret).Select(m => m.Id);
            var hiddenFiles = db.MeetingDocuments.Where(d => restricted.Contains(d.MeetingId))
                .Select(d => d.StoredFileId);
            query = query.Where(d => !hiddenFiles.Contains(d.StoredFileId));
        }
        // AF-1: a name search and a file-type filter within the location.
        if (!string.IsNullOrWhiteSpace(search))
            query = query.Where(d => EF.Functions.ILike(d.FileName, "%" + EscapeLike(search.Trim()) + "%", "\\"));
        if (contentType is not null) query = query.Where(d => d.StoredFile.ContentType == contentType);
        var readable = db.Meetings.Where(MeetingReadPolicy.ForUser(readerId));
        return await query.OrderBy(d => d.FileName).ThenBy(d => d.Id)
            .Select(d => new LibraryDocument(d.Id, d.FileName, d.StoredFile.ContentType, d.StoredFile.ByteSize,
                d.StoredFile.PageCount, d.FolderId, d.CreatedAt,
                // "Used in n meetings", counting only meetings the reader may view (BR-251).
                readable.Count(m => m.Documents.Any(md => md.DocumentId == d.Id && md.RemovedAt == null)),
                // The same file test that answers MSG165 (BR-193), and only for a meeting the reader may view, so the
                // flag says nothing about a meeting they cannot see.
                meetingId != null && readable.Any(m => m.Id == meetingId
                    && m.Documents.Any(md => md.StoredFileId == d.StoredFileId && md.RemovedAt == null))))
            .ToListAsync(ct);
    }

    public Task<AttachedDocumentFile?> FindLibraryFileAsync(Guid ownerId, Guid documentId,
        HashSet<Guid>? hiddenFolderIds, CancellationToken ct)
    {
        var query = db.Documents.AsNoTracking().Where(d => d.Id == documentId && d.OwnerId == ownerId);
        if (hiddenFolderIds is not null)
        {
            var restricted = db.Meetings.Where(m => m.Sensitivity == Sensitivity.Secret).Select(m => m.Id);
            var files = db.MeetingDocuments.Where(d => restricted.Contains(d.MeetingId)).Select(d => d.StoredFileId);
            query = query.Where(d => !files.Contains(d.StoredFileId)
                && (d.FolderId == null || !hiddenFolderIds.Contains(d.FolderId.Value)));
        }
        return query.Select(d => new AttachedDocumentFile(d.FileName, d.StoredFile.StorageKey, d.StoredFile.ContentType,
                d.StoredFile.ByteSize)).SingleOrDefaultAsync(ct);
    }

    public Task<LibraryDocument> GetDocumentRowAsync(Guid documentId, Guid readerId, CancellationToken ct)
    {
        var readable = db.Meetings.Where(MeetingReadPolicy.ForUser(readerId));
        return db.Documents.Where(d => d.Id == documentId)
            .Select(d => new LibraryDocument(d.Id, d.FileName, d.StoredFile.ContentType, d.StoredFile.ByteSize,
                d.StoredFile.PageCount, d.FolderId, d.CreatedAt,
                readable.Count(m => m.Documents.Any(md => md.DocumentId == d.Id && md.RemovedAt == null)), false))
            .SingleAsync(ct);
    }

    private static string EscapeLike(string value)
        => value.Replace("\\", "\\\\").Replace("%", "\\%").Replace("_", "\\_");
}
