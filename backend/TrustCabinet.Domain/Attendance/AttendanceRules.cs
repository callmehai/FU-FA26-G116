using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using TrustCabinet.Domain.Enums;

namespace TrustCabinet.Domain.Attendance;

public static class AttendanceRules
{
    /// <summary>Reject corrupt persisted values rather than treating them as unmarked attendance.</summary>
    public static AttendanceStatus ReadStored(string value)
        => Enum.TryParse<AttendanceStatus>(value, false, out var status) && Enum.IsDefined(status) && status.ToString() == value
            ? status : throw new InvalidDataException("Unrecognized persisted attendance value.");

    public static string Token(Guid meetingId, string kind, Guid entryId, long version)
        => Convert.ToHexStringLower(SHA256.HashData(Encoding.UTF8.GetBytes(
            $"attendance-v1|{meetingId:D}|{kind}|{entryId:D}|{version.ToString(CultureInfo.InvariantCulture)}")));

    public static bool Valid(AttendanceInput input) => input.Entries is { Length: > 0 } entries
        && entries.All(e => e is not null && e.Id != Guid.Empty && e.Kind is "Internal" or "Guest"
            && e.Attendance is "Present" or "Absent" && e.ExpectedVersion is { Length: 64 } token
            && token.All(c => c is >= '0' and <= '9' or >= 'a' and <= 'f'))
        && entries.Select(e => (e!.Kind, e.Id)).Distinct().Count() == entries.Length;

    public static (string? Official, string? Legacy) Display(AttendanceStatus status) => status switch
    {
        AttendanceStatus.Unknown => (null, null),
        AttendanceStatus.Present or AttendanceStatus.Absent => (status.ToString(), null),
        AttendanceStatus.Excused or AttendanceStatus.Delegated => (null, status.ToString()),
        _ => throw new InvalidDataException("Unrecognized persisted attendance value.")
    };
}
