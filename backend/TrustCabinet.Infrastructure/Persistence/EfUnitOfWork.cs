using System.Data;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;
using TrustCabinet.Application.Abstractions;

namespace TrustCabinet.Infrastructure.Persistence;

/// <summary>
/// <see cref="IUnitOfWork"/> over the request's <see cref="TrustCabinetDbContext"/>. Every repository of the
/// request shares that context, so they all write in the transaction opened here, and a service not moved to
/// Application yet that opened its own with <c>BeginTransactionAsync</c> is joined rather than nested.
/// </summary>
internal sealed class EfUnitOfWork(TrustCabinetDbContext db) : IUnitOfWork
{
    public Task<T> ExecuteAsync<T>(Func<CancellationToken, Task<T>> work,
        TransactionMode mode = TransactionMode.ReadCommitted, CancellationToken ct = default)
        => ExecuteAsync(work, static _ => true, mode, ct);

    public async Task<T> ExecuteAsync<T>(Func<CancellationToken, Task<T>> work, Func<T, bool> commitWhen,
        TransactionMode mode = TransactionMode.ReadCommitted, CancellationToken ct = default)
    {
        try
        {
            // The in-memory provider of the tests has no transactions and refuses to begin one.
            if (mode == TransactionMode.None || !db.Database.IsRelational()) return await work(ct);

            var level = mode == TransactionMode.RepeatableRead ? IsolationLevel.RepeatableRead : IsolationLevel.ReadCommitted;
            if (db.Database.CurrentTransaction is { } open)
            {
                var openLevel = open.GetDbTransaction().IsolationLevel;
                if (Strength(level) > Strength(openLevel))
                {
                    throw new InvalidOperationException(
                        $"This work needs a {level} transaction, but it was called inside one opened as {openLevel}. "
                        + "Open the outer transaction at the stronger level.");
                }

                return await work(ct);
            }

            return await db.Database.CreateExecutionStrategy().ExecuteAsync(async () =>
            {
                // Disposing a transaction that was not committed rolls it back: that is the path of an exception.
                await using var transaction = await db.Database.BeginTransactionAsync(level, ct);
                var result = await work(ct);
                if (commitWhen(result)) await transaction.CommitAsync(ct);
                else await transaction.RollbackAsync(ct);
                return result;
            });
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
            await db.SaveChangesAsync(ct);
        }
        catch (Exception exception) when (PersistenceExceptionTranslator.Translate(exception) is { } translated)
        {
            throw translated;
        }
    }

    public void DiscardChanges() => db.ChangeTracker.Clear();

    private static int Strength(IsolationLevel level) => level switch
    {
        IsolationLevel.Serializable => 3,
        IsolationLevel.RepeatableRead or IsolationLevel.Snapshot => 2,
        _ => 1,
    };
}
