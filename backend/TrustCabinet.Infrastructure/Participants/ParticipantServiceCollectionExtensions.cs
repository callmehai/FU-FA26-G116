using TrustCabinet.Domain.Meetings;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using TrustCabinet.Domain.Participants;

namespace TrustCabinet.Infrastructure.Participants;

public static class ParticipantServiceCollectionExtensions
{
    public static IServiceCollection AddTrustCabinetParticipants(this IServiceCollection services)
    {
        services.AddScoped<IParticipantRepository, ParticipantRepository>();
        services.AddScoped<IParticipantService, ParticipantService>();
        services.TryAddScoped<IParticipantGuestAccess, UnavailableParticipantGuestAccess>();
        services.TryAddScoped<IGuestInvitationLinkProvider, UnavailableGuestInvitationLinkProvider>();
        services.TryAddScoped<IMeetingParticipantAccessPublisher, UnavailableMeetingParticipantAccessPublisher>();
        return services;
    }
}

public sealed class UnavailableParticipantGuestAccess : IParticipantGuestAccess
{
    public Task<bool> CanAccessAsync(MeetingGuestSession session, CancellationToken ct) => Task.FromResult(false);
    public Task<IReadOnlyDictionary<Guid, GuestDisplayProfile>> GetProfilesAsync(Guid meetingId, IReadOnlyCollection<Guid> invitationIds, CancellationToken ct)
        => Task.FromResult<IReadOnlyDictionary<Guid, GuestDisplayProfile>>(new Dictionary<Guid, GuestDisplayProfile>());
}
public sealed class UnavailableGuestInvitationLinkProvider : IGuestInvitationLinkProvider
{
    public Task<bool> IsAvailableAsync(CancellationToken ct) => Task.FromResult(false);
    public Task<GuestInvitationLink> PrepareAsync(Guid invitationId, CancellationToken ct) => Task.FromResult(new GuestInvitationLink(GuestLinkStatus.Unavailable));
}
public sealed class UnavailableMeetingParticipantAccessPublisher : IMeetingParticipantAccessPublisher
{
    // No room transport exists yet. This hook never participates in authorization.
    public Task PublishAsync(MeetingAccessChange change, CancellationToken ct) => Task.CompletedTask;
}
