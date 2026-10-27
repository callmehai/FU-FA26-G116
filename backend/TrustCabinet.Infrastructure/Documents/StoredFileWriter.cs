using System.Data;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Npgsql;
using TrustCabinet.Domain.Documents;
using TrustCabinet.Domain.Entities;

namespace TrustCabinet.Infrastructure.Documents;

/// <summary>
/// The write order every upload shares (ADR-0024 §4): the <see cref="StoredFile"/> rows on their own, then the
/// bytes, then the rows that point at them in one transaction. Any failure deletes the objects of this
/// submission (BR-286): UC-33a AF-1, UC-33b AF-1 and UC-36.
/// </summary>
public sealed class StoredFileWriter(TrustCabinetDbContext db, IFileStorage storage, TimeProvider clock,
    ILogger<StoredFileWriter> logger)
{
    public bool IsConfigured => storage.IsConfigured;

    public static bool PersistenceError(Exception ex) => ex is DbUpdateException or NpgsqlException or TimeoutException;

    /// <summary>
    /// <paramref name="commit"/> runs in the transaction with the new rows and the upload time; it is committed
    /// only when <paramref name="committed"/> says so. Returns <paramref name="failed"/> when the rows, the bytes
    /// or the commit could not be stored.
    /// </summary>
    public async Task<T> WriteAsync<T>(Guid actorId, IReadOnlyList<DocumentUploadFile> files,
        Func<StoredFile[], DateTimeOffset, CancellationToken, Task<T>> commit, Func<T, bool> committed, T failed,
        CancellationToken ct)
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
            db.StoredFiles.AddRange(stored);
            await db.SaveChangesAsync(ct);
        }
        catch (Exception ex) when (PersistenceError(ex))
        {
            logger.LogError(ex, "Failed to record uploaded files of {ActorId}", actorId);
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
            await DeleteAsync(written);
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
            await using var transaction = await db.Database.BeginTransactionAsync(IsolationLevel.ReadCommitted, CancellationToken.None);
            outcome = await commit(stored, now, CancellationToken.None);
            if (committed(outcome)) await transaction.CommitAsync(CancellationToken.None);
        }
        catch (Exception ex)
        {
            // Any failure here, not only a database one: the purge would get them, but not for a day.
            await DeleteAsync(written);
            if (!PersistenceError(ex)) throw;
            logger.LogError(ex, "Failed to save uploaded files of {ActorId}", actorId);
            return failed;
        }

        if (!committed(outcome)) await DeleteAsync(written);
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
