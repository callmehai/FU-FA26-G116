using TrustCabinet.Domain.Audit;
using TrustCabinet.Domain.Entities;
using TrustCabinet.Domain.VoiceId;

namespace TrustCabinet.Infrastructure.VoiceId;

/// <summary>
/// SCRUM-35 — the one way a person's Voice ID ends, whoever ends it. Takes the consent row for update, the lock a new
/// sample and a running transcription's matching also take, so nothing about the person is stored after it.
///
/// ADR-0060: a person holds a consent per set of samples. Ending one set erases its samples only; the machine's
/// suggestions and the names it gave go either way, as they may have come from the set that ended — the next
/// transcription suggests again from whatever set remains.
/// </summary>
public sealed class VoiceIdErasure(IVoiceIdRepository repository, IAuditService audit, TimeProvider clock) : IVoiceIdErasure
{
    public async Task<bool> EndAsync(Guid userId, Guid actorId, VoiceIdEndReason reason, CancellationToken ct,
        VoiceConsentScope? scope = null)
    {
        var ended = false;
        foreach (var set in scope is { } one ? [one] : Enum.GetValues<VoiceConsentScope>())
        {
            var consent = await repository.FindActiveConsentAsync(userId, true, ct, set);
            if (consent is null) continue;
            var now = clock.GetUtcNow();
            var erased = await repository.DeleteSamplesAsync(consent.Id, ct);
            var suggestions = await repository.ClearSuggestionsAsync(userId, now, ct);
            consent.WithdrawnAt = now;
            consent.UpdatedAt = now;
            await audit.RecordAsync(AuditActions.VoiceConsentWithdrawn, AuditActions.VoiceConsentObjectType, consent.Id, actorId,
                new
                {
                    reason = reason == VoiceIdEndReason.AccountDeactivated ? "accountDeactivated" : "withdrawn",
                    scope = set == VoiceConsentScope.Proxy ? "proxy" : "self",
                    userId, erasedSamples = erased, clearedSuggestions = suggestions,
                }, ct);
            await repository.SaveChangesAsync(ct);
            ended = true;
        }
        return ended;
    }
}
