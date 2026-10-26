using TrustCabinet.Domain.Entities;
using TrustCabinet.Domain.Enums;
using TrustCabinet.Domain.Meetings;

namespace TrustCabinet.Web.Tests.Meetings;

public sealed class MeetingSubmissionReadinessTests
{
    private static readonly DateTimeOffset Start = new(2026, 10, 1, 2, 0, 0, TimeSpan.Zero);

    [Fact]
    public void Submission_readiness_does_not_depend_on_agenda_content()
    {
        var meeting = new Meeting
        {
            Title = "Giao ban tuần",
            Status = MeetingStatus.Rejected,
            Sensitivity = Sensitivity.Normal,
            Mode = MeetingMode.Online,
            PlannedStartTime = Start,
            PlannedEndTime = Start.AddHours(1),
            CreatedById = Guid.CreateVersion7(),
        };
        Assert.Empty(MeetingSubmissionReadinessRules.Validate(meeting));
        meeting.AgendaItems.Add(new AgendaItem { Title = " " });
        Assert.Empty(MeetingSubmissionReadinessRules.Validate(meeting));
    }

    [Fact]
    public void Submission_readiness_requires_a_valid_planned_schedule()
    {
        var meeting = new Meeting
        {
            Title = "Giao ban tuần",
            Status = MeetingStatus.Draft,
            Sensitivity = Sensitivity.Normal,
            Mode = MeetingMode.InPerson,
            CreatedById = Guid.CreateVersion7(),
        };

        var missing = MeetingSubmissionReadinessRules.Validate(meeting);
        Assert.Contains("plannedStartTime", missing.Keys);
        Assert.Contains("plannedEndTime", missing.Keys);

        meeting.PlannedStartTime = Start;
        meeting.PlannedEndTime = Start;
        Assert.Equal(
            "Thời gian kết thúc phải sau thời gian bắt đầu.",
            MeetingSubmissionReadinessRules.Validate(meeting)["plannedEndTime"]);
    }

    [Fact]
    public void Meeting_readiness_reuses_information_rules_and_keeps_optional_fields_optional()
    {
        var meeting = new Meeting
        {
            Title = "  Giao ban tuần  ",
            Description = string.Empty,
            Status = MeetingStatus.Draft,
            Sensitivity = Sensitivity.Normal,
            Mode = MeetingMode.Online,
            Location = string.Empty,
            PlannedStartTime = Start,
            PlannedEndTime = Start.AddHours(1),
            CreatedById = Guid.CreateVersion7(),
            AgendaItems =
            [
                new AgendaItem
                {
                    Position = 1,
                    Title = "Báo cáo",
                    PresenterId = Guid.CreateVersion7(),
                },
            ],
        };

        Assert.Empty(MeetingSubmissionReadinessRules.Validate(meeting));

        meeting.Title = " ";
        meeting.Sensitivity = (Sensitivity)999;
        meeting.Description = new string('x', 4001);
        meeting.OnlineLink = new string('l', 1001);
        var invalid = MeetingSubmissionReadinessRules.Validate(meeting);

        Assert.Contains("title", invalid.Keys);
        Assert.Contains("sensitivity", invalid.Keys);
        Assert.Contains("description", invalid.Keys);
        Assert.Contains("onlineLink", invalid.Keys);
    }
}
