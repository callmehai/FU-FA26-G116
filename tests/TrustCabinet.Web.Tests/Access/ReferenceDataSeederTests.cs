using Microsoft.EntityFrameworkCore;
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
    private const string ConnectionString =
        "Host=localhost;Port=5432;Database=trustcabinet_seed_tests;Username=trustcabinet;Password=123";

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
    public async Task Seeding_twice_does_not_duplicate_anything()
    {
        await ReferenceDataSeeder.SeedAsync(_db);
        _db.ChangeTracker.Clear();
        await ReferenceDataSeeder.SeedAsync(_db);

        // Safe on every boot, which is what lets it be switched on in a deployment.
        Assert.Equal(
            ReferenceData.RoleCapabilities.Length,
            await _db.RoleCapabilities.CountAsync());
    }

    [Fact]
    public async Task An_administrators_configuration_survives_a_reseed()
    {
        // What an administrator might do through UC-10: move AdminUsers to Secretary and take
        // MinutesEditor away from them.
        await ReferenceDataSeeder.SeedAsync(_db);
        _db.ChangeTracker.Clear();

        await _db.RoleCapabilities
            .Where(rc => rc.Role == SystemRole.Secretary && rc.Capability == Capability.MinutesEditor)
            .ExecuteDeleteAsync();

        // Re-running the seeder must not put it back. Filling in individually missing rows would
        // be indistinguishable from an administrator deliberately revoking them, so every restart
        // would silently undo their work — the exact failure this seeder is shaped to avoid.
        await ReferenceDataSeeder.SeedAsync(_db);

        var secretary = await _db.RoleCapabilities
            .Where(rc => rc.Role == SystemRole.Secretary)
            .Select(rc => rc.Capability)
            .ToArrayAsync();

        Assert.DoesNotContain(Capability.MinutesEditor, secretary);
    }
}
