using TrustCabinet.Application.Abstractions;
using TrustCabinet.Infrastructure.Persistence;

namespace TrustCabinet.Web.Tests.Persistence;

/// <summary>
/// An <see cref="IUnitOfWork"/> for the service tests written over in-memory fake repositories: no transaction,
/// but it translates storage failures exactly as <see cref="EfUnitOfWork"/> does, and its
/// <see cref="SaveChangesAsync"/> runs the fake repository's own save. So a test that makes the fake's save
/// throw the database's exception (a <c>DbUpdateException</c> over a <c>PostgresException</c>) still reaches the
/// service the way a real refusal would.
/// </summary>
internal sealed class TranslatingUnitOfWork(Func<CancellationToken, Task>? save = null) : IUnitOfWork
{
    public Task<T> ExecuteAsync<T>(Func<CancellationToken, Task<T>> work,
        TransactionMode mode = TransactionMode.ReadCommitted, CancellationToken ct = default)
        => ExecuteAsync(work, static _ => true, mode, ct);

    public async Task<T> ExecuteAsync<T>(Func<CancellationToken, Task<T>> work, Func<T, bool> commitWhen,
        TransactionMode mode = TransactionMode.ReadCommitted, CancellationToken ct = default)
    {
        try
        {
            return await work(ct);
        }
        catch (Exception exception) when (PersistenceExceptionTranslator.Translate(exception) is { } translated)
        {
            throw translated;
        }
    }

    public Task ExecuteAsync(Func<CancellationToken, Task> work,
        TransactionMode mode = TransactionMode.ReadCommitted, CancellationToken ct = default)
        => ExecuteAsync(async token =>
        {
            await work(token);
            return true;
        }, mode, ct);

    public async Task SaveChangesAsync(CancellationToken ct = default)
    {
        try
        {
            if (save is not null) await save(ct);
        }
        catch (Exception exception) when (PersistenceExceptionTranslator.Translate(exception) is { } translated)
        {
            throw translated;
        }
    }

    public void DiscardChanges()
    {
    }
}
