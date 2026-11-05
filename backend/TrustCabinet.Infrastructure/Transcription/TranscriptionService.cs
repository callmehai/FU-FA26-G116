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
                    s.StartMs, s.EndMs, s.Text, s.SpeakerLabel, s.EditedAt, s.EditedBy?.FullName, s.SpeakerUserId,
                    s.SpeakerGuestId, LineName(s)))],
                [.. transcript.Speakers.OrderBy(s => s.Label).Select(s => new TranscriptSpeakerView(s.Label, s.SpeechMs,
                    canEdit ? s.SuggestedUserId : null, canEdit ? s.SuggestedUser?.FullName : null,
                    canEdit ? s.SuggestionScore : null, s.AssignedUserId, AssignedName(s), sameAs.GetValueOrDefault(s.Label),
                    s.AssignedGuestId, AutoAssigned(s)))],
                people, canExport && transcript.Status == TranscriptStatus.Completed));
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
                    LineName(s) ?? names.GetValueOrDefault(s.SpeakerLabel) ?? SpeakerLabelName(s.SpeakerLabel), s.Text)));
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

    /// <summary>Who said this one line when the Secretary named it apart from its voice; null otherwise.</summary>
    private static string? LineName(TranscriptSegment segment)
        => segment.SpeakerUser?.FullName
            ?? (segment.SpeakerGuest is { } guest ? guest.SelfDeclaredDisplayName ?? guest.InvitedName : null);

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

    public async Task<TranscriptOutcome> AssignSegmentSpeakerAsync(Guid segmentId, Guid? userId, Guid? guestId, Guid actorId,
        CancellationToken ct = default)
    {
        try
        {
            var segment = await transcripts.FindSegmentAsync(segmentId, ct);
            if (segment is null) return new(TranscriptFailure.NotFound);
            var (recording, meeting, failure) = await ReadableAsync(segment.Transcript.RecordingId, actorId, ct);
            if (recording is null || meeting is null) return new(failure);
            if (!RecordingRules.IsSecretary(meeting, actorId)) return new(TranscriptFailure.Forbidden);
            if (userId is not null && guestId is not null) return new(TranscriptFailure.InvalidSpeaker);
            var person = userId is { } id ? Participants(meeting).FirstOrDefault(p => p.UserId == id) : null;
            if (userId is not null && person is null) return new(TranscriptFailure.InvalidSpeaker);
            var guest = guestId is { } g ? (await transcripts.ListGuestsAsync(meeting.Id, ct)).FirstOrDefault(p => p.Id == g) : null;
            if (guestId is not null && guest is null) return new(TranscriptFailure.InvalidSpeaker);
            var now = clock.GetUtcNow();
            segment.SpeakerUserId = userId;
            segment.SpeakerGuestId = guestId;
            segment.UpdatedAt = now;
            await audit.RecordAsync(AuditActions.TranscriptSegmentSpeakerAssigned, AuditActions.RecordingObjectType, recording.Id,
                actorId, new { recording.MeetingId, segmentId, segment.Position, segment.SpeakerLabel, userId, guestId }, ct);
            await transcripts.SaveChangesAsync(ct);
            return new(Data: new { segmentId, speakerUserId = userId, speakerGuestId = guestId,
                speakerName = person?.User.FullName ?? guest?.FullName });
        }
        catch (Exception ex) when (PersistenceError(ex))
        {
            logger.LogError(ex, "Failed to name the speaker of transcript segment {SegmentId}", segmentId);
            return new(TranscriptFailure.SaveFailed);
        }
    }

    public Task<int> RequeueInterruptedAsync(CancellationToken ct = default)
        => transcripts.RequeueProcessingAsync(clock.GetUtcNow(), ct);

    /// <summary>
    /// What the Secretary reads when the run failed for a reason they cannot act on (the worker dropped the
    /// connection, its answer was unreadable, the database failed while writing): the details are in the log.
    /// </summary>
    internal const string UnexpectedFailureMessage = "Không chuyển được bản ghi thành chữ do lỗi hệ thống. Hãy chạy lại.";

    public async Task<bool> ProcessNextAsync(CancellationToken ct = default)
    {
        if (await transcripts.ClaimNextAsync(clock.GetUtcNow(), ct) is not { } transcriptId) return false;
        try
        {
            await TranscribeAsync(transcriptId, ct);
        }
        catch (Exception ex) when (ex is not OperationCanceledException || !ct.IsCancellationRequested)
        {
            // Anything else left the transcript Processing, which the Secretary cannot rerun and only a restart
            // requeued. Our own shutdown is the one exception that leaves it so: the next start picks it up again.
            logger.LogError(ex, "Transcription of transcript {TranscriptId} failed unexpectedly", transcriptId);
            // Whatever the failed write had staged must not be saved with the failure.
            transcripts.DiscardChanges();
            await FinishAsync(transcriptId, null, UnexpectedFailureMessage, ct);
        }
        return true;
    }

    private async Task TranscribeAsync(Guid transcriptId, CancellationToken ct)
    {
        var transcript = await transcripts.FindForProcessingAsync(transcriptId, ct);
        if (transcript is null) return;
        var recording = transcript.Recording;
        SpeechTranscription result;
        try
        {
            if (recording.Status != RecordingStatus.Available) throw new SpeechEngineException("Bản ghi đã bị xoá.");
            await using var audio = storage.OpenRead(recording.StoragePath);
            result = await engine.TranscribeAsync(audio, recording.ContentType, diarize: true, clip: false, ct);
        }
        catch (Exception ex) when (ex is SpeechEngineException or FileNotFoundException or DirectoryNotFoundException)
        {
            var message = ex is SpeechEngineException speech ? speech.UserMessage : "Tệp âm thanh không còn trên máy chủ.";
            logger.LogWarning(ex, "Transcription of recording {RecordingId} failed: {Reason}", recording.Id, message);
            await FinishAsync(transcriptId, null, message, ct);
            return;
        }
        await FinishAsync(transcriptId, result, null, ct);
    }

    /// <summary>
    /// Writes the result under the recording's row lock — the same lock erasure takes — so a recording erased while
    /// the worker was busy never gets its transcript written back afterwards. The voices are matched in the same
    /// transaction, under a share lock on the consents they match against, so no suggestion or machine-given name is
    /// written for someone whose Voice ID ended meanwhile.
    /// </summary>
    private async Task FinishAsync(Guid transcriptId, SpeechTranscription? result, string? error, CancellationToken ct)
    {
        var meetingId = await meetings.InTransactionAsync(async token =>
        {
            var current = await transcripts.FindForProcessingAsync(transcriptId, token);
            if (current is null) return (Guid?)null;
            var recording = await recordings.FindAsync(current.RecordingId, true, token);
            if (recording is null || recording.Status != RecordingStatus.Available) return null;
            // A room-mic take was appended while this ran (ADR-0041): the result is of a shorter file, and the
            // transcript already waits to be made again from the whole.
            await transcripts.ReloadAsync(current, token);
            if (current.Status != TranscriptStatus.Processing) return null;
            var now = clock.GetUtcNow();
            current.UpdatedAt = now;
            if (result is null)
            {
                current.Status = TranscriptStatus.Failed;
                current.ErrorMessage = error ?? "Không chuyển được bản ghi thành chữ.";
            }
            else
            {
                // Matched before anything is deleted: it locks the consents it reads, and a withdrawal holding one of
                // them may be waiting on the speaker rows deleted below.
                var (suggestions, nameable) = await SuggestSpeakersAsync(recording.MeetingId, result, token);
                await transcripts.DeleteSegmentsAsync(transcriptId, token);
                transcripts.AddSegments(result.Segments.Where(s => s.Text.Length > 0).Select((s, i) =>
                {
                    var (startMs, endMs) = TranscriptRules.FitToTake(s.StartMs, s.EndMs, recording.Takes);
                    return new TranscriptSegment
                    {
                        TranscriptId = transcriptId, Position = i + 1, StartMs = startMs, EndMs = endMs,
                        Text = s.Text.Length > TranscriptRules.MaxSegmentText ? s.Text[..TranscriptRules.MaxSegmentText] : s.Text,
                        SpeakerLabel = s.Speaker ?? "", CreatedAt = now, UpdatedAt = now,
                    };
                }));
                await transcripts.DeleteSpeakersAsync(transcriptId, token);
                // A sure match names the voice at once (PO 01/11/2026): no assigner marks the machine's name, which
                // the Secretary may still change, and which goes when that person withdraws Voice ID consent. Only
                // for someone who agreed to that (notice 1.2, ADR-0048); the others are suggested, as they agreed.
                var sure = voiceOptions.Value.AutoAssignThreshold;
                var speakers = result.Speakers.Select(sp =>
                {
                    var guess = suggestions.GetValueOrDefault(sp.Label);
                    var named = guess is not null && guess.Score >= sure && nameable.Contains(guess.UserId);
                    return new TranscriptSpeaker
                    {
                        TranscriptId = transcriptId, Label = sp.Label, SpeechMs = sp.SpeechMs,
                        SuggestedUserId = guess?.UserId, SuggestionScore = guess?.Score,
                        AssignedUserId = named ? guess!.UserId : null, AssignedAt = named ? now : null,
                        CreatedAt = now, UpdatedAt = now,
                    };
                }).ToList();
                transcripts.AddSpeakers(speakers);
                foreach (var named in speakers.Where(s => s.AssignedUserId is not null))
                    await audit.RecordAsync(AuditActions.TranscriptSpeakerAssigned, AuditActions.TranscriptSpeakerObjectType,
                        named.Id, null, new { recordingId = recording.Id, label = named.Label, userId = named.AssignedUserId,
                            automatic = true, score = named.SuggestionScore }, token);
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

    /// <summary>
    /// SCRUM-35 — compares each voice with the enrolled samples of the meeting's own people who consented. The voices'
    /// embeddings are used here and dropped; only the resulting suggestion is stored. Also says who of them agreed to be
    /// named by the machine.
    /// </summary>
    private async Task<(IReadOnlyDictionary<string, SpeakerMatcher.Suggestion> Suggestions, IReadOnlySet<Guid> Nameable)>
        SuggestSpeakersAsync(Guid meetingId, SpeechTranscription result, CancellationToken ct)
    {
        var none = (new Dictionary<string, SpeakerMatcher.Suggestion>(), new HashSet<Guid>());
        if (result.Speakers.Count == 0 || result.EmbeddingModel.Length == 0) return none;
        var meeting = await meetings.FindWorkflowAsync(meetingId, false, ct);
        if (meeting is null) return none;
        var people = Participants(meeting).Select(p => p.UserId).Distinct().ToArray();
        // Held until the result is written: a withdrawal or a deactivation waits for it, then clears what was written
        // here; one that came first has erased the samples before they are read below.
        await voices.LockConsentsAsync(people, ct);
        var candidates = new List<SpeakerMatcher.Candidate>();
        var nameable = new HashSet<Guid>();
        foreach (var sample in await voices.ListMatchableSamplesAsync(people, result.EmbeddingModel, ct))
        {
            if (string.CompareOrdinal(sample.Consent.NoticeVersion, VoiceIdRules.AutoAssignNoticeVersion) >= 0)
                nameable.Add(sample.UserId);
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
        return (SpeakerMatcher.Suggest(result.Speakers, candidates, options.SuggestThreshold, options.SuggestMargin,
            options.SameVoiceThreshold, options.SecondVoiceThreshold, options.InheritVoiceThreshold), nameable);
    }

    private static bool AutoAssigned(TranscriptSpeaker s) => s.AssignedUserId is not null && s.AssignedById is null;

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
