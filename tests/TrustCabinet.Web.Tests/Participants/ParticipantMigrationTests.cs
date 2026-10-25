using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql;
using TrustCabinet.Domain.Entities;
using TrustCabinet.Domain.Enums;
using TrustCabinet.Domain.Participants;
using TrustCabinet.Infrastructure;

namespace TrustCabinet.Web.Tests.Participants;

public sealed class ParticipantMigrationTests
{
    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public async Task Upgrade_backfills_missing_chair_and_safe_downgrade_preserves_evidence(bool creatorAlreadyPresent)
    {
        await using var database = new PostgreSqlTestDatabase(PostgresTestDatabase.ConnectionString("trustcabinet_chair_backfill"));
        await using var db = new TrustCabinetDbContext(new DbContextOptionsBuilder<TrustCabinetDbContext>()
            .UseNpgsql(database.DataSource, o => o.MigrationsHistoryTable("__ef_migrations_history"))
            .UseSnakeCaseNamingConvention().Options);
        var previous = db.Database.GetMigrations().Last(m => m.EndsWith("AgendaExecutionInvariants", StringComparison.Ordinal));
        await db.GetService<IMigrator>().MigrateAsync(previous);
        var user = new User { FullName = "Creator", Email = "creator@backfill.test", PasswordHash = "test-only" };
        db.Users.Add(user);
        await db.SaveChangesAsync();
        var meeting = Guid.CreateVersion7();
        var participant = Guid.CreateVersion7();
        var now = new DateTimeOffset(2026, 10, 20, 0, 0, 0, TimeSpan.Zero);
        await db.Database.ExecuteSqlInterpolatedAsync($"""
            INSERT INTO meetings (id, title, description, status, sensitivity, mode, location,
                planned_start_time, planned_end_time, created_by_id, created_at, updated_at)
            VALUES ({meeting}, 'Backfill', '', 'Published', 'Internal', 'InPerson', '', {now}, {now.AddHours(1)}, {user.Id}, {now}, {now})
            """);
        if (creatorAlreadyPresent) await db.Database.ExecuteSqlInterpolatedAsync($"""
            INSERT INTO meeting_participants (id, meeting_id, user_id, role, response, responded_at, attendance,
                attendance_recorded_by_id, attendance_recorded_at, created_at, updated_at)
            VALUES ({participant}, {meeting}, {user.Id}, 'Member', 'Accepted', {now}, 'Present', {user.Id}, {now}, {now}, {now})
            """);
        await db.Database.MigrateAsync();
        var chair = await db.MeetingParticipants.AsNoTracking().SingleAsync();
        Assert.Equal(MeetingRole.Chair, chair.Role);
        Assert.Equal(user.Id, chair.UserId);
        Assert.Equal($"meeting:{meeting}:published:{user.Id}", chair.InitialInvitationDeliveryKey);
        Assert.Equal(creatorAlreadyPresent ? InvitationResponse.Accepted : InvitationResponse.Pending, chair.Response);
        Assert.Equal(creatorAlreadyPresent ? AttendanceStatus.Present : AttendanceStatus.Unknown, chair.Attendance);
        if (creatorAlreadyPresent) { Assert.Equal(participant, chair.Id); Assert.Equal(now, chair.RespondedAt); }
        Assert.Empty(await db.MeetingInvitationDispatches.ToArrayAsync());
        await db.GetService<IMigrator>().MigrateAsync(previous);
        await db.Database.MigrateAsync();
        var restored = await db.MeetingParticipants.AsNoTracking().SingleAsync();
        Assert.Equal(chair.Id, restored.Id);
        Assert.Equal(chair.Response, restored.Response);
        Assert.Equal(chair.RespondedAt, restored.RespondedAt);
        Assert.Equal(chair.Attendance, restored.Attendance);
        Assert.False(db.Database.HasPendingModelChanges());
    }

