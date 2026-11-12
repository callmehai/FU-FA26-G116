using System.Security.Cryptography;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Npgsql;
using TrustCabinet.Domain.Audit;
using TrustCabinet.Domain.Entities;
using TrustCabinet.Domain.Enums;
using TrustCabinet.Domain.Participants;
using TrustCabinet.Domain.Recordings;
using TrustCabinet.Domain.Transcription;
using TrustCabinet.Domain.VoiceId;

namespace TrustCabinet.Infrastructure.VoiceId;

/// <summary>
/// ADR-0060 — the Secretary of an in-person or hybrid meeting records Voice ID samples for its people on the room's mic.
///
/// Three locks keep this to people who trust the Secretary with their voice:
/// <list type="bullet">
/// <item>the caller holds <see cref="Capability.VoiceCollector"/> (the endpoint group checks it on every request);</item>
/// <item>the caller is the Secretary of this very meeting, now — checked here on every call, so a Secretary replaced
/// mid-meeting loses it at once; anyone else is answered as if the meeting did not exist;</item>
/// <item>the person has their own standing <see cref="VoiceConsentScope.Proxy"/> consent, which only they can give.</item>
/// </list>
/// A sample is bound to the person and encrypted exactly as one they took themself; the Secretary sees, hears and deletes
/// only the samples taken in their meeting, each play is audited, and the person is told each time one is taken.
/// </summary>
public sealed class VoiceCollectionService(TrustCabinetDbContext db, IVoiceIdRepository repository, IVoiceEmbedder embedder,
    IVoiceprintProtector protector, IAuditService audit, TimeProvider clock, ILogger<VoiceCollectionService> logger)
    : IVoiceCollectionService
{
    private const string NotificationTargetType = "VoiceSample";

    private static bool PersistenceError(Exception ex) => ex is DbUpdateException or NpgsqlException or TimeoutException;

    /// <summary>Samples can be taken while the people are gathered or about to be: Published or InProgress.</summary>
    public static bool CanCollect(MeetingStatus status) => status is MeetingStatus.Published or MeetingStatus.InProgress;

    public async Task<VoiceOutcome> ListAsync(Guid meetingId, Guid actorId, CancellationToken ct = default)
    {
        try
        {
            var meeting = await ManagedAsync(meetingId, actorId, ct);
            if (meeting is null) return new(VoiceFailure.NotFound);
            var people = meeting.Participants.Where(ParticipantRules.Active)
                .GroupBy(p => p.UserId).Select(g => g.OrderBy(p => p.Role).First()).ToArray();
            var ids = people.Select(p => p.UserId).ToArray();
            var consented = (await db.VoiceConsents.AsNoTracking()
                    .Where(c => ids.Contains(c.UserId) && c.Scope == VoiceConsentScope.Proxy && c.WithdrawnAt == null)
                    .Select(c => c.UserId).ToListAsync(ct)).ToHashSet();
            var samples = await db.VoiceSamples.AsNoTracking()
                .Where(s => ids.Contains(s.UserId) && s.CollectedById != null)
                .Select(s => new { s.Id, s.UserId, s.MeetingId, s.CreatedAt, s.SpeechMs, CollectedByName = s.CollectedBy!.FullName })
                .ToListAsync(ct);
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
            return new(Data: new VoiceCollectionView(CanCollect(meeting.Status), VoiceIdRules.MaxSamples, list));
        }
        catch (Exception ex) when (PersistenceError(ex))
        {
            logger.LogError(ex, "Failed to read voice collection of meeting {MeetingId}", meetingId);
            return new(VoiceFailure.ReadFailed);
        }
    }

    public async Task<VoiceOutcome> AddAsync(Guid meetingId, Guid actorId, Guid userId, Stream audio, string contentType,
        CancellationToken ct = default)
    {
        Meeting? meeting;
        try
        {
            meeting = await ManagedAsync(meetingId, actorId, ct);
            if (meeting is null) return new(VoiceFailure.NotFound);
            if (!CanCollect(meeting.Status)) return new(VoiceFailure.NotCollectable);
            // Only a person of this meeting, with an account: a guest has no Voice ID to keep a sample under.
            if (!meeting.Participants.Any(p => p.UserId == userId && ParticipantRules.Active(p))) return new(VoiceFailure.NotFound);
            // Checked before the audio goes to the worker, then again under the lock before anything is kept.
            if (await repository.FindActiveConsentAsync(userId, false, ct, VoiceConsentScope.Proxy) is null)
                return new(VoiceFailure.NoConsent);
            if ((await repository.ListProxySamplesAsync(userId, ct)).Count >= VoiceIdRules.MaxSamples)
                return new(VoiceFailure.LimitReached);
        }
        catch (Exception ex) when (PersistenceError(ex))
        {
            logger.LogError(ex, "Failed to read voice collection of meeting {MeetingId}", meetingId);
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
            return await repository.InTransactionAsync(async token =>
            {
                // The same row lock a withdrawal takes: a sample is never kept after the person said no.
                var consent = await repository.FindActiveConsentAsync(userId, true, token, VoiceConsentScope.Proxy);
                if (consent is null) return new VoiceOutcome(VoiceFailure.NoConsent);
                // The Secretary, and the meeting's state, again: either may have changed while the worker listened.
                var current = await ManagedAsync(meetingId, actorId, token);
                if (current is null) return new(VoiceFailure.NotFound);
                if (!CanCollect(current.Status)) return new(VoiceFailure.NotCollectable);
                if ((await repository.ListProxySamplesAsync(userId, token)).Count >= VoiceIdRules.MaxSamples)
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
                db.Notifications.Add(new Notification
                {
                    Id = Guid.CreateVersion7(), UserId = userId, Type = NotificationType.VoiceSampleCollected,
                    Title = "Thư ký đã thu giọng mẫu của bạn",
                    Body = Clip($"{secretary} đã thu giọng mẫu Voice ID của bạn ở cuộc họp \"{current.Title}\". "
                        + "Bạn nghe lại hoặc xoá được ở trang Giọng nói của tôi."),
                    TargetType = NotificationTargetType, TargetId = id, CreatedAt = now, UpdatedAt = now,
                });
                await audit.RecordAsync(AuditActions.VoiceSampleAdded, AuditActions.VoiceSampleObjectType, id, actorId,
                    new { scope = "proxy", userId, meetingId, embedding.Model, embedding.SpeechMs }, token);
                await repository.SaveChangesAsync(token);
                return new VoiceOutcome(Data: new CollectedSampleView(id, now, embedding.SpeechMs, secretary));
            }, ct);
        }
        catch (Exception ex) when (PersistenceError(ex))
        {
            logger.LogError(ex, "Failed to store a voice sample collected in meeting {MeetingId}", meetingId);
            return new(VoiceFailure.SaveFailed);
        }
    }

    public async Task<VoiceSampleAudio?> AudioAsync(Guid meetingId, Guid actorId, Guid sampleId, CancellationToken ct = default)
    {
        if (await ManagedAsync(meetingId, actorId, ct) is null) return null;
        var sample = await db.VoiceSamples.AsNoTracking()
            .SingleOrDefaultAsync(s => s.Id == sampleId && s.MeetingId == meetingId && s.CollectedById != null, ct);
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
        await db.SaveChangesAsync(ct);
        return new(bytes, sample.AudioContentType);
    }

    public async Task<VoiceOutcome> DeleteAsync(Guid meetingId, Guid actorId, Guid sampleId, CancellationToken ct = default)
    {
        try
        {
            if (await ManagedAsync(meetingId, actorId, ct) is null) return new(VoiceFailure.NotFound);
            var sample = await db.VoiceSamples
                .SingleOrDefaultAsync(s => s.Id == sampleId && s.MeetingId == meetingId && s.CollectedById != null, ct);
            // A sample taken in another meeting, or the person's own, is answered exactly like one that does not exist.
            if (sample is null) return new(VoiceFailure.NotFound);
            db.VoiceSamples.Remove(sample);
            await audit.RecordAsync(AuditActions.VoiceSampleDeleted, AuditActions.VoiceSampleObjectType, sampleId, actorId,
                new { scope = "proxy", sample.UserId, meetingId, sample.CollectedById }, ct);
            await db.SaveChangesAsync(ct);
            return new(Data: new { id = sampleId });
        }
        catch (Exception ex) when (PersistenceError(ex))
        {
            logger.LogError(ex, "Failed to delete voice sample {SampleId}", sampleId);
            return new(VoiceFailure.SaveFailed);
        }
    }

    /// <summary>
    /// The meeting, when the caller is now its Secretary, it has a room mic, and it is not Secret (a Secret meeting is
    /// never recorded, ADR-0057). Null otherwise: to the caller that is a meeting that does not exist.
    /// </summary>
    private async Task<Meeting?> ManagedAsync(Guid meetingId, Guid actorId, CancellationToken ct)
    {
        var meeting = await db.Meetings.AsNoTracking().AsSplitQuery()
            .Include(m => m.Participants).ThenInclude(p => p.User)
            .SingleOrDefaultAsync(m => m.Id == meetingId, ct);
        if (meeting is null || meeting.Sensitivity == Sensitivity.Secret || !RecordingRules.HasRoom(meeting.Mode)
            || meeting.Status is MeetingStatus.Cancelled || !ParticipantRules.IsSecretary(meeting, actorId))
            return null;
        return meeting;
    }

    private static string Clip(string body) => body.Length <= 2000 ? body : body[..2000];
}
