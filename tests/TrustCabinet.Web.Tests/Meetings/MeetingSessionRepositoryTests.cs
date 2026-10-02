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
    public async Task Publication_query_uses_the_current_transaction_and_domain_whitespace_validation()
    {
        var meeting = await AddMeetingAsync("Target meeting");
        var other = await AddMeetingAsync("Other meeting");
        var whitespace = NewSession(meeting.Id);
        whitespace.Title = "\u00A0\t\r\n";
        var invalidTimes = NewSession(meeting.Id);
        invalidTimes.PlannedEndsAt = invalidTimes.PlannedStartsAt;
        var cancelled = NewSession(meeting.Id);
        cancelled.Status = MeetingSessionStatus.Cancelled;
        _db.MeetingSessions.AddRange(whitespace, invalidTimes, cancelled, NewSession(other.Id));
        await _db.SaveChangesAsync();
        var repository = new MeetingRepository(_db);

        Assert.False(await repository.HasValidPlannedSessionAsync(meeting.Id));
        Assert.False(await repository.HasValidPlannedSessionAsync(Guid.CreateVersion7()));
        var ready = await repository.InTransactionAsync(async ct =>
        {
            Assert.NotNull(_db.Database.CurrentTransaction);
            _db.MeetingSessions.Add(NewSession(meeting.Id));
            await _db.SaveChangesAsync(ct);
            return await repository.HasValidPlannedSessionAsync(meeting.Id, ct);
        });

        Assert.True(ready);
        Assert.True(await repository.HasValidPlannedSessionAsync(meeting.Id));
    }

    [Theory]
    [InlineData(MeetingSessionStatus.InProgress)]
    [InlineData(MeetingSessionStatus.Completed)]
    [InlineData(MeetingSessionStatus.Cancelled)]
    public async Task Non_Planned_session_does_not_make_publication_ready(MeetingSessionStatus status)
    {
        var meeting = await AddMeetingAsync("Session status check");
        var session = NewSession(meeting.Id);
        session.Status = status;
        _db.MeetingSessions.Add(session);
        await _db.SaveChangesAsync();

        Assert.False(await new MeetingRepository(_db).HasValidPlannedSessionAsync(meeting.Id));
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
        Assert.False(await new MeetingRepository(_db).HasValidPlannedSessionAsync(meeting.Id));
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
