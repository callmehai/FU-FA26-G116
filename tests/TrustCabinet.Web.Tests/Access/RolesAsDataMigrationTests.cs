using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql;
using TrustCabinet.Domain.Access;
using TrustCabinet.Infrastructure;

namespace TrustCabinet.Web.Tests.Access;

/// <summary>
/// The RolesAsData migration against a real PostgreSQL holding data in the old shape (SCRUM-63).
///
/// The most important tests of the story: the migration is hand-edited precisely because the
/// scaffolded one erased every account's role and the whole matrix — and with no role holding
/// AdminUsers, nobody could sign in to repair it (ADR-0007). What a unit test cannot show, and
/// these do, is that the SQL actually carries each row across.
///
/// Every other database test builds its schema with EnsureCreated; this one runs the real
/// migrations, which is the only way to have `users.role` as text to migrate from.
///
/// Needs the dev database: `docker compose up -d`.
/// </summary>
public class RolesAsDataMigrationTests : IAsyncLifetime
{
    private readonly PostgreSqlTestDatabase _database = new(
        PostgresTestDatabase.ConnectionString("trustcabinet_migration_tests"));

    /// <summary>The migration just before RolesAsData: roles are still enum names in text columns.</summary>
    private const string BeforeRolesAsData = "20260922103215_FixAttendanceDefault";

    private TrustCabinetDbContext _db = null!;

    public async Task InitializeAsync()
    {
        _db = new TrustCabinetDbContext(
            new DbContextOptionsBuilder<TrustCabinetDbContext>()
                .UseNpgsql(_database.DataSource, npgsql => npgsql.MigrationsHistoryTable("__ef_migrations_history"))
                .UseSnakeCaseNamingConvention()
                .Options);

        try
        {
            await MigrateToAsync(BeforeRolesAsData);
        }
        catch (NpgsqlException e)
        {
            throw new InvalidOperationException(
                "These tests need the development PostgreSQL. Start it with "
                + "`docker compose up -d postgres`.", e);
        }
    }

    public async Task DisposeAsync()
    {
        await _db.DisposeAsync();
        await _database.DisposeAsync();
    }

    private Task MigrateToAsync(string? target = null)
        => _db.GetService<IMigrator>().MigrateAsync(target);

    /// <summary>An account in the pre-RolesAsData shape: its role is the enum's name.</summary>
    private Task InsertOldUserAsync(string email, string role, bool isActive = true)
        => _db.Database.ExecuteSqlAsync($"""
            INSERT INTO users (id, full_name, email, password_hash, role, is_active, created_at, updated_at)
            VALUES ({Guid.CreateVersion7()}, 'Người Dùng', {email}, 'hash', {role}, {isActive}, now(), now())
            """);

    private async Task<Dictionary<string, Guid>> RoleIdsByEmailAsync()
    {
        var rows = await _db.Database
            .SqlQuery<EmailRole>($"SELECT email::text AS email, role_id AS role_id FROM users")
            .ToArrayAsync();

        return rows.ToDictionary(r => r.Email, r => r.RoleId);
    }

    private sealed record EmailRole(string Email, Guid RoleId);

    private sealed record Grant(Guid RoleId, string Capability);

    [Fact]
    public async Task Every_account_keeps_its_role_including_deactivated_ones()
    {
        await InsertOldUserAsync("chutoa@x.vn", "Chair");
        await InsertOldUserAsync("thuky@x.vn", "Secretary");
        await InsertOldUserAsync("thanhvien@x.vn", "Member");
        await InsertOldUserAsync("quantri@x.vn", "Admin");
        await InsertOldUserAsync("danghi@x.vn", "Admin", isActive: false);

        await MigrateToAsync();

        Assert.Equal(
            new Dictionary<string, Guid>
            {
                ["chutoa@x.vn"] = BuiltInRoles.Chair,
                ["thuky@x.vn"] = BuiltInRoles.Secretary,
                ["thanhvien@x.vn"] = BuiltInRoles.Member,
                ["quantri@x.vn"] = BuiltInRoles.Admin,
                ["danghi@x.vn"] = BuiltInRoles.Admin,
            },
            await RoleIdsByEmailAsync());
    }

