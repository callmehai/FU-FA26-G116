using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using TrustCabinet.Domain.Entities;
using TrustCabinet.Domain.Enums;
using TrustCabinet.Infrastructure;
using TrustCabinet.Infrastructure.Seed;

namespace TrustCabinet.Web.Tests.Meetings;

/// <summary>ADR-0013: the schedule moves from meeting_sessions back onto meetings.</summary>
public sealed class MeetingScheduleMigrationTests : IAsyncLifetime
{
    private const string SessionsSchedule = "20261004095457_MoveMeetingScheduleToSessions";
    private static readonly DateTimeOffset Morning = new(2026, 10, 20, 1, 0, 0, TimeSpan.Zero);
    private static readonly DateTimeOffset CreatedAt = new(2026, 10, 1, 3, 0, 0, TimeSpan.Zero);
    private readonly PostgreSqlTestDatabase _database = new(
        PostgresTestDatabase.ConnectionString("trustcabinet_meeting_schedule_tests"));
    private TrustCabinetDbContext _db = null!;

    public async Task InitializeAsync()
    {
        _db = new TrustCabinetDbContext(new DbContextOptionsBuilder<TrustCabinetDbContext>()
            .UseNpgsql(_database.DataSource, options => options.MigrationsHistoryTable("__ef_migrations_history"))
            .UseSnakeCaseNamingConvention().Options);
        await _db.GetService<IMigrator>().MigrateAsync(SessionsSchedule);
    }

    public async Task DisposeAsync()
    {
        await _db.DisposeAsync();
        await _database.DisposeAsync();
    }

    [Fact]
    public async Task Takes_earliest_start_and_latest_end_of_sessions_that_were_not_cancelled()
    {
        var organizerId = await InsertUserAsync();
        var meetingId = await InsertMeetingAsync(organizerId, "Approved");
        await InsertSessionAsync(meetingId, Morning.AddHours(-3), Morning.AddHours(-2), "Cancelled");
        await InsertSessionAsync(meetingId, Morning, Morning.AddHours(2), "Planned");
        await InsertSessionAsync(meetingId, Morning.AddHours(4), Morning.AddHours(6), "Planned");
        await InsertSessionAsync(meetingId, Morning.AddHours(7), Morning.AddHours(9), "Cancelled");

        await MigrateToLatestAsync();

        var meeting = await _db.Meetings.SingleAsync(item => item.Id == meetingId);
        Assert.Equal(Morning, meeting.PlannedStartTime);
        Assert.Equal(Morning.AddHours(6), meeting.PlannedEndTime);
        Assert.Null(meeting.ActualStartedAt);
        Assert.Null(meeting.ActualEndedAt);
        Assert.Null(meeting.OnlineLink);
        Assert.False(await TableExistsAsync("meeting_sessions"));
    }

    [Fact]
    public async Task Falls_back_to_cancelled_sessions_then_to_creation_time()
    {
        var organizerId = await InsertUserAsync();
        var allCancelled = await InsertMeetingAsync(organizerId, "Draft");
        await InsertSessionAsync(allCancelled, Morning, Morning.AddHours(1), "Cancelled");
        var withoutSessions = await InsertMeetingAsync(organizerId, "Draft");

        await MigrateToLatestAsync();

        var cancelled = await _db.Meetings.SingleAsync(item => item.Id == allCancelled);
        Assert.Equal(Morning, cancelled.PlannedStartTime);
        Assert.Equal(Morning.AddHours(1), cancelled.PlannedEndTime);
        var empty = await _db.Meetings.SingleAsync(item => item.Id == withoutSessions);
        Assert.Equal(CreatedAt, empty.PlannedStartTime);
        Assert.Equal(CreatedAt.AddHours(1), empty.PlannedEndTime);
    }

