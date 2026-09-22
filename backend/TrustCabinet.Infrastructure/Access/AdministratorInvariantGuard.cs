using Microsoft.EntityFrameworkCore;
using TrustCabinet.Domain.Access;

namespace TrustCabinet.Infrastructure.Access;

/// <summary>
/// The shared lock described by <see cref="IAdministratorInvariantGuard"/>, as a PostgreSQL
/// transaction-scoped advisory lock.
/// </summary>
public sealed class AdministratorInvariantGuard(TrustCabinetDbContext db)
    : IAdministratorInvariantGuard
{
    /// <summary>
    /// The label both sides queue behind. Advisory locks are taken on a number, so the name is
    /// hashed — the string itself is never compared, it only has to be spelled the same way in
    /// one place, which is why it is a constant and not repeated at each call site.
    /// </summary>
    private const string LockName = "admin-users-invariant";

    public async Task<T> WriteAsync<T>(
        Func<CancellationToken, Task<T>> write, CancellationToken ct = default)
    {
        // An ambient transaction means an outer guard is already holding the lock: advisory locks
        // are re-entrant within a session, so taking it again would succeed and prove nothing,
        // while committing here would end the outer transaction early. Run inside the existing one
        // instead — the outer WriteAsync commits.
        if (db.Database.CurrentTransaction is not null)
        {
            return await write(ct);
        }

        // Serializable for the same reason RolePermissionRepository.InTransactionAsync uses it:
        // the reads the decision rests on must not change underneath. The advisory lock below is
        // what actually serialises the two sides, so the second caller waits rather than losing a
        // serialization failure and having to retry.
        await using var transaction = await db.Database.BeginTransactionAsync(
            System.Data.IsolationLevel.Serializable, ct);

        // pg_advisory_xact_lock, not pg_advisory_lock: the _xact_ form is released when the
        // transaction ends, however it ends. The session-scoped form would survive a rollback and
        // leak with the pooled connection, and the next request to borrow it would hold a lock
        // nobody takes responsibility for releasing.
        //
        // hashtext() turns the agreed label into the bigint the lock is actually taken on. It is
        // not stable across major PostgreSQL versions, which does not matter here: what matters is
        // that both call sites hash the same string on the same server at the same time.
        // Parameterised rather than interpolated into the string. LockName is a constant, so there
        // is nothing to inject today — but a literal in SQL is the kind of thing that later grows
        // an argument, and ExecuteSqlRawAsync would concatenate it without complaint.
        await db.Database.ExecuteSqlRawAsync(
            "SELECT pg_advisory_xact_lock(hashtext({0}))", [LockName], ct);

        var result = await write(ct);

        await transaction.CommitAsync(ct);

        return result;
    }
}
