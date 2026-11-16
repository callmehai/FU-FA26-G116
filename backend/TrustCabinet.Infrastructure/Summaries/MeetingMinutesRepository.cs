using Microsoft.EntityFrameworkCore;
using TrustCabinet.Application.Summaries;
using TrustCabinet.Domain.Entities;
using TrustCabinet.Domain.Enums;

namespace TrustCabinet.Infrastructure.Summaries;

public sealed class MeetingMinutesRepository(TrustCabinetDbContext db) : IMeetingMinutesRepository
{
    public Task<MeetingMinutes?> FindLatestAsync(Guid meetingId, CancellationToken ct)
        => db.MeetingMinutes.Where(m => m.MeetingId == meetingId)
            .OrderByDescending(m => m.RevisionNumber).FirstOrDefaultAsync(ct);

    public Task<MeetingMinutes?> FindAsync(Guid minutesId, CancellationToken ct)
        => db.MeetingMinutes.AsNoTracking().FirstOrDefaultAsync(m => m.Id == minutesId, ct);

    public Task<MeetingMinutes> GetAsync(Guid minutesId, CancellationToken ct)
        => db.MeetingMinutes.FirstAsync(m => m.Id == minutesId, ct);

    public Task<MeetingMinutes?> FindWithPeopleAsync(Guid minutesId, CancellationToken ct)
        => db.MeetingMinutes.AsNoTracking().Include(m => m.CreatedBy).Include(m => m.SubmittedBy)
            .Include(m => m.ApprovedBy).Include(m => m.ReturnedBy).FirstOrDefaultAsync(m => m.Id == minutesId, ct);

    public Task<Guid?> FindMeetingIdAsync(Guid minutesId, CancellationToken ct)
        => db.MeetingMinutes.AsNoTracking().Where(m => m.Id == minutesId)
            .Select(m => (Guid?)m.MeetingId).FirstOrDefaultAsync(ct);

    public async Task<MeetingMinutes?> LockAsync(Guid minutesId, CancellationToken ct)
        => (await db.MeetingMinutes
            .FromSqlInterpolated($"SELECT * FROM meeting_minutes WHERE id = {minutesId} FOR UPDATE")
            .ToListAsync(ct)).SingleOrDefault();

    public Task<bool> HasLaterRevisionAsync(Guid meetingId, int revisionNumber, CancellationToken ct)
        => db.MeetingMinutes.AnyAsync(m => m.MeetingId == meetingId && m.RevisionNumber > revisionNumber, ct);

    public async Task<IReadOnlyList<MinutesRevisionRow>> ListRevisionsAsync(Guid meetingId, bool manages, CancellationToken ct)
        => await db.MeetingMinutes.AsNoTracking()
            .Where(m => m.MeetingId == meetingId)
            .Where(m => manages || m.Status == MinutesStatus.Issued || m.Status == MinutesStatus.Superseded)
            .OrderByDescending(m => m.RevisionNumber)
            .Select(m => new MinutesRevisionRow(m.Id, m.RevisionNumber, m.Status, m.IssuedAt))
            .ToListAsync(ct);

    public Task<int> SaveContentAsync(Guid minutesId, string content, DateTimeOffset from, DateTimeOffset to,
        DateTimeOffset now, CancellationToken ct)
        => db.MeetingMinutes
            .Where(m => m.Id == minutesId && (m.Status == MinutesStatus.Draft || m.Status == MinutesStatus.Rejected)
                && m.UpdatedAt >= from && m.UpdatedAt <= to)
            .ExecuteUpdateAsync(m => m.SetProperty(x => x.Content, content).SetProperty(x => x.UpdatedAt, now), ct);

    public void Add(MeetingMinutes minutes) => db.MeetingMinutes.Add(minutes);
}
