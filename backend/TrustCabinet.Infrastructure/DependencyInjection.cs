using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using TrustCabinet.Application.Abstractions;
using TrustCabinet.Infrastructure.Access;
using TrustCabinet.Infrastructure.Agenda;
using TrustCabinet.Infrastructure.Attendance;
using TrustCabinet.Infrastructure.Authentication;
using TrustCabinet.Infrastructure.CalendarSharing;
using TrustCabinet.Infrastructure.Calls;
using TrustCabinet.Infrastructure.Documents;
using TrustCabinet.Infrastructure.Email;
using TrustCabinet.Infrastructure.GuestAccess;
using TrustCabinet.Infrastructure.Meetings;
using TrustCabinet.Infrastructure.Notes;
using TrustCabinet.Infrastructure.Notifications;
using TrustCabinet.Infrastructure.Participants;
using TrustCabinet.Infrastructure.Persistence;
using TrustCabinet.Infrastructure.Recordings;
using TrustCabinet.Infrastructure.Reports;
using TrustCabinet.Infrastructure.Retention;
using TrustCabinet.Infrastructure.Speaking;
using TrustCabinet.Infrastructure.Summaries;
using TrustCabinet.Infrastructure.Transcription;
using TrustCabinet.Infrastructure.Users;
using TrustCabinet.Infrastructure.VoiceId;
using TrustCabinet.Infrastructure.Votes;
using TrustCabinet.Infrastructure.Seed;

namespace TrustCabinet.Infrastructure;

public static class DependencyInjection
{
    /// <summary>
    /// Registers everything this project provides: the database, then each module's repositories, queries, adapters,
    /// options and its own background workers. The order matters where a later call replaces an earlier one (guest
    /// access replaces the fail-closed guest adapters <c>AddApplication</c> registered), so keep it.
    /// </summary>
    /// <param name="contentRootPath">The web host's content root, under which recordings are stored by default.</param>
    public static IServiceCollection AddInfrastructure(
        this IServiceCollection services, IConfiguration configuration, string contentRootPath)
    {
        services.AddTrustCabinetDatabase(configuration);
        services.AddTrustCabinetAuthentication(configuration);
        services.AddTrustCabinetUserManagement();
        services.AddTrustCabinetRolePermissions();
        services.AddTrustCabinetMeetings(configuration);
        services.AddTrustCabinetAgenda();
        services.AddTrustCabinetParticipants();
        services.AddTrustCabinetAttendance();
        services.AddTrustCabinetVotes();
        // SCRUM-38: the meeting report and its Excel export.
        services.AddTrustCabinetReports();
        // SCRUM-116: UC-04 guest access; replaces the fail-closed guest adapters.
        services.AddTrustCabinetGuestAccess(configuration);
        // SCRUM-27: raised hands and the Chair's queue of speakers (UC-50).
        services.AddTrustCabinetSpeaking();
        // SCRUM-30: the Secretary's running notes.
        services.AddTrustCabinetMeetingNotes();
        services.AddTrustCabinetNotifications();
        services.AddTrustCabinetEmailDelivery(configuration);
        // SCRUM-39: meeting call room on LiveKit (ADR-0010). Starts without LiveKit configured; the call endpoints
        // then answer 503.
        services.AddTrustCabinetCalls(configuration);
        // SCRUM-107: meeting documents, bytes in SeaweedFS over the S3 API (ADR-0024, ADR-0084). Starts without a store configured; uploads
        // then answer 503/MSG166.
        services.AddTrustCabinetDocuments(configuration);
        // SCRUM-29: recordings of a sitting, on local disk, erased when their retention period runs out.
        services.AddTrustCabinetRecordings(configuration, contentRootPath);
        // ADR-0067: orphaned audio, drafts of cancelled meetings, old notifications, guest codes and email bodies.
        services.AddTrustCabinetRetention(configuration);
        // SCRUM-142: calendar sharing (UC-80a → UC-80f, UC-81c).
        services.AddTrustCabinetCalendarSharing();
        // SCRUM-31: speech to text on the in-house speech worker (workers/speech), one recording at a time.
        services.AddTrustCabinetTranscription(configuration);
        services.AddTrustCabinetSummaries(configuration);
        // SCRUM-35: Voice ID — consent, encrypted enrolment samples, speaker suggestions in transcripts.
        services.AddTrustCabinetVoiceId(configuration);
        return services;
    }

