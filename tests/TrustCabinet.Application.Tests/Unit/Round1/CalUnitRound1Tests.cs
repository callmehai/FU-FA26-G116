using Microsoft.Extensions.Logging.Abstractions;
using TrustCabinet.Application.Abstractions;
using TrustCabinet.Application.CalendarSharing;
using TrustCabinet.Application.Meetings;
using TrustCabinet.Application.Tests.Fakes;
using TrustCabinet.Domain.Audit;
using TrustCabinet.Domain.CalendarSharing;
using TrustCabinet.Domain.Enums;
using TrustCabinet.Domain.Participants;

namespace TrustCabinet.Application.Tests.Unit.Round1;

// Round 1 causation tests for FE-16 (Calendar): one xUnit test per kept UTC below.
// Each test carries BOTH [Trait("UTC", "<Class.Method>/<UTCnnn>")] (what apply-trx matches on,
// since the trx carries no trait) AND [Trait("Trait", "UTC")] (so --filter "Trait=UTC" selects
// them), plus a DisplayName containing the same "<Class.Method>/<UTCnnn>" string.
// Tester: Phạm Gia Khánh.
public sealed class CalUnitRound1Tests
{
    // 15:00 in Vietnam, Monday 16/11/2026.
    private static readonly DateTimeOffset Now = new(2026, 11, 16, 8, 0, 0, TimeSpan.Zero);
    private static readonly DateOnly Today = new(2026, 11, 16);
    private static readonly TimeSpan Vietnam = TimeSpan.FromHours(7);

    private readonly FakeCalendarShareRepository _shares = new();
    private readonly FakeCalendarBusyQuery _busy = new();
    private readonly FakeNotificationRepository _notifications = new();
    private readonly FakeUnitOfWork _unitOfWork = new();
    private readonly FakeAuditService _audit = new();
    private readonly Guid _organizer;
    private readonly Guid _owner;

    public CalUnitRound1Tests()
    {
        _organizer = _shares.Person("Người tổ chức");
        _owner = _shares.Person("Chủ lịch", organizer: false);
    }

    private CalendarSharingService SharingService() => new(_shares, _notifications, _unitOfWork, _audit,
        new CalendarShareCloser(_notifications, _audit), new FixedClock(Now), NullLogger<CalendarSharingService>.Instance);

    private SharedCalendarService WeekService() => new(_shares, _unitOfWork, new CalendarBusyReader(_busy, new FixedClock(Now)),
        new FixedClock(Now), NullLogger<SharedCalendarService>.Instance);

    private MeetingSlotService SlotService()
    {
        var meetings = new MeetingService(new FakeMeetingRepository(), new FakeMeetingInvitationStager(), _unitOfWork,
            NullLogger<MeetingService>.Instance, new FixedClock(Now));
        return new(_shares, _unitOfWork, new CalendarBusyReader(_busy, new FixedClock(Now)), meetings,
            new FixedClock(Now), NullLogger<MeetingSlotService>.Instance);
    }

    [Fact(DisplayName = "CalendarSharingService.RequestAsync/UTC001 organizer request stages a Pending share")]
    [Trait("UTC", "CalendarSharingService.RequestAsync/UTC001")]
    [Trait("Trait", "UTC")]
    public async Task RequestAsync_UTC001()
    {
        var outcome = await SharingService().RequestAsync(_organizer, _owner, new ShareValues("Level1", Today, Today.AddDays(30)));

        Assert.Equal(CalendarShareSuccess.Requested, outcome.Success);
        Assert.Equal(CalendarShareStatus.Pending, Assert.Single(_shares.Added).Status);
        Assert.Equal([AuditActions.CalendarShareRequested], _audit.Actions);
    }

    [Fact(DisplayName = "CalendarSharingService.RequestAsync/UTC003 caller without the organizer right is refused")]
    [Trait("UTC", "CalendarSharingService.RequestAsync/UTC003")]
    [Trait("Trait", "UTC")]
    public async Task RequestAsync_UTC003()
    {
        var outcome = await SharingService().RequestAsync(_owner, _organizer, new ShareValues("Level1", Today, Today.AddDays(30)));

        Assert.Equal(CalendarShareFailure.NotOrganizer, outcome.Failure);
        Assert.Empty(_shares.Added);
        Assert.Empty(_audit.Actions);
    }

