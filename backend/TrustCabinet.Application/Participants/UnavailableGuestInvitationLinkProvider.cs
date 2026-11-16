namespace TrustCabinet.Application.Participants;

public sealed class UnavailableGuestInvitationLinkProvider : IGuestInvitationLinkProvider
{
    public Task<bool> IsAvailableAsync(CancellationToken ct) => Task.FromResult(false);
    public Task<GuestInvitationLink> PrepareAsync(Guid invitationId, CancellationToken ct) => Task.FromResult(new GuestInvitationLink(GuestLinkStatus.Unavailable));
}
