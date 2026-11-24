using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using TrustCabinet.Application.Abstractions;
using TrustCabinet.Domain.Documents;
using TrustCabinet.Domain.Entities;

namespace TrustCabinet.Application.Documents;

/// <summary>
/// The write order every upload shares (ADR-0024 §4): the <see cref="StoredFile"/> rows on their own, then the
/// bytes, then the rows that point at them in one transaction. Any failure deletes the objects of this
/// submission (UC-36a, UC-36b, UC-39): UC-36a AF-1, UC-36b AF-1 and UC-39.
///
/// The rows also reserve their room in the system's storage limit (ADR-0034): written under one lock for the
/// whole system, only while the files still fit, and released again when the upload fails.
/// </summary>
public sealed class StoredFileWriter(IStoredFileRepository storedFiles, IFileStorage storage, IUnitOfWork unitOfWork,
    TimeProvider clock, IOptions<FileStorageOptions> options, ILogger<StoredFileWriter> logger)
{
    public bool IsConfigured => storage.IsConfigured;

    /// <summary>What every stored document together may take (<c>FileStorage:MaxTotalGigabytes</c>).</summary>
    public long LimitBytes => options.Value.MaxTotalBytes;

    /// <summary>
    /// What the bucket holds: every file not purged yet, whoever owns it and whether anything still points at it,
    /// since an unreferenced file takes room until the orphan purge deletes it.
    /// </summary>
    public Task<long> UsedBytesAsync(CancellationToken ct)
        => unitOfWork.ExecuteAsync(token => storedFiles.UsedBytesAsync(token), TransactionMode.None, ct);

    /// <summary>
    /// <paramref name="commit"/> runs in the transaction with the new rows and the upload time; it is committed
    /// only when <paramref name="committed"/> says so. Returns <paramref name="full"/> when the files do not fit
    /// the storage limit, and <paramref name="failed"/> when the rows, the bytes or the commit could not be stored.
    /// </summary>
    public async Task<T> WriteAsync<T>(Guid actorId, IReadOnlyList<DocumentUploadFile> files,
        Func<StoredFile[], DateTimeOffset, CancellationToken, Task<T>> commit, Func<T, bool> committed, T failed,
        T full, CancellationToken ct)
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
        // They are written under one lock for the whole system, so two uploads cannot both take the last room.
        try
        {
            var reserved = await unitOfWork.ExecuteAsync(async token =>
            {
                await storedFiles.LockStorageAsync(token);
                if (await storedFiles.UsedBytesAsync(token) + stored.Sum(f => f.ByteSize) > LimitBytes) return false;
                storedFiles.AddStoredFiles(stored);
                await unitOfWork.SaveChangesAsync(token);
                return true;
            }, TransactionMode.ReadCommitted, ct);
            if (!reserved) return full;
        }
        catch (PersistenceException ex)
        {
            logger.LogError(ex.InnerException ?? ex, "Failed to record uploaded files of {ActorId}", actorId);
            return failed;
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
            await ReleaseAsync(stored, written);
            if (ex is OperationCanceledException && ct.IsCancellationRequested)
            {
                logger.LogInformation("Upload by {ActorId} was abandoned by the client", actorId);
                throw;
            }
            logger.LogError(ex, "Failed to store uploaded files of {ActorId}", actorId);
            return failed;
        }

        T outcome;
        try
        {
            // Not cancellable: the objects are already up, and an abandoned request must still end
            // in either committed rows or deleted objects, never in between.
            outcome = await unitOfWork.ExecuteAsync(token => commit(stored, now, token), result => committed(result),
                TransactionMode.ReadCommitted, CancellationToken.None);
        }
        catch (Exception ex)
        {
            // Any failure here, not only a database one: the purge would get them, but not for a day.
            await ReleaseAsync(stored, written);
            if (ex is not PersistenceException persistence) throw;
            logger.LogError(persistence.InnerException ?? ex, "Failed to save uploaded files of {ActorId}", actorId);
            return failed;
        }

        if (!committed(outcome)) await ReleaseAsync(stored, written);
        return outcome;
    }

    /// <summary>
    /// Best effort and early: deletes the objects of a failed upload and stamps their rows purged, so they stop
    /// taking room in the storage limit at once. What could not be deleted keeps its row unstamped: the orphan
    /// purge deletes it after the grace period anyway, and stamps it then.
    /// </summary>
    private async Task ReleaseAsync(StoredFile[] stored, IReadOnlyList<string> written)
    {
        var left = new HashSet<string>();
        foreach (var key in written)
        {
            try { await storage.DeleteAsync(key, CancellationToken.None); }
            catch (Exception ex)
            {
                left.Add(key);
                logger.LogWarning(ex, "Could not delete stored object {StorageKey} after a failed upload", key);
            }
        }
        var gone = stored.Where(f => !left.Contains(f.StorageKey)).Select(f => f.Id).ToArray();
        var now = clock.GetUtcNow();
        try
        {
            await unitOfWork.ExecuteAsync(token => storedFiles.StampPurgedAsync(gone, now, token),
                TransactionMode.None, CancellationToken.None);
        }
        catch (PersistenceException ex)
        {
            logger.LogWarning(ex.InnerException ?? ex, "Could not release the storage of a failed upload");
        }
    }
}
