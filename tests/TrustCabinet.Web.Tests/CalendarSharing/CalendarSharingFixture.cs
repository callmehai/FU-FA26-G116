using System.IdentityModel.Tokens.Jwt;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Security.Claims;
using System.Text;
using System.Text.Json;
using Microsoft.AspNetCore.Hosting;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Logging;
using Microsoft.IdentityModel.Tokens;
using TrustCabinet.Domain.Access;
using TrustCabinet.Domain.Authentication;
using TrustCabinet.Domain.Entities;
using TrustCabinet.Domain.Enums;
using TrustCabinet.Infrastructure;
using TrustCabinet.Web.Tests.Authentication;
using TrustCabinet.Web.Tests.Users;

namespace TrustCabinet.Web.Tests.CalendarSharing;

/// <summary>
/// SCRUM-142 against a real PostgreSQL: the app with a fake clock (08/11/2026 10:00 in Vietnam), no Redis, and a switch to
/// make reads or saves of <c>calendar_shares</c> fail. Every <see cref="Seed"/> makes new people, so tests in one class
/// never see each other's shares.
/// </summary>
public sealed class CalendarSharingFixture : IAsyncLifetime
{
    /// <summary>Sunday 08/11/2026 10:00 in Hanoi.</summary>
    internal static readonly DateTimeOffset Start = new(2026, 11, 8, 3, 0, 0, TimeSpan.Zero);
    internal static readonly DateOnly Today = new(2026, 11, 8);

    internal readonly PostgreSqlTestDatabase Database = new(PostgresTestDatabase.ConnectionString("trustcabinet_calendar_tests"));
    internal readonly TestClock Clock = new();
    internal readonly FailureInterceptor Failures = new();
    internal Factory Host = null!;
    private const string SigningKey = "khoa-ky-chi-dung-trong-test-du-32-ky-tu";

    internal TrustCabinetDbContext Db() => new(new DbContextOptionsBuilder<TrustCabinetDbContext>()
        .UseNpgsql(Database.DataSource, o => o.MigrationsHistoryTable("__ef_migrations_history"))
        .UseSnakeCaseNamingConvention().Options);

    public async Task InitializeAsync()
    {
        await Database.CreateFromTemplateAsync(TestSchema.Migrated);
        await using var db = Db();
        await db.Database.MigrateAsync();
        Host = new Factory(this);
        _ = Host.Server;
    }

    public async Task DisposeAsync()
    {
        await Host.DisposeAsync();
        await Database.DisposeAsync();
    }

    internal sealed class Factory(CalendarSharingFixture fixture) : TrustCabinetApiFactory
    {
        protected override void ConfigureWebHost(IWebHostBuilder builder)
        {
            base.ConfigureWebHost(builder);
            builder.UseSetting("Email:Smtp:Enabled", "false");
            builder.ConfigureLogging(logging => logging.ClearProviders());
            builder.ConfigureServices(services =>
            {
                services.RemoveAll<TrustCabinetDbContext>();
                services.RemoveAll<DbContextOptions<TrustCabinetDbContext>>();
                services.AddDbContext<TrustCabinetDbContext>(o => o.UseNpgsql(fixture.Database.DataSource,
                    p => p.MigrationsHistoryTable("__ef_migrations_history")).UseSnakeCaseNamingConvention()
                    .AddInterceptors(fixture.Failures));
                services.RemoveAll<TimeProvider>();
                services.AddSingleton<TimeProvider>(fixture.Clock);
                // Locking an account and changing a role end sessions in Redis after the commit; no Redis here.
                services.RemoveAll<IRefreshTokenStore>();
                services.AddSingleton<IRefreshTokenStore>(new FakeRefreshTokenStore());
                services.RemoveAll<IPasswordResetTokenStore>();
                services.AddSingleton<IPasswordResetTokenStore>(new FakePasswordResetTokenStore());
            });
        }
    }

    internal sealed class TestClock : TimeProvider
    {
        internal DateTimeOffset Now = Start;
        public override DateTimeOffset GetUtcNow() => Now;
    }

    /// <summary>A database outage on reads of <c>calendar_shares</c>, or on any save.</summary>
    internal sealed class FailureInterceptor : DbCommandInterceptor, ISaveChangesInterceptor
    {
        internal bool FailShareRead;
        internal bool FailSave;

        public override ValueTask<InterceptionResult<System.Data.Common.DbDataReader>> ReaderExecutingAsync(
            System.Data.Common.DbCommand command, CommandEventData eventData,
            InterceptionResult<System.Data.Common.DbDataReader> result, CancellationToken cancellationToken = default)
            => FailShareRead && command.CommandText.Contains("calendar_shares", StringComparison.Ordinal)
                ? throw new Npgsql.NpgsqlException("Injected read failure.")
                : ValueTask.FromResult(result);

        public ValueTask<InterceptionResult<int>> SavingChangesAsync(DbContextEventData eventData,
            InterceptionResult<int> result, CancellationToken cancellationToken = default)
            => FailSave ? throw new DbUpdateException("Injected persistence failure.") : ValueTask.FromResult(result);
    }

    /// <summary>
    /// Fresh people for one test. Organizer and Organizer2 hold MeetingCreator (Secretary and Chair roles); Owner and
    /// Owner2 are Members, who own a calendar but may not view one; Locked is a deactivated Member; Admin administers.
    /// </summary>
    internal sealed record People(Guid Organizer, Guid Organizer2, Guid Owner, Guid Owner2, Guid Locked, Guid Admin);

