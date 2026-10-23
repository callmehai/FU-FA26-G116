using TrustCabinet.Domain.Transcription;

namespace TrustCabinet.Web;

/// <summary>
/// Runs waiting transcripts one at a time (SCRUM-31): the speech worker is CPU bound, so a queue drained in order
/// is faster overall than several recordings fighting for the same cores.
/// </summary>
public sealed class TranscriptionWorker(IServiceScopeFactory scopes, ILogger<TranscriptionWorker> logger) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        try
        {
            using var scope = scopes.CreateScope();
            var requeued = await scope.ServiceProvider.GetRequiredService<ITranscriptionService>().RequeueInterruptedAsync(stoppingToken);
            if (requeued > 0) logger.LogInformation("Requeued {Count} transcripts interrupted by the last shutdown", requeued);
        }
        catch (Exception error) when (error is not OperationCanceledException)
        {
            logger.LogError(error, "Could not requeue interrupted transcripts");
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
                    worked = await scope.ServiceProvider.GetRequiredService<ITranscriptionService>().ProcessNextAsync(stoppingToken);
                }
                while (worked && !stoppingToken.IsCancellationRequested);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                break;
            }
            catch (Exception error)
            {
                logger.LogError(error, "Transcription run failed");
            }
        }
        while (await timer.WaitForNextTickAsync(stoppingToken));
    }
}
