using Microsoft.Extensions.Logging.Abstractions;
using TrustCabinet.Application.Abstractions;
using TrustCabinet.Application.CalendarSharing;
using TrustCabinet.Application.Meetings;
using TrustCabinet.Application.Tests.Fakes;
using TrustCabinet.Domain.CalendarSharing;
using TrustCabinet.Domain.Enums;
using TrustCabinet.Domain.Meetings;
using TrustCabinet.Domain.Participants;

namespace TrustCabinet.Application.Tests.CalendarSharing;

public sealed class MeetingSlotServiceTests
{
    // 15:00 in Vietnam, Monday 16/11/2026.
    private static readonly DateTimeOffset Now = new(2026, 11, 16, 8, 0, 0, TimeSpan.Zero);
    private static readonly DateOnly Today = new(2026, 11, 16);
    private static readonly TimeSpan Vietnam = TimeSpan.FromHours(7);

    private readonly FakeCalendarShareRepository _shares = new();
    private readonly FakeCalendarBusyQuery _busy = new();
    private readonly FakeMeetingRepository _meetings = new();
    private readonly FakeUnitOfWork _unitOfWork = new();
    private readonly Guid _finder;
    private readonly Guid _invitee;

    public MeetingSlotServiceTests()
    {
        _finder = _shares.Person("Người tìm");
        _invitee = _shares.Person("Người được mời", organizer: false);
    }

    private MeetingSlotService Service()
    {
        var clock = new FixedClock(Now);
        var meetings = new MeetingService(_meetings, new FakeMeetingInvitationStager(), _unitOfWork, NullLogger<MeetingService>.Instance, clock);
        return new(_shares, _unitOfWork, new CalendarBusyReader(_busy, clock), meetings, clock,
            NullLogger<MeetingSlotService>.Instance);
    }

    private static SlotSearchInput Search(params Guid[] userIds)
        => new(userIds, null, null, null, null, null, null, null, null, null, null, null, null);

    private static string Iso(DateTimeOffset value) => value.ToString("yyyy-MM-ddTHH:mm:sszzz");

    private SlotDraftInput Draft(DateTimeOffset start) => new("Họp triển khai quý IV", Iso(start), Iso(start.AddHours(1)),
        "InPerson", "Phòng 301", null, "Normal", "", [_invitee], null, null, null);

    [Fact]
    public async Task A_search_reads_the_shared_calendar_outside_a_transaction()
    {
        _shares.Share(_invitee, _finder, CalendarShareStatus.Effective, Today, Today.AddDays(10));

        var outcome = await Service().FindAsync(_finder, Search(_invitee));

        Assert.Equal(MeetingSlotFailure.None, outcome.Failure);
        Assert.Equal(SlotPersonStatus.Shared, Assert.Single(outcome.Result!.People).Status);
        Assert.Single(_busy.Ranges);
        Assert.Equal([TransactionMode.None], _unitOfWork.Modes);
    }

    [Fact]
    public async Task A_search_by_someone_who_is_not_a_meeting_organizer_is_refused()
    {
        Assert.Equal(MeetingSlotFailure.NotOrganizer, (await Service().FindAsync(_invitee, Search(_finder))).Failure);
        Assert.Empty(_busy.Ranges);
    }

    [Fact]
    public async Task A_search_for_a_deactivated_invitee_names_the_field()
    {
        var gone = _shares.Person("Đã nghỉ", active: false);

        var outcome = await Service().FindAsync(_finder, Search(gone));

        Assert.Equal(MeetingSlotFailure.InvalidSearch, outcome.Failure);
        Assert.Equal(["userIds[0]"], outcome.Errors!.Keys);
    }

    [Fact]
    public async Task A_store_failure_on_the_search_answers_read_failed()
    {
        _shares.Fail = new PersistenceUnavailableException();

        Assert.Equal(MeetingSlotFailure.ReadFailed, (await Service().FindAsync(_finder, Search(_invitee))).Failure);
    }

    [Fact]
    public async Task A_free_slot_becomes_a_draft_with_the_invitee_chairing_and_the_finder_as_secretary()
    {
        _shares.Share(_invitee, _finder, CalendarShareStatus.Effective, Today, Today.AddDays(10));

        var outcome = await Service().CreateDraftAsync(_finder, Draft(new DateTimeOffset(2026, 11, 18, 9, 0, 0, Vietnam)));

        Assert.Equal(MeetingSlotFailure.None, outcome.Failure);
        var meeting = Assert.Single(_meetings.Added);
        Assert.Equal([(_invitee, MeetingRole.Chair), (_finder, MeetingRole.Secretary)],
            meeting.Participants.Select(p => (p.UserId, p.Role)));
        // The recheck reads outside a transaction; UC-14 then saves in its own.
        Assert.Equal([TransactionMode.None, TransactionMode.ReadCommitted], _unitOfWork.Modes);
    }

    [Fact]
    public async Task A_slot_taken_since_the_search_is_refused_and_creates_nothing()
    {
        _shares.Share(_invitee, _finder, CalendarShareStatus.Effective, Today, Today.AddDays(10));
        var start = new DateTimeOffset(2026, 11, 18, 9, 0, 0, Vietnam);
        _busy.Rows.Add(new BusyAssignment(_invitee, AssignmentStatus.Current, MeetingRole.Member,
            InvitationResponse.Accepted, null, MeetingStatus.Published, Sensitivity.Normal, start, start.AddHours(1), null,
            null));

        var outcome = await Service().CreateDraftAsync(_finder, Draft(start));

        Assert.Equal(MeetingSlotFailure.SlotTaken, outcome.Failure);
        Assert.Empty(_meetings.Added);
    }

    [Fact]
    public async Task A_store_failure_on_the_recheck_answers_read_failed_and_creates_nothing()
    {
        _shares.Fail = new PersistenceUnavailableException();

        var outcome = await Service().CreateDraftAsync(_finder, Draft(new DateTimeOffset(2026, 11, 18, 9, 0, 0, Vietnam)));

        Assert.Equal(MeetingSlotFailure.ReadFailed, outcome.Failure);
        Assert.Empty(_meetings.Added);
    }
}
