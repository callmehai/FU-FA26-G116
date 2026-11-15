using System.Data;
using Microsoft.EntityFrameworkCore;
using Npgsql;
using TrustCabinet.Application.Abstractions;
using TrustCabinet.Domain.Entities;
using TrustCabinet.Infrastructure;
using TrustCabinet.Infrastructure.Persistence;

namespace TrustCabinet.Web.Tests.Persistence;

/// <summary>
/// SCRUM-143 (CA-00): <see cref="EfUnitOfWork"/> on a real PostgreSQL, because what it promises — commit,
/// rollback, joining an open transaction, the isolation level, and which storage failure becomes which
/// exception — is exactly what the in-memory provider does not have.
/// </summary>
public sealed class EfUnitOfWorkTests : IAsyncLifetime
{
    private readonly PostgreSqlTestDatabase _database = new(
        PostgresTestDatabase.ConnectionString("trustcabinet_unit_of_work_tests"));

    private TrustCabinetDbContext _db = null!;
    private EfUnitOfWork _unitOfWork = null!;

    public async Task InitializeAsync()
    {
        try
        {
            await _database.CreateFromTemplateAsync(TestSchema.EnsureCreated);
        }
        catch (NpgsqlException exception)
        {
            throw new InvalidOperationException(
                "The unit of work tests need the CI/development PostgreSQL. Set TRUSTCABINET_TEST_CONNECTION_STRING, "
                + "POSTGRES_PASSWORD, or the repository .env file, then run `docker compose up -d postgres`.", exception);
        }

        _db = NewContext();
        _unitOfWork = new EfUnitOfWork(_db);
    }

    public async Task DisposeAsync()
    {
        await _db.DisposeAsync();
        await _database.DisposeAsync();
    }

    [Fact]
    public async Task Work_that_returns_is_committed()
    {
        var role = NewRole();

        var result = await _unitOfWork.ExecuteAsync(async ct =>
        {
            Assert.NotNull(_db.Database.CurrentTransaction);
            _db.Roles.Add(role);
            await _unitOfWork.SaveChangesAsync(ct);
            return 7;
        });

        Assert.Equal(7, result);
        Assert.Null(_db.Database.CurrentTransaction);
        Assert.True(await IsStoredAsync(role));
    }

    [Fact]
    public async Task Work_that_throws_is_rolled_back_and_an_exception_that_is_not_about_storage_passes_through()
    {
        var role = NewRole();
        var failure = new InvalidOperationException("The use case gave up.");

        var thrown = await Assert.ThrowsAsync<InvalidOperationException>(() => _unitOfWork.ExecuteAsync(async ct =>
        {
            _db.Roles.Add(role);
            await _unitOfWork.SaveChangesAsync(ct);
            throw failure;
        }));

        Assert.Same(failure, thrown);
        Assert.Null(_db.Database.CurrentTransaction);
        Assert.False(await IsStoredAsync(role));
    }

    [Fact]
    public async Task CommitWhen_false_rolls_back_and_still_returns_the_result()
    {
        var kept = NewRole();
        var dropped = NewRole();

        var committed = await _unitOfWork.ExecuteAsync(ct => AddAsync(kept, "kept", ct), outcome => outcome == "kept");
        var rolledBack = await _unitOfWork.ExecuteAsync(ct => AddAsync(dropped, "refused", ct), outcome => outcome == "kept");

        Assert.Equal(("kept", "refused"), (committed, rolledBack));
        Assert.True(await IsStoredAsync(kept));
        Assert.False(await IsStoredAsync(dropped));
    }

    [Fact]
    public async Task Work_joins_a_transaction_opened_by_older_code_and_leaves_the_commit_to_it()
    {
        var role = NewRole();
        await using var outer = await _db.Database.BeginTransactionAsync();

        // commitWhen is not consulted inside an open transaction: its owner decides.
        await _unitOfWork.ExecuteAsync(ct => AddAsync(role, "refused", ct), outcome => outcome != "refused");

        Assert.Same(outer, _db.Database.CurrentTransaction);
        Assert.False(await IsStoredAsync(role));

        await outer.CommitAsync();
        Assert.True(await IsStoredAsync(role));
    }

    [Fact]
    public async Task A_nested_call_does_not_commit_before_the_outer_one_returns()
    {
        var role = NewRole();

        await _unitOfWork.ExecuteAsync(async ct =>
        {
            await _unitOfWork.ExecuteAsync(inner => AddAsync(role, "inner", inner), TransactionMode.ReadCommitted, ct);

            Assert.NotNull(_db.Database.CurrentTransaction);
            Assert.False(await IsStoredAsync(role));
        });

        Assert.True(await IsStoredAsync(role));
    }

