using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Security.Cryptography;
using System.Text;
using Microsoft.AspNetCore.Hosting;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Logging;
using TrustCabinet.Application.Authentication;
using TrustCabinet.Application.Users;
using TrustCabinet.Domain.Entities;
using TrustCabinet.Domain.Enums;
using TrustCabinet.Infrastructure;
using TrustCabinet.Web.Tests.Users;

namespace TrustCabinet.Web.Tests.Notifications;

/// <summary>Round 1 gap tests for FE-13 (SCRUM-14 notifications, UC-66–UC-68) and
/// the Resend delivery webhook (SCRUM-15, UC-20).</summary>
public sealed class NotificationRound1Tests : IDisposable
{
    private readonly Factory _factory = new();

    public void Dispose() => _factory.Dispose();

    private static User NewUser(string email) => new()
    {
        FullName = email,
        Email = email,
        PasswordHash = "hash",
    };

    private User Account(string email, bool active = true)
    {
        var user = NewUser(email);
        user.IsActive = active;
        _factory.Accounts.WithUser(user);
        return user;
    }

    private static Notification Note(Guid userId, DateTimeOffset createdAt, bool read = false) => new()
    {
        UserId = userId,
        Type = NotificationType.MeetingInvited,
        Title = "Mời họp",
        CreatedAt = createdAt,
        ReadAt = read ? createdAt.AddMinutes(5) : null,
    };

    [Fact]
    public async Task NotificationPaging_ReturnsSecondPageWithOneItem()
    {
        var user = Account("paging@example.com");
        var now = DateTimeOffset.UtcNow;
        await _factory.SeedAsync(
            Note(user.Id, now.AddMinutes(-3)),
            Note(user.Id, now.AddMinutes(-2)),
            Note(user.Id, now.AddMinutes(-1)));
        using var client = _factory.ClientFor(user);

        var page = await client.GetFromJsonAsync<ListJson>("/api/notifications?page=2&pageSize=1");

        Assert.NotNull(page);
        Assert.Equal(2, page.Page);
        Assert.Equal(1, page.PageSize);
        Assert.Equal(3, page.TotalCount);
        Assert.Equal(3, page.TotalPages);
        Assert.Equal(3, page.UnreadCount);
        var second = Assert.Single(page.Items);
        Assert.Equal(now.AddMinutes(-2), second.CreatedAt);
    }

    [Fact]
    public async Task NotificationPaging_AcceptsPageSize100()
    {
        var user = Account("maxpage@example.com");
        var now = DateTimeOffset.UtcNow;
        await _factory.SeedAsync(Note(user.Id, now.AddMinutes(-2)), Note(user.Id, now.AddMinutes(-1)));
        using var client = _factory.ClientFor(user);

        var response = await client.GetAsync("/api/notifications?pageSize=100");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var page = await response.Content.ReadFromJsonAsync<ListJson>();
        Assert.NotNull(page);
        Assert.Equal(100, page.PageSize);
        Assert.Equal(2, page.Items.Count);
    }

    [Fact]
    public async Task NotificationPaging_RejectsPageSize101()
    {
        var user = Account("overpage@example.com");
        using var client = _factory.ClientFor(user);

        var response = await client.GetAsync("/api/notifications?pageSize=101");

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        var error = await response.Content.ReadFromJsonAsync<ErrorJson>();
        Assert.NotNull(error);
        Assert.Equal("MSG39", error.Code);
        Assert.Equal("Trang thông báo được yêu cầu không khả dụng.", error.Message);
        Assert.Equal("pageSize phải là số nguyên từ 1 đến 100.", error.Errors!["pageSize"]);
    }

    [Fact]
    public async Task NotificationPaging_RejectsNonNumericPaging()
    {
        var user = Account("badpage@example.com");
        using var client = _factory.ClientFor(user);

        var response = await client.GetAsync("/api/notifications?page=abc&pageSize=0");

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        var error = await response.Content.ReadFromJsonAsync<ErrorJson>();
        Assert.NotNull(error);
        Assert.Equal("MSG39", error.Code);
        Assert.True(error.Errors!.ContainsKey("page"));
        Assert.True(error.Errors.ContainsKey("pageSize"));
    }

