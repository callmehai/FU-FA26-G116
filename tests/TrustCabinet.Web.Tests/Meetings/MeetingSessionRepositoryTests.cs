using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql;
using TrustCabinet.Domain.Entities;
using TrustCabinet.Domain.Enums;
using TrustCabinet.Infrastructure;
using TrustCabinet.Infrastructure.Meetings;
using TrustCabinet.Infrastructure.Seed;

namespace TrustCabinet.Web.Tests.Meetings;

public sealed class MeetingSessionRepositoryTests : IAsyncLifetime
{
    private const string SessionsMigration = "20261002070951_MeetingSessionsForPublication";
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
    public async Task Schedule_migration_backfills_valid_legacy_meeting_with_uuid_v7_and_preserves_related_rows()
    {
        await _db.GetService<IMigrator>().MigrateAsync(SessionsMigration);
        var (meetingId, organizer) = await InsertLegacyMeetingAsync(
            "Meeting retained across schema upgrade", PlannedStart, PlannedStart.AddHours(2));
        var history = new MeetingStatusHistory
        {
            MeetingId = meetingId,
            ActorId = organizer.Id,
            FromStatus = MeetingStatus.WaitingApproval,
            ToStatus = MeetingStatus.Approved,
            Action = MeetingHistoryAction.Approve,
            OccurredAt = PlannedStart,
        };
        var email = new EmailMessage
        {
            MeetingId = meetingId,
            DeduplicationKey = "session-migration:retained-email",
            ToAddress = "retained-email@example.vn",
            Subject = "Persisted invitation",
            Body = "Persisted content",
            Status = EmailStatus.Queued,
        };
        var participant = new MeetingParticipant
        {
            MeetingId = meetingId,
            UserId = organizer.Id,
            Role = MeetingRole.Secretary,
        };
        var notification = new Notification
        {
            MeetingId = meetingId,
            UserId = organizer.Id,
            Type = NotificationType.MeetingInvited,
            Title = "Persisted notification",
        };
        _db.MeetingStatusHistory.Add(history);
        _db.EmailMessages.Add(email);
        _db.MeetingParticipants.Add(participant);
        _db.Notifications.Add(notification);
        await _db.SaveChangesAsync();
        _db.ChangeTracker.Clear();

        await _db.GetService<IMigrator>().MigrateAsync();

        var session = await _db.MeetingSessions.SingleAsync(item => item.MeetingId == meetingId);
        Assert.Equal("Meeting retained across schema upgrade", session.Title);
        Assert.Equal(PlannedStart, session.PlannedStartsAt);
        Assert.Equal(PlannedStart.AddHours(2), session.PlannedEndsAt);
        Assert.Equal(MeetingSessionStatus.Planned, session.Status);
        Assert.Equal(7, session.Id.Version);
        Assert.Equal(history.Id, (await _db.MeetingStatusHistory.SingleAsync()).Id);
        Assert.Equal(participant.Id, (await _db.MeetingParticipants.SingleAsync()).Id);
        Assert.Equal(notification.Id, (await _db.Notifications.SingleAsync()).Id);
        var preservedEmail = await _db.EmailMessages.SingleAsync();
        Assert.Equal(email.Id, preservedEmail.Id);
        Assert.Equal(email.DeduplicationKey, preservedEmail.DeduplicationKey);
        Assert.Equal(email.Body, preservedEmail.Body);
    }

    [Fact]
    public async Task Schedule_migration_uses_one_hour_fallback_when_legacy_end_is_null()
    {
        await _db.GetService<IMigrator>().MigrateAsync(SessionsMigration);
        var (meetingId, _) = await InsertLegacyMeetingAsync("No legacy end", PlannedStart, null);

        await _db.GetService<IMigrator>().MigrateAsync();

        var session = await _db.MeetingSessions.SingleAsync(item => item.MeetingId == meetingId);
        Assert.Equal(PlannedStart.AddHours(1), session.PlannedEndsAt);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    public async Task Schedule_migration_rejects_legacy_end_not_after_start(int endOffsetMinutes)
    {
        await _db.GetService<IMigrator>().MigrateAsync(SessionsMigration);
        await InsertLegacyMeetingAsync(
            "Invalid legacy range", PlannedStart, PlannedStart.AddMinutes(endOffsetMinutes));

        var exception = await Assert.ThrowsAsync<PostgresException>(
            () => _db.GetService<IMigrator>().MigrateAsync());

        Assert.Contains("ends_at before or equal", exception.MessageText, StringComparison.Ordinal);
    }

    [Fact]
    public async Task Schedule_migration_does_not_add_a_legacy_session_when_one_already_exists()
    {
        await _db.GetService<IMigrator>().MigrateAsync(SessionsMigration);
        var (meetingId, _) = await InsertLegacyMeetingAsync(
            "Session is authoritative", PlannedStart, PlannedStart.AddHours(2));
        var existing = NewSession(meetingId);
        existing.Title = "Existing authoritative session";
        existing.PlannedStartsAt = PlannedStart.AddDays(2);
        existing.PlannedEndsAt = PlannedStart.AddDays(2).AddHours(1);
        _db.MeetingSessions.Add(existing);
        await _db.SaveChangesAsync();
        _db.ChangeTracker.Clear();

        await _db.GetService<IMigrator>().MigrateAsync();

        var session = await _db.MeetingSessions.SingleAsync(item => item.MeetingId == meetingId);
        Assert.Equal(existing.Id, session.Id);
        Assert.Equal("Existing authoritative session", session.Title);
    }

    [Fact]
    public async Task Session_schema_migration_can_still_be_reverted_before_schedule_migration()
    {
        const string beforeSessions = "20261001153906_MeetingWorkflowReviewCorrections";
        await _db.GetService<IMigrator>().MigrateAsync(SessionsMigration);
        await _db.GetService<IMigrator>().MigrateAsync(beforeSessions);

        Assert.False(await _db.Database.SqlQuery<bool>($"""
            SELECT EXISTS (SELECT 1 FROM information_schema.tables
                WHERE table_schema = 'public' AND table_name = 'meeting_sessions') AS "Value"
            """).SingleAsync());
    }

    private async Task<(Guid MeetingId, User Organizer)> InsertLegacyMeetingAsync(
        string title, DateTimeOffset startsAt, DateTimeOffset? endsAt)
    {
        var organizer = new User
        {
            FullName = "Session organizer",
            Email = $"session-organizer-{Guid.CreateVersion7():N}@example.vn",
            PasswordHash = "test-only-placeholder",
        };
        _db.Users.Add(organizer);
        await _db.SaveChangesAsync();
        var meetingId = Guid.CreateVersion7();
        var now = PlannedStart.AddDays(-1);
        await _db.Database.ExecuteSqlInterpolatedAsync($"""
            INSERT INTO meetings (
                id, title, description, status, sensitivity, mode, location,
                starts_at, ends_at, created_by_id, assigned_approver_id, created_at, updated_at)
            VALUES (
                {meetingId}, {title}, {string.Empty}, {MeetingStatus.Approved.ToString()},
                {Sensitivity.Normal.ToString()}, {MeetingMode.InPerson.ToString()}, {string.Empty},
                {startsAt}, {endsAt}, {organizer.Id}, {null}, {now}, {now})
            """);
        return (meetingId, organizer);
    }

    private static MeetingSession NewSession(Guid meetingId) => new()
    {
        MeetingId = meetingId,
        Title = "Valid planned session",
        PlannedStartsAt = PlannedStart,
        PlannedEndsAt = PlannedStart.AddHours(1),
    };
}
