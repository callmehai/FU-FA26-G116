using System.Security.Cryptography;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using TrustCabinet.Application.Abstractions;
using TrustCabinet.Application.Audit;
using TrustCabinet.Application.Meetings;
using TrustCabinet.Application.Recordings;
using TrustCabinet.Application.VoiceId;
using TrustCabinet.Domain.Audit;
using TrustCabinet.Domain.Entities;
using TrustCabinet.Domain.Enums;
using TrustCabinet.Domain.Participants;
using TrustCabinet.Domain.Transcription;
using TrustCabinet.Domain.VoiceId;

namespace TrustCabinet.Application.Transcription;

/// <summary>
/// SCRUM-31 — speech to text for a recording, run in the background by <c>TranscriptionWorker</c>. The audio is
/// sent to the in-house speech worker only; the transcript is the same personal data as the audio and is erased
/// with it (RecordingService).
/// </summary>
public sealed class TranscriptionService(IMeetingRepository meetings, IRecordingRepository recordings,
    ITranscriptRepository transcripts, IUnitOfWork unitOfWork, IRecordingStorage storage, ISpeechEngine engine, IVoiceIdRepository voices,
    IVoiceprintProtector protector, IOptions<VoiceIdOptions> voiceOptions, IAuditService audit,
    ISpeechChangePublisher publisher, TimeProvider clock, ILogger<TranscriptionService> logger) : ITranscriptionService
{
    public async Task<TranscriptOutcome> GetAsync(Guid recordingId, Guid actorId, CancellationToken ct = default)
    {
        try
        {
            return await ReadAsync(async token =>
            {
                var (recording, meeting, failure) = await ReadableAsync(recordingId, actorId, token);
                if (recording is null || meeting is null) return new TranscriptOutcome(failure);
                var canEdit = RecordingRules.IsSecretary(meeting, actorId);
                var canExport = RecordingRules.Export(meeting, actorId) == RecordingFailure.None;
                var transcript = await transcripts.FindByRecordingAsync(recordingId, false, token);
                // Who the secretary may name a voice as: the meeting's own people and its guests, offered only to the secretary.
                TranscriptPerson[] people = canEdit
                    ? [.. Participants(meeting).Select(p => new TranscriptPerson(p.UserId, p.User.FullName)).DistinctBy(p => p.Id)
                        .Concat(await transcripts.ListGuestsAsync(meeting.Id, token)).OrderBy(p => p.FullName)]
                    : [];
                if (transcript is null)
                    return new(Data: new TranscriptView(recordingId, "None", "", "vi", "", null, recording.DurationMs, canEdit, [], [], people));
                // A voice is listed while a line is said in it, as AI-eCabinet lists the voices of its lines: one whose every
                // line the Secretary moved to another voice goes, its name kept should a line move back to that person.
                var spoken = transcript.Segments.Select(s => s.SpeakerLabel).ToHashSet();
                var voices = transcript.Speakers.Where(s => spoken.Contains(s.Label)).OrderBy(s => s.Label).ToList();
                // The machine's guess at a voice is a hint for the Secretary who names it, and a guess about someone's
                // biometric identity: nobody else gets it, only the name the Secretary assigned.
                var sameAs = canEdit
                    ? SpeakerMatcher.SameAs([.. voices.Select(s => (s.Label, s.SuggestedUserId, s.SuggestionScore, s.AssignedUserId))])
                    : new Dictionary<string, string>();
                return new(Data: new TranscriptView(recordingId, transcript.Status.ToString(), transcript.Provider,
                    transcript.Language, transcript.ErrorMessage, transcript.CompletedAt, recording.DurationMs, canEdit,
                    [.. transcript.Segments.OrderBy(s => s.Position).Select(s => new TranscriptSegmentView(s.Id, s.Position,
                        s.StartMs, s.EndMs, s.Text, s.SpeakerLabel, s.EditedAt, s.EditedBy?.FullName,
                        WordTimings.ForLine(s.Text, s.WordStartsMs, s.EditedAt is not null, s.StartMs, s.EndMs),
                        WordScores.ForLine(s.Text, s.WordConfidence, s.EditedAt is not null)))],
                    [.. voices.Select(s => new TranscriptSpeakerView(s.Label, s.SpeechMs,
                        canEdit ? s.SuggestedUserId : null, canEdit ? s.SuggestedUser?.FullName : null,
                        canEdit ? s.SuggestionScore : null, s.AssignedUserId, AssignedName(s), sameAs.GetValueOrDefault(s.Label),
                        s.AssignedGuestId, AutoAssigned(s)))],
                    people, canExport && transcript.Status == TranscriptStatus.Completed));
            }, ct);
        }
        catch (PersistenceException ex)
        {
            logger.LogError(ex.InnerException ?? ex, "Failed to read transcript of {RecordingId}", recordingId);
            return new(TranscriptFailure.ReadFailed);
        }
    }

    public async Task<TranscriptOutcome> RetryAsync(Guid recordingId, Guid actorId, CancellationToken ct = default)
    {
        try
        {
            return await unitOfWork.ExecuteAsync(async token =>
            {
                var (recording, meeting, failure) = await ReadableAsync(recordingId, actorId, token, forUpdate: true);
                if (recording is null || meeting is null) return new TranscriptOutcome(failure);
                if (!RecordingRules.IsSecretary(meeting, actorId)) return new(TranscriptFailure.Forbidden);
                if (recording.Source == RecordingSource.Combined) return new(TranscriptFailure.Combined);
                var transcript = await transcripts.FindByRecordingAsync(recordingId, true, token);
                var now = clock.GetUtcNow();
                if (transcript is null)
                    // Explicitly added: a new entity hung off a tracked one would be taken for an existing row (its key is set).
                    transcripts.Add(new Transcript { RecordingId = recordingId, CreatedAt = now, UpdatedAt = now });
                else if (transcript.Status is TranscriptStatus.Failed or TranscriptStatus.Completed)
                {
                    // Redoing a finished transcript replaces its segments when it completes; what the Secretary corrected
                    // by hand is carried onto the new ones (ADR-0077).
                    if (transcript.Status == TranscriptStatus.Completed)
                        await audit.RecordAsync(AuditActions.TranscriptRerun, AuditActions.RecordingObjectType, recordingId,
                            actorId, new { recording.MeetingId, previousProvider = transcript.Provider }, token);
                    transcript.Status = TranscriptStatus.Pending;
                    transcript.ErrorMessage = "";
                    transcript.ProgressPercent = 0;
                    transcript.UpdatedAt = now;
                }
                else return new(TranscriptFailure.NotRetryable);
                await unitOfWork.SaveChangesAsync(token);
                return new TranscriptOutcome();
            }, TransactionMode.ReadCommitted, ct);
        }
        catch (PersistenceException ex)
        {
            logger.LogError(ex.InnerException ?? ex, "Failed to queue transcription of {RecordingId}", recordingId);
            return new(TranscriptFailure.SaveFailed);
        }
    }

    public async Task<TranscriptOutcome> ExportAsync(Guid recordingId, Guid actorId, CancellationToken ct = default)
    {
        try
        {
            return await ReadAsync(async token =>
            {
                var (recording, meeting, failure) = await ReadableAsync(recordingId, actorId, token);
                if (recording is null || meeting is null) return new TranscriptOutcome(failure);
                switch (RecordingRules.Export(meeting, actorId))
                {
                    case RecordingFailure.ExportForbidden: return new(TranscriptFailure.ExportForbidden);
                    case RecordingFailure.SecretMeeting: return new(TranscriptFailure.SecretMeeting);
                }
                var transcript = await transcripts.FindByRecordingAsync(recordingId, false, token);
                if (transcript is not { Status: TranscriptStatus.Completed }) return new(TranscriptFailure.NotReady);
                var names = transcript.Speakers.ToDictionary(s => s.Label, AssignedName);
                var now = clock.GetUtcNow();
                var text = TranscriptText.Render(meeting.Title, $"bản ghi {recording.FileName}", meeting.ActualStartedAt ?? recording.CreatedAt,
                    now, transcript.Segments.OrderBy(s => s.Position).Select(s => new TranscriptLine(
                        TranscriptText.LineTime(s.StartMs, recording.Takes),
                        names.GetValueOrDefault(s.SpeakerLabel) ?? SpeakerLabelName(s.SpeakerLabel), s.Text)));
                await audit.RecordAsync(AuditActions.TranscriptExported, AuditActions.RecordingObjectType, recordingId, actorId,
                    new { recording.MeetingId, segments = transcript.Segments.Count }, token);
                return new(Data: new ExportedText(TranscriptText.FileName(meeting.ActualStartedAt ?? recording.CreatedAt), text));
            }, ct);
        }
        catch (PersistenceException ex)
        {
            logger.LogError(ex.InnerException ?? ex, "Failed to export the transcript of {RecordingId}", recordingId);
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
        Guid? meetingId = null;
        TranscriptOutcome outcome;
        try
        {
            outcome = await ReadAsync(async token =>
            {
                var segment = await transcripts.FindSegmentAsync(segmentId, token);
                if (segment is null) return new TranscriptOutcome(TranscriptFailure.NotFound);
                var (recording, meeting, failure) = await ReadableAsync(segment.Transcript.RecordingId, actorId, token);
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
                        actorId, new { recording.MeetingId, segmentId, segment.Position }, token);
                // The machine's word times belonged to the words it wrote, not to the corrected ones.
                if (segment.Text != trimmed) (segment.WordStartsMs, segment.WordConfidence) = (null, null);
                segment.Text = trimmed;
                segment.EditedById = actorId;
                segment.EditedAt = now;
                segment.UpdatedAt = now;
                await unitOfWork.SaveChangesAsync(token);
                meetingId = recording.MeetingId;
                return new(Data: new { segmentId, text = trimmed, editedAt = now });
            }, ct);
        }
        catch (PersistenceException ex)
        {
            logger.LogError(ex.InnerException ?? ex, "Failed to edit transcript segment {SegmentId}", segmentId);
            return new(TranscriptFailure.SaveFailed);
        }
        await AnnounceCorrectionAsync(outcome, meetingId, ct);
        return outcome;
    }

    public async Task<TranscriptOutcome> AssignSpeakerAsync(Guid recordingId, string label, Guid? userId, Guid? guestId,
        Guid actorId, CancellationToken ct = default)
    {
        Guid? meetingId = null;
        TranscriptOutcome outcome;
        try
        {
            // Under the recording's lock, as a line moved to a person: naming a voice and giving that person a new voice
            // at the same moment would otherwise leave two voices with one name.
            outcome = await unitOfWork.ExecuteAsync(async token =>
            {
                var (recording, meeting, failure) = await ReadableAsync(recordingId, actorId, token, forUpdate: true);
                if (recording is null || meeting is null) return new TranscriptOutcome(failure);
                if (!RecordingRules.IsSecretary(meeting, actorId)) return new(TranscriptFailure.Forbidden);
                var transcript = await transcripts.FindByRecordingAsync(recordingId, true, token);
                var speaker = transcript?.Speakers.SingleOrDefault(s => s.Label == label);
                if (speaker is null) return new(TranscriptFailure.NotFound);
                if (userId is not null && guestId is not null) return new(TranscriptFailure.InvalidSpeaker);
                var person = userId is { } id ? Participants(meeting).FirstOrDefault(p => p.UserId == id) : null;
                if (userId is not null && person is null) return new(TranscriptFailure.InvalidSpeaker);
                var guest = guestId is { } g ? (await transcripts.ListGuestsAsync(meeting.Id, token)).FirstOrDefault(p => p.Id == g) : null;
                if (guestId is not null && guest is null) return new(TranscriptFailure.InvalidSpeaker);
                var now = clock.GetUtcNow();
                speaker.AssignedUserId = userId;
                speaker.AssignedGuestId = guestId;
                speaker.AssignedById = actorId;
                speaker.AssignedAt = now;
                speaker.UpdatedAt = now;
                await audit.RecordAsync(AuditActions.TranscriptSpeakerAssigned, AuditActions.TranscriptSpeakerObjectType, speaker.Id,
                    actorId, new { recordingId, label, userId, guestId, suggestedUserId = speaker.SuggestedUserId,
                        acceptedSuggestion = userId is not null && userId == speaker.SuggestedUserId }, token);
                await unitOfWork.SaveChangesAsync(token);
                meetingId = meeting.Id;
                return new(Data: new TranscriptSpeakerView(speaker.Label, speaker.SpeechMs, speaker.SuggestedUserId,
                    speaker.SuggestedUser?.FullName, speaker.SuggestionScore, userId, person?.User.FullName ?? guest?.FullName,
                    AssignedGuestId: guestId));
            }, TransactionMode.ReadCommitted, ct);
        }
        catch (PersistenceException ex)
        {
            logger.LogError(ex.InnerException ?? ex, "Failed to name speaker {Label} of {RecordingId}", label, recordingId);
            return new(TranscriptFailure.SaveFailed);
        }
        await AnnounceCorrectionAsync(outcome, meetingId, ct);
        return outcome;
    }

    public async Task<TranscriptOutcome> AssignSegmentSpeakerAsync(Guid segmentId, Guid? userId, Guid? guestId, string? label,
        Guid actorId, CancellationToken ct = default)
    {
        Guid? meetingId = null;
        TranscriptOutcome outcome;
        try
        {
            // Only the recording's id before the lock: a line read now and tracked would be handed back unchanged by the
            // read after it, so a move that waited on another one of the same line would undo the wrong voice's time.
            if (await ReadAsync(token => transcripts.FindSegmentRecordingAsync(segmentId, token), ct) is not { } recordingId)
                return new(TranscriptFailure.NotFound);
            outcome = await unitOfWork.ExecuteAsync(async token =>
            {
                // The recording's row is locked: moves on one recording run one after another, each reading what the one
                // before it wrote, and two of them never take the same free label.
                var (recording, meeting, failure) = await ReadableAsync(recordingId, actorId, token, forUpdate: true);
                if (recording is null || meeting is null) return new TranscriptOutcome(failure);
                if (!RecordingRules.IsSecretary(meeting, actorId)) return new(TranscriptFailure.Forbidden);
                label = string.IsNullOrWhiteSpace(label) ? null : label;
                // A line is said by one voice, user or guest.
                if (new object?[] { userId, guestId, label }.Count(x => x is not null) != 1) return new(TranscriptFailure.InvalidSpeaker);
                var transcript = await transcripts.FindByRecordingAsync(recordingId, true, token);
                var segment = transcript?.Segments.SingleOrDefault(s => s.Id == segmentId);
                if (transcript is null || segment is null) return new(TranscriptFailure.NotFound);
                var now = clock.GetUtcNow();
                TranscriptSpeaker? created = null;
                string target;
                if (label is not null)
                {
                    // Back to a voice of this recording, e.g. the one a line was wrongly taken from; another
                    // recording's label is not one, as a voice the voice endpoint does not know.
                    if (!transcript.Speakers.Any(s => s.Label == label) && !transcript.Segments.Any(s => s.SpeakerLabel == label))
                        return new(TranscriptFailure.NotFound);
                    target = label;
                }
                else
                {
                    var person = userId is { } id ? Participants(meeting).FirstOrDefault(p => p.UserId == id) : null;
                    if (userId is not null && person is null) return new(TranscriptFailure.InvalidSpeaker);
                    var guest = guestId is { } g ? (await transcripts.ListGuestsAsync(meeting.Id, token)).FirstOrDefault(p => p.Id == g) : null;
                    if (guestId is not null && guest is null) return new(TranscriptFailure.InvalidSpeaker);
                    bool Named(TranscriptSpeaker s) => userId is not null ? s.AssignedUserId == userId : s.AssignedGuestId == guestId;
                    // The person's voice: the line's own when it is already theirs, else the first one named as them,
                    // else a new voice carrying their name, as AI-eCabinet does — so they get a card, a colour and a time.
                    var theirs = transcript.Speakers.FirstOrDefault(s => s.Label == segment.SpeakerLabel && Named(s))
                        ?? transcript.Speakers.Where(Named).OrderBy(s => s.Label).FirstOrDefault();
                    if (theirs is null)
                    {
                        var free = FreeLabel(transcript);
                        if (free is null) return new(TranscriptFailure.InvalidSpeaker);
                        // No embedding and no suggestion: the voice exists because the Secretary said who spoke.
                        created = new TranscriptSpeaker
                        {
                            TranscriptId = transcript.Id, Label = free, AssignedUserId = userId, AssignedGuestId = guestId,
                            AssignedById = actorId, AssignedAt = now, CreatedAt = now, UpdatedAt = now,
                        };
                        // Explicitly added: a new entity hung off a tracked one would be taken for an existing row.
                        transcripts.AddSpeakers([created]);
                    }
                    target = theirs?.Label ?? created!.Label;
                }
                var from = segment.SpeakerLabel;
                // A change that changes nothing writes nothing.
                if (target == from) return new(Data: new { segmentId, speakerLabel = target, previousLabel = from, newVoice = false });
                segment.SpeakerLabel = target;
                // Marked as the Secretary's, so the next run of the recording moves it again (ADR-0077).
                (segment.SpeakerAssignedById, segment.SpeakerAssignedAt) = (actorId, now);
                segment.UpdatedAt = now;
                // The line's length moves with it. Not summed again from the lines: the diarizer measured each voice's
                // speech its own way, and a voice taking a line must never read as having spoken less. Moving the line
                // back gives each voice its time back.
                var length = Math.Max(0, segment.EndMs - segment.StartMs);
                foreach (var voice in transcript.Speakers.Where(s => s.Label == from || s.Label == target).Append(created).Distinct())
                {
                    if (voice is null) continue;
                    voice.SpeechMs = voice.Label == target ? voice.SpeechMs + length : Math.Max(0, voice.SpeechMs - length);
                    voice.UpdatedAt = now;
                }
                await audit.RecordAsync(AuditActions.TranscriptSegmentSpeakerAssigned, AuditActions.RecordingObjectType, recording.Id,
                    actorId, new { recording.MeetingId, segmentId, segment.Position, previousLabel = from, speakerLabel = target,
                        userId, guestId, newVoice = created is not null }, token);
                await unitOfWork.SaveChangesAsync(token);
                meetingId = meeting.Id;
                return new(Data: new { segmentId, speakerLabel = target, previousLabel = from, newVoice = created is not null });
            }, TransactionMode.ReadCommitted, ct);
        }
        catch (PersistenceException ex)
        {
            logger.LogError(ex.InnerException ?? ex, "Failed to move transcript segment {SegmentId} to another voice", segmentId);
            return new(TranscriptFailure.SaveFailed);
        }
        await AnnounceCorrectionAsync(outcome, meetingId, ct);
        return outcome;
    }

    /// <summary>
    /// Tells the meeting a transcript was corrected, once it is committed, so every open Bản ghi tab re-reads it (team
    /// test 21/11/2026: a name the Secretary gave a voice reached nobody else before a reload). Only a change that wrote
    /// something: a failure, or a move that changed nothing, leaves <paramref name="meetingId"/> unset.
    /// </summary>
    private async Task AnnounceCorrectionAsync(TranscriptOutcome outcome, Guid? meetingId, CancellationToken ct)
    {
        if (outcome.Failure != TranscriptFailure.None || meetingId is not { } id) return;
        try
        {
            using var timeout = CancellationTokenSource.CreateLinkedTokenSource(ct);
            timeout.CancelAfter(TimeSpan.FromSeconds(2));
            await publisher.PublishAsync(new(Guid.CreateVersion7(), id, RecordingRules.TranscriptChanged, clock.GetUtcNow()),
                timeout.Token);
        }
        catch (Exception ex) when (ex is not OperationCanceledException || !ct.IsCancellationRequested)
        {
            logger.LogWarning(ex, "Could not publish the corrected transcript of {MeetingId}", id);
        }
    }

    /// <summary>The lowest "SPEAKER_NN" no line or voice of the transcript uses; null when all hundred are taken.</summary>
    private static string? FreeLabel(Transcript transcript)
    {
        var taken = transcript.Segments.Select(s => s.SpeakerLabel).Concat(transcript.Speakers.Select(s => s.Label)).ToHashSet();
        return Enumerable.Range(0, 100).Select(i => $"SPEAKER_{i:00}").FirstOrDefault(l => !taken.Contains(l));
    }

    public Task<int> RequeueInterruptedAsync(CancellationToken ct = default)
        => ReadAsync(token => transcripts.RequeueProcessingAsync(clock.GetUtcNow(), token), ct);

    /// <summary>
    /// What the Secretary reads when the run failed for a reason they cannot act on (the worker dropped the
    /// connection, its answer was unreadable, the database failed while writing): the details are in the log.
    /// </summary>
    internal const string UnexpectedFailureMessage = "Không chuyển được bản ghi thành chữ do lỗi hệ thống. Hãy chạy lại.";

    public async Task<bool> ProcessNextAsync(CancellationToken ct = default)
    {
        if (await ReadAsync(token => transcripts.ClaimNextAsync(clock.GetUtcNow(), token), ct) is not { } transcriptId) return false;
        try
        {
            await TranscribeAsync(transcriptId, ct);
        }
        catch (Exception ex) when (ex is not OperationCanceledException || !ct.IsCancellationRequested)
        {
            // Anything else left the transcript Processing, which the Secretary cannot rerun and only a restart
            // requeued. Our own shutdown is the one exception that leaves it so: the next start picks it up again.
            logger.LogError(ex is PersistenceException { InnerException: { } inner } ? inner : ex,
                "Transcription of transcript {TranscriptId} failed unexpectedly", transcriptId);
            // Whatever the failed write had staged must not be saved with the failure.
            unitOfWork.DiscardChanges();
            await FinishAsync(transcriptId, null, UnexpectedFailureMessage, ct);
        }
        return true;
    }

    private async Task TranscribeAsync(Guid transcriptId, CancellationToken ct)
    {
        var transcript = await ReadAsync(token => transcripts.FindForProcessingAsync(transcriptId, token), ct);
        if (transcript is null) return;
        var recording = transcript.Recording;
        SpeechTranscription result;
        // Cancelled when the run goes stale: a room-mic take was appended meanwhile (ADR-0041), the recording was
        // erased, or the transcript was queued again. The worker stops on the dropped connection, and the newer version
        // is claimed next instead of waiting behind minutes of work whose result would be thrown away.
        using var stale = CancellationTokenSource.CreateLinkedTokenSource(ct);
        using var stopWatching = CancellationTokenSource.CreateLinkedTokenSource(ct);
        var watch = WatchAsync(transcriptId, stale, stopWatching.Token);
        try
        {
            if (recording.Status != RecordingStatus.Available) throw new SpeechEngineException("Bản ghi đã bị xoá.");
            await using var audio = storage.OpenRead(recording.StoragePath);
            result = await engine.TranscribeAsync(audio, recording.ContentType, diarize: true, clip: false, stale.Token);
        }
        catch (Exception ex) when (ex is OperationCanceledException && stale.IsCancellationRequested && !ct.IsCancellationRequested)
        {
            logger.LogInformation("Transcription of recording {RecordingId} stopped: a newer version waits", recording.Id);
            return;
        }
        catch (Exception ex) when (ex is SpeechEngineException or FileNotFoundException or DirectoryNotFoundException)
        {
            var message = ex is SpeechEngineException speech ? speech.UserMessage : "Tệp âm thanh không còn trên máy chủ.";
            logger.LogWarning(ex, "Transcription of recording {RecordingId} failed: {Reason}", recording.Id, message);
            await StopAsync(stopWatching, watch);
            await FinishAsync(transcriptId, null, message, ct);
            return;
        }
        finally
        {
            // The watch shares this scope's storage: it must be over before anything else reads or writes.
            await StopAsync(stopWatching, watch);
        }
        await FinishAsync(transcriptId, result, null, ct);
    }

    /// <summary>How often a running transcript checks that it is still the version to make.</summary>
    internal TimeSpan StaleCheckEvery { get; init; } = TimeSpan.FromSeconds(5);

    /// <summary>
    /// Cancels <paramref name="stale"/> once the transcript is no longer Processing: whatever moved it (a new take, an
    /// erasure, a retry) has made this run's result one that <see cref="FinishAsync"/> would throw away.
    /// </summary>
    private async Task WatchAsync(Guid transcriptId, CancellationTokenSource stale, CancellationToken ct)
    {
        try
        {
            while (true)
            {
                await Task.Delay(StaleCheckEvery, clock, ct);
                if (await ReadAsync(token => transcripts.IsProcessingAsync(transcriptId, token), ct)) continue;
                await stale.CancelAsync();
                return;
            }
        }
        catch (OperationCanceledException) when (ct.IsCancellationRequested)
        {
        }
        catch (PersistenceException ex)
        {
            // Not being able to look is no reason to stop: the result is checked again before it is written.
            logger.LogWarning(ex.InnerException ?? ex, "Could not check whether transcript {TranscriptId} is stale", transcriptId);
        }
    }

    private static async Task StopAsync(CancellationTokenSource stopWatching, Task watch)
    {
        if (!stopWatching.IsCancellationRequested) await stopWatching.CancelAsync();
        await watch;
    }

    /// <summary>
    /// Writes the result under the recording's row lock — the same lock erasure takes — so a recording erased while
    /// the worker was busy never gets its transcript written back afterwards. The voices are matched in the same
    /// transaction, under a share lock on the consents they match against, so no suggestion or machine-given name is
    /// written for someone whose Voice ID ended meanwhile.
    /// </summary>
    private async Task FinishAsync(Guid transcriptId, SpeechTranscription? result, string? error, CancellationToken ct)
    {
        var meetingId = await unitOfWork.ExecuteAsync(async token =>
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
                // What the Secretary corrected by hand, read before it is deleted, to be carried into the new lines (ADR-0077).
                var before = await transcripts.ReadCorrectionsAsync(transcriptId, token);
                var heard = result.Segments.Where(s => s.Text.Length > 0).Select(s =>
                {
                    var (startMs, endMs) = TranscriptRules.FitToTake(s.StartMs, s.EndMs, recording.Takes);
                    return (Segment: s, StartMs: startMs, EndMs: endMs);
                }).ToList();
                var corrected = before.Lines.Where(l => l.EditedAt is not null || l.SpeakerAssignedAt is not null).ToList();
                var matched = TranscriptCarry.Lines([.. corrected.Select(l => new TranscriptCarry.Span(l.StartMs, l.EndMs, null))],
                    [.. heard.Select(h => new TranscriptCarry.Span(h.StartMs, h.EndMs, null))], recording.Takes);
                var carriedTo = matched.ToDictionary(m => m.Value, m => corrected[m.Key]);
                // A short line no enrolled voice matched, or one of interjections only, is left out (team test
                // 21/11/2026), and a voice left with no line goes with it: the rest are numbered again without gaps. A
                // line the Secretary corrected is kept whatever it says: someone read it and found it worth correcting.
                var lines = heard.Select((h, i) => (h.Segment, h.StartMs, h.EndMs, Before: carriedTo.GetValueOrDefault(i)))
                    .Where(h => h.Before is not null || ShortLineRules.KeepsRoomLine(h.Segment.Text, h.Segment.EndMs - h.Segment.StartMs,
                        h.Segment.Speaker is not null && suggestions.ContainsKey(h.Segment.Speaker))).ToList();
                var labels = ShortLineRules.Renumber(lines.Select(s => s.Segment.Speaker));
                await transcripts.DeleteSegmentsAsync(transcriptId, token);
                var segments = lines.Select((s, i) =>
                {
                    var text = s.Segment.Text;
                    var edited = s.Before is { EditedAt: not null, Text: not null };
                    return new TranscriptSegment
                    {
                        TranscriptId = transcriptId, Position = i + 1, StartMs = s.StartMs, EndMs = s.EndMs,
                        Text = edited ? s.Before!.Text!
                            : text.Length > TranscriptRules.MaxSegmentText ? text[..TranscriptRules.MaxSegmentText] : text,
                        // The machine's word times belonged to the words it wrote, not to the Secretary's.
                        WordStartsMs = edited || text.Length > TranscriptRules.MaxSegmentText ? null : WordTimings.Within(WordTimings.Of(text, s.Segment.WordStartsMs), s.StartMs, s.EndMs),
                        WordConfidence = edited || text.Length > TranscriptRules.MaxSegmentText ? null : WordScores.Of(text, s.Segment.WordConfidence),
                        EditedById = edited ? s.Before!.EditedById : null, EditedAt = edited ? s.Before!.EditedAt : null,
                        SpeakerLabel = s.Segment.Speaker is null ? "" : labels[s.Segment.Speaker], CreatedAt = now, UpdatedAt = now,
                    };
                }).ToList();
                transcripts.AddSegments(segments);
                await transcripts.DeleteSpeakersAsync(transcriptId, token);
                // A match names the voice at once (PO 01/11/2026; every suggestion by default since 22/11, ADR-0074): no
                // assigner marks the machine's name, which the Secretary may still change, and which goes when that
                // person withdraws Voice ID consent. Only for someone who agreed to that (notice 1.2, ADR-0048); the
                // others are suggested, as they agreed.
                var sure = voiceOptions.Value.AutoAssignThreshold;
                var speakers = result.Speakers.Where(sp => labels.ContainsKey(sp.Label)).Select(sp =>
                {
                    var guess = suggestions.GetValueOrDefault(sp.Label);
                    var named = guess is not null && guess.Score >= sure && nameable.Contains(guess.UserId);
                    return new TranscriptSpeaker
                    {
                        TranscriptId = transcriptId, Label = labels[sp.Label], SpeechMs = sp.SpeechMs,
                        SuggestedUserId = guess?.UserId, SuggestionScore = guess?.Score,
                        AssignedUserId = named ? guess!.UserId : null, AssignedAt = named ? now : null,
                        CreatedAt = now, UpdatedAt = now,
                    };
                }).ToList();
                var carried = Carry(before, [.. lines.Select(l => l.Before)], segments, speakers,
                    [.. corrected.Where((_, i) => !matched.ContainsKey(i))], transcriptId, now);
                transcripts.AddSpeakers(speakers);
                foreach (var named in speakers.Where(s => s.AssignedUserId is not null && s.AssignedById is null))
                    await audit.RecordAsync(AuditActions.TranscriptSpeakerAssigned, AuditActions.TranscriptSpeakerObjectType,
                        named.Id, null, new { recordingId = recording.Id, label = named.Label, userId = named.AssignedUserId,
                            automatic = true, score = named.SuggestionScore }, token);
                if (carried is not null)
                    await audit.RecordAsync(AuditActions.TranscriptCorrectionsCarried, AuditActions.RecordingObjectType, recording.Id,
                        null, new { recording.MeetingId, carried.Names, carried.Edits, carried.Moves, carried.UnplacedEdits,
                            carried.UnplacedMoves }, token);
                current.Status = TranscriptStatus.Completed;
                current.Provider = TranscriptRules.Provider(result.AsrModel);
                current.Language = result.Language;
                current.ProgressPercent = 100;
                current.ErrorMessage = "";
                current.CompletedAt = now;
                // A room recording's length comes from its bytes, which its takes' places are counted in.
                if (result.DurationMs > 0 && recording.Takes is null) recording.DurationMs = result.DurationMs;
            }
            await unitOfWork.SaveChangesAsync(token);
            return (Guid?)recording.MeetingId;
        }, TransactionMode.ReadCommitted, ct);
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

    private sealed record CarriedName(string Label, string FromLabel, Guid? UserId, Guid? GuestId);
    private sealed record UnplacedLine(long StartMs, long EndMs, Guid? ById, DateTimeOffset? At);
    private sealed record Carried(IReadOnlyList<CarriedName> Names, int Edits, int Moves, IReadOnlyList<UnplacedLine> UnplacedEdits,
        IReadOnlyList<UnplacedLine> UnplacedMoves);

    /// <summary>
    /// ADR-0077 — puts the Secretary's hand corrections of the transcript being replaced onto the new run's voices.
    /// <paramref name="segments"/> come with the old line each was matched to (<paramref name="from"/>, same order; its
    /// edited text is already in place) and are changed in place, as are <paramref name="speakers"/>; a voice is added
    /// for a person a moved line names and no new voice is. Returns what was carried and what could not be placed, for
    /// the audit log; null when there was neither.
    /// <para>
    /// Only a name the Secretary gave (an assigner set, including a name they cleared) is carried, and it beats the
    /// machine's for that voice. A name the machine gave is made again from the voices, so one of a person whose Voice ID
    /// consent ended is not: the invariant of <c>ClearSuggestionsAsync</c>, which clears the machine's names only, holds.
    /// </para>
    /// </summary>
    private static Carried? Carry(TranscriptCorrections before, IReadOnlyList<PreviousLine?> from, List<TranscriptSegment> segments,
        List<TranscriptSpeaker> speakers, IReadOnlyList<PreviousLine> unplaced, Guid transcriptId, DateTimeOffset now)
    {
        var handNamed = before.Voices.Where(v => v.AssignedById is not null).ToDictionary(v => v.Label);
        if (handNamed.Count == 0 && unplaced.Count == 0 && from.All(l => l is null)) return null;
        // Which old voice each new one is, by the time they share; a moved line is the Secretary's word, not the diarizer's.
        var sameVoice = TranscriptCarry.Voices(
            [.. before.Lines.Select(l => new TranscriptCarry.Span(l.StartMs, l.EndMs, l.SpeakerAssignedAt is null ? l.SpeakerLabel : null))],
            [.. segments.Select(s => new TranscriptCarry.Span(s.StartMs, s.EndMs, s.SpeakerLabel))]);
        var names = new List<CarriedName>();
        foreach (var speaker in speakers)
        {
            if (!sameVoice.TryGetValue(speaker.Label, out var old) || !handNamed.TryGetValue(old, out var voice)) continue;
            (speaker.AssignedUserId, speaker.AssignedGuestId) = (voice.AssignedUserId, voice.AssignedGuestId);
            (speaker.AssignedById, speaker.AssignedAt) = (voice.AssignedById, voice.AssignedAt);
            names.Add(new(speaker.Label, old, voice.AssignedUserId, voice.AssignedGuestId));
        }

        var moves = 0;
        var unplacedMoves = unplaced.Where(l => l.SpeakerAssignedAt is not null).Select(Unplaced).ToList();
        for (var i = 0; i < segments.Count; i++)
        {
            if (from[i] is not { SpeakerAssignedAt: not null } line) continue;
            var segment = segments[i];
            var target = MovedTo(line, before, sameVoice, segment, speakers, segments, transcriptId, now);
            if (target is null)
            {
                unplacedMoves.Add(Unplaced(line));
                continue;
            }
            // The line's length moves with it, as when the Secretary moved it.
            var length = Math.Max(0, segment.EndMs - segment.StartMs);
            if (target != segment.SpeakerLabel)
                foreach (var voice in speakers.Where(s => s.Label == segment.SpeakerLabel || s.Label == target))
                    voice.SpeechMs = voice.Label == target ? voice.SpeechMs + length : Math.Max(0, voice.SpeechMs - length);
            segment.SpeakerLabel = target;
            (segment.SpeakerAssignedById, segment.SpeakerAssignedAt) = (line.SpeakerAssignedById, line.SpeakerAssignedAt);
            moves++;
        }
        var edits = from.Count(l => l is { EditedAt: not null });
        UnplacedLine[] unplacedEdits =
            [.. unplaced.Where(l => l.EditedAt is not null).Select(l => new UnplacedLine(l.StartMs, l.EndMs, l.EditedById, l.EditedAt))];
        // Nothing carried and nothing lost: no line in the audit log.
        return names.Count + edits + moves + unplacedEdits.Length + unplacedMoves.Count == 0 ? null
            : new(names, edits, moves, unplacedEdits, unplacedMoves);

        static UnplacedLine Unplaced(PreviousLine l) => new(l.StartMs, l.EndMs, l.SpeakerAssignedById, l.SpeakerAssignedAt);
    }

    /// <summary>
    /// The voice of the new run a line the Secretary moved goes to: when they moved it to a person they named, that person's voice
    /// (the line's own when already theirs, else the first named as them, else a new one carrying their name, as
    /// <see cref="AssignSegmentSpeakerAsync"/> does); when they moved it to a voice with no name, the new voice that is
    /// that old one. Null when there is none, or no free label is left.
    /// </summary>
    private static string? MovedTo(PreviousLine line, TranscriptCorrections before, IReadOnlyDictionary<string, string> sameVoice,
        TranscriptSegment segment, List<TranscriptSpeaker> speakers, List<TranscriptSegment> segments, Guid transcriptId, DateTimeOffset now)
    {
        var voice = before.Voices.FirstOrDefault(v => v.Label == line.SpeakerLabel);
        var same = sameVoice.Where(p => p.Value == line.SpeakerLabel).Select(p => p.Key).Order(StringComparer.Ordinal).FirstOrDefault();
        if (voice is not { } named || (named.AssignedUserId is null && named.AssignedGuestId is null)) return same;
        // A name the machine gave is not the Secretary's to carry: the line follows that voice, or the voice the machine
        // names as the same person this time — none when their Voice ID consent has ended meanwhile.
        if (named.AssignedById is null)
            return same ?? speakers.Where(s => s.AssignedById is null && s.AssignedUserId == named.AssignedUserId)
                .Select(s => s.Label).Order(StringComparer.Ordinal).FirstOrDefault();
        bool Theirs(TranscriptSpeaker s) => named.AssignedUserId is not null
            ? s.AssignedUserId == named.AssignedUserId : s.AssignedGuestId == named.AssignedGuestId;
        var theirs = speakers.FirstOrDefault(s => s.Label == segment.SpeakerLabel && Theirs(s))
            ?? speakers.Where(Theirs).OrderBy(s => s.Label, StringComparer.Ordinal).FirstOrDefault();
        if (theirs is not null) return theirs.Label;
        var taken = segments.Select(s => s.SpeakerLabel).Concat(speakers.Select(s => s.Label)).ToHashSet();
        var free = Enumerable.Range(0, 100).Select(i => $"SPEAKER_{i:00}").FirstOrDefault(l => !taken.Contains(l));
        if (free is null) return null;
        speakers.Add(new TranscriptSpeaker
        {
            TranscriptId = transcriptId, Label = free, AssignedUserId = named.AssignedUserId, AssignedGuestId = named.AssignedGuestId,
            AssignedById = named.AssignedById, AssignedAt = named.AssignedAt,
            CreatedAt = now, UpdatedAt = now,
        });
        return free;
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
            // The machine names the person only under notice 1.2 or later; a sample a Secretary took always is (1.3+).
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
        var suggestions = SpeakerMatcher.Suggest(result.Speakers, candidates, options.SuggestThreshold,
            options.SuggestMargin, options.SameVoiceThreshold, options.SecondVoiceThreshold, options.InheritVoiceThreshold);
        LogScores(meetingId, result.Speakers, candidates, suggestions);
        return (suggestions, nameable);
    }

    /// <summary>
    /// The two best scores of every voice, named or not, so a voice left "Người nói N" shows whether it fell short of
    /// <see cref="VoiceIdOptions.SuggestThreshold"/> or tied within <see cref="VoiceIdOptions.SuggestMargin"/> (Hải,
    /// 23/11/2026). Only labels, user ids and numbers are logged: never an embedding, a name or audio.
    /// </summary>
    private void LogScores(Guid meetingId, IReadOnlyList<SpeechSpeaker> speakers,
        IReadOnlyList<SpeakerMatcher.Candidate> candidates, IReadOnlyDictionary<string, SpeakerMatcher.Suggestion> suggestions)
    {
        if (candidates.Count == 0 || !logger.IsEnabled(LogLevel.Information)) return;
        foreach (var (label, ranked) in SpeakerMatcher.Scores(speakers, candidates).OrderBy(kv => kv.Key, StringComparer.Ordinal))
        {
            var top = string.Join(", ", ranked.Take(2).Select(x => $"{x.UserId}={x.Score:0.000}"));
            var named = suggestions.TryGetValue(label, out var suggestion) ? suggestion.UserId.ToString() : "none";
            logger.LogInformation("Voice scores in meeting {MeetingId}: {Label} best {Top}; suggested {Suggested}",
                meetingId, label, top, named);
        }
    }

    /// <summary>
    /// Storage calls outside a transaction (reads, and the few writes that never had one): run as they are, only their
    /// failures translated.
    /// </summary>
    private Task<T> ReadAsync<T>(Func<CancellationToken, Task<T>> work, CancellationToken ct)
        => unitOfWork.ExecuteAsync(work, TransactionMode.None, ct);

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
