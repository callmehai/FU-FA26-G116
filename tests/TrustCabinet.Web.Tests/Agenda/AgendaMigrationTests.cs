using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql;
using TrustCabinet.Domain.Entities;
using TrustCabinet.Domain.Enums;
using TrustCabinet.Infrastructure;

namespace TrustCabinet.Web.Tests.Agenda;

public sealed class AgendaMigrationTests
{
    [Theory]
    [InlineData("valid")]
    [InlineData("Postponed")]
    [InlineData("Unknown")]
    [InlineData("duplicate")]
    public async Task Upgrade_preserves_valid_data_and_rejects_unsupported_evidence(string scenario)
    {
        await using var database = new PostgreSqlTestDatabase(PostgresTestDatabase.ConnectionString("trustcabinet_agenda_migration"));
        await using var db = new TrustCabinetDbContext(new DbContextOptionsBuilder<TrustCabinetDbContext>()
            .UseNpgsql(database.DataSource, o => o.MigrationsHistoryTable("__ef_migrations_history"))
            .UseSnakeCaseNamingConvention().Options);
        var migrations = db.Database.GetMigrations().ToArray();
        var previous = migrations[Array.FindIndex(migrations, m => m.EndsWith("AgendaExecutionInvariants", StringComparison.Ordinal)) - 1];
        await db.GetService<IMigrator>().MigrateAsync(previous);
        var user = new User { FullName = "Migration", Email = "migration@agenda.test", PasswordHash = "test-only" };
        var meeting = new Meeting
        {
            Title = "Legacy",
            CreatedById = user.Id,
            Status = MeetingStatus.InProgress,
            PlannedStartTime = DateTimeOffset.UtcNow,
            PlannedEndTime = DateTimeOffset.UtcNow.AddHours(1)
        };
        var a = new AgendaItem
        {
            MeetingId = meeting.Id,
            Title = "A",
            Position = 1,
            PlannedDurationMinutes = 20,
            Status = AgendaItemStatus.InProgress,
            ActualStartedAt = new(2026, 10, 12, 0, 0, 0, TimeSpan.Zero)
        };
        var b = new AgendaItem
        {
            MeetingId = meeting.Id,
            Title = "B",
            Position = 8,
            PlannedDurationMinutes = 20,
            Status = scenario == "duplicate" ? AgendaItemStatus.InProgress : AgendaItemStatus.Pending
        };
        db.Add(user);
        await db.SaveChangesAsync();
        // Seed the historical schema without relying on newer entity columns.
        await db.Database.ExecuteSqlInterpolatedAsync($"""
            INSERT INTO meetings (id, title, description, status, sensitivity, mode, location,
                planned_start_time, planned_end_time, created_by_id, created_at, updated_at)
            VALUES ({meeting.Id}, {meeting.Title}, '', 'InProgress', 'Internal', 'InPerson', '',
                {meeting.PlannedStartTime}, {meeting.PlannedEndTime}, {user.Id}, {meeting.CreatedAt}, {meeting.UpdatedAt})
            """);
        db.AddRange(a, b);
        await db.SaveChangesAsync();
        if (scenario is "Postponed" or "Unknown")
            await db.Database.ExecuteSqlInterpolatedAsync($"UPDATE agenda_items SET status = {scenario} WHERE id = {b.Id}");
        if (scenario != "valid")
        {
            var error = await Assert.ThrowsAsync<PostgresException>(() => db.Database.MigrateAsync());
            Assert.Contains("Agenda migration blocked", error.MessageText);
            Assert.Equal(2, await db.AgendaItems.CountAsync());
            var statuses = await db.Database.SqlQueryRaw<string>("SELECT status AS \"Value\" FROM agenda_items ORDER BY position").ToListAsync();
            Assert.Equal(scenario == "duplicate" ? "InProgress" : scenario, statuses[1]);
            return;
        }
        await db.Database.MigrateAsync();
        Assert.False(db.Database.HasPendingModelChanges());
        var errorDuplicate = await Assert.ThrowsAsync<PostgresException>(() => db.Database.ExecuteSqlInterpolatedAsync(
            $"UPDATE agenda_items SET status = 'InProgress' WHERE id = {b.Id}"));
        Assert.Equal(PostgresErrorCodes.UniqueViolation, errorDuplicate.SqlState);
        var errorStatus = await Assert.ThrowsAsync<PostgresException>(() => db.Database.ExecuteSqlInterpolatedAsync(
            $"UPDATE agenda_items SET status = 'Postponed' WHERE id = {b.Id}"));
        Assert.Equal(PostgresErrorCodes.CheckViolation, errorStatus.SqlState);
        db.ChangeTracker.Clear();
        var preserved = await db.AgendaItems.SingleAsync(i => i.Id == a.Id);
        Assert.Equal(a.ActualStartedAt, preserved.ActualStartedAt);
        var other = new Meeting
        {
            Title = "Other",
            CreatedById = user.Id,
            Status = MeetingStatus.InProgress,
            PlannedStartTime = meeting.PlannedStartTime,
            PlannedEndTime = meeting.PlannedEndTime
        };
        db.Meetings.Add(other);
        db.AgendaItems.Add(new() { MeetingId = other.Id, Title = "Current", Position = 1, Status = AgendaItemStatus.InProgress });
        await db.SaveChangesAsync();
        Assert.Equal(2, await db.AgendaItems.CountAsync(i => i.Status == AgendaItemStatus.InProgress));
    }
}
