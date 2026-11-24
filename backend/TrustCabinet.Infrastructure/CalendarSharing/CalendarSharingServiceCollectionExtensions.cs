using Microsoft.Extensions.DependencyInjection;
using TrustCabinet.Application.CalendarSharing;

namespace TrustCabinet.Infrastructure.CalendarSharing;

public static class CalendarSharingServiceCollectionExtensions
{
    /// <summary>
    /// SCRUM-142 — calendar sharing (UC-80a → UC-80f), its stop (BR-142, BR-143) and the shared week (UC-81c); SCRUM-137 — the
    /// meeting-time search and the draft from a slot (UC-81a, UC-81b). The services are registered by
    /// <c>AddApplication</c>.
    /// </summary>
    public static IServiceCollection AddTrustCabinetCalendarSharing(this IServiceCollection services)
    {
        services.AddScoped<ICalendarShareRepository, CalendarShareRepository>();
        services.AddScoped<ICalendarBusyQuery, CalendarBusyQuery>();
        return services;
    }
}
