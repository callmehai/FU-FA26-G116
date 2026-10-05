using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using Microsoft.EntityFrameworkCore.Migrations.Operations;
using TrustCabinet.Infrastructure;

namespace TrustCabinet.Web.Tests.Meetings;

public sealed class MeetingSessionMigrationTests
{
    [Fact]
    public void Publication_session_migration_only_adds_the_new_table_and_its_index()
    {
        using var db = new TrustCabinetDbContextFactory().CreateDbContext([]);
        var assembly = db.GetService<IMigrationsAssembly>();
        var type = Assert.Single(assembly.Migrations,
            entry => entry.Key.EndsWith("_MeetingSessionsForPublication", StringComparison.Ordinal)).Value;
        var migration = assembly.CreateMigration(type, db.Database.ProviderName!);

        Assert.Equal(2, migration.UpOperations.Count);
        var table = Assert.Single(migration.UpOperations.OfType<CreateTableOperation>());
        Assert.Equal("meeting_sessions", table.Name);
        Assert.Equal(
            ["created_at", "id", "meeting_id", "planned_ends_at", "planned_starts_at", "status", "title", "updated_at"],
            table.Columns.Select(column => column.Name).Order(StringComparer.Ordinal));
        Assert.All(table.Columns, column => Assert.False(column.IsNullable));
        var foreignKey = Assert.Single(table.ForeignKeys);
        Assert.Equal("meetings", foreignKey.PrincipalTable);
        Assert.Equal(ReferentialAction.Cascade, foreignKey.OnDelete);
        var index = Assert.Single(migration.UpOperations.OfType<CreateIndexOperation>());
        Assert.Equal("meeting_sessions", index.Table);
        Assert.Equal(["meeting_id", "status"], index.Columns);
        Assert.False(index.IsUnique);
        Assert.Equal("meeting_sessions", Assert.IsType<DropTableOperation>(Assert.Single(migration.DownOperations)).Name);
    }
}
