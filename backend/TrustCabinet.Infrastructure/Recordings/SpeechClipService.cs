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
/// into text inside our network (ADR-0026) and the audio is dropped when the request ends. What is kept is the text,
/// when it was said and who said it — the owner of the mic, never a guess — and it goes when the recordings'
/// retention period ends.
/// </summary>
public sealed class SpeechClipService(IMeetingRepository meetings, TrustCabinetDbContext db, ISpeechEngine engine,
    SpeechClipGate gate, ISpeechChangePublisher publisher, IAuditService audit, TimeProvider clock,
    IOptions<RecordingOptions> options, ILogger<SpeechClipService> logger) : ISpeechClipService
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
            var query = db.SpeechClips.AsNoTracking().Where(c => c.MeetingId == meetingId);
            if (since is { } after) query = query.Where(c => c.UpdatedAt > after);
            var items = await query.OrderBy(c => c.StartedAt).ThenBy(c => c.CreatedAt)
                .Select(c => new SpeechClipView(c.Id, c.SpeakerId, c.Speaker.FullName, c.StartedAt, c.DurationMs, c.Text,
                    c.UpdatedAt, c.EditedAt, c.EditedBy == null ? null : c.EditedBy.FullName,
                    secretary || c.SpeakerId == actorId))
                .ToListAsync(ct);
            return new(Data: new SpeechClipList(meetingId, meeting.CaptureMode.ToString(),
                SpeechClipRules.Capturing(meeting), secretary,
                SpeechClipRules.RunsRecord(meeting, actorId) && SpeechClipRules.ModeChangeable(meeting.Status), now, items));
        }
        catch (Exception ex) when (PersistenceError(ex))
        {
            logger.LogError(ex, "Failed to list speech clips of {MeetingId}", meetingId);
            return new(SpeechClipFailure.ReadFailed);
        }
    }

    public async Task<SpeechClipOutcome> CanCaptureAsync(Guid meetingId, Guid actorId, CancellationToken ct = default)
    {
        try
        {
            var (meeting, failure) = await ListenableMeetingAsync(meetingId, actorId, ct);
            if (meeting is null) return new(failure);
            return SpeechClipRules.Capturing(meeting) ? new() : new(SpeechClipFailure.NotCapturing);
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

        var text = await TranscribeAsync(upload, ct);
        if (text is null) return new(SpeechClipFailure.EngineUnavailable);
        // Silence, a cough, a door: nothing worth a line. The browser drops it; nothing is stored.
        if (text.Length == 0) return new();

        try
        {
            var now = clock.GetUtcNow();
            var clip = await meetings.InTransactionAsync(async token =>
            {
                var meeting = await meetings.FindWorkflowAsync(meetingId, false, token);
                if (meeting is null || !RecordingRules.CanListen(meeting, actorId)) return null;
                // A sentence finished just as the Chair ended the meeting was still said in it.
                if (meeting.CaptureMode != CaptureMode.PerMic || meeting.Status is not (MeetingStatus.InProgress or MeetingStatus.Ended))
                    return null;
                var row = new SpeechClip
                {
                    MeetingId = meetingId, SpeakerId = actorId, ClientClipId = upload.ClientClipId,
                    StartedAt = StartedAt(upload, meeting, now), DurationMs = upload.DurationMs,
                    Text = text.Length > SpeechClipRules.MaxTextLength ? text[..SpeechClipRules.MaxTextLength] : text,
                    CreatedAt = now, UpdatedAt = now,
                };
                db.SpeechClips.Add(row);
                await db.SaveChangesAsync(token);
                return row;
            }, ct);
            if (clip is null) return new(SpeechClipFailure.NotCapturing);
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
            return await ChangeClipAsync(clipId, actorId, async (clip, meeting, now, token) =>
            {
                if (clip.SpeakerId != actorId && !RecordingRules.IsSecretary(meeting, actorId))
                    return new(SpeechClipFailure.Forbidden);
                await audit.RecordAsync(AuditActions.SpeechClipDeleted, AuditActions.SpeechClipObjectType, clip.Id, actorId,
                    new { clip.MeetingId, clip.SpeakerId, byOwner = clip.SpeakerId == actorId }, token);
                db.SpeechClips.Remove(clip);
                return new();
            }, "ClipDeleted", ct);
        }
        catch (Exception ex) when (PersistenceError(ex))
        {
            logger.LogError(ex, "Failed to delete speech clip {ClipId}", clipId);
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
        return await db.SpeechClips.Where(c => c.CreatedAt < cutoff).ExecuteDeleteAsync(ct);
    }

    /// <summary>Null when the worker cannot be reached or is busy for too long; the browser keeps the sentence and retries.</summary>
    private async Task<string?> TranscribeAsync(SpeechClipUpload upload, CancellationToken ct)
    {
        if (!engine.IsConfigured) return null;
        if (!await gate.Slots.WaitAsync(gate.Wait, ct)) return null;
        try
        {
            using var audio = new MemoryStream(upload.Audio, writable: false);
            // Diarization off: the owner of the mic is the speaker, so there is nothing to tell apart.
            var result = await engine.TranscribeAsync(audio, upload.ContentType, diarize: false, ct);
            return string.Join(' ', result.Segments.Select(s => s.Text.Trim()).Where(t => t.Length > 0));
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
        var fallback = now.AddMilliseconds(-upload.DurationMs);
        var started = upload.StartedAt.ToUniversalTime();
        var earliest = (meeting.ActualStartedAt ?? fallback) - SpeechClipRules.ClockTolerance;
        return started < earliest || started > now + SpeechClipRules.ClockTolerance ? fallback : started;
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
                c.UpdatedAt, c.EditedAt, c.EditedBy == null ? null : c.EditedBy.FullName, true))
            .SingleOrDefaultAsync(ct);

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
