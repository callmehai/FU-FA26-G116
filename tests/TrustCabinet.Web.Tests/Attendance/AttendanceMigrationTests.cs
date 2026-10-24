using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using Microsoft.EntityFrameworkCore.Migrations.Operations;
using TrustCabinet.Domain.Entities;
using TrustCabinet.Infrastructure;

namespace TrustCabinet.Web.Tests.Attendance;

public sealed class AttendanceMigrationTests
{
    [Fact]
    public async Task Upgrade_adds_only_two_defaulted_columns_and_preserves_history_through_downgrade()
    {
        await using var database = new PostgreSqlTestDatabase(PostgresTestDatabase.ConnectionString("trustcabinet_attendance_migration"));
        await using var db = new TrustCabinetDbContext(new DbContextOptionsBuilder<TrustCabinetDbContext>()
            .UseNpgsql(database.DataSource, o => o.MigrationsHistoryTable("__ef_migrations_history")).UseSnakeCaseNamingConvention().Options);
        var previous = db.Database.GetMigrations().Single(m => m.EndsWith("MeetingParticipantLifecycleAndGuestInvitations", StringComparison.Ordinal));
        await db.GetService<IMigrator>().MigrateAsync(previous);
        var user = new User { FullName = "Migration", Email = "migration@attendance.test", PasswordHash = "test-only" };
        db.Users.Add(user); await db.SaveChangesAsync();
        var meeting = new Meeting { CreatedById = user.Id, Title = "Historical", PlannedStartTime = DateTimeOffset.UtcNow, PlannedEndTime = DateTimeOffset.UtcNow.AddHours(1) };
        db.Meetings.Add(meeting); await db.SaveChangesAsync();
        var p = Guid.CreateVersion7(); var g = Guid.CreateVersion7(); var now = new DateTimeOffset(2026, 10, 20, 0, 0, 0, TimeSpan.Zero);
        await db.Database.ExecuteSqlInterpolatedAsync($"""
            INSERT INTO meeting_participants (id, meeting_id, user_id, role, response, attendance, attendance_recorded_by_id, attendance_recorded_at, created_at, updated_at)
            VALUES ({p}, {meeting.Id}, {user.Id}, 'Member', 'Declined', 'Excused', {user.Id}, {now}, {now}, {now})
            """);
        await db.Database.ExecuteSqlInterpolatedAsync($"""
            INSERT INTO meeting_guest_invitations (id, meeting_id, invited_name, email, invited_by_id, sensitivity_at_invitation,
              response, attendance, attendance_recorded_by_id, attendance_recorded_at, initial_invitation_delivery_key, created_at, updated_at)
            VALUES ({g}, {meeting.Id}, 'Guest', 'guest@attendance.test', {user.Id}, 'Internal', 'Accepted', 'Absent', {user.Id}, {now}, '', {now}, {now})
            """);
        var assembly = db.GetService<IMigrationsAssembly>();
        var info = assembly.Migrations.Single(m => m.Key.EndsWith("AddAttendanceVersions", StringComparison.Ordinal));
        var migration = assembly.CreateMigration(info.Value, db.Database.ProviderName!);
        Assert.Equal(2, migration.UpOperations.Count);
        Assert.All(migration.UpOperations, operation =>
        {
            var column = Assert.IsType<AddColumnOperation>(operation);
            Assert.Equal("attendance_version", column.Name); Assert.Equal("bigint", column.ColumnType);
            Assert.Equal(0L, column.DefaultValue); Assert.False(column.IsNullable);
        });
        await db.Database.MigrateAsync(); Assert.False(db.Database.HasPendingModelChanges());
        foreach (var type in new[] { typeof(MeetingParticipant), typeof(MeetingGuestInvitation) })
        {
            var property = db.Model.FindEntityType(type)!.FindProperty("AttendanceVersion")!;
            Assert.False(property.IsConcurrencyToken); Assert.Equal(typeof(long), property.ClrType);
        }
        var row = await db.MeetingParticipants.SingleAsync(); var guest = await db.MeetingGuestInvitations.SingleAsync();
        Assert.Equal(0, row.AttendanceVersion); Assert.Equal(0, guest.AttendanceVersion);
        Assert.Equal("Excused", row.Attendance.ToString()); Assert.Equal("Absent", guest.Attendance.ToString());
        Assert.Equal(user.Id, row.AttendanceRecordedById); Assert.Equal(now, row.AttendanceRecordedAt);
        Assert.Equal("Declined", row.Response.ToString()); Assert.Equal("Accepted", guest.Response.ToString());
        await db.GetService<IMigrator>().MigrateAsync(previous);
        Assert.Equal("Excused", await db.Database.SqlQuery<string>($"SELECT attendance AS \"Value\" FROM meeting_participants WHERE id = {p}").SingleAsync());
        await db.Database.MigrateAsync(); db.ChangeTracker.Clear();
        Assert.Equal(0, (await db.MeetingGuestInvitations.SingleAsync()).AttendanceVersion);
    }
}
