using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using TrustCabinet.Domain.Access;
using TrustCabinet.Domain.Entities;
using TrustCabinet.Domain.Enums;
using TrustCabinet.Infrastructure;
using TrustCabinet.Infrastructure.Access;
using TrustCabinet.Infrastructure.Seed;
using TrustCabinet.Web.Tests.Users;

namespace TrustCabinet.Web.Tests.Access;

/// <summary>
/// The real role repository and service against a real PostgreSQL (SCRUM-63).
///
/// These cover what a fake cannot: citext comparing Vietnamese names without regard to case, the
/// unique index behind MSG21, the RESTRICT foreign key behind MSG25, and the cascade that takes a
/// deleted role's capabilities with it. The races are staged with a repository that answers one
/// check wrongly — exactly the view a request has when another commits between its check and its
/// write — so what is under test is how the database refuses, and that the refusal becomes a
/// message rather than a 500.
///
/// Needs the dev database: `docker compose up -d`. Fails rather than skips when it is
/// unreachable, for the same reason UserRepositoryTests does.
/// </summary>
public class RoleRepositoryTests : IAsyncLifetime
{
    private readonly PostgreSqlTestDatabase _database = new(
        PostgresTestDatabase.ConnectionString("trustcabinet_role_tests"));

    private TrustCabinetDbContext _db = null!;

    private TrustCabinetDbContext NewContext() => new(
        new DbContextOptionsBuilder<TrustCabinetDbContext>()
            .UseNpgsql(_database.DataSource)
            .UseSnakeCaseNamingConvention()
            .Options);

    public async Task InitializeAsync()
    {
        _db = NewContext();

        try
        {
            await _db.Database.EnsureCreatedAsync();
        }
        catch (Npgsql.NpgsqlException e)
        {
            throw new InvalidOperationException(
                "These tests need the development PostgreSQL. Start it with "
                + "`docker compose up -d postgres`.", e);
        }

        // The built-in roles and the default matrix, as a real database has them.
        await ReferenceDataSeeder.SeedAsync(_db);
        _db.ChangeTracker.Clear();
    }

    public async Task DisposeAsync()
    {
        await _db.DisposeAsync();
        await _database.DisposeAsync();
    }

    private static RoleService ServiceOver(TrustCabinetDbContext db, IRoleRepository? roles = null)
        => new(
            roles ?? new RoleRepository(db),
            new FakeAuditService(),
            // The real guard: it opens the transaction the write runs in, and a refused statement
            // inside it must not poison that transaction.
            new AdministratorInvariantGuard(db),
            NullLogger<RoleService>.Instance);

    private async Task<User> AddUserAsync(Guid roleId, bool isActive = true)
    {
        var user = new User
        {
            FullName = "Người Dùng",
            Email = $"{Guid.CreateVersion7():N}@trustcabinet.local",
            PasswordHash = "hash",
            RoleId = roleId,
            IsActive = isActive,
        };

        _db.Users.Add(user);
        await _db.SaveChangesAsync();
        _db.ChangeTracker.Clear();

        return user;
    }

    private async Task<Guid> CreateRoleAsync(string name, params string[] capabilities)
    {
        var result = await ServiceOver(_db).CreateAsync(
            new NewRole(name, null, capabilities), Guid.CreateVersion7());

        Assert.True(result.Succeeded, $"Creating '{name}' failed: {result.Outcome}.");
        _db.ChangeTracker.Clear();

        return result.Role!.Id;
    }

    [Fact]
    public async Task List_counts_every_holder_including_deactivated_ones_in_display_order()
    {
        var deputy = await CreateRoleAsync("Thư ký phó");
        await AddUserAsync(BuiltInRoles.Chair);
        await AddUserAsync(BuiltInRoles.Chair, isActive: false);
        await AddUserAsync(deputy, isActive: false);

        var list = await new RoleRepository(_db).ListAsync();

        Assert.Equal([.. BuiltInRoles.All, deputy], list.Select(r => r.Id));
        Assert.Equal(2, list.Single(r => r.Id == BuiltInRoles.Chair).UserCount);
        Assert.Equal(1, list.Single(r => r.Id == deputy).UserCount);
        Assert.Equal(0, list.Single(r => r.Id == BuiltInRoles.Admin).UserCount);
    }

