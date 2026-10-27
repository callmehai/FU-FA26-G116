using Microsoft.EntityFrameworkCore;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Npgsql;
using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using TrustCabinet.Domain.Authentication;
using TrustCabinet.Domain.Entities;
using TrustCabinet.Domain.Enums;
using TrustCabinet.Domain.Meetings;
using TrustCabinet.Domain.Notifications;
using TrustCabinet.Infrastructure;
using TrustCabinet.Infrastructure.Meetings;
using TrustCabinet.Infrastructure.Email;
using TrustCabinet.Infrastructure.Seed;

namespace TrustCabinet.Web.Tests.Meetings;

/// <summary>PostgreSQL integration checks for meeting transactions, row locks and audit constraints.</summary>
public sealed class MeetingRepositoryTests : IAsyncLifetime
{
    private sealed class PostgreSqlMeetingApiFactory(string connectionString) : TrustCabinetApiFactory
    {
        protected override void ConfigureWebHost(IWebHostBuilder builder)
        {
            base.ConfigureWebHost(builder);
            builder.UseSetting("Email:Smtp:Enabled", "false");
            builder.ConfigureServices(services =>
            {
                services.RemoveAll<DbContextOptions<TrustCabinetDbContext>>();
                services.RemoveAll<TrustCabinetDbContext>();
                services.AddDbContext<TrustCabinetDbContext>(options => options
                    .UseNpgsql(connectionString)
                    .UseSnakeCaseNamingConvention());
                services.RemoveAll<IMeetingRepository>();
                services.AddScoped<IMeetingRepository, MeetingRepository>();
                services.RemoveAll<IMeetingService>();
                services.AddScoped<IMeetingService, MeetingService>();
                services.Configure<EmailDeliveryOptions>(options =>
                {
                    options.ApplicationBaseUrl = "https://hop.example.vn";
                    options.Smtp.FromAddress = "no-reply@example.vn";
                    options.Smtp.Host = "unused-in-test.example.vn";
                    // Enable the producer without registering the hosted SMTP worker.
                    options.Smtp.Enabled = true;
                });
            });
        }
    }

    private static readonly string AdminConnectionString =
        PostgresTestDatabase.ConnectionString("postgres");

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
            await _db.Database.MigrateAsync();

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
            _db.RoleCapabilities.AddRange(
                new RoleCapability { RoleId = role.Id, Capability = Capability.MeetingApprover },
                new RoleCapability { RoleId = role.Id, Capability = Capability.MeetingCreator });
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

        await using (var poolKey = new NpgsqlConnection(_connectionString))
        {
            NpgsqlConnection.ClearPool(poolKey);
        }

