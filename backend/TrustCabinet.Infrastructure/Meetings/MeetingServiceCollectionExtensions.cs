using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using TrustCabinet.Domain.Meetings;

namespace TrustCabinet.Infrastructure.Meetings;

public static class MeetingServiceCollectionExtensions
{
    public static IServiceCollection AddTrustCabinetMeetings(this IServiceCollection services, IConfiguration configuration)
    {
        services.AddOptions<MeetingOverdueOptions>()
            .Bind(configuration.GetSection(MeetingOverdueOptions.SectionName))
            .Validate(o => o.AutoCancelAfterHours is >= 1 and <= 720, "Meetings:AutoCancelAfterHours must be 1–720.")
            .ValidateOnStart();
        services.AddSingleton<TimeProvider>(TimeProvider.System);
        services.AddScoped<IMeetingRepository, MeetingRepository>();
        services.AddScoped<IMeetingService, MeetingService>();
        services.AddScoped<IMeetingLifecycleService, MeetingLifecycleService>();
        services.AddScoped<IMeetingOverdueService, MeetingOverdueService>();
        return services;
    }
}