    [Fact]
    public async Task NotificationList_EmptyForAccountWithNoNotification()
    {
        var user = Account("empty@example.com");
        using var client = _factory.ClientFor(user);

        var page = await client.GetFromJsonAsync<ListJson>("/api/notifications");

        Assert.NotNull(page);
        Assert.Empty(page.Items);
        Assert.Equal(0, page.UnreadCount);
        Assert.Equal(0, page.TotalCount);
        Assert.Equal(1, page.Page);
    }

    [Fact]
    public async Task UnreadCount_CountsOnlyCallersNotifications()
    {
        var first = Account("first-unread@example.com");
        var second = Account("second-unread@example.com");
        var now = DateTimeOffset.UtcNow;
        await _factory.SeedAsync(
            Note(first.Id, now.AddMinutes(-3)),
            Note(first.Id, now.AddMinutes(-2)),
            Note(second.Id, now.AddMinutes(-1)),
            Note(second.Id, now, read: true));

        using var firstClient = _factory.ClientFor(first);
        using var secondClient = _factory.ClientFor(second);

        Assert.Equal(2, (await firstClient.GetFromJsonAsync<UnreadJson>("/api/notifications/unread-count"))!.UnreadCount);
        Assert.Equal(1, (await secondClient.GetFromJsonAsync<UnreadJson>("/api/notifications/unread-count"))!.UnreadCount);
    }

    [Fact]
    public async Task UnreadCount_RequiresToken()
    {
        using var client = _factory.CreateClient();

        Assert.Equal(HttpStatusCode.Unauthorized,
            (await client.GetAsync("/api/notifications/unread-count")).StatusCode);
    }

    [Fact]
    public async Task UnreadCount_RefusesDeactivatedAccount()
    {
        var user = Account("gone-unread@example.com", active: false);
        using var client = _factory.ClientFor(user);

        Assert.Equal(HttpStatusCode.Forbidden,
            (await client.GetAsync("/api/notifications/unread-count")).StatusCode);
    }

    [Fact]
    public async Task MarkRead_UnknownIdIs404()
    {
        var user = Account("read-unknown@example.com");
        using var client = _factory.ClientFor(user);

        Assert.Equal(HttpStatusCode.NotFound,
            (await client.PostAsync($"/api/notifications/{Guid.CreateVersion7()}/read", null)).StatusCode);
    }

    [Fact]
    public async Task MarkRead_NonGuidIdIs404()
    {
        var user = Account("read-nonguid@example.com");
        using var client = _factory.ClientFor(user);

        Assert.Equal(HttpStatusCode.NotFound,
            (await client.PostAsync("/api/notifications/abc/read", null)).StatusCode);
    }

    [Fact]
    public async Task MarkRead_RequiresToken()
    {
        using var client = _factory.CreateClient();

        Assert.Equal(HttpStatusCode.Unauthorized,
            (await client.PostAsync($"/api/notifications/{Guid.CreateVersion7()}/read", null)).StatusCode);
    }

    [Fact]
    public async Task ReadAll_WithNothingUnreadChangesNothing()
    {
        var user = Account("all-read@example.com");
        var now = DateTimeOffset.UtcNow;
        await _factory.SeedAsync(Note(user.Id, now.AddMinutes(-2), read: true), Note(user.Id, now.AddMinutes(-1), read: true));
        using var client = _factory.ClientFor(user);
        var before = await client.GetFromJsonAsync<ListJson>("/api/notifications");

        var response = await client.PostAsync("/api/notifications/read-all", null);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var result = await response.Content.ReadFromJsonAsync<ReadAllJson>();
        Assert.NotNull(result);
        Assert.Equal(0, result.MarkedCount);
        Assert.Equal(0, result.UnreadCount);
        var after = await client.GetFromJsonAsync<ListJson>("/api/notifications");
        Assert.NotNull(after);
        Assert.Equal(
            before!.Items.Select(i => i.ReadAt).ToArray(),
            after.Items.Select(i => i.ReadAt).ToArray());
    }