    [Theory]
    [InlineData("MinutesIssued")]
    [InlineData("Archived")]
    public async Task Retired_statuses_become_Ended_on_the_meeting_and_its_history(string legacyStatus)
    {
        var organizerId = await InsertUserAsync();
        var meetingId = await InsertMeetingAsync(organizerId, legacyStatus);
        await InsertSessionAsync(meetingId, Morning, Morning.AddHours(1), "Completed");
        await _db.Database.ExecuteSqlInterpolatedAsync($"""
            INSERT INTO meeting_status_history (
                id, meeting_id, from_status, to_status, action, actor_id, reason, occurred_at, created_at, updated_at)
            VALUES (
                {Guid.CreateVersion7()}, {meetingId}, {"Ended"}, {legacyStatus}, {"LegacyTransition"},
                {organizerId}, {string.Empty}, {CreatedAt}, {CreatedAt}, {CreatedAt})
            """);

        await MigrateToLatestAsync();

        Assert.Equal(MeetingStatus.Ended, (await _db.Meetings.SingleAsync(item => item.Id == meetingId)).Status);
        var history = await _db.MeetingStatusHistory.SingleAsync(item => item.MeetingId == meetingId);
        Assert.Equal(MeetingStatus.Ended, history.FromStatus);
        Assert.Equal(MeetingStatus.Ended, history.ToStatus);
    }

    [Fact]
    public async Task Down_gives_each_meeting_one_session_with_its_schedule()
    {
        var organizerId = await InsertUserAsync();
        var meetingId = await InsertMeetingAsync(organizerId, "InProgress");
        await InsertSessionAsync(meetingId, Morning, Morning.AddHours(2), "InProgress");
        await MigrateToLatestAsync();

        await _db.GetService<IMigrator>().MigrateAsync(SessionsSchedule);

        var session = await _db.Database.SqlQuery<RestoredSession>($"""
            SELECT planned_starts_at AS "start", planned_ends_at AS "end", status AS "status"
            FROM meeting_sessions WHERE meeting_id = {meetingId}
            """).SingleAsync();
        Assert.Equal(Morning, session.Start);
        Assert.Equal(Morning.AddHours(2), session.End);
        Assert.Equal("InProgress", session.Status);
    }

    private async Task MigrateToLatestAsync()
    {
        await _db.GetService<IMigrator>().MigrateAsync();
        _db.ChangeTracker.Clear();
    }

    private async Task<Guid> InsertUserAsync()
    {
        // The users table has not changed since the sessions migration, so the current model fits.
        await ReferenceDataSeeder.SeedAsync(_db);
        var organizer = new User
        {
            FullName = "Schedule organizer",
            Email = $"organizer-{Guid.CreateVersion7():N}@example.vn",
            PasswordHash = "test-only-placeholder",
        };
        _db.Users.Add(organizer);
        await _db.SaveChangesAsync();
        return organizer.Id;
    }

    private async Task<Guid> InsertMeetingAsync(Guid organizerId, string status)
    {
        var id = Guid.CreateVersion7();
        await _db.Database.ExecuteSqlInterpolatedAsync($"""
            INSERT INTO meetings (
                id, title, description, status, sensitivity, mode, location,
                created_by_id, assigned_approver_id, created_at, updated_at)
            VALUES (
                {id}, {"Legacy meeting"}, {string.Empty}, {status}, {"Normal"}, {"InPerson"}, {string.Empty},
                {organizerId}, {null}, {CreatedAt}, {CreatedAt})
            """);
        return id;
    }

    private Task InsertSessionAsync(Guid meetingId, DateTimeOffset start, DateTimeOffset end, string status)
        => _db.Database.ExecuteSqlInterpolatedAsync($"""
            INSERT INTO meeting_sessions (
                id, meeting_id, title, planned_starts_at, planned_ends_at, status, created_at, updated_at)
            VALUES ({Guid.CreateVersion7()}, {meetingId}, {"Session"}, {start}, {end}, {status}, {CreatedAt}, {CreatedAt})
            """);

    private Task<bool> TableExistsAsync(string table)
        => _db.Database.SqlQuery<bool>($"""
            SELECT EXISTS (SELECT 1 FROM information_schema.tables
                WHERE table_schema = 'public' AND table_name = {table}) AS "Value"
            """).SingleAsync();

    private sealed record RestoredSession(DateTimeOffset Start, DateTimeOffset End, string Status);
}
