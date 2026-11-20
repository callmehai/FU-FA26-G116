using System.Buffers.Binary;
using System.Collections.Concurrent;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using TrustCabinet.Application.Abstractions;
using TrustCabinet.Application.Audit;
using TrustCabinet.Application.Meetings;
using TrustCabinet.Domain.Audit;
using TrustCabinet.Domain.Entities;
using TrustCabinet.Domain.Enums;
using TrustCabinet.Domain.Participants;

namespace TrustCabinet.Application.Recordings;

/// <summary>
/// ADR-0062 — after a hybrid meeting ends, the server lays its room recording and every online attendee's sentences on
/// the real clock and mixes them into one WAV, listed with the meeting's recordings as "Bản ghi tổng hợp". ADR-0069: an
/// online meeting gets one too, mixed from its attendees' sentences alone — it has no room — so it can be heard and
/// downloaded as one file. Built once the sources settle (no take open, nothing new for a minute), and built again
/// whenever one of them changes later: a late sentence, one taken out of the record, a take cut. A Secretary who erases
/// it is not overridden. Audio removed from a source never outlives it here (ADR-0067): erasing the room recording erases
/// this one, and a sentence deleted or a take cut past the rebuild window erases it too, as does a mix left with nothing
/// to mix: a hybrid one with only one side left, an online one with no sentence left.
///
/// Each stretch of sound keeps the real clock; silence between stretches is cut to two seconds (<see cref="CombinedTrack"/>),
/// and a track past twelve hours is not built.
///
/// Mixed in blocks of ten seconds, so a meeting of hours never sits in memory: the room recording is read where the
/// block needs it, and a sentence (a minute at most) is loaded when the block reaches it and dropped once passed.
/// </summary>
public sealed class CombinedRecordingService(IRecordingRepository recordingRows, ISpeechClipRepository clipRows,
    IMeetingRepository meetingRows, IUnitOfWork unitOfWork, IRecordingStorage storage, ISpeechChangePublisher publisher,
    IAuditService audit, TimeProvider clock, IOptions<RecordingOptions> options, ILogger<CombinedRecordingService> logger) : ICombinedRecordingService
{
    private const int BlockSamples = CombinedRecordingRules.SampleRate * 10;

    /// <summary>A build that failed is not retried until its sources change again, or the worker would spin on it.</summary>
    private static readonly ConcurrentDictionary<Guid, DateTimeOffset> Failed = new();

    private sealed record RoomPart(DateTimeOffset StartedAt, long DurationMs, string StoragePath, long OffsetMs);

    /// <param name="Changed">The last change to any source. The combined row's UpdatedAt holds the one it was built from,
    /// not when it was built: a source that changes while it is being mixed is then still newer, and gets its rebuild.</param>
    /// <param name="RetainUntil">When its earliest source goes (BR-347: the room's, counted from its first take; or a
    /// sentence's, if one came first), so the mix never keeps a voice longer than the source it was taken from.</param>
    /// <param name="Owner">Consent and owner, taken from the room recording; for an online meeting, the call room's
    /// recording notice every attendee confirmed before their mic was heard.</param>
    private sealed record Plan(Guid MeetingId, Guid? ExistingId, DateTimeOffset Changed, IReadOnlyList<RoomPart> Room,
        IReadOnlyList<CombinedClip> Clips, MixOwner Owner, DateTimeOffset RetainUntil);

    private sealed record MixOwner(DateTimeOffset? ConsentObtainedAt, string ConsentPurpose, Guid CreatedById);

    public async Task<bool> BuildNextAsync(CancellationToken ct = default)
    {
        var now = clock.GetUtcNow();
        var settled = now - CombinedRecordingRules.Settle;
        var since = now - CombinedRecordingRules.Window;
        var meetings = await unitOfWork.ExecuteAsync(token => recordingRows.ListCombinableMeetingsAsync(settled, since, token),
            TransactionMode.None, ct);
        foreach (var meeting in meetings)
        {
            var meetingId = meeting.Id;
            var (plan, stale) = await unitOfWork.ExecuteAsync(token => PlanAsync(meeting, settled, token), TransactionMode.None, ct);
            if (stale is { } staleId) await EraseStaleAsync(staleId, meetingId, ct);
            if (plan is null) continue;
            if (Failed.TryGetValue(meetingId, out var failedAt) && failedAt >= plan.Changed) continue;
            try
            {
                await BuildAsync(plan, ct);
                Failed.TryRemove(meetingId, out _);
            }
            catch (Exception ex) when (ex is not OperationCanceledException || !ct.IsCancellationRequested)
            {
                // A storage failure is logged as the database's own exception, as before the move (rule R8).
                logger.LogError(ex is PersistenceException { InnerException: { } inner } ? inner : ex,
                    "Could not build the combined recording of meeting {MeetingId}", meetingId);
                Failed[meetingId] = plan.Changed;
            }
            return true;
        }
        return false;
    }

    /// <summary>
    /// What to mix for this meeting, or null when there is nothing to (re)build yet; and the id of a mix left
    /// with nothing to mix (one side of a hybrid meeting, every sentence of an online one), which the caller erases in a
    /// transaction of its own.
    /// </summary>
    private async Task<(Plan? Plan, Guid? Stale)> PlanAsync(CombinableMeeting meeting, DateTimeOffset settled, CancellationToken ct)
    {
        var meetingId = meeting.Id;
        var online = meeting.Mode == MeetingMode.Online;
        var recordings = await recordingRows.ListByMeetingAsync(meetingId, ct);
        // A room take still streaming will be appended to the room recording: wait for it.
        if (recordings.Any(r => r.Source == RecordingSource.BrowserCapture && r.Status == RecordingStatus.Pending)) return (null, null);
        var existing = recordings.Where(r => r.Source == RecordingSource.Combined)
            .OrderByDescending(r => r.CreatedAt).FirstOrDefault();
        if (existing is { Status: RecordingStatus.Deleted }) return (null, null);

        // An online meeting has no room (ADR-0040): a room recording kept from before is not mixed into its track.
        var rooms = online ? []
            : recordings.Where(RecordingRules.IsRoomMic).Where(r => r.Status == RecordingStatus.Available).ToList();
        var clips = await clipRows.ListMixableAsync(meetingId, ct);
        // A hybrid meeting is mixed only when heard both in the room and online: either alone already plays as one track.
        // An online one needs one sentence. A mix built before that went (every sentence deleted or taken out, the room
        // erased) would keep that audio: it goes too.
        if (clips.Count == 0 || (!online && rooms.Count == 0))
            return (null, existing is { Status: RecordingStatus.Available } ? existing.Id : null);

        var clipChanged = await clipRows.FindLastChangeAsync(meetingId, ct);
        var roomChanged = rooms.Count == 0 ? DateTimeOffset.MinValue
            : recordings.Where(r => r.Source != RecordingSource.Combined).Max(r => r.UpdatedAt);
        var changed = clipChanged is { } c2 && c2 > roomChanged ? c2 : roomChanged;
        if (changed > settled) return (null, null);
        if (existing is not null && existing.UpdatedAt >= changed) return (null, null);

        var room = rooms.SelectMany(r => r.Takes!.Select(t => new RoomPart(t.StartedAt, t.DurationMs, r.StoragePath, t.OffsetMs)))
            .Where(p => p.DurationMs > 0).OrderBy(p => p.StartedAt).ToList();
        if (!online && room.Count == 0) return (null, null);
        var days = options.Value.RetentionDays;
        var fallback = clock.GetUtcNow().AddDays(days);
        var retainUntil = rooms.Select(r => r.RetainUntil ?? fallback).Append(clips.Min(c => c.CreatedAt).AddDays(days)).Min();
        var owner = online ? await OnlineOwnerAsync(meetingId, clips, ct)
            : new MixOwner(rooms[0].ConsentObtainedAt, rooms[0].ConsentPurpose, rooms[0].CreatedById);
        if (owner is null) return (null, null);
        return (new Plan(meetingId, existing?.Id, changed, room, clips, owner, retainUntil), null);
    }

    /// <summary>
    /// An online meeting's mix is listed under its Secretary (the organiser when it has none). Its consent is the call
    /// room's recording notice, which nobody's mic is heard before confirming; it dates from the meeting's start.
    /// </summary>
    private async Task<MixOwner?> OnlineOwnerAsync(Guid meetingId, IReadOnlyList<CombinedClip> clips, CancellationToken ct)
    {
        var meeting = await meetingRows.FindWorkflowAsync(meetingId, false, ct);
        if (meeting is null) return null;
        var secretary = meeting.Participants
            .Where(p => p.Role == MeetingRole.Secretary && ParticipantRules.Active(p)).Select(p => (Guid?)p.UserId).FirstOrDefault();
        return new(meeting.ActualStartedAt ?? clips.Min(c => c.StartedAt), RecordingRules.DefaultPurpose,
            secretary ?? meeting.CreatedById);
    }

    private async Task BuildAsync(Plan plan, CancellationToken ct)
    {
        var origin = plan.Room.Select(p => p.StartedAt).Concat(plan.Clips.Select(c => c.StartedAt)).Min();
        var track = new CombinedTrack([.. plan.Room.Select(p => (p.StartedAt, p.DurationMs)), .. plan.Clips.Select(c => (c.StartedAt, c.DurationMs))]);
        if (track.TotalMs > CombinedRecordingRules.MaxLength.TotalMilliseconds)
            throw new InvalidOperationException($"The combined recording would last {track.TotalMs} ms, past the limit.");
        var total = track.TotalSamples;
        long Sample(DateTimeOffset at) => track.SampleAt(at);

        var path = storage.Create(".wav");
        try
        {
            using (var header = new MemoryStream(CombinedRecordingRules.WavHeader()))
                await storage.WriteAtAsync(path, 0, header, RecordingRules.WavHeaderBytes, ct);
            await MixAsync(plan, path, total, Sample, ct);
            storage.SealWav(path);
        }
        catch
        {
            storage.Delete(path);
            throw;
        }

        var now = clock.GetUtcNow();
        var byteSize = RecordingRules.WavHeaderBytes + total * 2;
        var durationMs = track.TotalMs;
        string? replaced = null;
        bool kept;
        try
        {
            kept = await unitOfWork.ExecuteAsync(async token =>
            {
                Recording? row = null;
                if (plan.ExistingId is { } id)
                {
                    row = await recordingRows.LockAsync(id, token);
                    // Erased while it was being mixed: the Secretary's choice stands.
                    if (row is null || row.Status == RecordingStatus.Deleted) return false;
                    replaced = row.StoragePath;
                    row.StoragePath = path;
                    row.ByteSize = byteSize;
                    row.DurationMs = durationMs;
                    row.Status = RecordingStatus.Available;
                    row.RetainUntil = plan.RetainUntil;
                    row.UpdatedAt = plan.Changed;
                }
                else
                {
                    recordingRows.Add(new Recording
                    {
                        MeetingId = plan.MeetingId, Source = RecordingSource.Combined, Status = RecordingStatus.Available,
                        FileName = CombinedRecordingRules.FileName(origin), StoragePath = path, ContentType = "audio/wav",
                        ByteSize = byteSize, DurationMs = durationMs,
                        ConsentObtainedAt = plan.Owner.ConsentObtainedAt, ConsentPurpose = plan.Owner.ConsentPurpose,
                        RetainUntil = plan.RetainUntil, CreatedById = plan.Owner.CreatedById,
                        CreatedAt = now, UpdatedAt = plan.Changed,
                    });
                }
                await unitOfWork.SaveChangesAsync(token);
                return true;
            }, commitWhen: stored => stored, TransactionMode.ReadCommitted, ct);
        }
        catch
        {
            // Rolled back: no row points at the new mix, and nothing else would ever find it (personal data).
            storage.Delete(path);
            throw;
        }
        unitOfWork.DiscardChanges();
        if (!kept)
        {
            storage.Delete(path);
            return;
        }
        if (replaced is not null) storage.Delete(replaced);
        logger.LogInformation("Built the combined recording of meeting {MeetingId}: {Room} room takes, {Clips} online sentences, {Ms} ms",
            plan.MeetingId, plan.Room.Count, plan.Clips.Count, durationMs);
        await PublishAsync(plan.MeetingId, ct);
    }

    public Task<string?> SourceRemovedAsync(Guid meetingId, bool rebuild, Guid? actorId, CancellationToken ct = default)
        // Joins the caller's transaction, which commits it (IUnitOfWork); the row lock is held until then.
        => unitOfWork.ExecuteAsync<string?>(async token =>
        {
            var id = (await recordingRows.ListByMeetingAsync(meetingId, token))
                .Where(r => r.Source == RecordingSource.Combined && r.Status == RecordingStatus.Available)
                .Select(r => (Guid?)r.Id).FirstOrDefault();
            if (id is null) return null;
            var row = await recordingRows.LockAsync(id.Value, token);
            if (row is not { Status: RecordingStatus.Available }) return null;
            if (rebuild)
            {
                var meeting = await meetingRows.FindWorkflowAsync(meetingId, ct: token);
                if (meeting?.ActualEndedAt is { } ended && ended > clock.GetUtcNow() - CombinedRecordingRules.Window)
                {
                    // Older than any source: the next sweep mixes it again without the audio that went.
                    row.UpdatedAt = DateTimeOffset.UnixEpoch;
                    return null;
                }
            }
            return await MarkErasedAsync(row, actorId, rebuild ? "source_removed_after_window" : "source_erased", token);
        }, TransactionMode.ReadCommitted, ct);

    /// <summary>A mix left with nothing to mix: erased in a transaction of its own, file after commit.</summary>
    private async Task EraseStaleAsync(Guid id, Guid meetingId, CancellationToken ct)
    {
        var path = await unitOfWork.ExecuteAsync<string?>(async token =>
        {
            var row = await recordingRows.LockAsync(id, token);
            if (row is not { Status: RecordingStatus.Available }) return null;
            return await MarkErasedAsync(row, null, "source_erased", token);
        }, TransactionMode.ReadCommitted, ct);
        unitOfWork.DiscardChanges();
        if (path is null) return;
        DeleteQuietly(path);
        await PublishAsync(meetingId, ct);
    }

    /// <summary>As <c>RecordingService</c> erases one: the row stays as evidence, the bytes go after the commit.</summary>
    private async Task<string> MarkErasedAsync(Recording row, Guid? actorId, string reason, CancellationToken ct)
    {
        var now = clock.GetUtcNow();
        row.Status = RecordingStatus.Deleted;
        row.DeletedAt = now;
        row.UpdatedAt = now;
        await audit.RecordAsync(AuditActions.RecordingErased, AuditActions.RecordingObjectType, row.Id, actorId,
            new { row.MeetingId, row.FileName, reason }, ct);
        await unitOfWork.SaveChangesAsync(ct);
        return row.StoragePath;
    }

    private void DeleteQuietly(string path)
    {
        try { storage.Delete(path); }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            // The retention job's orphan sweep deletes it later: no row points at it any more.
            logger.LogError(ex, "Could not delete combined recording file {Path}", path);
        }
    }

    private async Task MixAsync(Plan plan, string path, long total, Func<DateTimeOffset, long> sample, CancellationToken ct)
    {
        var acc = new int[BlockSamples];
        var bytes = new byte[BlockSamples * 2];
        var active = new List<(long Start, short[] Samples)>();
        var nextClip = 0;
        var rooms = plan.Room.Select(p => (Start: sample(p.StartedAt), Length: p.DurationMs * CombinedRecordingRules.SampleRate / 1000,
            p.StoragePath, FileOffset: RecordingRules.WavHeaderBytes + p.OffsetMs * CombinedRecordingRules.BytesPerMs)).ToList();
        var files = new Dictionary<string, Stream>();
        try
        {
            for (long from = 0; from < total; from += BlockSamples)
            {
                ct.ThrowIfCancellationRequested();
                var count = (int)Math.Min(BlockSamples, total - from);
                Array.Clear(acc, 0, count);
                var to = from + count;

                foreach (var part in rooms)
                {
                    var start = Math.Max(from, part.Start);
                    var stop = Math.Min(to, part.Start + part.Length);
                    if (stop <= start) continue;
                    if (!files.TryGetValue(part.StoragePath, out var file))
                        files[part.StoragePath] = file = storage.OpenRead(part.StoragePath);
                    var need = (int)(stop - start) * 2;
                    file.Seek(part.FileOffset + (start - part.Start) * 2, SeekOrigin.Begin);
                    var read = await ReadFullyAsync(file, bytes.AsMemory(0, need), ct);
                    for (var i = 0; i + 1 < read; i += 2)
                        acc[start - from + i / 2] += BinaryPrimitives.ReadInt16LittleEndian(bytes.AsSpan(i));
                }

                while (nextClip < plan.Clips.Count && sample(plan.Clips[nextClip].StartedAt) < to)
                {
                    var clip = plan.Clips[nextClip++];
                    var pcm = await LoadClipAsync(clip, ct);
                    if (pcm is not null) active.Add((sample(clip.StartedAt), pcm));
                }
                foreach (var (start, pcm) in active)
                {
                    var first = Math.Max(from, start);
                    var last = Math.Min(to, start + pcm.Length);
                    for (var s = first; s < last; s++) acc[s - from] += pcm[s - start];
                }
                active.RemoveAll(a => a.Start + a.Samples.Length <= to);

                for (var i = 0; i < count; i++)
                    BinaryPrimitives.WriteInt16LittleEndian(bytes.AsSpan(i * 2), CombinedRecordingRules.Clip(acc[i]));
                using var block = new MemoryStream(bytes, 0, count * 2);
                await storage.WriteAtAsync(path, RecordingRules.WavHeaderBytes + from * 2, block, count * 2, ct);
            }
        }
        finally
        {
            foreach (var file in files.Values) await file.DisposeAsync();
        }
    }

    /// <summary>One sentence's samples; null (left out) when its audio is gone or is not 16 kHz mono PCM.</summary>
    private async Task<short[]?> LoadClipAsync(CombinedClip clip, CancellationToken ct)
    {
        try
        {
            await using var stream = storage.OpenRead(clip.AudioPath);
            using var copy = new MemoryStream();
            await stream.CopyToAsync(copy, ct);
            return CombinedRecordingRules.ReadPcm(copy.GetBuffer().AsSpan(0, (int)copy.Length));
        }
        catch (Exception ex) when (ex is FileNotFoundException or DirectoryNotFoundException)
        {
            return null;
        }
    }

    private static async Task<int> ReadFullyAsync(Stream stream, Memory<byte> buffer, CancellationToken ct)
    {
        var total = 0;
        while (total < buffer.Length)
        {
            var read = await stream.ReadAsync(buffer[total..], ct);
            if (read == 0) break;
            total += read;
        }
        return total;
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
}
