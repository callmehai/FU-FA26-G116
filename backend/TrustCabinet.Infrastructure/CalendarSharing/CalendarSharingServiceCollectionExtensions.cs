using Microsoft.Extensions.DependencyInjection;
using TrustCabinet.Domain.CalendarSharing;

namespace TrustCabinet.Infrastructure.CalendarSharing;

public static class CalendarSharingServiceCollectionExtensions
{
    /// <summary>SCRUM-142 — calendar sharing (UC-84a → UC-84f), its stop (BR-424) and the shared week (UC-85c).</summary>
    public static IServiceCollection AddTrustCabinetCalendarSharing(this IServiceCollection services)
    {
        services.AddScoped<CalendarShareCloser>();
        services.AddScoped<CalendarBusyReader>();
        services.AddScoped<ICalendarSharingService, CalendarSharingService>();
        services.AddScoped<ICalendarShareStopper, CalendarShareStopper>();
        services.AddScoped<ISharedCalendarService, SharedCalendarService>();
        return services;
    }
}
