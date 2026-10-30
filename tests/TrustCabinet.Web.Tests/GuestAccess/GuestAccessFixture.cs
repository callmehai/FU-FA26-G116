using System.Collections.Concurrent;
using System.Net.Http.Json;
using System.Text.Json;
using System.Text.RegularExpressions;
using Microsoft.AspNetCore.Hosting;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Logging;
using TrustCabinet.Domain.Entities;
using TrustCabinet.Domain.Enums;
using TrustCabinet.Infrastructure;
using TrustCabinet.Infrastructure.Email;
using TrustCabinet.Infrastructure.GuestAccess;

namespace TrustCabinet.Web.Tests.GuestAccess;

/// <summary>UC-04 against a real PostgreSQL, the real adapters and a recording mail transport.</summary>
public sealed partial class GuestAccessFixture : IAsyncLifetime
{
    internal readonly PostgreSqlTestDatabase Database = new(new Npgsql.NpgsqlConnectionStringBuilder(
        PostgresTestDatabase.ConnectionString("trustcabinet_guest_access_tests")) { MaxPoolSize = 20 }.ConnectionString);
    internal readonly TestClock Clock = new();
    internal readonly Mailbox Mail = new();
    internal readonly SaveFailureInterceptor SaveFailure = new();
    internal Factory Host = null!;

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

    internal sealed class Factory(GuestAccessFixture fixture) : TrustCabinetApiFactory
    {
        protected override void ConfigureWebHost(IWebHostBuilder builder)
        {
            base.ConfigureWebHost(builder);
            builder.ConfigureLogging(logging => logging.ClearProviders());
            builder.ConfigureServices(services =>
            {
                services.RemoveAll<TrustCabinetDbContext>();
                services.RemoveAll<DbContextOptions<TrustCabinetDbContext>>();
                services.AddDbContext<TrustCabinetDbContext>(o => o.UseNpgsql(fixture.Database.DataSource,
                    p => p.MigrationsHistoryTable("__ef_migrations_history")).UseSnakeCaseNamingConvention()
                    .AddInterceptors(fixture.SaveFailure));
                services.RemoveAll<TimeProvider>();
                services.AddSingleton<TimeProvider>(fixture.Clock);
                services.RemoveAll<IEmailSender>();
                services.AddSingleton<IEmailSender>(fixture.Mail);
                // Switched on after binding, so the queue worker is never registered.
                services.PostConfigure<EmailDeliveryOptions>(o =>
                {
                    o.ApplicationBaseUrl = "https://meetings.test";
                    o.Smtp.Enabled = true;
                    o.Smtp.Host = "smtp.meetings.test";
                    o.Smtp.FromAddress = "no-reply@meetings.test";
                });
            });
        }
    }

    internal sealed class TestClock : TimeProvider
    {
        // Real time, so guest JWTs (validated against the real clock) are live; tests only move it forward.
        // Whole seconds, because PostgreSQL keeps microseconds and the tests compare stored times exactly.
        internal DateTimeOffset Now = DateTimeOffset.FromUnixTimeSeconds(DateTimeOffset.UtcNow.ToUnixTimeSeconds());
        public override DateTimeOffset GetUtcNow() => Now;
    }

    internal sealed class Mailbox : IEmailSender
    {
        internal readonly ConcurrentQueue<EmailMessage> Sent = new();
        internal bool Fail;
        public Task<string?> SendAsync(EmailMessage message, CancellationToken ct = default)
        {
            if (Fail) throw new IOException("Injected mail outage.");
            Sent.Enqueue(message);
            return Task.FromResult<string?>(null);
        }
        internal string LastCode(string to) => SixDigits().Match(Sent.Last(m => m.ToAddress == to).Body).Value;
    }

    internal sealed class SaveFailureInterceptor : SaveChangesInterceptor
    {
        internal bool Enabled;
        public override ValueTask<InterceptionResult<int>> SavingChangesAsync(DbContextEventData eventData,
            InterceptionResult<int> result, CancellationToken cancellationToken = default)
            => Enabled ? throw new DbUpdateException("Injected persistence failure.") : ValueTask.FromResult(result);
    }

    internal sealed record Scenario(Guid MeetingId, Guid InvitationId, Guid SecretaryId, string Email, string LinkToken)
    {
        internal string Url => $"/api/guest-access/invitations/{InvitationId}";
    }

    internal async Task<Scenario> Seed(MeetingStatus status = MeetingStatus.Published)
    {
        await using var db = Db();
        var secretary = new User { FullName = "Thư ký", Email = $"{Guid.CreateVersion7():N}@internal.test", PasswordHash = "test-only" };
        var meeting = new Meeting { Title = "Họp có Khách", CreatedById = secretary.Id, Status = status,
            PlannedStartTime = Clock.Now, PlannedEndTime = Clock.Now.AddHours(2) };
        var invitation = new MeetingGuestInvitation { MeetingId = meeting.Id, InvitedName = "Khách mời",
            Email = $"{Guid.CreateVersion7():N}@guests.test", InvitedById = secretary.Id };
        db.Users.Add(secretary);
        db.Meetings.Add(meeting);
        db.MeetingParticipants.Add(new MeetingParticipant { MeetingId = meeting.Id, UserId = secretary.Id, Role = MeetingRole.Secretary });
        db.MeetingGuestInvitations.Add(invitation);
        await db.SaveChangesAsync();
        return new(meeting.Id, invitation.Id, secretary.Id, invitation.Email,
            Host.Services.GetRequiredService<GuestCredentials>().LinkToken(invitation.Id));
    }

    /// <summary>Sends a code and returns it as the guest would read it from the email.</summary>
    internal async Task<string> SendCode(HttpClient client, Scenario s)
    {
        (await client.PostAsJsonAsync(s.Url + "/code", new { token = s.LinkToken })).EnsureSuccessStatusCode();
        return Mail.LastCode(s.Email);
    }

    internal static async Task<JsonElement> Expect(HttpResponseMessage response, int status, string? code = null)
    {
        var text = await response.Content.ReadAsStringAsync();
        Assert.True((int)response.StatusCode == status, $"Expected {status}/{code}, got {(int)response.StatusCode}: {text}");
        if (code is null) return text.Length == 0 ? default : JsonDocument.Parse(text).RootElement.Clone();
        var body = JsonDocument.Parse(text).RootElement.Clone();
        Assert.Equal(code, body.GetProperty("code").GetString());
        return body;
    }

    [GeneratedRegex(@"\b\d{6}\b")]
    private static partial Regex SixDigits();
}
