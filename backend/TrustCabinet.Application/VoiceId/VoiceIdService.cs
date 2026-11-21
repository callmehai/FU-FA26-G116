using System.Security.Cryptography;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using TrustCabinet.Application.Abstractions;
using TrustCabinet.Application.Audit;
using TrustCabinet.Application.Transcription;
using TrustCabinet.Domain.Audit;
using TrustCabinet.Domain.Entities;
using TrustCabinet.Domain.VoiceId;

namespace TrustCabinet.Application.VoiceId;

/// <summary>
/// SCRUM-35 — a person's own Voice ID. Nothing here acts on anyone but the caller: no one can enrol, keep or withdraw
/// someone else's voice. One consent covers the samples the person records and the ones Secretaries record for them
/// (ADR-0060). Withdrawal erases every sample and every machine suggestion pointing at the person, in
/// one transaction, under the same row lock a new sample takes.
/// </summary>
public sealed class VoiceIdService(IVoiceIdRepository repository, IUnitOfWork unitOfWork, IVoiceIdErasure erasure, IVoiceEmbedder embedder,
    IVoiceprintProtector protector, IAuditService audit, TimeProvider clock, IOptions<VoiceIdOptions> options, ILogger<VoiceIdService> logger) : IVoiceIdService
{
    public async Task<VoiceOutcome> GetAsync(Guid actorId, CancellationToken ct = default)
    {
        try
        {
            return new(Data: await ProfileAsync(actorId, ct));
        }
        catch (PersistenceException ex)
        {
            logger.LogError(ex.InnerException ?? ex, "Failed to read Voice ID of {UserId}", actorId);
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
            await unitOfWork.ExecuteAsync(async token =>
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
                    await unitOfWork.SaveChangesAsync(token);
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
                await unitOfWork.SaveChangesAsync(token);
                return 0;
            }, TransactionMode.ReadCommitted, ct);
            return new(Data: await ProfileAsync(actorId, ct));
        }
        catch (UniqueConflictException)
        {
            // Two agreements at once (a double click): neither finds a row to lock, both insert, the unique index keeps
            // one. The loser's answer is the same as the winner's — the person has agreed.
            return new(Data: await ProfileAsync(actorId, ct));
        }
        catch (PersistenceException ex)
        {
            logger.LogError(ex.InnerException ?? ex, "Failed to record Voice ID consent of {UserId}", actorId);
            return new(VoiceFailure.SaveFailed);
        }
    }

    public async Task<VoiceOutcome> WithdrawAsync(Guid actorId, CancellationToken ct = default)
    {
        try
        {
            await unitOfWork.ExecuteAsync(
                token => erasure.EndAsync(actorId, actorId, VoiceIdEndReason.Withdrawn, token), TransactionMode.ReadCommitted, ct);
            return new(Data: await ProfileAsync(actorId, ct));
        }
        catch (PersistenceException ex)
        {
            logger.LogError(ex.InnerException ?? ex, "Failed to withdraw Voice ID of {UserId}", actorId);
            return new(VoiceFailure.SaveFailed);
        }
    }

    /// <summary>The samples the person recorded themself; the ones Secretaries took count against their own limit.</summary>
    private static int OwnCount(IEnumerable<VoiceSample> samples) => samples.Count(s => s.CollectedById is null);

    public async Task<VoiceOutcome> AddSampleAsync(Guid actorId, Stream audio, string contentType, CancellationToken ct = default)
    {
        try
        {
            // Checked before the audio goes to the worker, then again under the lock before anything is kept.
            if (await ReadAsync(token => repository.FindActiveConsentAsync(actorId, false, token), ct) is null)
                return new(VoiceFailure.NoConsent);
            if (OwnCount(await ReadAsync(token => repository.ListSamplesAsync(actorId, token), ct)) >= VoiceIdRules.MaxSamples)
                return new(VoiceFailure.LimitReached);
        }
        catch (PersistenceException ex)
        {
            logger.LogError(ex.InnerException ?? ex, "Failed to read Voice ID of {UserId}", actorId);
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
            return await unitOfWork.ExecuteAsync(async token =>
            {
                var consent = await repository.FindActiveConsentAsync(actorId, true, token);
                if (consent is null) return new VoiceOutcome(VoiceFailure.NoConsent);
                if (OwnCount(await repository.ListSamplesAsync(actorId, token)) >= VoiceIdRules.MaxSamples)
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
                await unitOfWork.SaveChangesAsync(token);
                return new VoiceOutcome(Data: new VoiceSampleView(id, now, embedding.SpeechMs, sample.AudioCiphertext is not null));
            }, TransactionMode.ReadCommitted, ct);
        }
        catch (PersistenceException ex)
        {
            logger.LogError(ex.InnerException ?? ex, "Failed to store a voice sample of {UserId}", actorId);
            return new(VoiceFailure.SaveFailed);
        }
    }

    public async Task<VoiceOutcome> DeleteSampleAsync(Guid actorId, Guid sampleId, CancellationToken ct = default)
    {
        try
        {
            var sample = (await ReadAsync(token => repository.ListSamplesAsync(actorId, token), ct)).SingleOrDefault(s => s.Id == sampleId);
            // Someone else's sample is answered exactly like one that does not exist.
            if (sample is null) return new(VoiceFailure.NotFound);
            // Either set: the samples a Secretary took are the person's too, and theirs to delete (ADR-0060).
            repository.Remove(sample);
            await audit.RecordAsync(AuditActions.VoiceSampleDeleted, AuditActions.VoiceSampleObjectType, sampleId, actorId,
                sample.CollectedById is null ? null : new { scope = "proxy", sample.MeetingId, sample.CollectedById }, ct);
            await unitOfWork.SaveChangesAsync(ct);
            return new(Data: await ProfileAsync(actorId, ct));
        }
        catch (PersistenceException ex)
        {
            logger.LogError(ex.InnerException ?? ex, "Failed to delete voice sample {SampleId}", sampleId);
            return new(VoiceFailure.SaveFailed);
        }
    }

    public async Task<VoiceSampleAudio?> SampleAudioAsync(Guid actorId, Guid sampleId, CancellationToken ct = default)
    {
        // Only the owner's own list is read: someone else's sample is answered exactly like one that does not exist.
        var sample = (await ReadAsync(token => repository.ListSamplesAsync(actorId, token), ct)).SingleOrDefault(s => s.Id == sampleId);
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

    private Task<VoiceProfile> ProfileAsync(Guid userId, CancellationToken ct) => ReadAsync(async token =>
    {
        var consent = await repository.FindActiveConsentAsync(userId, false, token);
        var samples = await repository.ListSamplesAsync(userId, token);
        var proxySamples = await repository.ListProxySamplesAsync(userId, token);
        return new VoiceProfile(View(consent),
            [.. samples.Where(s => s.CollectedById is null)
                .Select(s => new VoiceSampleView(s.Id, s.CreatedAt, s.SpeechMs, s.AudioCiphertext is not null))],
            VoiceIdRules.MaxSamples,
            VoiceIdRules.NoticeVersion, VoiceIdRules.Purpose, options.Value.Controller,
            new(consent is not null && VoiceIdRules.AllowsCollection(consent.NoticeVersion),
                [.. proxySamples.Select(s => new ProxySampleView(s.Id, s.CreatedAt, s.SpeechMs, s.AudioCiphertext is not null,
                    s.MeetingId!.Value, s.Meeting!.Title, s.CollectedBy!.FullName))],
                VoiceIdRules.MaxProxySamples));
    }, ct);

    /// <summary>Storage calls outside a transaction: run as they are, only their failures translated.</summary>
    private Task<T> ReadAsync<T>(Func<CancellationToken, Task<T>> work, CancellationToken ct)
        => unitOfWork.ExecuteAsync(work, TransactionMode.None, ct);

    private static VoiceConsentView? View(VoiceConsent? consent)
        => consent is null ? null : new(consent.GrantedAt, consent.NoticeVersion, consent.Purpose);
}
