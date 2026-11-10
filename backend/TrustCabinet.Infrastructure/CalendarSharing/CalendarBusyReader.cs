using Microsoft.EntityFrameworkCore;
using TrustCabinet.Domain.CalendarSharing;
using TrustCabinet.Domain.Enums;
using TrustCabinet.Domain.Participants;

namespace TrustCabinet.Infrastructure.CalendarSharing;

/// <summary>
/// The busy periods of a set of people over a time range, as far as each may be shown (BR-414 → BR-420, BR-427). The
/// shared week (UC-85c) uses it now; the meeting-time search (UC-85a, SCRUM-137) reuses it, passing a null level for the
/// finder's own calendar (BR-418). Nothing about a meeting leaves this class but when it keeps someone busy.
/// </summary>
public sealed class CalendarBusyReader(TrustCabinetDbContext db, TimeProvider clock)
{
    /// <param name="people">Each person and the level shown for them; null shows every level, Secret included.</param>
    public async Task<IReadOnlyDictionary<Guid, IReadOnlyList<BusyInterval>>> ReadAsync(
        IReadOnlyDictionary<Guid, CalendarShareLevel?> people, DateTimeOffset from, DateTimeOffset to,
        CancellationToken ct = default)
    {
        var result = people.Keys.ToDictionary(id => id, _ => (IReadOnlyList<BusyInterval>)[]);
        if (people.Count == 0 || to <= from) return result;

        // timestamptz parameters must be UTC for Npgsql; callers pass Vietnam-local instants.
        from = from.ToUniversalTime();
        to = to.ToUniversalTime();
        var ids = people.Keys.ToArray();
        var statuses = CalendarBusyRules.BusyStatuses.ToArray();
        // A loose overlap in SQL (start before the range ends; end after it starts, or still running); the exact period
        // and the level are decided in memory by the same rules the unit tests pin.
        var rows = await db.MeetingParticipants.AsNoTracking()
            .Where(p => ids.Contains(p.UserId)
                && p.AssignmentStatus == AssignmentStatus.Current
                && (p.Role == MeetingRole.Chair || p.Role == MeetingRole.Secretary || p.Role == MeetingRole.Member)
                && p.Response != InvitationResponse.Delegated && p.DelegatedToUserId == null
                && (p.Role == MeetingRole.Chair || p.Response != InvitationResponse.Declined)
                && statuses.Contains(p.Meeting.Status)
                && (p.Meeting.PlannedStartTime < to || (p.Meeting.ActualStartedAt != null && p.Meeting.ActualStartedAt < to))
                && (p.Meeting.PlannedEndTime > from || p.Meeting.Status == MeetingStatus.InProgress
                    || (p.Meeting.ActualEndedAt != null && p.Meeting.ActualEndedAt > from)))
            .Select(p => new
            {
                p.UserId, p.AssignmentStatus, p.Role, p.Response, p.DelegatedToUserId,
                p.Meeting.Status, p.Meeting.Sensitivity, p.Meeting.PlannedStartTime, p.Meeting.PlannedEndTime,
                p.Meeting.ActualStartedAt, p.Meeting.ActualEndedAt,
            })
            .ToListAsync(ct);

        var now = clock.GetUtcNow();
        foreach (var person in rows.GroupBy(r => r.UserId))
        {
            var level = people[person.Key];
            var intervals = person
                .Where(r => CalendarBusyRules.IsBusyAssignment(r.AssignmentStatus, r.Role, r.Response, r.DelegatedToUserId)
                    && CalendarShareRules.Covers(level, r.Sensitivity))
                .Select(r => CalendarBusyRules.Interval(r.Status, r.PlannedStartTime, r.PlannedEndTime, r.ActualStartedAt,
                    r.ActualEndedAt, now))
                .OfType<BusyInterval>();
            result[person.Key] = CalendarBusyRules.Clip(CalendarBusyRules.Merge(intervals), from, to);
        }

        return result;
    }
}
