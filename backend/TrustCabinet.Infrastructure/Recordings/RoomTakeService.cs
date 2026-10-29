using System.Globalization;
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
/// ADR-0041 — the room mic, as in AI-eCabinet. While it is on, the Secretary's browser streams the room as 16 kHz PCM
/// in parts of a fixed size, so a part's place in the file follows from its number and sending one again is harmless.
/// Switching it off saves the take: appended to the meeting's one room recording, which is then transcribed again as a
/// whole, so the transcript reads as one meeting and its voices are told apart across takes.
///
/// The take is personal data from its first byte: the participants are told before the meeting's first take, the
/// purpose and retention date are those of the recording it goes into, and a take the mic heard nothing in is dropped.
/// </summary>
public sealed class RoomTakeService(IMeetingRepository meetings, IRecordingRepository recordings,
    ITranscriptRepository transcripts, IRecordingStorage storage, IAuditService audit, ISpeechChangePublisher publisher,
    TimeProvider clock, IOptions<RecordingOptions> options, ILogger<RoomTakeService> logger) : IRoomTakeService
{
    private static readonly TimeSpan Vietnam = TimeSpan.FromHours(7);

    private static bool PersistenceError(Exception ex) => ex is DbUpdateException or NpgsqlException or TimeoutException;

    private long MaxBytes => options.Value.MaxUploadMegabytes * 1024L * 1024;

    public async Task<RecordingOutcome> BeginAsync(Guid meetingId, Guid actorId, RoomTakeStart start, CancellationToken ct = default)
    {
        string? path = null;
        var kept = false;
        try
        {
            var outcome = await meetings.InTransactionAsync(async token =>
            {
                // The meeting's row lock puts one take's opening after another's, and after a take being saved.
                var meeting = await meetings.FindWorkflowAsync(meetingId, true, token);
                if (!await meetings.IsUserActiveAsync(actorId, token)) return new RecordingOutcome(RecordingFailure.Inactive);
                if (meeting is null || !RecordingRules.CanListen(meeting, actorId)) return new(RecordingFailure.NotFound);
                if (!RecordingRules.IsSecretary(meeting, actorId)) return new(RecordingFailure.Forbidden);
                if (!RecordingRules.HasRoom(meeting.Mode)) return new(RecordingFailure.NoRoom);
                if (meeting.Status != MeetingStatus.InProgress) return new(RecordingFailure.NotRunning);
                var listed = await recordings.ListAsync(meetingId, token);
                if (listed.Any(r => r is { Source: RecordingSource.BrowserCapture, Status: RecordingStatus.Pending }))
                    return new(RecordingFailure.TakeOpen);

                var room = await recordings.FindRoomRecordingAsync(meetingId, false, token);
                var errors = new Dictionary<string, string[]>();
                if (room is null && !start.ConsentConfirmed)
                    errors["consentConfirmed"] = ["Phải xác nhận đã thông báo cho người dự và không ai phản đối trước khi ghi âm."];
                if (start.ConsentPurpose?.Length > 1000) errors["consentPurpose"] = ["Mục đích không quá 1000 ký tự."];
                if (errors.Count > 0) return new(RecordingFailure.Invalid, Errors: errors);

                var now = clock.GetUtcNow();
                var purpose = room?.ConsentPurpose
                    ?? (string.IsNullOrWhiteSpace(start.ConsentPurpose) ? RecordingRules.DefaultPurpose : start.ConsentPurpose.Trim());
                path = storage.Create(".wav");
                var take = new Recording
                {
                    MeetingId = meetingId, Source = RecordingSource.BrowserCapture, Status = RecordingStatus.Pending,
                    FileName = $"ghi-am-phong-{now.ToOffset(Vietnam).ToString("dd-MM-yyyy-HHmm", CultureInfo.InvariantCulture)}.wav",
                    StoragePath = path, ContentType = "audio/wav", ConsentObtainedAt = room?.ConsentObtainedAt ?? now,
                    ConsentPurpose = purpose, RetainUntil = now.AddDays(options.Value.RetentionDays), Takes = [],
                    CreatedById = actorId, CreatedAt = now, UpdatedAt = now,
                };
                recordings.Add(take);
                await audit.RecordAsync(AuditActions.RecordingTakeStarted, AuditActions.RecordingObjectType, take.Id, actorId,
                    new { meetingId, purpose, firstTake = room is null }, token);
                await recordings.SaveChangesAsync(token);
                return new RecordingOutcome(Data: new RoomTakeOpened(take.Id, options.Value.RoomPartBytes, MaxBytes),
                    RecordingId: take.Id);
            }, ct);
            kept = outcome.Failure == RecordingFailure.None;
            if (kept) await PublishAsync(meetingId, ct);
            return outcome;
        }
        catch (DbUpdateException ex) when (ex.InnerException is PostgresException { SqlState: PostgresErrorCodes.UniqueViolation })
        {
            return new(RecordingFailure.TakeOpen);
        }
        catch (Exception ex) when (PersistenceError(ex) || ex is IOException)
        {
            logger.LogError(ex, "Failed to open a room-mic take for {MeetingId}", meetingId);
            return new(RecordingFailure.SaveFailed);
        }
        finally
        {
            if (!kept && path is not null) DeleteQuietly(path);
        }
    }

    public async Task<RecordingOutcome> AppendAsync(Guid recordingId, Guid actorId, int part, Stream content, long? length,
        bool final, CancellationToken ct = default)
    {
        var size = options.Value.RoomPartBytes;
        if (part < 1 || length > size || (!final && length is { } declared && declared != size)) return new(RecordingFailure.BadPart);
        // A part is small; it is read whole before the take's row is locked, so a slow upload holds no lock.
        var data = new byte[size + 1];
        var read = await content.ReadAtLeastAsync(data, data.Length, throwOnEndOfStream: false, ct);
        if (read > size || (!final && read != size) || (part == 1 && !RecordingRules.IsRoomWavHeader(data.AsSpan(0, read))))
            return new(RecordingFailure.BadPart);
        try
        {
            return await meetings.InTransactionAsync(async token =>
            {
                var take = await recordings.FindAsync(recordingId, true, token);
                if (!await meetings.IsUserActiveAsync(actorId, token)) return new RecordingOutcome(RecordingFailure.Inactive);
                if (take is null || take.CreatedById != actorId || !RecordingRules.IsRoomMic(take)) return new(RecordingFailure.NotFound);
                var meeting = await meetings.FindWorkflowAsync(take.MeetingId, false, token);
                if (meeting is null || !RecordingRules.CanListen(meeting, actorId)) return new(RecordingFailure.NotFound);
                if (take.Status != RecordingStatus.Pending) return new(RecordingFailure.TakeClosed);
                var offset = (part - 1L) * size;
                // In order, and nothing after a short last part: the file never has a hole.
                if (offset > take.ByteSize || (take.ByteSize % size != 0 && offset >= take.ByteSize)) return new(RecordingFailure.BadPart);
                if (offset + read > MaxBytes) return new(RecordingFailure.TooLarge);
                await storage.WriteAtAsync(take.StoragePath, offset, new MemoryStream(data, 0, read), size, token);
                if (final) storage.Truncate(take.StoragePath, offset + read);
                take.ByteSize = final ? offset + read : Math.Max(take.ByteSize, offset + read);
                take.DurationMs = RecordingRules.PcmMs(take.ByteSize);
                take.UpdatedAt = clock.GetUtcNow();
                await recordings.SaveChangesAsync(token);
                return new RecordingOutcome(RecordingId: take.Id);
            }, ct);
        }
        catch (Exception ex) when (PersistenceError(ex) || ex is IOException)
        {
            logger.LogError(ex, "Failed to write part {Part} of take {RecordingId}", part, recordingId);
            return new(RecordingFailure.SaveFailed);
        }
    }

    public async Task<RecordingOutcome> CompleteAsync(Guid recordingId, Guid actorId, CancellationToken ct = default)
    {
        try
        {
            var found = await recordings.FindAsync(recordingId, false, ct);
            if (found is null) return new(RecordingFailure.NotFound);
            var (outcome, obsolete) = await meetings.InTransactionAsync(async token =>
            {
                var meeting = await meetings.FindWorkflowAsync(found.MeetingId, true, token);
                if (!await meetings.IsUserActiveAsync(actorId, token)) return (new RecordingOutcome(RecordingFailure.Inactive), Array.Empty<string>());
                if (meeting is null || !RecordingRules.CanListen(meeting, actorId)) return (new(RecordingFailure.NotFound), []);
                var take = await recordings.FindAsync(recordingId, true, token);
                if (take is null || !RecordingRules.IsRoomMic(take)) return (new(RecordingFailure.NotFound), []);
                // Its maker switches it off; any Secretary saves one left open on a laptop that is not coming back.
                if (take.CreatedById != actorId && !RecordingRules.IsSecretary(meeting, actorId)) return (new(RecordingFailure.Forbidden), []);
                // Saved by an earlier call whose answer was lost: the same answer again.
                if (take.Status == RecordingStatus.Available)
                    return (new RecordingOutcome(Data: new RoomTakeSaved(take.Id, false), RecordingId: take.Id), []);
                if (take.Status != RecordingStatus.Pending) return (new(RecordingFailure.TakeClosed), []);
                return await SaveAsync(take, actorId, token);
            }, ct);
            foreach (var path in obsolete) DeleteQuietly(path);
            if (outcome.Failure == RecordingFailure.None) await PublishAsync(found.MeetingId, ct);
            return outcome;
        }
        catch (Exception ex) when (PersistenceError(ex) || ex is IOException)
        {
            logger.LogError(ex, "Failed to save take {RecordingId}", recordingId);
            return new(RecordingFailure.SaveFailed);
        }
    }

    public async Task<int> CompleteIdleAsync(CancellationToken ct = default)
    {
        var saved = 0;
        var before = clock.GetUtcNow().AddHours(-options.Value.IdleTakeHours);
        foreach (var id in await recordings.ListIdleTakesAsync(before, 20, ct))
        {
            var found = await recordings.FindAsync(id, false, ct);
            if (found is null) continue;
            var (_, obsolete) = await meetings.InTransactionAsync(async token =>
            {
                _ = await meetings.FindWorkflowAsync(found.MeetingId, true, token);
                var take = await recordings.FindAsync(id, true, token);
                if (take is not { Status: RecordingStatus.Pending } || take.UpdatedAt >= before)
                    return (new RecordingOutcome(), Array.Empty<string>());
                return await SaveAsync(take, null, token);
            }, ct);
            foreach (var path in obsolete) DeleteQuietly(path);
            await PublishAsync(found.MeetingId, ct);
            saved++;
        }
        return saved;
    }

    /// <summary>
    /// Saves a take under the meeting's lock. Returns the files that are no longer anyone's, deleted by the caller once
    /// the transaction has committed, so a rollback never leaves a row pointing at bytes that are gone.
    /// </summary>
    private async Task<(RecordingOutcome, string[])> SaveAsync(Recording take, Guid? actorId, CancellationToken ct)
    {
        var now = clock.GetUtcNow();
        // Whole milliseconds of PCM (under 1 ms dropped at the end): a take's place in the room recording is kept in ms,
        // so its bytes start and end exactly there, and one can be cut out again without touching its neighbours.
        var bytesPerMs = RecordingRules.RoomBytesPerSecond / 1000;
        take.ByteSize = RecordingRules.WavHeaderBytes
            + Math.Max(0, take.ByteSize - RecordingRules.WavHeaderBytes) / bytesPerMs * bytesPerMs;
        if (take.ByteSize <= RecordingRules.WavHeaderBytes)
        {
            // The mic heard nothing: there is no one's voice to keep.
            recordings.Remove(take);
            await audit.RecordAsync(AuditActions.RecordingTakeSaved, AuditActions.RecordingObjectType, take.Id, actorId,
                new { take.MeetingId, kept = false }, ct);
            await recordings.SaveChangesAsync(ct);
            return (new(Data: new RoomTakeSaved(null, false)), [take.StoragePath]);
        }
        storage.Truncate(take.StoragePath, take.ByteSize);
        storage.SealWav(take.StoragePath);
        var takeMs = RecordingRules.PcmMs(take.ByteSize);

        var room = await recordings.FindRoomRecordingAsync(take.MeetingId, true, ct);
        if (room is null || room.ByteSize + take.ByteSize - RecordingRules.WavHeaderBytes > MaxBytes)
        {
            // The meeting's first take, or the room recording is full: this take starts the next one.
            take.Status = RecordingStatus.Available;
            take.Takes = [new(0, takeMs, take.CreatedAt)];
            take.DurationMs = takeMs;
            take.UpdatedAt = now;
            transcripts.Add(new Transcript { RecordingId = take.Id, CreatedAt = now, UpdatedAt = now });
            await audit.RecordAsync(AuditActions.RecordingTakeSaved, AuditActions.RecordingObjectType, take.Id, actorId,
                new { take.MeetingId, kept = true, takeMs }, ct);
            await recordings.SaveChangesAsync(ct);
            return (new(Data: new RoomTakeSaved(take.Id, false), RecordingId: take.Id), []);
        }

        var merged = await storage.ConcatAsync(room.StoragePath, take.StoragePath, RecordingRules.WavHeaderBytes, ct);
        try
        {
            storage.SealWav(merged);
            var previous = room.StoragePath;
            room.Takes = [.. room.Takes ?? [], new(RecordingRules.PcmMs(room.ByteSize), takeMs, take.CreatedAt)];
            room.StoragePath = merged;
            room.ByteSize += take.ByteSize - RecordingRules.WavHeaderBytes;
            room.DurationMs = RecordingRules.PcmMs(room.ByteSize);
            room.UpdatedAt = now;
            // The whole is transcribed again: a run already busy on the shorter file throws its result away.
            var transcript = await transcripts.FindByRecordingAsync(room.Id, true, ct);
            if (transcript is null) transcripts.Add(new Transcript { RecordingId = room.Id, CreatedAt = now, UpdatedAt = now });
            else
            {
                transcript.Status = TranscriptStatus.Pending;
                transcript.ErrorMessage = "";
                transcript.ProgressPercent = 0;
                transcript.UpdatedAt = now;
            }
            recordings.Remove(take);
            await audit.RecordAsync(AuditActions.RecordingTakeSaved, AuditActions.RecordingObjectType, room.Id, actorId,
                new { take.MeetingId, kept = true, appended = true, takeId = take.Id, takeMs, totalMs = room.DurationMs }, ct);
            await recordings.SaveChangesAsync(ct);
            return (new(Data: new RoomTakeSaved(room.Id, true), RecordingId: room.Id), [previous, take.StoragePath]);
        }
        catch
        {
            DeleteQuietly(merged);
            throw;
        }
    }

    public async Task<RecordingOutcome> DeleteTakeAsync(Guid recordingId, int take, Guid actorId, CancellationToken ct = default)
    {
        string? cut = null;
        var committed = false;
        try
        {
            var found = await recordings.FindAsync(recordingId, false, ct);
            if (found is null) return new(RecordingFailure.NotFound);
            var (outcome, previous) = await meetings.InTransactionAsync(async token =>
            {
                // The meeting row lock, as for saving a take: a take is never appended while another is cut out.
                var meeting = await meetings.FindWorkflowAsync(found.MeetingId, true, token);
                if (!await meetings.IsUserActiveAsync(actorId, token)) return (new RecordingOutcome(RecordingFailure.Inactive), (string?)null);
                if (meeting is null || !RecordingRules.CanListen(meeting, actorId)) return (new(RecordingFailure.NotFound), null);
                var recording = await recordings.FindAsync(recordingId, true, token);
                if (recording is null || recording.Status != RecordingStatus.Available || recording.Takes is not { } takes
                    || take < 1 || take > takes.Count) return (new(RecordingFailure.NotFound), null);
                if (!RecordingRules.IsSecretary(meeting, actorId)) return (new(RecordingFailure.Forbidden), null);
                if (takes.Count == 1) return (new(RecordingFailure.LastTake), null);

                var now = clock.GetUtcNow();
                var gone = takes[take - 1];
                var bytesPerMs = RecordingRules.RoomBytesPerSecond / 1000;
                var from = RecordingRules.WavHeaderBytes + gone.OffsetMs * bytesPerMs;
                // The last take runs to the end of the file, whatever rounding its length went through.
                var length = take == takes.Count ? recording.ByteSize - from : gone.DurationMs * bytesPerMs;
                cut = await storage.CutAsync(recording.StoragePath, from, length, token);
                storage.SealWav(cut);
                var old = recording.StoragePath;
                recording.StoragePath = cut;
                recording.ByteSize -= length;
                recording.DurationMs = RecordingRules.PcmMs(recording.ByteSize);
                recording.Takes = [.. takes.Take(take - 1), .. takes.Skip(take).Select(t => t with { OffsetMs = t.OffsetMs - gone.DurationMs })];
                recording.UpdatedAt = now;
                var transcript = await transcripts.FindByRecordingAsync(recording.Id, true, token);
                var lines = transcript is null ? 0 : CutTranscript(transcript, gone, now);
                await audit.RecordAsync(AuditActions.RecordingTakeDeleted, AuditActions.RecordingObjectType, recording.Id, actorId,
                    new { recording.MeetingId, take, gone.StartedAt, gone.DurationMs, lines, totalMs = recording.DurationMs }, token);
                await recordings.SaveChangesAsync(token);
                return (new RecordingOutcome(RecordingId: recording.Id), old);
            }, ct);
            committed = true;
            if (previous is not null) DeleteQuietly(previous);
            if (outcome.Failure == RecordingFailure.None) await PublishAsync(found.MeetingId, ct);
            return outcome;
        }
        catch (Exception ex) when (PersistenceError(ex) || ex is IOException)
        {
            logger.LogError(ex, "Failed to erase take {Take} of {RecordingId}", take, recordingId);
            return new(RecordingFailure.SaveFailed);
        }
        finally
        {
            if (!committed && cut is not null) DeleteQuietly(cut);
        }
    }

    /// <summary>
    /// Takes the erased take's lines out and moves the later ones up by its length; edits, names and positions stay. A
    /// transcript still being made is made again from the shorter file instead: its result would place lines wrongly.
    /// </summary>
    private static int CutTranscript(Transcript transcript, RecordingTake gone, DateTimeOffset now)
    {
        transcript.UpdatedAt = now;
        if (transcript.Status is TranscriptStatus.Pending or TranscriptStatus.Processing)
        {
            transcript.Status = TranscriptStatus.Pending;
            transcript.ErrorMessage = "";
            transcript.ProgressPercent = 0;
            return 0;
        }
        var start = gone.OffsetMs;
        var end = gone.OffsetMs + gone.DurationMs;
        var dropped = transcript.Segments.Where(s => s.StartMs >= start && s.StartMs < end).ToList();
        foreach (var segment in dropped) transcript.Segments.Remove(segment);
        foreach (var segment in transcript.Segments)
        {
            if (segment.StartMs >= end)
            {
                segment.StartMs -= gone.DurationMs;
                segment.EndMs -= gone.DurationMs;
            }
            // A line running on past the take's start ends where the take began, or later by what is left after it.
            else if (segment.EndMs > start)
                segment.EndMs = segment.EndMs > end ? segment.EndMs - gone.DurationMs : start;
        }
        var spoke = transcript.Segments.GroupBy(s => s.SpeakerLabel).ToDictionary(g => g.Key, g => g.Sum(s => s.EndMs - s.StartMs));
        foreach (var speaker in transcript.Speakers.ToList())
        {
            if (spoke.TryGetValue(speaker.Label, out var ms)) speaker.SpeechMs = ms;
            // A voice heard only in the erased take is gone with it, name and suggestion included.
            else transcript.Speakers.Remove(speaker);
        }
        return dropped.Count;
    }

    private async Task PublishAsync(Guid meetingId, CancellationToken ct)
    {
        try
        {
            using var timeout = CancellationTokenSource.CreateLinkedTokenSource(ct);
            timeout.CancelAfter(TimeSpan.FromSeconds(2));
            await publisher.PublishAsync(new(Guid.CreateVersion7(), meetingId, RecordingRules.RecordingsChanged, clock.GetUtcNow()),
                timeout.Token);
        }
        catch (Exception ex) when (ex is not OperationCanceledException || !ct.IsCancellationRequested)
        {
            logger.LogWarning(ex, "Could not publish recordings change for {MeetingId}", meetingId);
        }
    }

    private void DeleteQuietly(string path)
    {
        try { storage.Delete(path); }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            logger.LogError(ex, "Could not delete recording file {Path}", path);
        }
    }
}
