using System.Security.Cryptography;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Npgsql;
using TrustCabinet.Domain.Audit;
using TrustCabinet.Domain.Entities;
using TrustCabinet.Domain.Enums;
using TrustCabinet.Domain.Meetings;
using TrustCabinet.Domain.Participants;
using TrustCabinet.Domain.Recordings;
using TrustCabinet.Domain.Transcription;
using TrustCabinet.Domain.VoiceId;
using TrustCabinet.Infrastructure.VoiceId;

namespace TrustCabinet.Infrastructure.Transcription;

/// <summary>
/// SCRUM-31 — speech to text for a recording, run in the background by <c>TranscriptionWorker</c>. The audio is
/// sent to the in-house speech worker only; the transcript is the same personal data as the audio and is erased
/// with it (RecordingService).
/// </summary>
public sealed class TranscriptionService(IMeetingRepository meetings, IRecordingRepository recordings,
    ITranscriptRepository transcripts, IRecordingStorage storage, ISpeechEngine engine, IVoiceIdRepository voices,
    IVoiceprintProtector protector, IOptions<VoiceIdOptions> voiceOptions, IAuditService audit, TimeProvider clock,
    ILogger<TranscriptionService> logger) : ITranscriptionService
{
    private static bool PersistenceError(Exception ex) => ex is DbUpdateException or NpgsqlException or TimeoutException;

    public async Task<TranscriptOutcome> GetAsync(Guid recordingId, Guid actorId, CancellationToken ct = default)
    {
        try
        {
            var (recording, meeting, failure) = await ReadableAsync(recordingId, actorId, ct);
            if (recording is null || meeting is null) return new(failure);
            var canEdit = RecordingRules.IsSecretary(meeting, actorId);
            var transcript = await transcripts.FindByRecordingAsync(recordingId, false, ct);
            // Who the secretary may name a voice as: the meeting's own people, offered only to the secretary.
            TranscriptPerson[] people = canEdit
                ? [.. Participants(meeting).Select(p => new TranscriptPerson(p.UserId, p.User.FullName))
                    .DistinctBy(p => p.Id).OrderBy(p => p.FullName)]
                : [];
            if (transcript is null)
                return new(Data: new TranscriptView(recordingId, "None", "", "vi", "", null, recording.DurationMs, canEdit, [], [], people));
            return new(Data: new TranscriptView(recordingId, transcript.Status.ToString(), transcript.Provider,
                transcript.Language, transcript.ErrorMessage, transcript.CompletedAt, recording.DurationMs, canEdit,
                [.. transcript.Segments.OrderBy(s => s.Position).Select(s => new TranscriptSegmentView(s.Id, s.Position,
                    s.StartMs, s.EndMs, s.Text, s.SpeakerLabel, s.EditedAt, s.EditedBy?.FullName))],
                [.. transcript.Speakers.OrderBy(s => s.Label).Select(s => new TranscriptSpeakerView(s.Label, s.SpeechMs,
                    s.SuggestedUserId, s.SuggestedUser?.FullName, s.SuggestionScore, s.AssignedUserId, s.AssignedUser?.FullName))],
                people));
        }
        catch (Exception ex) when (PersistenceError(ex))
        {
            logger.LogError(ex, "Failed to read transcript of {RecordingId}", recordingId);
            return new(TranscriptFailure.ReadFailed);
        }
    }

    public async Task<TranscriptOutcome> RetryAsync(Guid recordingId, Guid actorId, CancellationToken ct = default)
    {
        try
        {
            return await meetings.InTransactionAsync(async token =>
            {
                var (recording, meeting, failure) = await ReadableAsync(recordingId, actorId, token, forUpdate: true);
                if (recording is null || meeting is null) return new TranscriptOutcome(failure);
                if (!RecordingRules.IsSecretary(meeting, actorId)) return new(TranscriptFailure.Forbidden);
                var transcript = await transcripts.FindByRecordingAsync(recordingId, true, token);
                var now = clock.GetUtcNow();
                if (transcript is null)
                    // Explicitly added: a new entity hung off a tracked one would be taken for an existing row (its key is set).
                    transcripts.Add(new Transcript { RecordingId = recordingId, CreatedAt = now, UpdatedAt = now });
                else if (transcript.Status == TranscriptStatus.Failed)
                {
                    transcript.Status = TranscriptStatus.Pending;
                    transcript.ErrorMessage = "";
                    transcript.ProgressPercent = 0;
                    transcript.UpdatedAt = now;
                }
                else return new(TranscriptFailure.NotRetryable);
                await transcripts.SaveChangesAsync(token);
                return new TranscriptOutcome();
            }, ct);
        }
        catch (Exception ex) when (PersistenceError(ex))
        {
            logger.LogError(ex, "Failed to queue transcription of {RecordingId}", recordingId);
            return new(TranscriptFailure.SaveFailed);
        }
    }

    public async Task<TranscriptOutcome> EditSegmentAsync(Guid segmentId, Guid actorId, string? text, CancellationToken ct = default)
    {
        try
        {
            var segment = await transcripts.FindSegmentAsync(segmentId, ct);
            if (segment is null) return new(TranscriptFailure.NotFound);
            var (recording, meeting, failure) = await ReadableAsync(segment.Transcript.RecordingId, actorId, ct);
            if (recording is null || meeting is null) return new(failure);
            if (!RecordingRules.IsSecretary(meeting, actorId)) return new(TranscriptFailure.Forbidden);
            var trimmed = text?.Trim() ?? "";
            if (trimmed.Length is 0 or > TranscriptRules.MaxSegmentText)
                return new(TranscriptFailure.Invalid, Errors: new Dictionary<string, string[]>
                {
                    ["text"] = [$"Nội dung đoạn phải có từ 1 đến {TranscriptRules.MaxSegmentText} ký tự."],
                });
            var now = clock.GetUtcNow();
            segment.Text = trimmed;
            segment.EditedById = actorId;
            segment.EditedAt = now;
            segment.UpdatedAt = now;
            await transcripts.SaveChangesAsync(ct);
            return new(Data: new { segmentId, text = trimmed, editedAt = now });
        }
        catch (Exception ex) when (PersistenceError(ex))
        {
            logger.LogError(ex, "Failed to edit transcript segment {SegmentId}", segmentId);
            return new(TranscriptFailure.SaveFailed);
        }
    }

    public async Task<TranscriptOutcome> AssignSpeakerAsync(Guid recordingId, string label, Guid? userId, Guid actorId,
        CancellationToken ct = default)
    {
        try
        {
            var (recording, meeting, failure) = await ReadableAsync(recordingId, actorId, ct);
            if (recording is null || meeting is null) return new(failure);
            if (!RecordingRules.IsSecretary(meeting, actorId)) return new(TranscriptFailure.Forbidden);
            var transcript = await transcripts.FindByRecordingAsync(recordingId, true, ct);
            var speaker = transcript?.Speakers.SingleOrDefault(s => s.Label == label);
            if (speaker is null) return new(TranscriptFailure.NotFound);
            var person = userId is { } id ? Participants(meeting).FirstOrDefault(p => p.UserId == id) : null;
            if (userId is not null && person is null) return new(TranscriptFailure.InvalidSpeaker);
            var now = clock.GetUtcNow();
            speaker.AssignedUserId = userId;
            speaker.AssignedById = actorId;
            speaker.AssignedAt = now;
            speaker.UpdatedAt = now;
            await audit.RecordAsync(AuditActions.TranscriptSpeakerAssigned, AuditActions.TranscriptSpeakerObjectType, speaker.Id,
                actorId, new { recordingId, label, userId, suggestedUserId = speaker.SuggestedUserId,
                    acceptedSuggestion = userId is not null && userId == speaker.SuggestedUserId }, ct);
            await transcripts.SaveChangesAsync(ct);
            return new(Data: new TranscriptSpeakerView(speaker.Label, speaker.SpeechMs, speaker.SuggestedUserId,
                speaker.SuggestedUser?.FullName, speaker.SuggestionScore, userId, person?.User.FullName));
        }
        catch (Exception ex) when (PersistenceError(ex))
        {
            logger.LogError(ex, "Failed to name speaker {Label} of {RecordingId}", label, recordingId);
            return new(TranscriptFailure.SaveFailed);
        }
    }

    public Task<int> RequeueInterruptedAsync(CancellationToken ct = default)
        => transcripts.RequeueProcessingAsync(clock.GetUtcNow(), ct);

    public async Task<bool> ProcessNextAsync(CancellationToken ct = default)
    {
        if (await transcripts.ClaimNextAsync(clock.GetUtcNow(), ct) is not { } transcriptId) return false;
        var transcript = await transcripts.FindForProcessingAsync(transcriptId, ct);
        if (transcript is null) return true;
        var recording = transcript.Recording;
        SpeechTranscription result;
        try
        {
            if (recording.Status != RecordingStatus.Available) throw new SpeechEngineException("Bản ghi đã bị xoá.");
            await using var audio = storage.OpenRead(recording.StoragePath);
            result = await engine.TranscribeAsync(audio, recording.ContentType, diarize: true, ct);
        }
        catch (Exception ex) when (ex is SpeechEngineException or FileNotFoundException or DirectoryNotFoundException)
        {
            var message = ex is SpeechEngineException speech ? speech.UserMessage : "Tệp âm thanh không còn trên máy chủ.";
            logger.LogWarning(ex, "Transcription of recording {RecordingId} failed: {Reason}", recording.Id, message);
            await FinishAsync(transcriptId, null, message, ct);
            return true;
        }
        await FinishAsync(transcriptId, result, null, ct);
        return true;
    }

    /// <summary>
    /// Writes the result under the recording's row lock — the same lock erasure takes — so a recording erased while
    /// the worker was busy never gets its transcript written back afterwards.
    /// </summary>
    private async Task FinishAsync(Guid transcriptId, SpeechTranscription? result, string? error, CancellationToken ct)
    {
        await meetings.InTransactionAsync(async token =>
        {
            var current = await transcripts.FindForProcessingAsync(transcriptId, token);
            if (current is null) return 0;
            var recording = await recordings.FindAsync(current.RecordingId, true, token);
            if (recording is null || recording.Status != RecordingStatus.Available) return 0;
            var now = clock.GetUtcNow();
            current.UpdatedAt = now;
            if (result is null)
            {
                current.Status = TranscriptStatus.Failed;
                current.ErrorMessage = error ?? "Không chuyển được bản ghi thành chữ.";
            }
            else
            {
                await transcripts.DeleteSegmentsAsync(transcriptId, token);
                transcripts.AddSegments(result.Segments.Where(s => s.Text.Length > 0).Select((s, i) => new TranscriptSegment
                {
                    TranscriptId = transcriptId, Position = i + 1, StartMs = s.StartMs, EndMs = s.EndMs,
                    Text = s.Text.Length > TranscriptRules.MaxSegmentText ? s.Text[..TranscriptRules.MaxSegmentText] : s.Text,
                    SpeakerLabel = s.Speaker ?? "", CreatedAt = now, UpdatedAt = now,
                }));
                await transcripts.DeleteSpeakersAsync(transcriptId, token);
                var suggestions = await SuggestSpeakersAsync(recording.MeetingId, result, token);
                transcripts.AddSpeakers(result.Speakers.Select(sp => new TranscriptSpeaker
                {
                    TranscriptId = transcriptId, Label = sp.Label, SpeechMs = sp.SpeechMs,
                    SuggestedUserId = suggestions.GetValueOrDefault(sp.Label)?.UserId,
                    SuggestionScore = suggestions.GetValueOrDefault(sp.Label)?.Score, CreatedAt = now, UpdatedAt = now,
                }));
                current.Status = TranscriptStatus.Completed;
                current.Provider = TranscriptRules.Provider(result.AsrModel);
                current.Language = result.Language;
                current.ProgressPercent = 100;
                current.ErrorMessage = "";
                current.CompletedAt = now;
                if (result.DurationMs > 0) recording.DurationMs = result.DurationMs;
            }
            await transcripts.SaveChangesAsync(token);
            return 0;
        }, ct);
    }

    /// <summary>
    /// SCRUM-35 — compares each voice with the enrolled samples of the meeting's own people who consented. The voices'
    /// embeddings are used here and dropped; only the resulting suggestion is stored.
    /// </summary>
    private async Task<IReadOnlyDictionary<string, SpeakerMatcher.Suggestion>> SuggestSpeakersAsync(Guid meetingId,
        SpeechTranscription result, CancellationToken ct)
    {
        if (result.Speakers.Count == 0 || result.EmbeddingModel.Length == 0) return new Dictionary<string, SpeakerMatcher.Suggestion>();
        var meeting = await meetings.FindWorkflowAsync(meetingId, false, ct);
        if (meeting is null) return new Dictionary<string, SpeakerMatcher.Suggestion>();
        var people = Participants(meeting).Select(p => p.UserId).Distinct().ToArray();
        var candidates = new List<SpeakerMatcher.Candidate>();
        foreach (var sample in await voices.ListMatchableSamplesAsync(people, result.EmbeddingModel, ct))
        {
            try
            {
                candidates.Add(new(sample.UserId, protector.Unprotect(sample.Ciphertext, sample.UserId, sample.Id, sample.EmbeddingModel)));
            }
            catch (CryptographicException ex)
            {
                // A key rotated without re-enrolment: that person is simply not suggested.
                logger.LogWarning(ex, "Voice sample {SampleId} could not be decrypted", sample.Id);
            }
        }
        var options = voiceOptions.Value;
        return SpeakerMatcher.Suggest(result.Speakers, candidates, options.SuggestThreshold, options.SuggestMargin);
    }

    private static IEnumerable<MeetingParticipant> Participants(Meeting meeting)
        => meeting.Participants.Where(p => p.MeetingId == meeting.Id && ParticipantRules.Active(p));

    private async Task<(Recording? Recording, Meeting? Meeting, TranscriptFailure Failure)> ReadableAsync(Guid recordingId,
        Guid actorId, CancellationToken ct, bool forUpdate = false)
    {
        if (!await meetings.IsUserActiveAsync(actorId, ct)) return (null, null, TranscriptFailure.Inactive);
        var recording = await recordings.FindAsync(recordingId, forUpdate, ct);
        if (recording is null) return (null, null, TranscriptFailure.NotFound);
        var meeting = await meetings.FindWorkflowAsync(recording.MeetingId, false, ct);
        if (meeting is null || !RecordingRules.CanListen(meeting, actorId) || recording.Status != RecordingStatus.Available)
            return (null, null, TranscriptFailure.NotFound);
        return (recording, meeting, TranscriptFailure.None);
    }
}
