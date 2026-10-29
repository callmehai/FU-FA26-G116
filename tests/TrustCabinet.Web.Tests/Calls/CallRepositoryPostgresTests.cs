using Microsoft.EntityFrameworkCore;
using Npgsql;
using TrustCabinet.Domain.Calls;
using TrustCabinet.Domain.Entities;
using TrustCabinet.Domain.Enums;
using TrustCabinet.Infrastructure;
using TrustCabinet.Infrastructure.Calls;

namespace TrustCabinet.Web.Tests.Calls;

/// <summary>
/// PostgreSQL coverage for what the fakes cannot prove: the partial unique index that keeps one
/// live room per meeting, and the room-access query.
/// </summary>
public sealed class CallRepositoryPostgresTests : IAsyncLifetime
{
    private static readonly string ConnectionString = PostgresTestDatabase.ConnectionString("trustcabinet_call_tests");

    private TrustCabinetDbContext _db = null!;

    public async Task InitializeAsync()
    {
        _db = NewContext();
        try
        {
            await _db.Database.EnsureDeletedAsync();
            await _db.Database.EnsureCreatedAsync();
        }
        catch (NpgsqlException exception)
        {
            throw new InvalidOperationException(
                "Call PostgreSQL integration tests need the CI/development PostgreSQL. Set "
                + "TRUSTCABINET_TEST_CONNECTION_STRING, POSTGRES_PASSWORD, or the repository .env file, "
                + "then run `docker compose up -d postgres`.", exception);
        }
    }

    public async Task DisposeAsync()
    {
        await _db.Database.EnsureDeletedAsync();
        await _db.DisposeAsync();
    }

    [Fact]
    public async Task Concurrent_first_joins_share_one_active_session()
    {
        var (meeting, users) = await SeedAsync(MeetingStatus.InProgress, MeetingMode.Online, 2);

        await using var first = NewContext();
        await using var second = NewContext();
        var sessions = await Task.WhenAll(
            new CallRepository(first).EnsureActiveRoomSessionAsync(meeting.Id, users[0].Id, DateTimeOffset.UtcNow),
            new CallRepository(second).EnsureActiveRoomSessionAsync(meeting.Id, users[1].Id, DateTimeOffset.UtcNow));

        Assert.Equal(sessions[0].Id, sessions[1].Id);
        Assert.Equal(1, await _db.CallSessions.CountAsync());
    }

    [Fact]
    public async Task A_second_active_room_for_the_same_meeting_is_rejected_by_postgresql()
    {
        var (meeting, users) = await SeedAsync(MeetingStatus.InProgress, MeetingMode.Online, 1);
        _db.CallSessions.AddRange(ActiveSession(meeting.Id, users[0].Id), ActiveSession(meeting.Id, users[0].Id));

        var error = await Assert.ThrowsAsync<DbUpdateException>(() => _db.SaveChangesAsync());
        var postgres = Assert.IsType<PostgresException>(error.InnerException);
        Assert.Equal(CallRepository.ActiveRoomIndex, postgres.ConstraintName);
    }

    [Fact]
    public async Task Join_is_idempotent_and_ending_closes_open_stints()
    {
        var (meeting, users) = await SeedAsync(MeetingStatus.InProgress, MeetingMode.Online, 1);
        var calls = new CallRepository(_db);
        var session = await calls.EnsureActiveRoomSessionAsync(meeting.Id, users[0].Id, DateTimeOffset.UtcNow);

        await calls.RecordJoinAsync(session.Id, users[0].Id, DateTimeOffset.UtcNow);
        await calls.RecordJoinAsync(session.Id, users[0].Id, DateTimeOffset.UtcNow);
        await calls.EndSessionAsync(session.Id, DateTimeOffset.UtcNow);

        var stint = await _db.CallParticipants.AsNoTracking().SingleAsync();
        Assert.NotNull(stint.LeftAt);
        Assert.Null(await calls.FindActiveRoomSessionAsync(meeting.Id));
        // Once ended, a new session may open for the same meeting.
        Assert.NotEqual(session.Id, (await calls.EnsureActiveRoomSessionAsync(meeting.Id, users[0].Id, DateTimeOffset.UtcNow)).Id);
    }

