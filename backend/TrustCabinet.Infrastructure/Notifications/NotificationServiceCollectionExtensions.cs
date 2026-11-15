using Microsoft.Extensions.DependencyInjection;
using TrustCabinet.Application.Notifications;

namespace TrustCabinet.Infrastructure.Notifications;

public static class NotificationServiceCollectionExtensions
{
    public static IServiceCollection AddTrustCabinetNotifications(this IServiceCollection services)
    {
        services.AddScoped<INotificationRepository, NotificationRepository>();
        return services;
    }
}
