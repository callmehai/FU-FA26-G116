using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql;
using TrustCabinet.Domain.Entities;
using TrustCabinet.Domain.Enums;
using TrustCabinet.Infrastructure;

namespace TrustCabinet.Web.Tests.Agenda;

public sealed class AgendaPoMigrationTests
{
    [Fact]
    public async Task Upgrade_preserves_legacy_data_adds_constraints_and_refuses_lossy_downgrade()
    {
        await using var database = new PostgreSqlTestDatabase(PostgresTestDatabase.ConnectionString("trustcabinet_agenda_po_migration"));
        await using var db = new TrustCabinetDbContext(new DbContextOptionsBuilder<TrustCabinetDbContext>()
            .UseNpgsql(database.DataSource, o => o.MigrationsHistoryTable("__ef_migrations_history"))
            .UseSnakeCaseNamingConvention().Options);
        var previous = db.Database.GetMigrations().TakeWhile(m => !m.EndsWith("SynchronizeAgendaWithPoRequirements", StringComparison.Ordinal)).Last();
        await db.GetService<IMigrator>().MigrateAsync(previous);
        var user = new User { Email = "legacy@agenda.test", FullName = "Legacy User", PasswordHash = "test-only" };
        var now = new DateTimeOffset(2026, 10, 24, 0, 0, 0, TimeSpan.Zero);
        var meeting = new Meeting { Title = "Preserve", CreatedById = user.Id, PlannedStartTime = now, PlannedEndTime = now.AddHours(1) };
        db.Add(user); await db.SaveChangesAsync();
        await LegacyMeetingRows.InsertAsync(db, meeting);
        var guest = new MeetingGuestInvitation { MeetingId = meeting.Id, InvitedName = "Legacy guest", Email = "guest@agenda.test", InvitedById = user.Id };
        db.Add(guest); await db.SaveChangesAsync();
        var ids = Enumerable.Range(0, 3).Select(_ => Guid.CreateVersion7()).ToArray();
        for (var i = 0; i < ids.Length; i++)
        {
            var status = ((AgendaItemStatus)i).ToString();
            DateTimeOffset? start = i == 0 ? null : now;
            DateTimeOffset? end = i == 2 ? now.AddMinutes(5) : null;
            await db.Database.ExecuteSqlInterpolatedAsync($"""
                INSERT INTO agenda_items (id, meeting_id, title, description, position, planned_duration_minutes,
                    presenter_id, presenter_name, status, actual_started_at, actual_ended_at, created_at, updated_at)
                VALUES ({ids[i]}, {meeting.Id}, 'Historical', '', {1 + i * 3}, {10 + i}, {user.Id}, 'Tên tự nhập cũ',
                    {status}, {start}, {end}, {now}, {now})
                """);
        }
        // Rows written by SQL with the columns of that point: the model's later columns do not exist yet.
        var document = AgendaFixture.TestDocument(meeting.Id, "Preserved file", user.Id);
        db.Add(document.StoredFile); await db.SaveChangesAsync();
        await db.Database.ExecuteSqlInterpolatedAsync($"""
            INSERT INTO meeting_documents (id, meeting_id, stored_file_id, file_name, added_by_id, added_at, created_at, updated_at)
            VALUES ({document.Id}, {meeting.Id}, {document.StoredFile.Id}, {document.FileName}, {user.Id}, {now}, {now}, {now})
            """);
        foreach (var item in new[] { ids[0], ids[2] })
            await db.Database.ExecuteSqlInterpolatedAsync(
                $"INSERT INTO meeting_document_agenda_items (meeting_document_id, agenda_item_id) VALUES ({document.Id}, {item})");
        await db.Database.MigrateAsync();
        db.ChangeTracker.Clear();
        var items = await db.AgendaItems.OrderBy(i => i.Position).ToArrayAsync();
        Assert.Equal(ids, items.Select(i => i.Id));
        Assert.Equal(new[] { 1, 4, 7 }, items.Select(i => i.Position));
        Assert.All(items, i =>
        {
            Assert.Equal(user.Id, i.PresenterId);
            Assert.Equal("Tên tự nhập cũ", i.LegacyPresenterName);
            Assert.Null(i.PresenterGuestInvitationId);
            Assert.Null(i.Conclusion); Assert.Null(i.ConclusionUpdatedById); Assert.Null(i.ConclusionUpdatedAt);
            Assert.Equal(now, i.CreatedAt); Assert.Equal(now, i.UpdatedAt);
        });
        Assert.Null(items[0].ActualStartedAt);
        Assert.Equal(now, items[1].ActualStartedAt);
        Assert.Equal(now.AddMinutes(5), items[2].ActualEndedAt);
        Assert.Equal(2, await db.MeetingDocumentAgendaItems.CountAsync());
        Assert.True(await db.StoredFiles.AnyAsync(f => f.Id == document.StoredFile.Id));
        Assert.True(await db.MeetingGuestInvitations.AnyAsync(g => g.Id == guest.Id && g.RevokedAt == null));
        Assert.Equal(0, await db.Database.SqlQueryRaw<int>("SELECT count(*)::int AS \"Value\" FROM information_schema.columns WHERE table_name = 'agenda_items' AND column_name = 'planned_duration_minutes'").SingleAsync());
        var presenterCheck = await Assert.ThrowsAsync<PostgresException>(() => db.Database.ExecuteSqlInterpolatedAsync($"UPDATE agenda_items SET presenter_guest_invitation_id = {guest.Id} WHERE id = {ids[0]}"));
        Assert.Equal(PostgresErrorCodes.CheckViolation, presenterCheck.SqlState);
        var metadataCheck = await Assert.ThrowsAsync<PostgresException>(() => db.Database.ExecuteSqlInterpolatedAsync($"UPDATE agenda_items SET conclusion = 'Incomplete' WHERE id = {ids[0]}"));
        Assert.Equal(PostgresErrorCodes.CheckViolation, metadataCheck.SqlState);
        var currentCheck = await Assert.ThrowsAsync<PostgresException>(() => db.Database.ExecuteSqlInterpolatedAsync($"UPDATE agenda_items SET status = 'InProgress' WHERE id = {ids[0]}"));
        Assert.Equal(PostgresErrorCodes.UniqueViolation, currentCheck.SqlState);
        var missing = Guid.NewGuid();
        var guestFk = await Assert.ThrowsAsync<PostgresException>(() => db.Database.ExecuteSqlInterpolatedAsync($"UPDATE agenda_items SET presenter_id = NULL, presenter_guest_invitation_id = {missing} WHERE id = {ids[0]}"));
        Assert.Equal(PostgresErrorCodes.ForeignKeyViolation, guestFk.SqlState);
        var authorFk = await Assert.ThrowsAsync<PostgresException>(() => db.Database.ExecuteSqlInterpolatedAsync($"UPDATE agenda_items SET conclusion = 'Valid', conclusion_updated_at = {now}, conclusion_updated_by_id = {missing} WHERE id = {ids[0]}"));
        Assert.Equal(PostgresErrorCodes.ForeignKeyViolation, authorFk.SqlState);
        Assert.False(db.Database.HasPendingModelChanges());
        var downgrade = await Assert.ThrowsAsync<NotSupportedException>(() => db.GetService<IMigrator>().MigrateAsync(previous));
        Assert.Contains("backup", downgrade.Message);
        Assert.Contains(await db.Database.GetAppliedMigrationsAsync(), m => m.EndsWith("SynchronizeAgendaWithPoRequirements", StringComparison.Ordinal));
    }
}
