using System.Globalization;
using TrustCabinet.Domain.Attendance;
using TrustCabinet.Domain.Enums;

namespace TrustCabinet.Web.Tests.Attendance;

public sealed class AttendanceRulesTests
{
    [Fact]
    public void Tokens_bind_meeting_kind_entry_and_version_and_are_culture_independent()
    {
        var meeting = Guid.CreateVersion7(); var entry = Guid.CreateVersion7();
        var expected = AttendanceRules.Token(meeting, "Internal", entry, long.MaxValue);
        var original = CultureInfo.CurrentCulture;
        try
        {
            CultureInfo.CurrentCulture = CultureInfo.GetCultureInfo("ar-SA");
            Assert.Equal(expected, AttendanceRules.Token(meeting, "Internal", entry, long.MaxValue));
        }
        finally { CultureInfo.CurrentCulture = original; }
        Assert.Matches("^[a-f0-9]{64}$", expected);
        Assert.NotEqual(expected, AttendanceRules.Token(Guid.CreateVersion7(), "Internal", entry, long.MaxValue));
        Assert.NotEqual(expected, AttendanceRules.Token(meeting, "Guest", entry, long.MaxValue));
        Assert.NotEqual(expected, AttendanceRules.Token(meeting, "Internal", Guid.CreateVersion7(), long.MaxValue));
        Assert.NotEqual(expected, AttendanceRules.Token(meeting, "Internal", entry, long.MaxValue - 1));
    }

    [Fact]
    public void Unmarked_and_legacy_are_distinct_and_unknown_enum_is_rejected()
    {
        Assert.Equal((null, (string?)null), AttendanceRules.Display(AttendanceStatus.Unknown));
        Assert.Equal((null, "Excused"), AttendanceRules.Display(AttendanceStatus.Excused));
        Assert.Equal(("Absent", (string?)null), AttendanceRules.Display(AttendanceStatus.Absent));
        Assert.Throws<InvalidDataException>(() => AttendanceRules.Display((AttendanceStatus)99));
    }
}