    [Theory]
    [InlineData("valid")]
    [InlineData("Guest")]
    [InlineData("Delegated")]
    [InlineData("chairs")]
    public async Task Upgrade_preserves_evidence_or_stops_without_modifying_unsupported_rows(string scenario)
    {
        await using var database = new PostgreSqlTestDatabase(PostgresTestDatabase.ConnectionString("trustcabinet_participant_migration"));
        await using var db = new TrustCabinetDbContext(new DbContextOptionsBuilder<TrustCabinetDbContext>()
            .UseNpgsql(database.DataSource, o => o.MigrationsHistoryTable("__ef_migrations_history")).UseSnakeCaseNamingConvention().Options);
        var previous = db.Database.GetMigrations().Last(m => m.EndsWith("AgendaExecutionInvariants", StringComparison.Ordinal));
        await db.GetService<IMigrator>().MigrateAsync(previous);
        var user = new User { FullName = "Migration", Email = "migration@participants.test", PasswordHash = "test-only" };
        var second = new User { FullName = "Second", Email = "second@participants.test", PasswordHash = "test-only" };
        db.Users.AddRange(user, second); await db.SaveChangesAsync();
        var meeting = Guid.CreateVersion7(); var participant = Guid.CreateVersion7();
        var now = new DateTimeOffset(2026, 10, 14, 0, 0, 0, TimeSpan.Zero);
        await db.Database.ExecuteSqlInterpolatedAsync($"""
            INSERT INTO meetings (id, title, description, status, sensitivity, mode, location,
                planned_start_time, planned_end_time, created_by_id, created_at, updated_at)
            VALUES ({meeting}, 'Historical', '', 'Draft', 'Internal', 'InPerson', '', {now}, {now.AddHours(1)}, {user.Id}, {now}, {now})
            """);
        var role = scenario == "Guest" ? "Guest" : "Chair";
        var response = scenario == "Delegated" ? "Delegated" : "Declined";
        await db.Database.ExecuteSqlInterpolatedAsync($"""
            INSERT INTO meeting_participants (id, meeting_id, user_id, role, response, responded_at, attendance,
                attendance_recorded_by_id, attendance_recorded_at, created_at, updated_at)
            VALUES ({participant}, {meeting}, {user.Id}, {role}, {response}, {now}, 'Present', {user.Id}, {now}, {now}, {now})
            """);
        if (scenario == "chairs") await db.Database.ExecuteSqlInterpolatedAsync($"""
            INSERT INTO meeting_participants (id, meeting_id, user_id, role, response, created_at, updated_at)
            VALUES ({Guid.CreateVersion7()}, {meeting}, {second.Id}, 'Chair', 'Pending', {now}, {now})
            """);
        if (scenario != "valid")
        {
            var error = await Assert.ThrowsAsync<PostgresException>(() => db.Database.MigrateAsync());
            Assert.Contains("SCRUM-20", error.MessageText);
            Assert.Equal(response, await db.Database.SqlQuery<string>($"SELECT response AS \"Value\" FROM meeting_participants WHERE id = {participant}").SingleAsync());
            Assert.DoesNotContain(await db.Database.GetAppliedMigrationsAsync(), m => m.EndsWith("MeetingParticipantLifecycleAndGuestInvitations", StringComparison.Ordinal));
            return;
        }
        await db.Database.MigrateAsync();
        Assert.False(db.Database.HasPendingModelChanges());
        var p = await db.MeetingParticipants.SingleAsync();
        Assert.Equal(AssignmentStatus.Current, p.AssignmentStatus); Assert.Equal(InvitationResponse.Declined, p.Response);
        Assert.Equal(now, p.RespondedAt); Assert.Equal(AttendanceStatus.Present, p.Attendance);
        Assert.Equal(user.Id, p.AttendanceRecordedById); Assert.Equal(now, p.AttendanceRecordedAt);
        Assert.Equal($"meeting:{meeting}:published:{user.Id}", p.InitialInvitationDeliveryKey);
        var duplicate = await Assert.ThrowsAsync<PostgresException>(() => db.Database.ExecuteSqlInterpolatedAsync($"""
            INSERT INTO meeting_participants (id, meeting_id, user_id, role, response, created_at, updated_at)
            VALUES ({Guid.CreateVersion7()}, {meeting}, {second.Id}, 'Chair', 'Pending', {now}, {now})
            """));
        Assert.Equal(PostgresErrorCodes.UniqueViolation, duplicate.SqlState);
        Assert.Null(await db.Database.SqlQuery<string?>($"SELECT to_regclass('meeting_participant_permissions')::text AS \"Value\"").SingleAsync());
        p.AssignmentStatus = AssignmentStatus.Removed; p.RemovedAt = now; p.RemovedById = second.Id;
        await db.SaveChangesAsync();
        db.MeetingParticipants.Add(new() { MeetingId = meeting, UserId = user.Id, Role = MeetingRole.Chair }); await db.SaveChangesAsync();
        Assert.Equal(2, await db.MeetingParticipants.CountAsync());
        var downgrade = await Assert.ThrowsAsync<PostgresException>(() => db.GetService<IMigrator>().MigrateAsync(previous));
        Assert.Contains("SCRUM-20", downgrade.MessageText);
        Assert.Equal(2, await db.MeetingParticipants.CountAsync());
    }
}
