using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Npgsql;
using TrustCabinet.Domain.Audit;
using TrustCabinet.Domain.Entities;
using TrustCabinet.Domain.Enums;
using TrustCabinet.Domain.Meetings;
using TrustCabinet.Infrastructure.GuestAccess;
using TrustCabinet.Domain.Recordings;
using TrustCabinet.Domain.Transcription;

namespace TrustCabinet.Infrastructure.Recordings;

/// <summary>
/// How many sentences go to the speech worker at once, across the whole server. The worker runs on a couple of CPU
/// cores; a meeting where everyone pauses together would otherwise queue a dozen requests inside it.
/// </summary>
public sealed class SpeechClipGate
{
    public SemaphoreSlim Slots { get; } = new(2, 2);

    /// <summary>How long a sentence waits for a slot before the browser is told to send it again later.</summary>
    public TimeSpan Wait { get; init; } = TimeSpan.FromSeconds(30);
}

/// <summary>
/// SCRUM-109 — the per-mic transcript. Each attendee's browser sends a sentence once they pause; the worker turns it
/// into text inside our network (ADR-0026). What is kept is the text, its audio to hear it again (ADR-0039), when it
/// was said and who said it — the owner of the mic, never a guess — and all of it goes when the recordings'
/// retention period ends. A guest's mic is captured the same way (SCRUM-129, ADR-0051), through their session.
/// </summary>
public sealed class SpeechClipService(IMeetingRepository meetings, TrustCabinetDbContext db, ISpeechEngine engine,
    SpeechClipGate gate, ISpeechChangePublisher publisher, IAuditService audit, TimeProvider clock,
    IOptions<RecordingOptions> options, IRecordingStorage storage, ISpeechClipAudioSigner signer,
    ILogger<SpeechClipService> logger) : ISpeechClipService
{
    private static bool PersistenceError(Exception ex) => ex is DbUpdateException or NpgsqlException or TimeoutException;

    public async Task<SpeechClipOutcome> ListAsync(Guid meetingId, Guid actorId, DateTimeOffset? since, CancellationToken ct = default)
    {
        try
        {
            var now = clock.GetUtcNow();
            var (meeting, failure) = await ListenableMeetingAsync(meetingId, actorId, ct);
            if (meeting is null) return new(failure);
            var secretary = RecordingRules.IsSecretary(meeting, actorId);
            var officer = SpeechClipRules.RunsRecord(meeting, actorId);
            var rows = await ReadWithNeighboursAsync(meetingId, since, ct);
            var marks = SpeechClipFold.Fold([.. rows.All.Select(FoldOf)]);
            var byId = rows.All.ToDictionary(r => r.Id);
            var items = rows.Shown.Select(r => View(r, marks.GetValueOrDefault(r.Id), byId, actorId, secretary, officer)).ToList();
            return new(Data: new SpeechClipList(meetingId, SpeechClipRules.Capturing(meeting), secretary, now, items, officer, RecordingRules.Export(meeting, actorId) == RecordingFailure.None, options.Value.RetentionDays));
        }
        catch (Exception ex) when (PersistenceError(ex))
        {
            logger.LogError(ex, "Failed to list speech clips of {MeetingId}", meetingId);
            return new(SpeechClipFailure.ReadFailed);
        }
    }

    public async Task<SpeechClipOutcome> ExportAsync(Guid meetingId, Guid actorId, CancellationToken ct = default)
    {
        try
        {
            var (meeting, failure) = await ListenableMeetingAsync(meetingId, actorId, ct);
            if (meeting is null) return new(failure);
            switch (RecordingRules.Export(meeting, actorId))
            {
                case RecordingFailure.ExportForbidden: return new(SpeechClipFailure.ExportForbidden);
                case RecordingFailure.SecretMeeting: return new(SpeechClipFailure.SecretMeeting);
            }
            // What the record holds: a sentence taken out of it is not exported, an overheard copy still is until then.
            var rows = (await RowsAsync(db.SpeechClips.AsNoTracking().Where(c => c.MeetingId == meetingId), ct))
                .Where(r => r.ExcludedAt is null).ToList();
            var now = clock.GetUtcNow();
            var started = meeting.ActualStartedAt ?? rows.FirstOrDefault()?.StartedAt ?? now;
            var text = TranscriptText.Render(meeting.Title, "ghi theo micro từng người", started, now,
                rows.Select(r => new TranscriptLine(TranscriptText.Clock(r.StartedAt), Labelled(r), r.Text)));
            await audit.RecordAsync(AuditActions.SpeechClipsExported, AuditActions.MeetingObjectType, meetingId, actorId,
                new { sentences = rows.Count }, ct);
            return new(Data: new ExportedText(TranscriptText.FileName(started), text));
        }
        catch (Exception ex) when (PersistenceError(ex))
        {
            logger.LogError(ex, "Failed to export the speech clips of {MeetingId}", meetingId);
            return new(SpeechClipFailure.ReadFailed);
        }
    }

    public async Task<SpeechClipOutcome> CanCaptureAsync(Guid meetingId, Guid actorId, CancellationToken ct = default)
    {
        try
        {
            var (meeting, failure) = await ListenableMeetingAsync(meetingId, actorId, ct);
            if (meeting is null) return new(failure);
            return SpeechClipRules.Accepts(meeting, clock.GetUtcNow()) ? new() : new(SpeechClipFailure.NotCapturing);
        }
        catch (Exception ex) when (PersistenceError(ex))
        {
            logger.LogError(ex, "Failed to check speech capture for {MeetingId}", meetingId);
            return new(SpeechClipFailure.SaveFailed);
        }
    }

    public Task<SpeechClipOutcome> AddAsync(Guid meetingId, Guid actorId, SpeechClipUpload upload, CancellationToken ct = default)
        => AddAsync(meetingId, Speaker.Member(actorId), token => CanCaptureAsync(meetingId, actorId, token),
            // A sentence finished just as the Chair ended the meeting was still said in it, and one the browser could
            // only send later (offline, tab closed) too.
            (meeting, now, _) => Task.FromResult(RecordingRules.CanListen(meeting, actorId) && SpeechClipRules.Accepts(meeting, now)),
            upload, ct);

    /// <summary>
    /// What both kinds of speaker share: <paramref name="check"/> refuses before the worker is asked, and
    /// <paramref name="stillAccepts"/> again inside the transaction that stores the sentence.
    /// </summary>
    private async Task<SpeechClipOutcome> AddAsync(Guid meetingId, Speaker speaker,
        Func<CancellationToken, Task<SpeechClipOutcome>> check,
        Func<Meeting, DateTimeOffset, CancellationToken, Task<bool>> stillAccepts, SpeechClipUpload upload, CancellationToken ct)
    {
        var errors = new Dictionary<string, string[]>();
        if (upload.ClientClipId == Guid.Empty) errors["clipId"] = ["Thiếu mã câu nói."];
        if (upload.DurationMs is <= 0 or > SpeechClipRules.MaxDurationMs)
            errors["durationMs"] = [$"Một câu dài từ 1 ms đến {SpeechClipRules.MaxDurationMs / 1000} giây."];
        if (upload.Audio.Length == 0) errors["audio"] = ["Câu nói không có âm thanh."];
        if (errors.Count > 0) return new(SpeechClipFailure.Invalid, Errors: errors);

        SpeechClipView? existing;
        try
        {
            var allowed = await check(ct);
            if (allowed.Failure != SpeechClipFailure.None) return allowed;
            // A retry of a sentence already stored (its response was lost): answer as before, transcribe nothing.
            existing = await FindByClientIdAsync(meetingId, speaker, upload.ClientClipId, ct);
            if (existing is not null) return new(Data: existing);
        }
        catch (Exception ex) when (PersistenceError(ex))
        {
            logger.LogError(ex, "Failed to read speech clips of {MeetingId}", meetingId);
            return new(SpeechClipFailure.SaveFailed);
        }

        var heard = await TranscribeAsync(upload, ct);
        if (heard is not { } transcribed) return new(SpeechClipFailure.EngineUnavailable);
        var (text, segments) = transcribed;
        // Silence, a cough, a door: nothing worth a line. The browser drops it; nothing is stored.
        if (text.Length == 0) return new();

        var audioPath = await StoreAudioAsync(upload, ct);
        var kept = false;
        try
        {
            var now = clock.GetUtcNow();
            var clip = await meetings.InTransactionAsync(async token =>
            {
                var meeting = await meetings.FindWorkflowAsync(meetingId, false, token);
                if (meeting is null || !await stillAccepts(meeting, now, token)) return null;
                var row = new SpeechClip
                {
                    MeetingId = meetingId, SpeakerId = speaker.UserId, GuestInvitationId = speaker.GuestInvitationId,
                    ClientClipId = upload.ClientClipId,
                    StartedAt = StartedAt(upload, meeting, now), DurationMs = upload.DurationMs,
                    Text = text.Length > SpeechClipRules.MaxTextLength ? text[..SpeechClipRules.MaxTextLength] : text,
                    Segments = segments, CreatedAt = now, UpdatedAt = now,
                    AudioPath = audioPath, AudioContentType = audioPath is null ? null : upload.ContentType,
                    Peaks = audioPath is null ? null : SpeechClipPeaks.FromWav(upload.Audio),
                };
                db.SpeechClips.Add(row);
                await db.SaveChangesAsync(token);
                return row;
            }, ct);
            if (clip is null) return new(SpeechClipFailure.NotCapturing);
            kept = true;
            await PublishAsync(meetingId, "ClipAdded", ct);
            return new(Data: await FindByClientIdAsync(meetingId, speaker, upload.ClientClipId, ct));
        }
        catch (DbUpdateException ex) when (ex.InnerException is PostgresException { SqlState: PostgresErrorCodes.UniqueViolation })
        {
            // The same sentence sent twice at once: the other request stored it.
            db.ChangeTracker.Clear();
            return new(Data: await FindByClientIdAsync(meetingId, speaker, upload.ClientClipId, ct));
        }
        catch (Exception ex) when (PersistenceError(ex))
        {
            logger.LogError(ex, "Failed to store a speech clip of {MeetingId}", meetingId);
            return new(SpeechClipFailure.SaveFailed);
        }
        finally
        {
            // Refused, failed, or stored by a concurrent copy of the same sentence: no row points at this file.
            if (!kept) DeleteAudio(audioPath);
        }
    }

    public async Task<SpeechClipOutcome> EditAsync(Guid clipId, Guid actorId, string? text, CancellationToken ct = default)
    {
        var trimmed = text?.Trim() ?? "";
        try
        {
            return await ChangeClipAsync(clipId, actorId, async (clip, meeting, now, token) =>
            {
                if (!RecordingRules.IsSecretary(meeting, actorId)) return new(SpeechClipFailure.Forbidden);
                if (trimmed.Length is 0 or > SpeechClipRules.MaxTextLength)
                    return new(SpeechClipFailure.Invalid, Errors: new Dictionary<string, string[]>
                    {
                        ["text"] = [$"Nội dung từ 1 đến {SpeechClipRules.MaxTextLength} ký tự."],
                    });
                if (clip.Text == trimmed) return new();
                await audit.RecordAsync(AuditActions.SpeechClipEdited, AuditActions.SpeechClipObjectType, clip.Id, actorId,
                    new { clip.MeetingId, clip.SpeakerId, clip.GuestInvitationId }, token);
                clip.Text = trimmed;
                clip.EditedById = actorId;
                clip.EditedAt = now;
                clip.UpdatedAt = now;
                return new();
            }, "ClipEdited", ct);
        }
        catch (Exception ex) when (PersistenceError(ex))
        {
            logger.LogError(ex, "Failed to edit speech clip {ClipId}", clipId);
            return new(SpeechClipFailure.SaveFailed);
        }
    }

    /// <summary>
    /// The speaker takes back what they said, or the Secretary removes a line that should not be there — a guest's too,
    /// when the guest asks after their session is over (SCRUM-129).
    /// </summary>
    public async Task<SpeechClipOutcome> DeleteAsync(Guid clipId, Guid actorId, CancellationToken ct = default)
    {
        try
        {
            string? audioPath = null;
            var outcome = await ChangeClipAsync(clipId, actorId, async (clip, meeting, now, token) =>
            {
                if (clip.SpeakerId != actorId && !RecordingRules.IsSecretary(meeting, actorId))
                    return new(SpeechClipFailure.Forbidden);
                await audit.RecordAsync(AuditActions.SpeechClipDeleted, AuditActions.SpeechClipObjectType, clip.Id, actorId,
                    new { clip.MeetingId, clip.SpeakerId, clip.GuestInvitationId, byOwner = clip.SpeakerId == actorId }, token);
                db.SpeechClips.Remove(clip);
                audioPath = clip.AudioPath;
                return new();
            }, "ClipDeleted", ct);
            // After the commit: a failed delete keeps the row, and with it the way to the file.
            if (outcome.Failure == SpeechClipFailure.None) DeleteAudio(audioPath);
            return outcome;
        }
        catch (Exception ex) when (PersistenceError(ex))
        {
            logger.LogError(ex, "Failed to delete speech clip {ClipId}", clipId);
            return new(SpeechClipFailure.SaveFailed);
        }
    }

    public async Task<SpeechClipOutcome> SetExclusionAsync(Guid clipId, Guid actorId, bool excluded, string? reason,
        CancellationToken ct = default)
    {
        SpeechClipExclusion? why = null;
        if (excluded)
        {
            if (!Enum.TryParse<SpeechClipExclusion>(reason, false, out var parsed) || !Enum.IsDefined(parsed))
                return new(SpeechClipFailure.Invalid, Errors: new Dictionary<string, string[]>
                {
                    ["reason"] = ["Lý do phải là Leak hoặc Other."],
                });
            why = parsed;
        }
        try
        {
            return await ChangeClipAsync(clipId, actorId, async (clip, meeting, now, token) =>
            {
                if (!SpeechClipRules.RunsRecord(meeting, actorId)) return new(SpeechClipFailure.Forbidden);
                // Already so: a double tap writes and audits nothing.
                if (clip.ExcludedAt is not null == excluded && clip.ExclusionReason == why) return new(Data: excluded);
                await audit.RecordAsync(excluded ? AuditActions.SpeechClipExcluded : AuditActions.SpeechClipRestored,
                    AuditActions.SpeechClipObjectType, clip.Id, actorId,
                    new { clip.MeetingId, clip.SpeakerId, clip.GuestInvitationId, reason = (why ?? clip.ExclusionReason)?.ToString() }, token);
                clip.ExcludedAt = excluded ? now : null;
                clip.ExcludedById = excluded ? actorId : null;
                clip.ExclusionReason = why;
                clip.UpdatedAt = now;
                return new(Data: excluded);
            }, "ClipEdited", ct);
        }
        catch (Exception ex) when (PersistenceError(ex))
        {
            logger.LogError(ex, "Failed to change the exclusion of speech clip {ClipId}", clipId);
            return new(SpeechClipFailure.SaveFailed);
        }
    }

    // SCRUM-129, ADR-0051 — a guest's own mic. The guest is whoever holds a current session of the invitation, for the
    // meeting it names: the session, the invitation and the meeting are read again on every call, so signing out,
    // revoking the invitation or the meeting ending stop the mic at once. A guest reads only their own sentences.

    public async Task<SpeechClipOutcome> ListForGuestAsync(MeetingGuestSession guest, Guid meetingId, DateTimeOffset? since,
        CancellationToken ct = default)
    {
        try
        {
            var now = clock.GetUtcNow();
            if (!await GuestAdmittedAsync(guest, meetingId, ct)) return new(SpeechClipFailure.NotFound);
            var meeting = await meetings.FindWorkflowAsync(meetingId, false, ct);
            if (meeting is null) return new(SpeechClipFailure.NotFound);
            var clips = db.SpeechClips.AsNoTracking().Where(c => c.MeetingId == meetingId && c.GuestInvitationId == guest.InvitationId);
            if (since is { } after) clips = clips.Where(c => c.UpdatedAt > after);
            // No leak marks: they name other people's sentences, which a guest does not read.
            var items = (await RowsAsync(clips, ct)).Select(r =>
            {
                var excluded = r.ExcludedAt is not null;
                return new SpeechClipView(r.Id, r.SpeakerId, r.SpeakerName, r.StartedAt, r.DurationMs, excluded ? "" : r.Text,
                    r.UpdatedAt, r.EditedAt, null, true, excluded, HasAudio: r.HasAudio && !excluded,
                    Peaks: excluded ? null : r.Peaks, SpeakerIsGuest: true);
            }).ToList();
            var capturing = SpeechClipRules.AcceptsGuest(meeting) && await GuestNoticeConfirmedAsync(guest, ct);
            return new(Data: new SpeechClipList(meetingId, capturing, false, now, items, RetentionDays: options.Value.RetentionDays));
        }
        catch (Exception ex) when (PersistenceError(ex))
        {
            logger.LogError(ex, "Failed to list a guest's speech clips of {MeetingId}", meetingId);
            return new(SpeechClipFailure.ReadFailed);
        }
    }

    public async Task<SpeechClipOutcome> CanGuestCaptureAsync(MeetingGuestSession guest, Guid meetingId, CancellationToken ct = default)
    {
        try
        {
            if (!await GuestAdmittedAsync(guest, meetingId, ct)) return new(SpeechClipFailure.NotFound);
            var meeting = await meetings.FindWorkflowAsync(meetingId, false, ct);
            if (meeting is null) return new(SpeechClipFailure.NotFound);
            if (!SpeechClipRules.AcceptsGuest(meeting)) return new(SpeechClipFailure.NotCapturing);
            // Never recorded before being told: the call room's pre-join notice is what the guest consented to.
            return await GuestNoticeConfirmedAsync(guest, ct) ? new() : new(SpeechClipFailure.NoticeRequired);
        }
        catch (Exception ex) when (PersistenceError(ex))
        {
            logger.LogError(ex, "Failed to check a guest's speech capture for {MeetingId}", meetingId);
            return new(SpeechClipFailure.SaveFailed);
        }
    }

    public Task<SpeechClipOutcome> AddForGuestAsync(MeetingGuestSession guest, Guid meetingId, SpeechClipUpload upload,
        CancellationToken ct = default)
        => AddAsync(meetingId, Speaker.Guest(guest.InvitationId), token => CanGuestCaptureAsync(guest, meetingId, token),
            async (meeting, _, token) => SpeechClipRules.AcceptsGuest(meeting) && await GuestAdmittedAsync(guest, meetingId, token),
            upload, ct);

    /// <summary>
    /// A guest takes back what they said while their session lasts. After it, the Secretary deletes it on their request.
    /// </summary>
    public async Task<SpeechClipOutcome> DeleteForGuestAsync(MeetingGuestSession guest, Guid clipId, CancellationToken ct = default)
    {
        try
        {
            string? audioPath = null;
            var outcome = await meetings.InTransactionAsync(async token =>
            {
                if (!await GuestAdmittedAsync(guest, guest.MeetingId, token)) return new SpeechClipOutcome(SpeechClipFailure.NotFound);
                // Someone else's sentence does not exist for a guest.
                var clip = await db.SpeechClips.SingleOrDefaultAsync(c => c.Id == clipId && c.MeetingId == guest.MeetingId
                    && c.GuestInvitationId == guest.InvitationId, token);
                if (clip is null) return new(SpeechClipFailure.NotFound);
                await audit.RecordAsync(AuditActions.SpeechClipDeleted, AuditActions.SpeechClipObjectType, clip.Id, actorId: null,
                    new { clip.MeetingId, clip.GuestInvitationId, guestSessionId = guest.SessionId, byOwner = true }, token);
                db.SpeechClips.Remove(clip);
                await db.SaveChangesAsync(token);
                audioPath = clip.AudioPath;
                return new();
            }, ct);
            if (outcome.Failure != SpeechClipFailure.None) return outcome;
            DeleteAudio(audioPath);
            await PublishAsync(guest.MeetingId, "ClipDeleted", ct);
            return outcome;
        }
        catch (Exception ex) when (PersistenceError(ex))
        {
            logger.LogError(ex, "Failed to delete speech clip {ClipId} for a guest", clipId);
            return new(SpeechClipFailure.SaveFailed);
        }
    }

    public async Task<SpeechClipOutcome> PlaybackForGuestAsync(MeetingGuestSession guest, Guid clipId, CancellationToken ct = default)
    {
        try
        {
            if (!await GuestAdmittedAsync(guest, guest.MeetingId, ct)) return new(SpeechClipFailure.NotFound);
            var heard = await db.SpeechClips.AsNoTracking().AnyAsync(c => c.Id == clipId && c.MeetingId == guest.MeetingId
                && c.GuestInvitationId == guest.InvitationId && c.AudioPath != null && c.ExcludedAt == null, ct);
            return heard ? new(Data: signer.Sign(clipId, clock.GetUtcNow())) : new(SpeechClipFailure.NotFound);
        }
        catch (Exception ex) when (PersistenceError(ex))
        {
            logger.LogError(ex, "Failed to sign playback for speech clip {ClipId} for a guest", clipId);
            return new(SpeechClipFailure.ReadFailed);
        }
    }

    /// <summary>
    /// The same sessions the guest's agenda, roster and call room accept: current, of an invitation still standing, for
    /// this meeting while it is open, and named (UC-04 step 5). A token for another meeting never gets this far.
    /// </summary>
    private async Task<bool> GuestAdmittedAsync(MeetingGuestSession guest, Guid meetingId, CancellationToken ct)
        => guest.IsValid && guest.MeetingId == meetingId && await GuestAccessService.CurrentSessions(db, clock.GetUtcNow())
            .AnyAsync(x => x.Id == guest.SessionId && x.InvitationId == guest.InvitationId
                && x.Invitation.MeetingId == meetingId && x.Invitation.SelfDeclaredDisplayName != null, ct);

    /// <summary>
    /// The evidence the call room keeps when a guest confirms the recording notice before joining (SCRUM-59): without
    /// it, this session never saw the notice and nothing it sends is recorded.
    /// </summary>
    private Task<bool> GuestNoticeConfirmedAsync(MeetingGuestSession guest, CancellationToken ct)
        => db.AuditLog.AsNoTracking().AnyAsync(a => a.ObjectType == AuditActions.GuestSessionObjectType
            && a.ObjectId == guest.SessionId && a.Action == AuditActions.CallGuestAdmitted, ct);

    public async Task<int> EraseExpiredAsync(CancellationToken ct = default)
    {
        var cutoff = clock.GetUtcNow().AddDays(-options.Value.RetentionDays);
        var expired = db.SpeechClips.Where(c => c.CreatedAt < cutoff);
        var files = await expired.Where(c => c.AudioPath != null).Select(c => c.AudioPath!).ToListAsync(ct);
        var erased = await expired.ExecuteDeleteAsync(ct);
        foreach (var file in files) DeleteAudio(file);
        return erased;
    }

    public async Task<SpeechClipOutcome> PlaybackAsync(Guid clipId, Guid actorId, CancellationToken ct = default)
    {
        try
        {
            var clip = await db.SpeechClips.AsNoTracking().Where(c => c.Id == clipId)
                .Select(c => new { c.MeetingId, c.AudioPath, c.ExcludedAt }).SingleOrDefaultAsync(ct);
            if (clip is null) return new(SpeechClipFailure.NotFound);
            var (meeting, failure) = await ListenableMeetingAsync(clip.MeetingId, actorId, ct);
            if (meeting is null) return new(failure);
            // An excluded sentence is heard by whoever still reads its words; to everyone else it does not exist.
            if (clip.AudioPath is null || (clip.ExcludedAt is not null && !SpeechClipRules.RunsRecord(meeting, actorId)))
                return new(SpeechClipFailure.NotFound);
            return new(Data: signer.Sign(clipId, clock.GetUtcNow()));
        }
        catch (Exception ex) when (PersistenceError(ex))
        {
            logger.LogError(ex, "Failed to sign playback for speech clip {ClipId}", clipId);
            return new(SpeechClipFailure.ReadFailed);
        }
    }

    public Task<SpeechClipAudio?> FindAudioAsync(Guid clipId, CancellationToken ct = default)
        => db.SpeechClips.AsNoTracking().Where(c => c.Id == clipId && c.AudioPath != null)
            .Select(c => new SpeechClipAudio(c.AudioPath!, c.AudioContentType ?? "audio/wav")).SingleOrDefaultAsync(ct);

    /// <summary>
    /// Keeps the sentence's audio beside its words. A full or failing disk loses only the audio: the words are what
    /// the minutes are written from, so the sentence is stored without it rather than refused.
    /// </summary>
    private async Task<string?> StoreAudioAsync(SpeechClipUpload upload, CancellationToken ct)
    {
        if (!SpeechClipRules.Extensions.TryGetValue(upload.ContentType, out var extension)) return null;
        try
        {
            using var audio = new MemoryStream(upload.Audio, writable: false);
            return (await storage.SaveAsync(audio, extension, SpeechClipRules.MaxClipBytes, ct)).StoragePath;
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            logger.LogError(ex, "Could not store the audio of a speech clip");
            return null;
        }
    }

    private void DeleteAudio(string? path)
    {
        if (path is null) return;
        try
        {
            storage.Delete(path);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            logger.LogWarning(ex, "Could not delete the audio of a speech clip at {Path}", path);
        }
    }

    /// <summary>
    /// The sentence's text and its segments, or null when the worker cannot be reached or is busy for too long; the
    /// browser keeps the sentence and retries.
    /// </summary>
    private async Task<(string Text, List<SpeechClipSegment> Segments)?> TranscribeAsync(SpeechClipUpload upload,
        CancellationToken ct)
    {
        if (!engine.IsConfigured) return null;
        if (!await gate.Slots.WaitAsync(gate.Wait, ct)) return null;
        try
        {
            using var audio = new MemoryStream(upload.Audio, writable: false);
            // Diarization off: the owner of the mic is the speaker, so there is nothing to tell apart.
            var result = await engine.TranscribeAsync(audio, upload.ContentType, diarize: false, clip: true, ct);
            var spoken = result.Segments.Select(s => (Segment: s, Text: s.Text.Trim())).Where(s => s.Text.Length > 0).ToList();
            return (string.Join(' ', spoken.Select(s => s.Text)), [.. spoken.Select(s => new SpeechClipSegment(
                s.Segment.StartMs, s.Segment.EndMs, s.Text.Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries).Length,
                s.Segment.LevelDbfs))]);
        }
        catch (SpeechEngineException ex)
        {
            logger.LogWarning(ex, "The speech worker could not transcribe a clip");
            return null;
        }
        finally
        {
            gate.Slots.Release();
        }
    }

    /// <summary>
    /// The browser's clock, unless it is too far off the server's or puts the sentence outside the meeting; then the
    /// sentence ended when it arrived.
    /// </summary>
    private static DateTimeOffset StartedAt(SpeechClipUpload upload, Meeting meeting, DateTimeOffset now)
    {
        // Sent late, after the meeting ended: the sentence ended at the latest when the meeting did.
        var ended = meeting.Status == MeetingStatus.Ended && meeting.ActualEndedAt is { } at && at < now ? at : now;
        var fallback = ended.AddMilliseconds(-upload.DurationMs);
        var started = upload.StartedAt.ToUniversalTime();
        var earliest = (meeting.ActualStartedAt ?? fallback) - SpeechClipRules.ClockTolerance;
        return started < earliest || started > ended + SpeechClipRules.ClockTolerance ? fallback : started;
    }

    private async Task<SpeechClipOutcome> ChangeClipAsync(Guid clipId, Guid actorId,
        Func<SpeechClip, Meeting, DateTimeOffset, CancellationToken, Task<SpeechClipOutcome>> change, string changeType,
        CancellationToken ct)
    {
        Guid? meetingId = null;
        var outcome = await meetings.InTransactionAsync(async token =>
        {
            if (!await meetings.IsUserActiveAsync(actorId, token)) return new SpeechClipOutcome(SpeechClipFailure.Inactive);
            var clip = await db.SpeechClips.SingleOrDefaultAsync(c => c.Id == clipId, token);
            if (clip is null) return new(SpeechClipFailure.NotFound);
            var meeting = await meetings.FindWorkflowAsync(clip.MeetingId, false, token);
            if (meeting is null || !RecordingRules.CanListen(meeting, actorId)) return new(SpeechClipFailure.NotFound);
            var result = await change(clip, meeting, clock.GetUtcNow(), token);
            if (result.Failure != SpeechClipFailure.None) return result;
            await db.SaveChangesAsync(token);
            meetingId = meeting.Id;
            return result;
        }, ct);
        if (meetingId is { } id) await PublishAsync(id, changeType, ct);
        return outcome;
    }

    private async Task<SpeechClipView?> FindByClientIdAsync(Guid meetingId, Speaker speaker, Guid clientClipId, CancellationToken ct)
    {
        var clips = db.SpeechClips.AsNoTracking().Where(c => c.MeetingId == meetingId && c.ClientClipId == clientClipId);
        clips = speaker.UserId is { } user ? clips.Where(c => c.SpeakerId == user)
            : clips.Where(c => c.GuestInvitationId == speaker.GuestInvitationId);
        var row = (await RowsAsync(clips, ct)).SingleOrDefault();
        if (row is null) return null;
        // A retry answers as the list does for its sender: an excluded sentence keeps its words from them.
        var excluded = row.ExcludedAt is not null;
        return new SpeechClipView(row.Id, row.SpeakerId, row.SpeakerName, row.StartedAt, row.DurationMs,
            excluded ? "" : row.Text, row.UpdatedAt, row.EditedAt, row.EditedByName, true, excluded,
            HasAudio: row.HasAudio && !excluded, Peaks: excluded ? null : row.Peaks, SpeakerIsGuest: row.Guest);
    }

    /// <summary>Who sends a sentence: a member by their user id, or a guest by their invitation (SCRUM-129).</summary>
    private readonly record struct Speaker(Guid? UserId, Guid? GuestInvitationId)
    {
        public static Speaker Member(Guid userId) => new(userId, null);
        public static Speaker Guest(Guid invitationId) => new(null, invitationId);
    }

    /// <summary>
    /// One sentence with what the list and the fold need of it. <c>SpeakerId</c> tells speakers apart: a member's user
    /// id, or a guest's invitation id (<c>Guest</c>); a guest is named as the transcripts name them.
    /// </summary>
    private sealed record ClipRow(Guid Id, Guid SpeakerId, string SpeakerName, DateTimeOffset StartedAt, long DurationMs,
        string Text, DateTimeOffset UpdatedAt, DateTimeOffset? EditedAt, string? EditedByName, List<SpeechClipSegment>? Segments,
        DateTimeOffset? ExcludedAt, SpeechClipExclusion? ExclusionReason, string? ExcludedByName, bool HasAudio, byte[]? Peaks,
        bool Guest);

    /// <summary>A guest's line in the exported text says so, as the transcript on screen does.</summary>
    private static string Labelled(ClipRow r) => r.Guest ? $"{r.SpeakerName} (Khách)" : r.SpeakerName;

    private sealed record ClipRows(IReadOnlyList<ClipRow> All, IReadOnlyList<ClipRow> Shown);

    /// <summary>
    /// A full read is every sentence. A read since the last one is what changed plus the sentences close to it in
    /// time: whether a sentence was overheard depends on its neighbours, so a louder copy arriving later, or one being
    /// excluded, changes the mark on an older line that did not change itself.
    /// </summary>
    private async Task<ClipRows> ReadWithNeighboursAsync(Guid meetingId, DateTimeOffset? since, CancellationToken ct)
    {
        var clips = db.SpeechClips.AsNoTracking().Where(c => c.MeetingId == meetingId);
        if (since is not { } after)
        {
            var all = await RowsAsync(clips, ct);
            return new(all, all);
        }
        var changed = await RowsAsync(clips.Where(c => c.UpdatedAt > after), ct);
        if (changed.Count == 0) return new([], []);
        // Neighbours within the fold's clock lag of a changed sentence; a sentence lasts a minute at most.
        var reach = TimeSpan.FromMilliseconds(SpeechClipFold.LagMs + SpeechClipRules.MaxDurationMs);
        var from = changed.Min(c => c.StartedAt) - reach;
        var to = changed.Max(c => c.StartedAt.AddMilliseconds(c.DurationMs)) + reach;
        var window = await RowsAsync(clips.Where(c => c.StartedAt >= from && c.StartedAt <= to), ct);
        var lag = TimeSpan.FromMilliseconds(SpeechClipFold.LagMs);
        var shown = window.Where(r => changed.Any(c => r.StartedAt <= c.StartedAt.AddMilliseconds(c.DurationMs) + lag
            && c.StartedAt <= r.StartedAt.AddMilliseconds(r.DurationMs) + lag)).ToList();
        return new(window, shown);
    }

    private static async Task<List<ClipRow>> RowsAsync(IQueryable<SpeechClip> clips, CancellationToken ct)
        => await clips.OrderBy(c => c.StartedAt).ThenBy(c => c.CreatedAt)
            .Select(c => new ClipRow(c.Id, c.SpeakerId ?? c.GuestInvitationId ?? Guid.Empty,
                c.Speaker != null ? c.Speaker.FullName
                    : c.GuestInvitation!.SelfDeclaredDisplayName ?? c.GuestInvitation.InvitedName,
                c.StartedAt, c.DurationMs, c.Text, c.UpdatedAt,
                c.EditedAt, c.EditedBy == null ? null : c.EditedBy.FullName, c.Segments, c.ExcludedAt, c.ExclusionReason,
                c.ExcludedBy == null ? null : c.ExcludedBy.FullName, c.AudioPath != null, c.Peaks, c.GuestInvitationId != null))
            .ToListAsync(ct);

    private static FoldClip FoldOf(ClipRow r) => new(r.Id, r.SpeakerId, r.StartedAt.ToUnixTimeMilliseconds(), r.DurationMs,
        r.Text, r.Segments ?? [], r.ExcludedAt is not null, r.EditedAt is not null);

    /// <summary>
    /// The Chair and the Secretary read and hear an excluded sentence; everyone else sees only that it was there.
    /// </summary>
    private static SpeechClipView View(ClipRow r, FoldMark? mark, Dictionary<Guid, ClipRow> byId, Guid actorId,
        bool secretary, bool officer)
    {
        var excluded = r.ExcludedAt is not null;
        return new(r.Id, r.SpeakerId, r.SpeakerName, r.StartedAt, r.DurationMs, excluded && !officer ? "" : r.Text,
            r.UpdatedAt, r.EditedAt, r.EditedByName, secretary || (!r.Guest && r.SpeakerId == actorId), excluded,
            r.ExclusionReason?.ToString(), officer ? r.ExcludedByName : null,
            mark is null || excluded ? null : Leak(r, mark, byId),
            r.HasAudio && (!excluded || officer), excluded && !officer ? null : r.Peaks, r.Guest);
    }

    private static SpeechClipLeak? Leak(ClipRow r, FoldMark mark, Dictionary<Guid, ClipRow> byId)
    {
        var of = mark.LeakOf ?? mark.Spans.GroupBy(s => s.Of).OrderByDescending(g => g.Sum(s => s.EndMs - s.StartMs)).First().Key;
        if (!byId.TryGetValue(of, out var source)) return null;
        var words = new List<int[]>();
        var at = 0;
        foreach (var segment in r.Segments ?? [])
        {
            if (mark.Spans.Any(s => s.StartMs == segment.StartMs && s.EndMs == segment.EndMs)) words.Add([at, at + segment.Words]);
            at += segment.Words;
        }
        return new(source.Id, source.SpeakerId, source.SpeakerName, mark.LeakOf is not null, words);
    }

    private async Task<(Meeting? Meeting, SpeechClipFailure Failure)> ListenableMeetingAsync(Guid meetingId, Guid actorId,
        CancellationToken ct)
    {
        if (!await meetings.IsUserActiveAsync(actorId, ct)) return (null, SpeechClipFailure.Inactive);
        var meeting = await meetings.FindWorkflowAsync(meetingId, false, ct);
        return meeting is not null && RecordingRules.CanListen(meeting, actorId)
            ? (meeting, SpeechClipFailure.None) : (null, SpeechClipFailure.NotFound);
    }

    /// <summary>The write has committed; a lost event only delays the others' view until their next read.</summary>
    private async Task PublishAsync(Guid meetingId, string changeType, CancellationToken ct)
    {
        try
        {
            using var timeout = CancellationTokenSource.CreateLinkedTokenSource(ct);
            timeout.CancelAfter(TimeSpan.FromSeconds(2));
            await publisher.PublishAsync(new(Guid.CreateVersion7(), meetingId, changeType, clock.GetUtcNow()), timeout.Token);
        }
        catch (Exception ex) when (ex is not OperationCanceledException || !ct.IsCancellationRequested)
        {
            logger.LogWarning(ex, "Could not publish {ChangeType} for {MeetingId}", changeType, meetingId);
        }
    }
}
