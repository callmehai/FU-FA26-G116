using Microsoft.EntityFrameworkCore;
using TrustCabinet.Domain.Entities;
using TrustCabinet.Domain.Enums;
using TrustCabinet.Domain.Recordings;

namespace TrustCabinet.Infrastructure.Recordings;

public sealed class RecordingRepository(TrustCabinetDbContext db) : IRecordingRepository
{
    public async Task<IReadOnlyList<Recording>> ListAsync(Guid meetingId, CancellationToken ct)
        => await db.Recordings.AsNoTracking().Include(r => r.CreatedBy).Include(r => r.Transcript)
            .Where(r => r.MeetingId == meetingId)
            .OrderByDescending(r => r.CreatedAt).ThenByDescending(r => r.Id).ToListAsync(ct);

    public async Task<Recording?> FindAsync(Guid recordingId, bool forUpdate, CancellationToken ct)
    {
        if (forUpdate)
            await db.Recordings.FromSqlInterpolated($"SELECT * FROM recordings WHERE id = {recordingId} FOR UPDATE")
                .AsNoTracking().ToListAsync(ct);
        var query = db.Recordings.Include(r => r.CreatedBy).Where(r => r.Id == recordingId);
        return await (forUpdate ? query : query.AsNoTracking()).SingleOrDefaultAsync(ct);
    }

    public async Task<IReadOnlyList<Guid>> ListExpiredAsync(DateTimeOffset now, int take, CancellationToken ct)
        => await db.Recordings.AsNoTracking()
            .Where(r => r.Status != RecordingStatus.Deleted && r.RetainUntil != null && r.RetainUntil < now)
            .OrderBy(r => r.RetainUntil).Select(r => r.Id).Take(take).ToListAsync(ct);

    public async Task<Recording?> FindRoomRecordingAsync(Guid meetingId, bool forUpdate, CancellationToken ct)
    {
        var id = await db.Recordings.AsNoTracking()
            .Where(r => r.MeetingId == meetingId && r.Source == RecordingSource.BrowserCapture
                && r.Status == RecordingStatus.Available && r.ContentType == "audio/wav" && r.Takes != null)
            .OrderByDescending(r => r.CreatedAt).ThenByDescending(r => r.Id).Select(r => (Guid?)r.Id).FirstOrDefaultAsync(ct);
        if (id is null) return null;
        var recording = await FindAsync(id.Value, forUpdate, ct);
        // Erased between the two reads: there is nothing to append to.
        return recording is { Status: RecordingStatus.Available } ? recording : null;
    }

    public async Task<IReadOnlyList<Guid>> ListIdleTakesAsync(DateTimeOffset before, int take, CancellationToken ct)
        => await db.Recordings.AsNoTracking()
            .Where(r => r.Source == RecordingSource.BrowserCapture && r.Status == RecordingStatus.Pending && r.UpdatedAt < before)
            .OrderBy(r => r.UpdatedAt).Select(r => r.Id).Take(take).ToListAsync(ct);

    public void Add(Recording recording) => db.Recordings.Add(recording);

    public void Remove(Recording recording) => db.Recordings.Remove(recording);

    public Task DeleteTranscriptAsync(Guid recordingId, CancellationToken ct)
        => db.Transcripts.Where(t => t.RecordingId == recordingId).ExecuteDeleteAsync(ct);

    public Task SaveChangesAsync(CancellationToken ct) => db.SaveChangesAsync(ct);
}
