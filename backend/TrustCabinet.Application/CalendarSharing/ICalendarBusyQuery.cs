using TrustCabinet.Domain.Enums;
using TrustCabinet.Domain.Participants;

namespace TrustCabinet.Application.CalendarSharing;

/// <summary>
/// One person's place in a meeting that may keep them busy, with the meeting's status, sensitivity and times — nothing
/// else about the meeting (BR-140, BR-141).
/// </summary>
public sealed record BusyAssignment(
    Guid UserId,
    AssignmentStatus AssignmentStatus,
    MeetingRole Role,
    InvitationResponse Response,
    MeetingStatus Status,
    Sensitivity Sensitivity,
    DateTimeOffset PlannedStartTime,
    DateTimeOffset PlannedEndTime,
    DateTimeOffset? ActualStartedAt,
    DateTimeOffset? ActualEndedAt);

/// <summary>The meetings that may keep people busy over a time range (BR-140, BR-141, BR-144–BR-147), untracked.</summary>
public interface ICalendarBusyQuery
{
    /// <summary>
    /// The current Chair, Secretary and Member places of these people, not declined (a Chair counts
    /// even when declined), in meetings of a busy status that loosely overlap the range: starting before it ends, and
    /// ending after it starts or still running. The exact period is for the caller to decide. Pass UTC instants.
    /// </summary>
    Task<List<BusyAssignment>> FindAsync(Guid[] userIds, DateTimeOffset from, DateTimeOffset to,
        CancellationToken ct = default);
}
