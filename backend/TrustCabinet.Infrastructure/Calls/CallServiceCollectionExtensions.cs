using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using TrustCabinet.Domain.Calls;
using TrustCabinet.Domain.Participants;

namespace TrustCabinet.Infrastructure.Calls;

public static class CallServiceCollectionExtensions
{
    /// <summary>
    /// Registers the meeting call room (ADR-0010). A missing LiveKit section is not an error: the
    /// app still starts and the call endpoints answer 503, so nobody working on something else has
    /// to run LiveKit.
    /// </summary>
    public static IServiceCollection AddTrustCabinetCalls(this IServiceCollection services, IConfiguration configuration)
    {
        services.Configure<LiveKitOptions>(configuration.GetSection(LiveKitOptions.SectionName));
        services.AddHttpClient(LiveKitGateway.HttpClientName, client => client.Timeout = TimeSpan.FromSeconds(5));
        services.AddSingleton<ILiveKitGateway, LiveKitGateway>();
        services.AddScoped<ICallRepository, CallRepository>();
        services.AddScoped<ICallRoomService, CallRoomService>();
        // In place of the no-op Participants registers, whichever of the two is added first.
        services.Replace(ServiceDescriptor.Scoped<IMeetingParticipantAccessPublisher, CallRoomAccessPublisher>());
        return services;
    }
}