    [Fact]
    public async Task ReadAll_RequiresToken()
    {
        using var client = _factory.CreateClient();

        Assert.Equal(HttpStatusCode.Unauthorized,
            (await client.PostAsync("/api/notifications/read-all", null)).StatusCode);
    }

    [Fact]
    public async Task Target_DoesNotMarkNotificationRead()
    {
        var user = Account("target-meeting@example.com");
        var meeting = new Meeting
        {
            Title = "Họp target",
            CreatedById = user.Id,
            Status = MeetingStatus.Published,
            PlannedStartTime = DateTimeOffset.UtcNow.AddHours(1),
            PlannedEndTime = DateTimeOffset.UtcNow.AddHours(2),
        };
        var notice = new Notification
        {
            UserId = user.Id,
            Type = NotificationType.MeetingPublished,
            Title = "Đã công bố",
            MeetingId = meeting.Id,
            TargetType = "Meeting",
            TargetId = meeting.Id,
        };
        await _factory.SeedAsync(meeting, notice);
        using var client = _factory.ClientFor(user);

        var target = await client.GetAsync($"/api/notifications/{notice.Id}/target");

        Assert.Equal(HttpStatusCode.OK, target.StatusCode);
        var body = await target.Content.ReadFromJsonAsync<TargetJson>();
        Assert.Equal(new TargetJson("Meeting", meeting.Id, $"/mgmt/meetings/{meeting.Id:D}"), body);
        var list = await client.GetFromJsonAsync<ListJson>("/api/notifications");
        var item = Assert.Single(list!.Items);
        Assert.False(item.IsRead);
        Assert.Null(item.ReadAt);
    }

    [Fact]
    public async Task Target_WithoutTargetIs404()
    {
        var user = Account("target-none@example.com");
        var notice = new Notification
        {
            UserId = user.Id,
            Type = NotificationType.MeetingInvited,
            Title = "Không target",
        };
        await _factory.SeedAsync(notice);
        using var client = _factory.ClientFor(user);

        Assert.Equal(HttpStatusCode.NotFound,
            (await client.GetAsync($"/api/notifications/{notice.Id}/target")).StatusCode);
    }

    [Fact]
    public async Task Target_CalendarShareOpensSharingPage()
    {
        var user = Account("share-party@example.com");
        var other = Account("share-other@example.com");
        var share = new CalendarShare { OwnerId = other.Id, ViewerId = user.Id };
        var notice = new Notification
        {
            UserId = user.Id,
            Type = NotificationType.CalendarShareReceived,
            Title = "Chia sẻ lịch",
            TargetType = "CalendarShare",
            TargetId = share.Id,
        };
        await _factory.SeedAsync(share, notice);
        using var client = _factory.ClientFor(user);

        var target = await client.GetAsync($"/api/notifications/{notice.Id}/target");

        Assert.Equal(HttpStatusCode.OK, target.StatusCode);
        Assert.Equal(
            new TargetJson("CalendarShare", share.Id, $"/calendar/sharing?share={share.Id:D}"),
            await target.Content.ReadFromJsonAsync<TargetJson>());
    }

    [Fact]
    public async Task Target_CalendarShareForStrangerIs404()
    {
        var user = Account("share-stranger@example.com");
        var owner = Account("share-owner@example.com");
        var viewer = Account("share-viewer@example.com");
        var share = new CalendarShare { OwnerId = owner.Id, ViewerId = viewer.Id };
        var notice = new Notification
        {
            UserId = user.Id,
            Type = NotificationType.CalendarShareReceived,
            Title = "Chia sẻ lịch",
            TargetType = "CalendarShare",
            TargetId = share.Id,
        };
        await _factory.SeedAsync(share, notice);
        using var client = _factory.ClientFor(user);

        Assert.Equal(HttpStatusCode.NotFound,
            (await client.GetAsync($"/api/notifications/{notice.Id}/target")).StatusCode);
    }

