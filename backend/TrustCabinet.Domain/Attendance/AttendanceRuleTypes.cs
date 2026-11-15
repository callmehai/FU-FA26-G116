namespace TrustCabinet.Domain.Attendance;

public enum AttendanceFailure { None, NotFound, Forbidden, Locked, Invalid, Stale, UnsupportedMedia, TooLarge, ReadFailed, SaveFailed, InvalidQuery }

public sealed record AttendanceEntry(Guid Id, string? Kind, string? Attendance, string? ExpectedVersion);

public sealed record AttendanceInput(AttendanceEntry?[]? Entries = null, AttendanceFailure ParseFailure = AttendanceFailure.None);
