using Microsoft.AspNetCore.SignalR;
using TrustCabinet.Application.Notifications;
using TrustCabinet.Application.Users;
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
        var service = scope.ServiceProvider.GetRequiredService<INotificationService>();
        var users = scope.ServiceProvider.GetRequiredService<ICallerAccountQuery>();
        await service.QueueStartingSoonAsync(ct);
        // The reminder email rides the same tick; a failure there must not hold back the bells below.
        if (scope.ServiceProvider.GetService<IMeetingReminderEmails>() is { } reminderEmails)
        {
            try
            {
                await reminderEmails.QueueDueAsync(ct);
            }
            catch (Exception error) when (!ct.IsCancellationRequested)
            {
                logger.LogError(error, "Queueing reminder emails failed");
            }
        }
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
