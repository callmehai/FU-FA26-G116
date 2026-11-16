using Microsoft.EntityFrameworkCore;
using TrustCabinet.Application.CalendarSharing;
using TrustCabinet.Domain.CalendarSharing;
using TrustCabinet.Domain.Enums;
using TrustCabinet.Domain.Participants;

namespace TrustCabinet.Infrastructure.CalendarSharing;

public sealed class CalendarBusyQuery(TrustCabinetDbContext db) : ICalendarBusyQuery
{
    public Task<List<BusyAssignment>> FindAsync(Guid[] userIds, DateTimeOffset from, DateTimeOffset to,
        CancellationToken ct = default)
    {
        var statuses = CalendarBusyRules.BusyStatuses.ToArray();
        // A loose overlap in SQL (start before the range ends; end after it starts, or still running); the exact period
        // and the level are decided in memory by the same rules the unit tests pin.
        return db.MeetingParticipants.AsNoTracking()
            .Where(p => userIds.Contains(p.UserId)
                && p.AssignmentStatus == AssignmentStatus.Current
                && (p.Role == MeetingRole.Chair || p.Role == MeetingRole.Secretary || p.Role == MeetingRole.Member)
                && p.Response != InvitationResponse.Delegated && p.DelegatedToUserId == null
                && (p.Role == MeetingRole.Chair || p.Response != InvitationResponse.Declined)
                && statuses.Contains(p.Meeting.Status)
                && (p.Meeting.PlannedStartTime < to || (p.Meeting.ActualStartedAt != null && p.Meeting.ActualStartedAt < to))
                && (p.Meeting.PlannedEndTime > from || p.Meeting.Status == MeetingStatus.InProgress
                    || (p.Meeting.ActualEndedAt != null && p.Meeting.ActualEndedAt > from)))
            .Select(p => new BusyAssignment(
                p.UserId, p.AssignmentStatus, p.Role, p.Response, p.DelegatedToUserId,
                p.Meeting.Status, p.Meeting.Sensitivity, p.Meeting.PlannedStartTime, p.Meeting.PlannedEndTime,
                p.Meeting.ActualStartedAt, p.Meeting.ActualEndedAt))
            .ToListAsync(ct);
    }
}
