using TrustCabinet.Application.Abstractions;

namespace TrustCabinet.Application.Access;

/// <summary>
/// The shared lock described by <see cref="IAdministratorInvariantGuard"/>: a Read Committed transaction that
/// takes <see cref="IRolePermissionRepository.LockAdministratorInvariantAsync"/> before the write runs.
/// </summary>
public sealed class AdministratorInvariantGuard(IUnitOfWork unitOfWork, IRolePermissionRepository roles)
    : IAdministratorInvariantGuard
{
    public Task<T> WriteAsync<T>(
        Func<CancellationToken, Task<T>> write, CancellationToken ct = default)
        // An ambient transaction means an outer guard is already holding the lock: the unit of work then runs
        // this inside the existing one rather than committing it early — the outer WriteAsync commits. Taking
        // the lock again there is harmless, since advisory locks are re-entrant within a session.
        //
        // Read Committed, not Serializable, and the difference matters here.
        //
        // What makes the decision safe is the advisory lock taken first: only one of these runs at a
        // time, so the second one reads a matrix the first has already finished changing. The
        // isolation level is not what provides that.
        //
        // Serializable would actively make it worse. A transaction's snapshot is taken at its
        // first statement — before the lock is granted — so the caller that waits ends up holding
        // a snapshot from before the winner committed. Its own writes then collide with what it
        // could not see, and PostgreSQL aborts it with 40001 rather than letting it re-read. The
        // effect is that a perfectly ordinary second edit fails with a serialization error that
        // the caller can only answer by retrying, where here it should simply see the new state
        // and be validated against it.
        //
        // Under Read Committed each statement after the lock sees the committed result of the
        // winner, which is exactly what the validation needs to judge.
        => unitOfWork.ExecuteAsync(async token =>
        {
            await roles.LockAdministratorInvariantAsync(token);
            return await write(token);
        }, TransactionMode.ReadCommitted, ct);
}
