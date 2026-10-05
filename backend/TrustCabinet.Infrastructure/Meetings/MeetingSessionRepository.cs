using Microsoft.EntityFrameworkCore;
using TrustCabinet.Domain.Entities;
using TrustCabinet.Domain.Enums;
using TrustCabinet.Domain.Meetings;

namespace TrustCabinet.Infrastructure.Meetings;

public sealed class MeetingSessionRepository(TrustCabinetDbContext db) : IMeetingSessionRepository
{
    public async Task<IReadOnlyList<MeetingSessionDetails>> ListByMeetingAsync(
        Guid meetingId, CancellationToken ct = default)
        => await db.MeetingSessions
            .AsNoTracking()
            .Where(session => session.MeetingId == meetingId)
            .OrderBy(session => session.PlannedStartsAt)
            .ThenBy(session => session.Id)
            .Select(session => new MeetingSessionDetails(
                session.Id,
                session.MeetingId,
                session.Title,
                session.PlannedStartsAt,
                session.PlannedEndsAt,
                session.Status,
                session.ActualStartedAt,
                session.ActualEndedAt))
            .ToListAsync(ct);

    public Task<MeetingSession?> FindAsync(Guid meetingId, Guid sessionId, CancellationToken ct = default)
        => db.MeetingSessions.SingleOrDefaultAsync(
            session => session.Id == sessionId && session.MeetingId == meetingId, ct);

    public Task AddAsync(MeetingSession session, CancellationToken ct = default)
    {
        ct.ThrowIfCancellationRequested();
        db.MeetingSessions.Add(session);
        return Task.CompletedTask;
    }

    public Task<bool> HasSessionInProgressAsync(Guid meetingId, CancellationToken ct = default)
        => db.MeetingSessions.AnyAsync(
            session => session.MeetingId == meetingId && session.Status == MeetingSessionStatus.InProgress, ct);
}
