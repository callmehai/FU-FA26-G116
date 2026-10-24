using Microsoft.AspNetCore.SignalR;
using TrustCabinet.Domain.Access;

namespace TrustCabinet.Web;

/// <summary>Pushes "your capabilities changed" on /hubs/notifications; the browser refreshes its token.</summary>
public sealed class CapabilitiesChangePublisher(IHubContext<NotificationHub> hub) : ICapabilitiesChangePublisher
{
    public Task PublishAsync(IReadOnlyCollection<Guid> userIds, CancellationToken ct = default)
        => userIds.Count == 0 ? Task.CompletedTask
            // No payload: what the account may now do is read back through /me and /refresh.
            : hub.Clients.Users(userIds.Select(id => id.ToString("D")).ToArray())
                .SendAsync("capabilitiesChanged", ct);
}
