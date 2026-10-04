using Microsoft.EntityFrameworkCore;
using Npgsql;
using TrustCabinet.Domain.Entities;
using TrustCabinet.Domain.Enums;
using TrustCabinet.Infrastructure;
using TrustCabinet.Infrastructure.Notifications;

namespace TrustCabinet.Web.Tests.Notifications;

/// <summary>
/// PostgreSQL coverage for notification constraints and queries that the in-memory provider cannot
/// verify. Requires the same local/CI database configuration as the other repository tests.
/// </summary>
public sealed class NotificationRepositoryPostgresTests : IAsyncLifetime
{
    private readonly PostgreSqlTestDatabase _database = new(
        PostgresTestDatabase.ConnectionString("trustcabinet_notification_tests"));

    private TrustCabinetDbContext _db = null!;

    public async Task InitializeAsync()
    {
        _db = NewContext();
        try
        {
            await _db.Database.EnsureCreatedAsync();
        }
        catch (NpgsqlException exception)
        {
            throw new InvalidOperationException(
                "Notification PostgreSQL integration tests need the CI/development PostgreSQL. "
                + "Set TRUSTCABINET_TEST_CONNECTION_STRING, POSTGRES_PASSWORD, or the repository "
                + ".env file, then run `docker compose up -d postgres`.", exception);
        }
    }

    public async Task DisposeAsync()
    {
        await _db.DisposeAsync();
        await _database.DisposeAsync();
    }

    [Fact]
    public async Task Duplicate_deduplication_key_is_rejected_by_postgresql()
    {
        var role = NewRole();
        var recipient = NewUser("recipient@trustcabinet.local", role.Id);
        _db.AddRange(role, recipient);
        _db.Notifications.Add(NewNotification(recipient.Id, "meeting-reminder:duplicate"));
        await _db.SaveChangesAsync();
        _db.ChangeTracker.Clear();

        _db.Notifications.Add(NewNotification(recipient.Id, "meeting-reminder:duplicate"));

        var exception = await Assert.ThrowsAsync<DbUpdateException>(
            () => _db.SaveChangesAsync());
        var postgres = Assert.IsType<PostgresException>(exception.InnerException);
        Assert.Equal(PostgresErrorCodes.UniqueViolation, postgres.SqlState);
    }

    [Fact]
    public async Task Upcoming_recipients_returns_only_active_participants_of_published_meetings_in_window()
    {
        var now = new DateTimeOffset(2026, 9, 29, 9, 0, 0, TimeSpan.Zero);
        var role = NewRole();
        var organizer = NewUser("organizer@trustcabinet.local", role.Id);
        var included = NewUser("included@trustcabinet.local", role.Id);
        var inactive = NewUser("inactive@trustcabinet.local", role.Id, isActive: false);
        var draftParticipant = NewUser("draft@trustcabinet.local", role.Id);
        var lateParticipant = NewUser("late@trustcabinet.local", role.Id);
        var published = NewMeeting("Published in window", MeetingStatus.Published,
            now.AddMinutes(10), organizer.Id);
        var draft = NewMeeting("Draft in window", MeetingStatus.Draft,
            now.AddMinutes(10), organizer.Id);
        var late = NewMeeting("Published after window", MeetingStatus.Published,
            now.AddMinutes(16), organizer.Id);

        _db.AddRange(role, organizer, included, inactive, draftParticipant, lateParticipant);
        _db.Meetings.AddRange(published, draft, late);
        _db.MeetingParticipants.AddRange(
            NewParticipant(published.Id, included.Id),
            NewParticipant(published.Id, inactive.Id),
            NewParticipant(draft.Id, draftParticipant.Id),
            NewParticipant(late.Id, lateParticipant.Id));
        await _db.SaveChangesAsync();
        _db.ChangeTracker.Clear();

        var recipients = await new NotificationRepository(_db)
            .FindUpcomingMeetingRecipientsAsync(now, now.AddMinutes(15));

        var recipient = Assert.Single(recipients);
        Assert.Equal(published.Id, recipient.MeetingId);
        Assert.Equal(published.Title, recipient.MeetingTitle);
        Assert.Equal(Assert.Single(published.Sessions).Id, recipient.SessionId);
        Assert.Equal(included.Id, recipient.UserId);
    }

    private TrustCabinetDbContext NewContext() => new(
        new DbContextOptionsBuilder<TrustCabinetDbContext>()
            .UseNpgsql(_database.DataSource)
            .UseSnakeCaseNamingConvention()
            .Options);

    private static Role NewRole() => new()
    {
        Name = $"Notification test {Guid.CreateVersion7():N}",
        IsBuiltIn = false,
    };

    private static User NewUser(string email, Guid roleId, bool isActive = true) => new()
    {
        FullName = email,
        Email = email,
        PasswordHash = "test-hash",
        RoleId = roleId,
        IsActive = isActive,
    };

    private static Notification NewNotification(Guid userId, string deduplicationKey) => new()
    {
        UserId = userId,
        Type = NotificationType.MeetingStartingSoon,
        Title = "Cuộc họp sắp bắt đầu",
        DeduplicationKey = deduplicationKey,
    };

    private static Meeting NewMeeting(
        string title, MeetingStatus status, DateTimeOffset startsAt, Guid organizerId)
    {
        var meeting = new Meeting
        {
            Title = title,
            Status = status,
            Sensitivity = Sensitivity.Internal,
            Mode = MeetingMode.Online,
            CreatedById = organizerId,
        };
        meeting.Sessions.Add(new MeetingSession
        {
            Meeting = meeting,
            Title = $"Phiên {title}",
            PlannedStartsAt = startsAt,
            PlannedEndsAt = startsAt.AddHours(1),
            Status = MeetingSessionStatus.Planned,
        });
        return meeting;
    }

    private static MeetingParticipant NewParticipant(Guid meetingId, Guid userId) => new()
    {
        MeetingId = meetingId,
        UserId = userId,
    };
}