    [Theory]
    [InlineData("THƯ KÝ")]
    [InlineData("thư ký")]
    [InlineData("Thư Ký")]
    public async Task Names_are_compared_without_regard_to_case_including_vietnamese(string name)
    {
        var repository = new RoleRepository(_db);

        Assert.True(await repository.NameExistsAsync(name));
        Assert.False(await repository.NameExistsAsync(name, exceptId: BuiltInRoles.Secretary));
    }

    [Fact]
    public async Task Create_stores_the_role_and_its_capabilities_in_one_go()
    {
        var id = await CreateRoleAsync("Thư ký phó", "MinutesEditor", "MeetingCreator");

        var role = await _db.Roles.SingleAsync(r => r.Id == id);
        Assert.False(role.IsBuiltIn);

        Assert.Equal(
            [Capability.MeetingCreator, Capability.MinutesEditor],
            await _db.RoleCapabilities.Where(rc => rc.RoleId == id)
                .Select(rc => rc.Capability).OrderBy(c => c).ToArrayAsync());
    }

    [Fact]
    public async Task The_unique_index_refuses_a_name_that_differs_only_in_case_and_answers_MSG21()
    {
        // Two creates raced past the name check: this repository says the name is free, as it was
        // when both requests looked. The citext unique index refuses the second.
        await CreateRoleAsync("Thư ký phó");

        var result = await ServiceOver(_db, new NameCheckMisses(new RoleRepository(_db)))
            .CreateAsync(new NewRole("THƯ KÝ PHÓ", null, ["MinutesEditor"]), Guid.CreateVersion7());

        Assert.Equal(RoleOutcome.DuplicateName, result.Outcome);

        // Neither the role nor its capability row got in, and the transaction was left usable.
        _db.ChangeTracker.Clear();
        Assert.Equal(1, await _db.Roles.CountAsync(r => !r.IsBuiltIn));
        Assert.Equal(ReferenceData.RoleCapabilities.Length, await _db.RoleCapabilities.CountAsync());
    }

    [Fact]
    public async Task Renaming_into_a_taken_name_is_refused_by_the_index_too()
    {
        await CreateRoleAsync("Uỷ viên thường trực");
        var deputy = await CreateRoleAsync("Thư ký phó");

        var result = await ServiceOver(_db, new NameCheckMisses(new RoleRepository(_db)))
            .UpdateAsync(deputy, new RoleEdit("uỷ viên thường trực", null), Guid.CreateVersion7());

        Assert.Equal(RoleOutcome.DuplicateName, result.Outcome);

        await using var fresh = NewContext();
        Assert.Equal("Thư ký phó", (await fresh.Roles.SingleAsync(r => r.Id == deputy)).Name);
    }

    [Fact]
    public async Task Deleting_a_role_takes_its_capabilities_with_it()
    {
        var deputy = await CreateRoleAsync("Thư ký phó", "MinutesEditor", "MeetingCreator");

        var result = await ServiceOver(_db).DeleteAsync(deputy, Guid.CreateVersion7());

        Assert.True(result.Succeeded);
        _db.ChangeTracker.Clear();
        Assert.False(await _db.Roles.AnyAsync(r => r.Id == deputy));
        Assert.False(await _db.RoleCapabilities.AnyAsync(rc => rc.RoleId == deputy));

        // Nobody else's capabilities went with it.
        Assert.Equal(ReferenceData.RoleCapabilities.Length, await _db.RoleCapabilities.CountAsync());
    }

    [Fact]
    public async Task A_role_held_only_by_a_deactivated_account_is_not_deleted()
    {
        var deputy = await CreateRoleAsync("Thư ký phó");
        await AddUserAsync(deputy, isActive: false);

        var result = await ServiceOver(_db).DeleteAsync(deputy, Guid.CreateVersion7());

        Assert.Equal(RoleOutcome.InUse, result.Outcome);
        Assert.Equal(1, result.UserCount);
        Assert.True(await _db.Roles.AnyAsync(r => r.Id == deputy));
    }

