using TrustCabinet.Domain.Agenda;
using TrustCabinet.Domain.Entities;
using TrustCabinet.Domain.Enums;
using TrustCabinet.Domain.Meetings;

namespace TrustCabinet.Web.Tests.Meetings;

public sealed class MeetingSubmissionReadinessTests
{
    [Fact]
    public void Agenda_readiness_requires_a_nonempty_complete_contiguous_agenda()
    {
        var noAgenda = AgendaSubmissionRules.Validate([]);
        var invalid = AgendaSubmissionRules.Validate(
        [
            new AgendaItem
            {
                Position = 2,
                Title = " ",
                PlannedDurationMinutes = 0,
            },
        ]);

        Assert.Contains("agendaItems", noAgenda.Keys);
        Assert.Contains("agendaItems", invalid.Keys);
    }

    [Fact]
    public void Agenda_readiness_accepts_either_an_internal_or_guest_presenter()
    {
        var internalPresenter = new AgendaItem
        {
            Position = 1,
            Title = "Báo cáo",
            PlannedDurationMinutes = 30,
            PresenterId = Guid.CreateVersion7(),
        };
        var guestPresenter = new AgendaItem
        {
            Position = 2,
            Title = "Thảo luận",
            PlannedDurationMinutes = 15,
            PresenterName = "Khách mời",
        };

        var errors = AgendaSubmissionRules.Validate([internalPresenter, guestPresenter]);

        Assert.Empty(errors);
    }

    [Fact]
    public void Agenda_readiness_rejects_invalid_fields_and_non_pending_items()
    {
        var item = new AgendaItem
        {
            Position = 2,
            Title = new string('x', 301),
            Description = new string('x', 4001),
            PlannedDurationMinutes = 0,
            PresenterName = new string('x', 201),
            Status = AgendaItemStatus.InProgress,
        };

        var errors = AgendaSubmissionRules.Validate([item]);

        Assert.Contains("agendaItems", errors.Keys); // Position starts at 2 rather than 1.
        Assert.Contains($"agendaItems[{item.Id}].title", errors.Keys);
        Assert.Contains($"agendaItems[{item.Id}].description", errors.Keys);
        Assert.Contains($"agendaItems[{item.Id}].plannedDurationMinutes", errors.Keys);
        Assert.Contains($"agendaItems[{item.Id}].presenter", errors.Keys);
        Assert.Contains($"agendaItems[{item.Id}].status", errors.Keys);
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
            StartsAt = new DateTimeOffset(2026, 10, 1, 2, 0, 0, TimeSpan.Zero),
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
        meeting.EndsAt = meeting.StartsAt;
        meeting.Sensitivity = (Sensitivity)999;
        meeting.Description = new string('x', 4001);
        var invalid = MeetingSubmissionReadinessRules.Validate(meeting);

        Assert.Contains("title", invalid.Keys);
        Assert.Contains("endsAt", invalid.Keys);
        Assert.Contains("sensitivity", invalid.Keys);
        Assert.Contains("description", invalid.Keys);
    }
}
