using System.Buffers.Binary;
using System.Collections.Concurrent;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using TrustCabinet.Domain.Entities;
using TrustCabinet.Domain.Enums;
using TrustCabinet.Domain.Recordings;

namespace TrustCabinet.Infrastructure.Recordings;

/// <summary>
/// ADR-0062 — after a hybrid meeting ends, the server lays its room recording and every online attendee's sentences on
/// the real clock and mixes them into one WAV, listed with the meeting's recordings as "Bản ghi tổng hợp". Built once the
/// sources settle (no take open, nothing new for a minute), and built again whenever one of them changes later: a late
/// sentence, one taken out of the record, a take cut. A Secretary who erases it is not overridden.
///
/// Mixed in blocks of ten seconds, so a meeting of hours never sits in memory: the room recording is read where the
/// block needs it, and a sentence (a minute at most) is loaded when the block reaches it and dropped once passed.
/// </summary>
public sealed class CombinedRecordingService(TrustCabinetDbContext db, IRecordingStorage storage, ISpeechChangePublisher publisher,
    TimeProvider clock, IOptions<RecordingOptions> options, ILogger<CombinedRecordingService> logger) : ICombinedRecordingService
{
    private const int BlockSamples = CombinedRecordingRules.SampleRate * 10;

    /// <summary>A build that failed is not retried until its sources change again, or the worker would spin on it.</summary>
    private static readonly ConcurrentDictionary<Guid, DateTimeOffset> Failed = new();

    private sealed record RoomPart(DateTimeOffset StartedAt, long DurationMs, string StoragePath, long OffsetMs);

    private sealed record Clip(DateTimeOffset StartedAt, long DurationMs, string AudioPath);

    /// <param name="Changed">The last change to any source. The combined row's UpdatedAt holds the one it was built from,
    /// not when it was built: a source that changes while it is being mixed is then still newer, and gets its rebuild.</param>
    private sealed record Plan(Guid MeetingId, Guid? ExistingId, DateTimeOffset Changed, IReadOnlyList<RoomPart> Room,
        IReadOnlyList<Clip> Clips, Recording Template);

    public async Task<bool> BuildNextAsync(CancellationToken ct = default)
    {
        var now = clock.GetUtcNow();
        var settled = now - CombinedRecordingRules.Settle;
        var since = now - CombinedRecordingRules.Window;
        var meetings = await db.Meetings.AsNoTracking()
            .Where(m => m.Mode == MeetingMode.Hybrid && m.Status == MeetingStatus.Ended && m.Sensitivity != Sensitivity.Secret
                && m.ActualEndedAt != null && m.ActualEndedAt < settled && m.ActualEndedAt > since)
            .OrderBy(m => m.ActualEndedAt).Select(m => m.Id).ToListAsync(ct);
        foreach (var meetingId in meetings)
        {
            var plan = await PlanAsync(meetingId, settled, ct);
            if (plan is null) continue;
            if (Failed.TryGetValue(meetingId, out var failedAt) && failedAt >= plan.Changed) continue;
            try
            {
                await BuildAsync(plan, ct);
                Failed.TryRemove(meetingId, out _);
            }
            catch (Exception ex) when (ex is not OperationCanceledException || !ct.IsCancellationRequested)
            {
                logger.LogError(ex, "Could not build the combined recording of meeting {MeetingId}", meetingId);
                Failed[meetingId] = plan.Changed;
            }
            return true;
        }
        return false;
    }

    /// <summary>What to mix for this meeting, or null when there is nothing to (re)build yet.</summary>
    private async Task<Plan?> PlanAsync(Guid meetingId, DateTimeOffset settled, CancellationToken ct)
    {
        var recordings = await db.Recordings.AsNoTracking().Where(r => r.MeetingId == meetingId).ToListAsync(ct);
        // A room take still streaming will be appended to the room recording: wait for it.
        if (recordings.Any(r => r.Source == RecordingSource.BrowserCapture && r.Status == RecordingStatus.Pending)) return null;
        var existing = recordings.Where(r => r.Source == RecordingSource.Combined)
            .OrderByDescending(r => r.CreatedAt).FirstOrDefault();
        if (existing is { Status: RecordingStatus.Deleted }) return null;

        var rooms = recordings.Where(RecordingRules.IsRoomMic).Where(r => r.Status == RecordingStatus.Available).ToList();
        var clips = await db.SpeechClips.AsNoTracking()
            .Where(c => c.MeetingId == meetingId && c.AudioPath != null && c.ExcludedAt == null && c.DurationMs > 0)
            .OrderBy(c => c.StartedAt)
            .Select(c => new Clip(c.StartedAt, c.DurationMs, c.AudioPath!))
            .ToListAsync(ct);
        // Only a meeting heard both in the room and online: either alone already plays as one track.
        if (rooms.Count == 0 || clips.Count == 0) return null;

        var clipChanged = await db.SpeechClips.Where(c => c.MeetingId == meetingId).MaxAsync(c => (DateTimeOffset?)c.UpdatedAt, ct);
        var roomChanged = recordings.Where(r => r.Source != RecordingSource.Combined).Max(r => r.UpdatedAt);
        var changed = clipChanged is { } c2 && c2 > roomChanged ? c2 : roomChanged;
        if (changed > settled) return null;
        if (existing is not null && existing.UpdatedAt >= changed) return null;

        var room = rooms.SelectMany(r => r.Takes!.Select(t => new RoomPart(t.StartedAt, t.DurationMs, r.StoragePath, t.OffsetMs)))
            .Where(p => p.DurationMs > 0).OrderBy(p => p.StartedAt).ToList();
        if (room.Count == 0) return null;
        return new(meetingId, existing?.Id, changed, room, clips, rooms[0]);
    }

    private async Task BuildAsync(Plan plan, CancellationToken ct)
    {
        var origin = Min(plan.Room.Min(p => p.StartedAt), plan.Clips.Min(c => c.StartedAt));
        var end = Max(plan.Room.Max(p => p.StartedAt.AddMilliseconds(p.DurationMs)),
            plan.Clips.Max(c => c.StartedAt.AddMilliseconds(c.DurationMs)));
        long Sample(DateTimeOffset at) => (long)Math.Round((at - origin).TotalMilliseconds * CombinedRecordingRules.SampleRate / 1000);
        var total = Sample(end);

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
        var durationMs = total * 1000 / CombinedRecordingRules.SampleRate;
        string? replaced = null;
        var kept = await db.Database.CreateExecutionStrategy().ExecuteAsync(async () =>
        {
            await using var transaction = await db.Database.BeginTransactionAsync(ct);
            Recording? row = null;
            if (plan.ExistingId is { } id)
            {
                await db.Recordings.FromSqlInterpolated($"SELECT * FROM recordings WHERE id = {id} FOR UPDATE").AsNoTracking().ToListAsync(ct);
                row = await db.Recordings.SingleOrDefaultAsync(r => r.Id == id, ct);
                // Erased while it was being mixed: the Secretary's choice stands.
                if (row is null || row.Status == RecordingStatus.Deleted) return false;
                replaced = row.StoragePath;
                row.StoragePath = path;
                row.ByteSize = byteSize;
                row.DurationMs = durationMs;
                row.Status = RecordingStatus.Available;
                row.UpdatedAt = plan.Changed;
            }
            else
            {
                db.Recordings.Add(new Recording
                {
                    MeetingId = plan.MeetingId, Source = RecordingSource.Combined, Status = RecordingStatus.Available,
                    FileName = CombinedRecordingRules.FileName(origin), StoragePath = path, ContentType = "audio/wav",
                    ByteSize = byteSize, DurationMs = durationMs,
                    ConsentObtainedAt = plan.Template.ConsentObtainedAt, ConsentPurpose = plan.Template.ConsentPurpose,
                    RetainUntil = now.AddDays(options.Value.RetentionDays), CreatedById = plan.Template.CreatedById,
                    CreatedAt = now, UpdatedAt = plan.Changed,
                });
            }
            await db.SaveChangesAsync(ct);
            await transaction.CommitAsync(ct);
            return true;
        });
        db.ChangeTracker.Clear();
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
    private async Task<short[]?> LoadClipAsync(Clip clip, CancellationToken ct)
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

    private static DateTimeOffset Min(DateTimeOffset a, DateTimeOffset b) => a < b ? a : b;
    private static DateTimeOffset Max(DateTimeOffset a, DateTimeOffset b) => a > b ? a : b;
}
