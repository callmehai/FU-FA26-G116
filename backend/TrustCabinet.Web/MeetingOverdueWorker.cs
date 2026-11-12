using TrustCabinet.Domain.Meetings;

namespace TrustCabinet.Web;

/// <summary>
/// Cancels meetings nobody started a day after their planned start (SCRUM-99, BR-301), and ends meetings nobody ended
/// 12 hours after their planned end (ADR-0062). Every 15 minutes is enough: both rules are about hours.
/// </summary>
public sealed class MeetingOverdueWorker(IServiceScopeFactory scopes, ILogger<MeetingOverdueWorker> logger)
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
                var overdue = scope.ServiceProvider.GetRequiredService<IMeetingOverdueService>();
                var cancelled = await overdue.CancelOverdueAsync(stoppingToken);
                if (cancelled > 0) logger.LogInformation("Cancelled {Count} overdue meetings", cancelled);
                var ended = await overdue.EndOverrunAsync(stoppingToken);
                if (ended > 0) logger.LogInformation("Ended {Count} meetings left running", ended);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                break;
            }
            catch (Exception error)
            {
                logger.LogError(error, "Overdue meeting sweep failed");
            }
        }
        while (await timer.WaitForNextTickAsync(stoppingToken));
    }
}
