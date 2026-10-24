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
using TrustCabinet.Domain.Documents;
using TrustCabinet.Domain.Entities;
using TrustCabinet.Domain.Enums;
using TrustCabinet.Infrastructure;

namespace TrustCabinet.Web.Tests.Agenda;

public sealed class AgendaFixture : IAsyncLifetime
{
    // Bound this fixture's pool so a 100-user load does not consume PostgreSQL's entire server limit.
    internal readonly PostgreSqlTestDatabase Database = new(new Npgsql.NpgsqlConnectionStringBuilder(
        PostgresTestDatabase.ConnectionString("trustcabinet_agenda_tests")) { MaxPoolSize = 20 }.ConnectionString);
    internal readonly TestClock Clock = new();
    internal readonly AgendaGuestTestAdapter Guests = new();
    internal readonly SaveFailureInterceptor SaveFailure = new();
    internal readonly ConcurrentQueue<AgendaChange> Events = new();
    internal bool FailPush;
    internal bool FailRead;
    internal long QueryCount;
    internal Factory Host = null!;
    internal const string SigningKey = "khoa-ky-chi-dung-trong-test-du-32-ky-tu";

    internal TrustCabinetDbContext Db() => new(new DbContextOptionsBuilder<TrustCabinetDbContext>()
        .UseNpgsql(Database.DataSource, o => o.MigrationsHistoryTable("__ef_migrations_history"))
        .UseSnakeCaseNamingConvention().Options);

    public async Task InitializeAsync()
    {
        await using var db = Db();
        await db.Database.MigrateAsync();
        Host = new Factory(this);
        _ = Host.Server;
    }
    public async Task DisposeAsync() { await Host.DisposeAsync(); await Database.DisposeAsync(); }

    internal sealed class Factory(AgendaFixture fixture) : TrustCabinetApiFactory
    {
        protected override void ConfigureWebHost(IWebHostBuilder builder)
        {
            base.ConfigureWebHost(builder);
            builder.ConfigureServices(services =>
            {
                services.RemoveAll<TrustCabinetDbContext>();
                services.RemoveAll<DbContextOptions<TrustCabinetDbContext>>();
                services.AddDbContext<TrustCabinetDbContext>(o => o.UseNpgsql(fixture.Database.DataSource,
                    p => p.MigrationsHistoryTable("__ef_migrations_history")).UseSnakeCaseNamingConvention()
                    .AddInterceptors(fixture.SaveFailure, new ReadFailure(fixture)));
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
    private sealed class Push(AgendaFixture fixture, AgendaChangePublisher inner) : IAgendaChangePublisher
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
    internal sealed class SaveFailureInterceptor : SaveChangesInterceptor
    {
        internal bool Enabled;
        public override ValueTask<InterceptionResult<int>> SavingChangesAsync(DbContextEventData eventData,
            InterceptionResult<int> result, CancellationToken cancellationToken = default)
            => Enabled ? throw new DbUpdateException("Injected persistence failure.") : ValueTask.FromResult(result);
    }
    private sealed class ReadFailure(AgendaFixture fixture) : DbCommandInterceptor
    {
        public override ValueTask<InterceptionResult<System.Data.Common.DbDataReader>> ReaderExecutingAsync(
            System.Data.Common.DbCommand command, CommandEventData eventData,
            InterceptionResult<System.Data.Common.DbDataReader> result, CancellationToken cancellationToken = default)
        {
            Interlocked.Increment(ref fixture.QueryCount);
            return fixture.FailRead && command.CommandText.Contains("agenda_items", StringComparison.Ordinal)
                ? throw new Npgsql.NpgsqlException("Injected read failure.") : ValueTask.FromResult(result);
        }
    }

    internal sealed record Scenario(Guid MeetingId, Guid Secretary, Guid Chair, Guid Member, Guid Organizer, Guid Outsider, Guid[] Items, Guid DocumentId)
    {
        internal string Url => $"/api/meetings/{MeetingId}/agenda";
    }

    internal async Task<Scenario> Seed(MeetingStatus status = MeetingStatus.InProgress, int count = 3)
    {
        await using var db = Db();
        var users = Enumerable.Range(0, 5).Select(i => new User { FullName = $"Agenda test {i}",
            Email = $"{Guid.NewGuid():N}@agenda.test", PasswordHash = "test-only-non-login-hash" }).ToArray();
        db.Users.AddRange(users);
        var meeting = new Meeting { Title = "Agenda test", CreatedById = users[3].Id, Status = status,
            PlannedStartTime = Clock.Now, PlannedEndTime = Clock.Now.AddHours(1) };
        db.Meetings.Add(meeting);
        for (var i = 0; i < 3; i++) db.MeetingParticipants.Add(new() { MeetingId = meeting.Id, UserId = users[i].Id,
            Role = i == 0 ? MeetingRole.Secretary : i == 1 ? MeetingRole.Chair : MeetingRole.Member });
        var items = Enumerable.Range(0, count).Select(i => new AgendaItem { MeetingId = meeting.Id,
            Title = $"Mục {i + 1}", Position = 1 + i * 3 }).ToArray();
        db.AgendaItems.AddRange(items);
        var document = TestDocument(meeting.Id, "Tài liệu nội bộ", users[0].Id);
        db.MeetingDocuments.Add(document);
        await db.SaveChangesAsync();
        return new(meeting.Id, users[0].Id, users[1].Id, users[2].Id, users[3].Id, users[4].Id, items.Select(i => i.Id).ToArray(), document.Id);
    }

    /// <summary>A meeting document row with its stored file; no object exists in any store.</summary>
    internal static MeetingDocument TestDocument(Guid meetingId, string fileName, Guid uploader, Guid? agendaItemId = null)
    {
        var fileId = Guid.CreateVersion7();
        var document = new MeetingDocument { MeetingId = meetingId, FileName = fileName, AddedById = uploader,
            AddedAt = DateTimeOffset.UtcNow, StoredFile = new StoredFile { Id = fileId, StorageKey = DocumentFileRules.StorageKey(fileId),
                OriginalFileName = fileName, ContentType = "application/pdf", ContentSha256 = new string('a', 64), ByteSize = 10,
                UploadedById = uploader, UploadedAt = DateTimeOffset.UtcNow } };
        if (agendaItemId is { } item) document.AgendaItemLinks.Add(new() { AgendaItemId = item });
        return document;
    }

    /// <summary>The agenda item a document is linked to, or null for a general document.</summary>
    internal static Task<Guid?> LinkedItem(TrustCabinetDbContext db, Guid documentId)
        => db.MeetingDocumentAgendaItems.Where(l => l.MeetingDocumentId == documentId)
            .Select(l => (Guid?)l.AgendaItemId).SingleOrDefaultAsync();

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
