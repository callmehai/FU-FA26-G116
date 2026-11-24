using TrustCabinet.Domain.Enums;

namespace TrustCabinet.Application.Reports;

/// <summary>
/// One row of the BR-149, BR-150 group table: how many readable meetings started in a Vietnam month
/// with this status and sensitivity. Untracked.
/// </summary>
public sealed record MeetingReportGroup(int Year, int Month, MeetingStatus Status, Sensitivity Sensitivity, int Count);

/// <summary>
/// One Ended meeting the caller may read, with only the numbers its averages need (BR-151
/// and the duration): internal and guest invitee/present/marked counts, the actual times and the
/// first issue time of its minutes. Never the meeting's id or title. Untracked.
/// </summary>
public sealed record MeetingReportEndedRow(
    int InternalInvitees,
    int InternalPresent,
    int InternalMarked,
    int GuestInvitees,
    int GuestPresent,
    int GuestMarked,
    DateTimeOffset? ActualStartedAt,
    DateTimeOffset? ActualEndedAt,
    DateTimeOffset? FirstIssuedAt);

/// <summary>
/// The two reads behind the meeting report (UC-79a, UC-79b), untracked. Each call filters by
/// <see cref="TrustCabinet.Domain.Meetings.MeetingReadPolicy.ForUser"/> before anything is counted
/// (BR-149, BR-150) — for a System Admin as for anyone else — over meetings whose planned start is in
/// [<paramref name="start"/>, <paramref name="end"/>). Pass UTC instants.
/// </summary>
public interface IMeetingReportQuery
{
    /// <summary>One GROUP BY over month (Vietnam time), status and sensitivity (BR-149, BR-150).</summary>
    Task<List<MeetingReportGroup>> ListGroupsAsync(Guid actorId, DateTimeOffset start, DateTimeOffset end,
        CancellationToken ct = default);

    /// <summary>One row per Ended meeting with the numbers its averages need.</summary>
    Task<List<MeetingReportEndedRow>> ListEndedRowsAsync(Guid actorId, DateTimeOffset start, DateTimeOffset end,
        CancellationToken ct = default);
}
