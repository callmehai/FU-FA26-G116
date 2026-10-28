using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Npgsql;
using TrustCabinet.Domain.Audit;
using TrustCabinet.Domain.Entities;
using TrustCabinet.Domain.Enums;
using TrustCabinet.Domain.Meetings;
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
/// retention period ends.
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
            return new(Data: new SpeechClipList(meetingId, meeting.CaptureMode.ToString(),
                SpeechClipRules.Capturing(meeting), secretary,
                SpeechClipRules.RunsRecord(meeting, actorId) && SpeechClipRules.ModeChangeable(meeting.Status), now, items,
                officer, RecordingRules.Export(meeting, actorId) == RecordingFailure.None, options.Value.RetentionDays));
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
                rows.Select(r => new TranscriptLine(TranscriptText.Clock(r.StartedAt), r.SpeakerName, r.Text)));
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

    public async Task<SpeechClipOutcome> AddAsync(Guid meetingId, Guid actorId, SpeechClipUpload upload, CancellationToken ct = default)
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
            var allowed = await CanCaptureAsync(meetingId, actorId, ct);
            if (allowed.Failure != SpeechClipFailure.None) return allowed;
            // A retry of a sentence already stored (its response was lost): answer as before, transcribe nothing.
            existing = await FindByClientIdAsync(meetingId, actorId, upload.ClientClipId, ct);
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
                if (meeting is null || !RecordingRules.CanListen(meeting, actorId)) return null;
                // A sentence finished just as the Chair ended the meeting was still said in it, and one the browser
                // could only send later (offline, tab closed) too.
                if (!SpeechClipRules.Accepts(meeting, now)) return null;
                var row = new SpeechClip
                {
                    MeetingId = meetingId, SpeakerId = actorId, ClientClipId = upload.ClientClipId,
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
            return new(Data: await FindByClientIdAsync(meetingId, actorId, upload.ClientClipId, ct));
        }
        catch (DbUpdateException ex) when (ex.InnerException is PostgresException { SqlState: PostgresErrorCodes.UniqueViolation })
        {
            // The same sentence sent twice at once: the other request stored it.
            db.ChangeTracker.Clear();
            return new(Data: await FindByClientIdAsync(meetingId, actorId, upload.ClientClipId, ct));
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
                    new { clip.MeetingId, clip.SpeakerId }, token);
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

    /// <summary>The speaker takes back what they said, or the Secretary removes a line that should not be there.</summary>
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
                    new { clip.MeetingId, clip.SpeakerId, byOwner = clip.SpeakerId == actorId }, token);
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
                    new { clip.MeetingId, clip.SpeakerId, reason = (why ?? clip.ExclusionReason)?.ToString() }, token);
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

    public async Task<SpeechClipOutcome> SetCaptureModeAsync(Guid meetingId, Guid actorId, string? mode, CancellationToken ct = default)
    {
        if (!Enum.TryParse<CaptureMode>(mode, false, out var capture) || !Enum.IsDefined(capture))
            return new(SpeechClipFailure.Invalid, Errors: new Dictionary<string, string[]>
            {
                ["mode"] = ["Cách ghi âm phải là PerMic hoặc Room."],
            });
        try
        {
            var changed = false;
            var outcome = await meetings.InTransactionAsync(async token =>
            {
                if (!await meetings.IsUserActiveAsync(actorId, token)) return new SpeechClipOutcome(SpeechClipFailure.Inactive);
                var meeting = await meetings.FindWorkflowAsync(meetingId, true, token);
                if (meeting is null || !RecordingRules.CanListen(meeting, actorId)) return new(SpeechClipFailure.NotFound);
                if (!SpeechClipRules.RunsRecord(meeting, actorId)) return new(SpeechClipFailure.Forbidden);
                if (meeting.Status == MeetingStatus.Cancelled) return new(SpeechClipFailure.Cancelled);
                if (!SpeechClipRules.ModeChangeable(meeting.Status)) return new(SpeechClipFailure.Locked);
                if (meeting.CaptureMode != capture)
                {
                    await audit.RecordAsync(AuditActions.CaptureModeChanged, AuditActions.MeetingObjectType, meeting.Id, actorId,
                        new { from = meeting.CaptureMode.ToString(), to = capture.ToString() }, token);
                    meeting.CaptureMode = capture;
                    await db.SaveChangesAsync(token);
                    changed = true;
                }
                return new SpeechClipOutcome(Data: capture.ToString());
            }, ct);
            if (changed) await PublishAsync(meetingId, "CaptureModeChanged", ct);
            return outcome;
        }
        catch (Exception ex) when (PersistenceError(ex))
        {
            logger.LogError(ex, "Failed to change the capture mode of {MeetingId}", meetingId);
            return new(SpeechClipFailure.SaveFailed);
        }
    }

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

    private Task<SpeechClipView?> FindByClientIdAsync(Guid meetingId, Guid actorId, Guid clientClipId, CancellationToken ct)
        => db.SpeechClips.AsNoTracking()
            .Where(c => c.MeetingId == meetingId && c.SpeakerId == actorId && c.ClientClipId == clientClipId)
            .Select(c => new SpeechClipView(c.Id, c.SpeakerId, c.Speaker.FullName, c.StartedAt, c.DurationMs, c.Text,
                c.UpdatedAt, c.EditedAt, c.EditedBy == null ? null : c.EditedBy.FullName, true, false, null, null, null,
                c.AudioPath != null, c.Peaks))
            .SingleOrDefaultAsync(ct);

    /// <summary>One sentence with what the list and the fold need of it.</summary>
    private sealed record ClipRow(Guid Id, Guid SpeakerId, string SpeakerName, DateTimeOffset StartedAt, long DurationMs,
        string Text, DateTimeOffset UpdatedAt, DateTimeOffset? EditedAt, string? EditedByName, List<SpeechClipSegment>? Segments,
        DateTimeOffset? ExcludedAt, SpeechClipExclusion? ExclusionReason, string? ExcludedByName, bool HasAudio, byte[]? Peaks);

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
            .Select(c => new ClipRow(c.Id, c.SpeakerId, c.Speaker.FullName, c.StartedAt, c.DurationMs, c.Text, c.UpdatedAt,
                c.EditedAt, c.EditedBy == null ? null : c.EditedBy.FullName, c.Segments, c.ExcludedAt, c.ExclusionReason,
                c.ExcludedBy == null ? null : c.ExcludedBy.FullName, c.AudioPath != null, c.Peaks))
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
            r.UpdatedAt, r.EditedAt, r.EditedByName, secretary || r.SpeakerId == actorId, excluded,
            r.ExclusionReason?.ToString(), officer ? r.ExcludedByName : null,
            mark is null || excluded ? null : Leak(r, mark, byId),
            r.HasAudio && (!excluded || officer), excluded && !officer ? null : r.Peaks);
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
