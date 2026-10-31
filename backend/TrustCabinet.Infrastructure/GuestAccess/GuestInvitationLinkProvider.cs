using Microsoft.Extensions.Options;
using TrustCabinet.Domain.Participants;
using TrustCabinet.Infrastructure.Email;

namespace TrustCabinet.Infrastructure.GuestAccess;

/// <summary>
/// UC-04's verification link for the SCRUM-20 invitation email. Nothing is persisted, so it joins the
/// dispatch transaction without writing, sending or rotating anything.
/// </summary>
public sealed class GuestInvitationLinkProvider(GuestCredentials credentials, IOptions<EmailDeliveryOptions> email)
    : IGuestInvitationLinkProvider
{
    public Task<bool> IsAvailableAsync(CancellationToken ct) => Task.FromResult(true);

    public Task<GuestInvitationLink> PrepareAsync(Guid invitationId, CancellationToken ct)
        => Task.FromResult(new GuestInvitationLink(GuestLinkStatus.Ready,
            BuildUrl(email.Value.ApplicationBaseUrl, invitationId, credentials.LinkToken(invitationId))));

    /// <summary>The token rides in the fragment: it never reaches the server, its logs or a Referer header.</summary>
    internal static string BuildUrl(string applicationBaseUrl, Guid invitationId, string token)
        => $"{applicationBaseUrl.Trim().TrimEnd('/')}/guest/{invitationId:D}#token={token}";
}
