using Microsoft.Extensions.Logging.Abstractions;
using TrustCabinet.Application.Abstractions;
using TrustCabinet.Application.CalendarSharing;
using TrustCabinet.Application.Tests.Fakes;
using TrustCabinet.Domain.CalendarSharing;
using TrustCabinet.Domain.Enums;
using TrustCabinet.Domain.Participants;

namespace TrustCabinet.Application.Tests.CalendarSharing;

public sealed class SharedCalendarServiceTests
{
    // 15:00 in Vietnam, Monday 16/11/2026.
    private static readonly DateTimeOffset Now = new(2026, 11, 16, 8, 0, 0, TimeSpan.Zero);
    private static readonly DateOnly Today = new(2026, 11, 16);
    private static readonly TimeSpan Vietnam = TimeSpan.FromHours(7);

    private readonly FakeCalendarShareRepository _shares = new();
    private readonly FakeCalendarBusyQuery _busy = new();
    private readonly FakeUnitOfWork _unitOfWork = new();
    private readonly Guid _viewer;
    private readonly Guid _owner;

    public SharedCalendarServiceTests()
    {
        _viewer = _shares.Person("Người xem");
        _owner = _shares.Person("Chủ lịch", organizer: false);
    }

    private SharedCalendarService Service()
    {
        var clock = new FixedClock(Now);
        return new(_shares, _unitOfWork, new CalendarBusyReader(_busy, clock), clock,
            NullLogger<SharedCalendarService>.Instance);
    }

    [Fact]
    public async Task The_week_shows_the_owners_busy_hours_read_outside_a_transaction()
    {
        _shares.Share(_owner, _viewer, CalendarShareStatus.Effective, Today, Today.AddDays(10));
        var start = new DateTimeOffset(2026, 11, 17, 9, 0, 0, Vietnam);
        _busy.Rows.Add(new BusyAssignment(_owner, AssignmentStatus.Current, MeetingRole.Member, InvitationResponse.Accepted,
            null, MeetingStatus.Published, Sensitivity.Normal, start, start.AddHours(1), null, null));

        var week = (await Service().ReadWeekAsync(_viewer, null)).Value!;

        var person = Assert.Single(week.People);
        Assert.Equal(nameof(CalendarShareStatus.Effective), person.Status);
        var tuesday = person.Days.Single(d => d.Date == Today.AddDays(1));
        Assert.Equal([new BusyInterval(start, start.AddHours(1))], tuesday.Busy);
        Assert.Equal([TransactionMode.None], _unitOfWork.Modes);
    }

    [Fact]
    public async Task Someone_who_is_not_a_meeting_organizer_is_refused()
    {
        Assert.Equal(CalendarShareFailure.NotOrganizer, (await Service().ReadWeekAsync(_owner, null)).Failure);
        Assert.Empty(_busy.Ranges);
    }

    [Fact]
    public async Task A_store_failure_answers_read_failed()
    {
        _shares.Fail = new PersistenceUnavailableException();

        Assert.Equal(CalendarShareFailure.ReadFailed, (await Service().ReadWeekAsync(_viewer, null)).Failure);
    }
}