    [Fact]
    public async Task Target_VoiceSampleOpensVoiceIdPageThen404AfterDelete()
    {
        var user = Account("voice-owner@example.com");
        var sample = new VoiceSample
        {
            UserId = user.Id,
            ConsentId = Guid.CreateVersion7(),
            Ciphertext = [1, 2, 3],
            EmbeddingModel = "eres2net-base-200k@test",
        };
        var notice = new Notification
        {
            UserId = user.Id,
            Type = NotificationType.MeetingInvited,
            Title = "Mẫu giọng nói",
            TargetType = "VoiceSample",
            TargetId = sample.Id,
        };
        await _factory.SeedAsync(sample, notice);
        using var client = _factory.ClientFor(user);

        var target = await client.GetAsync($"/api/notifications/{notice.Id}/target");

        Assert.Equal(HttpStatusCode.OK, target.StatusCode);
        Assert.Equal(
            new TargetJson("VoiceSample", sample.Id, "/mgmt/my-voice?tab=proxy"),
            await target.Content.ReadFromJsonAsync<TargetJson>());

        await _factory.ActAsync(async db =>
        {
            var row = await db.VoiceSamples.SingleAsync(s => s.Id == sample.Id);
            db.VoiceSamples.Remove(row);
            await db.SaveChangesAsync();
        });

        Assert.Equal(HttpStatusCode.NotFound,
            (await client.GetAsync($"/api/notifications/{notice.Id}/target")).StatusCode);
    }

    [Fact]
    public async Task Target_RequiresToken()
    {
        using var client = _factory.CreateClient();

        Assert.Equal(HttpStatusCode.Unauthorized,
            (await client.GetAsync($"/api/notifications/{Guid.CreateVersion7()}/target")).StatusCode);
    }

    private sealed record ListJson(List<ItemJson> Items, int UnreadCount, int TotalCount, int Page, int PageSize, int TotalPages);
    private sealed record ItemJson(Guid Id, bool IsRead, DateTimeOffset? ReadAt, DateTimeOffset CreatedAt);
    private sealed record UnreadJson(int UnreadCount);
    private sealed record ReadAllJson(int MarkedCount, int UnreadCount);
    private sealed record TargetJson(string TargetType, Guid TargetId, string Url);
    private sealed record ErrorJson(string Code, string Message, Dictionary<string, string>? Errors);

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

        internal async Task SeedAsync(params object[] entities)
        {
            using var scope = Services.CreateScope();
            var db = scope.ServiceProvider.GetRequiredService<TrustCabinetDbContext>();
            foreach (var entity in entities)
            {
                db.Add(entity);
            }

            await db.SaveChangesAsync();
        }

        internal async Task ActAsync(Func<TrustCabinetDbContext, Task> action)
        {
            using var scope = Services.CreateScope();
            await action(scope.ServiceProvider.GetRequiredService<TrustCabinetDbContext>());
        }
    }
}

/// <summary>Round 1 gap tests for the Resend delivery webhook (FE-13, UC-20) over HTTP.
/// The older-event rule needs the real receiver row update, so this class runs on PostgreSQL.</summary>
public sealed class NotificationWebhookRound1Tests : IAsyncLifetime
{
    private const string WebhookPath = "/api/email/resend/webhook";

    private static readonly string Secret =
        "whsec_" + Convert.ToBase64String(SHA256.HashData(Encoding.UTF8.GetBytes("round1-noti-webhook")));

    private readonly PostgreSqlTestDatabase _database = new(
        PostgresTestDatabase.ConnectionString("trustcabinet_notification_round1_tests"));

    private Factory _host = null!;

    internal TrustCabinetDbContext Db() => new(new DbContextOptionsBuilder<TrustCabinetDbContext>()
        .UseNpgsql(_database.DataSource, o => o.MigrationsHistoryTable("__ef_migrations_history"))
        .UseSnakeCaseNamingConvention().Options);

    public async Task InitializeAsync()
    {
        await _database.CreateFromTemplateAsync(TestSchema.Migrated);
        await using var db = Db();
        await db.Database.MigrateAsync();
        _host = new Factory(this);
        _ = _host.Server;
    }

