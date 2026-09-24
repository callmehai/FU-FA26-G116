using Microsoft.EntityFrameworkCore;
using TrustCabinet.Domain.Entities;
using TrustCabinet.Domain.Meetings;

namespace TrustCabinet.Infrastructure.Meetings;

/// <summary>Queries and persists meetings; authorization and state rules stay in the service.</summary>
public sealed class MeetingRepository(TrustCabinetDbContext db) : IMeetingRepository
{
    public Task<Meeting?> FindOwnedAsync(
        Guid id, Guid creatorId, CancellationToken ct = default)
        => db.Meetings
            .AsNoTracking()
            .SingleOrDefaultAsync(m => m.Id == id && m.CreatedById == creatorId, ct);

    public async Task<Meeting?> FindOwnedForUpdateAsync(
        Guid id, Guid creatorId, CancellationToken ct = default)
    {
        // Issue the lock as a parameterized PostgreSQL query. Materialize it without composing
        // another query over SELECT ... FOR UPDATE, then load the tracked row under the same lock.
        var lockedRows = await db.Meetings
            .FromSqlInterpolated($"SELECT * FROM meetings WHERE id = {id} AND created_by_id = {creatorId} FOR UPDATE")
            .AsNoTracking()
            .ToListAsync(ct);

        if (lockedRows.Count == 0)
        {
            return null;
        }

        return await db.Meetings.SingleOrDefaultAsync(
            m => m.Id == id && m.CreatedById == creatorId, ct);
    }

    public Task AddAsync(Meeting meeting, CancellationToken ct = default)
    {
        ct.ThrowIfCancellationRequested();
        db.Meetings.Add(meeting);
        return Task.CompletedTask;
    }

    public Task SaveChangesAsync(CancellationToken ct = default) => db.SaveChangesAsync(ct);

    public async Task<T> InTransactionAsync<T>(
        Func<CancellationToken, Task<T>> operation, CancellationToken ct = default)
    {
        await using var transaction = await db.Database.BeginTransactionAsync(ct);
        var result = await operation(ct);
        await transaction.CommitAsync(ct);
        return result;
    }
}
