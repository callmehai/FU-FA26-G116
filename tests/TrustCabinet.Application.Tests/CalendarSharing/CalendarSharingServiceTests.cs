using Microsoft.Extensions.Logging.Abstractions;
using TrustCabinet.Application.Abstractions;
using TrustCabinet.Application.CalendarSharing;
using TrustCabinet.Application.Tests.Fakes;
using TrustCabinet.Domain.Audit;
using TrustCabinet.Domain.CalendarSharing;
using TrustCabinet.Domain.Enums;

namespace TrustCabinet.Application.Tests.CalendarSharing;

public sealed class CalendarSharingServiceTests
{
    // 15:00 in Vietnam, Monday 16/11/2026.
    private static readonly DateTimeOffset Now = new(2026, 11, 16, 8, 0, 0, TimeSpan.Zero);
    private static readonly DateOnly Today = new(2026, 11, 16);

    private readonly FakeCalendarShareRepository _shares = new();
    private readonly FakeNotificationRepository _notifications = new();
    private readonly FakeUnitOfWork _unitOfWork = new();
    private readonly FakeAuditService _audit = new();
    private readonly Guid _organizer;
    private readonly Guid _owner;

    public CalendarSharingServiceTests()
    {
        _organizer = _shares.Person("Người tổ chức");
        _owner = _shares.Person("Chủ lịch", organizer: false);
    }

    private CalendarSharingService Service() => new(_shares, _notifications, _unitOfWork, _audit,
        new CalendarShareCloser(_notifications, _audit), new FixedClock(Now), NullLogger<CalendarSharingService>.Instance);

    private static ShareValues Asked() => new("Level1", Today, Today.AddDays(5));

    [Fact]
    public async Task A_request_stages_the_share_and_its_notice_and_commits_one_read_committed_transaction()
    {
        var outcome = await Service().RequestAsync(_organizer, _owner, Asked());

        Assert.Equal(CalendarShareSuccess.Requested, outcome.Success);
        var share = Assert.Single(_shares.Added);
        Assert.Equal((_owner, _organizer, CalendarShareStatus.Pending), (share.OwnerId, share.ViewerId, share.Status));
        Assert.Equal(_owner, Assert.Single(_notifications.Added).UserId);
        Assert.Equal([AuditActions.CalendarShareRequested], _audit.Actions);
        Assert.Equal(["lapsed"], _shares.Locks);
        Assert.Equal([TransactionMode.ReadCommitted], _unitOfWork.Modes);
        Assert.Equal((1, 0), (_unitOfWork.Commits, _unitOfWork.Rollbacks));
    }

    [Fact]
    public async Task A_request_from_someone_who_is_not_a_meeting_organizer_is_refused_and_stages_nothing()
    {
        var outcome = await Service().RequestAsync(_owner, _organizer, Asked());

        Assert.Equal(CalendarShareFailure.NotOrganizer, outcome.Failure);
        Assert.Empty(_shares.Added);
        Assert.Empty(_notifications.Added);
        Assert.Empty(_audit.Actions);
    }

    [Fact]
    public async Task A_request_for_a_pair_with_an_open_share_answers_already_shared()
    {
        _shares.Share(_owner, _organizer, CalendarShareStatus.Effective, Today, Today.AddDays(3));

        var outcome = await Service().RequestAsync(_organizer, _owner, Asked());

        Assert.Equal(CalendarShareFailure.AlreadyShared, outcome.Failure);
        Assert.Empty(_shares.Added);
    }

    [Fact]
    public async Task A_request_closes_the_pairs_share_past_its_end_date_without_telling_anyone()
    {
        var lapsed = _shares.Share(_owner, _organizer, CalendarShareStatus.Effective, Today.AddDays(-9), Today.AddDays(-1));

        var outcome = await Service().RequestAsync(_organizer, _owner, Asked());

        Assert.Equal(CalendarShareSuccess.Requested, outcome.Success);
        Assert.Equal((CalendarShareStatus.Ended, (Guid?)null), (lapsed.Status, lapsed.ClosedById));
        Assert.Equal([AuditActions.CalendarShareClosedAfterEndDate, AuditActions.CalendarShareRequested], _audit.Actions);
        Assert.Single(_notifications.Added);
    }

    [Fact]
    public async Task The_same_pair_saved_first_by_another_request_answers_already_shared_and_discards_the_staged_share()
    {
        _audit.Fail = new UniqueConflictException(UniqueConflict.CalendarShareOpenPair);

        var outcome = await Service().RequestAsync(_organizer, _owner, Asked());

        Assert.Equal(CalendarShareFailure.AlreadyShared, outcome.Failure);
        Assert.Equal((1, 1), (_unitOfWork.Rollbacks, _unitOfWork.Discards));
    }

