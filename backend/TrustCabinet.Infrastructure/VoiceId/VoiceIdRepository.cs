using System.Data;
using Microsoft.EntityFrameworkCore;
using TrustCabinet.Domain.Entities;
using TrustCabinet.Domain.VoiceId;

namespace TrustCabinet.Infrastructure.VoiceId;

public sealed class VoiceIdRepository(TrustCabinetDbContext db) : IVoiceIdRepository
{
    public async Task<VoiceConsent?> FindActiveConsentAsync(Guid userId, bool forUpdate, CancellationToken ct)
    {
        // Withdrawal and adding a sample lock the same row, so no sample is ever stored after a withdrawal.
        if (forUpdate)
            await db.VoiceConsents.FromSqlInterpolated(
                $"SELECT * FROM voice_consents WHERE user_id = {userId} AND withdrawn_at IS NULL FOR UPDATE")
                .AsNoTracking().ToListAsync(ct);
        return await db.VoiceConsents.SingleOrDefaultAsync(c => c.UserId == userId && c.WithdrawnAt == null, ct);
    }

    public async Task<IReadOnlyList<VoiceSample>> ListSamplesAsync(Guid userId, CancellationToken ct)
        => await db.VoiceSamples.Where(s => s.UserId == userId).OrderBy(s => s.CreatedAt).ThenBy(s => s.Id).ToListAsync(ct);

    public async Task<IReadOnlyList<VoiceSample>> ListProxySamplesAsync(Guid userId, CancellationToken ct)
        => await db.VoiceSamples.AsNoTracking().Include(s => s.Meeting).Include(s => s.CollectedBy)
            .Where(s => s.UserId == userId && s.CollectedById != null)
            .OrderBy(s => s.CreatedAt).ThenBy(s => s.Id).ToListAsync(ct);

    public async Task<IReadOnlyList<VoiceSample>> ListMatchableSamplesAsync(IReadOnlyCollection<Guid> userIds,
        string embeddingModel, CancellationToken ct)
        => await db.VoiceSamples.AsNoTracking().Include(s => s.Consent)
            .Where(s => userIds.Contains(s.UserId) && s.EmbeddingModel == embeddingModel && s.Consent.WithdrawnAt == null
                && s.User.IsActive)
            .ToListAsync(ct);

    public async Task LockConsentsAsync(IReadOnlyCollection<Guid> userIds, CancellationToken ct)
    {
        if (userIds.Count == 0) return;
        Guid[] ids = [.. userIds];
        // FOR SHARE: transcriptions do not wait on each other, only on a withdrawal's FOR UPDATE of the same row.
        await db.Database.SqlQuery<Guid>(
            $"SELECT id AS \"Value\" FROM voice_consents WHERE user_id = ANY({ids}) AND withdrawn_at IS NULL FOR SHARE").ToListAsync(ct);
    }

    public Task<int> DeleteSamplesAsync(Guid consentId, CancellationToken ct)
        => db.VoiceSamples.Where(s => s.ConsentId == consentId).ExecuteDeleteAsync(ct);

    public async Task<int> ClearSuggestionsAsync(Guid userId, DateTimeOffset now, CancellationToken ct)
    {
        // A name the machine gave from the voice goes with the consent; one a Secretary gave is their decision and stays.
        // UpdatedAt moves, so a minutes draft that named them by their voice reads as behind (readiness.transcriptChangedAt).
        // A suggestion alone is not: no draft reads it, so clearing it leaves UpdatedAt and the draft as they were.
        await db.TranscriptSpeakers.Where(s => s.SuggestedUserId == userId && s.AssignedUserId == userId && s.AssignedById == null)
            .ExecuteUpdateAsync(u => u.SetProperty(s => s.AssignedUserId, (Guid?)null).SetProperty(s => s.AssignedAt, (DateTimeOffset?)null)
                .SetProperty(s => s.UpdatedAt, now), ct);
        return await db.TranscriptSpeakers.Where(s => s.SuggestedUserId == userId)
            .ExecuteUpdateAsync(u => u.SetProperty(s => s.SuggestedUserId, (Guid?)null).SetProperty(s => s.SuggestionScore, (double?)null), ct);
    }

    public void Add(VoiceConsent consent) => db.VoiceConsents.Add(consent);
    public void Add(VoiceSample sample) => db.VoiceSamples.Add(sample);
    public void Remove(VoiceSample sample) => db.VoiceSamples.Remove(sample);
    public Task SaveChangesAsync(CancellationToken ct) => db.SaveChangesAsync(ct);

    public async Task<T> InTransactionAsync<T>(Func<CancellationToken, Task<T>> operation, CancellationToken ct)
    {
        await using var transaction = await db.Database.BeginTransactionAsync(IsolationLevel.ReadCommitted, ct);
        var result = await operation(ct);
        await transaction.CommitAsync(ct);
        return result;
    }
}
