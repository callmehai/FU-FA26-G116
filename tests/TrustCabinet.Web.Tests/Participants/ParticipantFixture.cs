using System.Collections.Concurrent;
using System.IdentityModel.Tokens.Jwt;
using System.Net.Http.Headers;
using System.Security.Claims;
using System.Text;
using System.Data.Common;
using Microsoft.AspNetCore.Hosting;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Logging;
using Microsoft.IdentityModel.Tokens;
using TrustCabinet.Domain.Entities;
using TrustCabinet.Domain.Enums;
using TrustCabinet.Domain.Participants;
using TrustCabinet.Infrastructure;
using TrustCabinet.Web;

namespace TrustCabinet.Web.Tests.Participants;

public sealed class ParticipantFixture : IAsyncLifetime
{
    internal readonly PostgreSqlTestDatabase Database = new(new Npgsql.NpgsqlConnectionStringBuilder(
        PostgresTestDatabase.ConnectionString("trustcabinet_participants_tests"))
    { MaxPoolSize = 20 }.ConnectionString);
    internal readonly TestClock Clock = new();
    internal readonly GuestAdapter Guests = new();
    internal readonly ChangePublisher Changes = new();
    internal readonly FailureInterceptor Failure = new();
    internal readonly QueryCounter Queries = new();
    internal Factory Host = null!;
    internal TrustCabinetDbContext Db() => new(new DbContextOptionsBuilder<TrustCabinetDbContext>().UseNpgsql(Database.DataSource,
        o => o.MigrationsHistoryTable("__ef_migrations_history")).UseSnakeCaseNamingConvention().Options);
    public async Task InitializeAsync() { await using var db = Db(); await db.Database.MigrateAsync(); Guests.Clock = Clock; Host = new(this); _ = Host.Server; }
    public async Task DisposeAsync() { await Host.DisposeAsync(); await Database.DisposeAsync(); }
    internal sealed class Factory(ParticipantFixture f) : TrustCabinetApiFactory
    {
        protected override void ConfigureWebHost(IWebHostBuilder builder)
        {
            base.ConfigureWebHost(builder);
            builder.UseSetting("Email:Smtp:Enabled", "false");
            builder.ConfigureLogging(logging => logging.ClearProviders());
            builder.ConfigureServices(s =>
            {
                s.RemoveAll<TrustCabinetDbContext>(); s.RemoveAll<DbContextOptions<TrustCabinetDbContext>>();
                s.AddDbContext<TrustCabinetDbContext>(o => o.UseNpgsql(f.Database.DataSource, p => p.MigrationsHistoryTable("__ef_migrations_history"))
                    .UseSnakeCaseNamingConvention().AddInterceptors(f.Failure, f.Queries));
                s.RemoveAll<TimeProvider>(); s.AddSingleton<TimeProvider>(f.Clock);
                s.Replace(ServiceDescriptor.Singleton<IParticipantsChangePublisher>(f.Changes));
                s.Replace(ServiceDescriptor.Singleton<IParticipantGuestAccess>(f.Guests));
                s.Replace(ServiceDescriptor.Singleton<IParticipantGuestIdentityResolver>(f.Guests));
            });
        }
    }
    internal sealed class ChangePublisher : IParticipantsChangePublisher
    {
        internal readonly ConcurrentQueue<(ParticipantsChange Change, Guid[] Users, ParticipantGuestSession[] Guests)> Events = new();
        internal Guid? FailMeeting;
        internal IParticipantsChangePublisher? Transport;
        public Task PublishAsync(ParticipantsChange change, IReadOnlyCollection<Guid> users,
            IReadOnlyCollection<ParticipantGuestSession> guests, CancellationToken ct)
        {
            if (FailMeeting == change.MeetingId) throw new InvalidOperationException("Injected transport failure");
            Events.Enqueue((change, users.ToArray(), guests.ToArray()));
            return Transport?.PublishAsync(change, users, guests, ct) ?? Task.CompletedTask;
        }
    }
    internal sealed class TestClock : TimeProvider
    {
        public DateTimeOffset Now = new(2026, 10, 14, 9, 0, 0, TimeSpan.Zero);
        public override DateTimeOffset GetUtcNow() => Now;
    }
    internal sealed class QueryCounter : DbCommandInterceptor
    {
        internal int Count;
        public override ValueTask<InterceptionResult<DbDataReader>> ReaderExecutingAsync(DbCommand command,
            CommandEventData eventData, InterceptionResult<DbDataReader> result, CancellationToken ct = default)
        { Interlocked.Increment(ref Count); return ValueTask.FromResult(result); }
    }
    internal sealed class FailureInterceptor : SaveChangesInterceptor
    {
        internal Guid? MeetingId;
        internal int FailOnSave = 1;
        internal int Calls;
        public override ValueTask<InterceptionResult<int>> SavingChangesAsync(DbContextEventData e, InterceptionResult<int> result, CancellationToken ct = default)
        {
            if (MeetingId is { } id && (e.Context!.ChangeTracker.Entries<Meeting>().Any(m => m.Entity.Id == id)
                || e.Context.ChangeTracker.Entries<MeetingInvitationDispatch>().Any(d => d.Entity.MeetingId == id))
                && Interlocked.Increment(ref Calls) == FailOnSave) throw new DbUpdateException("Injected participant persistence failure.");
            return ValueTask.FromResult(result);
        }
        internal void Reset() { MeetingId = null; Calls = 0; FailOnSave = 1; }
    }
    internal sealed record Session(ParticipantGuestSession Identity, DateTimeOffset ExpiresAt, bool Verified = true, bool Revoked = false);
    internal sealed class GuestAdapter : IParticipantGuestAccess, IParticipantGuestIdentityResolver
    {
        internal TestClock Clock = null!;
        internal readonly ConcurrentDictionary<Guid, Session> Sessions = new();
        internal readonly ConcurrentDictionary<Guid, GuestDisplayProfile> Profiles = new();
        internal bool FailProfiles;
        public ParticipantGuestSession? Resolve(ClaimsPrincipal p)
        {
            if (!p.HasClaim(c => c.Type == "test_guest")) return null;
            Guid.TryParse(p.FindFirstValue("test_meeting"), out var m);
            Guid.TryParse(p.FindFirstValue("test_invitation"), out var i);
            Guid.TryParse(p.FindFirstValue("test_session"), out var s);
            return new(m, i, s);
        }
        public Task<IReadOnlyList<ParticipantGuestSession>> ListReadersAsync(Guid meetingId, CancellationToken ct)
            => Task.FromResult<IReadOnlyList<ParticipantGuestSession>>(Sessions.Values
                .Where(s => s.Identity.MeetingId == meetingId && s.Verified && !s.Revoked && s.ExpiresAt > Clock.Now)
                .Select(s => s.Identity).ToArray());
        public Task<bool> CanAccessAsync(ParticipantGuestSession identity, CancellationToken ct) => Task.FromResult(
            Sessions.TryGetValue(identity.SessionId, out var s) && s.Identity == identity && s.Verified && !s.Revoked && s.ExpiresAt > Clock.Now);
        public Task<IReadOnlyDictionary<Guid, GuestDisplayProfile>> GetProfilesAsync(Guid meetingId, IReadOnlyCollection<Guid> ids, CancellationToken ct)
            => FailProfiles ? throw new TimeoutException("Injected profile read failure.")
                : Task.FromResult<IReadOnlyDictionary<Guid, GuestDisplayProfile>>(Profiles.Where(p => ids.Contains(p.Key)).ToDictionary());
    }
    internal sealed record Scenario(Guid MeetingId, Guid Organizer, Guid Secretary, Guid Chair, Guid Member, Guid Outsider,
        Guid SecretaryAssignment, Guid ChairAssignment, Guid MemberAssignment)
    {
        public string Url => $"/api/meetings/{MeetingId}";
    }
    internal async Task<Scenario> Seed(MeetingStatus status = MeetingStatus.Published)
    {
        await using var db = Db();
        var users = Enumerable.Range(0, 5).Select(i => new User { FullName = $"SCRUM20 User {i}", Email = $"{Guid.CreateVersion7():N}@participants.test", PasswordHash = "test-only" }).ToArray();
        db.Users.AddRange(users);
        var meeting = new Meeting
        {
            Title = "SCRUM20 fixture",
            CreatedById = users[0].Id,
            Status = status,
            PlannedStartTime = Clock.Now,
            PlannedEndTime = Clock.Now.AddHours(1)
        };
        db.Meetings.Add(meeting);
        var participants = new[] { new MeetingParticipant { MeetingId = meeting.Id, UserId = users[1].Id, Role = MeetingRole.Secretary },
            new MeetingParticipant { MeetingId = meeting.Id, UserId = users[2].Id, Role = MeetingRole.Chair },
            new MeetingParticipant { MeetingId = meeting.Id, UserId = users[3].Id, Role = MeetingRole.Member } };
        db.MeetingParticipants.AddRange(participants); await db.SaveChangesAsync();
        return new(meeting.Id, users[0].Id, users[1].Id, users[2].Id, users[3].Id, users[4].Id, participants[0].Id, participants[1].Id, participants[2].Id);
    }
    internal HttpClient Client(Guid? id = null)
    {
        var client = Host.CreateClient();
        if (id is not null) client.DefaultRequestHeaders.Authorization = new("Bearer", Token([new("sub", id.ToString()!)]));
        return client;
    }
    internal HttpClient Guest(Guid meeting, Guid invitation, bool verified = true, bool expired = false, Guid? user = null)
    {
        var identity = new ParticipantGuestSession(meeting, invitation, Guid.CreateVersion7());
        Guests.Sessions[identity.SessionId] = new(identity, Clock.Now.AddHours(expired ? -1 : 1), verified);
        var client = Host.CreateClient();
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", Token([
            new("sub", user?.ToString() ?? "guest:" + invitation), new("test_guest", "true"), new("test_meeting", meeting.ToString()),
            new("test_invitation", invitation.ToString()), new("test_session", identity.SessionId.ToString())]));
        return client;
    }
    private static string Token(Claim[] claims) => new JwtSecurityTokenHandler().WriteToken(new JwtSecurityToken("TrustCabinet", "TrustCabinet", claims,
        expires: DateTime.UtcNow.AddMinutes(30), signingCredentials: new SigningCredentials(
            new SymmetricSecurityKey(Encoding.UTF8.GetBytes("khoa-ky-chi-dung-trong-test-du-32-ky-tu")), SecurityAlgorithms.HmacSha256)));
}