    [Fact]
    public async Task A_failure_in_a_nested_call_rolls_back_what_the_outer_one_wrote()
    {
        var role = NewRole();

        await Assert.ThrowsAsync<ReferenceConflictException>(() => _unitOfWork.ExecuteAsync(async ct =>
        {
            await AddAsync(role, "outer", ct);
            await _unitOfWork.ExecuteAsync(async inner =>
            {
                _db.Users.Add(NewUser(roleId: Guid.CreateVersion7()));
                await _unitOfWork.SaveChangesAsync(inner);
            }, TransactionMode.ReadCommitted, ct);
        }));

        Assert.False(await IsStoredAsync(role));
    }

    [Theory]
    [InlineData(TransactionMode.ReadCommitted, "read committed")]
    [InlineData(TransactionMode.RepeatableRead, "repeatable read")]
    public async Task The_transaction_is_opened_at_the_level_asked_for(TransactionMode mode, string expected)
    {
        var level = await _unitOfWork.ExecuteAsync(_ => CurrentIsolationAsync(), mode);

        Assert.Equal(expected, level);
    }

    [Fact]
    public async Task No_transaction_is_opened_for_mode_none()
    {
        var open = await _unitOfWork.ExecuteAsync(
            _ => Task.FromResult(_db.Database.CurrentTransaction is not null), TransactionMode.None);

        Assert.False(open);
    }

    [Fact]
    public async Task Asking_for_a_stronger_level_than_the_open_transaction_is_a_programming_error()
    {
        await using var outer = await _db.Database.BeginTransactionAsync(IsolationLevel.ReadCommitted);
        var ran = false;

        var error = await Assert.ThrowsAsync<InvalidOperationException>(() => _unitOfWork.ExecuteAsync(_ =>
        {
            ran = true;
            return Task.CompletedTask;
        }, TransactionMode.RepeatableRead));

        Assert.Contains("RepeatableRead", error.Message);
        Assert.False(ran);
    }

    [Fact]
    public async Task Asking_for_a_weaker_level_than_the_open_transaction_joins_it()
    {
        await using var outer = await _db.Database.BeginTransactionAsync(IsolationLevel.RepeatableRead);

        var level = await _unitOfWork.ExecuteAsync(_ => CurrentIsolationAsync(), TransactionMode.ReadCommitted);

        Assert.Equal("repeatable read", level);
    }

    [Fact]
    public async Task A_duplicate_becomes_a_unique_conflict_that_keeps_the_database_error()
    {
        var first = NewRole();
        await _unitOfWork.ExecuteAsync(ct => AddAsync(first, "first", ct));
        _db.Roles.Add(new Role { Name = first.Name, IsBuiltIn = false });

        var conflict = await Assert.ThrowsAsync<UniqueConflictException>(() => _unitOfWork.SaveChangesAsync());

        // No module has registered an index yet, so every duplicate is Unknown.
        Assert.Equal(UniqueConflict.Unknown, conflict.Conflict);
        var postgres = Assert.IsType<PostgresException>(Assert.IsType<DbUpdateException>(conflict.InnerException).InnerException);
        Assert.Equal("ix_roles_name", postgres.ConstraintName);

        // The same failure with that index in the table names the rule.
        var named = PersistenceExceptionTranslator.Translate(conflict.InnerException!,
            new Dictionary<string, UniqueConflict> { ["ix_roles_name"] = (UniqueConflict)1 });
        Assert.Equal((UniqueConflict)1, Assert.IsType<UniqueConflictException>(named).Conflict);
    }

    [Fact]
    public async Task A_missing_parent_row_becomes_a_reference_conflict()
    {
        _db.Users.Add(NewUser(roleId: Guid.CreateVersion7()));

        var conflict = await Assert.ThrowsAsync<ReferenceConflictException>(() => _unitOfWork.SaveChangesAsync());

        Assert.IsType<DbUpdateException>(conflict.InnerException);
    }

    [Fact]
    public async Task A_failure_of_a_save_made_on_the_context_directly_is_translated_when_it_leaves_the_work()
    {
        // What a repository not moved yet does: it saves on the context, not through the unit of work.
        _db.Users.Add(NewUser(roleId: Guid.CreateVersion7()));

        await Assert.ThrowsAsync<ReferenceConflictException>(() => _unitOfWork.ExecuteAsync(
            ct => _db.SaveChangesAsync(ct), TransactionMode.None));
    }

