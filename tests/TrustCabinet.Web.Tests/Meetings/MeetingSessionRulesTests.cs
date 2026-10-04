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
            StartsAt = PlannedStart.AddDays(10),
            EndsAt = PlannedStart.AddDays(11),
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

    private static MeetingSession NewSession(Guid meetingId) => new()
    {
        MeetingId = meetingId,
        Title = "Planned review session",
        PlannedStartsAt = PlannedStart,
        PlannedEndsAt = PlannedStart.AddHours(1),
    };
}
