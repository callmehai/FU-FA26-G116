using Microsoft.Extensions.DependencyInjection;
using TrustCabinet.Application.CalendarSharing;

namespace TrustCabinet.Infrastructure.CalendarSharing;

public static class CalendarSharingServiceCollectionExtensions
{
    /// <summary>
    /// SCRUM-142 — calendar sharing (UC-84a → UC-84f), its stop (BR-424) and the shared week (UC-85c); SCRUM-137 — the
    /// meeting-time search and the draft from a slot (UC-85a, UC-85b). The services are registered by
    /// <c>AddApplication</c>.
    /// </summary>
    public static IServiceCollection AddTrustCabinetCalendarSharing(this IServiceCollection services)
    {
        services.AddScoped<ICalendarShareRepository, CalendarShareRepository>();
        services.AddScoped<ICalendarBusyQuery, CalendarBusyQuery>();
        return services;
    }
}