    [Fact]
    public async Task A_row_deleted_by_someone_else_becomes_a_concurrency_conflict()
    {
        var role = NewRole();
        await _unitOfWork.ExecuteAsync(ct => AddAsync(role, "stored", ct));
        await using (var other = NewContext())
        {
            await other.Roles.Where(r => r.Id == role.Id).ExecuteDeleteAsync();
        }

        role.Description = "changed after the row was gone";

        await Assert.ThrowsAsync<ConcurrencyConflictException>(() => _unitOfWork.SaveChangesAsync());
    }

    [Fact]
    public async Task An_unreachable_store_becomes_persistence_unavailable()
    {
        // Nothing listens on port 1. Without a retrying strategy EF Core reports the failed connection either bare
        // or wrapped in an InvalidOperationException; both must come out the same.
        await using var db = new TrustCabinetDbContext(new DbContextOptionsBuilder<TrustCabinetDbContext>()
            .UseNpgsql("Host=127.0.0.1;Port=1;Database=none;Username=none;Password=none;Timeout=3")
            .UseSnakeCaseNamingConvention()
            .Options);
        var unitOfWork = new EfUnitOfWork(db);

        await Assert.ThrowsAsync<PersistenceUnavailableException>(() => unitOfWork.ExecuteAsync(
            ct => db.Roles.AnyAsync(ct), TransactionMode.None));
        await Assert.ThrowsAsync<PersistenceUnavailableException>(() => unitOfWork.ExecuteAsync(
            ct => db.Roles.AnyAsync(ct)));
    }

    [Fact]
    public async Task A_cancellation_is_not_translated()
    {
        using var cancelled = new CancellationTokenSource();
        await cancelled.CancelAsync();

        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => _unitOfWork.ExecuteAsync(
            ct => _db.Roles.AnyAsync(ct), TransactionMode.None, cancelled.Token));
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => _unitOfWork.ExecuteAsync(
            ct => _db.Roles.AnyAsync(ct), TransactionMode.ReadCommitted, cancelled.Token));
    }

    [Fact]
    public async Task DiscardChanges_forgets_what_was_loaded_and_not_saved()
    {
        _db.Roles.Add(NewRole());

        _unitOfWork.DiscardChanges();
        await _unitOfWork.SaveChangesAsync();

        Assert.Empty(_db.ChangeTracker.Entries());
        Assert.False(await _db.Roles.AnyAsync(r => !r.IsBuiltIn));
    }

    [Fact]
    public async Task The_in_memory_provider_of_the_older_tests_runs_the_work_without_a_transaction()
    {
        await using var db = new TrustCabinetDbContext(new DbContextOptionsBuilder<TrustCabinetDbContext>()
            .UseInMemoryDatabase($"unit-of-work-{Guid.CreateVersion7()}")
            .Options);
        var unitOfWork = new EfUnitOfWork(db);
        var role = NewRole();

        await unitOfWork.ExecuteAsync(async ct =>
        {
            db.Roles.Add(role);
            await unitOfWork.SaveChangesAsync(ct);
        }, TransactionMode.RepeatableRead);

        Assert.True(await db.Roles.AnyAsync(r => r.Id == role.Id));
    }

    private async Task<string> AddAsync(Role role, string outcome, CancellationToken ct)
    {
        _db.Roles.Add(role);
        await _unitOfWork.SaveChangesAsync(ct);
        return outcome;
    }

    /// <summary>Asked on another connection, so it answers with what is committed.</summary>
    private async Task<bool> IsStoredAsync(Role role)
    {
        await using var other = NewContext();
        return await other.Roles.AnyAsync(r => r.Id == role.Id);
    }

    private Task<string> CurrentIsolationAsync()
        => _db.Database.SqlQueryRaw<string>("SELECT current_setting('transaction_isolation') AS \"Value\"").SingleAsync();

    private TrustCabinetDbContext NewContext() => new(
        new DbContextOptionsBuilder<TrustCabinetDbContext>()
            .UseNpgsql(_database.DataSource)
            .UseSnakeCaseNamingConvention()
            .Options);

    private static Role NewRole() => new()
    {
        Name = $"Unit of work test {Guid.CreateVersion7():N}",
        IsBuiltIn = false,
    };

    private static User NewUser(Guid roleId) => new()
    {
        FullName = "Unit of work test",
        Email = $"{Guid.CreateVersion7():N}@trustcabinet.local",
        PasswordHash = "test-hash",
        RoleId = roleId,
    };
}
