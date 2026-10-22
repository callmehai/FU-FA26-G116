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
                result = await ProcessOnceAsync(stoppingToken);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                break;
            }
            catch (Exception exception)
            {
                logger.LogError(exception, "Could not read or update the invitation email queue.");
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

    internal async Task<EmailProcessingResult> ProcessOnceAsync(CancellationToken ct)
    {

        try
        {
            await using var scope = scopeFactory.CreateAsyncScope();
            // Optional only for legacy worker test doubles; production registers the processor.
            var dispatches = scope.ServiceProvider.GetService<MeetingInvitationDispatchProcessor>();
            if (dispatches is not null) await dispatches.ProcessNextAsync(ct);
        }
        catch (Exception exception) when (!ct.IsCancellationRequested)
        {
            // Provider exceptions may contain credentials. Queued email processing must continue.
            logger.LogWarning("Invitation dispatch failed; failure type {FailureType}. Queued email processing will continue.",
                exception.GetType().Name);
        }

        await using var processingScope = scopeFactory.CreateAsyncScope();
        var processor = processingScope.ServiceProvider.GetRequiredService<IEmailQueueProcessor>();
        return await processor.ProcessNextAsync(ct);
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
