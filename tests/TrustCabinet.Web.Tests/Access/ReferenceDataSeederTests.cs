using Microsoft.EntityFrameworkCore;
using TrustCabinet.Domain.Access;
using TrustCabinet.Domain.Enums;
using TrustCabinet.Infrastructure;
using TrustCabinet.Infrastructure.Seed;

namespace TrustCabinet.Web.Tests.Access;

/// <summary>
/// The reference data seeder against a real PostgreSQL.
///
/// Worth a real database rather than a fake: the whole point of this seeder is what it does to a
/// table, and the rule it enforces ("only when completely empty") is the one thing standing
/// between an administrator's configuration and a restart quietly undoing it.
///
/// Needs the dev database: `docker compose up -d`. Fails rather than skips when it is unreachable,
/// for the same reason UserRepositoryTests does.
/// </summary>
public class ReferenceDataSeederTests : IAsyncLifetime
{
    private static readonly string ConnectionString =
        PostgresTestDatabase.ConnectionString("trustcabinet_seed_tests");

    private TrustCabinetDbContext _db = null!;

    public async Task InitializeAsync()
    {
        var options = new DbContextOptionsBuilder<TrustCabinetDbContext>()
            .UseNpgsql(ConnectionString)
            .UseSnakeCaseNamingConvention()
            .Options;

        _db = new TrustCabinetDbContext(options);

        try
        {
            await _db.Database.EnsureDeletedAsync();
            await _db.Database.EnsureCreatedAsync();
        }
        catch (Npgsql.NpgsqlException e)
        {
            throw new InvalidOperationException(
                "These tests need the development PostgreSQL. Start it with "
                + "`docker compose up -d postgres`.", e);
        }

        // EnsureCreated builds the schema from the model, which no longer declares the matrix
        // through HasData — so the table starts empty, which is the state under test.
        await _db.RoleCapabilities.ExecuteDeleteAsync();
    }

    public async Task DisposeAsync()
    {
        await _db.Database.EnsureDeletedAsync();
        await _db.DisposeAsync();
    }

    [Fact]
    public async Task An_empty_table_gets_the_default_matrix()
    {
        await ReferenceDataSeeder.SeedAsync(_db);

        var rows = await _db.RoleCapabilities.ToArrayAsync();

        Assert.Equal(ReferenceData.RoleCapabilities.Length, rows.Length);

        // The one that matters: without it nobody can reach the administration screens at all.
        Assert.Contains(rows, rc => rc.Capability == Capability.AdminUsers);
    }

    [Fact]
    public async Task The_built_in_roles_are_inserted_before_the_matrix_that_references_them()
    {
        // EnsureCreated leaves `roles` empty too; the matrix could not be inserted without them.
        await ReferenceDataSeeder.SeedAsync(_db);

        Assert.Equal(
            BuiltInRoles.All.Order(),
            (await _db.Roles.Select(r => r.Id).ToArrayAsync()).Order());
    }

    [Fact]
    public async Task A_missing_built_in_role_is_put_back_but_an_existing_one_is_never_changed()
    {
        await ReferenceDataSeeder.SeedAsync(_db);
        _db.ChangeTracker.Clear();

        // A description edited in the database, and one built-in role gone (with its grants).
        await _db.Roles
            .Where(r => r.Id == BuiltInRoles.Chair)
            .ExecuteUpdateAsync(s => s.SetProperty(r => r.Description, "Mô tả do DBA sửa."));
        await _db.Roles.Where(r => r.Id == BuiltInRoles.Member).ExecuteDeleteAsync();

        await ReferenceDataSeeder.SeedAsync(_db);
        _db.ChangeTracker.Clear();

        Assert.True(await _db.Roles.AnyAsync(r => r.Id == BuiltInRoles.Member));
        Assert.Equal(
            "Mô tả do DBA sửa.",
            (await _db.Roles.SingleAsync(r => r.Id == BuiltInRoles.Chair)).Description);
    }

    [Fact]
    public async Task Seeding_twice_does_not_duplicate_anything()
    {
        await ReferenceDataSeeder.SeedAsync(_db);
        _db.ChangeTracker.Clear();
        await ReferenceDataSeeder.SeedAsync(_db);

        // Safe on every boot, which is what lets it be switched on in a deployment.
        Assert.Equal(
            ReferenceData.RoleCapabilities.Length,
            await _db.RoleCapabilities.CountAsync());
        Assert.Equal(BuiltInRoles.All.Count, await _db.Roles.CountAsync());
    }

    [Fact]
    public async Task An_administrators_configuration_survives_a_reseed()
    {
        // What an administrator might do through UC-10: move AdminUsers to Secretary and take
        // MinutesEditor away from them.
        await ReferenceDataSeeder.SeedAsync(_db);
        _db.ChangeTracker.Clear();

        await _db.RoleCapabilities
            .Where(rc => rc.RoleId == BuiltInRoles.Secretary && rc.Capability == Capability.MinutesEditor)
            .ExecuteDeleteAsync();

        // Re-running the seeder must not put it back. Filling in individually missing rows would
        // be indistinguishable from an administrator deliberately revoking them, so every restart
        // would silently undo their work — the exact failure this seeder is shaped to avoid.
        await ReferenceDataSeeder.SeedAsync(_db);

        var secretary = await _db.RoleCapabilities
            .Where(rc => rc.RoleId == BuiltInRoles.Secretary)
            .Select(rc => rc.Capability)
            .ToArrayAsync();

        Assert.DoesNotContain(Capability.MinutesEditor, secretary);
    }
}
