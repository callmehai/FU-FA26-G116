using System.Collections.Concurrent;
using System.IdentityModel.Tokens.Jwt;
using System.Net.Http.Headers;
using System.Security.Claims;
using System.Text;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http.Connections;
using Microsoft.AspNetCore.SignalR.Client;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.IdentityModel.Tokens;
using TrustCabinet.Domain.Agenda;
using TrustCabinet.Domain.Entities;
using TrustCabinet.Domain.Enums;
using TrustCabinet.Infrastructure;

using TrustCabinet.Web.Tests.Agenda;
using TrustCabinet.Domain.Calls;
using TrustCabinet.Domain.Meetings;
using System.Data.Common;

namespace TrustCabinet.Web.Tests.Meetings;

public sealed class MeetingLifecycleFixture : IAsyncLifetime
{
    // Bound this fixture's pool so a 100-user load does not consume PostgreSQL's entire server limit.
    internal readonly PostgreSqlTestDatabase Database = new(new Npgsql.NpgsqlConnectionStringBuilder(
        PostgresTestDatabase.ConnectionString("trustcabinet_lifecycle_tests")) { MaxPoolSize = 20 }.ConnectionString);
    internal readonly TestClock Clock = new();
    internal readonly AgendaGuestTestAdapter Guests = new();
    internal SaveFailureInterceptor SaveFailure = null!;
    internal bool FailCommit;
    internal bool ObserveAccountLock;
    internal Action? OnCommitted;
    internal bool FailWorkflowRead;
    internal bool FailGate;
    internal bool FailNotifications;
    internal bool FailCalls;
    internal readonly ConcurrentQueue<string> Queries = new();
    internal readonly ConcurrentQueue<string> ClosedRooms = new();
    internal readonly ConcurrentDictionary<string, byte> ProviderRooms = new();
    internal bool ProviderUnavailable;
    internal bool ProviderConfigured = true;
    internal ILiveKitGateway? GatewayOverride;
    internal Func<Guid, CancellationToken, Task>? BeforeRoomClosed;
    internal TaskCompletionSource? LockAttempt;
    internal int ExpectedLockAttempts = 1;
    private int lockAttempts;
    internal void ObserveLocks(int count = 1)
    {
        lockAttempts = 0;
        ExpectedLockAttempts = count;
        LockAttempt = new(TaskCreationOptions.RunContinuationsAsynchronously);
    }
    internal readonly ConcurrentQueue<AgendaChange> Events = new();
    internal bool FailPush;
    internal long QueryCount;
    internal Factory Host = null!;
    internal const string SigningKey = "khoa-ky-chi-dung-trong-test-du-32-ky-tu";

    internal TrustCabinetDbContext Db() => new(new DbContextOptionsBuilder<TrustCabinetDbContext>()
        .UseNpgsql(Database.DataSource, o => o.MigrationsHistoryTable("__ef_migrations_history"))
        .UseSnakeCaseNamingConvention().Options);

    public async Task InitializeAsync()
    {
        SaveFailure = new SaveFailureInterceptor(this);
        await using var db = Db();
        await db.Database.MigrateAsync();
        Host = new Factory(this);
        _ = Host.Server;
    }
    public async Task DisposeAsync() { await Host.DisposeAsync(); await Database.DisposeAsync(); }

