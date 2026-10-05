using TrustCabinet.Domain.Entities;
using TrustCabinet.Domain.Enums;
using TrustCabinet.Domain.Meetings;

namespace TrustCabinet.Web.Tests.Meetings;

public sealed class MeetingSubmissionReadinessTests
{
    [Fact]
    public void Submission_readiness_does_not_depend_on_agenda_or_session_content()
    {
        var meeting = new Meeting
        {
            Title = "Giao ban tuần",
            Status = MeetingStatus.Rejected,
            Sensitivity = Sensitivity.Normal,
            Mode = MeetingMode.Online,
            CreatedById = Guid.CreateVersion7(),
        };
        Assert.Empty(MeetingSubmissionReadinessRules.Validate(meeting));
        meeting.AgendaItems.Add(new AgendaItem { Title = " " });
        meeting.Sessions.Add(new MeetingSession
        {
            MeetingId = meeting.Id,
            Title = " ",
            Status = MeetingSessionStatus.Cancelled,
        });
        Assert.Empty(MeetingSubmissionReadinessRules.Validate(meeting));
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
            CreatedById = Guid.CreateVersion7(),
            AgendaItems =
            [
                new AgendaItem
                {
                    Position = 1,
                    Title = "Báo cáo",
                    PlannedDurationMinutes = 30,
                    PresenterId = Guid.CreateVersion7(),
                },
            ],
        };

        Assert.Empty(MeetingSubmissionReadinessRules.Validate(meeting));

        meeting.Title = " ";
        meeting.Sensitivity = (Sensitivity)999;
        meeting.Description = new string('x', 4001);
        var invalid = MeetingSubmissionReadinessRules.Validate(meeting);

        Assert.Contains("title", invalid.Keys);
        Assert.Contains("sensitivity", invalid.Keys);
        Assert.Contains("description", invalid.Keys);
    }
}
