using System.Security.Cryptography;
using Microsoft.Extensions.Logging;
using TrustCabinet.Application.Abstractions;
using TrustCabinet.Application.Audit;
using TrustCabinet.Application.Notifications;
using TrustCabinet.Application.Recordings;
using TrustCabinet.Application.Transcription;
using TrustCabinet.Application.Users;
using TrustCabinet.Domain.Audit;
using TrustCabinet.Domain.Entities;
using TrustCabinet.Domain.Enums;
using TrustCabinet.Domain.Participants;
using TrustCabinet.Domain.VoiceId;

namespace TrustCabinet.Application.VoiceId;

/// <summary>
/// ADR-0060 — the Secretary of an in-person or hybrid meeting records Voice ID samples for its people on the room's mic.
///
/// Four locks keep this to people who trust the Secretary with their voice:
/// <list type="bullet">
/// <item>the caller holds <see cref="Capability.VoiceCollector"/>, or — PO 2026-11-21 — is a meeting Secretary without
/// it, who may then act only while the meeting is Published or InProgress: a stand-in for that one meeting, with no
/// standing right over the samples once it ends;</item>
/// <item>the caller is the Secretary of this very meeting, now — checked here on every call, so a Secretary replaced
/// mid-meeting loses it at once; anyone else is answered as if the meeting did not exist;</item>
/// <item>the person has their own standing consent, to a notice that names this, which only they can give: notice 1.4 or
/// later on both ways in (ADR-0073);</item>
/// <item>the person agreed to this very take: the collector confirms it with each upload (<c>personAgreed</c>), the audit
/// row keeps the confirmation, and the person's notice says so, with the way to delete the sample (ADR-0073).</item>
/// </list>
/// A sample is bound to the person and encrypted exactly as one they took themself; the Secretary sees, hears and deletes
/// only the samples taken in their meeting, each play is audited, and the person is told each time one is taken.
///
/// ADR-0071 adds a second way in, with no meeting: a holder of <see cref="Capability.VoiceCollector"/> (gated by the
/// endpoint) records an active person at any time, under that person's consent to notice 1.4 or later, and sees, hears
/// and deletes every sample anyone took for them. Same limits, encryption, notice to the person and audit.
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

    /// <summary>Samples can be taken while the people are gathered or about to be: Published or InProgress.</summary>
    public static bool CanCollect(MeetingStatus status) => status is MeetingStatus.Published or MeetingStatus.InProgress;

    public async Task<VoiceOutcome> ListAsync(Guid meetingId, Guid actorId, CancellationToken ct = default)
    {
        try
        {
            return await ReadAsync(async token =>
            {
                var meeting = await ManagedAsync(meetingId, actorId, token);
                if (meeting is null) return new VoiceOutcome(VoiceFailure.NotFound);
                var people = meeting.Participants.Where(ParticipantRules.Active)
                    .GroupBy(p => p.UserId).Select(g => g.OrderBy(p => p.Role).First()).ToArray();
                var ids = people.Select(p => p.UserId).ToArray();
                var consented = (await repository.ListConsentNoticesAsync(ids, token))
                    .Where(c => VoiceIdRules.AllowsProxyCollection(c.NoticeVersion)).Select(c => c.UserId).ToHashSet();
                var samples = await repository.ListCollectedSamplesAsync(ids, token);
                var list = people
                    .OrderBy(p => p.Role).ThenBy(p => p.User.FullName, StringComparer.CurrentCulture)
                    .Select(p =>
                    {
                        var own = samples.Where(s => s.UserId == p.UserId).ToArray();
                        return new VoiceCollectionPerson(p.UserId, p.User.FullName, p.Role.ToString(), consented.Contains(p.UserId),
                            own.Length,
                            [.. own.Where(s => s.MeetingId == meetingId).OrderBy(s => s.CreatedAt)
                                .Select(s => new CollectedSampleView(s.Id, s.CreatedAt, s.SpeechMs, s.CollectedByName))]);
                    })
                    .ToArray();
                return new(Data: new VoiceCollectionView(CanCollect(meeting.Status), VoiceIdRules.MaxProxySamples, list));
            }, ct);
        }
        catch (PersistenceException ex)
        {
            logger.LogError(ex.InnerException ?? ex, "Failed to read voice collection of meeting {MeetingId}", meetingId);
            return new(VoiceFailure.ReadFailed);
        }
    }

    public async Task<VoiceOutcome> AddAsync(Guid meetingId, Guid actorId, Guid userId, Stream audio, string contentType,
        bool personAgreed, CancellationToken ct = default)
    {
        Meeting? meeting;
        try
        {
            meeting = await ReadAsync(token => ManagedAsync(meetingId, actorId, token), ct);
            if (meeting is null) return new(VoiceFailure.NotFound);
            if (!CanCollect(meeting.Status)) return new(VoiceFailure.NotCollectable);
            // Only a person of this meeting, with an account: a guest has no Voice ID to keep a sample under.
            if (!meeting.Participants.Any(p => p.UserId == userId && ParticipantRules.Active(p))) return new(VoiceFailure.NotFound);
            // Checked before the audio goes to the worker, then again under the lock before anything is kept.
            if (await ReadAsync(token => repository.FindActiveConsentAsync(userId, false, token), ct) is not { } given
                || !VoiceIdRules.AllowsProxyCollection(given.NoticeVersion))
                return new(VoiceFailure.NoConsent);
            // The standing consent says who may record them; this take is recorded only when they said yes to it, now.
            if (!personAgreed) return new(VoiceFailure.NotConfirmed);
            if (VoiceIdRules.ProxyFull(await ReadAsync(token => repository.ListProxySamplesAsync(userId, token), ct), actorId))
                return new(VoiceFailure.LimitReached);
        }
        catch (PersistenceException ex)
        {
            logger.LogError(ex.InnerException ?? ex, "Failed to read voice collection of meeting {MeetingId}", meetingId);
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
            logger.LogWarning(ex, "Speech worker could not embed a sample collected in meeting {MeetingId}", meetingId);
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
                // The Secretary, and the meeting's state, again: either may have changed while the worker listened.
                var current = await ManagedAsync(meetingId, actorId, token);
                if (current is null) return new(VoiceFailure.NotFound);
                if (!CanCollect(current.Status)) return new(VoiceFailure.NotCollectable);
                if (VoiceIdRules.ProxyFull(await repository.ListProxySamplesAsync(userId, token), actorId))
                    return new(VoiceFailure.LimitReached);
                var now = clock.GetUtcNow();
                var id = Guid.CreateVersion7();
                var sample = new VoiceSample
                {
                    Id = id, UserId = userId, ConsentId = consent.Id, EmbeddingModel = embedding.Model,
                    Ciphertext = protector.Protect(embedding.Embedding, userId, id, embedding.Model),
                    SpeechMs = embedding.SpeechMs,
                    // Always kept: the Secretary checks the take is clean, the owner hears what was recorded of them.
                    AudioCiphertext = protector.ProtectAudio(recorded, userId, id), AudioContentType = contentType,
                    CollectedById = actorId, MeetingId = meetingId, CreatedAt = now, UpdatedAt = now,
                };
                repository.Add(sample);
                var secretary = current.Participants.First(p => p.UserId == actorId).User.FullName;
                notifications.Add(new Notification
                {
                    Id = Guid.CreateVersion7(), UserId = userId, Type = NotificationType.VoiceSampleCollected,
                    Title = "Thư ký đã thu giọng mẫu của bạn",
                    Body = Clip($"{secretary} đã thu giọng mẫu Voice ID của bạn ở cuộc họp \"{current.Title}\". "
                        + AskedNote),
                    TargetType = NotificationTargetType, TargetId = id, CreatedAt = now, UpdatedAt = now,
                });
                await audit.RecordAsync(AuditActions.VoiceSampleAdded, AuditActions.VoiceSampleObjectType, id, actorId,
                    new { scope = "proxy", userId, meetingId, embedding.Model, embedding.SpeechMs, personAgreed }, token);
                await unitOfWork.SaveChangesAsync(token);
                return new VoiceOutcome(Data: new CollectedSampleView(id, now, embedding.SpeechMs, secretary));
            }, TransactionMode.ReadCommitted, ct);
        }
        catch (PersistenceException ex)
        {
            logger.LogError(ex.InnerException ?? ex, "Failed to store a voice sample collected in meeting {MeetingId}", meetingId);
            return new(VoiceFailure.SaveFailed);
        }
    }

    public async Task<VoiceSampleAudio?> AudioAsync(Guid meetingId, Guid actorId, Guid sampleId, CancellationToken ct = default)
    {
        var sample = await ReadAsync(async token => await ManagedAsync(meetingId, actorId, token) is null ? null
            : await repository.FindCollectedSampleAsync(meetingId, sampleId, false, token), ct);
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
            new { userId = sample.UserId, meetingId }, ct);
        await unitOfWork.SaveChangesAsync(ct);
        return new(bytes, sample.AudioContentType);
    }

    public async Task<VoiceOutcome> DeleteAsync(Guid meetingId, Guid actorId, Guid sampleId, CancellationToken ct = default)
    {
        try
        {
            return await ReadAsync(async token =>
            {
                if (await ManagedAsync(meetingId, actorId, token) is null) return new VoiceOutcome(VoiceFailure.NotFound);
                var sample = await repository.FindCollectedSampleAsync(meetingId, sampleId, true, token);
                // A sample taken in another meeting, or the person's own, is answered exactly like one that does not exist.
                if (sample is null) return new(VoiceFailure.NotFound);
                repository.Remove(sample);
                await audit.RecordAsync(AuditActions.VoiceSampleDeleted, AuditActions.VoiceSampleObjectType, sampleId, actorId,
                    new { scope = "proxy", sample.UserId, meetingId, sample.CollectedById }, token);
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
                    consented && !VoiceIdRules.ProxyFull(samples, actorId), VoiceIdRules.MaxProxySamples,
                    VoiceIdRules.MaxProxySamplesPerCollector,
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
            if (VoiceIdRules.ProxyFull(await ReadAsync(token => repository.ListProxySamplesAsync(userId, token), ct), actorId))
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
                if (VoiceIdRules.ProxyFull(await repository.ListProxySamplesAsync(userId, token), actorId))
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
                    Title = "Có người đã thu giọng mẫu của bạn",
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
    /// The meeting, when the caller is now its Secretary, it has a room mic, and it is not Secret (a Secret meeting is
    /// never recorded, ADR-0057). Null otherwise: to the caller that is a meeting that does not exist.
    /// </summary>
    private async Task<Meeting?> ManagedAsync(Guid meetingId, Guid actorId, CancellationToken ct)
    {
        var meeting = await repository.FindMeetingWithPeopleAsync(meetingId, ct);
        if (meeting is null || meeting.Sensitivity == Sensitivity.Secret || !RecordingRules.HasRoom(meeting.Mode)
            || meeting.Status is MeetingStatus.Cancelled || !ParticipantRules.IsSecretary(meeting, actorId))
            return null;
        // A Secretary without the system capability holds it only for this meeting, while it is open.
        if (!CanCollect(meeting.Status) && !await HoldsCollectorAsync(actorId, ct)) return null;
        return meeting;
    }

    private async Task<bool> HoldsCollectorAsync(Guid actorId, CancellationToken ct)
    {
        var caller = await callers.FindByIdAsync(actorId, ct);
        return caller is { IsActive: true }
            && (await callers.GetCapabilitiesAsync(caller.RoleId, ct)).Contains(Capability.VoiceCollector);
    }

    /// <summary>
    /// Storage calls outside a transaction (reads, and the few writes that never had one): run as they are, only their
    /// failures translated.
    /// </summary>
    private Task<T> ReadAsync<T>(Func<CancellationToken, Task<T>> work, CancellationToken ct)
        => unitOfWork.ExecuteAsync(work, TransactionMode.None, ct);

    private static string Clip(string body) => body.Length <= 2000 ? body : body[..2000];
}
