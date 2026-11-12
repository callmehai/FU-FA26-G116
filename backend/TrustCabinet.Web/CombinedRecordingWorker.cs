using TrustCabinet.Domain.Recordings;

namespace TrustCabinet.Web;

/// <summary>
/// ADR-0062 — builds the combined recording of each hybrid meeting once its sources settle after the end, and again
/// when one changes. Every 30 seconds, one meeting at a time, until none is waiting.
/// </summary>
public sealed class CombinedRecordingWorker(IServiceScopeFactory scopes, ILogger<CombinedRecordingWorker> logger) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        using var timer = new PeriodicTimer(TimeSpan.FromSeconds(30));
        do
        {
            try
            {
                bool built;
                do
                {
                    using var scope = scopes.CreateScope();
                    built = await scope.ServiceProvider.GetRequiredService<ICombinedRecordingService>().BuildNextAsync(stoppingToken);
                }
                while (built && !stoppingToken.IsCancellationRequested);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                break;
            }
            catch (Exception error)
            {
                logger.LogError(error, "Combined recording sweep failed");
            }
        }
        while (await timer.WaitForNextTickAsync(stoppingToken));
    }
}
