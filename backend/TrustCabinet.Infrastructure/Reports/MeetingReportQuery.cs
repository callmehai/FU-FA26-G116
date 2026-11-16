using Microsoft.EntityFrameworkCore;
using TrustCabinet.Application.Reports;
using TrustCabinet.Domain.Enums;
using TrustCabinet.Domain.Meetings;
using TrustCabinet.Domain.Participants;

namespace TrustCabinet.Infrastructure.Reports;

public sealed class MeetingReportQuery(TrustCabinetDbContext db) : IMeetingReportQuery
{
    public Task<List<MeetingReportGroup>> ListGroupsAsync(Guid actorId, DateTimeOffset start, DateTimeOffset end,
        CancellationToken ct = default)
    {
        // BR-434: one GROUP BY over month (Vietnam time), status and sensitivity; the three tables are sums of it.
        // The permission filter stays in the query, before anything is counted (BR-432).
        return db.Meetings.AsNoTracking()
            .Where(MeetingReadPolicy.ForUser(actorId))
            .Where(m => m.PlannedStartTime >= start && m.PlannedStartTime < end)
            .Select(m => new { Local = m.PlannedStartTime.AddHours(7), m.Status, m.Sensitivity })
            .GroupBy(m => new { m.Local.Year, m.Local.Month, m.Status, m.Sensitivity })
            .Select(g => new MeetingReportGroup(g.Key.Year, g.Key.Month, g.Key.Status, g.Key.Sensitivity, g.Count()))
            .ToListAsync(ct);
    }

    public Task<List<MeetingReportEndedRow>> ListEndedRowsAsync(Guid actorId, DateTimeOffset start, DateTimeOffset end,
        CancellationToken ct = default)
    {
        // BR-435, BR-436 and the duration (PO 09/11/2026, Q2): per Ended meeting, the numbers its averages need — nothing that names the meeting.
        return db.Meetings.AsNoTracking()
            .Where(MeetingReadPolicy.ForUser(actorId))
            .Where(m => m.PlannedStartTime >= start && m.PlannedStartTime < end)
            .Where(m => m.Status == MeetingStatus.Ended)
            .Select(m => new MeetingReportEndedRow(
                m.Participants.AsQueryable().Count(ParticipantRules.CurrentExpression),
                m.Participants.AsQueryable().Where(ParticipantRules.CurrentExpression)
                    .Count(p => p.Attendance == AttendanceStatus.Present),
                m.Participants.AsQueryable().Where(ParticipantRules.CurrentExpression)
                    .Count(p => p.Attendance == AttendanceStatus.Present || p.Attendance == AttendanceStatus.Absent),
                db.MeetingGuestInvitations.Count(g => g.MeetingId == m.Id && g.RevokedAt == null),
                db.MeetingGuestInvitations.Count(g => g.MeetingId == m.Id && g.RevokedAt == null
                    && g.Attendance == AttendanceStatus.Present),
                db.MeetingGuestInvitations.Count(g => g.MeetingId == m.Id && g.RevokedAt == null
                    && (g.Attendance == AttendanceStatus.Present || g.Attendance == AttendanceStatus.Absent)),
                m.ActualStartedAt,
                m.ActualEndedAt,
                // The first issue: a correction issued later does not restart the clock.
                m.Minutes.Where(x => x.IssuedAt != null).Min(x => x.IssuedAt)))
            .ToListAsync(ct);
    }
}