    internal async Task<People> Seed()
    {
        Clock.Now = Start;
        Failures.FailShareRead = false;
        Failures.FailSave = false;
        await using var db = Db();
        var tag = Guid.NewGuid().ToString("N")[..8];
        User Person(string name, Guid role, bool active = true) => new()
        {
            FullName = $"{name} {tag}", Email = $"{name.Replace(' ', '.').ToLowerInvariant()}.{tag}@calendar.test",
            PasswordHash = "test-only-non-login-hash", RoleId = role, IsActive = active,
        };
        var users = new[]
        {
            Person("Lê Tổ Chức", BuiltInRoles.Secretary), Person("Phạm Chủ Toạ", BuiltInRoles.Chair),
            Person("Trần Chủ Lịch", BuiltInRoles.Member), Person("Võ Chủ Lịch", BuiltInRoles.Member),
            Person("Đỗ Đã Khoá", BuiltInRoles.Member, false), Person("Ngô Quản Trị", BuiltInRoles.Admin),
        };
        db.Users.AddRange(users);
        await db.SaveChangesAsync();
        return new(users[0].Id, users[1].Id, users[2].Id, users[3].Id, users[4].Id, users[5].Id);
    }

    /// <summary>A share written straight to the table, for tests about what happens to one that already exists.</summary>
    internal async Task<Guid> AddShare(Guid owner, Guid viewer, CalendarShareStatus status = CalendarShareStatus.Effective,
        CalendarShareLevel level = CalendarShareLevel.Level1, DateOnly? start = null, DateOnly? end = null,
        CalendarShareInitiator initiatedBy = CalendarShareInitiator.Viewer, DateTimeOffset? createdAt = null)
    {
        await using var db = Db();
        var share = new CalendarShare
        {
            OwnerId = owner, ViewerId = viewer, Status = status, Level = level, InitiatedBy = initiatedBy,
            StartDate = start ?? Today, EndDate = end ?? Today.AddDays(30),
            CreatedAt = createdAt ?? Clock.Now, UpdatedAt = createdAt ?? Clock.Now,
            DecidedAt = status == CalendarShareStatus.Pending ? null : Clock.Now,
        };
        db.CalendarShares.Add(share);
        await db.SaveChangesAsync();
        return share.Id;
    }

    internal async Task<CalendarShare> Share(Guid id)
    {
        await using var db = Db();
        return await db.CalendarShares.AsNoTracking().SingleAsync(s => s.Id == id);
    }

    internal async Task<List<Notification>> NotificationsOf(Guid userId)
    {
        await using var db = Db();
        return await db.Notifications.AsNoTracking().Where(n => n.UserId == userId).OrderBy(n => n.CreatedAt)
            .ThenBy(n => n.Id).ToListAsync();
    }

    internal async Task<List<AuditLogEntry>> AuditOf(Guid shareId)
    {
        await using var db = Db();
        // OccurredAt is the real clock (AuditService does not take a TimeProvider), so it orders the steps of a test.
        return await db.AuditLog.AsNoTracking().Where(a => a.ObjectId == shareId).OrderBy(a => a.OccurredAt)
            .ThenBy(a => a.Id).ToListAsync();
    }

    internal HttpClient Client(Guid? actor = null, params string[] capabilities)
    {
        var client = Host.CreateClient();
        if (actor is { } id) client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", Token(id, capabilities));
        return client;
    }

    private static string Token(Guid actor, params string[] capabilities) => new JwtSecurityTokenHandler().WriteToken(
        new JwtSecurityToken("TrustCabinet", "TrustCabinet",
            [new Claim("sub", actor.ToString()), .. capabilities.Select(c => new Claim("cap", c))],
            expires: DateTime.UtcNow.AddMinutes(10),
            signingCredentials: new SigningCredentials(new SymmetricSecurityKey(Encoding.UTF8.GetBytes(SigningKey)),
                SecurityAlgorithms.HmacSha256)));

    /// <summary>Asserts the status and the code, and returns the body.</summary>
    internal static async Task<JsonElement> AssertCode(HttpResponseMessage response, int status, string code)
    {
        var raw = await response.Content.ReadAsStringAsync();
        Assert.True((int)response.StatusCode == status, $"Expected {status}, got {(int)response.StatusCode}: {raw}");
        var body = JsonDocument.Parse(raw).RootElement;
        Assert.Equal(code, body.GetProperty("code").GetString());
        return body;
    }

    internal static async Task AssertNotFound(HttpResponseMessage response)
    {
        var raw = await response.Content.ReadAsStringAsync();
        Assert.True(response.StatusCode == System.Net.HttpStatusCode.NotFound, $"Expected 404, got {(int)response.StatusCode}: {raw}");
        Assert.Equal("", raw);
    }

    internal static string Day(DateOnly date) => date.ToString("yyyy-MM-dd");

    internal static Task<HttpResponseMessage> Request(HttpClient client, Guid ownerId, string level, DateOnly start, DateOnly end)
        => client.PostAsJsonAsync("/api/calendar/shares", new { ownerId, level, startDate = Day(start), endDate = Day(end) });

    internal static Task<HttpResponseMessage> Direct(HttpClient client, Guid viewerId, DateOnly start, DateOnly end)
        => client.PostAsJsonAsync("/api/calendar/shares/direct", new { viewerId, startDate = Day(start), endDate = Day(end) });
}