    [Fact(DisplayName = "CalendarSharingService.AcceptAsync/UTC001 owner accepts as asked and the share turns Effective")]
    [Trait("UTC", "CalendarSharingService.AcceptAsync/UTC001")]
    [Trait("Trait", "UTC")]
    public async Task AcceptAsync_UTC001()
    {
        var share = _shares.Share(_owner, _organizer, CalendarShareStatus.Pending, Today, Today.AddDays(30),
            initiatedBy: CalendarShareInitiator.Viewer, level: CalendarShareLevel.Level2);

        var outcome = await SharingService().AcceptAsync(_owner, share.Id, new ShareValues(null, null, null));

        Assert.Equal(CalendarShareSuccess.Accepted, outcome.Success);
        Assert.Equal(CalendarShareStatus.Effective, share.Status);
        Assert.Equal([AuditActions.CalendarShareAccepted], _audit.Actions);
    }

    [Fact(DisplayName = "CalendarSharingService.AcceptAsync/UTC005 approving with nothing pending is refused")]
    [Trait("UTC", "CalendarSharingService.AcceptAsync/UTC005")]
    [Trait("Trait", "UTC")]
    public async Task AcceptAsync_UTC005()
    {
        var share = _shares.Share(_owner, _organizer, CalendarShareStatus.Effective, Today, Today.AddDays(30));

        var outcome = await SharingService().AcceptAsync(_owner, share.Id, new ShareValues(null, null, null));

        Assert.Equal(CalendarShareFailure.NotAllowed, outcome.Failure);
        Assert.Empty(_audit.Actions);
        Assert.Equal(CalendarShareStatus.Effective, share.Status);
    }

    [Fact(DisplayName = "CalendarSharingService.DeclineAsync/UTC001 owner declines a Pending request")]
    [Trait("UTC", "CalendarSharingService.DeclineAsync/UTC001")]
    [Trait("Trait", "UTC")]
    public async Task DeclineAsync_UTC001()
    {
        var share = _shares.Share(_owner, _organizer, CalendarShareStatus.Pending, Today, Today.AddDays(30));

        var outcome = await SharingService().DeclineAsync(_owner, share.Id);

        Assert.Equal(CalendarShareSuccess.Declined, outcome.Success);
        Assert.Equal((CalendarShareStatus.Declined, (Guid?)_owner), (share.Status, share.ClosedById));
        Assert.Equal([AuditActions.CalendarShareDeclined], _audit.Actions);
    }

    [Fact(DisplayName = "CalendarSharingService.ChangeAsync/UTC001 viewer asks for a higher level")]
    [Trait("UTC", "CalendarSharingService.ChangeAsync/UTC001")]
    [Trait("Trait", "UTC")]
    public async Task ChangeAsync_UTC001()
    {
        var share = _shares.Share(_owner, _organizer, CalendarShareStatus.Effective, Today, Today.AddDays(30));

        var outcome = await SharingService().ChangeAsync(_organizer, share.Id, new ShareValues("Level2", null, null));

        Assert.Equal(CalendarShareSuccess.ChangeRequested, outcome.Success);
        Assert.Equal(CalendarShareLevel.Level2, share.PendingLevel);
        Assert.Equal([AuditActions.CalendarShareChangeRequested], _audit.Actions);
    }

    [Fact(DisplayName = "CalendarSharingService.ChangeAsync/UTC004 asking for nothing is refused")]
    [Trait("UTC", "CalendarSharingService.ChangeAsync/UTC004")]
    [Trait("Trait", "UTC")]
    public async Task ChangeAsync_UTC004()
    {
        var share = _shares.Share(_owner, _organizer, CalendarShareStatus.Effective, Today, Today.AddDays(30));

        var outcome = await SharingService().ChangeAsync(_organizer, share.Id, new ShareValues(null, null, null));

        Assert.Equal(CalendarShareFailure.InvalidValues, outcome.Failure);
        Assert.Null(share.PendingLevel);
        Assert.Empty(_audit.Actions);
    }

