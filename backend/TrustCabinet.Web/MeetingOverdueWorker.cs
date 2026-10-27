using TrustCabinet.Domain.Meetings;

namespace TrustCabinet.Web;

/// <summary>
/// Cancels meetings nobody started a day after their planned start (SCRUM-99, BR-301). Every 15 minutes is enough: the
/// rule is about a day, and the organizer has all of it to reschedule.
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
                var cancelled = await scope.ServiceProvider.GetRequiredService<IMeetingOverdueService>()
                    .CancelOverdueAsync(stoppingToken);
                if (cancelled > 0) logger.LogInformation("Cancelled {Count} overdue meetings", cancelled);
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
