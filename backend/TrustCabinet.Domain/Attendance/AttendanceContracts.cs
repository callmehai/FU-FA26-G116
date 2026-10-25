using TrustCabinet.Domain.Participants;

namespace TrustCabinet.Domain.Attendance;

public enum AttendanceFailure { None, NotFound, Forbidden, Locked, Invalid, Stale, UnsupportedMedia, TooLarge, ReadFailed, SaveFailed, InvalidQuery }
public sealed record AttendanceEntry(Guid Id, string? Kind, string? Attendance, string? ExpectedVersion);
public sealed record AttendanceInput(AttendanceEntry?[]? Entries = null, AttendanceFailure ParseFailure = AttendanceFailure.None);
public sealed record AttendanceQuery(int Page = 1, int PageSize = 20, bool Invalid = false);
public sealed record AttendancePermissions(bool CanRecordAttendance, bool CanViewInvitationResponses);
public sealed record AttendanceRecorder(Guid Id, string DisplayName);
public sealed record AttendanceRow(Guid Id, string Kind, string DisplayName, string? Role, bool? IsActive,
    string? InvitationResponse, string? Attendance, string? LegacyAttendance, string RowVersion,
    AttendanceRecorder? RecordedBy, DateTimeOffset? RecordedAt);
public sealed record AttendanceTotals(int Present, int Absent, int Unmarked, int Legacy, int Total);
public sealed record AttendancePage(Guid MeetingId, string MeetingStatus, DateTimeOffset ServerTime,
    AttendancePermissions Permissions, IReadOnlyList<AttendanceRow> Items, int Total, int Page, int PageSize, AttendanceTotals Totals);
public sealed record AttendanceSavedEntry(Guid Id, string Kind, string? Attendance, string? LegacyAttendance,
    string RowVersion, AttendanceRecorder? RecordedBy, DateTimeOffset? RecordedAt);
public sealed record AttendanceSaved(Guid MeetingId, int ChangedCount, IReadOnlyList<AttendanceSavedEntry> Entries);
public sealed record AttendanceResult(AttendanceFailure Failure = AttendanceFailure.None, AttendancePage? Page = null, AttendanceSaved? Saved = null);
public sealed record AttendanceChange(Guid EventId, Guid MeetingId, DateTimeOffset OccurredAt);
public interface IAttendanceService
{
    Task<AttendanceResult> ReadAsync(Guid meetingId, ParticipantActor actor, AttendanceQuery query, CancellationToken ct);
    Task<AttendanceResult> SaveAsync(Guid meetingId, ParticipantActor actor, AttendanceInput input, CancellationToken ct);
}
public interface IAttendanceChangePublisher
{
    Task PublishAsync(AttendanceChange change, CancellationToken ct);
}
