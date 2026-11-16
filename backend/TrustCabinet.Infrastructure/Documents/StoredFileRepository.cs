using Microsoft.EntityFrameworkCore;
using TrustCabinet.Application.Documents;
using TrustCabinet.Domain.Entities;

namespace TrustCabinet.Infrastructure.Documents;

public sealed class StoredFileRepository(TrustCabinetDbContext db) : IStoredFileRepository
{
    public Task<long> UsedBytesAsync(CancellationToken ct)
        => db.StoredFiles.Where(f => f.PurgedAt == null).SumAsync(f => f.ByteSize, ct);

    public void AddStoredFiles(IEnumerable<StoredFile> files) => db.StoredFiles.AddRange(files);

    public Task LockStorageAsync(CancellationToken ct)
        => db.Database.ExecuteSqlInterpolatedAsync(
            $"SELECT pg_advisory_xact_lock(hashtextextended({"stored-files"}, 0))", ct);

    public Task<int> StampPurgedAsync(Guid[] ids, DateTimeOffset now, CancellationToken ct)
        => db.StoredFiles.Where(f => ids.Contains(f.Id))
            .ExecuteUpdateAsync(u => u.SetProperty(f => f.PurgedAt, now), ct);

    public Task<int> TryStampPurgedAsync(Guid id, DateTimeOffset now, CancellationToken ct)
        => db.StoredFiles.Where(f => f.Id == id && f.PurgedAt == null)
            .ExecuteUpdateAsync(s => s.SetProperty(f => f.PurgedAt, now).SetProperty(f => f.UpdatedAt, now), ct);

    public async Task<IReadOnlyList<StoredFileRef>> ListOrphansAsync(DateTimeOffset cutoff,
        IReadOnlyList<Guid> exclude, int take, CancellationToken ct)
        => await db.StoredFiles.AsNoTracking()
            .Where(f => f.PurgedAt == null && f.UploadedAt < cutoff && !exclude.Contains(f.Id)
                && !db.Documents.Any(d => d.StoredFileId == f.Id)
                && !db.MeetingDocuments.Any(d => d.StoredFileId == f.Id && d.RemovedAt == null))
            .OrderBy(f => f.UploadedAt).Take(take)
            .Select(f => new StoredFileRef(f.Id, f.StorageKey))
            .ToListAsync(ct);
}
