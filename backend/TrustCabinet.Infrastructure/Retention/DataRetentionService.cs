using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Npgsql;
using TrustCabinet.Domain.Audit;
using TrustCabinet.Domain.Enums;
using TrustCabinet.Domain.Meetings;
using TrustCabinet.Domain.Recordings;
using TrustCabinet.Domain.Retention;
using TrustCabinet.Domain.Votes;
using TrustCabinet.Infrastructure.Recordings;

namespace TrustCabinet.Infrastructure.Retention;

/// <summary>How long each kind of leftover is kept before the retention job removes it (ADR-0067).</summary>
public sealed class RetentionOptions
{
    public const string SectionName = "Retention";

    /// <summary>How often the job runs. Every rule is about hours or days, so an hour is plenty.</summary>
    public int IntervalMinutes { get; set; } = 60;

    /// <summary>
    /// An audio file nothing points at is deleted only once it has not been written for this long: an upload, a take
    /// being saved or a mix being built writes its file before the row that names it commits.
    /// </summary>
    public int OrphanAudioGraceHours { get; set; } = 24;

    /// <summary>A notification already read is deleted this many days after it was created.</summary>
    public int ReadNotificationDays { get; set; } = 90;

    /// <summary>Any notification, read or not, is deleted this many days after it was created.</summary>
    public int NotificationDays { get; set; } = 180;

    /// <summary>A guest's verification code is deleted this many days after it expired; by then it no longer counts
    /// towards the daily limit either (BR-153).</summary>
    public int GuestChallengeDays { get; set; } = 1;

    /// <summary>The body, HTML and calendar attachment of an email sent or given up on are cleared after this many days.</summary>
    public int EmailBodyDays { get; set; } = 30;
}

public static class RetentionServiceCollectionExtensions
{
    public static IServiceCollection AddTrustCabinetRetention(this IServiceCollection services, IConfiguration configuration)
    {
        services.AddOptions<RetentionOptions>()
            .Bind(configuration.GetSection(RetentionOptions.SectionName))
            .Validate(o => o.IntervalMinutes is >= 1 and <= 1440, "Retention:IntervalMinutes must be 1–1440.")
            // Under an hour could delete a file whose upload has not committed yet.
            .Validate(o => o.OrphanAudioGraceHours is >= 1 and <= 720, "Retention:OrphanAudioGraceHours must be 1–720.")
            .Validate(o => o.ReadNotificationDays >= 1 && o.NotificationDays >= o.ReadNotificationDays,
                "Retention:ReadNotificationDays must be at least 1 and NotificationDays at least as long.")
            .Validate(o => o.GuestChallengeDays >= 1, "Retention:GuestChallengeDays must be at least 1.")
            .Validate(o => o.EmailBodyDays >= 1, "Retention:EmailBodyDays must be at least 1.")
            .ValidateOnStart();
        services.AddScoped<IDataRetentionService, DataRetentionService>();
        return services;
    }
}

