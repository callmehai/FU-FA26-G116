using Microsoft.Extensions.DependencyInjection;
using TrustCabinet.Application.Email;
using TrustCabinet.Application.Participants;
using TrustCabinet.Application.Speaking;
using TrustCabinet.Infrastructure.Email;
using TrustCabinet.Infrastructure.Speaking;

namespace TrustCabinet.Infrastructure.Participants;

public static class ParticipantServiceCollectionExtensions
{
    public static IServiceCollection AddTrustCabinetParticipants(this IServiceCollection services)
    {
        services.AddScoped<IParticipantRepository, ParticipantRepository>();
        // The roster notice and the speaking cleanup are owned by modules that move later (Email at CA-17,
        // Speaking at CA-13); these thin adapters call their helpers unchanged (rule R9).
        services.AddScoped<IMeetingInvitationStager, MeetingInvitationStager>();
        services.AddScoped<ISpeakingRequestCloser, SpeakingRequestCloser>();
        return services;
    }
}
