using TrustCabinet.Domain.Recordings;

namespace TrustCabinet.Web;

/// <summary>
/// Erases recordings whose retention period has run out (SCRUM-29). Without it <c>RetainUntil</c> would be a promise
/// on a screen; with it, the date shown to participants is the date the audio actually goes.
/// </summary>
public sealed class RecordingRetentionWorker(IServiceScopeFactory scopes, ILogger<RecordingRetentionWorker> logger)
    : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        using var timer = new PeriodicTimer(TimeSpan.FromMinutes(15));
        do
        {
            try
            {
                using var scope = scopes.CreateScope();
                var erased = await scope.ServiceProvider.GetRequiredService<IRecordingService>().EraseExpiredAsync(stoppingToken);
                if (erased > 0) logger.LogInformation("Erased {Count} recordings past their retention date", erased);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                break;
            }
            catch (Exception error)
            {
                logger.LogError(error, "Recording retention sweep failed");
            }
        }
        while (await timer.WaitForNextTickAsync(stoppingToken));
    }
}