/// <summary>
/// ADR-0067 — removes what has outlived its purpose. Each rule deletes or clears by a fixed age and never reads a
/// person's choice: erasing a recording, withdrawing a voice consent and deleting a sentence happen where the person
/// acts, and only their leftovers end up here.
/// </summary>
public sealed class DataRetentionService(TrustCabinetDbContext db, IMeetingRepository meetings, IVoteMeetingEnd voteEnd,
    IAuditService audit, TimeProvider clock, IOptions<RetentionOptions> options, IOptions<RecordingOptions> recordingOptions,
    ILogger<DataRetentionService> logger) : IDataRetentionService
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
            logger.LogError(ex, "Retention rule {Rule} failed", rule);
            db.ChangeTracker.Clear();
            return none;
        }
    }

    public async Task<(int Files, long Bytes)> DeleteOrphanAudioAsync(CancellationToken ct = default)
    {
        var root = Path.GetFullPath(recordingOptions.Value.StorageRoot);
        if (!Directory.Exists(root)) return (0, 0);
        var prefix = Path.EndsInDirectorySeparator(root) ? root : root + Path.DirectorySeparatorChar;
        var graceHours = options.Value.OrphanAudioGraceHours;
        var cutoff = (clock.GetUtcNow() - TimeSpan.FromHours(graceHours)).UtcDateTime;
        // Streamed, never listed whole; a link (to a file or a folder) is skipped, not followed, so nothing outside
        // the folder can be reached through one.
        var walk = new EnumerationOptions
        {
            RecurseSubdirectories = true, IgnoreInaccessible = true, AttributesToSkip = FileAttributes.ReparsePoint,
        };
        var files = 0;
        long bytes = 0;
        var batch = new List<(string Relative, FileInfo File)>(BatchSize);
        foreach (var path in Directory.EnumerateFiles(root, "*", walk))
        {
            ct.ThrowIfCancellationRequested();
            if (!AudioExtensions.Contains(Path.GetExtension(path))) continue;
            var full = Path.GetFullPath(path);
            if (!full.StartsWith(prefix, StringComparison.Ordinal)) continue;
            var file = new FileInfo(full);
            if (file.LinkTarget is not null || file.LastWriteTimeUtc >= cutoff) continue;
            batch.Add((Path.GetRelativePath(root, full).Replace('\\', '/'), file));
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

    /// <summary>
    /// A path still named by a recording that is not erased, or by a sentence, stays. An erased recording's path does
    /// not count: its row says the audio is gone, so a file still there is one its delete failed to remove.
    /// </summary>
    private async Task<(int Files, long Bytes)> DeleteUnreferencedAsync(List<(string Relative, FileInfo File)> batch,
        CancellationToken ct)
    {
        var paths = batch.Select(b => b.Relative).ToList();
        var kept = new HashSet<string>(await db.Recordings.AsNoTracking()
            .Where(r => r.Status != RecordingStatus.Deleted && paths.Contains(r.StoragePath))
            .Select(r => r.StoragePath).ToListAsync(ct), StringComparer.Ordinal);
        kept.UnionWith(await db.SpeechClips.AsNoTracking()
            .Where(c => c.AudioPath != null && paths.Contains(c.AudioPath))
            .Select(c => c.AudioPath!).ToListAsync(ct));
        var files = 0;
        long bytes = 0;
        foreach (var (relative, file) in batch)
        {
            if (kept.Contains(relative)) continue;
            try
            {
                var size = file.Length;
                file.Delete();
                files++;
                bytes += size;
            }
            catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
            {
                logger.LogWarning(ex, "Could not delete orphaned audio file {Path}", relative);
            }
        }
        return (files, bytes);
    }

    public async Task<int> DeleteCancelledMeetingDraftsAsync(CancellationToken ct = default)
    {
        var ids = await db.VotingSessions.AsNoTracking()
            .Where(v => v.Status == VotingStatus.Draft && v.Meeting.Status == MeetingStatus.Cancelled)
            .Select(v => v.MeetingId).Distinct().Take(MeetingBatchSize).ToListAsync(ct);
        var deleted = 0;
        foreach (var id in ids)
        {
            try
            {
                // Under the meeting lock, as every vote write and the cancel itself.
                deleted += await meetings.InTransactionAsync(async token =>
                {
                    var meeting = await meetings.FindWorkflowAsync(id, forUpdate: true, token);
                    if (meeting is not { Status: MeetingStatus.Cancelled }) return 0;
                    var changes = await voteEnd.DeleteDraftsOnCancelAsync(id, meeting.CancelledById, clock.GetUtcNow(), token);
                    await meetings.SaveChangesAsync(token);
                    return changes.Count;
                }, ct);
            }
            catch (Exception ex) when (ex is DbUpdateException or NpgsqlException or TimeoutException)
            {
                logger.LogError(ex, "Could not delete the vote drafts of cancelled meeting {MeetingId}", id);
                meetings.DiscardChanges();
            }
        }
        return deleted;
    }

    public Task<int> PruneNotificationsAsync(CancellationToken ct = default)
    {
        var now = clock.GetUtcNow();
        var read = now.AddDays(-options.Value.ReadNotificationDays);
        var any = now.AddDays(-options.Value.NotificationDays);
        return db.Notifications.Where(n => n.CreatedAt < any || (n.ReadAt != null && n.CreatedAt < read)).ExecuteDeleteAsync(ct);
    }

    public Task<int> PruneGuestChallengesAsync(CancellationToken ct = default)
    {
        var cutoff = clock.GetUtcNow().AddDays(-options.Value.GuestChallengeDays);
        return db.GuestVerificationChallenges.Where(c => c.ExpiresAt < cutoff).ExecuteDeleteAsync(ct);
    }

    public Task<int> ScrubEmailBodiesAsync(CancellationToken ct = default)
    {
        var now = clock.GetUtcNow();
        var cutoff = now.AddDays(-options.Value.EmailBodyDays);
        return db.EmailMessages
            .Where(e => (e.Status == EmailStatus.Sent || e.Status == EmailStatus.Failed) && e.CreatedAt < cutoff
                && (e.Body != "" || e.HtmlBody != null || e.CalendarAttachment != null))
            .ExecuteUpdateAsync(u => u
                .SetProperty(e => e.Body, "")
                .SetProperty(e => e.HtmlBody, (string?)null)
                .SetProperty(e => e.CalendarAttachment, (string?)null)
                .SetProperty(e => e.UpdatedAt, now), ct);
    }
}
