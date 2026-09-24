using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using Npgsql;
using TrustCabinet.Domain.Entities;
using TrustCabinet.Domain.Enums;
using TrustCabinet.Domain.Meetings;
using TrustCabinet.Infrastructure;
using TrustCabinet.Infrastructure.Meetings;

namespace TrustCabinet.Web.Tests.Meetings;

/// <summary>PostgreSQL integration checks for atomic creation and state-safe concurrent updates.</summary>
public sealed class MeetingRepositoryTests : IAsyncLifetime
{
    private const string AdminConnectionString =
        "Host=localhost;Port=5432;Database=postgres;Username=trustcabinet;Password=123";

    private string _databaseName = null!;
    private string _connectionString = null!;
    private TrustCabinetDbContext _db = null!;
    private Guid _creatorId;
    private Guid _approverRoleId;

    private TrustCabinetDbContext NewContext() => new(
        new DbContextOptionsBuilder<TrustCabinetDbContext>()
            .UseNpgsql(_connectionString)
            .UseSnakeCaseNamingConvention()
            .Options);

    public async Task InitializeAsync()
    {
        _databaseName = $"trustcabinet_meeting_{Guid.CreateVersion7():N}";
        _connectionString = new NpgsqlConnectionStringBuilder(AdminConnectionString)
        {
            Database = _databaseName,
        }.ConnectionString;

        try
        {
            await using var admin = new NpgsqlConnection(AdminConnectionString);
            await admin.OpenAsync();
            await using (var create = admin.CreateCommand())
            {
                create.CommandText = $"CREATE DATABASE \"{_databaseName}\"";
                await create.ExecuteNonQueryAsync();
            }

            _db = NewContext();
            await _db.Database.EnsureCreatedAsync();

            var role = new Role { Name = "Meeting test secretary", IsBuiltIn = false };
            _approverRoleId = role.Id;
            var creator = new User
            {
                FullName = "Meeting Test Creator",
                Email = $"{Guid.CreateVersion7():N}@trustcabinet.local",
                PasswordHash = "test-hash",
                Role = role,
            };
            _creatorId = creator.Id;
            _db.Users.Add(creator);
            await _db.SaveChangesAsync();
            _db.RoleCapabilities.Add(new RoleCapability
            {
                RoleId = role.Id,
                Capability = Capability.MeetingApprover,
            });
            await _db.SaveChangesAsync();
            _db.ChangeTracker.Clear();
        }
        catch (NpgsqlException exception)
        {
            throw new InvalidOperationException(
                "Meeting PostgreSQL integration tests need the CI/development PostgreSQL. "
                + "Start it with `docker compose up -d postgres`.", exception);
        }
    }

    public async Task DisposeAsync()
    {
        if (_db is not null)
        {
            await _db.DisposeAsync();
        }

        NpgsqlConnection.ClearAllPools();
        await using var admin = new NpgsqlConnection(AdminConnectionString);
        await admin.OpenAsync();
        await using var drop = admin.CreateCommand();
        drop.CommandText = $"DROP DATABASE IF EXISTS \"{_databaseName}\" WITH (FORCE)";
        await drop.ExecuteNonQueryAsync();
    }

    private MeetingService NewService(TrustCabinetDbContext? db = null)
    {
        var context = db ?? _db;
        return new MeetingService(new MeetingRepository(context), NullLogger<MeetingService>.Instance);
    }

    private MeetingInput Input(string title = "PostgreSQL meeting")
        => new(
            title,
            "2026-10-01T09:00:00+07:00",
            "2026-10-01T10:00:00+07:00",
            "Online",
            "Conference link",
            "Confidential",
            "Integration test",
            _creatorId);

    private static User NewUser(string fullName, Guid roleId, bool isActive = true) => new()
    {
        FullName = fullName,
        Email = $"{Guid.CreateVersion7():N}@trustcabinet.local",
        PasswordHash = "test-hash",
        RoleId = roleId,
        IsActive = isActive,
    };

    private Role NewRole(string prefix) => new()
    {
        Name = $"{prefix} {Guid.CreateVersion7():N}",
        IsBuiltIn = false,
    };

    [Fact]
    public async Task Create_persists_the_meeting_and_initial_history_as_one_operation()
    {
        var result = await NewService().CreateAsync(Input(), _creatorId);

        Assert.True(result.Succeeded);
        await using var verify = NewContext();
        var meeting = await verify.Meetings.SingleAsync(m => m.Id == result.Meeting!.Id);
        Assert.Equal("PostgreSQL meeting", meeting.Title);
        Assert.Equal(MeetingStatus.Draft, meeting.Status);
        Assert.Equal(_creatorId, meeting.CreatedById);
        Assert.Equal(TimeSpan.Zero, meeting.StartsAt.Offset);

        var history = await verify.MeetingStatusHistory.SingleAsync(h => h.MeetingId == meeting.Id);
        Assert.Null(history.FromStatus);
        Assert.Equal(MeetingStatus.Draft, history.ToStatus);
        Assert.Equal(_creatorId, history.ActorId);
    }

