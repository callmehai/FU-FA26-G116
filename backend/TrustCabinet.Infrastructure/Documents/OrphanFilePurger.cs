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
/// so checking once before the delete is enough.
/// </summary>
public sealed class OrphanFilePurger(TrustCabinetDbContext db, IFileStorage storage, TimeProvider clock,
    IOptions<FileStorageOptions> options, ILogger<OrphanFilePurger> logger)
{
    private const int BatchSize = 100;

    public async Task<int> PurgeAsync(CancellationToken ct)
    {
        var now = clock.GetUtcNow();
        var cutoff = now - TimeSpan.FromHours(options.Value.OrphanGracePeriodHours);
        var orphans = await db.StoredFiles.AsNoTracking()
            .Where(f => f.PurgedAt == null && f.UploadedAt < cutoff
                && !db.Documents.Any(d => d.StoredFileId == f.Id)
                && !db.MeetingDocuments.Any(d => d.StoredFileId == f.Id && d.RemovedAt == null))
            .OrderBy(f => f.UploadedAt).Take(BatchSize)
            .Select(f => new { f.Id, f.StorageKey })
            .ToListAsync(ct);

        var purged = 0;
        foreach (var orphan in orphans)
        {
            try { await storage.DeleteAsync(orphan.StorageKey, ct); }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                // Left unstamped, so the next run tries again.
                logger.LogWarning(ex, "Could not purge stored object {StorageKey}", orphan.StorageKey);
                continue;
            }
            purged += await db.StoredFiles.Where(f => f.Id == orphan.Id && f.PurgedAt == null)
                .ExecuteUpdateAsync(s => s.SetProperty(f => f.PurgedAt, now).SetProperty(f => f.UpdatedAt, now), ct);
        }
        return purged;
    }
}

/// <summary>Runs <see cref="OrphanFilePurger"/> at start-up and then every interval, while a store is configured.</summary>
public sealed class OrphanFilePurgeWorker(IServiceScopeFactory scopes, IFileStorage storage,
    IOptions<FileStorageOptions> options, ILogger<OrphanFilePurgeWorker> logger) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        if (!storage.IsConfigured) return;
        using var timer = new PeriodicTimer(TimeSpan.FromMinutes(options.Value.OrphanPurgeIntervalMinutes));
        do
        {
            try
            {
                await using var scope = scopes.CreateAsyncScope();
                var purged = await scope.ServiceProvider.GetRequiredService<OrphanFilePurger>().PurgeAsync(stoppingToken);
                if (purged > 0) logger.LogInformation("Purged {Count} orphaned stored files", purged);
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                logger.LogError(ex, "Orphan file purge failed");
            }
        }
        while (await timer.WaitForNextTickAsync(stoppingToken));
    }
}