    /// <summary>
    /// Registers the database. The connection string comes from configuration under
    /// ConnectionStrings:TrustCabinet — from appsettings in development, from user-secrets or an
    /// environment variable for anything with a real password in it.
    /// </summary>
    public static IServiceCollection AddTrustCabinetDatabase(
        this IServiceCollection services, IConfiguration configuration)
    {
        var connectionString = configuration.GetConnectionString("TrustCabinet");

        if (string.IsNullOrWhiteSpace(connectionString))
        {
            throw new InvalidOperationException(
                "No connection string 'TrustCabinet' is configured, so the application cannot "
                + "start. Set one with: dotnet user-secrets set \"ConnectionStrings:TrustCabinet\" "
                + "\"...\" --project backend/TrustCabinet.Web. Setup instructions are in "
                + "docs/02-design/03-co-so-du-lieu.md.");
        }

        services.AddDbContext<TrustCabinetDbContext>(options => options
            // EF names its own bookkeeping table __EFMigrationsHistory, which in PostgreSQL then
            // needs quoting in every query. Renamed so it matches the rest of the schema.
            .UseNpgsql(connectionString, npgsql => npgsql.MigrationsHistoryTable("__ef_migrations_history"))
            // PostgreSQL lowercases any identifier that is not quoted, so without this EF would
            // emit SELECT ... FROM "Meetings" everywhere and hand-written SQL would need the
            // quotes too. snake_case keeps the database idiomatic and the SQL readable.
            .UseSnakeCaseNamingConvention());

        // SCRUM-143: transactions and the translation of storage failures for the use cases in Application. It wraps
        // the same scoped context the repositories of the request use.
        services.AddScoped<IUnitOfWork, EfUnitOfWork>();
        services.AddScoped<IDatabaseProbe, DatabaseProbe>();

        return services;
    }

    /// <summary>
    /// Applies pending migrations and fills an empty database with demo rows.
    ///
    /// For local development only — the caller checks the environment — and for rebuilding the shared
    /// test server from an empty database (<c>--migrate --seed-demo</c>, ADR-0047). A deployment migrates
    /// with <see cref="MigrateForDeploymentAsync"/> as a deliberate, reviewable step, because a web app
    /// that migrates its own database on boot will happily do so from several instances at once.
    /// </summary>
    public static async Task MigrateAndSeedDevelopmentAsync(this IServiceProvider services)
    {
        await using var scope = services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<TrustCabinetDbContext>();

        await db.Database.MigrateAsync();
        await ReferenceDataSeeder.SeedAsync(db);
        await DevelopmentDataSeeder.SeedAsync(db);
        await AgendaDevelopmentDataSeeder.SeedAsync(db);
        await ParticipantDevelopmentDataSeeder.SeedAsync(db);
    }

    /// <summary>
    /// The deliberate migration step of a deployment (ADR-0047, <c>--migrate</c>): applies every pending migration
    /// and the reference data, and returns the last migration now applied.
    ///
    /// With <paramref name="seedDemo"/> it runs <see cref="MigrateAndSeedDevelopmentAsync"/> instead, demo accounts
    /// with the public password included — only for rebuilding the shared test server from an empty database.
    /// </summary>
    public static async Task<string?> MigrateForDeploymentAsync(this IServiceProvider services, bool seedDemo = false)
    {
        if (seedDemo)
        {
            await services.MigrateAndSeedDevelopmentAsync();
        }
        else
        {
            await using var migrate = services.CreateAsyncScope();
            var target = migrate.ServiceProvider.GetRequiredService<TrustCabinetDbContext>();
            await target.Database.MigrateAsync();
            await ReferenceDataSeeder.SeedAsync(target);
        }

        await using var scope = services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<TrustCabinetDbContext>();
        return (await db.Database.GetAppliedMigrationsAsync()).LastOrDefault();
    }

    /// <summary>
    /// Puts the reference data in place if the database has none — the role ↔ capability matrix
    /// (ADR-0007).
    ///
    /// Usable in any environment, unlike <see cref="MigrateAndSeedDevelopmentAsync"/>. A database
    /// migrated from scratch already has the rows, because <c>InitialCreate</c> inserts them and
    /// is immutable; this covers the case where it does not — a restore without that migration's
    /// data, or a schema provisioned some other way — since an empty <c>role_capabilities</c>
    /// means no role holds AdminUsers and nobody can sign in to repair it.
    ///
    /// Safe to run on every boot and from several instances at once: it inserts only into a
    /// completely empty table, and the composite primary key rejects a duplicate insert if two
    /// instances race, which surfaces as a failed start rather than a doubled matrix.
    ///
    /// It does not migrate. A deployment still applies migrations deliberately; this only fills a
    /// table that a migration no longer fills by itself.
    /// </summary>
    public static async Task SeedReferenceDataAsync(this IServiceProvider services)
    {
        await using var scope = services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<TrustCabinetDbContext>();

        await ReferenceDataSeeder.SeedAsync(db);
    }
}