    [Fact]
    public async Task Create_rolls_back_the_meeting_when_initial_history_cannot_be_written()
    {
        await _db.Database.ExecuteSqlRawAsync("""
            CREATE FUNCTION reject_test_meeting_history() RETURNS trigger LANGUAGE plpgsql AS $$
            BEGIN
                RAISE EXCEPTION 'test history failure' USING ERRCODE = '23514';
            END;
            $$;
            CREATE TRIGGER reject_test_meeting_history
                BEFORE INSERT ON meeting_status_history
                FOR EACH ROW EXECUTE FUNCTION reject_test_meeting_history();
            """);

        var result = await NewService().CreateAsync(Input(), _creatorId);

        Assert.Equal(MeetingOutcome.SaveFailed, result.Outcome);
        Assert.Equal(0, await _db.Meetings.CountAsync());
        Assert.Equal(0, await _db.MeetingStatusHistory.CountAsync());
    }

    [Fact]
    public async Task Update_waits_for_a_concurrent_status_change_then_refuses_the_stale_edit()
    {
        var created = await NewService().CreateAsync(Input(), _creatorId);
        Assert.True(created.Succeeded);
        var id = created.Meeting!.Id;

        await using var blocker = new NpgsqlConnection(_connectionString);
        await blocker.OpenAsync();
        await using var statusTransaction = await blocker.BeginTransactionAsync();
        await using (var changeStatus = blocker.CreateCommand())
        {
            changeStatus.Transaction = statusTransaction;
            changeStatus.CommandText = "UPDATE meetings SET status = 'WaitingApproval' WHERE id = @id";
            changeStatus.Parameters.AddWithValue("id", id);
            await changeStatus.ExecuteNonQueryAsync();
        }

        await using var updateContext = NewContext();
        var pendingUpdate = NewService(updateContext).UpdateAsync(id, Input("Stale edit"), _creatorId);
        await Task.Delay(100);
        Assert.False(pendingUpdate.IsCompleted);

        await statusTransaction.CommitAsync();
        var result = await pendingUpdate.WaitAsync(TimeSpan.FromSeconds(10));

        Assert.Equal(MeetingOutcome.NotEditable, result.Outcome);
        Assert.Equal("PostgreSQL meeting", await _db.Meetings
            .Where(m => m.Id == id)
            .Select(m => m.Title)
            .SingleAsync());
        Assert.Equal(MeetingStatus.WaitingApproval, await _db.Meetings
            .Where(m => m.Id == id)
            .Select(m => m.Status)
            .SingleAsync());
    }

    [Fact]
    public async Task ListEligibleApprovers_returns_only_active_users_with_the_capability_in_name_order()
    {
        var alice = NewUser("Alice Approver", _approverRoleId);
        var bob = NewUser("Bob Approver", _approverRoleId);
        var inactive = NewUser("Inactive Approver", _approverRoleId, isActive: false);
        var noApproverRole = NewRole("Meeting test member");
        var notAnApprover = NewUser("Not An Approver", noApproverRole.Id);
        _db.Roles.Add(noApproverRole);
        _db.Users.AddRange(alice, bob, inactive, notAnApprover);
        _db.RoleCapabilities.Add(new RoleCapability
        {
            RoleId = noApproverRole.Id,
            Capability = Capability.MinutesEditor,
        });
        await _db.SaveChangesAsync();

        var result = await new MeetingRepository(_db).ListEligibleApproversAsync();

        Assert.Equal(
            ["Alice Approver", "Bob Approver", "Meeting Test Creator"],
            result.Select(option => option.FullName));
        Assert.Equal(alice.Id, result[0].Id);
        Assert.Equal(bob.Id, result[1].Id);
        Assert.Equal(_creatorId, result[2].Id);
    }

    [Fact]
    public async Task IsEligibleApprover_requires_an_active_account_with_the_capability()
    {
        var inactive = NewUser("Inactive Approver", _approverRoleId, isActive: false);
        var noApproverRole = NewRole("Meeting test member");
        var notAnApprover = NewUser("Not An Approver", noApproverRole.Id);
        _db.Roles.Add(noApproverRole);
        _db.Users.AddRange(inactive, notAnApprover);
        await _db.SaveChangesAsync();

        var repository = new MeetingRepository(_db);

        Assert.True(await repository.IsEligibleApproverAsync(_creatorId));
        Assert.False(await repository.IsEligibleApproverAsync(inactive.Id));
        Assert.False(await repository.IsEligibleApproverAsync(notAnApprover.Id));
        Assert.False(await repository.IsEligibleApproverAsync(Guid.CreateVersion7()));
    }

