using Microsoft.Extensions.DependencyInjection;
using TrustCabinet.Application.Email;
using TrustCabinet.Application.Participants;
using TrustCabinet.Infrastructure.Email;

namespace TrustCabinet.Infrastructure.Participants;

public static class ParticipantServiceCollectionExtensions
{
    public static IServiceCollection AddTrustCabinetParticipants(this IServiceCollection services)
    {
        services.AddScoped<IParticipantRepository, ParticipantRepository>();
        // The roster notice is owned by Email, which moves at CA-17; this thin adapter calls its helper unchanged
        // (rule R9). The speaking cleanup is registered by AddTrustCabinetSpeaking.
        services.AddScoped<IMeetingInvitationStager, MeetingInvitationStager>();
        return services;
    }
}
