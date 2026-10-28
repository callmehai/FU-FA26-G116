using Microsoft.EntityFrameworkCore;
using TrustCabinet.Domain.Entities;
using TrustCabinet.Domain.Enums;
using TrustCabinet.Domain.Transcription;

namespace TrustCabinet.Infrastructure.Transcription;

public sealed class TranscriptRepository(TrustCabinetDbContext db) : ITranscriptRepository
{
    public async Task<Transcript?> FindByRecordingAsync(Guid recordingId, bool tracked, CancellationToken ct)
    {
        var query = db.Transcripts.Include(t => t.Segments).ThenInclude(s => s.EditedBy)
            .Include(t => t.Speakers).ThenInclude(s => s.SuggestedUser)
            .Include(t => t.Speakers).ThenInclude(s => s.AssignedUser)
            .AsSplitQuery().Where(t => t.RecordingId == recordingId);
        return await (tracked ? query : query.AsNoTracking()).SingleOrDefaultAsync(ct);
    }

    public Task<TranscriptSegment?> FindSegmentAsync(Guid segmentId, CancellationToken ct)
        => db.TranscriptSegments.Include(s => s.Transcript).ThenInclude(t => t.Recording)
            .SingleOrDefaultAsync(s => s.Id == segmentId, ct);

    public async Task<Guid?> ClaimNextAsync(DateTimeOffset now, CancellationToken ct)
    {
        // SKIP LOCKED: two app instances never take the same recording, and neither waits on the other.
        var claimed = await db.Database.SqlQuery<Guid>($"""
            UPDATE transcripts SET status = 'Processing', progress_percent = 0, error_message = '', updated_at = {now}
            WHERE id = (SELECT id FROM transcripts WHERE status = 'Pending' ORDER BY created_at, id LIMIT 1 FOR UPDATE SKIP LOCKED)
            RETURNING id AS "Value"
            """).ToListAsync(ct);
        return claimed.Count == 0 ? null : claimed[0];
    }

    public Task<int> RequeueProcessingAsync(DateTimeOffset now, CancellationToken ct)
        => db.Transcripts.Where(t => t.Status == TranscriptStatus.Processing)
            .ExecuteUpdateAsync(s => s.SetProperty(t => t.Status, TranscriptStatus.Pending).SetProperty(t => t.UpdatedAt, now), ct);

    public Task<Transcript?> FindForProcessingAsync(Guid transcriptId, CancellationToken ct)
        => db.Transcripts.Include(t => t.Recording).SingleOrDefaultAsync(t => t.Id == transcriptId, ct);

    public Task DeleteSegmentsAsync(Guid transcriptId, CancellationToken ct)
        => db.TranscriptSegments.Where(s => s.TranscriptId == transcriptId).ExecuteDeleteAsync(ct);

    public void Add(Transcript transcript) => db.Transcripts.Add(transcript);

    public Task DeleteSpeakersAsync(Guid transcriptId, CancellationToken ct)
        => db.TranscriptSpeakers.Where(s => s.TranscriptId == transcriptId).ExecuteDeleteAsync(ct);

    public void AddSpeakers(IEnumerable<TranscriptSpeaker> speakers) => db.TranscriptSpeakers.AddRange(speakers);

    public void AddSegments(IEnumerable<TranscriptSegment> segments) => db.TranscriptSegments.AddRange(segments);

    public Task SaveChangesAsync(CancellationToken ct) => db.SaveChangesAsync(ct);
}
