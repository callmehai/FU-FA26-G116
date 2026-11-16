using TrustCabinet.Application.Abstractions;
using TrustCabinet.Application.CalendarSharing;
using TrustCabinet.Application.Tests.Fakes;
using TrustCabinet.Domain.Audit;
using TrustCabinet.Domain.Enums;

namespace TrustCabinet.Application.Tests.CalendarSharing;

public sealed class CalendarShareStopperTests
{
    private static readonly DateTimeOffset Now = new(2026, 11, 16, 8, 0, 0, TimeSpan.Zero);
    private static readonly DateOnly Today = new(2026, 11, 16);

    private readonly FakeCalendarShareRepository _shares = new();
    private readonly FakeNotificationRepository _notifications = new();
    private readonly FakeUnitOfWork _unitOfWork = new();
    private readonly FakeAuditService _audit = new();
    private readonly Guid _owner;

    public CalendarShareStopperTests() => _owner = _shares.Person("Chủ lịch", organizer: false);

    private CalendarShareStopper Stopper()
        => new(_shares, _unitOfWork, new CalendarShareCloser(_notifications, _audit), new FixedClock(Now));

    /// <summary>The way UserService and RolePermissionService call it: inside their own transaction.</summary>
    private Task<int> StopInsideTheCallersTransaction(Guid userId)
        => _unitOfWork.ExecuteAsync(token => Stopper().StopDeadSharesAsync([userId], null,
            CalendarShareStopReason.ViewerLostMeetingCreator, token), TransactionMode.ReadCommitted);

    [Fact]
    public async Task A_share_whose_viewer_lost_the_right_is_stopped_inside_the_callers_transaction_and_both_sides_told()
    {
        var viewer = _shares.Person("Người xem", organizer: false);
        var share = _shares.Share(_owner, viewer, CalendarShareStatus.Effective, Today, Today.AddDays(5));

        Assert.Equal(1, await StopInsideTheCallersTransaction(viewer));

        Assert.Equal(CalendarShareStatus.Stopped, share.Status);
        Assert.Equal([_owner, viewer], _notifications.Added.Select(n => n.UserId));
        Assert.Equal([AuditActions.CalendarShareStopped], _audit.Actions);
        Assert.Equal(["effective"], _shares.Locks);
        // Joined, not opened: the caller's transaction is the one that commits.
        Assert.Equal([TransactionMode.ReadCommitted, TransactionMode.ReadCommitted], _unitOfWork.Modes);
        Assert.Equal(1, _unitOfWork.Commits);
    }

    [Fact]
    public async Task A_share_that_is_still_live_is_left_alone()
    {
        var viewer = _shares.Person("Người xem");
        var share = _shares.Share(_owner, viewer, CalendarShareStatus.Effective, Today, Today.AddDays(5));

        Assert.Equal(0, await StopInsideTheCallersTransaction(viewer));

        Assert.Equal(CalendarShareStatus.Effective, share.Status);
        Assert.Empty(_notifications.Added);
        Assert.Empty(_audit.Actions);
    }

    [Fact]
    public async Task No_people_reads_nothing()
    {
        Assert.Equal(0, await Stopper().StopDeadSharesAsync([], null, CalendarShareStopReason.AccountDeactivated));

        Assert.Empty(_unitOfWork.Modes);
    }

    [Fact]
    public async Task A_store_failure_reaches_the_caller_whose_transaction_it_is()
    {
        _shares.Fail = new PersistenceUnavailableException();

        await Assert.ThrowsAsync<PersistenceUnavailableException>(() => StopInsideTheCallersTransaction(_owner));

        Assert.Equal(1, _unitOfWork.Rollbacks);
    }
}