    [Fact]
    public async Task Room_access_needs_an_invitation_that_was_not_delegated_and_an_active_account()
    {
        var (meeting, users) = await SeedAsync(MeetingStatus.InProgress, MeetingMode.Hybrid, 4);
        (await _db.MeetingParticipants.SingleAsync(p => p.UserId == users[1].Id)).Response = InvitationResponse.Delegated;
        (await _db.Users.SingleAsync(u => u.Id == users[2].Id)).IsActive = false;
        // A delegation target set without the Delegated response still hands the seat away.
        (await _db.MeetingParticipants.SingleAsync(p => p.UserId == users[3].Id)).DelegatedToUserId = users[0].Id;
        await _db.SaveChangesAsync();
        var calls = new CallRepository(_db);

        var access = await calls.FindRoomAccessAsync(meeting.Id, users[0].Id);
        Assert.Equal(MeetingMode.Hybrid, access!.Mode);
        Assert.Null(await calls.FindRoomAccessAsync(meeting.Id, users[1].Id));
        Assert.Null(await calls.FindRoomAccessAsync(meeting.Id, users[2].Id));
        Assert.Null(await calls.FindRoomAccessAsync(meeting.Id, users[3].Id));
        Assert.Equal([users[0].Id], await calls.ListAllowedUserIdsAsync(meeting.Id));
    }

    [Fact]
    public async Task Nobody_is_allowed_in_the_room_of_a_meeting_that_is_not_in_progress()
    {
        var (meeting, users) = await SeedAsync(MeetingStatus.Ended, MeetingMode.Online, 1);
        var calls = new CallRepository(_db);

        Assert.NotNull(await calls.FindRoomAccessAsync(meeting.Id, users[0].Id));
        Assert.Empty(await calls.ListAllowedUserIdsAsync(meeting.Id));
    }

    [Theory]
    [InlineData(MeetingStatus.Published, true)]
    [InlineData(MeetingStatus.Approved, false)]
    public async Task A_cancelled_meeting_shows_its_room_only_if_it_was_published(MeetingStatus cancelledFrom, bool visible)
    {
        var (meeting, users) = await SeedAsync(MeetingStatus.Cancelled, MeetingMode.Online, 1);
        meeting.CancelledFromStatus = cancelledFrom;
        await _db.SaveChangesAsync();
        var calls = new CallRepository(_db);

        var access = await calls.FindRoomAccessAsync(meeting.Id, users[0].Id);
        Assert.Equal(visible, access is not null);
        if (visible) Assert.Equal(CallRoomStatus.Cancelled, CallRoomRules.StatusOf(access!.Status, access.Mode));
        Assert.Empty(await calls.ListAllowedUserIdsAsync(meeting.Id));
    }

    private async Task<(Meeting Meeting, User[] Users)> SeedAsync(MeetingStatus status, MeetingMode mode, int invited)
    {
        var role = new Role { Name = $"Call test {Guid.CreateVersion7():N}", IsBuiltIn = false };
        var users = Enumerable.Range(0, invited)
            .Select(i => new User { FullName = $"User {i}", Email = $"call{i}@trustcabinet.local", PasswordHash = "h", RoleId = role.Id })
            .ToArray();
        var meeting = new Meeting
        {
            Title = "Call test",
            Status = status,
            Sensitivity = Sensitivity.Internal,
            Mode = mode,
            PlannedStartTime = DateTimeOffset.UtcNow,
            PlannedEndTime = DateTimeOffset.UtcNow.AddHours(1),
            CreatedById = users[0].Id,
        };
        _db.AddRange(role);
        _db.AddRange(users);
        _db.Add(meeting);
        _db.AddRange(users.Select(u => new MeetingParticipant { MeetingId = meeting.Id, UserId = u.Id }));
        await _db.SaveChangesAsync();
        return (meeting, users);
    }

    private static CallSession ActiveSession(Guid meetingId, Guid startedBy) => new()
    {
        Kind = CallKind.MeetingRoom,
        MeetingId = meetingId,
        Status = CallStatus.Active,
        StartedById = startedBy,
    };

    private static TrustCabinetDbContext NewContext() => new(
        new DbContextOptionsBuilder<TrustCabinetDbContext>()
            .UseNpgsql(ConnectionString)
            .UseSnakeCaseNamingConvention()
            .Options);
}
