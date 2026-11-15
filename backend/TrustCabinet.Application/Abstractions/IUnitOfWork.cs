namespace TrustCabinet.Application.Abstractions;

/// <summary>The transaction a unit of work runs in.</summary>
public enum TransactionMode
{
    /// <summary>No transaction: the work runs as it is, and only storage failures are translated.</summary>
    None,

    /// <summary>Read committed, the storage default.</summary>
    ReadCommitted,

    /// <summary>Repeatable read: every statement of the work sees the same snapshot.</summary>
    RepeatableRead,
}

/// <summary>
/// The one port a use case reaches storage transactions through, in place of <c>DbContext</c>,
/// <c>BeginTransactionAsync</c> and the <c>InTransactionAsync</c>/<c>SaveChangesAsync</c> of each repository.
///
/// It is also the only place a storage failure is translated: whatever leaves <c>ExecuteAsync</c> or
/// <see cref="SaveChangesAsync"/> because the store rejected or could not take the work arrives as a
/// <see cref="PersistenceException"/>. So a use case calls its repositories and query ports inside the delegate
/// (a plain read with <see cref="TransactionMode.None"/>) and catches outside it.
/// <see cref="OperationCanceledException"/> and every other exception pass through unchanged.
///
/// With <see cref="TransactionMode.ReadCommitted"/> or <see cref="TransactionMode.RepeatableRead"/>:
/// <list type="bullet">
/// <item>no transaction is open: one is opened at that level, committed when the work returns and rolled back
/// when it throws;</item>
/// <item>a transaction is already open, begun by an outer call or by code not moved yet: the work runs inside
/// it and nothing is committed here, the owner of that transaction decides. Asking for a stronger level than
/// the open one is a programming error and throws <see cref="InvalidOperationException"/>.</item>
/// </list>
///
/// <c>ExecuteAsync</c> never saves by itself: the use case calls <see cref="SaveChangesAsync"/> where it means
/// to. What follows a commit (SignalR, email, deleting a file) belongs after <c>ExecuteAsync</c> has returned.
/// </summary>
public interface IUnitOfWork
{
    /// <summary>Runs <paramref name="work"/> under <paramref name="mode"/> and returns its result.</summary>
    Task<T> ExecuteAsync<T>(Func<CancellationToken, Task<T>> work,
        TransactionMode mode = TransactionMode.ReadCommitted, CancellationToken ct = default);

    /// <summary>
    /// As the overload without <paramref name="commitWhen"/>, but commits only when it returns <c>true</c> for
    /// the result; otherwise the transaction is rolled back and the result is still returned. For a block that
    /// leaves on purpose without committing after it has written. Use it on the outermost call only: inside a
    /// transaction that is already open nothing is committed or rolled back here, and
    /// <paramref name="commitWhen"/> is not consulted.
    /// </summary>
    Task<T> ExecuteAsync<T>(Func<CancellationToken, Task<T>> work, Func<T, bool> commitWhen,
        TransactionMode mode = TransactionMode.ReadCommitted, CancellationToken ct = default);

    /// <summary>Runs <paramref name="work"/> under <paramref name="mode"/>.</summary>
    Task ExecuteAsync(Func<CancellationToken, Task> work,
        TransactionMode mode = TransactionMode.ReadCommitted, CancellationToken ct = default);

    /// <summary>Writes the changes made to the entities the repositories handed out.</summary>
    Task SaveChangesAsync(CancellationToken ct = default);

    /// <summary>
    /// Forgets every entity loaded and every change not saved yet, so a retry starts from what is stored.
    /// </summary>
    void DiscardChanges();
}