        await using var admin = new NpgsqlConnection(AdminConnectionString);
        await admin.OpenAsync();
        await using var drop = admin.CreateCommand();
        drop.CommandText = $"DROP DATABASE IF EXISTS \"{_databaseName}\"";
        await drop.ExecuteNonQueryAsync();
    }

    private MeetingService NewService(TrustCabinetDbContext? db = null, bool smtpEnabled = true)
    {
        var context = db ?? _db;
        return new MeetingService(new MeetingRepository(context), NullLogger<MeetingService>.Instance);
    }

    private async Task MaterializeInvitations(Guid meetingId, bool smtpEnabled = true)
    {
        await using var db = NewContext();
        var processor = new MeetingInvitationDispatchProcessor(db, Options.Create(new EmailDeliveryOptions
        {
            ApplicationBaseUrl = "https://hop.example.vn",
            Smtp = new() { Enabled = smtpEnabled, FromAddress = "no-reply@example.vn" }
        }), TimeProvider.System, new TrustCabinet.Infrastructure.Participants.UnavailableGuestInvitationLinkProvider(),
            NullLogger<MeetingInvitationDispatchProcessor>.Instance);
        var intents = await db.MeetingInvitationDispatches.Where(d => d.MeetingId == meetingId).Select(d => d.Id).ToArrayAsync();
        Assert.NotEmpty(intents);
        foreach (var id in intents) await processor.ProcessAsync(id, default);
    }

    private static readonly DateTimeOffset FutureStart =
        new(DateTimeOffset.UtcNow.Date.AddDays(7).AddHours(2), TimeSpan.Zero);

    private MeetingInput Input(string title = "PostgreSQL meeting")
        => new(
            title,
            FutureStart.ToString("O", System.Globalization.CultureInfo.InvariantCulture),
            FutureStart.AddHours(1).ToString("O", System.Globalization.CultureInfo.InvariantCulture),
            "Online",
            "Conference link",
            null,
            "Confidential",
            "Integration test");

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

    private static MeetingListQuery ListQuery(
        MeetingStatus? status = null,
        Sensitivity? sensitivity = null,
        string? search = null,
        DateTimeOffset? from = null,
        DateTimeOffset? to = null,
        int page = 1,
        int pageSize = 20,
        MeetingSortBy sortBy = MeetingSortBy.CreatedAt,
        MeetingSortDirection direction = MeetingSortDirection.Desc)
        => new(status, sensitivity, search, from, to, page, pageSize, sortBy, direction);

    private Meeting ListMeeting(
        string title,
        MeetingStatus status,
        DateTimeOffset startsAt,
        Sensitivity sensitivity = Sensitivity.Normal,
        Guid? approverId = null,
        Guid? participantId = null,
        DateTimeOffset? createdAt = null)
    {
        var meeting = new Meeting
        {
            Title = title,
            Status = status,
            Sensitivity = sensitivity,
            Mode = MeetingMode.InPerson,
            CreatedAt = createdAt ?? startsAt.AddDays(-1),
            UpdatedAt = createdAt ?? startsAt.AddDays(-1),
            PlannedStartTime = startsAt,
            PlannedEndTime = startsAt.AddHours(1),
            CreatedById = _creatorId,
            AssignedApproverId = approverId,
        };
        if (participantId is { } id)
        {
            meeting.Participants.Add(new MeetingParticipant
            {
                UserId = id,
                Role = MeetingRole.Member,
            });
        }

        return meeting;
    }

    [Fact]
    public async Task Create_persists_the_meeting_and_initial_history_as_one_operation()
    {
        var result = await NewService().CreateAsync(Input(), _creatorId, [new(_creatorId, "Chair")]);

        Assert.True(result.Succeeded);
        await using var verify = NewContext();
        var meeting = await verify.Meetings.SingleAsync(m => m.Id == result.Meeting!.Id);
        Assert.Equal("PostgreSQL meeting", meeting.Title);
        Assert.Equal(MeetingStatus.Draft, meeting.Status);
        Assert.Equal(_creatorId, meeting.CreatedById);

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

        var result = await NewService().CreateAsync(Input(), _creatorId, [new(_creatorId, "Chair")]);

        Assert.Equal(MeetingOutcome.SaveFailed, result.Outcome);
        Assert.Equal(0, await _db.Meetings.CountAsync());
        Assert.Equal(0, await _db.MeetingStatusHistory.CountAsync());
    }

    [Fact]
    public async Task PostgreSql_rejects_history_update_delete_truncate_and_meeting_deletion()
    {
        var created = await NewService().CreateAsync(Input(), _creatorId, [new(_creatorId, "Chair")]);
        Assert.True(created.Succeeded);
        var history = await _db.MeetingStatusHistory.SingleAsync();
        var tampered = "tampered";

        await Assert.ThrowsAsync<PostgresException>(async () =>
            await _db.Database.ExecuteSqlInterpolatedAsync(
                $"UPDATE meeting_status_history SET reason = {tampered} WHERE id = {history.Id}"));

        await Assert.ThrowsAsync<PostgresException>(async () =>
            await _db.Database.ExecuteSqlRawAsync("TRUNCATE TABLE meeting_status_history"));

        await Assert.ThrowsAsync<PostgresException>(async () =>
            await _db.Database.ExecuteSqlInterpolatedAsync(
                $"DELETE FROM meeting_status_history WHERE id = {history.Id}"));
        Assert.Equal(1, await _db.MeetingStatusHistory.CountAsync());

        await Assert.ThrowsAsync<PostgresException>(async () =>
            await _db.Database.ExecuteSqlInterpolatedAsync(
                $"DELETE FROM meetings WHERE id = {created.Meeting!.Id}"));

        Assert.Equal(1, await _db.MeetingStatusHistory.CountAsync());
    }

    [Fact]
    public async Task Workflow_migration_backfills_known_actions_and_marks_unclassifiable_rows_legacy()
    {
        // Start the historical migration test on its own old-schema database; SCRUM-98 is irreversible.
        await using var legacyDatabase = new PostgreSqlTestDatabase(AdminConnectionString);
        await using var legacyDb = new TrustCabinetDbContext(new DbContextOptionsBuilder<TrustCabinetDbContext>()
            .UseNpgsql(legacyDatabase.DataSource).UseSnakeCaseNamingConvention().Options);
        await legacyDb.Database.MigrateAsync("20260924150426_MeetingApproverAndOptionalEndTime");
        var legacyUser = new User { FullName = "Legacy author", Email = "legacy@migration.test", PasswordHash = "test-only" };
        legacyDb.Users.Add(legacyUser);
        await legacyDb.SaveChangesAsync();
        var meeting = new Meeting
        {
            Title = "Legacy history migration",
            Description = string.Empty,
            Status = MeetingStatus.Published,
            Sensitivity = Sensitivity.Internal,
            Mode = MeetingMode.InPerson,
            Location = string.Empty,
            CreatedById = legacyUser.Id,
        };
        var legacySchedule = new DateTimeOffset(2026, 10, 1, 2, 0, 0, TimeSpan.Zero);
        await legacyDb.Database.ExecuteSqlInterpolatedAsync($"""
            INSERT INTO meetings (
                id, title, description, status, sensitivity, mode, location,
                starts_at, ends_at, created_by_id, assigned_approver_id, created_at, updated_at)
            VALUES (
                {meeting.Id}, {meeting.Title}, {meeting.Description}, {meeting.Status.ToString()},
                {meeting.Sensitivity.ToString()}, {meeting.Mode.ToString()}, {meeting.Location},
                {legacySchedule}, {legacySchedule.AddHours(1)}, {legacyUser.Id}, {null},
                {legacySchedule.AddDays(-1)}, {legacySchedule.AddDays(-1)})
            """);

        var createHistoryId = Guid.CreateVersion7();
        var legacyHistoryId = Guid.CreateVersion7();
        var occurredAt = new DateTimeOffset(2026, 9, 20, 10, 0, 0, TimeSpan.Zero);
        var reason = "Giữ nguyên lý do cũ";
        await legacyDb.Database.ExecuteSqlInterpolatedAsync($"""
            INSERT INTO meeting_status_history
                (id, meeting_id, from_status, to_status, actor_id, reason, occurred_at, created_at, updated_at)
            VALUES
                ({createHistoryId}, {meeting.Id}, {null}, {"Draft"}, {legacyUser.Id}, {reason}, {occurredAt}, {occurredAt}, {occurredAt}),
                ({legacyHistoryId}, {meeting.Id}, {"Draft"}, {"Published"}, {legacyUser.Id}, {string.Empty}, {occurredAt.AddMinutes(1)}, {occurredAt}, {occurredAt})
            """);

        await legacyDb.Database.MigrateAsync();
        legacyDb.ChangeTracker.Clear();
        var rows = await legacyDb.MeetingStatusHistory
            .Where(item => item.MeetingId == meeting.Id)
            .ToDictionaryAsync(item => item.Id);

        Assert.Equal(MeetingHistoryAction.Create, rows[createHistoryId].Action);
        Assert.Equal(MeetingHistoryAction.LegacyTransition, rows[legacyHistoryId].Action);
        Assert.Null(rows[createHistoryId].AssignedApproverId);
        Assert.Equal(reason, rows[createHistoryId].Reason);
        Assert.Equal(occurredAt, rows[createHistoryId].OccurredAt);
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public async Task Http_withdrawal_recovers_an_ineligible_approver_and_resubmits_to_a_new_one(bool deactivate)
    {
        var oldApprover = NewUser("Blocked approver", _approverRoleId);
        var newApprover = NewUser("Replacement approver", _approverRoleId);
        _db.Users.AddRange(oldApprover, newApprover);
        await _db.SaveChangesAsync();
        var created = await NewService().CreateAsync(Input(), _creatorId, [new(_creatorId, "Chair")]);
        var meetingId = created.Meeting!.Id;
        _db.AgendaItems.Add(new AgendaItem { MeetingId = meetingId, Title = "Báo cáo", Position = 1 });
        await _db.SaveChangesAsync();
        Assert.True((await NewService().TransitionAsync(meetingId,
            new MeetingTransitionCommand(MeetingTransitionAction.SubmitForApproval, oldApprover.Id), _creatorId)).Succeeded);

        if (deactivate)
            oldApprover.IsActive = false;
        else
        {
            var roleWithoutApproval = new Role { Name = "No approval capability" };
            _db.Roles.Add(roleWithoutApproval);
            oldApprover.RoleId = roleWithoutApproval.Id;
        }
        await _db.SaveChangesAsync();
        using var app = new PostgreSqlMeetingApiFactory(_connectionString);
        using var client = app.CreateClient();
        var organizer = await _db.Users.SingleAsync(user => user.Id == _creatorId);
        var issuer = app.Services.GetRequiredService<ITokenIssuer>();
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer",
            issuer.CreateAccessToken(organizer, [Capability.MeetingCreator]));

        var withdrawal = await client.PostAsJsonAsync($"/api/meetings/{meetingId}/transitions",
            new { action = "ReturnToDraft" });
        Assert.Equal(HttpStatusCode.OK, withdrawal.StatusCode);
        await using var verify = NewContext();
        var draft = await verify.Meetings.AsNoTracking().SingleAsync(meeting => meeting.Id == meetingId);
        Assert.Equal(MeetingStatus.Draft, draft.Status);
        Assert.Null(draft.AssignedApproverId);
        var history = await verify.MeetingStatusHistory.AsNoTracking()
            .Where(item => item.MeetingId == meetingId).OrderBy(item => item.OccurredAt).ThenBy(item => item.Id).ToListAsync();
        Assert.Equal(3, history.Count);
        Assert.Equal(oldApprover.Id, history[1].AssignedApproverId);
        Assert.Equal(MeetingHistoryAction.ReturnToDraft, history[2].Action);
        Assert.Equal(MeetingStatus.WaitingApproval, history[2].FromStatus);
        Assert.Equal(MeetingStatus.Draft, history[2].ToStatus);
        Assert.Equal(_creatorId, history[2].ActorId);
        Assert.Equal(1, await verify.Notifications.CountAsync(item => item.MeetingId == meetingId));
        Assert.False(await verify.EmailMessages.AnyAsync(item => item.MeetingId == meetingId));

        var retry = await client.PostAsJsonAsync($"/api/meetings/{meetingId}/transitions",
            new { action = "ReturnToDraft" });
        Assert.Equal(HttpStatusCode.Conflict, retry.StatusCode);
        var resubmit = await client.PostAsJsonAsync($"/api/meetings/{meetingId}/transitions",
            new { action = "SubmitForApproval", assignedApproverId = newApprover.Id });
        Assert.Equal(HttpStatusCode.OK, resubmit.StatusCode);
        var waiting = await verify.Meetings.AsNoTracking().SingleAsync(meeting => meeting.Id == meetingId);
        Assert.Equal(MeetingStatus.WaitingApproval, waiting.Status);
        Assert.Equal(newApprover.Id, waiting.AssignedApproverId);
        Assert.Equal(4, await verify.MeetingStatusHistory.CountAsync(item => item.MeetingId == meetingId));
        Assert.Equal(1, await verify.Notifications.CountAsync(item =>
            item.MeetingId == meetingId && item.UserId == newApprover.Id));
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public async Task Http_withdrawal_recovers_legacy_missing_or_self_assignment_and_preserves_immutable_history(bool selfAssignment)
    {
        var newApprover = NewUser("Replacement for self assignment", _approverRoleId);
        _db.Users.Add(newApprover);
        Guid? legacyApproverId = selfAssignment ? _creatorId : null;
        var meeting = ListMeeting("Legacy self assignment", MeetingStatus.WaitingApproval,
            new DateTimeOffset(2026, 10, 1, 2, 0, 0, TimeSpan.Zero), approverId: legacyApproverId);
        meeting.AgendaItems.Add(new AgendaItem { Title = "Báo cáo", Position = 1 });
        _db.Meetings.Add(meeting);
        var originalHistory = new MeetingStatusHistory
        {
            Meeting = meeting,
            Action = MeetingHistoryAction.SubmitForApproval,
            FromStatus = MeetingStatus.Draft,
            ToStatus = MeetingStatus.WaitingApproval,
            ActorId = _creatorId,
            AssignedApproverId = legacyApproverId,
            OccurredAt = DateTimeOffset.UtcNow.AddMinutes(-1),
        };
        _db.MeetingStatusHistory.Add(originalHistory);
        await _db.SaveChangesAsync();

        using var app = new PostgreSqlMeetingApiFactory(_connectionString);
        using var client = app.CreateClient();
        var organizer = await _db.Users.SingleAsync(user => user.Id == _creatorId);
        var issuer = app.Services.GetRequiredService<ITokenIssuer>();
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer",
            issuer.CreateAccessToken(organizer, [Capability.MeetingCreator, Capability.MeetingApprover]));

        var withdrawal = await client.PostAsJsonAsync($"/api/meetings/{meeting.Id}/transitions",
            new { action = "ReturnToDraft" });
        Assert.Equal(HttpStatusCode.OK, withdrawal.StatusCode);

        await using var verify = NewContext();
        var draft = await verify.Meetings.AsNoTracking().SingleAsync(item => item.Id == meeting.Id);
        Assert.Equal(MeetingStatus.Draft, draft.Status);
        Assert.Null(draft.AssignedApproverId);
        var history = await verify.MeetingStatusHistory.AsNoTracking()
            .Where(item => item.MeetingId == meeting.Id).OrderBy(item => item.OccurredAt).ThenBy(item => item.Id).ToListAsync();
        Assert.Equal(2, history.Count);
        Assert.Equal(originalHistory.Id, history[0].Id);
        Assert.Equal(legacyApproverId, history[0].AssignedApproverId);
        Assert.Equal(MeetingHistoryAction.ReturnToDraft, history[1].Action);
        Assert.Equal(MeetingStatus.WaitingApproval, history[1].FromStatus);
        Assert.Equal(MeetingStatus.Draft, history[1].ToStatus);
        Assert.Equal(_creatorId, history[1].ActorId);
        Assert.False(await verify.Notifications.AnyAsync(item => item.MeetingId == meeting.Id));
        Assert.False(await verify.EmailMessages.AnyAsync(item => item.MeetingId == meeting.Id));
        await Assert.ThrowsAsync<PostgresException>(async () =>
            await verify.Database.ExecuteSqlInterpolatedAsync(
                $"UPDATE meeting_status_history SET reason = {"changed"} WHERE id = {history[1].Id}"));

        var resubmit = await client.PostAsJsonAsync($"/api/meetings/{meeting.Id}/transitions",
            new { action = "SubmitForApproval", assignedApproverId = newApprover.Id });
        Assert.Equal(HttpStatusCode.OK, resubmit.StatusCode);
        var waiting = await verify.Meetings.AsNoTracking().SingleAsync(item => item.Id == meeting.Id);
        Assert.Equal(MeetingStatus.WaitingApproval, waiting.Status);
        Assert.Equal(newApprover.Id, waiting.AssignedApproverId);
        Assert.Equal(3, await verify.MeetingStatusHistory.CountAsync(item => item.MeetingId == meeting.Id));
        Assert.Equal(1, await verify.Notifications.CountAsync(item =>
            item.MeetingId == meeting.Id && item.UserId == newApprover.Id));
        Assert.False(await verify.EmailMessages.AnyAsync(item => item.MeetingId == meeting.Id));
    }

    [Fact]
    public async Task Http_administrator_transfers_an_approved_meeting_and_new_organizer_publishes_with_immutable_history()
    {
        var adminRole = NewRole("Recovery administrator");
        var administrator = NewUser("Recovery Administrator", adminRole.Id);
        var replacement = NewUser("Replacement Organizer", _approverRoleId);
        var approver = NewUser("Original Assigned Approver", _approverRoleId);
        _db.Roles.Add(adminRole);
        _db.Users.AddRange(administrator, replacement, approver);
        _db.RoleCapabilities.Add(new RoleCapability { RoleId = adminRole.Id, Capability = Capability.AdminUsers });
        await _db.SaveChangesAsync();
        var created = await NewService().CreateAsync(Input("Organizer recovery"), _creatorId, [new(replacement.Id, "Chair")]);
        var meetingId = created.Meeting!.Id;
        _db.AgendaItems.Add(new AgendaItem { MeetingId = meetingId, Title = "Báo cáo", Position = 1 });
        _db.MeetingParticipants.Add(new MeetingParticipant
        {
            MeetingId = meetingId,
            UserId = approver.Id,
            Role = MeetingRole.Secretary,
            Response = InvitationResponse.Pending,
        });
        await _db.SaveChangesAsync();
        Assert.True((await NewService().TransitionAsync(meetingId,
            new MeetingTransitionCommand(MeetingTransitionAction.SubmitForApproval, approver.Id), _creatorId)).Succeeded);
        Assert.True((await NewService().TransitionAsync(meetingId,
            new MeetingTransitionCommand(MeetingTransitionAction.Approve), approver.Id)).Succeeded);
        var originalHistory = await _db.MeetingStatusHistory.AsNoTracking()
            .Where(item => item.MeetingId == meetingId).OrderBy(item => item.Id).ToListAsync();
        var oldOrganizer = await _db.Users.SingleAsync(user => user.Id == _creatorId);
        oldOrganizer.IsActive = false;
        await _db.SaveChangesAsync();

        using var app = new PostgreSqlMeetingApiFactory(_connectionString);
        using var administratorClient = app.CreateClient();
        using var replacementClient = app.CreateClient();
        var issuer = app.Services.GetRequiredService<ITokenIssuer>();
        administratorClient.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer",
            issuer.CreateAccessToken(administrator, [Capability.AdminUsers]));
        replacementClient.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer",
            issuer.CreateAccessToken(replacement, [Capability.MeetingCreator]));

        var transferred = await administratorClient.PostAsJsonAsync($"/api/meetings/{meetingId}/transitions",
            new { action = "TransferOrganizer", organizerId = replacement.Id });
        Assert.Equal(HttpStatusCode.OK, transferred.StatusCode);
        using var responseBody = System.Text.Json.JsonDocument.Parse(await transferred.Content.ReadAsStringAsync());
        Assert.Equal("MSG89", responseBody.RootElement.GetProperty("code").GetString());
        Assert.Equal(meetingId, responseBody.RootElement.GetProperty("meetingId").GetGuid());
        Assert.Equal("Approved", responseBody.RootElement.GetProperty("status").GetString());
        Assert.Equal(replacement.Id, responseBody.RootElement.GetProperty("organizerId").GetGuid());
        Assert.Equal(
            ["code", "history", "meetingId", "message", "organizerId", "status"],
            responseBody.RootElement.EnumerateObject().Select(property => property.Name).Order(StringComparer.Ordinal));
        Assert.DoesNotContain("Organizer recovery", responseBody.RootElement.GetRawText(), StringComparison.Ordinal);
        Assert.Equal(_creatorId, responseBody.RootElement.GetProperty("history").GetProperty("previousOrganizerId").GetGuid());
        Assert.Equal(replacement.Id, responseBody.RootElement.GetProperty("history").GetProperty("newOrganizerId").GetGuid());

        await using var verify = NewContext();
        var recovered = await verify.Meetings.AsNoTracking().SingleAsync(item => item.Id == meetingId);
        Assert.Equal(MeetingStatus.Approved, recovered.Status);
        Assert.Equal(replacement.Id, recovered.CreatedById);
        Assert.Equal(approver.Id, recovered.AssignedApproverId);
        var transfer = await verify.MeetingStatusHistory.AsNoTracking().SingleAsync(item =>
            item.MeetingId == meetingId && item.Action == MeetingHistoryAction.TransferOrganizer);
        Assert.Equal(MeetingStatus.Approved, transfer.FromStatus);
        Assert.Equal(MeetingStatus.Approved, transfer.ToStatus);
        Assert.Equal(administrator.Id, transfer.ActorId);
        Assert.Equal(_creatorId, transfer.PreviousOrganizerId);
        Assert.Equal(replacement.Id, transfer.NewOrganizerId);
        Assert.Equal(4, await verify.MeetingStatusHistory.CountAsync(item => item.MeetingId == meetingId));
        // Submit → approver, Approve → organizer; the transfer itself notifies no one.
        Assert.Equal(2, await verify.Notifications.CountAsync(item => item.MeetingId == meetingId));
        Assert.False(await verify.EmailMessages.AnyAsync(item => item.MeetingId == meetingId));
        Assert.Equal(HttpStatusCode.NotFound,
            (await administratorClient.GetAsync($"/api/meetings/{meetingId}")).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound,
            (await administratorClient.GetAsync($"/api/meetings/{meetingId}/history")).StatusCode);

        await Assert.ThrowsAsync<PostgresException>(async () =>
            await verify.Database.ExecuteSqlInterpolatedAsync(
                $"UPDATE meeting_status_history SET new_organizer_id = {_creatorId} WHERE id = {transfer.Id}"));
        await Assert.ThrowsAsync<PostgresException>(async () =>
            await verify.Database.ExecuteSqlInterpolatedAsync(
                $"DELETE FROM meeting_status_history WHERE id = {transfer.Id}"));
        var persistedOriginal = await verify.MeetingStatusHistory.AsNoTracking()
            .Where(item => item.MeetingId == meetingId && item.Action != MeetingHistoryAction.TransferOrganizer)
            .OrderBy(item => item.Id).ToListAsync();
        Assert.Equal(originalHistory.Select(item => item.Id), persistedOriginal.Select(item => item.Id));
        for (var index = 0; index < originalHistory.Count; index++)
        {
            Assert.Equal(originalHistory[index].ActorId, persistedOriginal[index].ActorId);
            Assert.Equal(originalHistory[index].Action, persistedOriginal[index].Action);
            Assert.Equal(originalHistory[index].OccurredAt, persistedOriginal[index].OccurredAt);
            Assert.Equal(originalHistory[index].AssignedApproverId, persistedOriginal[index].AssignedApproverId);
            Assert.Null(persistedOriginal[index].PreviousOrganizerId);
            Assert.Null(persistedOriginal[index].NewOrganizerId);
        }

        var published = await replacementClient.PostAsJsonAsync($"/api/meetings/{meetingId}/transitions",
            new { action = "Publish" });
        Assert.Equal(HttpStatusCode.OK, published.StatusCode);
        Assert.Equal(MeetingStatus.Published,
            await verify.Meetings.Where(item => item.Id == meetingId).Select(item => item.Status).SingleAsync());
        Assert.Equal(5, await verify.MeetingStatusHistory.CountAsync(item => item.MeetingId == meetingId));
        await MaterializeInvitations(meetingId);
        var emails = await verify.EmailMessages.Where(item => item.MeetingId == meetingId).ToListAsync();
        Assert.Equal(2, emails.Count);
        Assert.Contains(emails, email => email.ToAddress == replacement.Email && email.Status == EmailStatus.Queued);
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public async Task Lifecycle_round_trip_persists_history_and_queues_invitations_only_when_SMTP_is_enabled(bool smtpEnabled)
    {
        var approver = NewUser("Workflow approver", _approverRoleId);
        _db.Users.Add(approver);
        await _db.SaveChangesAsync();
        var created = await NewService().CreateAsync(Input(), _creatorId, [new(_creatorId, "Chair")]);
        Assert.True(created.Succeeded);
        var meetingId = created.Meeting!.Id;
        _db.AgendaItems.Add(new AgendaItem
        {
            MeetingId = meetingId,
            Position = 1,
            Title = "Báo cáo",
            PresenterId = _creatorId,
        });

        var inactive = NewUser("Inactive recipient", _approverRoleId, isActive: false);
        var delegated = NewUser("Delegated recipient", _approverRoleId);
        var member = NewUser("Member recipient", _approverRoleId);
        _db.Users.AddRange(inactive, delegated, member);
        await _db.SaveChangesAsync();
        _db.MeetingParticipants.AddRange(
            new MeetingParticipant { MeetingId = meetingId, UserId = inactive.Id },
            new MeetingParticipant { MeetingId = meetingId, UserId = member.Id, Role = MeetingRole.Secretary },
            new MeetingParticipant
            {
                MeetingId = meetingId,
                UserId = delegated.Id,
                Role = MeetingRole.Member,
                Response = InvitationResponse.Delegated,
                DelegatedToUserId = member.Id,
            });
        await _db.SaveChangesAsync();

        var service = NewService(smtpEnabled: smtpEnabled);
        var submit = await service.TransitionAsync(
            meetingId,
            new MeetingTransitionCommand(MeetingTransitionAction.SubmitForApproval, approver.Id),
            _creatorId);
        var reject = await service.TransitionAsync(
            meetingId,
            new MeetingTransitionCommand(MeetingTransitionAction.Reject, Reason: "Bổ sung số liệu"),
            approver.Id);
        var returnToDraft = await service.TransitionAsync(
            meetingId,
            new MeetingTransitionCommand(MeetingTransitionAction.ReturnToDraft),
            _creatorId);
        var resubmit = await service.TransitionAsync(
            meetingId,
            new MeetingTransitionCommand(MeetingTransitionAction.SubmitForApproval, approver.Id),
            _creatorId);
        var approve = await service.TransitionAsync(
            meetingId,
            new MeetingTransitionCommand(MeetingTransitionAction.Approve),
            approver.Id);
        var publish = await service.TransitionAsync(
            meetingId,
            new MeetingTransitionCommand(MeetingTransitionAction.Publish),
            _creatorId);

        Assert.All([submit, reject, returnToDraft, resubmit, approve, publish], result =>
            Assert.Equal(MeetingWorkflowOutcome.Success, result.Outcome));
        Assert.Equal(MeetingStatus.Published, publish.Meeting!.Status);

        await using var verify = NewContext();
        var history = await verify.MeetingStatusHistory
            .Where(item => item.MeetingId == meetingId)
            .OrderBy(item => item.OccurredAt)
            .ThenBy(item => item.Id)
            .ToListAsync();
        Assert.Equal(7, history.Count);
        Assert.Equal(
            [
                MeetingHistoryAction.Create,
                MeetingHistoryAction.SubmitForApproval,
                MeetingHistoryAction.Reject,
                MeetingHistoryAction.ReturnToDraft,
                MeetingHistoryAction.SubmitForApproval,
                MeetingHistoryAction.Approve,
                MeetingHistoryAction.Publish,
            ],
            history.Select(item => item.Action));
        Assert.Equal("Bổ sung số liệu", history.Single(item => item.Action == MeetingHistoryAction.Reject).Reason);
        Assert.All(history.Where(item => item.Action == MeetingHistoryAction.SubmitForApproval), item =>
            Assert.Equal(approver.Id, item.AssignedApproverId));

        await MaterializeInvitations(meetingId, smtpEnabled);
        var invitations = await verify.EmailMessages.Where(item => item.MeetingId == meetingId).ToListAsync();
        // Email reaches active non-delegated participants, including the publishing creator.
        Assert.Equal(smtpEnabled ? 2 : 0, invitations.Count);
        Assert.Equal(
            (smtpEnabled
                ? new[]
                {
                    created.Meeting!.Chair is not null
                        ? (await verify.MeetingParticipants.SingleAsync(p => p.MeetingId == meetingId && p.UserId == _creatorId)).InitialInvitationDeliveryKey!
                        : throw new InvalidOperationException(),
                    $"meeting:{meetingId:D}:published:{member.Id:D}",
                }
                : Array.Empty<string>()).Order(),
            invitations.Select(item => item.DeduplicationKey).Order());
        Assert.All(invitations, invitation =>
        {
        });
        var publishNotifications = await verify.Notifications
            .Where(item => item.MeetingId == meetingId && item.Type == NotificationType.MeetingInvited)
            .ToListAsync();
        // In-app skips the publisher (the creator here) and the delegated participant.
        Assert.Equal(member.Id, Assert.Single(publishNotifications).UserId);
        var organizerNotices = await verify.Notifications
            .Where(item => item.MeetingId == meetingId && item.UserId == _creatorId)
            .Select(item => item.Type)
            .ToListAsync();
        Assert.Equal(
            new[] { NotificationType.MeetingApproved, NotificationType.MeetingRejected }.Order(),
            organizerNotices.Order());
        Assert.Equal(history[^1].Id, publishNotifications[0].OriginHistoryId);
    }

    [Fact]
    public async Task History_endpoint_reads_while_another_transaction_holds_the_meeting_row_lock()
    {
        var created = await NewService().CreateAsync(Input(), _creatorId, [new(_creatorId, "Chair")]);
        Assert.True(created.Succeeded);
        var meetingId = created.Meeting!.Id;

        await using var blocker = new NpgsqlConnection(_connectionString);
        await blocker.OpenAsync();
        await using var transaction = await blocker.BeginTransactionAsync();
        await using (var locked = blocker.CreateCommand())
        {
            locked.Transaction = transaction;
            locked.CommandText = "SELECT id FROM meetings WHERE id = @id FOR UPDATE";
            locked.Parameters.AddWithValue("id", meetingId);
            await locked.ExecuteScalarAsync();
        }

        // A regression to FOR UPDATE fails quickly with lock_timeout while the blocker is held.
        var readerConnection = new NpgsqlConnectionStringBuilder(_connectionString)
        {
            Options = "-c lock_timeout=500ms",
            Pooling = false,
        }.ConnectionString;
        using var app = new PostgreSqlMeetingApiFactory(readerConnection);
        using var client = app.CreateClient();
        var user = await _db.Users.AsNoTracking().SingleAsync(item => item.Id == _creatorId);
        var issuer = app.Services.GetRequiredService<ITokenIssuer>();
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue(
            "Bearer", issuer.CreateAccessToken(user, []));

        var response = await client.GetAsync($"/api/meetings/{meetingId}/history");
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        using var body = System.Text.Json.JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        Assert.Equal("Create", Assert.Single(body.RootElement.GetProperty("items").EnumerateArray())
            .GetProperty("action").GetString());
        await transaction.RollbackAsync();
    }

    [Fact]
    public async Task Development_workflow_demo_is_ready_and_can_be_published_without_reseeding()
    {
        // This fixture owns its database; remove its placeholder user to exercise a fresh dev seed.
        await _db.Users.Where(user => user.Id == _creatorId).ExecuteDeleteAsync();
        await ReferenceDataSeeder.SeedAsync(_db);
        await DevelopmentDataSeeder.SeedAsync(_db);
        _db.ChangeTracker.Clear();
        var organizer = await _db.Users.SingleAsync(user => user.Email == "thuky@trustcabinet.local");
        var approver = await _db.Users.SingleAsync(user => user.Email == "chutoa@trustcabinet.local");
        var demo = await _db.Meetings.SingleAsync(meeting => meeting.Title == "SCRUM-19 — Luồng duyệt và công bố (demo)");
        var meetingId = demo.Id;
        Assert.Null(demo.AssignedApproverId);

        using var app = new PostgreSqlMeetingApiFactory(_connectionString);
        var issuer = app.Services.GetRequiredService<ITokenIssuer>();
        using var organizerClient = app.CreateClient();
        organizerClient.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue(
            "Bearer", issuer.CreateAccessToken(organizer, [Capability.MeetingCreator]));
        using var approverClient = app.CreateClient();
        approverClient.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue(
            "Bearer", issuer.CreateAccessToken(approver, [Capability.MeetingApprover]));

        var readiness = await organizerClient.GetAsync($"/api/meetings/{meetingId}/submission-readiness");
        Assert.Equal(HttpStatusCode.OK, readiness.StatusCode);
        using var readinessBody = System.Text.Json.JsonDocument.Parse(await readiness.Content.ReadAsStringAsync());
        Assert.True(readinessBody.RootElement.GetProperty("ready").GetBoolean());

        var submitted = await organizerClient.PostAsJsonAsync($"/api/meetings/{meetingId}/transitions",
            new { action = "SubmitForApproval", assignedApproverId = approver.Id });
        Assert.Equal(HttpStatusCode.OK, submitted.StatusCode);
        var approved = await approverClient.PostAsJsonAsync($"/api/meetings/{meetingId}/transitions",
            new { action = "Approve" });
        Assert.Equal(HttpStatusCode.OK, approved.StatusCode);
        var published = await organizerClient.PostAsJsonAsync($"/api/meetings/{meetingId}/transitions",
            new { action = "Publish" });
        Assert.Equal(HttpStatusCode.OK, published.StatusCode);

        _db.ChangeTracker.Clear();
        var count = await _db.Meetings.CountAsync();
        await DevelopmentDataSeeder.SeedAsync(_db);
        _db.ChangeTracker.Clear();
        Assert.Equal(count, await _db.Meetings.CountAsync());
        Assert.Equal(MeetingStatus.Published, (await _db.Meetings.SingleAsync(meeting => meeting.Id == meetingId)).Status);
        Assert.Equal(4, await _db.MeetingStatusHistory.CountAsync(history => history.MeetingId == meetingId));
        await MaterializeInvitations(meetingId);
        var invitations = await _db.EmailMessages.Where(email => email.MeetingId == meetingId).ToListAsync();
        Assert.Equal(3, invitations.Count);
        Assert.All(invitations, email =>
        {
            Assert.Equal(EmailStatus.Queued, email.Status);
            Assert.Null(email.SentAt);
        });
        Assert.Equal(3, invitations.Select(email => email.DeduplicationKey).Distinct().Count());
        Assert.All(invitations, email =>
        {
            Assert.False(string.IsNullOrWhiteSpace(email.DeduplicationKey));
            Assert.StartsWith("BEGIN:VCALENDAR", email.CalendarAttachment, StringComparison.Ordinal);
        });
        var expectedAddresses = invitations.Select(email => email.ToAddress).Order().ToArray();
        var expectedBodies = invitations.Select(email => email.Body).Order().ToArray();
        var expectedCalendars = invitations.Select(email => email.CalendarAttachment).Order().ToArray();
        await _db.Meetings.Where(meeting => meeting.Id == meetingId)
            .ExecuteUpdateAsync(setters => setters.SetProperty(meeting => meeting.Title, "Changed after commit"));
        var recipientIds = await _db.Users.Where(user => expectedAddresses.Contains(user.Email))
            .Select(user => user.Id).ToArrayAsync();
        await _db.Users.Where(user => recipientIds.Contains(user.Id)).ExecuteUpdateAsync(setters =>
            setters.SetProperty(user => user.IsActive, false));
        await _db.MeetingParticipants.Where(participant => participant.MeetingId == meetingId)
            .ExecuteUpdateAsync(setters => setters.SetProperty(participant => participant.Response, InvitationResponse.Delegated));

        await using var workerDb = NewContext();
        var sender = new RecordingInvitationSender();
        var processor = new EmailQueueProcessor(workerDb, sender, Options.Create(new EmailDeliveryOptions()),
            TimeProvider.System, NullLogger<EmailQueueProcessor>.Instance);
        for (var index = 0; index < 3; index++)
            Assert.Equal(EmailProcessingOutcome.Sent, (await processor.ProcessNextAsync()).Outcome);
        Assert.Equal(EmailProcessingOutcome.NoWork, (await processor.ProcessNextAsync()).Outcome);
        Assert.Equal(expectedAddresses, sender.Sent.Select(email => email.ToAddress).Order());
        Assert.Equal(expectedBodies, sender.Sent.Select(email => email.Body).Order());
        Assert.Equal(expectedCalendars, sender.Sent.Select(email => email.CalendarAttachment).Order());
        _db.ChangeTracker.Clear();
        Assert.All(await _db.EmailMessages.Where(email => email.MeetingId == meetingId).ToListAsync(), email =>
        {
            Assert.Equal(EmailStatus.Sent, email.Status);
            Assert.NotNull(email.SentAt);
        });
    }

    private sealed class RecordingInvitationSender : IEmailSender
    {
        public List<EmailMessage> Sent { get; } = [];
        public Task<string?> SendAsync(EmailMessage message, CancellationToken ct = default)
        {
            ct.ThrowIfCancellationRequested();
            Sent.Add(message);
            return Task.FromResult<string?>(null);
        }
    }

    [Fact]
    public async Task Publish_endpoint_commits_durable_dispatch_without_legacy_producer()
    {
        var created = await NewService().CreateAsync(Input(), _creatorId, [new(_creatorId, "Chair")]);
        var meetingId = created.Meeting!.Id;
        await _db.Meetings.Where(meeting => meeting.Id == meetingId)
            .ExecuteUpdateAsync(setters => setters.SetProperty(meeting => meeting.Status, MeetingStatus.Approved));
        var secretary = NewUser("Failure-path Secretary", _approverRoleId);
        _db.Users.Add(secretary);
        _db.MeetingParticipants.AddRange(
            new MeetingParticipant
            {
                MeetingId = meetingId,
                User = secretary,
                Role = MeetingRole.Secretary,
                Response = InvitationResponse.Pending,
            });
        await _db.SaveChangesAsync();
        using var app = new PostgreSqlMeetingApiFactory(_connectionString);
        using var client = app.CreateClient();
        var user = await _db.Users.AsNoTracking().SingleAsync(item => item.Id == _creatorId);
        var issuer = app.Services.GetRequiredService<ITokenIssuer>();
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue(
            "Bearer", issuer.CreateAccessToken(user, [Capability.MeetingCreator]));

        var response = await client.PostAsJsonAsync(
            $"/api/meetings/{meetingId}/transitions", new { action = "Publish" });
        using var body = System.Text.Json.JsonDocument.Parse(await response.Content.ReadAsStringAsync());

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal("MSG45", body.RootElement.GetProperty("code").GetString());
        await using var verify = NewContext();
        Assert.Equal(MeetingStatus.Published, (await verify.Meetings.SingleAsync(item => item.Id == meetingId)).Status);
        Assert.Equal(2, await verify.MeetingStatusHistory.CountAsync(item => item.MeetingId == meetingId));
        var publicationHistory = await verify.MeetingStatusHistory.SingleAsync(item =>
            item.MeetingId == meetingId && item.Action == MeetingHistoryAction.Publish);
        var notification = Assert.Single(
            await verify.Notifications.Where(item => item.MeetingId == meetingId).ToListAsync());
        Assert.Equal(NotificationType.MeetingInvited, notification.Type);
        Assert.Equal(secretary.Id, notification.UserId);
        Assert.Equal(publicationHistory.Id, notification.OriginHistoryId);
        Assert.Empty(await verify.EmailMessages.Where(item => item.MeetingId == meetingId).ToListAsync());
        Assert.Equal(2, await verify.MeetingInvitationDispatches.CountAsync(d => d.MeetingId == meetingId));
    }

    [Fact]
    public async Task Approve_endpoint_persists_transition_history_through_the_real_EF_repository()
    {
        var approver = NewUser("HTTP workflow approver", _approverRoleId);
        _db.Users.Add(approver);
        var meeting = new Meeting
        {
            Title = "EF-backed approval",
            Description = "Integration test",
            Status = MeetingStatus.WaitingApproval,
            Sensitivity = Sensitivity.Internal,
            Mode = MeetingMode.InPerson,
            Location = "Phòng A",
            CreatedById = _creatorId,
            AssignedApproverId = approver.Id,
        };
        _db.Meetings.Add(meeting);
        _db.MeetingStatusHistory.Add(new MeetingStatusHistory
        {
            MeetingId = meeting.Id,
            FromStatus = MeetingStatus.Draft,
            ToStatus = MeetingStatus.WaitingApproval,
            Action = MeetingHistoryAction.SubmitForApproval,
            ActorId = _creatorId,
            AssignedApproverId = approver.Id,
            OccurredAt = DateTimeOffset.UtcNow,
        });
        await _db.SaveChangesAsync();
        _db.ChangeTracker.Clear();

        using var app = new PostgreSqlMeetingApiFactory(_connectionString);
        using var client = app.CreateClient();
        var user = await _db.Users.AsNoTracking().SingleAsync(item => item.Id == approver.Id);
        var issuer = app.Services.GetRequiredService<ITokenIssuer>();
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue(
            "Bearer", issuer.CreateAccessToken(user, [Capability.MeetingApprover]));

        var response = await client.PostAsJsonAsync(
            $"/api/meetings/{meeting.Id}/transitions", new { action = "Approve" });
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        _db.ChangeTracker.Clear();
        var persisted = await _db.Meetings.AsNoTracking().SingleAsync(item => item.Id == meeting.Id);
        var approvalHistory = await _db.MeetingStatusHistory.AsNoTracking()
            .SingleAsync(item => item.MeetingId == meeting.Id && item.Action == MeetingHistoryAction.Approve);
        Assert.Equal(MeetingStatus.Approved, persisted.Status);
        Assert.Equal(MeetingStatus.WaitingApproval, approvalHistory.FromStatus);
        Assert.Equal(MeetingStatus.Approved, approvalHistory.ToStatus);
        Assert.Equal(approver.Id, approvalHistory.ActorId);
    }

    [Fact]
    public async Task Publish_persists_a_long_unicode_invitation_subject_without_corrupting_the_title_snapshot()
    {
        var approver = NewUser("Unicode workflow approver", _approverRoleId);
        _db.Users.Add(approver);
        var title = new string('a', 290) + string.Concat(Enumerable.Repeat("📋", 5));
        var service = NewService();
        var created = await service.CreateAsync(Input(title), _creatorId, [new(_creatorId, "Chair")]);
        Assert.True(created.Succeeded);
        var meetingId = created.Meeting!.Id;
        _db.AgendaItems.Add(new AgendaItem
        {
            MeetingId = meetingId,
            Position = 1,
            Title = "Báo cáo",
            PresenterId = _creatorId,
        });
        _db.MeetingParticipants.AddRange(
            new MeetingParticipant
            {
                MeetingId = meetingId,
                UserId = approver.Id,
                Role = MeetingRole.Secretary,
                Response = InvitationResponse.Pending,
            });
        await _db.SaveChangesAsync();

        Assert.Equal(MeetingWorkflowOutcome.Success, (await service.TransitionAsync(
            meetingId, new MeetingTransitionCommand(MeetingTransitionAction.SubmitForApproval, approver.Id),
            _creatorId)).Outcome);
        Assert.Equal(MeetingWorkflowOutcome.Success, (await service.TransitionAsync(
            meetingId, new MeetingTransitionCommand(MeetingTransitionAction.Approve), approver.Id)).Outcome);
        var published = await service.TransitionAsync(
            meetingId, new MeetingTransitionCommand(MeetingTransitionAction.Publish), _creatorId);

        Assert.Equal(MeetingWorkflowOutcome.Success, published.Outcome);
        _db.ChangeTracker.Clear();
        await MaterializeInvitations(meetingId);
        var emails = await _db.EmailMessages.Where(item => item.MeetingId == meetingId).ToListAsync();
        Assert.Equal(2, emails.Count);
        var creatorEmail = await _db.Users
            .Where(user => user.Id == _creatorId).Select(user => user.Email).SingleAsync();
        var email = Assert.Single(emails, item => item.ToAddress == creatorEmail);
        Assert.StartsWith("Lời mời tham dự cuộc họp: ", email.Subject);
        Assert.True(email.Subject.Length <= 300);
        Assert.DoesNotContain("\uFFFD", email.Subject);
        Assert.Contains(title, email.Body, StringComparison.Ordinal);
        Assert.StartsWith("BEGIN:VCALENDAR", email.CalendarAttachment, StringComparison.Ordinal);
        Assert.False(string.IsNullOrWhiteSpace(email.DeduplicationKey));
    }

    [Fact]
    public async Task Rejected_resubmission_persists_a_new_cycle_without_returning_to_draft_or_rewriting_rejection()
    {
        var originalApprover = NewUser("Original approver", _approverRoleId);
        var replacement = NewUser("Replacement approver", _approverRoleId);
        _db.Users.AddRange(originalApprover, replacement);
        await _db.SaveChangesAsync();
        var created = await NewService().CreateAsync(Input(), _creatorId, [new(_creatorId, "Chair")]);
        var meetingId = created.Meeting!.Id;
        Assert.True((await NewService().TransitionAsync(meetingId,
            new MeetingTransitionCommand(MeetingTransitionAction.SubmitForApproval, originalApprover.Id),
            _creatorId)).Succeeded);
        Assert.True((await NewService().TransitionAsync(meetingId,
            new MeetingTransitionCommand(MeetingTransitionAction.Reject, Reason: "Bổ sung tài liệu"),
            originalApprover.Id)).Succeeded);
        var rejection = await _db.MeetingStatusHistory.AsNoTracking().SingleAsync(item =>
            item.MeetingId == meetingId && item.Action == MeetingHistoryAction.Reject);

        var readiness = await NewService().GetSubmissionReadinessAsync(meetingId, _creatorId);
        var resubmit = await NewService().TransitionAsync(meetingId,
            new MeetingTransitionCommand(MeetingTransitionAction.SubmitForApproval, replacement.Id), _creatorId);

        Assert.True(readiness.Ready);
        Assert.Equal(MeetingStatus.Rejected, readiness.Status);
        Assert.Equal(MeetingWorkflowOutcome.Success, resubmit.Outcome);
        await using var verify = NewContext();
        var meeting = await verify.Meetings.AsNoTracking().SingleAsync(item => item.Id == meetingId);
        Assert.Equal(MeetingStatus.WaitingApproval, meeting.Status);
        Assert.Equal(replacement.Id, meeting.AssignedApproverId);
        var history = await verify.MeetingStatusHistory.AsNoTracking().Where(item => item.MeetingId == meetingId)
            .OrderBy(item => item.OccurredAt).ThenBy(item => item.Id).ToListAsync();
        Assert.Equal(4, history.Count);
        Assert.DoesNotContain(history, item => item.Action == MeetingHistoryAction.ReturnToDraft);
        Assert.Equal(MeetingStatus.Rejected, history.Last().FromStatus);
        Assert.Equal(replacement.Id, history.Last().AssignedApproverId);
        var persistedRejection = Assert.Single(history, item => item.Id == rejection.Id);
        Assert.Equal(rejection.Reason, persistedRejection.Reason);
        Assert.Equal(rejection.ActorId, persistedRejection.ActorId);
        Assert.Equal(rejection.OccurredAt, persistedRejection.OccurredAt);
        // Two submissions to approvers plus the rejection notice to the organizer.
        Assert.Equal(3, await verify.Notifications.CountAsync(item => item.MeetingId == meetingId));
        Assert.False(await verify.EmailMessages.AnyAsync(item => item.MeetingId == meetingId));
    }

    [Fact]
    public async Task Publish_endpoint_allows_a_meeting_without_sessions()
    {
        var created = await NewService().CreateAsync(Input(), _creatorId, [new(_creatorId, "Chair")]);
        var meetingId = created.Meeting!.Id;
        var chair = await _db.Users.SingleAsync(u => u.Id == _creatorId);
        var secretary = NewUser("Publish Secretary", _approverRoleId);
        _db.Users.Add(secretary);
        _db.MeetingParticipants.AddRange(
            new MeetingParticipant
            {
                MeetingId = meetingId,
                User = secretary,
                Role = MeetingRole.Secretary,
                Response = InvitationResponse.Pending,
            });
        await _db.SaveChangesAsync();
        await _db.Meetings.Where(item => item.Id == meetingId).ExecuteUpdateAsync(setters =>
            setters.SetProperty(item => item.Status, MeetingStatus.Approved));
        using var app = new PostgreSqlMeetingApiFactory(_connectionString);
        using var client = app.CreateClient();
        var organizer = await _db.Users.AsNoTracking().SingleAsync(item => item.Id == _creatorId);
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer",
            app.Services.GetRequiredService<ITokenIssuer>().CreateAccessToken(organizer, [Capability.MeetingCreator]));

        var published = await client.PostAsJsonAsync($"/api/meetings/{meetingId}/transitions", new { action = "Publish" });
        using var body = System.Text.Json.JsonDocument.Parse(await published.Content.ReadAsStringAsync());
        Assert.Equal(HttpStatusCode.OK, published.StatusCode);
        Assert.Equal("MSG45", body.RootElement.GetProperty("code").GetString());
        await using var verify = NewContext();
        Assert.Equal(MeetingStatus.Published, await verify.Meetings.Where(item => item.Id == meetingId)
            .Select(item => item.Status).SingleAsync());
        Assert.Equal(2, await verify.MeetingStatusHistory.CountAsync(item => item.MeetingId == meetingId));
        Assert.Equal(1, await verify.Notifications.CountAsync(item => item.MeetingId == meetingId));
        await MaterializeInvitations(meetingId);
        Assert.Equal(2, await verify.EmailMessages.CountAsync(item => item.MeetingId == meetingId));
    }

    [Theory]
    [InlineData("member")]
    [InlineData("delegated")]
    [InlineData("delegationTarget")]
    public async Task Secretary_history_uses_current_persisted_assignment_and_preserves_detail_privacy(string invalidated)
    {
        var secretary = NewUser("Assigned Secretary", _approverRoleId);
        _db.Users.Add(secretary);
        await _db.SaveChangesAsync();
        var meeting = ListMeeting("Restricted meeting", MeetingStatus.Rejected,
            new DateTimeOffset(2026, 10, 1, 2, 0, 0, TimeSpan.Zero), Sensitivity.Secret);
        _db.Meetings.Add(meeting);
        var assignment = new MeetingParticipant
        {
            MeetingId = meeting.Id,
            UserId = secretary.Id,
            Role = MeetingRole.Secretary,
            Response = InvitationResponse.Accepted,
        };
        _db.MeetingParticipants.Add(assignment);
        _db.MeetingStatusHistory.AddRange(new MeetingStatusHistory
        {
            MeetingId = meeting.Id,
            Action = MeetingHistoryAction.Reject,
            FromStatus = MeetingStatus.WaitingApproval,
            ToStatus = MeetingStatus.Rejected,
            ActorId = _creatorId,
            Reason = "Lý do dành cho thư ký",
        }, new MeetingStatusHistory
        {
            MeetingId = meeting.Id,
            Action = MeetingHistoryAction.TransferOrganizer,
            FromStatus = MeetingStatus.Approved,
            ToStatus = MeetingStatus.Approved,
            ActorId = _creatorId,
            PreviousOrganizerId = _creatorId,
            NewOrganizerId = _creatorId,
        });
        await _db.SaveChangesAsync();
        using var app = new PostgreSqlMeetingApiFactory(_connectionString);
        using var client = app.CreateClient();
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer",
            app.Services.GetRequiredService<ITokenIssuer>().CreateAccessToken(secretary, []));

        var response = await client.GetAsync($"/api/meetings/{meeting.Id}/history");
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        using var body = System.Text.Json.JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        var items = body.RootElement.GetProperty("items").EnumerateArray().ToArray();
        Assert.Equal("Lý do dành cho thư ký", Assert.Single(items,
            item => item.GetProperty("action").GetString() == "Reject")
            .GetProperty("rejectionReason").GetString());
        Assert.All(items, item =>
        {
            Assert.Equal(System.Text.Json.JsonValueKind.Null, item.GetProperty("previousOrganizerId").ValueKind);
            Assert.Equal(System.Text.Json.JsonValueKind.Null, item.GetProperty("newOrganizerId").ValueKind);
        });
        Assert.Equal(HttpStatusCode.OK, (await client.GetAsync($"/api/meetings/{meeting.Id}")).StatusCode);
        using var list = System.Text.Json.JsonDocument.Parse(await (await client.GetAsync("/api/meetings"))
            .Content.ReadAsStringAsync());
        Assert.Equal(meeting.Id, Assert.Single(list.RootElement.GetProperty("items").EnumerateArray())
            .GetProperty("id").GetGuid());

        switch (invalidated)
        {
            case "member": assignment.Role = MeetingRole.Member; break;
            case "delegated": assignment.Response = InvitationResponse.Delegated; break;
            case "delegationTarget": assignment.DelegatedToUserId = _creatorId; break;
        }
        await _db.SaveChangesAsync();
        Assert.Equal(HttpStatusCode.NotFound, (await client.GetAsync($"/api/meetings/{meeting.Id}/history")).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await client.GetAsync($"/api/meetings/{meeting.Id}")).StatusCode);
        using var invalidatedList = System.Text.Json.JsonDocument.Parse(await (await client.GetAsync("/api/meetings"))
            .Content.ReadAsStringAsync());
        Assert.Empty(invalidatedList.RootElement.GetProperty("items").EnumerateArray());
    }

    [Fact]
    public async Task Concurrent_publish_requests_only_commit_one_transition_and_invitation_batch()
    {
        var approver = NewUser("Concurrent workflow approver", _approverRoleId);
        _db.Users.Add(approver);
        var created = await NewService().CreateAsync(Input(), _creatorId, [new(_creatorId, "Chair")]);
        Assert.True(created.Succeeded);
        var meetingId = created.Meeting!.Id;
        _db.AgendaItems.Add(new AgendaItem
        {
            MeetingId = meetingId,
            Position = 1,
            Title = "Báo cáo",
            PresenterId = _creatorId,
        });
        _db.MeetingParticipants.AddRange(
            new MeetingParticipant
            {
                MeetingId = meetingId,
                UserId = approver.Id,
                Role = MeetingRole.Secretary,
                Response = InvitationResponse.Pending,
            });
        await _db.SaveChangesAsync();

        var service = NewService();
        Assert.Equal(MeetingWorkflowOutcome.Success, (await service.TransitionAsync(
            meetingId,
            new MeetingTransitionCommand(MeetingTransitionAction.SubmitForApproval, approver.Id),
            _creatorId)).Outcome);
        Assert.Equal(MeetingWorkflowOutcome.Success, (await service.TransitionAsync(
            meetingId,
            new MeetingTransitionCommand(MeetingTransitionAction.Approve),
            approver.Id)).Outcome);

        await using var competingContext = NewContext();
        var competingService = NewService(competingContext);
        var request1 = service.TransitionAsync(
            meetingId, new MeetingTransitionCommand(MeetingTransitionAction.Publish), _creatorId);
        var request2 = competingService.TransitionAsync(
            meetingId, new MeetingTransitionCommand(MeetingTransitionAction.Publish), _creatorId);
        var results = await Task.WhenAll(request1, request2);

        Assert.Single(results, result => result.Outcome == MeetingWorkflowOutcome.Success);
        Assert.Single(results, result => result.Outcome == MeetingWorkflowOutcome.StatusConflict);
        await using var verify = NewContext();
        Assert.Single(await verify.MeetingStatusHistory.Where(item =>
            item.MeetingId == meetingId && item.Action == MeetingHistoryAction.Publish).ToListAsync());
        await MaterializeInvitations(meetingId);
        Assert.Equal(2, await verify.EmailMessages.CountAsync(item => item.MeetingId == meetingId));
    }

    [Fact]
    public async Task Update_waits_for_a_concurrent_status_change_then_refuses_the_stale_edit()
    {
        var created = await NewService().CreateAsync(Input(), _creatorId, [new(_creatorId, "Chair")]);
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
        var chair = NewUser("Meeting Chair", _approverRoleId);
        var secretary = NewUser("Meeting Secretary", _approverRoleId);
        var unrelated = NewUser("Unrelated User", _approverRoleId);
        _db.Users.AddRange(approver, participant, chair, secretary, unrelated);

        var statuses = Enum.GetValues<MeetingStatus>();
        var meetings = statuses.Select(status => new Meeting
        {
            Title = $"Accessible meeting {status}",
            Description = string.Empty,
            Status = status,
            Sensitivity = Sensitivity.Internal,
            Mode = MeetingMode.InPerson,
            CreatedById = creator.Id,
            AssignedApproverId = approver.Id,
            Participants =
            [
                new MeetingParticipant
                {
                    UserId = participant.Id,
                    Role = MeetingRole.Member,
                },
                new MeetingParticipant { UserId = chair.Id, Role = MeetingRole.Chair },
                new MeetingParticipant { UserId = secretary.Id, Role = MeetingRole.Secretary },
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
                or MeetingStatus.Ended;
            Assert.Equal(participantCanRead, await repository.FindAccessibleAsync(meeting.Id, participant.Id) is not null);
            Assert.NotNull(await repository.FindAccessibleAsync(meeting.Id, chair.Id));
            Assert.NotNull(await repository.FindAccessibleAsync(meeting.Id, secretary.Id));

            Assert.Null(await repository.FindAccessibleAsync(meeting.Id, unrelated.Id));
        }
    }

    [Fact]
    public async Task Authorized_page_uses_the_same_lifecycle_visibility_for_items_and_total_count()
    {
        var approver = NewUser("List Approver", _approverRoleId);
        var participant = NewUser("List Participant", _approverRoleId);
        var chair = NewUser("List Chair", _approverRoleId);
        var secretary = NewUser("List Secretary", _approverRoleId);
        var unrelated = NewUser("List Unrelated", _approverRoleId);
        _db.Users.AddRange(approver, participant, chair, secretary, unrelated);
        var startsAt = new DateTimeOffset(2026, 10, 1, 2, 0, 0, TimeSpan.Zero);
        var meetings = Enum.GetValues<MeetingStatus>()
            .Select(status => ListMeeting(
                $"List visibility {status}", status, startsAt,
                approverId: approver.Id, participantId: participant.Id))
            .ToArray();
        foreach (var meeting in meetings)
        {
            meeting.Participants.Add(new MeetingParticipant { UserId = chair.Id, Role = MeetingRole.Chair });
            meeting.Participants.Add(new MeetingParticipant { UserId = secretary.Id, Role = MeetingRole.Secretary });
        }
        _db.Meetings.AddRange(meetings);
        await _db.SaveChangesAsync();

        var repository = new MeetingRepository(_db);
        var creatorPage = await repository.FindAuthorizedPageAsync(_creatorId, ListQuery());
        var approverPage = await repository.FindAuthorizedPageAsync(approver.Id, ListQuery());
        var participantPage = await repository.FindAuthorizedPageAsync(participant.Id, ListQuery());
        var chairPage = await repository.FindAuthorizedPageAsync(chair.Id, ListQuery());
        var secretaryPage = await repository.FindAuthorizedPageAsync(secretary.Id, ListQuery());
        var unrelatedPage = await repository.FindAuthorizedPageAsync(unrelated.Id, ListQuery());

        Assert.Equal(7, creatorPage.TotalCount);
        Assert.Equal(7, creatorPage.Items.Count);
        Assert.Equal(6, approverPage.TotalCount);
        Assert.DoesNotContain(approverPage.Items, item => item.Status == MeetingStatus.Draft);
        Assert.Equal(3, participantPage.TotalCount);
        MeetingStatus[] publishedStatuses =
        [
            MeetingStatus.Published, MeetingStatus.InProgress, MeetingStatus.Ended,
        ];
        Assert.All(participantPage.Items, item => Assert.Contains(item.Status, publishedStatuses));
        Assert.Equal(7, chairPage.TotalCount);
        Assert.Equal(Enum.GetValues<MeetingStatus>().Order(), chairPage.Items.Select(item => item.Status).Order());
        Assert.Equal(7, secretaryPage.TotalCount);
        Assert.Equal(0, unrelatedPage.TotalCount);
        Assert.Empty(unrelatedPage.Items);
        var approverFirst = await repository.FindAuthorizedPageAsync(
            approver.Id, ListQuery(pageSize: 2));
        Assert.Equal(6, approverFirst.TotalCount);
        Assert.Equal(2, approverFirst.Items.Count);
    }

    [Fact]
    public async Task Authorized_page_applies_all_filters_and_literal_case_insensitive_search()
    {
        var from = new DateTimeOffset(2026, 10, 1, 0, 0, 0, TimeSpan.Zero);
        var to = from.AddDays(1);
        var lowerBoundary = ListMeeting("Giao % Ban", MeetingStatus.Published, from, Sensitivity.Secret);
        var upperBoundary = ListMeeting("GIAO % BAN", MeetingStatus.Published, to, Sensitivity.Secret);
        var underscore = ListMeeting("Giao_Ban", MeetingStatus.Published, from.AddHours(1), Sensitivity.Secret);
        var backslash = ListMeeting(@"Giao\Ban", MeetingStatus.Published, from.AddHours(1), Sensitivity.Secret);
        var wrongStatus = ListMeeting("Giao % Ban", MeetingStatus.Draft, from.AddHours(1), Sensitivity.Secret);
        var wrongSensitivity = ListMeeting("Giao % Ban", MeetingStatus.Published, from.AddHours(1), Sensitivity.Internal);
        var outsideRange = ListMeeting("Giao % Ban", MeetingStatus.Published, to.AddSeconds(1), Sensitivity.Secret);
        _db.Meetings.AddRange(
            lowerBoundary, upperBoundary, underscore, backslash,
            wrongStatus, wrongSensitivity, outsideRange);
        await _db.SaveChangesAsync();

        var repository = new MeetingRepository(_db);
        var percent = await repository.FindAuthorizedPageAsync(_creatorId, ListQuery(
            MeetingStatus.Published, Sensitivity.Secret, "%", from, to));
        var underscorePage = await repository.FindAuthorizedPageAsync(_creatorId, ListQuery(
            MeetingStatus.Published, Sensitivity.Secret, "_", from, to));
        var backslashPage = await repository.FindAuthorizedPageAsync(_creatorId, ListQuery(
            MeetingStatus.Published, Sensitivity.Secret, "\\", from, to));
        var onlyFrom = await repository.FindAuthorizedPageAsync(_creatorId, ListQuery(
            MeetingStatus.Published, Sensitivity.Secret, "%", from: from));
        var onlyTo = await repository.FindAuthorizedPageAsync(_creatorId, ListQuery(
            MeetingStatus.Published, Sensitivity.Secret, "%", to: to));

        Assert.Equal(2, percent.TotalCount);
        Assert.Equal(
            new[] { lowerBoundary.Id, upperBoundary.Id }.Order(),
            percent.Items.Select(item => item.Id).Order());
        Assert.Equal(1, underscorePage.TotalCount);
        Assert.Equal(underscore.Id, Assert.Single(underscorePage.Items).Id);
        Assert.Equal(backslash.Id, Assert.Single(backslashPage.Items).Id);
        Assert.Equal(3, onlyFrom.TotalCount);
        Assert.Equal(2, onlyTo.TotalCount);
    }

    [Fact]
    public async Task Authorized_page_time_range_matches_meetings_whose_own_schedule_overlaps()
    {
        // BR-30: [PlannedStartTime, PlannedEndTime] overlaps [from, to], both ends inclusive.
        var from = new DateTimeOffset(2026, 10, 1, 2, 0, 0, TimeSpan.Zero);
        var to = from.AddHours(1);
        var startsBefore = ListMeeting("Starts before", MeetingStatus.Draft, from.AddMinutes(-30));
        var inside = ListMeeting("Inside", MeetingStatus.Draft, from.AddMinutes(15));
        var endsAtFrom = ListMeeting("Ends exactly at from", MeetingStatus.Draft, from.AddHours(-1));
        var spanning = ListMeeting("Spans the range", MeetingStatus.Draft, from.AddHours(-2));
        spanning.PlannedEndTime = to.AddHours(2);
        var endsBefore = ListMeeting("Ends before", MeetingStatus.Draft, from.AddHours(-1).AddMinutes(-1));
        var startsAfter = ListMeeting("Starts after", MeetingStatus.Draft, to.AddMinutes(1));
        _db.Meetings.AddRange(startsBefore, inside, endsAtFrom, spanning, endsBefore, startsAfter);
        await _db.SaveChangesAsync();

        var page = await new MeetingRepository(_db).FindAuthorizedPageAsync(
            _creatorId, ListQuery(from: from, to: to));

        Assert.Equal(
            new[] { startsBefore.Id, inside.Id, endsAtFrom.Id, spanning.Id }.Order(),
            page.Items.Select(item => item.Id).Order());
        var item = page.Items.Single(row => row.Id == inside.Id);
        Assert.Equal(from.AddMinutes(15), item.PlannedStartTime);
        Assert.Equal(from.AddMinutes(75), item.PlannedEndTime);
        Assert.Equal(MeetingMode.InPerson, item.Mode);
    }

    [Fact]
    public async Task Authorized_page_timing_splits_on_the_planned_start_against_now()
    {
        // As AI-eCabinet: upcoming starts after now; past started at or before now, under way included.
        var now = new DateTimeOffset(2026, 10, 1, 2, 0, 0, TimeSpan.Zero);
        var underWay = ListMeeting("Under way", MeetingStatus.Draft, now.AddMinutes(-30));
        var startsNow = ListMeeting("Starts now", MeetingStatus.Draft, now);
        var later = ListMeeting("Later", MeetingStatus.Draft, now.AddMinutes(1));
        _db.Meetings.AddRange(underWay, startsNow, later);
        await _db.SaveChangesAsync();
        var repository = new MeetingRepository(_db);

        var upcoming = await repository.FindAuthorizedPageAsync(
            _creatorId, ListQuery() with { Timing = MeetingTiming.Upcoming, Now = now });
        var past = await repository.FindAuthorizedPageAsync(
            _creatorId, ListQuery() with { Timing = MeetingTiming.Past, Now = now });

        Assert.Equal([later.Id], upcoming.Items.Select(item => item.Id));
        Assert.Equal(new[] { underWay.Id, startsNow.Id }.Order(), past.Items.Select(item => item.Id).Order());
    }

    [Fact]
    public async Task Authorized_page_sorts_all_columns_in_both_directions_and_pages_stably()
    {
        var baseTime = new DateTimeOffset(2026, 10, 1, 0, 0, 0, TimeSpan.Zero);
        var first = ListMeeting("A", MeetingStatus.Draft, baseTime, createdAt: baseTime.AddDays(3));
        var second = ListMeeting("B", MeetingStatus.Draft, baseTime.AddDays(2), createdAt: baseTime);
        var third = ListMeeting("A", MeetingStatus.Draft, baseTime.AddDays(1), createdAt: baseTime.AddDays(1));
        var fourth = ListMeeting("C", MeetingStatus.Draft, baseTime.AddDays(1), createdAt: baseTime.AddDays(1));
        var all = new[] { first, second, third, fourth };
        _db.Meetings.AddRange(all);
        await _db.SaveChangesAsync();

        var repository = new MeetingRepository(_db);
        foreach (var sortBy in Enum.GetValues<MeetingSortBy>())
        {
            foreach (var direction in Enum.GetValues<MeetingSortDirection>())
            {
                var sorted = (sortBy, direction) switch
                {
                    (MeetingSortBy.Title, MeetingSortDirection.Asc) => all.OrderBy(m => m.Title),
                    (MeetingSortBy.Title, MeetingSortDirection.Desc) => all.OrderByDescending(m => m.Title),
                    (MeetingSortBy.CreatedAt, MeetingSortDirection.Asc) => all.OrderBy(m => m.CreatedAt),
                    (MeetingSortBy.CreatedAt, MeetingSortDirection.Desc) => all.OrderByDescending(m => m.CreatedAt),
                    (MeetingSortBy.PlannedStartTime, MeetingSortDirection.Asc) => all.OrderBy(m => m.PlannedStartTime),
                    _ => all.OrderByDescending(m => m.PlannedStartTime),
                };
                var expected = sorted.ThenBy(m => m.Id).Select(m => m.Id).ToArray();
                var firstPage = await repository.FindAuthorizedPageAsync(_creatorId,
                    ListQuery(pageSize: 2, sortBy: sortBy, direction: direction));
                var secondPage = await repository.FindAuthorizedPageAsync(_creatorId,
                    ListQuery(page: 2, pageSize: 2, sortBy: sortBy, direction: direction));
                var beyond = await repository.FindAuthorizedPageAsync(_creatorId,
                    ListQuery(page: 3, pageSize: 2, sortBy: sortBy, direction: direction));

                Assert.Equal(4, firstPage.TotalCount);
                Assert.Equal(4, secondPage.TotalCount);
                Assert.Equal(expected,
                    firstPage.Items.Concat(secondPage.Items).Select(item => item.Id));
                Assert.Empty(beyond.Items);
                Assert.Equal(4, beyond.TotalCount);
            }
        }
    }

    [Fact]
    public async Task Authorized_page_beyond_int_offset_returns_empty_with_the_authorized_count()
    {
        _db.Meetings.Add(ListMeeting("Visible", MeetingStatus.Draft,
            new DateTimeOffset(2026, 10, 1, 0, 0, 0, TimeSpan.Zero)));
        await _db.SaveChangesAsync();

        var page = await new MeetingRepository(_db).FindAuthorizedPageAsync(
            _creatorId, ListQuery(page: int.MaxValue, pageSize: 20));

        Assert.Empty(page.Items);
        Assert.Equal(int.MaxValue, page.Page);
        Assert.Equal(1, page.TotalCount);
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
                or MeetingStatus.Ended;
            Assert.Equal(
                participantCanRead,
                await repository.FindAccessibleAsync(meeting.Id, participantId) is not null);
            Assert.Null(await repository.FindAccessibleAsync(meeting.Id, unrelatedId));
        }
    }

    [Fact]
    public async Task FindHistoryPage_returns_empty_for_an_out_of_range_offset_before_casting()
    {
        var actor = new User
        {
            FullName = "History Actor",
            Email = "history-actor@trustcabinet.local",
            PasswordHash = "test-hash",
        };
        var meeting = new Meeting
        {
            Title = "History paging",
            Description = string.Empty,
            Status = MeetingStatus.Draft,
            Sensitivity = Sensitivity.Normal,
            Mode = MeetingMode.InPerson,
            CreatedById = Guid.CreateVersion7(),
        };
        _db.Meetings.Add(meeting);
        _db.MeetingStatusHistory.Add(new MeetingStatusHistory
        {
            MeetingId = meeting.Id,
            Action = MeetingHistoryAction.Create,
            ToStatus = MeetingStatus.Draft,
            ActorId = actor.Id,
            Actor = actor,
            OccurredAt = DateTimeOffset.UtcNow,
        });
        await _db.SaveChangesAsync();

        var page = await new MeetingRepository(_db).FindHistoryPageAsync(
            meeting.Id, int.MaxValue, 100);

        Assert.NotNull(page);
        Assert.Empty(page.Items);
        Assert.Equal(1, page.TotalCount);
    }
}