    [Fact]
    public async Task Another_duplicate_is_a_save_failure_not_an_already_shared()
    {
        _audit.Fail = new UniqueConflictException(UniqueConflict.Unknown);

        var outcome = await Service().RequestAsync(_organizer, _owner, Asked());

        Assert.Equal(CalendarShareFailure.SaveFailed, outcome.Failure);
        Assert.Equal((1, 1), (_unitOfWork.Rollbacks, _unitOfWork.Discards));
    }

    [Fact]
    public async Task A_store_failure_answers_save_failed_rolls_back_and_discards()
    {
        _shares.Fail = new PersistenceUnavailableException();

        var outcome = await Service().RequestAsync(_organizer, _owner, Asked());

        Assert.Equal(CalendarShareFailure.SaveFailed, outcome.Failure);
        Assert.Equal((0, 1, 1), (_unitOfWork.Commits, _unitOfWork.Rollbacks, _unitOfWork.Discards));
        Assert.Empty(_notifications.Added);
    }

    [Fact]
    public async Task Accepting_a_pending_share_locks_the_share_and_both_people_then_tells_the_sender()
    {
        var share = _shares.Share(_owner, _organizer, CalendarShareStatus.Pending, Today, Today.AddDays(5));

        var outcome = await Service().AcceptAsync(_owner, share.Id, new ShareValues(null, null, null));

        Assert.Equal(CalendarShareSuccess.Accepted, outcome.Success);
        Assert.Equal(CalendarShareStatus.Effective, share.Status);
        Assert.Equal(["share", "people"], _shares.Locks);
        Assert.Equal(_organizer, Assert.Single(_notifications.Added).UserId);
        Assert.Equal([AuditActions.CalendarShareAccepted], _audit.Actions);
    }

    [Fact]
    public async Task Someone_who_is_not_on_the_share_is_answered_not_found()
    {
        var share = _shares.Share(_owner, _organizer, CalendarShareStatus.Pending, Today, Today.AddDays(5));
        var stranger = _shares.Person("Người ngoài");

        var outcome = await Service().DeclineAsync(stranger, share.Id);

        Assert.Equal(CalendarShareFailure.NotFound, outcome.Failure);
        Assert.Equal(CalendarShareStatus.Pending, share.Status);
    }

    [Fact]
    public async Task Ending_a_share_whose_viewer_lost_the_right_stops_it_and_tells_both_sides()
    {
        var viewer = _shares.Person("Người xem", organizer: false);
        var share = _shares.Share(_owner, viewer, CalendarShareStatus.Effective, Today, Today.AddDays(5));

        var outcome = await Service().EndAsync(_owner, share.Id);

        Assert.Equal(CalendarShareFailure.NotAllowed, outcome.Failure);
        Assert.Equal(CalendarShareStatus.Stopped, share.Status);
        Assert.Equal([_owner, viewer], _notifications.Added.Select(n => n.UserId));
        Assert.Equal([AuditActions.CalendarShareStopped], _audit.Actions);
        Assert.Equal(1, _unitOfWork.Commits);
    }

    [Fact]
    public async Task The_list_reads_outside_a_transaction_and_offers_no_accept_to_a_viewer_who_lost_the_right()
    {
        var viewer = _shares.Person("Người xem", organizer: false);
        _shares.Share(_owner, viewer, CalendarShareStatus.Pending, Today, Today.AddDays(5));

        var lists = (await Service().ListAsync(_owner)).Value!;

        Assert.Equal([CalendarShareRules.Decline], Assert.Single(lists.AsOwner).Actions);
        Assert.Empty(lists.AsViewer);
        Assert.Equal([TransactionMode.None], _unitOfWork.Modes);
    }

    [Fact]
    public async Task A_store_failure_on_the_list_answers_read_failed()
    {
        _shares.Fail = new PersistenceUnavailableException();

        Assert.Equal(CalendarShareFailure.ReadFailed, (await Service().ListAsync(_organizer)).Failure);
        Assert.Equal(CalendarShareFailure.CandidatesReadFailed,
            (await Service().CandidatesAsync(_organizer, new CandidateQuery(CandidatePurpose.Direct, null, 1, 20))).Failure);
    }

    [Fact]
    public async Task Only_a_meeting_organizer_may_list_people_to_ask()
    {
        var refused = await Service().CandidatesAsync(_owner, new CandidateQuery(CandidatePurpose.Request, null, 1, 20));
        var asked = await Service().CandidatesAsync(_organizer, new CandidateQuery(CandidatePurpose.Request, null, 1, 20));

        Assert.Equal(CalendarShareFailure.NotOrganizer, refused.Failure);
        Assert.Equal([_owner], asked.Value!.Items.Select(c => c.Id));
    }
}
