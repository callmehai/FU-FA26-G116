using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using TrustCabinet.Domain.Access;
using TrustCabinet.Domain.Entities;
using TrustCabinet.Domain.Enums;
using TrustCabinet.Infrastructure;
using TrustCabinet.Infrastructure.Access;
using TrustCabinet.Infrastructure.Seed;
using TrustCabinet.Web.Tests.Authentication;
using TrustCabinet.Web.Tests.Users;

namespace TrustCabinet.Web.Tests.Access;

/// <summary>
/// The real repository and service against a real PostgreSQL.
///
/// These cover what a fake cannot: the transaction, the table lock, and the SQL the counting rule
/// compiles to. The concurrency case is the reason this file exists — a single-threaded fake has
/// no way to contend for a lock, so the guard against two administrators locking everyone out can
/// only be demonstrated here.
///
/// Needs the dev database: `docker compose up -d`. Fails rather than skips when it is
/// unreachable, for the same reason UserRepositoryTests does.
/// </summary>
public class RolePermissionRepositoryTests : IAsyncLifetime
{
    private const string ConnectionString =
        "Host=localhost;Port=5432;Database=trustcabinet_roleperm_tests;Username=trustcabinet;Password=123";

    private TrustCabinetDbContext _db = null!;

    private static DbContextOptions<TrustCabinetDbContext> Options()
        => new DbContextOptionsBuilder<TrustCabinetDbContext>()
            .UseNpgsql(ConnectionString)
            .UseSnakeCaseNamingConvention()
            .Options;

    public async Task InitializeAsync()
    {
        _db = new TrustCabinetDbContext(Options());

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

        await _db.RoleCapabilities.ExecuteDeleteAsync();
        await ReferenceDataSeeder.SeedAsync(_db);
        _db.ChangeTracker.Clear();
    }

    public async Task DisposeAsync()
    {
        await _db.Database.EnsureDeletedAsync();
        await _db.DisposeAsync();
    }

    private async Task<User> AddActiveUserAsync(SystemRole role)
    {
        var user = new User
        {
            FullName = "Người Dùng",
            Email = $"{Guid.CreateVersion7():N}@trustcabinet.local",
            PasswordHash = "hash",
            Role = role,
            IsActive = true,
        };

        _db.Users.Add(user);
        await _db.SaveChangesAsync();
        _db.ChangeTracker.Clear();

        return user;
    }

    private static RolePermissionService ServiceOver(TrustCabinetDbContext db)
        => new(
            new RolePermissionRepository(db),
            new FakeAuditService(),
            new FakeRefreshTokenStore(),
            // The real guard, not the fake: these tests run against a real PostgreSQL, and the
            // lock is the thing under test on the concurrency cases below.
            new AdministratorInvariantGuard(db),
            NullLogger<RolePermissionService>.Instance);

    [Fact]
    public async Task The_counting_query_only_counts_active_accounts()
    {
        var admin = await AddActiveUserAsync(SystemRole.Admin);

        var repository = new RolePermissionRepository(_db);

        Assert.Equal(1, await repository.CountActiveUsersInRolesAsync([SystemRole.Admin]));

        // Deactivated: still a row in `users`, but not somebody who can sign in and administer.
        var tracked = await _db.Users.FirstAsync(u => u.Id == admin.Id);
        tracked.IsActive = false;
        await _db.SaveChangesAsync();
        _db.ChangeTracker.Clear();

        Assert.Equal(0, await repository.CountActiveUsersInRolesAsync([SystemRole.Admin]));

        // And an empty set of roles is answered without a query at all.
        Assert.Equal(0, await repository.CountActiveUsersInRolesAsync([]));
    }

    [Fact]
    public async Task Replacing_capabilities_writes_only_the_difference()
    {
        await AddActiveUserAsync(SystemRole.Admin);

        var result = await ServiceOver(_db).UpdateAsync(
            SystemRole.Secretary, [Capability.MinutesEditor], Guid.CreateVersion7());

        Assert.True(result.Succeeded);
        _db.ChangeTracker.Clear();

        var secretary = await _db.RoleCapabilities
            .Where(rc => rc.Role == SystemRole.Secretary)
            .Select(rc => rc.Capability)
            .ToArrayAsync();

        // MeetingCreator removed, MinutesEditor kept — the row that did not change was not
        // deleted and re-inserted.
        Assert.Equal([Capability.MinutesEditor], secretary);
    }

    [Fact]
    public async Task Two_administrators_cannot_strip_the_last_administering_role_at_once()
    {
        // The race the table lock exists to stop. Both roles administer and both have a user, so
        // either request alone is legal; together they would leave nobody able to administer.
        await AddActiveUserAsync(SystemRole.Admin);
        await AddActiveUserAsync(SystemRole.Secretary);

        _db.RoleCapabilities.Add(
            new RoleCapability { Role = SystemRole.Secretary, Capability = Capability.AdminUsers });
        await _db.SaveChangesAsync();
        _db.ChangeTracker.Clear();

        // Separate contexts, as two concurrent requests would have.
        await using var first = new TrustCabinetDbContext(Options());
        await using var second = new TrustCabinetDbContext(Options());

        var actor = Guid.CreateVersion7();

        var results = await Task.WhenAll(
            ServiceOver(first).UpdateAsync(SystemRole.Admin, [], actor),
            ServiceOver(second).UpdateAsync(
                SystemRole.Secretary, [Capability.MinutesEditor], actor));

        // Exactly one may win. Without the lock both would read a matrix in which the other role
        // still holds AdminUsers, both would pass validation, and both would commit.
        Assert.Equal(1, results.Count(r => r.Succeeded));

        var stillAdministering = await _db.RoleCapabilities
            .CountAsync(rc => rc.Capability == Capability.AdminUsers);

        Assert.True(
            stillAdministering >= 1,
            "No role holds AdminUsers any more — the two updates raced past the lock-out rule.");
    }
}
