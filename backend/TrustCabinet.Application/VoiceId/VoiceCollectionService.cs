using System.Security.Cryptography;
using Microsoft.Extensions.Logging;
using TrustCabinet.Application.Abstractions;
using TrustCabinet.Application.Audit;
using TrustCabinet.Application.Notifications;
using TrustCabinet.Application.Transcription;
using TrustCabinet.Application.Users;
using TrustCabinet.Domain.Audit;
using TrustCabinet.Domain.Entities;
using TrustCabinet.Domain.Enums;
using TrustCabinet.Domain.VoiceId;

namespace TrustCabinet.Application.VoiceId;

/// <summary>
/// ADR-0071, ADR-0075 — a holder of <see cref="Capability.VoiceCollector"/> ("Thu giọng mẫu hộ", by default only Quản trị
/// viên) records a person's Voice ID samples for them, at any time, with no meeting. A meeting's Secretary has no part in
/// it any more: the meeting path of ADR-0060 was removed with the system roles Chủ toạ and Thư ký (ADR-0075).
///
/// Three locks keep this to people who trust the collector with their voice:
/// <list type="bullet">
/// <item>the capability, granted by an administrator and re-read from the database on every call (the endpoint's gate);</item>
/// <item>the person's own standing consent, to notice 1.4 or later, which only they can give (ADR-0071, ADR-0073);</item>
/// <item>the person agreed to this very take: the collector confirms it with each upload (<c>personAgreed</c>), the audit
/// row keeps the confirmation, and the person's notice says so, with the way to delete the sample (ADR-0073).</item>
/// </list>
/// A sample is bound to the person and encrypted exactly as one they took themself; the collector sees, hears and deletes
/// every proxy sample of the person, each play is audited, and the person is told each time one is taken. Samples a
/// Secretary took in a meeting before ADR-0075 stay, listed with their meeting.
/// </summary>
public sealed class VoiceCollectionService(IVoiceIdRepository repository, INotificationRepository notifications,
    IUnitOfWork unitOfWork, IVoiceEmbedder embedder, IVoiceprintProtector protector, IAuditService audit, TimeProvider clock,
    ICallerAccountQuery callers, ILogger<VoiceCollectionService> logger)
    : IVoiceCollectionService
{
    private const string NotificationTargetType = "VoiceSample";

    /// <summary>
    /// Ends the note the person gets for each proxy sample: the collector said they agreed to this take, and the person is
    /// the one who can tell whether that is true. Deleting is one click away, on their own page.
    /// </summary>
    private const string AskedNote = "Người thu đã xác nhận bạn đồng ý cho lần thu này. Nếu bạn không đồng ý, hãy xoá mẫu ở trang "
        + "Giọng nói của tôi.";

    public async Task<VoiceOutcome> GetPersonAsync(Guid actorId, Guid userId, CancellationToken ct = default)
    {
        try
        {
            return await ReadAsync(async token =>
            {
                if (await PersonAsync(userId, token) is not { } person) return new VoiceOutcome(VoiceFailure.NotFound);
                var consent = await repository.FindActiveConsentAsync(userId, false, token);
                var consented = consent is not null && VoiceIdRules.AllowsProxyCollection(consent.NoticeVersion);
                var samples = await repository.ListProxySamplesAsync(userId, token);
                return new(Data: new PersonVoiceCollectionView(userId, person.FullName, consented,
                    consented && !VoiceIdRules.ProxyFull(samples), VoiceIdRules.MaxProxySamples,
                    [.. samples.Select(s => new ProxySampleView(s.Id, s.CreatedAt, s.SpeechMs, s.AudioCiphertext is not null,
                        s.MeetingId, s.Meeting?.Title, s.CollectedBy?.FullName ?? ""))]));
            }, ct);
        }
        catch (PersistenceException ex)
        {
            logger.LogError(ex.InnerException ?? ex, "Failed to read proxy voice samples of {UserId}", userId);
            return new(VoiceFailure.ReadFailed);
        }
    }

    public async Task<VoiceOutcome> AddForPersonAsync(Guid actorId, Guid userId, Stream audio, string contentType,
        bool personAgreed, CancellationToken ct = default)
    {
        try
        {
            if (await ReadAsync(token => PersonAsync(userId, token), ct) is null) return new(VoiceFailure.NotFound);
            // Checked before the audio goes to the worker, then again under the lock before anything is kept.
            if (await ReadAsync(token => repository.FindActiveConsentAsync(userId, false, token), ct) is not { } given
                || !VoiceIdRules.AllowsProxyCollection(given.NoticeVersion))
                return new(VoiceFailure.NoConsent);
            // The standing consent says who may record them; this take is recorded only when they said yes to it, now.
            if (!personAgreed) return new(VoiceFailure.NotConfirmed);
            if (VoiceIdRules.ProxyFull(await ReadAsync(token => repository.ListProxySamplesAsync(userId, token), ct)))
                return new(VoiceFailure.LimitReached);
        }
        catch (PersistenceException ex)
        {
            logger.LogError(ex.InnerException ?? ex, "Failed to read proxy voice samples of {UserId}", userId);
            return new(VoiceFailure.SaveFailed);
        }

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
            logger.LogWarning(ex, "Speech worker could not embed a sample collected for {UserId}", userId);
            return new(VoiceFailure.EngineUnavailable);
        }

        try
        {
            return await unitOfWork.ExecuteAsync(async token =>
            {
                // The same row lock a withdrawal takes: a sample is never kept after the person said no.
                var consent = await repository.FindActiveConsentAsync(userId, true, token);
                if (consent is null || !VoiceIdRules.AllowsProxyCollection(consent.NoticeVersion))
                    return new VoiceOutcome(VoiceFailure.NoConsent);
                if (await PersonAsync(userId, token) is null) return new(VoiceFailure.NotFound);
                if (VoiceIdRules.ProxyFull(await repository.ListProxySamplesAsync(userId, token)))
                    return new(VoiceFailure.LimitReached);
                var now = clock.GetUtcNow();
                var id = Guid.CreateVersion7();
                repository.Add(new VoiceSample
                {
                    Id = id, UserId = userId, ConsentId = consent.Id, EmbeddingModel = embedding.Model,
                    Ciphertext = protector.Protect(embedding.Embedding, userId, id, embedding.Model),
                    SpeechMs = embedding.SpeechMs,
                    AudioCiphertext = protector.ProtectAudio(recorded, userId, id), AudioContentType = contentType,
                    CollectedById = actorId, MeetingId = null, CreatedAt = now, UpdatedAt = now,
                });
                var collector = (await callers.FindByIdAsync(actorId, token))?.FullName ?? "Người thu hộ";
                notifications.Add(new Notification
                {
                    Id = Guid.CreateVersion7(), UserId = userId, Type = NotificationType.VoiceSampleCollected,
                    Title = "Quản trị viên đã thu giọng mẫu của bạn",
                    Body = Clip($"{collector} đã thu giọng mẫu Voice ID của bạn. "
                        + AskedNote),
                    TargetType = NotificationTargetType, TargetId = id, CreatedAt = now, UpdatedAt = now,
                });
                await audit.RecordAsync(AuditActions.VoiceSampleAdded, AuditActions.VoiceSampleObjectType, id, actorId,
                    new { scope = "proxy", userId, meetingId = (Guid?)null, embedding.Model, embedding.SpeechMs, personAgreed }, token);
                await unitOfWork.SaveChangesAsync(token);
                return new VoiceOutcome(Data: new ProxySampleView(id, now, embedding.SpeechMs, true, null, null, collector));
            }, TransactionMode.ReadCommitted, ct);
        }
        catch (PersistenceException ex)
        {
            logger.LogError(ex.InnerException ?? ex, "Failed to store a voice sample collected for {UserId}", userId);
            return new(VoiceFailure.SaveFailed);
        }
    }

    public async Task<VoiceSampleAudio?> PersonAudioAsync(Guid actorId, Guid userId, Guid sampleId, CancellationToken ct = default)
    {
        var sample = await ReadAsync(async token => await PersonAsync(userId, token) is null ? null
            : await repository.FindProxySampleAsync(userId, sampleId, false, token), ct);
        if (sample?.AudioCiphertext is null || sample.AudioContentType is null) return null;
        byte[] bytes;
        try
        {
            bytes = protector.UnprotectAudio(sample.AudioCiphertext, sample.UserId, sample.Id);
        }
        catch (CryptographicException ex)
        {
            logger.LogWarning(ex, "Audio of voice sample {SampleId} could not be decrypted", sampleId);
            return null;
        }
        // Someone else's biometric sample heard: on the record before a byte leaves.
        await audit.RecordAsync(AuditActions.VoiceSamplePlayed, AuditActions.VoiceSampleObjectType, sampleId, actorId,
            new { userId, sample.MeetingId }, ct);
        await unitOfWork.SaveChangesAsync(ct);
        return new(bytes, sample.AudioContentType);
    }

    public async Task<VoiceOutcome> DeletePersonSampleAsync(Guid actorId, Guid userId, Guid sampleId, CancellationToken ct = default)
    {
        try
        {
            return await ReadAsync(async token =>
            {
                if (await PersonAsync(userId, token) is null) return new VoiceOutcome(VoiceFailure.NotFound);
                // The person's own sample, or one of someone else, is answered exactly like one that does not exist.
                if (await repository.FindProxySampleAsync(userId, sampleId, true, token) is not { } sample)
                    return new(VoiceFailure.NotFound);
                repository.Remove(sample);
                await audit.RecordAsync(AuditActions.VoiceSampleDeleted, AuditActions.VoiceSampleObjectType, sampleId, actorId,
                    new { scope = "proxy", userId, sample.MeetingId, sample.CollectedById }, token);
                await unitOfWork.SaveChangesAsync(token);
                return new(Data: new { id = sampleId });
            }, ct);
        }
        catch (PersistenceException ex)
        {
            logger.LogError(ex.InnerException ?? ex, "Failed to delete voice sample {SampleId}", sampleId);
            return new(VoiceFailure.SaveFailed);
        }
    }

    /// <summary>The person, when their account exists and is active; a deactivated one has no Voice ID left (ADR-0049).</summary>
    private async Task<User?> PersonAsync(Guid userId, CancellationToken ct)
        => await callers.FindByIdAsync(userId, ct) is { IsActive: true } user ? user : null;

    /// <summary>
    /// Storage calls outside a transaction (reads, and the few writes that never had one): run as they are, only their
    /// failures translated.
    /// </summary>
    private Task<T> ReadAsync<T>(Func<CancellationToken, Task<T>> work, CancellationToken ct)
        => unitOfWork.ExecuteAsync(work, TransactionMode.None, ct);

    private static string Clip(string body) => body.Length <= 2000 ? body : body[..2000];
}
