using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using TrustCabinet.Domain.Entities;
using TrustCabinet.Domain.Enums;
using TrustCabinet.Infrastructure;
using TrustCabinet.Infrastructure.Meetings;
using TrustCabinet.Infrastructure.Seed;

namespace TrustCabinet.Web.Tests.Meetings;

public sealed class MeetingSessionRepositoryTests : IAsyncLifetime
{
    private static readonly DateTimeOffset PlannedStart = new(2026, 9, 1, 2, 0, 0, TimeSpan.Zero);
    private readonly PostgreSqlTestDatabase _database = new(
        PostgresTestDatabase.ConnectionString("trustcabinet_meeting_session_tests"));
    private TrustCabinetDbContext _db = null!;

    public async Task InitializeAsync()
    {
        _db = new TrustCabinetDbContext(new DbContextOptionsBuilder<TrustCabinetDbContext>()
            .UseNpgsql(_database.DataSource, options => options.MigrationsHistoryTable("__ef_migrations_history"))
            .UseSnakeCaseNamingConvention().Options);
        await _db.Database.MigrateAsync();
        await ReferenceDataSeeder.SeedAsync(_db);
    }

    public async Task DisposeAsync()
    {
        await _db.DisposeAsync();
        await _database.DisposeAsync();
    }

    [Fact]
    public async Task Session_migration_preserves_existing_meeting_history_and_email_without_backfilling_sessions()
    {
        const string beforeSessions = "20261001153906_MeetingWorkflowReviewCorrections";
        await _db.GetService<IMigrator>().MigrateAsync(beforeSessions);
        var meeting = await AddMeetingAsync("Meeting retained across schema upgrade");
        var history = new MeetingStatusHistory
        {
            MeetingId = meeting.Id,
            ActorId = meeting.CreatedById,
            FromStatus = MeetingStatus.WaitingApproval,
            ToStatus = MeetingStatus.Approved,
            Action = MeetingHistoryAction.Approve,
            OccurredAt = PlannedStart,
        };
        var email = new EmailMessage
        {
            MeetingId = meeting.Id,
            DeduplicationKey = "session-migration:retained-email",
            ToAddress = "retained-email@example.vn",
            Subject = "Persisted invitation",
            Body = "Persisted content",
            Status = EmailStatus.Queued,
        };
        _db.MeetingStatusHistory.Add(history);
        _db.EmailMessages.Add(email);
        await _db.SaveChangesAsync();
        _db.ChangeTracker.Clear();

        await _db.GetService<IMigrator>().MigrateAsync();

        Assert.Empty(await _db.MeetingSessions.ToListAsync());
        Assert.Equal(meeting.Title, (await _db.Meetings.SingleAsync()).Title);
        Assert.Equal(history.Id, (await _db.MeetingStatusHistory.SingleAsync()).Id);
        var preservedEmail = await _db.EmailMessages.SingleAsync();
        Assert.Equal(email.Id, preservedEmail.Id);
        Assert.Equal(email.DeduplicationKey, preservedEmail.DeduplicationKey);
        Assert.Equal(email.Body, preservedEmail.Body);
        _db.MeetingSessions.Add(NewSession(meeting.Id));
        await _db.SaveChangesAsync();
        _db.ChangeTracker.Clear();
        await _db.GetService<IMigrator>().MigrateAsync(beforeSessions);

        Assert.False(await _db.Database.SqlQuery<bool>($"""
            SELECT EXISTS (SELECT 1 FROM information_schema.tables
                WHERE table_schema = 'public' AND table_name = 'meeting_sessions') AS "Value"
            """).SingleAsync());
        Assert.Equal(meeting.Id, (await _db.Meetings.SingleAsync()).Id);
        Assert.Equal(history.Id, (await _db.MeetingStatusHistory.SingleAsync()).Id);
        Assert.Equal(email.Id, (await _db.EmailMessages.SingleAsync()).Id);
    }

    private async Task<Meeting> AddMeetingAsync(string title)
    {
        var organizer = new User
        {
            FullName = "Session organizer",
            Email = $"session-organizer-{Guid.CreateVersion7():N}@example.vn",
            PasswordHash = "test-only-placeholder",
        };
        var meeting = new Meeting
        {
            Title = title,
            Status = MeetingStatus.Approved,
            StartsAt = PlannedStart.AddDays(10),
            CreatedBy = organizer,
            CreatedById = organizer.Id,
        };
        _db.Meetings.Add(meeting);
        await _db.SaveChangesAsync();
        return meeting;
    }

    private static MeetingSession NewSession(Guid meetingId) => new()
    {
        MeetingId = meetingId,
        Title = "Valid planned session",
        PlannedStartsAt = PlannedStart,
        PlannedEndsAt = PlannedStart.AddHours(1),
    };
}
