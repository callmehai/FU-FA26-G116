using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using TrustCabinet.Application.Agenda;
using TrustCabinet.Application.GuestAccess;
using TrustCabinet.Application.Participants;

namespace TrustCabinet.Infrastructure.GuestAccess;

public static class GuestAccessServiceCollectionExtensions
{
    /// <summary>
    /// Installs UC-04 in place of the fail-closed defaults registered by Agenda and Participants, so
    /// it must be called after those. Scoped, because the adapters read the request's DbContext.
    /// </summary>
    public static IServiceCollection AddTrustCabinetGuestAccess(this IServiceCollection services, IConfiguration configuration)
    {
        services.AddOptions<GuestAccessOptions>()
            .Bind(configuration.GetSection(GuestAccessOptions.SectionName))
            .ValidateDataAnnotations()
            .ValidateOnStart();
        services.AddSingleton<GuestCredentials>();
        services.AddSingleton<IGuestCredentials>(p => p.GetRequiredService<GuestCredentials>());
        services.AddScoped<IGuestSessionRepository, GuestSessionRepository>();
        services.AddScoped<IGuestVerificationCodeSender, GuestVerificationCodeSender>();
        services.Replace(ServiceDescriptor.Scoped<IGuestInvitationLinkProvider, GuestInvitationLinkProvider>());
        services.AddScoped<GuestSessionAccess>();
        services.Replace(ServiceDescriptor.Scoped<IAgendaGuestAccess>(p => p.GetRequiredService<GuestSessionAccess>()));
        services.Replace(ServiceDescriptor.Scoped<IParticipantGuestAccess>(p => p.GetRequiredService<GuestSessionAccess>()));
        return services;
    }
}