    internal sealed class Factory(MeetingLifecycleFixture fixture) : TrustCabinetApiFactory
    {
        protected override void ConfigureWebHost(IWebHostBuilder builder)
        {
            base.ConfigureWebHost(builder);
            if (fixture.GatewayOverride is not null) builder.UseSetting("Logging:LogLevel:Default", "Warning");
            builder.ConfigureServices(services =>
            {
                services.RemoveAll<TrustCabinetDbContext>();
                services.RemoveAll<DbContextOptions<TrustCabinetDbContext>>();
                services.AddDbContext<TrustCabinetDbContext>(o => o.UseNpgsql(fixture.Database.DataSource,
                    p => p.MigrationsHistoryTable("__ef_migrations_history")).UseSnakeCaseNamingConvention()
                    .AddInterceptors(fixture.SaveFailure, new ReadFailure(fixture), new Failure(fixture)));
                services.RemoveAll<ILiveKitGateway>();
                services.AddSingleton<ILiveKitGateway>(fixture.GatewayOverride ?? new Gateway(fixture));
                services.RemoveAll<TimeProvider>();
                services.AddSingleton<TimeProvider>(fixture.Clock);
                fixture.Guests.Clock = fixture.Clock;
                services.RemoveAll<IAgendaGuestAccess>();
                services.AddSingleton<IAgendaGuestAccess>(fixture.Guests);
                services.RemoveAll<IMeetingGuestIdentityResolver>();
                services.AddSingleton<IMeetingGuestIdentityResolver>(fixture.Guests);
                services.RemoveAll<IAgendaChangePublisher>();
                services.AddScoped<AgendaChangePublisher>();
                services.AddScoped<IAgendaChangePublisher>(p => new Push(fixture, p.GetRequiredService<AgendaChangePublisher>()));
            });
        }
    }
    private sealed class Push(MeetingLifecycleFixture fixture, AgendaChangePublisher inner) : IAgendaChangePublisher
    {
        public async Task PublishAsync(AgendaChange change, CancellationToken ct)
        {
            if (fixture.FailPush) throw new IOException("Injected push outage.");
            fixture.Events.Enqueue(change);
            await inner.PublishAsync(change, ct);
        }
    }
    internal sealed class TestClock : TimeProvider
    {
        internal DateTimeOffset Now = new(2026, 10, 12, 8, 0, 0, TimeSpan.Zero);
        public override DateTimeOffset GetUtcNow() => Now;
    }
    internal sealed class SaveFailureInterceptor(MeetingLifecycleFixture fixture) : SaveChangesInterceptor
    {
        internal bool Enabled;
        public override ValueTask<InterceptionResult<int>> SavingChangesAsync(DbContextEventData eventData,
            InterceptionResult<int> result, CancellationToken cancellationToken = default)
            {
            var db = eventData.Context!;
            if (Enabled || (fixture.FailNotifications && db.ChangeTracker.Entries<Notification>().Any(e => e.State == EntityState.Added))
                || (fixture.FailCalls && db.ChangeTracker.Entries<CallSession>().Any(e => e.State == EntityState.Modified)))
                throw new DbUpdateException("Injected persistence failure.");
            return ValueTask.FromResult(result);
        }
    }
    private sealed class ReadFailure(MeetingLifecycleFixture fixture) : DbCommandInterceptor
    {
        public override ValueTask<InterceptionResult<System.Data.Common.DbDataReader>> ReaderExecutingAsync(
            System.Data.Common.DbCommand command, CommandEventData eventData,
            InterceptionResult<System.Data.Common.DbDataReader> result, CancellationToken cancellationToken = default)
        {
            Interlocked.Increment(ref fixture.QueryCount);
            fixture.Queries.Enqueue(command.CommandText);
            if (command.CommandText.Contains(fixture.ObserveAccountLock ? "FOR SHARE" : "FOR UPDATE", StringComparison.Ordinal))
            {
                if (Interlocked.Increment(ref fixture.lockAttempts) >= fixture.ExpectedLockAttempts)
                    fixture.LockAttempt?.TrySetResult();
                if (fixture.FailWorkflowRead) throw new Npgsql.NpgsqlException("Injected lifecycle read failure.");
            }
            if (fixture.FailGate && command.CommandText.Contains("users", StringComparison.Ordinal))
                throw new Npgsql.NpgsqlException("Injected active-user read failure.");
            return ValueTask.FromResult(result);
        }
    }

