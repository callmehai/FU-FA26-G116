using TrustCabinet.Domain.Entities;
using TrustCabinet.Domain.Enums;
using TrustCabinet.Domain.Meetings;

namespace TrustCabinet.Web.Tests.Meetings;

public sealed class MeetingSessionRulesTests
{
    private static readonly DateTimeOffset PlannedStart = new(2026, 9, 1, 2, 0, 0, TimeSpan.Zero);

    [Fact]
    public void Valid_planned_session_does_not_require_a_future_start_or_parent_time_bounds()
    {
        var meetingId = Guid.CreateVersion7();
        var session = NewSession(meetingId);
        session.Title = new string('a', MeetingSessionRules.MaximumTitleLength);
        session.Meeting = new Meeting
        {
            Id = meetingId,
            Title = "Parent schedule differs",
        };

        Assert.True(MeetingSessionRules.IsValidPlanned(session, meetingId));
    }

    [Theory]
    [InlineData("empty-session-id")]
    [InlineData("wrong-meeting")]
    [InlineData("empty-meeting-id")]
    [InlineData("blank-title")]
    [InlineData("title-too-long")]
    [InlineData("missing-start")]
    [InlineData("missing-end")]
    [InlineData("equal-times")]
    [InlineData("end-before-start")]
    public void Invalid_planned_session_does_not_satisfy_publication_readiness(string invalidField)
    {
        var meetingId = Guid.CreateVersion7();
        var session = NewSession(meetingId);
        switch (invalidField)
        {
            case "empty-session-id": session.Id = Guid.Empty; break;
            case "wrong-meeting": session.MeetingId = Guid.CreateVersion7(); break;
            case "empty-meeting-id": session.MeetingId = meetingId = Guid.Empty; break;
            case "blank-title": session.Title = "\u00A0\t\r\n"; break;
            case "title-too-long": session.Title = new string('a', MeetingSessionRules.MaximumTitleLength + 1); break;
            case "missing-start": session.PlannedStartsAt = default; break;
            case "missing-end": session.PlannedEndsAt = default; break;
            case "equal-times": session.PlannedEndsAt = session.PlannedStartsAt; break;
            case "end-before-start": session.PlannedEndsAt = session.PlannedStartsAt.AddMinutes(-1); break;
            default: throw new ArgumentOutOfRangeException(nameof(invalidField));
        }

        Assert.False(MeetingSessionRules.IsValidPlanned(session, meetingId));
    }

    [Theory]
    [InlineData(MeetingSessionStatus.InProgress)]
    [InlineData(MeetingSessionStatus.Completed)]
    [InlineData(MeetingSessionStatus.Cancelled)]
    [InlineData((MeetingSessionStatus)99)]
    public void Only_Planned_sessions_satisfy_publication_readiness(MeetingSessionStatus status)
    {
        var meetingId = Guid.CreateVersion7();
        var session = NewSession(meetingId);
        session.Status = status;

        Assert.False(MeetingSessionRules.IsValidPlanned(session, meetingId));
    }

    [Theory]
    [InlineData("2026-09-01T09:00+07:00", true)]
    [InlineData("2026-09-01T02:00:00Z", true)]
    [InlineData("2026-09-01T02:00:00.1234567Z", true)]
    [InlineData("2026-09-01T09:00:00", false)]
    [InlineData("2026-09-01", false)]
    [InlineData("01/09/2026 09:00 +07:00", false)]
    [InlineData("2026-13-01T09:00:00Z", false)]
    public void Session_timestamps_need_an_iso_value_with_an_explicit_offset(string start, bool valid)
    {
        var input = new MeetingSessionInput("Phiên sáng", start, "2026-09-01T23:00:00Z");

        Assert.Equal(valid, MeetingSessionRules.TryValidate(input, null, out var result, out var errors));
        Assert.Equal(valid, !errors.ContainsKey("plannedStartsAt"));
        if (valid)
        {
            Assert.Equal(TimeSpan.Zero, result!.PlannedStartsAt.Offset);
        }
    }

    [Fact]
    public void Future_start_rule_skips_an_unchanged_start_but_not_a_rescheduled_one()
    {
        var now = PlannedStart.AddHours(1);
        var unchanged = new MeetingSessionInput("Đổi tên", "2026-09-01T02:00:00Z", "2026-09-01T05:00:00Z");
        var moved = unchanged with { PlannedStartsAt = "2026-09-01T02:30:00Z" };

        Assert.True(MeetingSessionRules.TryValidate(unchanged, now, out _, out _, unchangedStart: PlannedStart));
        Assert.False(MeetingSessionRules.TryValidate(moved, now, out _, out var errors, unchangedStart: PlannedStart));
        Assert.True(errors.ContainsKey("plannedStartsAt"));
        Assert.False(MeetingSessionRules.TryValidate(unchanged, now, out _, out _));
    }

    [Theory]
    [InlineData(MeetingStatus.Draft, true)]
    [InlineData(MeetingStatus.Rejected, true)]
    [InlineData(MeetingStatus.Approved, true)]
    [InlineData(MeetingStatus.Published, true)]
    [InlineData(MeetingStatus.InProgress, true)]
    [InlineData(MeetingStatus.WaitingApproval, false)]
    [InlineData(MeetingStatus.Ended, false)]
    [InlineData(MeetingStatus.MinutesIssued, false)]
    [InlineData(MeetingStatus.Archived, false)]
    public void Sessions_are_planned_only_in_BR54_statuses(MeetingStatus status, bool allowed)
        => Assert.Equal(allowed, MeetingSessionRules.AllowsPlanning(status));

    private static MeetingSession NewSession(Guid meetingId) => new()
    {
        MeetingId = meetingId,
        Title = "Planned review session",
        PlannedStartsAt = PlannedStart,
        PlannedEndsAt = PlannedStart.AddHours(1),
    };
}
