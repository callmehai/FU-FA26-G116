using System.Security.Cryptography;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Npgsql;
using TrustCabinet.Domain.Audit;
using TrustCabinet.Domain.Entities;
using TrustCabinet.Domain.Transcription;
using TrustCabinet.Domain.VoiceId;

namespace TrustCabinet.Infrastructure.VoiceId;

/// <summary>
/// SCRUM-35 — a person's own Voice ID. Nothing here acts on anyone but the caller: no one can enrol, keep or withdraw
/// someone else's voice. Withdrawal erases every sample and every machine suggestion pointing at the person, in
/// one transaction, under the same row lock a new sample takes.
/// </summary>
public sealed class VoiceIdService(IVoiceIdRepository repository, IVoiceEmbedder embedder, IVoiceprintProtector protector,
    IAuditService audit, TimeProvider clock, IOptions<VoiceIdOptions> options, ILogger<VoiceIdService> logger) : IVoiceIdService
{
    private static bool PersistenceError(Exception ex) => ex is DbUpdateException or NpgsqlException or TimeoutException;

    public async Task<VoiceOutcome> GetAsync(Guid actorId, CancellationToken ct = default)
    {
        try
        {
            return new(Data: await ProfileAsync(actorId, ct));
        }
        catch (Exception ex) when (PersistenceError(ex))
        {
            logger.LogError(ex, "Failed to read Voice ID of {UserId}", actorId);
            return new(VoiceFailure.ReadFailed);
        }
    }

    public async Task<VoiceOutcome> GrantAsync(Guid actorId, string? noticeVersion, CancellationToken ct = default)
    {
        if (noticeVersion != VoiceIdRules.NoticeVersion)
            return new(VoiceFailure.Invalid, Errors: new Dictionary<string, string[]>
            {
                ["noticeVersion"] = ["Nội dung thông báo đã thay đổi. Hãy tải lại trang và đọc lại trước khi đồng ý."],
            });
        try
        {
            await repository.InTransactionAsync(async token =>
            {
                var now = clock.GetUtcNow();
                var active = await repository.FindActiveConsentAsync(actorId, true, token);
                if (active is not null)
                {
                    // Agreeing to the newer notice renews the consent in place: its samples stay, and what changed is
                    // what may be kept from now on (ADR-0042). The audit row keeps both versions as evidence.
                    if (active.NoticeVersion == VoiceIdRules.NoticeVersion) return 0;
                    var from = active.NoticeVersion;
                    active.NoticeVersion = VoiceIdRules.NoticeVersion;
                    active.Purpose = VoiceIdRules.Purpose;
                    active.UpdatedAt = now;
                    await audit.RecordAsync(AuditActions.VoiceConsentRenewed, AuditActions.VoiceConsentObjectType, active.Id,
                        actorId, new { from, to = active.NoticeVersion, active.Purpose }, token);
                    await repository.SaveChangesAsync(token);
                    return 0;
                }
                var consent = new VoiceConsent
                {
                    UserId = actorId, GrantedAt = now, NoticeVersion = VoiceIdRules.NoticeVersion,
                    Purpose = VoiceIdRules.Purpose, CreatedAt = now, UpdatedAt = now,
                };
                repository.Add(consent);
                await audit.RecordAsync(AuditActions.VoiceConsentGranted, AuditActions.VoiceConsentObjectType, consent.Id, actorId,
                    new { consent.NoticeVersion, consent.Purpose }, token);
                await repository.SaveChangesAsync(token);
                return 0;
            }, ct);
            return new(Data: await ProfileAsync(actorId, ct));
        }
        catch (Exception ex) when (PersistenceError(ex))
        {
            logger.LogError(ex, "Failed to record Voice ID consent of {UserId}", actorId);
            return new(VoiceFailure.SaveFailed);
        }
    }

    public async Task<VoiceOutcome> WithdrawAsync(Guid actorId, CancellationToken ct = default)
    {
        try
        {
            await repository.InTransactionAsync(async token =>
            {
                var consent = await repository.FindActiveConsentAsync(actorId, true, token);
                if (consent is null) return 0;
                var now = clock.GetUtcNow();
                var erased = await repository.DeleteSamplesAsync(actorId, token);
                var suggestions = await repository.ClearSuggestionsAsync(actorId, token);
                consent.WithdrawnAt = now;
                consent.UpdatedAt = now;
                await audit.RecordAsync(AuditActions.VoiceConsentWithdrawn, AuditActions.VoiceConsentObjectType, consent.Id, actorId,
                    new { erasedSamples = erased, clearedSuggestions = suggestions }, token);
                await repository.SaveChangesAsync(token);
                return 0;
            }, ct);
            return new(Data: await ProfileAsync(actorId, ct));
        }
        catch (Exception ex) when (PersistenceError(ex))
        {
            logger.LogError(ex, "Failed to withdraw Voice ID of {UserId}", actorId);
            return new(VoiceFailure.SaveFailed);
        }
    }

    public async Task<VoiceOutcome> AddSampleAsync(Guid actorId, Stream audio, string contentType, CancellationToken ct = default)
    {
        try
        {
            // Checked before the audio goes to the worker, then again under the lock before anything is kept.
            if (await repository.FindActiveConsentAsync(actorId, false, ct) is null) return new(VoiceFailure.NoConsent);
            if ((await repository.ListSamplesAsync(actorId, ct)).Count >= VoiceIdRules.MaxSamples) return new(VoiceFailure.LimitReached);
        }
        catch (Exception ex) when (PersistenceError(ex))
        {
            logger.LogError(ex, "Failed to read Voice ID of {UserId}", actorId);
            return new(VoiceFailure.SaveFailed);
        }

        // Kept in memory to be stored encrypted after the worker reads it: a sample is at most a minute.
        using var copy = new MemoryStream();
        await audio.CopyToAsync(copy, ct);
        var recorded = copy.ToArray();
        SpeechEmbedding embedding;
        try
        {
            copy.Position = 0;
            embedding = await embedder.EmbedAsync(copy, contentType, ct);
        }
        catch (VoiceSampleRejectedException rejected)
        {
            return new(VoiceFailure.Rejected, Reason: rejected.Reason, Errors: new Dictionary<string, string[]>
            {
                ["file"] = [VoiceIdRules.RejectionMessage(rejected.Reason)],
            });
        }
        catch (SpeechEngineException ex)
        {
            logger.LogWarning(ex, "Speech worker could not embed a sample of {UserId}", actorId);
            return new(VoiceFailure.EngineUnavailable);
        }

        try
        {
            return await repository.InTransactionAsync(async token =>
            {
                var consent = await repository.FindActiveConsentAsync(actorId, true, token);
                if (consent is null) return new VoiceOutcome(VoiceFailure.NoConsent);
                if ((await repository.ListSamplesAsync(actorId, token)).Count >= VoiceIdRules.MaxSamples)
                    return new(VoiceFailure.LimitReached);
                var now = clock.GetUtcNow();
                var id = Guid.CreateVersion7();
                var sample = new VoiceSample
                {
                    Id = id, UserId = actorId, ConsentId = consent.Id, EmbeddingModel = embedding.Model,
                    Ciphertext = protector.Protect(embedding.Embedding, actorId, id, embedding.Model),
                    SpeechMs = embedding.SpeechMs, CreatedAt = now, UpdatedAt = now,
                };
                // The recording is kept only under a consent whose notice said so (ADR-0042).
                if (string.CompareOrdinal(consent.NoticeVersion, VoiceIdRules.AudioNoticeVersion) >= 0)
                {
                    sample.AudioCiphertext = protector.ProtectAudio(recorded, actorId, id);
                    sample.AudioContentType = contentType;
                }
                repository.Add(sample);
                await audit.RecordAsync(AuditActions.VoiceSampleAdded, AuditActions.VoiceSampleObjectType, id, actorId,
                    new { embedding.Model, embedding.SpeechMs }, token);
                await repository.SaveChangesAsync(token);
                return new VoiceOutcome(Data: new VoiceSampleView(id, now, embedding.SpeechMs, sample.AudioCiphertext is not null));
            }, ct);
        }
        catch (Exception ex) when (PersistenceError(ex))
        {
            logger.LogError(ex, "Failed to store a voice sample of {UserId}", actorId);
            return new(VoiceFailure.SaveFailed);
        }
    }

    public async Task<VoiceOutcome> DeleteSampleAsync(Guid actorId, Guid sampleId, CancellationToken ct = default)
    {
        try
        {
            var sample = (await repository.ListSamplesAsync(actorId, ct)).SingleOrDefault(s => s.Id == sampleId);
            // Someone else's sample is answered exactly like one that does not exist.
            if (sample is null) return new(VoiceFailure.NotFound);
            repository.Remove(sample);
            await audit.RecordAsync(AuditActions.VoiceSampleDeleted, AuditActions.VoiceSampleObjectType, sampleId, actorId, null, ct);
            await repository.SaveChangesAsync(ct);
            return new(Data: await ProfileAsync(actorId, ct));
        }
        catch (Exception ex) when (PersistenceError(ex))
        {
            logger.LogError(ex, "Failed to delete voice sample {SampleId}", sampleId);
            return new(VoiceFailure.SaveFailed);
        }
    }

    public async Task<VoiceSampleAudio?> SampleAudioAsync(Guid actorId, Guid sampleId, CancellationToken ct = default)
    {
        // Only the owner's own list is read: someone else's sample is answered exactly like one that does not exist.
        var sample = (await repository.ListSamplesAsync(actorId, ct)).SingleOrDefault(s => s.Id == sampleId);
        if (sample?.AudioCiphertext is null || sample.AudioContentType is null) return null;
        try
        {
            return new(protector.UnprotectAudio(sample.AudioCiphertext, actorId, sample.Id), sample.AudioContentType);
        }
        catch (CryptographicException ex)
        {
            logger.LogWarning(ex, "Audio of voice sample {SampleId} could not be decrypted", sampleId);
            return null;
        }
    }

    private async Task<VoiceProfile> ProfileAsync(Guid userId, CancellationToken ct)
    {
        var consent = await repository.FindActiveConsentAsync(userId, false, ct);
        var samples = await repository.ListSamplesAsync(userId, ct);
        return new(consent is null ? null : new(consent.GrantedAt, consent.NoticeVersion, consent.Purpose),
            [.. samples.Select(s => new VoiceSampleView(s.Id, s.CreatedAt, s.SpeechMs, s.AudioCiphertext is not null))],
            VoiceIdRules.MaxSamples,
            VoiceIdRules.NoticeVersion, VoiceIdRules.Purpose, options.Value.Controller);
    }
}
