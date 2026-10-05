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
        _db = NewDb();
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
        // Raw SQL: the current model has columns this intermediate schema does not have yet.
        var existingId = Guid.CreateVersion7();
        var existingStart = PlannedStart.AddDays(2);
        await _db.Database.ExecuteSqlInterpolatedAsync($"""
            INSERT INTO meeting_sessions (
                id, meeting_id, title, planned_starts_at, planned_ends_at, status, created_at, updated_at)
            VALUES (
                {existingId}, {meetingId}, {"Existing authoritative session"}, {existingStart},
                {existingStart.AddHours(1)}, {MeetingSessionStatus.Planned.ToString()}, {PlannedStart}, {PlannedStart})
            """);

        await _db.GetService<IMigrator>().MigrateAsync();

        var session = await _db.MeetingSessions.SingleAsync(item => item.MeetingId == meetingId);
        Assert.Equal(existingId, session.Id);
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

    [Fact]
    public async Task Session_service_plans_sessions_against_postgres_without_touching_the_meeting()
    {
        var secretary = new User { FullName = "Thư ký", Email = "session-secretary@example.vn", PasswordHash = "x" };
        var member = new User { FullName = "Thành viên", Email = "session-member@example.vn", PasswordHash = "x" };
        var meeting = new Meeting
        {
            Title = "Họp công bố",
            Status = MeetingStatus.Published,
            CreatedById = secretary.Id,
            CreatedBy = secretary,
        };
        meeting.Participants.Add(new MeetingParticipant { UserId = secretary.Id, Role = MeetingRole.Secretary });
        meeting.Participants.Add(new MeetingParticipant { UserId = member.Id, Role = MeetingRole.Member });
        _db.Users.AddRange(secretary, member);
        _db.Meetings.Add(meeting);
        await _db.SaveChangesAsync();
        _db.ChangeTracker.Clear();
        var service = new MeetingSessionService(
            new MeetingRepository(_db),
            new MeetingSessionRepository(_db),
            Microsoft.Extensions.Logging.Abstractions.NullLogger<MeetingSessionService>.Instance);
        var start = DateTimeOffset.UtcNow.AddDays(3);

        var late = await service.CreateAsync(
            meeting.Id, new("Phiên chiều", start.AddHours(5).ToString("O"), start.AddHours(6).ToString("O")), secretary.Id);
        var early = await service.CreateAsync(
            meeting.Id, new("Phiên sáng", start.ToString("O"), start.AddHours(1).ToString("O")), secretary.Id);
        var updated = await service.UpdateAsync(
            meeting.Id, early.Session!.Id, new("Phiên sáng sớm", start.ToString("O"), start.AddHours(2).ToString("O")), secretary.Id);
        var cancelled = await service.CancelAsync(meeting.Id, late.Session!.Id, secretary.Id);
        _db.ChangeTracker.Clear();
        var list = await service.ListAsync(meeting.Id, member.Id);

        Assert.Equal(Domain.Meetings.MeetingSessionOutcome.Success, updated.Outcome);
        Assert.Equal(MeetingSessionStatus.Cancelled, cancelled.Session!.Status);
        Assert.Equal(
            new[] { ("Phiên sáng sớm", MeetingSessionStatus.Planned), ("Phiên chiều", MeetingSessionStatus.Cancelled) },
            list.Sessions!.Select(session => (session.Title, session.Status)));
        Assert.Equal(MeetingStatus.Published, (await _db.Meetings.SingleAsync()).Status);
        var notifications = await _db.Notifications.ToListAsync();
        Assert.Equal(4, notifications.Count);
        Assert.All(notifications, notification =>
        {
            Assert.Equal(member.Id, notification.UserId);
            Assert.Equal(NotificationType.MeetingSessionChanged, notification.Type);
        });
    }

    [Fact]
    public async Task Concurrent_starts_leave_exactly_one_session_in_progress_and_end_keeps_the_meeting_running()
    {
        var chair = new User { FullName = "Chủ tọa", Email = "session-chair@example.vn", PasswordHash = "x" };
        var secretary = new User { FullName = "Thư ký", Email = "run-secretary@example.vn", PasswordHash = "x" };
        var meeting = new Meeting
        {
            Title = "Họp đang diễn ra",
            Status = MeetingStatus.InProgress,
            CreatedById = secretary.Id,
            CreatedBy = secretary,
        };
        meeting.Participants.Add(new MeetingParticipant { UserId = chair.Id, Role = MeetingRole.Chair });
        meeting.Participants.Add(new MeetingParticipant { UserId = secretary.Id, Role = MeetingRole.Secretary });
        var first = NewSession(meeting.Id);
        var second = NewSession(meeting.Id);
        second.PlannedStartsAt = PlannedStart.AddHours(2);
        second.PlannedEndsAt = PlannedStart.AddHours(3);
        meeting.Sessions.Add(first);
        meeting.Sessions.Add(second);
        _db.Users.AddRange(chair, secretary);
        _db.Meetings.Add(meeting);
        await _db.SaveChangesAsync();
        _db.ChangeTracker.Clear();

        // Hold the meeting lock so both requests are queued behind it before either can check BR-59.
        await using var holder = NewDb();
        await using var hold = await holder.Database.BeginTransactionAsync();
        await holder.Database.ExecuteSqlInterpolatedAsync($"SELECT 1 FROM meetings WHERE id = {meeting.Id} FOR UPDATE");

        // Each request has its own unit of work, as it does in the app; only the meeting lock orders them.
        var starts = new[] { first.Id, second.Id }.Select(async sessionId =>
        {
            await using var db = NewDb();
            return await NewService(db).StartAsync(meeting.Id, sessionId, chair.Id);
        }).ToArray();
        await Task.Delay(TimeSpan.FromMilliseconds(500));
        Assert.All(starts, start => Assert.False(start.IsCompleted));
        await hold.CommitAsync();
        var results = await Task.WhenAll(starts);

        Assert.Equal(
            new[] { Domain.Meetings.MeetingSessionOutcome.Success, Domain.Meetings.MeetingSessionOutcome.AnotherSessionInProgress },
            results.Select(result => result.Outcome).Order());
        var started = Assert.Single(await _db.MeetingSessions.Where(s => s.Status == MeetingSessionStatus.InProgress).ToListAsync());
        Assert.NotNull(started.ActualStartedAt);
        // The response carries the value PostgreSQL stored, so a later GET returns the same instant.
        Assert.Equal(started.ActualStartedAt, results.Single(result => result.Session is not null).Session!.ActualStartedAt);

        var ended = await NewService(_db).EndAsync(meeting.Id, started.Id, chair.Id);
        _db.ChangeTracker.Clear();

        Assert.Equal(MeetingSessionStatus.Completed, ended.Session!.Status);
        var stored = await _db.MeetingSessions.SingleAsync(s => s.Id == started.Id);
        Assert.Equal(started.ActualStartedAt, stored.ActualStartedAt);
        Assert.Equal(ended.Session.ActualEndedAt, stored.ActualEndedAt);
        Assert.Equal(MeetingStatus.InProgress, (await _db.Meetings.SingleAsync()).Status);
        Assert.Empty(await _db.MeetingStatusHistory.ToListAsync());
    }

    [Fact]
    public async Task Database_rejects_a_second_in_progress_session_of_one_meeting()
    {
        var organizer = new User { FullName = "Người tổ chức", Email = "unique-organizer@example.vn", PasswordHash = "x" };
        var meeting = new Meeting { Title = "Một phiên đang họp", Status = MeetingStatus.InProgress, CreatedBy = organizer };
        _db.Meetings.Add(meeting);
        await _db.SaveChangesAsync();
        var meetingId = meeting.Id;
        var running = NewSession(meetingId);
        running.Status = MeetingSessionStatus.InProgress;
        var other = NewSession(meetingId);
        other.Status = MeetingSessionStatus.InProgress;
        _db.MeetingSessions.AddRange(running, other);

        await Assert.ThrowsAsync<DbUpdateException>(() => _db.SaveChangesAsync());
    }

    private TrustCabinetDbContext NewDb()
        => new(new DbContextOptionsBuilder<TrustCabinetDbContext>()
            .UseNpgsql(_database.DataSource, options => options.MigrationsHistoryTable("__ef_migrations_history"))
            .UseSnakeCaseNamingConvention().Options);

    private static MeetingSessionService NewService(TrustCabinetDbContext db)
        => new(
            new MeetingRepository(db),
            new MeetingSessionRepository(db),
            Microsoft.Extensions.Logging.Abstractions.NullLogger<MeetingSessionService>.Instance);

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
