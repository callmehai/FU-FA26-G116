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

    private static MeetingInput Input(string title = "PostgreSQL meeting")
        => new(
            title,
            "2026-10-01T09:00:00+07:00",
            "2026-10-01T10:00:00+07:00",
            "Online",
            "Conference link",
            "Confidential",
            "Integration test");

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
}
