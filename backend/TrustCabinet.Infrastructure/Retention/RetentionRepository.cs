using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using TrustCabinet.Application.Retention;
using TrustCabinet.Domain.Enums;

namespace TrustCabinet.Infrastructure.Retention;

public static class RetentionServiceCollectionExtensions
{
    /// <summary>ADR-0067: the retention job's options and its repository; the job itself is in Application.</summary>
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
        services.AddScoped<IRetentionRepository, RetentionRepository>();
        return services;
    }
}

public sealed class RetentionRepository(TrustCabinetDbContext db) : IRetentionRepository
{
    public async Task<IReadOnlySet<string>> FindReferencedAudioAsync(IReadOnlyCollection<string> paths, CancellationToken ct)
    {
        var list = paths.ToList();
        var kept = new HashSet<string>(await db.Recordings.AsNoTracking()
            .Where(r => r.Status != RecordingStatus.Deleted && list.Contains(r.StoragePath))
            .Select(r => r.StoragePath).ToListAsync(ct), StringComparer.Ordinal);
        kept.UnionWith(await db.SpeechClips.AsNoTracking()
            .Where(c => c.AudioPath != null && list.Contains(c.AudioPath))
            .Select(c => c.AudioPath!).ToListAsync(ct));
        return kept;
    }

    public async Task<IReadOnlyList<Guid>> ListCancelledMeetingsWithDraftsAsync(int take, CancellationToken ct)
        => await db.VotingSessions.AsNoTracking()
            .Where(v => v.Status == VotingStatus.Draft && v.Meeting.Status == MeetingStatus.Cancelled)
            .Select(v => v.MeetingId).Distinct().Take(take).ToListAsync(ct);

    public Task<int> DeleteNotificationsAsync(DateTimeOffset createdBefore, DateTimeOffset readCreatedBefore, CancellationToken ct)
        => db.Notifications
            .Where(n => n.CreatedAt < createdBefore || (n.ReadAt != null && n.CreatedAt < readCreatedBefore))
            .ExecuteDeleteAsync(ct);

    public Task<int> DeleteGuestChallengesAsync(DateTimeOffset expiredBefore, CancellationToken ct)
        => db.GuestVerificationChallenges.Where(c => c.ExpiresAt < expiredBefore).ExecuteDeleteAsync(ct);

    public Task<int> ScrubEmailBodiesAsync(DateTimeOffset createdBefore, DateTimeOffset now, CancellationToken ct)
        => db.EmailMessages
            .Where(e => (e.Status == EmailStatus.Sent || e.Status == EmailStatus.Failed) && e.CreatedAt < createdBefore
                && (e.Body != "" || e.HtmlBody != null || e.CalendarAttachment != null))
            .ExecuteUpdateAsync(u => u
                .SetProperty(e => e.Body, "")
                .SetProperty(e => e.HtmlBody, (string?)null)
                .SetProperty(e => e.CalendarAttachment, (string?)null)
                .SetProperty(e => e.UpdatedAt, now), ct);
}
