using TrustCabinet.Web.Tests.Notifications;

namespace TrustCabinet.Web.Tests.Access;

/// <summary>
/// Pins the hub side of SCRUM-105. The event name is a bare string the frontend listens for
/// (<c>NotificationsProvider.tsx</c>); a typo on either side passes every other test and silently
/// stops the live update.
/// </summary>
public class CapabilitiesChangePublisherTests
{
    [Fact]
    public async Task Pushes_capabilities_changed_to_each_account_by_its_user_id()
    {
        var hub = new NotificationWorkerTests.FakeHubContext();
        var first = Guid.CreateVersion7();
        var second = Guid.CreateVersion7();

        await new CapabilitiesChangePublisher(hub).PublishAsync([first, second]);

        // "D" format, the same NotificationUserIdProvider derives from the token's "sub".
        Assert.Equal([first.ToString("D"), second.ToString("D")], hub.ClientsImpl.SelectedUsers);
        var call = Assert.Single(hub.ClientsImpl.Proxy.Calls);
        Assert.Equal("capabilitiesChanged", call.Method);
        Assert.Empty(call.Args);
    }

    [Fact]
    public async Task Sends_nothing_when_nobody_holds_the_role()
    {
        var hub = new NotificationWorkerTests.FakeHubContext();

        await new CapabilitiesChangePublisher(hub).PublishAsync([]);

        Assert.Empty(hub.ClientsImpl.Proxy.Calls);
    }
}