    [Fact]
    public async Task The_matrix_keeps_every_row_including_an_administrators_edits()
    {
        // InitialCreate inserted the five default rows; an administrator then moved AdminUsers to
        // Secretary as well (SCRUM-60). All six must come across, pointing at the right roles.
        await _db.Database.ExecuteSqlAsync(
            $"INSERT INTO role_capabilities (role, capability) VALUES ('Secretary', 'AdminUsers')");

        await MigrateToAsync();

        var grants = await _db.Database
            .SqlQuery<Grant>($"SELECT role_id AS role_id, capability::text AS capability FROM role_capabilities")
            .ToArrayAsync();

        Assert.Equal(
            [
                new Grant(BuiltInRoles.Chair, "MeetingApprover"),
                new Grant(BuiltInRoles.Chair, "MeetingCreator"),
                new Grant(BuiltInRoles.Secretary, "AdminUsers"),
                new Grant(BuiltInRoles.Secretary, "MeetingCreator"),
                new Grant(BuiltInRoles.Secretary, "MinutesEditor"),
                new Grant(BuiltInRoles.Admin, "AdminUsers"),
            ],
            grants
                .OrderBy(g => BuiltInRoles.SortKey(g.RoleId))
                .ThenBy(g => g.Capability, StringComparer.Ordinal));
    }

    [Fact]
    public async Task The_four_built_in_roles_arrive_with_their_fixed_ids()
    {
        await MigrateToAsync();

        var roles = await _db.Roles.OrderBy(r => r.Name).ToArrayAsync();

        Assert.Equal(
            BuiltInRoles.All.Order(),
            roles.Select(r => r.Id).Order());
        Assert.All(roles, r => Assert.True(r.IsBuiltIn));
        Assert.Equal(
            ["Chủ toạ", "Quản trị viên", "Thành viên", "Thư ký"],
            roles.Select(r => r.Name).Order(StringComparer.Ordinal));
    }

    [Fact]
    public async Task A_role_value_that_is_not_an_enum_name_stops_the_migration()
    {
        // Guessing a role for it would silently hand someone the wrong rights.
        await InsertOldUserAsync("la@x.vn", "SuperAdmin");

        var failure = await Assert.ThrowsAsync<PostgresException>(() => MigrateToAsync());

        Assert.Contains("users.role", failure.MessageText, StringComparison.Ordinal);

        // Rolled back whole: the account still has its old role column, and no roles table exists.
        Assert.Equal(
            "SuperAdmin",
            await _db.Database
                .SqlQuery<string>($"SELECT role AS \"Value\" FROM users WHERE email = 'la@x.vn'")
                .SingleAsync());
    }

    [Fact]
    public async Task Reverting_puts_every_enum_name_back()
    {
        await InsertOldUserAsync("thuky@x.vn", "Secretary");
        await MigrateToAsync();

        await MigrateToAsync(BeforeRolesAsData);

        Assert.Equal(
            "Secretary",
            await _db.Database
                .SqlQuery<string>($"SELECT role AS \"Value\" FROM users WHERE email = 'thuky@x.vn'")
                .SingleAsync());
        Assert.Equal(
            5,
            await _db.Database
                .SqlQuery<int>($"SELECT count(*)::int AS \"Value\" FROM role_capabilities WHERE role IS NOT NULL")
                .SingleAsync());
    }

    [Fact]
    public async Task Reverting_is_refused_while_a_custom_role_exists()
    {
        // A role an administrator created has no enum name to go back to; folding its holders into
        // Member would silently change what they may do.
        await MigrateToAsync();
        await _db.Database.ExecuteSqlAsync($"""
            INSERT INTO roles (id, name, is_built_in, created_at, updated_at)
            VALUES ({Guid.CreateVersion7()}, 'Thư ký phó', false, now(), now())
            """);

        var failure = await Assert.ThrowsAsync<PostgresException>(() => MigrateToAsync(BeforeRolesAsData));

        Assert.Contains("Cannot revert RolesAsData", failure.MessageText, StringComparison.Ordinal);
        Assert.True(await _db.Roles.AnyAsync(r => r.Name == "Thư ký phó"));
    }
}
