using Microsoft.Extensions.DependencyInjection;
using TrustCabinet.Domain.Notifications;

namespace TrustCabinet.Infrastructure.Notifications;

public static class NotificationServiceCollectionExtensions
{
    public static IServiceCollection AddTrustCabinetNotifications(this IServiceCollection services)
    {
        services.AddScoped<INotificationRepository, NotificationRepository>();
        services.AddScoped<INotificationService, NotificationService>();
        return services;
    }
}