    private sealed class Failure(MeetingLifecycleFixture fixture) : DbTransactionInterceptor
    {
        public override Task TransactionCommittedAsync(DbTransaction transaction, TransactionEndEventData eventData, CancellationToken cancellationToken = default)
        {
            fixture.OnCommitted?.Invoke();
            return Task.CompletedTask;
        }
        public override ValueTask<InterceptionResult> TransactionCommittingAsync(DbTransaction transaction,
            TransactionEventData eventData, InterceptionResult result, CancellationToken cancellationToken = default)
            => fixture.FailCommit ? throw new Npgsql.NpgsqlException("Injected failure after save, before commit.") : ValueTask.FromResult(result);
    }
    internal sealed class Gateway(MeetingLifecycleFixture fixture) : ILiveKitGateway
    {
        public bool IsConfigured => fixture.ProviderConfigured;
        public CallJoinTicket CreateJoinTicket(string room, Guid user, string name, string metadata, TimeSpan ttl)
            => new("ws://test", "test-token", room, user.ToString(), DateTimeOffset.UtcNow.Add(ttl));
        public Task<IReadOnlyList<RoomParticipant>> ListParticipantsAsync(string room, CancellationToken ct)
            => Task.FromResult<IReadOnlyList<RoomParticipant>>([]);
        public Task<IReadOnlyList<string>> ListRoomNamesAsync(CancellationToken ct)
            => Task.FromResult<IReadOnlyList<string>>(fixture.ProviderRooms.Keys.ToArray());
        public Task MuteTrackAsync(string room, string identity, string track, CancellationToken ct) => Task.CompletedTask;
        public Task RemoveParticipantAsync(string room, string identity, CancellationToken ct) => Task.CompletedTask;
        public CallWebhookEvent? ReadWebhook(byte[] body, string? authorization) => null;
        public async Task DeleteRoomAsync(string room, CancellationToken ct)
        {
            Assert.False(ct.IsCancellationRequested);
            Assert.True(CallRoomNames.TryParseMeeting(room, out var id));
            await using var db = fixture.Db();
            Assert.Equal(MeetingStatus.Ended, (await db.Meetings.SingleAsync(m => m.Id == id, ct)).Status);
            if (fixture.BeforeRoomClosed is { } callback) await callback(id, ct);
            fixture.ClosedRooms.Enqueue(room);
            if (fixture.ProviderUnavailable) throw new LiveKitUnavailableException("Injected provider outage.");
            fixture.ProviderRooms.TryRemove(room, out _);
        }
    }

    internal sealed record Scenario(Guid MeetingId, Guid Secretary, Guid Chair, Guid Member, Guid Organizer, Guid Outsider, Guid[] Items)
    {
        internal string Url => $"/api/meetings/{MeetingId}/agenda";
    }

    internal async Task<Scenario> Seed(MeetingStatus status = MeetingStatus.Published, int count = 3, MeetingMode mode = MeetingMode.Online)
    {
        await using var db = Db();
        var users = Enumerable.Range(0, 5).Select(i => new User { FullName = $"Agenda test {i}",
            Email = $"{Guid.NewGuid():N}@agenda.test", PasswordHash = "test-only-non-login-hash" }).ToArray();
        db.Users.AddRange(users);
        var meeting = new Meeting { Title = "Agenda test", CreatedById = users[3].Id, Status = status, Mode = mode,
            ActualStartedAt = status == MeetingStatus.InProgress ? Clock.Now.AddMinutes(-30) : null,
            PlannedStartTime = Clock.Now, PlannedEndTime = Clock.Now.AddHours(1) };
        db.Meetings.Add(meeting);
        for (var i = 0; i < 3; i++) db.MeetingParticipants.Add(new() { MeetingId = meeting.Id, UserId = users[i].Id,
            Role = i == 0 ? MeetingRole.Secretary : i == 1 ? MeetingRole.Chair : MeetingRole.Member });
        var items = Enumerable.Range(0, count).Select(i => new AgendaItem { MeetingId = meeting.Id,
            Title = $"Mục {i + 1}", Position = 1 + i * 3 }).ToArray();
        db.AgendaItems.AddRange(items);
        await db.SaveChangesAsync();
        return new(meeting.Id, users[0].Id, users[1].Id, users[2].Id, users[3].Id, users[4].Id, items.Select(i => i.Id).ToArray());
    }

    internal static string Token(Guid actor, DateTimeOffset? expires = null) => new JwtSecurityTokenHandler().WriteToken(
        new JwtSecurityToken("TrustCabinet", "TrustCabinet", [new Claim("sub", actor.ToString())],
            expires: (expires ?? DateTimeOffset.UtcNow.AddMinutes(10)).UtcDateTime,
            signingCredentials: new SigningCredentials(new SymmetricSecurityKey(Encoding.UTF8.GetBytes(SigningKey)), SecurityAlgorithms.HmacSha256)));

    internal HttpClient Client(Guid? actor = null)
    {
        var client = Host.CreateClient();
        if (actor is { } id) client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", Token(id));
        return client;
    }

    internal HubConnection Hub(Guid actor, DateTimeOffset? expires = null) => new HubConnectionBuilder()
        .WithUrl("http://localhost/hubs/meetings", options =>
        {
            options.Transports = HttpTransportType.LongPolling;
            options.HttpMessageHandlerFactory = _ => Host.Server.CreateHandler();
            options.AccessTokenProvider = () => Task.FromResult<string?>(Token(actor, expires));
        }).Build();
}
