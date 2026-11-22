using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql;
using TrustCabinet.Domain.Access;
using TrustCabinet.Infrastructure;
using TrustCabinet.Infrastructure.Seed;

namespace TrustCabinet.Web.Tests.Access;

/// <summary>
/// ADR-0074 — the SystemRolesWithoutChairSecretary migration against a real PostgreSQL holding data in the old shape: the
/// four built-in roles, people in each, an administrator's custom roles (one granting "Soạn biên bản", one already named
/// "Phê duyệt viên"). Nobody may be left without a role, and the custom roles keep everything but the capability that
/// no longer exists.
/// </summary>
public class SystemRolesMigrationTests : IAsyncLifetime
{
    private readonly PostgreSqlTestDatabase _database = new(
        PostgresTestDatabase.ConnectionString("trustcabinet_system_roles_migration_tests"));

    private const string Before = "20261122053859_TranscriptWordConfidence";
    private const string Migration = "20261122075357_SystemRolesWithoutChairSecretary";

    // The old built-in ids; BuiltInRoles.Approver is Chair's.
    private static readonly Guid Chair = new("01a0ce78-7202-7945-be13-d2022cf25203");
    private static readonly Guid Secretary = new("01a0ce78-7203-7c7f-8326-7a0baebf4c22");

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
            await _database.CreateFromTemplateAsync(TestSchema.MigratedTo(Before));
            await _db.GetService<IMigrator>().MigrateAsync(Before);
        }
        catch (NpgsqlException e)
        {
            throw new InvalidOperationException(
                "These tests need the development PostgreSQL. Start it with `docker compose up -d postgres`.", e);
        }
    }

    public async Task DisposeAsync()
    {
        await _db.DisposeAsync();
        await _database.DisposeAsync();
    }

    private Task MigrateAsync(string target = Migration) => _db.GetService<IMigrator>().MigrateAsync(target);

    private Task InsertUserAsync(string email, Guid roleId)
        => _db.Database.ExecuteSqlAsync($"""
            INSERT INTO users (id, full_name, email, password_hash, role_id, is_active, created_at, updated_at)
            VALUES ({Guid.CreateVersion7()}, 'Người Dùng', {email}, 'hash', {roleId}, true, now(), now())
            """);

    private Task InsertRoleAsync(Guid id, string name, params string[] capabilities)
        => _db.Database.ExecuteSqlAsync($"""
            INSERT INTO roles (id, name, description, is_built_in, created_at, updated_at)
            VALUES ({id}, {name}, NULL, false, now(), now());
            INSERT INTO role_capabilities (role_id, capability) SELECT {id}, unnest({capabilities});
            """);

    private async Task<string[]> CapabilitiesAsync(Guid roleId)
        => (await _db.RoleCapabilities.Where(rc => rc.RoleId == roleId).Select(rc => rc.Capability).ToArrayAsync())
            .Select(c => c.ToString()).Order(StringComparer.Ordinal).ToArray();

    [Fact]
    public async Task Holders_of_Chair_and_Secretary_end_up_Approvers_and_nobody_is_left_without_a_role()
    {
        await InsertUserAsync("chutoa@x.vn", Chair);
        await InsertUserAsync("thuky@x.vn", Secretary);
        await InsertUserAsync("thanhvien@x.vn", BuiltInRoles.Member);
        await InsertUserAsync("quantri@x.vn", BuiltInRoles.Admin);

        await MigrateAsync();

        var roles = await _db.Users.Where(u => u.Email.EndsWith("@x.vn")).ToDictionaryAsync(u => u.Email, u => u.RoleId);
        Assert.Equal(BuiltInRoles.Approver, roles["chutoa@x.vn"]);
        Assert.Equal(BuiltInRoles.Approver, roles["thuky@x.vn"]);
        Assert.Equal(BuiltInRoles.Member, roles["thanhvien@x.vn"]);
        Assert.Equal(BuiltInRoles.Admin, roles["quantri@x.vn"]);
        Assert.False(await _db.Roles.AnyAsync(r => r.Id == Secretary));
    }

    [Fact]
    public async Task The_built_in_roles_match_the_seed_text_and_defaults()
    {
        await MigrateAsync();

        var roles = await _db.Roles.Where(r => r.IsBuiltIn).ToArrayAsync();
        var seeded = ReferenceData.Roles();
        Assert.Equal(seeded.Select(r => (r.Id, r.Name, r.Description)).OrderBy(r => r.Id),
            roles.Select(r => (r.Id, r.Name, r.Description)).OrderBy(r => r.Id));
        foreach (var role in BuiltInRoles.All)
            Assert.Equal(
                ReferenceData.RoleCapabilities.Where(rc => rc.RoleId == role).Select(rc => rc.Capability.ToString())
                    .Order(StringComparer.Ordinal),
                await CapabilitiesAsync(role));
    }

    [Fact]
    public async Task A_custom_role_keeps_everything_but_Soan_bien_ban()
    {
        var allInOne = Guid.CreateVersion7();
        await InsertRoleAsync(allInOne, "All-in-one",
            "MeetingCreator", "MeetingApprover", "MinutesEditor", "AdminUsers", "VoiceCollector");
        await InsertUserAsync("allinone@x.vn", allInOne);

        await MigrateAsync();

        Assert.Equal(["AdminUsers", "MeetingApprover", "MeetingCreator", "VoiceCollector"], await CapabilitiesAsync(allInOne));
        Assert.Equal(allInOne, (await _db.Users.SingleAsync(u => u.Email == "allinone@x.vn")).RoleId);
        Assert.False(await _db.Database.SqlQuery<int>($"SELECT 1 AS \"Value\" FROM role_capabilities WHERE capability = 'MinutesEditor'").AnyAsync());
    }

    [Fact]
    public async Task A_custom_role_already_named_Phe_duyet_vien_is_renamed_aside_and_keeps_its_people()
    {
        var custom = Guid.CreateVersion7();
        await InsertRoleAsync(custom, "phê duyệt viên", "MeetingApprover");
        await InsertUserAsync("custom@x.vn", custom);

        await MigrateAsync();

        var renamed = await _db.Roles.SingleAsync(r => r.Id == custom);
        Assert.Equal($"phê duyệt viên ({custom.ToString()[..8]})", renamed.Name);
        Assert.Equal("Phê duyệt viên", (await _db.Roles.SingleAsync(r => r.Id == BuiltInRoles.Approver)).Name);
        Assert.Equal(custom, (await _db.Users.SingleAsync(u => u.Email == "custom@x.vn")).RoleId);
        Assert.Equal(["MeetingApprover"], await CapabilitiesAsync(custom));
    }

    [Fact]
    public async Task Reverting_puts_the_four_roles_back_and_moved_people_stay_on_Chu_toa()
    {
        await InsertUserAsync("thuky@x.vn", Secretary);
        await MigrateAsync();

        await MigrateAsync(Before);

        Assert.Equal("Chủ toạ", (await _db.Roles.SingleAsync(r => r.Id == Chair)).Name);
        Assert.Equal("Thư ký", (await _db.Roles.SingleAsync(r => r.Id == Secretary)).Name);
        Assert.Equal(Chair, (await _db.Users.SingleAsync(u => u.Email == "thuky@x.vn")).RoleId);
        Assert.Equal(["MeetingCreator", "MinutesEditor", "VoiceCollector"],
            (await _db.Database.SqlQuery<string>($"SELECT capability AS \"Value\" FROM role_capabilities WHERE role_id = {Secretary}")
                .ToArrayAsync()).Order(StringComparer.Ordinal));
    }
}
