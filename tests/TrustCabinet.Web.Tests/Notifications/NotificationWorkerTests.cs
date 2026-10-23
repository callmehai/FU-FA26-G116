using System.Text.Json;
using Microsoft.AspNetCore.SignalR;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Migrations.Operations;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using TrustCabinet.Domain.Entities;
using TrustCabinet.Domain.Enums;
using TrustCabinet.Domain.Meetings;
using TrustCabinet.Domain.Users;
using TrustCabinet.Infrastructure;
using TrustCabinet.Infrastructure.Meetings;
using TrustCabinet.Infrastructure.Migrations;
using TrustCabinet.Infrastructure.Notifications;
using TrustCabinet.Web.Tests.Users;

namespace TrustCabinet.Web.Tests.Notifications;

public class NotificationWorkerTests
{
    [Fact]
    public void Migration_marks_preexisting_notifications_as_already_pushed()
    {
        var migration = new NotificationManagement();
        var sql = Assert.Single(migration.UpOperations.OfType<SqlOperation>()).Sql;

        Assert.Contains("SET pushed_at = created_at", sql, StringComparison.Ordinal);
        Assert.DoesNotContain(
            migration.UpOperations.OfType<AddColumnOperation>(),
            operation => operation.Name == "action_url");
    }

    [Fact]
    public async Task Persisted_notification_is_pushed_only_to_its_recipient_and_not_repeated()
    {
        var recipient = new User
        {
            FullName = "Người nhận",
            Email = "recipient@example.com",
            PasswordHash = "hash",
        };
        var accounts = new FakeUserAccountRepository().WithUser(recipient);
        var databaseName = Guid.NewGuid().ToString();
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddSingleton(TimeProvider.System);
        services.AddDbContext<TrustCabinetDbContext>(options =>
            options.UseInMemoryDatabase(databaseName));
        services.AddScoped<IMeetingRepository, MeetingRepository>();
        services.AddTrustCabinetNotifications();
        services.AddSingleton<IUserAccountRepository>(accounts);
        using var provider = services.BuildServiceProvider();
        var notification = new Notification
        {
            UserId = recipient.Id,
            Title = "Thư mời",
            Type = NotificationType.MeetingInvited,
        };
        using (var scope = provider.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<TrustCabinetDbContext>();
            db.Notifications.Add(notification);
            await db.SaveChangesAsync();
        }

        var hub = new FakeHubContext();
        var worker = new NotificationWorker(provider.GetRequiredService<IServiceScopeFactory>(),
            hub, NullLogger<NotificationWorker>.Instance);
        await worker.ProcessOnceAsync(CancellationToken.None);
        await worker.ProcessOnceAsync(CancellationToken.None);

        Assert.Equal(recipient.Id.ToString("D"), hub.ClientsImpl.SelectedUser);
        Assert.Single(hub.ClientsImpl.Proxy.Calls);
        var call = hub.ClientsImpl.Proxy.Calls[0];
        Assert.Equal("notificationReceived", call.Method);
        var payload = JsonSerializer.SerializeToElement(Assert.Single(call.Args));
        Assert.Equal(notification.Id,
            payload.GetProperty("notification").GetProperty("Id").GetGuid());
        Assert.Equal(1, payload.GetProperty("unreadCount").GetInt32());
    }

    /// <summary>Also used by CapabilitiesChangePublisherTests.</summary>
    internal sealed class FakeHubContext : IHubContext<NotificationHub>
    {
        public FakeHubClients ClientsImpl { get; } = new();
        public IHubClients Clients => ClientsImpl;
        public IGroupManager Groups => throw new NotSupportedException();
    }

    internal sealed class FakeHubClients : IHubClients
    {
        public string? SelectedUser { get; private set; }
        public FakeClientProxy Proxy { get; } = new();
        public IClientProxy All => throw new NotSupportedException();
        public IClientProxy AllExcept(IReadOnlyList<string> excludedConnectionIds)
            => throw new NotSupportedException();
        public IClientProxy Client(string connectionId) => throw new NotSupportedException();
        public IClientProxy Clients(IReadOnlyList<string> connectionIds)
            => throw new NotSupportedException();
        public IClientProxy Group(string groupName) => throw new NotSupportedException();
        public IClientProxy GroupExcept(string groupName, IReadOnlyList<string> excludedConnectionIds)
            => throw new NotSupportedException();
        public IClientProxy Groups(IReadOnlyList<string> groupNames)
            => throw new NotSupportedException();
        public IClientProxy User(string userId)
        {
            SelectedUser = userId;
            return Proxy;
        }
        public IReadOnlyList<string>? SelectedUsers { get; private set; }
        public IClientProxy Users(IReadOnlyList<string> userIds)
        {
            SelectedUsers = userIds;
            return Proxy;
        }
    }

    internal sealed class FakeClientProxy : IClientProxy
    {
        public List<(string Method, object?[] Args)> Calls { get; } = [];

        public Task SendCoreAsync(string method, object?[] args, CancellationToken cancellationToken = default)
        {
            Calls.Add((method, args));
            return Task.CompletedTask;
        }
    }
}
