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
    IVoiceprintProtector protector, IOptions<VoiceIdOptions> voiceOptions, IAuditService audit,
    ISpeechChangePublisher publisher, TimeProvider clock, ILogger<TranscriptionService> logger) : ITranscriptionService
{
    private static bool PersistenceError(Exception ex) => ex is DbUpdateException or NpgsqlException or TimeoutException;

    public async Task<TranscriptOutcome> GetAsync(Guid recordingId, Guid actorId, CancellationToken ct = default)
    {
        try
        {
            var (recording, meeting, failure) = await ReadableAsync(recordingId, actorId, ct);
            if (recording is null || meeting is null) return new(failure);
            var canEdit = RecordingRules.IsSecretary(meeting, actorId);
            var canExport = RecordingRules.Export(meeting, actorId) == RecordingFailure.None;
            var transcript = await transcripts.FindByRecordingAsync(recordingId, false, ct);
            // Who the secretary may name a voice as: the meeting's own people and its guests, offered only to the secretary.
            TranscriptPerson[] people = canEdit
                ? [.. Participants(meeting).Select(p => new TranscriptPerson(p.UserId, p.User.FullName)).DistinctBy(p => p.Id)
                    .Concat(await transcripts.ListGuestsAsync(meeting.Id, ct)).OrderBy(p => p.FullName)]
                : [];
            if (transcript is null)
                return new(Data: new TranscriptView(recordingId, "None", "", "vi", "", null, recording.DurationMs, canEdit, [], [], people));
            // The machine's guess at a voice is a hint for the Secretary who names it, and a guess about someone's
            // biometric identity: nobody else gets it, only the name the Secretary assigned.
            var sameAs = canEdit
                ? SpeakerMatcher.SameAs([.. transcript.Speakers.Select(s => (s.Label, s.SuggestedUserId, s.SuggestionScore, s.AssignedUserId))])
                : new Dictionary<string, string>();
            return new(Data: new TranscriptView(recordingId, transcript.Status.ToString(), transcript.Provider,
                transcript.Language, transcript.ErrorMessage, transcript.CompletedAt, recording.DurationMs, canEdit,
                [.. transcript.Segments.OrderBy(s => s.Position).Select(s => new TranscriptSegmentView(s.Id, s.Position,
                    s.StartMs, s.EndMs, s.Text, s.SpeakerLabel, s.EditedAt, s.EditedBy?.FullName))],
                [.. transcript.Speakers.OrderBy(s => s.Label).Select(s => new TranscriptSpeakerView(s.Label, s.SpeechMs,
                    canEdit ? s.SuggestedUserId : null, canEdit ? s.SuggestedUser?.FullName : null,
                    canEdit ? s.SuggestionScore : null, s.AssignedUserId, AssignedName(s), sameAs.GetValueOrDefault(s.Label),
                    s.AssignedGuestId))],
                people, canExport && transcript.Status == TranscriptStatus.Completed,
                // ADR-0044: the latest takes are being added; the lines already there stay as they are, open to correction.
                transcript.AppendFromMs is not null && transcript.Status is TranscriptStatus.Pending or TranscriptStatus.Processing));
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
                else if (transcript.Status is TranscriptStatus.Failed or TranscriptStatus.Completed)
                {
                    // Redoing a finished transcript replaces its segments, corrections and names when it completes.
                    if (transcript.Status == TranscriptStatus.Completed)
                        await audit.RecordAsync(AuditActions.TranscriptRerun, AuditActions.RecordingObjectType, recordingId,
                            actorId, new { recording.MeetingId, previousProvider = transcript.Provider }, token);
                    transcript.Status = TranscriptStatus.Pending;
                    transcript.AppendFromMs = null;
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

    public async Task<TranscriptOutcome> ExportAsync(Guid recordingId, Guid actorId, CancellationToken ct = default)
    {
        try
        {
            var (recording, meeting, failure) = await ReadableAsync(recordingId, actorId, ct);
            if (recording is null || meeting is null) return new(failure);
            switch (RecordingRules.Export(meeting, actorId))
            {
                case RecordingFailure.ExportForbidden: return new(TranscriptFailure.ExportForbidden);
                case RecordingFailure.SecretMeeting: return new(TranscriptFailure.SecretMeeting);
            }
            var transcript = await transcripts.FindByRecordingAsync(recordingId, false, ct);
            if (transcript is not { Status: TranscriptStatus.Completed }) return new(TranscriptFailure.NotReady);
            var names = transcript.Speakers.ToDictionary(s => s.Label, AssignedName);
            var now = clock.GetUtcNow();
            var text = TranscriptText.Render(meeting.Title, $"bản ghi {recording.FileName}", meeting.ActualStartedAt ?? recording.CreatedAt,
                now, transcript.Segments.OrderBy(s => s.Position).Select(s => new TranscriptLine(TranscriptText.Offset(s.StartMs),
                    names.GetValueOrDefault(s.SpeakerLabel) ?? SpeakerLabelName(s.SpeakerLabel), s.Text)));
            await audit.RecordAsync(AuditActions.TranscriptExported, AuditActions.RecordingObjectType, recordingId, actorId,
                new { recording.MeetingId, segments = transcript.Segments.Count }, ct);
            return new(Data: new ExportedText(TranscriptText.FileName(meeting.ActualStartedAt ?? recording.CreatedAt), text));
        }
        catch (Exception ex) when (PersistenceError(ex))
        {
            logger.LogError(ex, "Failed to export the transcript of {RecordingId}", recordingId);
            return new(TranscriptFailure.ReadFailed);
        }
    }

    private static string? AssignedName(TranscriptSpeaker speaker)
        => speaker.AssignedUser?.FullName
            ?? (speaker.AssignedGuest is { } guest ? guest.SelfDeclaredDisplayName ?? guest.InvitedName : null);

    /// <summary>"SPEAKER_00" reads as "Người nói 1", as on screen.</summary>
    private static string SpeakerLabelName(string label)
        => label.StartsWith("SPEAKER_", StringComparison.Ordinal) && int.TryParse(label[8..], out var n)
            ? $"Người nói {n + 1}" : label.Length > 0 ? label : "Người nói";

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
            if (segment.Text != trimmed)
                await audit.RecordAsync(AuditActions.TranscriptSegmentEdited, AuditActions.RecordingObjectType, recording.Id,
                    actorId, new { recording.MeetingId, segmentId, segment.Position }, ct);
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

    public async Task<TranscriptOutcome> AssignSpeakerAsync(Guid recordingId, string label, Guid? userId, Guid? guestId,
        Guid actorId, CancellationToken ct = default)
    {
        try
        {
            var (recording, meeting, failure) = await ReadableAsync(recordingId, actorId, ct);
            if (recording is null || meeting is null) return new(failure);
            if (!RecordingRules.IsSecretary(meeting, actorId)) return new(TranscriptFailure.Forbidden);
            var transcript = await transcripts.FindByRecordingAsync(recordingId, true, ct);
            var speaker = transcript?.Speakers.SingleOrDefault(s => s.Label == label);
            if (speaker is null) return new(TranscriptFailure.NotFound);
            if (userId is not null && guestId is not null) return new(TranscriptFailure.InvalidSpeaker);
            var person = userId is { } id ? Participants(meeting).FirstOrDefault(p => p.UserId == id) : null;
            if (userId is not null && person is null) return new(TranscriptFailure.InvalidSpeaker);
            var guest = guestId is { } g ? (await transcripts.ListGuestsAsync(meeting.Id, ct)).FirstOrDefault(p => p.Id == g) : null;
            if (guestId is not null && guest is null) return new(TranscriptFailure.InvalidSpeaker);
            var now = clock.GetUtcNow();
            speaker.AssignedUserId = userId;
            speaker.AssignedGuestId = guestId;
            speaker.AssignedById = actorId;
            speaker.AssignedAt = now;
            speaker.UpdatedAt = now;
            await audit.RecordAsync(AuditActions.TranscriptSpeakerAssigned, AuditActions.TranscriptSpeakerObjectType, speaker.Id,
                actorId, new { recordingId, label, userId, guestId, suggestedUserId = speaker.SuggestedUserId,
                    acceptedSuggestion = userId is not null && userId == speaker.SuggestedUserId }, ct);
            await transcripts.SaveChangesAsync(ct);
            return new(Data: new TranscriptSpeakerView(speaker.Label, speaker.SpeechMs, speaker.SuggestedUserId,
                speaker.SuggestedUser?.FullName, speaker.SuggestionScore, userId, person?.User.FullName ?? guest?.FullName,
                AssignedGuestId: guestId));
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
        var from = recording.Takes is not null ? transcript.AppendFromMs : null;
        Append? append = null;
        string? audioPath = null;
        SpeechTranscription result;
        try
        {
            if (recording.Status != RecordingStatus.Available) throw new SpeechEngineException("Bản ghi đã bị xoá.");
            // ADR-0044: only the room-mic takes not transcribed yet, after a sample of each voice already there, so the
            // worker tells whether someone heard before speaks again.
            if (from is { } fromMs)
            {
                var samples = TranscriptRules.PickSamples(await transcripts.ListLinesAsync(transcriptId, ct), fromMs);
                var bytesPerMs = RecordingRules.RoomBytesPerSecond / 1000;
                var placed = new List<(string Label, long StartMs, long EndMs)>();
                var at = 0L;
                foreach (var (label, startMs, endMs) in samples)
                {
                    placed.Add((label, at, at + endMs - startMs));
                    at += endMs - startMs + TranscriptRules.SampleGapMs;
                }
                audioPath = await storage.PieceAsync(recording.StoragePath, RecordingRules.WavHeaderBytes,
                    [.. samples.Select(p => (p.StartMs * bytesPerMs, (p.EndMs - p.StartMs) * bytesPerMs)),
                        (fromMs * bytesPerMs, long.MaxValue)],
                    TranscriptRules.SampleGapMs * bytesPerMs, ct);
                storage.SealWav(audioPath);
                append = new(at, placed);
            }
            await using var audio = storage.OpenRead(audioPath ?? recording.StoragePath);
            result = await engine.TranscribeAsync(audio, recording.ContentType, diarize: true, clip: false, ct);
        }
        catch (Exception ex) when (ex is SpeechEngineException or FileNotFoundException or DirectoryNotFoundException)
        {
            var message = ex is SpeechEngineException speech ? speech.UserMessage : "Tệp âm thanh không còn trên máy chủ.";
            logger.LogWarning(ex, "Transcription of recording {RecordingId} failed: {Reason}", recording.Id, message);
            await FinishAsync(transcriptId, null, message, from, null, ct);
            return true;
        }
        finally
        {
            if (audioPath is not null) DeleteQuietly(audioPath);
        }
        await FinishAsync(transcriptId, result, null, from, append, ct);
        return true;
    }

    /// <summary>
    /// ADR-0044 — a run over the latest takes: how long the samples of the voices already there play before them in the
    /// audio sent, and where each sample sits.
    /// </summary>
    private sealed record Append(long LeadMs, IReadOnlyList<(string Label, long StartMs, long EndMs)> Placed);

    private void DeleteQuietly(string path)
    {
        try { storage.Delete(path); }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            logger.LogError(ex, "Could not delete the audio cut out for transcription {Path}", path);
        }
    }

    /// <summary>
    /// Writes the result under the recording's row lock — the same lock erasure takes — so a recording erased while
    /// the worker was busy never gets its transcript written back afterwards.
    /// </summary>
    /// <param name="from">Where the latest takes start, for a run over them only (ADR-0044); null for the whole.</param>
    private async Task FinishAsync(Guid transcriptId, SpeechTranscription? result, string? error, long? from, Append? append,
        CancellationToken ct)
    {
        var meetingId = await meetings.InTransactionAsync(async token =>
        {
            var current = await transcripts.FindForProcessingAsync(transcriptId, token);
            if (current is null) return (Guid?)null;
            var recording = await recordings.FindAsync(current.RecordingId, true, token);
            if (recording is null || recording.Status != RecordingStatus.Available) return null;
            // A room-mic take was added or cut out while this ran: the result is of other audio, and the transcript
            // already waits to be made again.
            await transcripts.ReloadAsync(current, token);
            if (current.Status != TranscriptStatus.Processing || current.AppendFromMs != from) return null;
            var now = clock.GetUtcNow();
            current.UpdatedAt = now;
            if (result is null)
            {
                current.Status = TranscriptStatus.Failed;
                current.ErrorMessage = error ?? "Không chuyển được bản ghi thành chữ.";
            }
            else
            {
                if (from is { } fromMs && append is not null)
                    await AppendLinesAsync(transcriptId, recording, result, fromMs, append, now, token);
                else
                {
                    await transcripts.DeleteSegmentsAsync(transcriptId, token);
                    await transcripts.DeleteSpeakersAsync(transcriptId, token);
                    transcripts.AddSegments(result.Segments.Where(s => s.Text.Length > 0).Select((s, i) =>
                    {
                        var (startMs, endMs) = TranscriptRules.FitToTake(s.StartMs, s.EndMs, recording.Takes);
                        return Line(transcriptId, i + 1, startMs, endMs, s.Text, s.Speaker ?? "", now);
                    }));
                    var suggestions = await SuggestSpeakersAsync(recording.MeetingId, result, token);
                    transcripts.AddSpeakers(result.Speakers.Select(sp => new TranscriptSpeaker
                    {
                        TranscriptId = transcriptId, Label = sp.Label, SpeechMs = sp.SpeechMs,
                        SuggestedUserId = suggestions.GetValueOrDefault(sp.Label)?.UserId,
                        SuggestionScore = suggestions.GetValueOrDefault(sp.Label)?.Score, CreatedAt = now, UpdatedAt = now,
                    }));
                }
                current.AppendFromMs = null;
                current.Status = TranscriptStatus.Completed;
                current.Provider = TranscriptRules.Provider(result.AsrModel);
                current.Language = result.Language;
                current.ProgressPercent = 100;
                current.ErrorMessage = "";
                current.CompletedAt = now;
                // A room recording's length comes from its bytes, which its takes' places are counted in.
                if (result.DurationMs > 0 && recording.Takes is null) recording.DurationMs = result.DurationMs;
            }
            await transcripts.SaveChangesAsync(token);
            return (Guid?)recording.MeetingId;
        }, ct);
        if (meetingId is not { } id) return;
        try
        {
            using var timeout = CancellationTokenSource.CreateLinkedTokenSource(ct);
            timeout.CancelAfter(TimeSpan.FromSeconds(2));
            await publisher.PublishAsync(new(Guid.CreateVersion7(), id, RecordingRules.RecordingsChanged, clock.GetUtcNow()),
                timeout.Token);
        }
        catch (Exception ex) when (ex is not OperationCanceledException || !ct.IsCancellationRequested)
        {
            logger.LogWarning(ex, "Could not publish the finished transcript of {MeetingId}", id);
        }
    }

    private static TranscriptSegment Line(Guid transcriptId, int position, long startMs, long endMs, string text, string label,
        DateTimeOffset now) => new()
    {
        TranscriptId = transcriptId, Position = position, StartMs = startMs, EndMs = endMs,
        Text = text.Length > TranscriptRules.MaxSegmentText ? text[..TranscriptRules.MaxSegmentText] : text,
        SpeakerLabel = label, CreatedAt = now, UpdatedAt = now,
    };

    /// <summary>
    /// ADR-0044 — the lines of the latest takes go after the ones there, and nothing there is touched. What the worker heard
    /// in the samples is dropped. A voice it put with an earlier voice's samples keeps that voice's label, and so the name
    /// the Secretary gave it; any other voice is numbered after the last one there.
    /// </summary>
    private async Task AppendLinesAsync(Guid transcriptId, Recording recording, SpeechTranscription result, long fromMs,
        Append append, DateTimeOffset now, CancellationToken ct)
    {
        var same = TranscriptRules.SameVoices(result.Segments, append.Placed);
        var (lastPosition, nextVoice) = await transcripts.CountLinesAsync(transcriptId, ct);
        var labels = new Dictionary<string, string>();
        var segments = new List<TranscriptSegment>();
        // A line's end is where its words stopped; one reaching back into the samples starts where the takes do.
        foreach (var s in result.Segments.Where(s => s.EndMs > append.LeadMs && s.Text.Length > 0))
        {
            var voice = s.Speaker ?? "";
            if (!labels.TryGetValue(voice, out var label))
                labels[voice] = label = same.GetValueOrDefault(voice)
                    ?? (TranscriptRules.VoiceNumber(voice) is null ? voice : $"SPEAKER_{nextVoice++:D2}");
            var (startMs, endMs) = TranscriptRules.FitToTake(Math.Max(s.StartMs, append.LeadMs) - append.LeadMs + fromMs,
                s.EndMs - append.LeadMs + fromMs, recording.Takes);
            segments.Add(Line(transcriptId, lastPosition + segments.Count + 1, startMs, endMs, s.Text, label, now));
        }
        transcripts.AddSegments(segments);
        var suggestions = await SuggestSpeakersAsync(recording.MeetingId, result, ct);
        var speakers = (await transcripts.ListSpeakersAsync(transcriptId, ct)).ToDictionary(s => s.Label);
        foreach (var (voice, label) in labels.Where(l => l.Value.Length > 0))
        {
            // The worker's count of a voice's speech takes in the samples: what was kept of it is counted instead.
            var speechMs = segments.Where(s => s.SpeakerLabel == label).Sum(s => s.EndMs - s.StartMs);
            var suggestion = suggestions.GetValueOrDefault(voice);
            if (speakers.TryGetValue(label, out var speaker))
            {
                speaker.SpeechMs += speechMs;
                if (speaker.SuggestedUserId is null && suggestion is not null)
                    (speaker.SuggestedUserId, speaker.SuggestionScore) = (suggestion.UserId, suggestion.Score);
                speaker.UpdatedAt = now;
            }
            else transcripts.AddSpeakers([new TranscriptSpeaker
            {
                TranscriptId = transcriptId, Label = label, SpeechMs = speechMs, SuggestedUserId = suggestion?.UserId,
                SuggestionScore = suggestion?.Score, CreatedAt = now, UpdatedAt = now,
            }]);
        }
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
        return SpeakerMatcher.Suggest(result.Speakers, candidates, options.SuggestThreshold, options.SuggestMargin,
            options.SameVoiceThreshold);
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
