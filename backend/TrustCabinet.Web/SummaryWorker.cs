using TrustCabinet.Domain.Summaries;

namespace TrustCabinet.Web;

/// <summary>
/// Writes queued AI drafts one at a time (SCRUM-36): the model runs on one laptop, which answers one request at a time
/// anyway, so a queue drained in order is no slower and never piles requests on it.
/// </summary>
public sealed class SummaryWorker(IServiceScopeFactory scopes, ILogger<SummaryWorker> logger) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        try
        {
            using var scope = scopes.CreateScope();
            var requeued = await scope.ServiceProvider.GetRequiredService<ISummaryService>().RequeueInterruptedAsync(stoppingToken);
            if (requeued > 0) logger.LogInformation("Requeued {Count} AI drafts interrupted by the last shutdown", requeued);
        }
        catch (Exception error) when (error is not OperationCanceledException)
        {
            logger.LogError(error, "Could not requeue interrupted AI drafts");
        }

        using var timer = new PeriodicTimer(TimeSpan.FromSeconds(3));
        do
        {
            try
            {
                bool worked;
                do
                {
                    using var scope = scopes.CreateScope();
                    worked = await scope.ServiceProvider.GetRequiredService<ISummaryService>().ProcessNextAsync(stoppingToken);
                }
                while (worked && !stoppingToken.IsCancellationRequested);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                break;
            }
            catch (Exception error)
            {
                logger.LogError(error, "AI draft run failed");
            }
        }
        while (await timer.WaitForNextTickAsync(stoppingToken));
    }
}