    [Fact]
    public async Task FindAccessible_applies_lifecycle_visibility_to_approvers_and_participants()
    {
        var creator = await _db.Users.SingleAsync(user => user.Id == _creatorId);
        var approver = NewUser("Meeting Approver", _approverRoleId);
        var participant = NewUser("Meeting Participant", _approverRoleId);
        var unrelated = NewUser("Unrelated User", _approverRoleId);
        _db.Users.AddRange(approver, participant, unrelated);

        var statuses = Enum.GetValues<MeetingStatus>();
        var meetings = statuses.Select(status => new Meeting
        {
            Title = $"Accessible meeting {status}",
            Description = string.Empty,
            Status = status,
            Sensitivity = Sensitivity.Internal,
            Mode = MeetingMode.InPerson,
            StartsAt = new DateTimeOffset(2026, 10, 1, 2, 0, 0, TimeSpan.Zero),
            CreatedById = creator.Id,
            AssignedApproverId = approver.Id,
            Participants =
            [
                new MeetingParticipant
                {
                    UserId = participant.Id,
                    Role = MeetingRole.Member,
                },
            ],
        }).ToArray();
        _db.Meetings.AddRange(meetings);
        await _db.SaveChangesAsync();

        var repository = new MeetingRepository(_db);

        foreach (var meeting in meetings)
        {
            Assert.NotNull(await repository.FindAccessibleAsync(meeting.Id, creator.Id));

            var approverCanRead = meeting.Status != MeetingStatus.Draft;
            Assert.Equal(approverCanRead, await repository.FindAccessibleAsync(meeting.Id, approver.Id) is not null);

            var participantCanRead = meeting.Status is MeetingStatus.Published
                or MeetingStatus.InProgress
                or MeetingStatus.Ended
                or MeetingStatus.MinutesIssued
                or MeetingStatus.Archived;
            Assert.Equal(participantCanRead, await repository.FindAccessibleAsync(meeting.Id, participant.Id) is not null);

            Assert.Null(await repository.FindAccessibleAsync(meeting.Id, unrelated.Id));
        }
    }
}

/// <summary>Lifecycle visibility query coverage that does not require PostgreSQL to create a database.</summary>
public sealed class MeetingRepositoryVisibilityTests : IDisposable
{
    private readonly TrustCabinetDbContext _db = new(
        new DbContextOptionsBuilder<TrustCabinetDbContext>()
            .UseInMemoryDatabase($"meeting-visibility-{Guid.CreateVersion7()}")
            .Options);

    public void Dispose() => _db.Dispose();

    [Fact]
    public async Task FindAccessible_filters_approvers_and_participants_by_lifecycle_status()
    {
        var creatorId = Guid.CreateVersion7();
        var approverId = Guid.CreateVersion7();
        var participantId = Guid.CreateVersion7();
        var unrelatedId = Guid.CreateVersion7();
        var meetings = Enum.GetValues<MeetingStatus>().Select(status => new Meeting
        {
            Title = $"Visibility test {status}",
            Description = string.Empty,
            Status = status,
            Sensitivity = Sensitivity.Normal,
            Mode = MeetingMode.InPerson,
            StartsAt = new DateTimeOffset(2026, 10, 1, 2, 0, 0, TimeSpan.Zero),
            CreatedById = creatorId,
            AssignedApproverId = approverId,
            Participants =
            [
                new MeetingParticipant
                {
                    UserId = participantId,
                    Role = MeetingRole.Member,
                },
            ],
        }).ToArray();
        _db.Meetings.AddRange(meetings);
        await _db.SaveChangesAsync();

        var repository = new MeetingRepository(_db);
        foreach (var meeting in meetings)
        {
            Assert.NotNull(await repository.FindAccessibleAsync(meeting.Id, creatorId));
            Assert.Equal(
                meeting.Status != MeetingStatus.Draft,
                await repository.FindAccessibleAsync(meeting.Id, approverId) is not null);
            var participantCanRead = meeting.Status is MeetingStatus.Published
                or MeetingStatus.InProgress
                or MeetingStatus.Ended
                or MeetingStatus.MinutesIssued
                or MeetingStatus.Archived;
            Assert.Equal(
                participantCanRead,
                await repository.FindAccessibleAsync(meeting.Id, participantId) is not null);
            Assert.Null(await repository.FindAccessibleAsync(meeting.Id, unrelatedId));
        }
    }
}
