using Microsoft.Extensions.Options;
using TrustCabinet.Domain.Retention;
using TrustCabinet.Infrastructure.Retention;

namespace TrustCabinet.Web;

/// <summary>
/// ADR-0067 — runs the retention rules at start-up and then every <c>Retention:IntervalMinutes</c>: orphaned audio
/// files, vote drafts left in cancelled meetings, old notifications, expired guest codes and old email bodies.
/// </summary>
public sealed class DataRetentionWorker(IServiceScopeFactory scopes, IOptions<RetentionOptions> options,
    ILogger<DataRetentionWorker> logger) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        using var timer = new PeriodicTimer(TimeSpan.FromMinutes(Math.Max(1, options.Value.IntervalMinutes)));
        do
        {
            try
            {
                await using var scope = scopes.CreateAsyncScope();
                var run = await scope.ServiceProvider.GetRequiredService<IDataRetentionService>().RunAsync(stoppingToken);
                if (run.OrphanAudioFiles + run.CancelledMeetingDrafts + run.Notifications + run.GuestChallenges + run.EmailBodies > 0)
                    logger.LogInformation(
                        "Retention: {Files} orphaned audio files ({Bytes} bytes), {Drafts} drafts of cancelled meetings, "
                        + "{Notifications} notifications, {Codes} guest codes, {Emails} email bodies",
                        run.OrphanAudioFiles, run.OrphanAudioBytes, run.CancelledMeetingDrafts, run.Notifications,
                        run.GuestChallenges, run.EmailBodies);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                break;
            }
            catch (Exception error)
            {
                logger.LogError(error, "Retention sweep failed");
            }
        }
        while (await timer.WaitForNextTickAsync(stoppingToken));
    }
}
