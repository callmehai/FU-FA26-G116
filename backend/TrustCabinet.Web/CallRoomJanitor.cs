using TrustCabinet.Domain.Calls;

namespace TrustCabinet.Web;

/// <summary>
/// Reconciles meeting rooms with LiveKit every 30 seconds (ADR-0010). LiveKit webhooks are not
/// guaranteed to arrive, and access can be lost mid-meeting (account deactivated, participant
/// removed, meeting ended) while the connected client keeps a refreshed token — so this, not the
/// token lifetime, is what closes rooms and removes people who may no longer be there.
/// </summary>
public sealed class CallRoomJanitor(
    IServiceScopeFactory scopes,
    ILogger<CallRoomJanitor> logger) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        using var timer = new PeriodicTimer(TimeSpan.FromSeconds(30));
        do
        {
            try
            {
                using var scope = scopes.CreateScope();
                await scope.ServiceProvider.GetRequiredService<ICallRoomService>().ReconcileAsync(stoppingToken);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                break;
            }
            catch (Exception error)
            {
                logger.LogError(error, "Call room reconciliation failed");
            }
        }
        while (await timer.WaitForNextTickAsync(stoppingToken));
    }
}
