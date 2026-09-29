using Microsoft.EntityFrameworkCore;
using TrustCabinet.Infrastructure;
using TrustCabinet.Infrastructure.Access;

namespace TrustCabinet.Web.Tests.Access;

/// <summary>
/// That the shared lock actually serialises — against a real PostgreSQL, because an advisory lock
/// is the one thing no fake can stand in for. A fake that simply runs the callback proves the
/// write went through the guard; only a real server proves the second caller waits.
///
/// The hole this closes is described in <c>IAdministratorInvariantGuard</c>: user writes and
/// role-matrix writes both decide "is anyone still able to administer?", and before this they took
/// different locks — one on <c>role_capabilities</c>, one on nothing at all — so neither ever
/// blocked the other and two individually legal requests could combine into a system nobody can
/// administer.
///
/// Needs the dev database: `docker compose up -d postgres`. Fails rather than skips when it is
/// unreachable, like <c>UserRepositoryTests</c>.
/// </summary>
public class AdministratorInvariantGuardTests : IAsyncLifetime
{
    private readonly PostgreSqlTestDatabase _database = new(
        "Host=localhost;Port=5432;Database=trustcabinet_lock_tests;Username=trustcabinet;Password=123");

    private TrustCabinetDbContext _db = null!;

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
    }

    public async Task DisposeAsync()
    {
        await _db.DisposeAsync();
        await _database.DisposeAsync();
    }

    private TrustCabinetDbContext NewContext() => new(
        new DbContextOptionsBuilder<TrustCabinetDbContext>()
            .UseNpgsql(_database.DataSource)
            .UseSnakeCaseNamingConvention()
            .Options);

    [Fact]
    public async Task A_second_write_waits_for_the_first_to_commit()
    {
        // Two connections, because a lock is only meaningful between them — the same DbContext
        // would re-enter its own transaction and prove nothing.
        await using var otherDb = NewContext();

        var firstIsInside = new TaskCompletionSource();
        var firstMayFinish = new TaskCompletionSource();
        var order = new List<string>();
        var orderLock = new Lock();

        void Record(string what)
        {
            lock (orderLock)
            {
                order.Add(what);
            }
        }

        var first = new AdministratorInvariantGuard(_db).WriteAsync(async _ =>
        {
            Record("first entered");
            firstIsInside.SetResult();

            // Held until the test says so, with the lock taken.
            await firstMayFinish.Task;
            Record("first leaving");
            return 0;
        });

        await firstIsInside.Task;

        var second = new AdministratorInvariantGuard(otherDb).WriteAsync(_ =>
        {
            Record("second entered");
            return Task.FromResult(0);
        });

        // The second write must not get in while the first holds the lock. Waiting on a timeout is
        // the only way to assert "did not happen yet"; it is generous enough not to be flaky and
        // still fails fast if the lock is not being taken at all.
        var finishedEarly = await Task.WhenAny(second, Task.Delay(TimeSpan.FromSeconds(2)));

        Assert.NotSame(second, finishedEarly);

        firstMayFinish.SetResult();

        await first;
        await second;

        // The whole point: the second body ran after the first had finished, not alongside it.
        Assert.Equal(["first entered", "first leaving", "second entered"], order);
    }

    [Fact]
    public async Task The_lock_is_released_when_a_write_throws()
    {
        // A transaction-scoped advisory lock goes with the transaction however it ends. If it
        // leaked on the failure path, one failed request would wedge every later administrative
        // write until the connection was recycled.
        await using var otherDb = NewContext();

        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            new AdministratorInvariantGuard(_db).WriteAsync<int>(
                _ => throw new InvalidOperationException("khong luu duoc")));

        var second = new AdministratorInvariantGuard(otherDb)
            .WriteAsync(_ => Task.FromResult(7));

        var completed = await Task.WhenAny(second, Task.Delay(TimeSpan.FromSeconds(5)));

        Assert.Same(second, completed);
        Assert.Equal(7, await second);
    }

    [Fact]
    public async Task A_nested_write_runs_in_the_outer_transaction()
    {
        // Advisory locks are re-entrant within one session, so a nested call would take the lock
        // again and commit the outer transaction early on its way out. It has to join instead.
        var guard = new AdministratorInvariantGuard(_db);

        var result = await guard.WriteAsync(async _ =>
        {
            var transaction = _db.Database.CurrentTransaction;

            var inner = await guard.WriteAsync(_ => Task.FromResult("inner"));

            // Same transaction still open after the nested call returned: it neither committed
            // nor started one of its own.
            Assert.NotNull(_db.Database.CurrentTransaction);
            Assert.Same(transaction, _db.Database.CurrentTransaction);

            return inner;
        });

        Assert.Equal("inner", result);
        Assert.Null(_db.Database.CurrentTransaction);
    }
}
