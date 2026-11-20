using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using TrustCabinet.Application.Abstractions;
using TrustCabinet.Application.Audit;
using TrustCabinet.Application.Meetings;
using TrustCabinet.Application.Recordings;
using TrustCabinet.Application.Votes;
using TrustCabinet.Domain.Audit;
using TrustCabinet.Domain.Enums;
using TrustCabinet.Domain.Recordings;

namespace TrustCabinet.Application.Retention;

/// <summary>
/// ADR-0067 — removes what has outlived its purpose. Each rule deletes or clears by a fixed age and never reads a
/// person's choice: erasing a recording, withdrawing a voice consent and deleting a sentence happen where the person
/// acts, and only their leftovers end up here.
/// </summary>
public sealed class DataRetentionService(IRetentionRepository rows, IMeetingRepository meetings, IUnitOfWork unitOfWork,
    IRecordingStorage storage, IVoteMeetingEnd voteEnd, IAuditService audit, TimeProvider clock,
    IOptions<RetentionOptions> options, ILogger<DataRetentionService> logger) : IDataRetentionService
{
    private const int BatchSize = 500;
    private const int MeetingBatchSize = 50;

    /// <summary>Every extension a recording or a sentence is stored under; nothing else under the folder is touched.</summary>
    private static readonly HashSet<string> AudioExtensions = new(
        RecordingRules.Extensions.Values.Concat(SpeechClipRules.Extensions.Values), StringComparer.OrdinalIgnoreCase);

    public async Task<DataRetentionRun> RunAsync(CancellationToken ct = default)
    {
        var (files, bytes) = await Safely("orphan audio", () => DeleteOrphanAudioAsync(ct), (0, 0L), ct);
        var drafts = await Safely("cancelled meeting drafts", () => DeleteCancelledMeetingDraftsAsync(ct), 0, ct);
        var notifications = await Safely("notifications", () => PruneNotificationsAsync(ct), 0, ct);
        var challenges = await Safely("guest verification codes", () => PruneGuestChallengesAsync(ct), 0, ct);
        var emails = await Safely("email bodies", () => ScrubEmailBodiesAsync(ct), 0, ct);
        return new(files, bytes, drafts, notifications, challenges, emails);
    }

    private async Task<T> Safely<T>(string rule, Func<Task<T>> run, T none, CancellationToken ct)
    {
        try
        {
            return await run();
        }
        catch (Exception ex) when (!ct.IsCancellationRequested)
        {
            logger.LogError(ex is PersistenceException { InnerException: { } inner } ? inner : ex, "Retention rule {Rule} failed", rule);
            unitOfWork.DiscardChanges();
            return none;
        }
    }

    public async Task<(int Files, long Bytes)> DeleteOrphanAudioAsync(CancellationToken ct = default)
    {
        var graceHours = options.Value.OrphanAudioGraceHours;
        var cutoff = clock.GetUtcNow() - TimeSpan.FromHours(graceHours);
        var files = 0;
        long bytes = 0;
        var batch = new List<StoredAudioFile>(BatchSize);
        // Streamed by the storage, never listed whole; links are skipped there, so nothing outside the folder is reached.
        foreach (var file in storage.ListWrittenBefore(cutoff))
        {
            ct.ThrowIfCancellationRequested();
            if (!AudioExtensions.Contains(Path.GetExtension(file.StoragePath))) continue;
            batch.Add(file);
            if (batch.Count < BatchSize) continue;
            var (n, b) = await DeleteUnreferencedAsync(batch, ct);
            files += n;
            bytes += b;
            batch.Clear();
        }
        if (batch.Count > 0)
        {
            var (n, b) = await DeleteUnreferencedAsync(batch, ct);
            files += n;
            bytes += b;
        }
        if (files > 0)
        {
            logger.LogInformation("Deleted {Count} orphaned audio files ({Bytes} bytes)", files, bytes);
            await audit.RecordAsync(AuditActions.RecordingOrphanFilesDeleted, AuditActions.RecordingStorageObjectType, null, null,
                new { files, bytes, graceHours }, ct);
        }
        return (files, bytes);
    }

    /// <summary>A path still named by a recording that is not erased, or by a sentence, stays.</summary>
    private async Task<(int Files, long Bytes)> DeleteUnreferencedAsync(List<StoredAudioFile> batch, CancellationToken ct)
    {
        var kept = await unitOfWork.ExecuteAsync(
            token => rows.FindReferencedAudioAsync(batch.Select(b => b.StoragePath).ToList(), token), TransactionMode.None, ct);
        var files = 0;
        long bytes = 0;
        foreach (var file in batch)
        {
            if (kept.Contains(file.StoragePath)) continue;
            try
            {
                storage.Delete(file.StoragePath);
                files++;
                bytes += file.ByteSize;
            }
            catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
            {
                logger.LogWarning(ex, "Could not delete orphaned audio file {Path}", file.StoragePath);
            }
        }
        return (files, bytes);
    }

    public async Task<int> DeleteCancelledMeetingDraftsAsync(CancellationToken ct = default)
    {
        var ids = await unitOfWork.ExecuteAsync(
            token => rows.ListCancelledMeetingsWithDraftsAsync(MeetingBatchSize, token), TransactionMode.None, ct);
        var deleted = 0;
        foreach (var id in ids)
        {
            try
            {
                // Under the meeting lock, as every vote write and the cancel itself.
                deleted += await unitOfWork.ExecuteAsync(async token =>
                {
                    var meeting = await meetings.FindWorkflowAsync(id, forUpdate: true, token);
                    if (meeting is not { Status: MeetingStatus.Cancelled }) return 0;
                    var changes = await voteEnd.DeleteDraftsOnCancelAsync(id, meeting.CancelledById, clock.GetUtcNow(), token);
                    await unitOfWork.SaveChangesAsync(token);
                    return changes.Count;
                }, TransactionMode.ReadCommitted, ct);
            }
            catch (PersistenceException ex)
            {
                logger.LogError(ex.InnerException ?? ex, "Could not delete the vote drafts of cancelled meeting {MeetingId}", id);
                unitOfWork.DiscardChanges();
            }
        }
        return deleted;
    }

    public Task<int> PruneNotificationsAsync(CancellationToken ct = default)
    {
        var now = clock.GetUtcNow();
        var read = now.AddDays(-options.Value.ReadNotificationDays);
        var any = now.AddDays(-options.Value.NotificationDays);
        return unitOfWork.ExecuteAsync(token => rows.DeleteNotificationsAsync(any, read, token), TransactionMode.None, ct);
    }

    public Task<int> PruneGuestChallengesAsync(CancellationToken ct = default)
    {
        var cutoff = clock.GetUtcNow().AddDays(-options.Value.GuestChallengeDays);
        return unitOfWork.ExecuteAsync(token => rows.DeleteGuestChallengesAsync(cutoff, token), TransactionMode.None, ct);
    }

    public Task<int> ScrubEmailBodiesAsync(CancellationToken ct = default)
    {
        var now = clock.GetUtcNow();
        var cutoff = now.AddDays(-options.Value.EmailBodyDays);
        return unitOfWork.ExecuteAsync(token => rows.ScrubEmailBodiesAsync(cutoff, now, token), TransactionMode.None, ct);
    }
}
