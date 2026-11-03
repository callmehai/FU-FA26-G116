using TrustCabinet.Domain.Audit;
using TrustCabinet.Domain.VoiceId;

namespace TrustCabinet.Infrastructure.VoiceId;

/// <summary>
/// SCRUM-35 — the one way a person's Voice ID ends, whoever ends it. Takes the consent row for update, the lock a new
/// sample and a running transcription's matching also take, so nothing about the person is stored after it.
/// </summary>
public sealed class VoiceIdErasure(IVoiceIdRepository repository, IAuditService audit, TimeProvider clock) : IVoiceIdErasure
{
    public async Task<bool> EndAsync(Guid userId, Guid actorId, VoiceIdEndReason reason, CancellationToken ct)
    {
        var consent = await repository.FindActiveConsentAsync(userId, true, ct);
        if (consent is null) return false;
        var now = clock.GetUtcNow();
        var erased = await repository.DeleteSamplesAsync(userId, ct);
        var suggestions = await repository.ClearSuggestionsAsync(userId, ct);
        consent.WithdrawnAt = now;
        consent.UpdatedAt = now;
        await audit.RecordAsync(AuditActions.VoiceConsentWithdrawn, AuditActions.VoiceConsentObjectType, consent.Id, actorId,
            new
            {
                reason = reason == VoiceIdEndReason.AccountDeactivated ? "accountDeactivated" : "withdrawn",
                userId, erasedSamples = erased, clearedSuggestions = suggestions,
            }, ct);
        await repository.SaveChangesAsync(ct);
        return true;
    }
}
