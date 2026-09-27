using Microsoft.AspNetCore.SignalR;
using TrustCabinet.Domain.Users;
using TrustCabinet.Infrastructure.Notifications;
using TrustCabinet.Web.Endpoints;

namespace TrustCabinet.Web;

/// <summary>Creates due reminders and pushes committed notifications; the list API catches missed pushes.</summary>
public sealed class NotificationWorker(
    IServiceScopeFactory scopes,
    IHubContext<NotificationHub> hub,
    ILogger<NotificationWorker> logger) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        using var timer = new PeriodicTimer(TimeSpan.FromSeconds(5));
        do
        {
            try
            {
                await ProcessOnceAsync(stoppingToken);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                break;
            }
            catch (Exception error)
            {
                logger.LogError(error, "Notification dispatch failed");
            }
        }
        while (await timer.WaitForNextTickAsync(stoppingToken));
    }

    public async Task ProcessOnceAsync(CancellationToken ct)
    {
        using var scope = scopes.CreateScope();
        var service = scope.ServiceProvider.GetRequiredService<NotificationService>();
        var users = scope.ServiceProvider.GetRequiredService<IUserAccountRepository>();
        await service.QueueStartingSoonAsync(ct);
        foreach (var notification in await service.PendingPushAsync(100, ct))
        {
            var recipient = await users.FindByIdAsync(notification.UserId, ct);
            if (recipient is { IsActive: true })
            {
                await hub.Clients.User(notification.UserId.ToString("D"))
                    .SendAsync("notificationReceived", new
                    {
                        notification = NotificationResponse.From(notification),
                        unreadCount = await service.GetUnreadCountAsync(notification.UserId, ct),
                    }, ct);
            }

            await service.MarkPushedAsync(notification.Id, ct);
        }
    }
}
