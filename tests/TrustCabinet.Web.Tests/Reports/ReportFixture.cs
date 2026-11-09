using System.IdentityModel.Tokens.Jwt;
using System.Net.Http.Headers;
using System.Security.Claims;
using System.Text;
using Microsoft.AspNetCore.Hosting;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Logging;
using Microsoft.IdentityModel.Tokens;
using TrustCabinet.Domain.Entities;
using TrustCabinet.Domain.Enums;
using TrustCabinet.Infrastructure;

namespace TrustCabinet.Web.Tests.Reports;

/// <summary>
/// The meeting report (SCRUM-38) against a real PostgreSQL. Every test seeds its own users, and the report counts only
/// what its viewer may read, so tests do not see each other's meetings.
/// </summary>
public sealed class ReportFixture : IAsyncLifetime
{
    internal readonly PostgreSqlTestDatabase Database = new(PostgresTestDatabase.ConnectionString("trustcabinet_report_tests"));
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

    internal sealed class Factory(ReportFixture fixture) : TrustCabinetApiFactory
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
            });
        }
    }

    internal sealed class TestClock : TimeProvider
    {
        /// <summary>04/11/2026 09:00 in Vietnam.</summary>
        internal DateTimeOffset Now = new(2026, 11, 4, 2, 0, 0, TimeSpan.Zero);
        public override DateTimeOffset GetUtcNow() => Now;
    }

    /// <summary>A database outage on reads of the meetings table, or on any save (the audit row).</summary>
    internal sealed class FailureInterceptor : DbCommandInterceptor, ISaveChangesInterceptor
    {
        internal bool FailMeetingsRead;
        internal bool FailSave;

        public override ValueTask<InterceptionResult<System.Data.Common.DbDataReader>> ReaderExecutingAsync(
            System.Data.Common.DbCommand command, CommandEventData eventData,
            InterceptionResult<System.Data.Common.DbDataReader> result, CancellationToken cancellationToken = default)
            => FailMeetingsRead && command.CommandText.Contains("FROM meetings", StringComparison.Ordinal)
                ? throw new Npgsql.NpgsqlException("Injected read failure.")
                : ValueTask.FromResult(result);

        public ValueTask<InterceptionResult<int>> SavingChangesAsync(DbContextEventData eventData,
            InterceptionResult<int> result, CancellationToken cancellationToken = default)
            => FailSave ? throw new DbUpdateException("Injected persistence failure.") : ValueTask.FromResult(result);
    }

    internal async Task<Guid[]> Users(int count, bool active = true)
    {
        await using var db = Db();
        var users = Enumerable.Range(0, count).Select(i => new User { FullName = $"Người báo cáo {i}",
            Email = $"{Guid.NewGuid():N}@report.test", PasswordHash = "test-only-non-login-hash", IsActive = active }).ToArray();
        db.Users.AddRange(users);
        await db.SaveChangesAsync();
        return users.Select(u => u.Id).ToArray();
    }

    /// <summary>A meeting created by <paramref name="organizer"/>, who may always read it.</summary>
    internal async Task<Guid> Meeting(Guid organizer, DateTimeOffset start, MeetingStatus status = MeetingStatus.Draft,
        Sensitivity sensitivity = Sensitivity.Normal, DateTimeOffset? endedAt = null, string title = "Cuộc họp trong báo cáo")
    {
        await using var db = Db();
        var meeting = new Meeting { Title = title, CreatedById = organizer, Status = status, Sensitivity = sensitivity,
            Mode = MeetingMode.InPerson, Location = "Phòng họp số 1", PlannedStartTime = start, PlannedEndTime = start.AddHours(1),
            ActualStartedAt = status is MeetingStatus.InProgress or MeetingStatus.Ended ? start : null,
            ActualEndedAt = endedAt,
            CancelledFromStatus = status == MeetingStatus.Cancelled ? MeetingStatus.Approved : null };
        db.Meetings.Add(meeting);
        await db.SaveChangesAsync();
        return meeting.Id;
    }

    internal HttpClient Client(Guid? actor = null)
    {
        var client = Host.CreateClient();
        if (actor is { } id) client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", Token(id));
        return client;
    }

    private static string Token(Guid actor) => new JwtSecurityTokenHandler().WriteToken(
        new JwtSecurityToken("TrustCabinet", "TrustCabinet", [new Claim("sub", actor.ToString())],
            expires: DateTime.UtcNow.AddMinutes(10),
            signingCredentials: new SigningCredentials(new SymmetricSecurityKey(Encoding.UTF8.GetBytes(SigningKey)),
                SecurityAlgorithms.HmacSha256)));
}