    [Fact(DisplayName = "CalendarSharingService.WithdrawAsync/UTC001 sender withdraws a Pending request")]
    [Trait("UTC", "CalendarSharingService.WithdrawAsync/UTC001")]
    [Trait("Trait", "UTC")]
    public async Task WithdrawAsync_UTC001()
    {
        var share = _shares.Share(_owner, _organizer, CalendarShareStatus.Pending, Today, Today.AddDays(30),
            initiatedBy: CalendarShareInitiator.Viewer);

        var outcome = await SharingService().WithdrawAsync(_organizer, share.Id);

        Assert.Equal(CalendarShareSuccess.Withdrawn, outcome.Success);
        Assert.Equal((CalendarShareStatus.Withdrawn, (Guid?)_organizer), (share.Status, share.ClosedById));
        Assert.Equal([AuditActions.CalendarShareWithdrawn], _audit.Actions);
    }

    [Fact(DisplayName = "CalendarSharingService.EndAsync/UTC001 owner ends an Effective share")]
    [Trait("UTC", "CalendarSharingService.EndAsync/UTC001")]
    [Trait("Trait", "UTC")]
    public async Task EndAsync_UTC001()
    {
        var share = _shares.Share(_owner, _organizer, CalendarShareStatus.Effective, Today, Today.AddDays(30));

        var outcome = await SharingService().EndAsync(_owner, share.Id);

        Assert.Equal(CalendarShareSuccess.Ended, outcome.Success);
        Assert.Equal((CalendarShareStatus.Ended, (Guid?)_owner), (share.Status, share.ClosedById));
        Assert.Equal([AuditActions.CalendarShareEnded], _audit.Actions);
    }

    private static SlotSearchInput Search(params Guid[] userIds)
        => new(userIds, null, null, null, null, null, null, null, null, null, null, null, null);

    [Fact(DisplayName = "MeetingSlotService.FindAsync/UTC004 caller without the organizer right is refused")]
    [Trait("UTC", "MeetingSlotService.FindAsync/UTC004")]
    [Trait("Trait", "UTC")]
    public async Task FindAsync_UTC004()
    {
        var outcome = await SlotService().FindAsync(_owner, Search(_organizer));

        Assert.Equal(MeetingSlotFailure.NotOrganizer, outcome.Failure);
        Assert.Empty(_busy.Ranges);
        Assert.Equal([TransactionMode.None], _unitOfWork.Modes);
    }

    [Fact(DisplayName = "MeetingSlotService.FindAsync/UTC005 finder inviting themselves names the field")]
    [Trait("UTC", "MeetingSlotService.FindAsync/UTC005")]
    [Trait("Trait", "UTC")]
    public async Task FindAsync_UTC005()
    {
        var outcome = await SlotService().FindAsync(_organizer, Search(_organizer));

        Assert.Equal(MeetingSlotFailure.InvalidSearch, outcome.Failure);
        Assert.Equal("userIds", Assert.Single(outcome.Errors!.Keys));
        Assert.Empty(_busy.Ranges);
    }

    private static SlotDraftInput Draft(Guid invitee, string start, string end) => new("Họp triển khai quý IV", start, end,
        "InPerson", "Phòng 301", null, "Normal", "", [invitee], null, null, null);

    private static string Iso(DateTimeOffset value) => value.ToString("yyyy-MM-ddTHH:mm:sszzz");

    [Fact(DisplayName = "MeetingSlotService.CreateDraftAsync/UTC002 caller without the organizer right is refused")]
    [Trait("UTC", "MeetingSlotService.CreateDraftAsync/UTC002")]
    [Trait("Trait", "UTC")]
    public async Task CreateDraftAsync_UTC002()
    {
        var start = new DateTimeOffset(2026, 11, 18, 9, 0, 0, Vietnam);

        var outcome = await SlotService().CreateDraftAsync(_owner, Draft(_organizer, Iso(start), Iso(start.AddHours(1))));

        Assert.Equal(MeetingSlotFailure.NotOrganizer, outcome.Failure);
        Assert.Null(outcome.Meeting);
        Assert.Empty(_busy.Ranges);
    }

