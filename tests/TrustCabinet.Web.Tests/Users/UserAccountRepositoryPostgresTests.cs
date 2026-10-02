using Microsoft.EntityFrameworkCore;
using TrustCabinet.Domain.Access;
using TrustCabinet.Domain.Entities;
using TrustCabinet.Infrastructure;
using TrustCabinet.Infrastructure.Seed;
using TrustCabinet.Infrastructure.Users;

namespace TrustCabinet.Web.Tests.Users;

/// <summary>
/// The parts of <see cref="UserAccountRepository"/> only a real PostgreSQL can prove.
///
/// Needs the dev database: `docker compose up -d`. Fails rather than skips when it is unreachable,
/// for the same reason RedisRefreshTokenStoreTests does.
/// </summary>
public class UserAccountRepositoryPostgresTests : IAsyncLifetime
{
    private static readonly string ConnectionString =
        PostgresTestDatabase.ConnectionString("trustcabinet_account_repo_tests");

    private TrustCabinetDbContext _db = null!;

    private static TrustCabinetDbContext NewContext() => new(
        new DbContextOptionsBuilder<TrustCabinetDbContext>()
            .UseNpgsql(ConnectionString)
            .UseSnakeCaseNamingConvention()
            .Options);

    public async Task InitializeAsync()
    {
        _db = NewContext();

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

        _db.Roles.AddRange(ReferenceData.Roles());
        await _db.SaveChangesAsync();
        _db.ChangeTracker.Clear();
    }

    public async Task DisposeAsync()
    {
        await _db.Database.EnsureDeletedAsync();
        await _db.DisposeAsync();
    }

    [Fact]
    public async Task The_active_email_is_read_from_the_row_not_from_the_tracked_entity()
    {
        // UC-08 loads the account, spends up to a minute sending the email, then asks again. If
        // another administrator changed the row meanwhile, the answer must be the row's — an
        // entity query would return the instance this context already tracks, old values and all.
        var user = new User
        {
            FullName = "Người Dùng",
            Email = "cu@trustcabinet.local",
            PasswordHash = "hash",
            RoleId = BuiltInRoles.Member,
        };
        _db.Users.Add(user);
        await _db.SaveChangesAsync();
        _db.ChangeTracker.Clear();

        var repository = new UserAccountRepository(_db);
        var tracked = await repository.FindByIdAsync(user.Id);

        await using (var otherAdministrator = NewContext())
        {
            await otherAdministrator.Users
                .Where(u => u.Id == user.Id)
                .ExecuteUpdateAsync(s => s.SetProperty(u => u.Email, "moi@trustcabinet.local"));
        }

        Assert.Equal("cu@trustcabinet.local", tracked!.Email);
        Assert.Equal("moi@trustcabinet.local", await repository.FindActiveEmailAsync(user.Id));

        await using (var otherAdministrator = NewContext())
        {
            await otherAdministrator.Users
                .Where(u => u.Id == user.Id)
                .ExecuteUpdateAsync(s => s.SetProperty(u => u.IsActive, false));
        }

        Assert.Null(await repository.FindActiveEmailAsync(user.Id));
    }
}
