using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using TrustCabinet.Domain.Notifications;

namespace TrustCabinet.Infrastructure.Email;

internal sealed class EmailDeliveryWorker(
    IServiceScopeFactory scopeFactory,
    IOptions<EmailDeliveryOptions> options,
    TimeProvider time,
    ILogger<EmailDeliveryWorker> logger) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        logger.LogInformation("The invitation email delivery worker started.");

        while (!stoppingToken.IsCancellationRequested)
        {
            EmailProcessingResult result;
            try
            {
                await using var scope = scopeFactory.CreateAsyncScope();
                var invitationQueue = scope.ServiceProvider.GetRequiredService<IMeetingInvitationQueue>();
                await invitationQueue.QueueNextUninitializedPublishedMeetingAsync(stoppingToken);
                var processor = scope.ServiceProvider.GetRequiredService<EmailQueueProcessor>();
                result = await processor.ProcessNextAsync(stoppingToken);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                break;
            }
            catch (Exception exception)
            {
                logger.LogError(exception, "Could not initialize, read or update the invitation email queue.");
                await Delay(TimeSpan.FromSeconds(options.Value.PollingIntervalSeconds), stoppingToken);
                continue;
            }

            // A failed email has its own NextAttemptAt. Continue immediately so one unavailable
            // recipient does not block later messages in this worker's sequential queue.
            var delay = result.Outcome == EmailProcessingOutcome.NoWork
                ? TimeSpan.FromSeconds(options.Value.PollingIntervalSeconds)
                : TimeSpan.Zero;

            if (delay > TimeSpan.Zero)
            {
                await Delay(delay, stoppingToken);
            }
        }
    }

    private async Task Delay(TimeSpan delay, CancellationToken ct)
    {
        try
        {
            await Task.Delay(delay, time, ct);
        }
        catch (OperationCanceledException) when (ct.IsCancellationRequested)
        {
            // Normal host shutdown.
        }
    }
}