    [Fact(DisplayName = "MeetingSlotService.CreateDraftAsync/UTC004 offset-less start names the field")]
    [Trait("UTC", "MeetingSlotService.CreateDraftAsync/UTC004")]
    [Trait("Trait", "UTC")]
    public async Task CreateDraftAsync_UTC004()
    {
        var actor = _shares.Person("Người tìm khác");
        var outcome = await SlotService().CreateDraftAsync(actor,
            Draft(_organizer, "2026-11-24 09:00", "2026-11-24T10:00:00+07:00"));

        Assert.Equal(MeetingSlotFailure.InvalidSlot, outcome.Failure);
        Assert.Equal("plannedStartTime", Assert.Single(outcome.Errors!.Keys));
        Assert.Null(outcome.Meeting);
    }

    [Fact(DisplayName = "SharedCalendarService.ReadWeekAsync/UTC001 week shows whole days Monday to Sunday")]
    [Trait("UTC", "SharedCalendarService.ReadWeekAsync/UTC001")]
    [Trait("Trait", "UTC")]
    public async Task ReadWeekAsync_UTC001()
    {
        // Deviation covered by the UTC note: the code now shows the whole week
        // (Monday to Sunday) and the whole day (00:00-24:00).
        _shares.Share(_owner, _organizer, CalendarShareStatus.Effective, Today, Today.AddDays(10));
        var start = new DateTimeOffset(2026, 11, 16, 9, 0, 0, Vietnam);
        _busy.Rows.Add(new BusyAssignment(_owner, AssignmentStatus.Current, MeetingRole.Member, InvitationResponse.Accepted,
            null, MeetingStatus.Published, Sensitivity.Normal, start, start.AddHours(1), null, null));

        var week = (await WeekService().ReadWeekAsync(_organizer, null)).Value!;

        Assert.Equal((Today, Today.AddDays(6)), (week.WeekStart, week.WeekEnd));
        Assert.Equal(("00:00", "24:00"), (week.DayStart, week.DayEnd));
        Assert.Equal([new BusyInterval(start, start.AddHours(1))],
            week.People[0].Days.Single(d => d.Date == Today).Busy);
    }

    [Fact(DisplayName = "SharedCalendarService.ReadWeekAsync/UTC003 caller without the organizer right is refused")]
    [Trait("UTC", "SharedCalendarService.ReadWeekAsync/UTC003")]
    [Trait("Trait", "UTC")]
    public async Task ReadWeekAsync_UTC003()
    {
        Assert.Equal(CalendarShareFailure.NotOrganizer, (await WeekService().ReadWeekAsync(_owner, null)).Failure);
        Assert.Empty(_busy.Ranges);
        Assert.Equal([TransactionMode.None], _unitOfWork.Modes);
    }

    [Fact(DisplayName = "CalendarShareRules.Actions/UTC001 owner of a Pending request may accept or decline")]
    [Trait("UTC", "CalendarShareRules.Actions/UTC001")]
    [Trait("Trait", "UTC")]
    public Task Actions_UTC001()
    {
        var share = _shares.Share(_owner, _organizer, CalendarShareStatus.Pending, Today, Today.AddDays(30));

        var actions = CalendarShareRules.Actions(share, _owner, Today, acceptBlocked: false);

        Assert.Equal(["accept", "decline"], actions);
        return Task.CompletedTask;
    }

    [Fact(DisplayName = "CalendarShareRules.Actions/UTC006 stranger to the share may do nothing")]
    [Trait("UTC", "CalendarShareRules.Actions/UTC006")]
    [Trait("Trait", "UTC")]
    public Task Actions_UTC006()
    {
        var share = _shares.Share(_owner, _organizer, CalendarShareStatus.Pending, Today, Today.AddDays(30));

        var actions = CalendarShareRules.Actions(share, Guid.CreateVersion7(), Today, acceptBlocked: false);

        Assert.Empty(actions);
        return Task.CompletedTask;
    }
}
