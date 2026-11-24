using TrustCabinet.Domain.Enums;
using TrustCabinet.Domain.Participants;

namespace TrustCabinet.Domain.CalendarSharing;

/// <summary>A busy period, without anything about the meeting behind it (BR-140, BR-141).</summary>
public readonly record struct BusyInterval(DateTimeOffset Start, DateTimeOffset End);

/// <summary>
/// Which meetings make a person busy and when (BR-146, BR-147), shared by the shared calendar (UC-81c, SCRUM-142) and
/// the meeting-time search (UC-81a, SCRUM-137). Which sensitivity a viewer sees is <see cref="CalendarShareRules.Covers"/>.
/// </summary>
public static class CalendarBusyRules
{
    /// <summary>BR-146, BR-147 (PO 08/11/2026, Q9). Draft, Rejected and Cancelled meetings never count.</summary>
    public static readonly IReadOnlyList<MeetingStatus> BusyStatuses =
    [
        MeetingStatus.WaitingApproval, MeetingStatus.Approved, MeetingStatus.Published, MeetingStatus.InProgress,
        MeetingStatus.Ended,
    ];

    public static bool CountsAsBusy(MeetingStatus status) => BusyStatuses.Contains(status);

    /// <summary>
    /// BR-146, BR-147: only a current Chair, Secretary or Member assignment counts; never the creator or approver alone, never a
    /// Guest, never someone who declined. The Chair is never asked, so an answer left from before they took the chair
    /// does not count — as in the meeting list.
    /// </summary>
    public static bool IsBusyAssignment(AssignmentStatus assignment, MeetingRole role, InvitationResponse response,
        Guid? delegatedTo)
        => assignment == AssignmentStatus.Current
            && role is MeetingRole.Chair or MeetingRole.Secretary or MeetingRole.Member
            && response != InvitationResponse.Delegated && delegatedTo is null
            && (role == MeetingRole.Chair || response != InvitationResponse.Declined);

    /// <summary>
    /// When a meeting keeps its people busy, or null when it does not (BR-146, BR-147). An ended meeting until it actually ended;
    /// one still running for its planned length from when it really started, and at least until now; any other until
    /// its planned end. A meeting started early — next month's, opened today — is busy today, not from today until next
    /// month.
    /// </summary>
    public static BusyInterval? Interval(MeetingStatus status, DateTimeOffset plannedStart, DateTimeOffset plannedEnd,
        DateTimeOffset? actualStart, DateTimeOffset? actualEnd, DateTimeOffset now)
    {
        if (!CountsAsBusy(status)) return null;
        var start = status is MeetingStatus.InProgress or MeetingStatus.Ended && actualStart is { } started ? started : plannedStart;
        var end = status switch
        {
            MeetingStatus.Ended => actualEnd ?? plannedEnd,
            MeetingStatus.InProgress => start + (plannedEnd - plannedStart) is var due && due > now ? due : now,
            _ => plannedEnd,
        };
        return end > start ? new BusyInterval(start, end) : null;
    }

    /// <summary>Sorted, with overlapping or touching periods joined, so the count of meetings does not show.</summary>
    public static IReadOnlyList<BusyInterval> Merge(IEnumerable<BusyInterval> intervals)
    {
        var merged = new List<BusyInterval>();
        foreach (var next in intervals.Where(i => i.End > i.Start).OrderBy(i => i.Start))
        {
            if (merged.Count > 0 && next.Start <= merged[^1].End)
            {
                var last = merged[^1];
                merged[^1] = last with { End = next.End > last.End ? next.End : last.End };
            }
            else
            {
                merged.Add(next);
            }
        }

        return merged;
    }

    /// <summary>The parts of <paramref name="intervals"/> inside [<paramref name="from"/>, <paramref name="to"/>).</summary>
    public static IReadOnlyList<BusyInterval> Clip(IEnumerable<BusyInterval> intervals, DateTimeOffset from, DateTimeOffset to)
        => [.. intervals.Where(i => i.End > from && i.Start < to)
            .Select(i => new BusyInterval(i.Start < from ? from : i.Start, i.End > to ? to : i.End))];
}