    [Fact]
    public async Task An_assignment_that_slips_in_before_the_delete_is_refused_by_the_foreign_key()
    {
        // The diagram's "Role was assigned to a user account in the meantime". The count says
        // nobody — as it did when the request looked — but an account now holds the role. The
        // RESTRICT foreign key refuses the delete; the answer is MSG25 with the real count, not a
        // 500, and the role and its capabilities are still there.
        var deputy = await CreateRoleAsync("Thư ký phó", "MinutesEditor");
        await AddUserAsync(deputy);

        var result = await ServiceOver(_db, new FirstCountMisses(new RoleRepository(_db)))
            .DeleteAsync(deputy, Guid.CreateVersion7());

        Assert.Equal(RoleOutcome.InUse, result.Outcome);
        Assert.Equal(1, result.UserCount);

        await using var fresh = NewContext();
        Assert.True(await fresh.Roles.AnyAsync(r => r.Id == deputy));
        Assert.True(await fresh.RoleCapabilities.AnyAsync(rc => rc.RoleId == deputy));
    }

    [Fact]
    public async Task The_database_refuses_to_delete_a_built_in_role_somebody_holds()
    {
        // The service never gets this far for a built-in role (MSG24); this pins the last line
        // behind it — RESTRICT, not CASCADE, from users.
        await AddUserAsync(BuiltInRoles.Member);

        await Assert.ThrowsAsync<DbUpdateException>(async () =>
        {
            _db.Roles.Remove(await _db.Roles.SingleAsync(r => r.Id == BuiltInRoles.Member));
            await _db.SaveChangesAsync();
        });
    }

    /// <summary>A repository whose name check always says "free" — a request that lost the race.</summary>
    private sealed class NameCheckMisses(IRoleRepository inner) : DelegatingRoleRepository(inner)
    {
        public override Task<bool> NameExistsAsync(
            string name, Guid? exceptId = null, CancellationToken ct = default)
            => Task.FromResult(false);
    }

    /// <summary>
    /// A repository whose first user count says nobody — the view of a request that counted
    /// before another assigned the role.
    /// </summary>
    private sealed class FirstCountMisses(IRoleRepository inner) : DelegatingRoleRepository(inner)
    {
        private bool _counted;

        public override Task<int> CountUsersAsync(Guid roleId, CancellationToken ct = default)
        {
            if (_counted)
            {
                return base.CountUsersAsync(roleId, ct);
            }

            _counted = true;
            return Task.FromResult(0);
        }
    }

    private abstract class DelegatingRoleRepository(IRoleRepository inner) : IRoleRepository
    {
        public Task<IReadOnlyCollection<RoleSummary>> ListAsync(CancellationToken ct = default)
            => inner.ListAsync(ct);

        public Task<Role?> FindByIdAsync(Guid id, CancellationToken ct = default)
            => inner.FindByIdAsync(id, ct);

        public virtual Task<bool> NameExistsAsync(
            string name, Guid? exceptId = null, CancellationToken ct = default)
            => inner.NameExistsAsync(name, exceptId, ct);

        public virtual Task<int> CountUsersAsync(Guid roleId, CancellationToken ct = default)
            => inner.CountUsersAsync(roleId, ct);

        public Task<IReadOnlyCollection<Capability>> GetCapabilitiesAsync(
            Guid roleId, CancellationToken ct = default)
            => inner.GetCapabilitiesAsync(roleId, ct);

        public Task AddAsync(
            Role role, IReadOnlyCollection<Capability> capabilities, CancellationToken ct = default)
            => inner.AddAsync(role, capabilities, ct);

        public void Remove(Role role) => inner.Remove(role);

        public Task SaveChangesAsync(CancellationToken ct = default) => inner.SaveChangesAsync(ct);
    }
}
