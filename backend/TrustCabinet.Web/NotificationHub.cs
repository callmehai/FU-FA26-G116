using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.SignalR;
using TrustCabinet.Domain.Users;
using TrustCabinet.Web.Endpoints;

namespace TrustCabinet.Web;

[Authorize]
public sealed class NotificationHub(IUserAccountRepository users) : Hub
{
    public override async Task OnConnectedAsync()
    {
        var userId = Context.User is null ? null : CapabilityGate.CurrentUserId(Context.User);
        var user = userId is null ? null : await users.FindByIdAsync(userId.Value,
            Context.ConnectionAborted);
        if (user is not { IsActive: true })
        {
            Context.Abort();
            return;
        }

        await base.OnConnectedAsync();
    }
}

public sealed class NotificationUserIdProvider : IUserIdProvider
{
    public string? GetUserId(HubConnectionContext connection)
        => connection.User is null
            ? null
            : CapabilityGate.CurrentUserId(connection.User)?.ToString("D");
}