    public async Task DisposeAsync()
    {
        await _host.DisposeAsync();
        await _database.DisposeAsync();
    }

    private sealed class Factory(NotificationWebhookRound1Tests test) : TrustCabinetApiFactory
    {
        protected override void ConfigureWebHost(IWebHostBuilder builder)
        {
            base.ConfigureWebHost(builder);
            builder.UseSetting("Email:Resend:WebhookSecret", Secret);
            builder.ConfigureLogging(logging => logging.ClearProviders());
            builder.ConfigureServices(services =>
            {
                services.RemoveAll<TrustCabinetDbContext>();
                services.RemoveAll<DbContextOptions<TrustCabinetDbContext>>();
                services.AddDbContext<TrustCabinetDbContext>(o => o.UseNpgsql(test._database.DataSource,
                    p => p.MigrationsHistoryTable("__ef_migrations_history")).UseSnakeCaseNamingConvention());
            });
        }
    }

    private static string Sign(string id, long timestamp, byte[] body)
    {
        var key = Convert.FromBase64String(Secret["whsec_".Length..]);
        var prefix = Encoding.UTF8.GetBytes($"{id}.{timestamp}.");
        return "v1," + Convert.ToBase64String(HMACSHA256.HashData(key, prefix.Concat(body).ToArray()));
    }

    private async Task<HttpResponseMessage> PostWebhook(string type, string emailId, DateTimeOffset occurredAt)
    {
        var body = Encoding.UTF8.GetBytes(
            "{\"type\":\"" + type + "\",\"created_at\":\"" + occurredAt.ToString("O") + "\",\"data\":{\"email_id\":\"" + emailId + "\"}}");
        var timestamp = DateTimeOffset.UtcNow.ToUnixTimeSeconds();
        var id = $"msg_{Guid.CreateVersion7():N}";
        using var request = new HttpRequestMessage(HttpMethod.Post, WebhookPath)
        {
            Content = new ByteArrayContent(body),
        };
        request.Headers.Add("svix-id", id);
        request.Headers.Add("svix-timestamp", timestamp.ToString(System.Globalization.CultureInfo.InvariantCulture));
        request.Headers.Add("svix-signature", Sign(id, timestamp, body));
        return await _host.CreateClient().SendAsync(request);
    }

    [Fact]
    public async Task Webhook_IgnoresOlderEvent()
    {
        var at = new DateTimeOffset(2026, 11, 20, 9, 0, 0, TimeSpan.Zero);
        await using (var db = Db())
        {
            db.EmailMessages.Add(new EmailMessage
            {
                DeduplicationKey = $"round1:{Guid.CreateVersion7():D}",
                ToAddress = "member@example.com",
                Subject = "Mời họp",
                Body = "Nội dung",
                Status = EmailStatus.Sent,
                ProviderMessageId = "re-round1-provider",
                CreatedAt = at,
                UpdatedAt = at,
            });
            await db.SaveChangesAsync();
        }

        Assert.Equal(HttpStatusCode.OK,
            (await PostWebhook("email.delivered", "re-round1-provider", at.AddSeconds(10))).StatusCode);
        Assert.Equal(HttpStatusCode.OK,
            (await PostWebhook("email.delivery_delayed", "re-round1-provider", at.AddSeconds(5))).StatusCode);

        await using var check = Db();
        var stored = await check.EmailMessages.AsNoTracking().SingleAsync();
        Assert.Equal(EmailDeliveryState.Delivered, stored.DeliveryState);
        Assert.Equal(at.AddSeconds(10), stored.DeliveryStateAt);
        Assert.Equal(EmailStatus.Sent, stored.Status);
    }

    [Fact]
    public async Task Webhook_BodyAbove256KBIs413()
    {
        using var content = new ByteArrayContent(new byte[256 * 1024 + 1]);

        var response = await _host.CreateClient().PostAsync(WebhookPath, content);

        Assert.Equal(HttpStatusCode.RequestEntityTooLarge, response.StatusCode);
    }
}
