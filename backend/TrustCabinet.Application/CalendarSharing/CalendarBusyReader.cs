using TrustCabinet.Domain.CalendarSharing;
using TrustCabinet.Domain.Enums;

namespace TrustCabinet.Application.CalendarSharing;

/// <summary>
/// The busy periods of a set of people over a time range, as far as each may be shown (BR-140, BR-141, BR-144–BR-147). The
/// shared week (UC-81c) uses it now; the meeting-time search (UC-81a, SCRUM-137) reuses it, passing a null level for the
/// finder's own calendar (BR-144, BR-145). Nothing about a meeting leaves this class but when it keeps someone busy.
/// </summary>
public sealed class CalendarBusyReader(ICalendarBusyQuery query, TimeProvider clock)
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
        var rows = await query.FindAsync(people.Keys.ToArray(), from, to, ct);

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
