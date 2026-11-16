using TrustCabinet.Application.Abstractions;

namespace TrustCabinet.Application.Tests.Fakes;

/// <summary>
/// An <see cref="IUnitOfWork"/> with no store behind it: it runs the work at once and counts what the real
/// one would have done, so a use case test can assert "committed once" or "rolled back and nothing saved".
/// As with the real one, only the outermost transactional call commits or rolls back; a call made inside it
/// joins and is counted in <see cref="Modes"/> alone, and asking there for a stronger level than the outer
/// one throws.
/// </summary>
public sealed class FakeUnitOfWork : IUnitOfWork
{
    private TransactionMode? _open;

    public int Commits { get; private set; }
    public int Rollbacks { get; private set; }
    public int Saves { get; private set; }
    public int Discards { get; private set; }

    /// <summary>Whether a transactional call is running now, as the real one has a transaction open.</summary>
    public bool InTransaction => _open is not null;

    /// <summary>The mode of every <c>ExecuteAsync</c> call, in the order they were made.</summary>
    public List<TransactionMode> Modes { get; } = [];

    /// <summary>
    /// Thrown by the next <see cref="SaveChangesAsync"/>, once, in place of saving: set it to a
    /// <see cref="PersistenceException"/> to see which outcome the use case answers with.
    /// </summary>
    public Exception? FailNextSave { get; set; }

    public Task<T> ExecuteAsync<T>(Func<CancellationToken, Task<T>> work,
        TransactionMode mode = TransactionMode.ReadCommitted, CancellationToken ct = default)
        => ExecuteAsync(work, static _ => true, mode, ct);

    public async Task<T> ExecuteAsync<T>(Func<CancellationToken, Task<T>> work, Func<T, bool> commitWhen,
        TransactionMode mode = TransactionMode.ReadCommitted, CancellationToken ct = default)
    {
        Modes.Add(mode);
        if (mode == TransactionMode.None) return await work(ct);
        if (_open is { } open)
        {
            if (mode > open)
                throw new InvalidOperationException($"This work needs a {mode} transaction, but it was called inside one opened as {open}.");
            return await work(ct);
        }

        _open = mode;
        try
        {
            var result = await work(ct);
            if (commitWhen(result)) Commits++;
            else Rollbacks++;
            return result;
        }
        catch
        {
            Rollbacks++;
            throw;
        }
        finally
        {
            _open = null;
        }
    }

    public Task ExecuteAsync(Func<CancellationToken, Task> work,
        TransactionMode mode = TransactionMode.ReadCommitted, CancellationToken ct = default)
        => ExecuteAsync(async token =>
        {
            await work(token);
            return true;
        }, mode, ct);

    public Task SaveChangesAsync(CancellationToken ct = default)
    {
        if (FailNextSave is { } failure)
        {
            FailNextSave = null;
            throw failure;
        }

        Saves++;
        return Task.CompletedTask;
    }

    public void DiscardChanges() => Discards++;
}
