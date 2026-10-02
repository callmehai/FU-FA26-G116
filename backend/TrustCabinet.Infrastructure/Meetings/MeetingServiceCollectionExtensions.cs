using Microsoft.Extensions.DependencyInjection;
using TrustCabinet.Domain.Meetings;

namespace TrustCabinet.Infrastructure.Meetings;

public static class MeetingServiceCollectionExtensions
{
    public static IServiceCollection AddTrustCabinetMeetings(this IServiceCollection services)
    {
        services.AddSingleton<TimeProvider>(TimeProvider.System);
        services.AddScoped<IMeetingRepository, MeetingRepository>();
        services.AddScoped<IMeetingService, MeetingService>();
        return services;
    }
}
