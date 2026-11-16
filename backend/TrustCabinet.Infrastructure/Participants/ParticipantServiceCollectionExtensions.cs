using Microsoft.Extensions.DependencyInjection;
using TrustCabinet.Application.Participants;

namespace TrustCabinet.Infrastructure.Participants;

public static class ParticipantServiceCollectionExtensions
{
    public static IServiceCollection AddTrustCabinetParticipants(this IServiceCollection services)
    {
        services.AddScoped<IParticipantRepository, ParticipantRepository>();
        // The roster notice is registered by AddApplication (Email), the speaking cleanup by AddTrustCabinetSpeaking.
        return services;
    }
}
