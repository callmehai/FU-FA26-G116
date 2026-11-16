using Microsoft.Extensions.DependencyInjection;
using TrustCabinet.Application.Speaking;

namespace TrustCabinet.Infrastructure.Speaking;

public static class SpeakingServiceCollectionExtensions
{
    public static IServiceCollection AddTrustCabinetSpeaking(this IServiceCollection services)
    {
        services.AddScoped<ISpeakingRequestRepository, SpeakingRequestRepository>();
        // Participants closes a removed reader's requests through the narrower port (BR-337).
        services.AddScoped<ISpeakingRequestCloser, SpeakingRequestRepository>();
        return services;
    }
}
