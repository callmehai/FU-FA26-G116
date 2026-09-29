using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using Microsoft.AspNetCore.Hosting;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Hosting;
using TrustCabinet.Domain.Authentication;
using TrustCabinet.Domain.Entities;
using TrustCabinet.Domain.Enums;
using TrustCabinet.Domain.Users;
using TrustCabinet.Infrastructure;
using TrustCabinet.Web.Tests.Users;

namespace TrustCabinet.Web.Tests.Notifications;

public class NotificationEndpointsTests : IDisposable
{
    private readonly Factory _factory = new();

    public void Dispose() => _factory.Dispose();

    [Fact]
    public async Task Notifications_and_hub_require_a_valid_sign_in()
    {
        using var client = _factory.CreateClient();

        Assert.Equal(HttpStatusCode.Unauthorized,
            (await client.GetAsync("/api/notifications")).StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized,
            (await client.PostAsync("/hubs/notifications/negotiate", null)).StatusCode);
    }

    [Fact]
    public async Task List_read_all_and_target_are_scoped_to_the_signed_in_user()
    {
        var owner = NewUser("owner@example.com");
        var other = NewUser("other@example.com");
        _factory.Accounts.WithUser(owner).WithUser(other);
        var mine = new Notification
        {
            UserId = owner.Id,
            Type = NotificationType.MeetingInvited,
            Title = "Thông báo của tôi",
        };
        var theirs = new Notification
        {
            UserId = other.Id,
            Type = NotificationType.MeetingInvited,
            Title = "Thông báo người khác",
        };
        await _factory.SeedAsync(mine, theirs);
        using var client = _factory.ClientFor(owner);

        var list = await client.GetFromJsonAsync<NotificationListJson>("/api/notifications");
        Assert.NotNull(list);
        Assert.Equal(1, list.UnreadCount);
        Assert.Equal(1, list.Page);
        Assert.Equal(20, list.PageSize);
        Assert.Equal(1, list.TotalCount);
        Assert.Equal(1, list.TotalPages);
        Assert.Equal(mine.Id, Assert.Single(list.Items).Id);
        Assert.Equal(HttpStatusCode.NotFound,
            (await client.PostAsync($"/api/notifications/{theirs.Id}/read", null)).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound,
            (await client.GetAsync($"/api/notifications/{theirs.Id}/target")).StatusCode);

        var read = await client.PostAsync($"/api/notifications/{mine.Id}/read", null);
        Assert.Equal(HttpStatusCode.OK, read.StatusCode);
        Assert.Equal(0,
            (await client.GetFromJsonAsync<UnreadCountJson>(
                "/api/notifications/unread-count"))!.UnreadCount);
        Assert.Equal(HttpStatusCode.OK,
            (await client.PostAsync("/api/notifications/read-all", null)).StatusCode);
        var invalid = await client.GetAsync("/api/notifications?page=0");
        Assert.Equal(HttpStatusCode.BadRequest, invalid.StatusCode);
        Assert.Equal("NOTIFICATION_INVALID_QUERY",
            (await invalid.Content.ReadFromJsonAsync<ErrorJson>())!.Code);
    }

    [Fact]
    public async Task Deactivated_account_is_refused_even_with_a_valid_token()
    {
        var user = NewUser("inactive@example.com");
        user.IsActive = false;
        _factory.Accounts.WithUser(user);
        using var client = _factory.ClientFor(user);

        Assert.Equal(HttpStatusCode.Forbidden,
            (await client.GetAsync("/api/notifications")).StatusCode);
    }

    [Fact]
    public async Task List_returns_NOTIFICATION_LOAD_FAILED_and_500_when_account_lookup_fails()
    {
        var user = NewUser("database-error@example.com");
        _factory.Accounts.WithUser(user);
        _factory.Accounts.NextReadThrows = new Npgsql.NpgsqlException("database unavailable");
        using var client = _factory.ClientFor(user);

        var response = await client.GetAsync("/api/notifications");

        Assert.Equal(HttpStatusCode.InternalServerError, response.StatusCode);
        Assert.Equal("NOTIFICATION_LOAD_FAILED",
            (await response.Content.ReadFromJsonAsync<ErrorJson>())!.Code);
    }

    [Fact]
    public void Test_factory_removes_the_notification_worker()
    {
        _ = _factory.CreateClient();

        Assert.DoesNotContain(
            _factory.Services.GetServices<IHostedService>(),
            service => service is NotificationWorker);
    }

    private static User NewUser(string email) => new()
    {
        FullName = email,
        Email = email,
        PasswordHash = "hash",
    };

    private sealed record NotificationListJson(
        NotificationJson[] Items,
        int UnreadCount,
        int TotalCount,
        int Page,
        int PageSize,
        int TotalPages);
    private sealed record NotificationJson(Guid Id);
    private sealed record UnreadCountJson(int UnreadCount);
    private sealed record ErrorJson(string Code);

    private sealed class Factory : TrustCabinetApiFactory
    {
        internal FakeUserAccountRepository Accounts { get; } = new();
        private readonly string _databaseName = Guid.NewGuid().ToString();

        protected override void ConfigureWebHost(IWebHostBuilder builder)
        {
            base.ConfigureWebHost(builder);
            builder.ConfigureServices(services =>
            {
                services.RemoveAll<TrustCabinetDbContext>();
                services.RemoveAll<DbContextOptions<TrustCabinetDbContext>>();
                services.RemoveAll<IDbContextOptionsConfiguration<TrustCabinetDbContext>>();
                services.AddDbContext<TrustCabinetDbContext>(options =>
                    options.UseInMemoryDatabase(_databaseName));
                services.RemoveAll<IUserAccountRepository>();
                services.AddSingleton<IUserAccountRepository>(Accounts);
            });
        }

        internal HttpClient ClientFor(User user)
        {
            var client = CreateClient();
            var issuer = Services.GetRequiredService<ITokenIssuer>();
            client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue(
                "Bearer", issuer.CreateAccessToken(user, []));
            return client;
        }

        internal async Task SeedAsync(params Notification[] notifications)
        {
            using var scope = Services.CreateScope();
            var db = scope.ServiceProvider.GetRequiredService<TrustCabinetDbContext>();
            db.Notifications.AddRange(notifications);
            await db.SaveChangesAsync();
        }
    }
}
