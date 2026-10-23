using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using TrustCabinet.Domain.Documents;

namespace TrustCabinet.Infrastructure.Documents;

/// <summary>
/// BR-213: deletes the object of every stored file no library row and no active meeting document
/// points at any more, once its grace period is over, then stamps <c>PurgedAt</c>. The row stays.
/// Nothing can start pointing at such a file again — attaching always goes through a library row —
/// so checking once before the delete is enough. That includes the rows an upload writes before its
/// objects (ADR-0024 §4): if the upload never committed, they are orphans like any other.
/// </summary>
public sealed class OrphanFilePurger(TrustCabinetDbContext db, IFileStorage storage, TimeProvider clock,
    IOptions<FileStorageOptions> options, ILogger<OrphanFilePurger> logger)
{
    private const int BatchSize = 100;

    public async Task<int> PurgeAsync(CancellationToken ct)
    {
        var now = clock.GetUtcNow();
        var cutoff = now - TimeSpan.FromHours(options.Value.OrphanGracePeriodHours);
        // Batches until none are left. A file whose delete fails sits out the rest of this run, so
        // it cannot keep the files behind it from ever being reached. A dead store therefore costs
        // one failed delete per orphan per run, which is fine at this scale.
        var failed = new List<Guid>();
        var purged = 0;
        while (true)
        {
            var orphans = await db.StoredFiles.AsNoTracking()
                .Where(f => f.PurgedAt == null && f.UploadedAt < cutoff && !failed.Contains(f.Id)
                    && !db.Documents.Any(d => d.StoredFileId == f.Id)
                    && !db.MeetingDocuments.Any(d => d.StoredFileId == f.Id && d.RemovedAt == null))
                .OrderBy(f => f.UploadedAt).Take(BatchSize)
                .Select(f => new { f.Id, f.StorageKey })
                .ToListAsync(ct);
            if (orphans.Count == 0) return purged;

            foreach (var orphan in orphans)
            {
                try { await storage.DeleteAsync(orphan.StorageKey, ct); }
                // Only our own shutdown stops the run; an SDK timeout surfaces as a cancellation too.
                catch (Exception ex) when (!ct.IsCancellationRequested)
                {
                    // Left unstamped, so the next run tries again.
                    logger.LogWarning(ex, "Could not purge stored object {StorageKey}", orphan.StorageKey);
                    failed.Add(orphan.Id);
                    continue;
                }
                purged += await db.StoredFiles.Where(f => f.Id == orphan.Id && f.PurgedAt == null)
                    .ExecuteUpdateAsync(s => s.SetProperty(f => f.PurgedAt, now).SetProperty(f => f.UpdatedAt, now), ct);
            }
        }
    }
}

/// <summary>Runs <see cref="OrphanFilePurger"/> at start-up and then every interval, while a store is configured.</summary>
public sealed class OrphanFilePurgeWorker(IServiceScopeFactory scopes, IFileStorage storage,
    IOptions<FileStorageOptions> options, ILogger<OrphanFilePurgeWorker> logger) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        if (!storage.IsConfigured) return;
        // PeriodicTimer throws on a zero period, which would stop the whole host at start-up.
        using var timer = new PeriodicTimer(TimeSpan.FromMinutes(Math.Max(1, options.Value.OrphanPurgeIntervalMinutes)));
        do
        {
            try
            {
                await using var scope = scopes.CreateAsyncScope();
                var purged = await scope.ServiceProvider.GetRequiredService<OrphanFilePurger>().PurgeAsync(stoppingToken);
                if (purged > 0) logger.LogInformation("Purged {Count} orphaned stored files", purged);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                break;
            }
            catch (Exception ex)
            {
                // Anything else, a timeout's cancellation included, must not stop the host (StopHost is the default).
                logger.LogError(ex, "Orphan file purge failed");
            }
        }
        while (await timer.WaitForNextTickAsync(stoppingToken));
    }
}
