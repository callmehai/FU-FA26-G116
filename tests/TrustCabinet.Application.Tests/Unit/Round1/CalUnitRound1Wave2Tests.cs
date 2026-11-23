using Microsoft.Extensions.Logging;
using TrustCabinet.Application.Abstractions;
using TrustCabinet.Application.CalendarSharing;
using TrustCabinet.Application.Meetings;
using TrustCabinet.Application.Tests.Fakes;
using TrustCabinet.Domain.Audit;
using TrustCabinet.Domain.CalendarSharing;
using TrustCabinet.Domain.Entities;
using TrustCabinet.Domain.Enums;
using TrustCabinet.Domain.Meetings;
using TrustCabinet.Domain.Participants;

namespace TrustCabinet.Application.Tests.Unit.Round1;

// Round 1 wave-2 causation tests for FE-16 (Calendar): one xUnit test per UTC listed in
// wave2_W2A.txt (CalendarShareRules.Actions + CalendarSharingService.* + MeetingSlotService.*
// + SharedCalendarService.ReadWeekAsync).
// Each test carries BOTH [Trait("UTC", "<Class.Method>/<UTCnnn>")] (what apply-trx matches on,
// since the trx carries no trait) AND [Trait("Trait", "UTC")] (so --filter "Trait=UTC" selects
// them), plus a DisplayName containing the same "<Class.Method>/<UTCnnn>" string.
// The asserts cover the Expected triple (Return / Exception / Log Message).
// Tester: Phạm Gia Khánh.
public sealed class CalUnitRound1Wave2Tests
{
    // 10:00 in Vietnam, Tuesday 18/11/2026.
    private static readonly DateTimeOffset Now = new(2026, 11, 18, 3, 0, 0, TimeSpan.Zero);
    private static readonly DateOnly Today = new(2026, 11, 18);
    private static readonly DateOnly End30 = new(2026, 12, 18);
    private static readonly TimeSpan Vietnam = TimeSpan.FromHours(7);

    private static DateTimeOffset At(int day, int hour, int minute)
        => new(2026, 11, day, hour, minute, 0, Vietnam);

    private static string Iso(DateTimeOffset value) => value.ToString("yyyy-MM-ddTHH:mm:sszzz");

    // ---- CalendarShareRules.Actions ----

    [Fact(DisplayName = "CalendarShareRules.Actions/UTC002 Pending share with blocked accept offers decline only")]
    [Trait("UTC", "CalendarShareRules.Actions/UTC002")]
    [Trait("Trait", "UTC")]
    public void Actions_UTC002()
    {
        var c = new Cal();
        var share = c.Shares.Share(c.Owner, c.Viewer, CalendarShareStatus.Pending, Today, End30,
            CalendarShareInitiator.Viewer);

        Assert.Equal(["decline"], CalendarShareRules.Actions(share, c.Owner, Today, acceptBlocked: true));
    }

    [Fact(DisplayName = "CalendarShareRules.Actions/UTC003 blocked accept does not touch the sender side")]
    [Trait("UTC", "CalendarShareRules.Actions/UTC003")]
    [Trait("Trait", "UTC")]
    public void Actions_UTC003()
    {
        var c = new Cal();
        var share = c.Shares.Share(c.Owner, c.Viewer, CalendarShareStatus.Pending, Today, End30,
            CalendarShareInitiator.Viewer);

        Assert.Equal(["withdraw"], CalendarShareRules.Actions(share, c.Viewer, Today, acceptBlocked: true));
    }

    [Fact(DisplayName = "CalendarShareRules.Actions/UTC004 stale pending change can only be declined")]
    [Trait("UTC", "CalendarShareRules.Actions/UTC004")]
    [Trait("Trait", "UTC")]
    public void Actions_UTC004()
    {
        var c = new Cal();
        var share = c.Shares.Share(c.Owner, c.Viewer, CalendarShareStatus.Effective, Today, End30);
        share.PendingLevel = CalendarShareLevel.Level2;
        share.PendingEndDate = Today.AddDays(-1);
        share.PendingRequestedAt = Now;

        Assert.Equal(["decline", "update", "end"],
            CalendarShareRules.Actions(share, c.Owner, Today, acceptBlocked: false));
    }

    [Fact(DisplayName = "CalendarShareRules.Actions/UTC005 blocked accept is ignored for an Effective share")]
    [Trait("UTC", "CalendarShareRules.Actions/UTC005")]
    [Trait("Trait", "UTC")]
    public void Actions_UTC005()
    {
        var c = new Cal();
        var share = c.Shares.Share(c.Owner, c.Viewer, CalendarShareStatus.Effective, Today, End30);

        Assert.Equal(["update", "end"],
            CalendarShareRules.Actions(share, c.Owner, Today, acceptBlocked: true));
    }

    [Fact(DisplayName = "CalendarShareRules.Actions/UTC007 lapsed share allows nothing")]
    [Trait("UTC", "CalendarShareRules.Actions/UTC007")]
    [Trait("Trait", "UTC")]
    public void Actions_UTC007()
    {
        var c = new Cal();
        var share = c.Shares.Share(c.Owner, c.Viewer, CalendarShareStatus.Effective,
            Today.AddDays(-30), Today.AddDays(-1));

        Assert.Empty(CalendarShareRules.Actions(share, c.Owner, Today, acceptBlocked: false));
    }

    [Fact(DisplayName = "CalendarShareRules.Actions/UTC008 last day still counts as Effective")]
    [Trait("UTC", "CalendarShareRules.Actions/UTC008")]
    [Trait("Trait", "UTC")]
    public void Actions_UTC008()
    {
        var c = new Cal();
        var share = c.Shares.Share(c.Owner, c.Viewer, CalendarShareStatus.Effective,
            Today.AddDays(-30), Today);

        Assert.Equal(["update", "end"],
            CalendarShareRules.Actions(share, c.Owner, Today, acceptBlocked: false));
    }

    // ---- CalendarSharingService.RequestAsync ----

    [Fact(DisplayName = "CalendarSharingService.RequestAsync/UTC002 longest range at Level2 is requested")]
    [Trait("UTC", "CalendarSharingService.RequestAsync/UTC002")]
    [Trait("Trait", "UTC")]
    public async Task Request_UTC002()
    {
        var c = new Cal();

        var outcome = await c.Sharing().RequestAsync(c.Viewer, c.Owner,
            new ShareValues("Level2", Today, new DateOnly(2027, 11, 17)));

        Assert.Equal(CalendarShareFailure.None, outcome.Failure); // Return + Exception: answered, not thrown.
        Assert.Equal(CalendarShareSuccess.Requested, outcome.Success); // Return.
        var saved = Assert.Single(c.Shares.Added); // Effect: staged.
        Assert.Equal((c.Owner, c.Viewer, CalendarShareStatus.Pending, CalendarShareInitiator.Viewer,
            CalendarShareLevel.Level2), (saved.OwnerId, saved.ViewerId, saved.Status, saved.InitiatedBy,
            saved.Level)); // Effect.
        Assert.Equal(c.Owner, Assert.Single(c.Notifications.Added).UserId); // Effect: owner told.
        Assert.Equal([AuditActions.CalendarShareRequested], c.Audit.Actions); // Effect: audited.
        Assert.Empty(c.ShareLog.Entries); // Log: N/A.
    }

    [Fact(DisplayName = "CalendarSharingService.RequestAsync/UTC004 unknown owner is refused silently")]
    [Trait("UTC", "CalendarSharingService.RequestAsync/UTC004")]
    [Trait("Trait", "UTC")]
    public async Task Request_UTC004()
    {
        var c = new Cal();

        var outcome = await c.Sharing().RequestAsync(c.Viewer, null,
            new ShareValues("Level1", Today, End30));

        Assert.Equal(CalendarShareFailure.NotFound, outcome.Failure); // Return + Exception: answered, not thrown.
        Assert.Empty(c.Shares.Added); // Effect: nothing saved.
        Assert.Empty(c.Notifications.Added); // Effect: nobody told.
        Assert.Empty(c.Audit.Actions); // Effect: nothing audited.
        Assert.Empty(c.ShareLog.Entries); // Log: N/A.
    }

    [Fact(DisplayName = "CalendarSharingService.RequestAsync/UTC005 open share blocks a second request")]
    [Trait("UTC", "CalendarSharingService.RequestAsync/UTC005")]
    [Trait("Trait", "UTC")]
    public async Task Request_UTC005()
    {
        var c = new Cal();
        c.Shares.Share(c.Owner, c.Viewer, CalendarShareStatus.Pending, Today, End30,
            CalendarShareInitiator.Viewer);

        var outcome = await c.Sharing().RequestAsync(c.Viewer, c.Owner,
            new ShareValues("Level1", Today, End30));

        Assert.Equal(CalendarShareFailure.AlreadyShared, outcome.Failure); // Return + Exception.
        Assert.Empty(c.Shares.Added); // Effect: nothing saved.
        Assert.Empty(c.Notifications.Added); // Effect.
        Assert.Empty(c.Audit.Actions); // Effect.
        Assert.Empty(c.ShareLog.Entries); // Log: N/A.
    }

    [Fact(DisplayName = "CalendarSharingService.RequestAsync/UTC006 closed share does not block a new one")]
    [Trait("UTC", "CalendarSharingService.RequestAsync/UTC006")]
    [Trait("Trait", "UTC")]
    public async Task Request_UTC006()
    {
        var c = new Cal();
        c.Shares.Share(c.Owner, c.Viewer, CalendarShareStatus.Declined, Today, End30,
            CalendarShareInitiator.Viewer);

        var outcome = await c.Sharing().RequestAsync(c.Viewer, c.Owner,
            new ShareValues("Level1", Today, End30));

        Assert.Equal(CalendarShareSuccess.Requested, outcome.Success); // Return.
        Assert.Equal(CalendarShareFailure.None, outcome.Failure); // Exception: answered, not thrown.
        Assert.Single(c.Shares.Added); // Effect: the new share staged.
        Assert.Equal(c.Owner, Assert.Single(c.Notifications.Added).UserId); // Effect.
        Assert.Equal([AuditActions.CalendarShareRequested], c.Audit.Actions); // Effect.
        Assert.Empty(c.ShareLog.Entries); // Log: N/A.
    }

    [Fact(DisplayName = "CalendarSharingService.RequestAsync/UTC007 request without a level is refused")]
    [Trait("UTC", "CalendarSharingService.RequestAsync/UTC007")]
    [Trait("Trait", "UTC")]
    public async Task Request_UTC007()
    {
        var c = new Cal();

        var outcome = await c.Sharing().RequestAsync(c.Viewer, c.Owner,
            new ShareValues(null, Today, End30));

        Assert.Equal(CalendarShareFailure.InvalidValues, outcome.Failure); // Return + Exception.
        Assert.Empty(c.Shares.Added); // Effect.
        Assert.Empty(c.Audit.Actions); // Effect.
        Assert.Empty(c.ShareLog.Entries); // Log: N/A.
    }

    [Fact(DisplayName = "CalendarSharingService.RequestAsync/UTC008 save failure answers save failed")]
    [Trait("UTC", "CalendarSharingService.RequestAsync/UTC008")]
    [Trait("Trait", "UTC")]
    public async Task Request_UTC008()
    {
        var c = new Cal();
        c.Audit.Fail = new PersistenceUnavailableException();

        var outcome = await c.Sharing().RequestAsync(c.Viewer, c.Owner,
            new ShareValues("Level1", Today, End30));

        Assert.Equal(CalendarShareFailure.SaveFailed, outcome.Failure); // Return + Exception: converted, not thrown.
        Assert.Empty(c.Audit.Actions); // Effect: no audit row kept.
        Assert.Equal(1, c.Uow.Discards); // Effect: changes discarded.
        Assert.Contains(c.ShareLog.Entries, e => e.Level == LogLevel.Error
            && e.Message.Contains("Could not save the calendar share")); // Log.
    }

    // ---- CalendarSharingService.AcceptAsync ----

    [Fact(DisplayName = "CalendarSharingService.AcceptAsync/UTC002 accepting exactly as asked succeeds")]
    [Trait("UTC", "CalendarSharingService.AcceptAsync/UTC002")]
    [Trait("Trait", "UTC")]
    public async Task Accept_UTC002()
    {
        var c = new Cal();
        var share = c.Shares.Share(c.Owner, c.Viewer, CalendarShareStatus.Pending,
            new DateOnly(2026, 11, 20), new DateOnly(2026, 12, 20),
            CalendarShareInitiator.Viewer, CalendarShareLevel.Level2);

        var outcome = await c.Sharing().AcceptAsync(c.Owner, share.Id, new ShareValues("Level2",
            new DateOnly(2026, 11, 20), new DateOnly(2026, 12, 20)));

        Assert.Equal(CalendarShareFailure.None, outcome.Failure); // Return + Exception.
        Assert.Equal(CalendarShareSuccess.Accepted, outcome.Success); // Return.
        Assert.Equal((CalendarShareStatus.Effective, CalendarShareLevel.Level2, Now),
            (share.Status, share.Level, share.DecidedAt)); // Effect: Effective as asked.
        Assert.Contains("people", c.Shares.Locks); // Effect: both accounts locked and re-read.
        Assert.Equal(c.Viewer, Assert.Single(c.Notifications.Added).UserId); // Effect: sender told.
        Assert.Equal([AuditActions.CalendarShareAccepted], c.Audit.Actions); // Effect.
        Assert.Empty(c.ShareLog.Entries); // Log: N/A.
    }

    [Fact(DisplayName = "CalendarSharingService.AcceptAsync/UTC003 longer end than asked is refused")]
    [Trait("UTC", "CalendarSharingService.AcceptAsync/UTC003")]
    [Trait("Trait", "UTC")]
    public async Task Accept_UTC003()
    {
        var c = new Cal();
        var share = c.Shares.Share(c.Owner, c.Viewer, CalendarShareStatus.Pending,
            new DateOnly(2026, 11, 20), new DateOnly(2026, 12, 20),
            CalendarShareInitiator.Viewer, CalendarShareLevel.Level2);

        var outcome = await c.Sharing().AcceptAsync(c.Owner, share.Id,
            new ShareValues(null, null, new DateOnly(2026, 12, 21)));

        Assert.Equal(CalendarShareFailure.InvalidValues, outcome.Failure); // Return + Exception.
        Assert.Equal(CalendarShareStatus.Pending, share.Status); // Effect: stays Pending.
        Assert.Empty(c.Notifications.Added); // Effect.
        Assert.Empty(c.Audit.Actions); // Effect.
        Assert.Empty(c.ShareLog.Entries); // Log: N/A.
    }

    [Fact(DisplayName = "CalendarSharingService.AcceptAsync/UTC004 sender cannot answer their own request")]
    [Trait("UTC", "CalendarSharingService.AcceptAsync/UTC004")]
    [Trait("Trait", "UTC")]
    public async Task Accept_UTC004()
    {
        var c = new Cal();
        var share = c.Shares.Share(c.Owner, c.Viewer, CalendarShareStatus.Pending,
            new DateOnly(2026, 11, 20), new DateOnly(2026, 12, 20),
            CalendarShareInitiator.Viewer, CalendarShareLevel.Level2);

        var outcome = await c.Sharing().AcceptAsync(c.Viewer, share.Id, new ShareValues(null, null, null));

        Assert.Equal(CalendarShareFailure.NotFound, outcome.Failure); // Return + Exception.
        Assert.Equal(CalendarShareStatus.Pending, share.Status); // Effect.
        Assert.Empty(c.Audit.Actions); // Effect.
        Assert.Empty(c.ShareLog.Entries); // Log: N/A.
    }

    [Fact(DisplayName = "CalendarSharingService.AcceptAsync/UTC006 deactivated sender blocks accepting")]
    [Trait("UTC", "CalendarSharingService.AcceptAsync/UTC006")]
    [Trait("Trait", "UTC")]
    public async Task Accept_UTC006()
    {
        var c = new Cal();
        var share = c.Shares.Share(c.Owner, c.Viewer, CalendarShareStatus.Pending,
            new DateOnly(2026, 11, 20), new DateOnly(2026, 12, 20),
            CalendarShareInitiator.Viewer, CalendarShareLevel.Level2);
        c.Deactivate(c.Viewer);

        var outcome = await c.Sharing().AcceptAsync(c.Owner, share.Id, new ShareValues(null, null, null));

        Assert.Equal(CalendarShareFailure.SenderDeactivated, outcome.Failure); // Return + Exception.
        Assert.Equal(CalendarShareStatus.Pending, share.Status); // Effect: stays Pending.
        Assert.Empty(c.Notifications.Added); // Effect.
        Assert.Empty(c.Audit.Actions); // Effect.
        Assert.Empty(c.ShareLog.Entries); // Log: N/A.
    }

    [Fact(DisplayName = "CalendarSharingService.AcceptAsync/UTC007 pending change is approved as asked")]
    [Trait("UTC", "CalendarSharingService.AcceptAsync/UTC007")]
    [Trait("Trait", "UTC")]
    public async Task Accept_UTC007()
    {
        var c = new Cal();
        var share = c.Shares.Share(c.Owner, c.Viewer, CalendarShareStatus.Effective, Today, End30);
        share.PendingLevel = CalendarShareLevel.Level2;
        share.PendingEndDate = new DateOnly(2026, 12, 30);
        share.PendingRequestedAt = Now;

        var outcome = await c.Sharing().AcceptAsync(c.Owner, share.Id, new ShareValues(null, null, null));

        Assert.Equal(CalendarShareFailure.None, outcome.Failure); // Return + Exception.
        Assert.Equal(CalendarShareSuccess.Updated, outcome.Success); // Return.
        Assert.Equal((CalendarShareLevel.Level2, new DateOnly(2026, 12, 30)),
            (share.Level, share.EndDate)); // Effect: applied.
        Assert.Null(share.PendingRequestedAt); // Effect: pending cleared.
        Assert.Equal(c.Viewer, Assert.Single(c.Notifications.Added).UserId); // Effect.
        Assert.Equal([AuditActions.CalendarShareChangeAccepted], c.Audit.Actions); // Effect.
        Assert.Empty(c.ShareLog.Entries); // Log: N/A.
    }

    [Fact(DisplayName = "CalendarSharingService.AcceptAsync/UTC008 save failure keeps the share Pending")]
    [Trait("UTC", "CalendarSharingService.AcceptAsync/UTC008")]
    [Trait("Trait", "UTC")]
    public async Task Accept_UTC008()
    {
        var c = new Cal();
        var share = c.Shares.Share(c.Owner, c.Viewer, CalendarShareStatus.Pending,
            new DateOnly(2026, 11, 20), new DateOnly(2026, 12, 20),
            CalendarShareInitiator.Viewer, CalendarShareLevel.Level2);
        c.Audit.Fail = new PersistenceUnavailableException();

        var outcome = await c.Sharing().AcceptAsync(c.Owner, share.Id, new ShareValues(null, null, null));

        Assert.Equal(CalendarShareFailure.SaveFailed, outcome.Failure); // Return + Exception: converted.
        // No share-state assert: the hand fake stages in memory without rollback (real EF discards
        // via the UoW); Discards == 1 below is the rollback evidence.
        Assert.Equal(1, c.Uow.Discards); // Effect: discarded.
        Assert.Contains(c.ShareLog.Entries, e => e.Level == LogLevel.Error
            && e.Message.Contains("Could not save the calendar share")); // Log.
    }

    // ---- CalendarSharingService.ChangeAsync ----

    [Fact(DisplayName = "CalendarSharingService.ChangeAsync/UTC002 level and end change together")]
    [Trait("UTC", "CalendarSharingService.ChangeAsync/UTC002")]
    [Trait("Trait", "UTC")]
    public async Task Change_UTC002()
    {
        var c = new Cal();
        var share = c.Shares.Share(c.Owner, c.Viewer, CalendarShareStatus.Effective, Today, End30);

        var outcome = await c.Sharing().ChangeAsync(c.Viewer, share.Id,
            new ShareValues("Level2", Today, new DateOnly(2026, 12, 25)));

        Assert.Equal(CalendarShareFailure.None, outcome.Failure); // Return + Exception.
        Assert.Equal(CalendarShareSuccess.ChangeRequested, outcome.Success); // Return.
        Assert.Equal(CalendarShareLevel.Level1, share.Level); // Effect: stays at Level1.
        Assert.Equal((CalendarShareLevel.Level2, new DateOnly(2026, 12, 25), Now),
            (share.PendingLevel, share.PendingEndDate, share.PendingRequestedAt)); // Effect: pending asked.
        Assert.Equal(c.Owner, Assert.Single(c.Notifications.Added).UserId); // Effect.
        Assert.Equal([AuditActions.CalendarShareChangeRequested], c.Audit.Actions); // Effect.
        Assert.Empty(c.ShareLog.Entries); // Log: N/A.
    }

    [Fact(DisplayName = "CalendarSharingService.ChangeAsync/UTC003 end at exactly 365 days is asked")]
    [Trait("UTC", "CalendarSharingService.ChangeAsync/UTC003")]
    [Trait("Trait", "UTC")]
    public async Task Change_UTC003()
    {
        var c = new Cal();
        var share = c.Shares.Share(c.Owner, c.Viewer, CalendarShareStatus.Effective, Today, End30);

        var outcome = await c.Sharing().ChangeAsync(c.Viewer, share.Id,
            new ShareValues(null, null, new DateOnly(2027, 11, 17)));

        Assert.Equal(CalendarShareSuccess.ChangeRequested, outcome.Success); // Return.
        Assert.Equal(CalendarShareFailure.None, outcome.Failure); // Exception: answered, not thrown.
        Assert.Equal(new DateOnly(2027, 11, 17), share.PendingEndDate); // Effect.
        Assert.Equal(c.Owner, Assert.Single(c.Notifications.Added).UserId); // Effect.
        Assert.Equal([AuditActions.CalendarShareChangeRequested], c.Audit.Actions); // Effect.
        Assert.Empty(c.ShareLog.Entries); // Log: N/A.
    }

    [Fact(DisplayName = "CalendarSharingService.ChangeAsync/UTC005 second pending change is refused")]
    [Trait("UTC", "CalendarSharingService.ChangeAsync/UTC005")]
    [Trait("Trait", "UTC")]
    public async Task Change_UTC005()
    {
        var c = new Cal();
        var share = c.Shares.Share(c.Owner, c.Viewer, CalendarShareStatus.Effective, Today, End30);
        share.PendingLevel = CalendarShareLevel.Level2;
        share.PendingEndDate = new DateOnly(2026, 12, 30);
        share.PendingRequestedAt = Now;

        var outcome = await c.Sharing().ChangeAsync(c.Viewer, share.Id,
            new ShareValues(null, null, new DateOnly(2027, 1, 18)));

        Assert.Equal(CalendarShareFailure.NotAllowed, outcome.Failure); // Return + Exception.
        Assert.Equal((CalendarShareLevel.Level2, new DateOnly(2026, 12, 30)),
            (share.PendingLevel, share.PendingEndDate)); // Effect: first ask kept.
        Assert.Empty(c.Notifications.Added); // Effect.
        Assert.Empty(c.Audit.Actions); // Effect.
        Assert.Empty(c.ShareLog.Entries); // Log: N/A.
    }

    [Fact(DisplayName = "CalendarSharingService.ChangeAsync/UTC006 owner lowers the level at once")]
    [Trait("UTC", "CalendarSharingService.ChangeAsync/UTC006")]
    [Trait("Trait", "UTC")]
    public async Task Change_UTC006()
    {
        var c = new Cal();
        var share = c.Shares.Share(c.Owner, c.Viewer, CalendarShareStatus.Effective,
            new DateOnly(2026, 11, 10), new DateOnly(2026, 12, 10),
            CalendarShareInitiator.Viewer, CalendarShareLevel.Level2);

        var outcome = await c.Sharing().ChangeAsync(c.Owner, share.Id,
            new ShareValues("Level1", null, null));

        Assert.Equal(CalendarShareFailure.None, outcome.Failure); // Return + Exception.
        Assert.Equal(CalendarShareSuccess.Updated, outcome.Success); // Return.
        Assert.Equal((CalendarShareLevel.Level1, new DateOnly(2026, 11, 10), new DateOnly(2026, 12, 10)),
            (share.Level, share.StartDate, share.EndDate)); // Effect: past start stays.
        Assert.Equal(c.Viewer, Assert.Single(c.Notifications.Added).UserId); // Effect.
        Assert.Equal([AuditActions.CalendarShareUpdated], c.Audit.Actions); // Effect.
        Assert.Empty(c.ShareLog.Entries); // Log: N/A.
    }

    [Fact(DisplayName = "CalendarSharingService.ChangeAsync/UTC007 stranger change is refused silently")]
    [Trait("UTC", "CalendarSharingService.ChangeAsync/UTC007")]
    [Trait("Trait", "UTC")]
    public async Task Change_UTC007()
    {
        var c = new Cal();
        var share = c.Shares.Share(c.Owner, c.Viewer, CalendarShareStatus.Effective, Today, End30);
        var stranger = c.Person("Người lạ");

        var outcome = await c.Sharing().ChangeAsync(stranger, share.Id,
            new ShareValues("Level2", null, null));

        Assert.Equal(CalendarShareFailure.NotFound, outcome.Failure); // Return + Exception.
        Assert.Null(share.PendingRequestedAt); // Effect: nothing pending.
        Assert.Empty(c.Audit.Actions); // Effect.
        Assert.Empty(c.ShareLog.Entries); // Log: N/A.
    }

    [Fact(DisplayName = "CalendarSharingService.ChangeAsync/UTC008 save failure keeps no pending change")]
    [Trait("UTC", "CalendarSharingService.ChangeAsync/UTC008")]
    [Trait("Trait", "UTC")]
    public async Task Change_UTC008()
    {
        var c = new Cal();
        var share = c.Shares.Share(c.Owner, c.Viewer, CalendarShareStatus.Effective, Today, End30);
        c.Audit.Fail = new PersistenceUnavailableException();

        var outcome = await c.Sharing().ChangeAsync(c.Viewer, share.Id,
            new ShareValues("Level2", null, null));

        Assert.Equal(CalendarShareFailure.SaveFailed, outcome.Failure); // Return + Exception: converted.
        // No pending-state assert: the hand fake stages in memory without rollback (real EF discards
        // via the UoW); Discards == 1 below is the rollback evidence.
        Assert.Equal(1, c.Uow.Discards); // Effect.
        Assert.Contains(c.ShareLog.Entries, e => e.Level == LogLevel.Error
            && e.Message.Contains("Could not save the calendar share")); // Log.
    }

    // ---- CalendarSharingService.DeclineAsync ----

    [Fact(DisplayName = "CalendarSharingService.DeclineAsync/UTC002 deactivated sender request still declines")]
    [Trait("UTC", "CalendarSharingService.DeclineAsync/UTC002")]
    [Trait("Trait", "UTC")]
    public async Task Decline_UTC002()
    {
        var c = new Cal();
        var share = c.Shares.Share(c.Owner, c.Viewer, CalendarShareStatus.Pending,
            new DateOnly(2026, 11, 20), new DateOnly(2026, 12, 20),
            CalendarShareInitiator.Viewer, CalendarShareLevel.Level2);
        c.Deactivate(c.Viewer);

        var outcome = await c.Sharing().DeclineAsync(c.Owner, share.Id);

        Assert.Equal(CalendarShareFailure.None, outcome.Failure); // Return + Exception.
        Assert.Equal(CalendarShareSuccess.Declined, outcome.Success); // Return.
        Assert.Equal((CalendarShareStatus.Declined, c.Owner, Now),
            (share.Status, share.ClosedById, share.ClosedAt)); // Effect: closed.
        Assert.Equal(c.Viewer, Assert.Single(c.Notifications.Added).UserId); // Effect.
        Assert.Equal([AuditActions.CalendarShareDeclined], c.Audit.Actions); // Effect.
        Assert.Empty(c.ShareLog.Entries); // Log: N/A.
    }

    [Fact(DisplayName = "CalendarSharingService.DeclineAsync/UTC003 pending change is declined, share kept")]
    [Trait("UTC", "CalendarSharingService.DeclineAsync/UTC003")]
    [Trait("Trait", "UTC")]
    public async Task Decline_UTC003()
    {
        var c = new Cal();
        var share = c.Shares.Share(c.Owner, c.Viewer, CalendarShareStatus.Effective, Today, End30);
        share.PendingLevel = CalendarShareLevel.Level2;
        share.PendingEndDate = new DateOnly(2026, 12, 30);
        share.PendingRequestedAt = Now;

        var outcome = await c.Sharing().DeclineAsync(c.Owner, share.Id);

        Assert.Equal(CalendarShareFailure.None, outcome.Failure); // Return + Exception.
        Assert.Equal(CalendarShareSuccess.ChangeDeclined, outcome.Success); // Return.
        Assert.Equal((CalendarShareStatus.Effective, CalendarShareLevel.Level1, End30),
            (share.Status, share.Level, share.EndDate)); // Effect: kept as before.
        Assert.Null(share.PendingRequestedAt); // Effect: pending cleared.
        Assert.Equal(c.Viewer, Assert.Single(c.Notifications.Added).UserId); // Effect.
        Assert.Equal([AuditActions.CalendarShareChangeDeclined], c.Audit.Actions); // Effect.
        Assert.Empty(c.ShareLog.Entries); // Log: N/A.
    }

    [Fact(DisplayName = "CalendarSharingService.DeclineAsync/UTC004 stale pending change still declines")]
    [Trait("UTC", "CalendarSharingService.DeclineAsync/UTC004")]
    [Trait("Trait", "UTC")]
    public async Task Decline_UTC004()
    {
        var c = new Cal();
        var share = c.Shares.Share(c.Owner, c.Viewer, CalendarShareStatus.Effective, Today, End30);
        share.PendingLevel = CalendarShareLevel.Level2;
        share.PendingEndDate = Today.AddDays(-1);
        share.PendingRequestedAt = Now;

        var outcome = await c.Sharing().DeclineAsync(c.Owner, share.Id);

        Assert.Equal(CalendarShareSuccess.ChangeDeclined, outcome.Success); // Return.
        Assert.Equal(CalendarShareFailure.None, outcome.Failure); // Exception: answered, not thrown.
        Assert.Null(share.PendingRequestedAt); // Effect: pending cleared.
        Assert.Equal(c.Viewer, Assert.Single(c.Notifications.Added).UserId); // Effect.
        Assert.Equal([AuditActions.CalendarShareChangeDeclined], c.Audit.Actions); // Effect.
        Assert.Empty(c.ShareLog.Entries); // Log: N/A.
    }

    [Fact(DisplayName = "CalendarSharingService.DeclineAsync/UTC005 nothing pending cannot be declined")]
    [Trait("UTC", "CalendarSharingService.DeclineAsync/UTC005")]
    [Trait("Trait", "UTC")]
    public async Task Decline_UTC005()
    {
        var c = new Cal();
        var share = c.Shares.Share(c.Owner, c.Viewer, CalendarShareStatus.Effective, Today, End30);

        var outcome = await c.Sharing().DeclineAsync(c.Owner, share.Id);

        Assert.Equal(CalendarShareFailure.NotAllowed, outcome.Failure); // Return + Exception.
        Assert.Equal(CalendarShareStatus.Effective, share.Status); // Effect: untouched.
        Assert.Empty(c.Notifications.Added); // Effect.
        Assert.Empty(c.Audit.Actions); // Effect.
        Assert.Empty(c.ShareLog.Entries); // Log: N/A.
    }

    [Fact(DisplayName = "CalendarSharingService.DeclineAsync/UTC006 sender cannot decline their own request")]
    [Trait("UTC", "CalendarSharingService.DeclineAsync/UTC006")]
    [Trait("Trait", "UTC")]
    public async Task Decline_UTC006()
    {
        var c = new Cal();
        var share = c.Shares.Share(c.Owner, c.Viewer, CalendarShareStatus.Pending,
            new DateOnly(2026, 11, 20), new DateOnly(2026, 12, 20),
            CalendarShareInitiator.Viewer, CalendarShareLevel.Level2);

        var outcome = await c.Sharing().DeclineAsync(c.Viewer, share.Id);

        Assert.Equal(CalendarShareFailure.NotFound, outcome.Failure); // Return + Exception.
        Assert.Equal(CalendarShareStatus.Pending, share.Status); // Effect.
        Assert.Empty(c.Audit.Actions); // Effect.
        Assert.Empty(c.ShareLog.Entries); // Log: N/A.
    }

    [Fact(DisplayName = "CalendarSharingService.DeclineAsync/UTC007 save failure keeps the share Pending")]
    [Trait("UTC", "CalendarSharingService.DeclineAsync/UTC007")]
    [Trait("Trait", "UTC")]
    public async Task Decline_UTC007()
    {
        var c = new Cal();
        var share = c.Shares.Share(c.Owner, c.Viewer, CalendarShareStatus.Pending,
            new DateOnly(2026, 11, 20), new DateOnly(2026, 12, 20),
            CalendarShareInitiator.Viewer, CalendarShareLevel.Level2);
        c.Audit.Fail = new PersistenceUnavailableException();

        var outcome = await c.Sharing().DeclineAsync(c.Owner, share.Id);

        Assert.Equal(CalendarShareFailure.SaveFailed, outcome.Failure); // Return + Exception: converted.
        // No share-state assert: the hand fake stages in memory without rollback (real EF discards
        // via the UoW); Discards == 1 below is the rollback evidence.
        Assert.Equal(1, c.Uow.Discards); // Effect.
        Assert.Contains(c.ShareLog.Entries, e => e.Level == LogLevel.Error
            && e.Message.Contains("Could not save the calendar share")); // Log.
    }

    // ---- CalendarSharingService.EndAsync ----

    [Fact(DisplayName = "CalendarSharingService.EndAsync/UTC002 viewer ends share, pending cleared with it")]
    [Trait("UTC", "CalendarSharingService.EndAsync/UTC002")]
    [Trait("Trait", "UTC")]
    public async Task End_UTC002()
    {
        var c = new Cal();
        var share = c.Shares.Share(c.Owner, c.Viewer, CalendarShareStatus.Effective, Today, End30);
        share.PendingLevel = CalendarShareLevel.Level2;
        share.PendingEndDate = new DateOnly(2026, 12, 30);
        share.PendingRequestedAt = Now;

        var outcome = await c.Sharing().EndAsync(c.Viewer, share.Id);

        Assert.Equal(CalendarShareFailure.None, outcome.Failure); // Return + Exception.
        Assert.Equal(CalendarShareSuccess.Ended, outcome.Success); // Return.
        Assert.Equal((CalendarShareStatus.Ended, c.Viewer, Now),
            (share.Status, share.ClosedById, share.ClosedAt)); // Effect: closed.
        Assert.Null(share.PendingRequestedAt); // Effect: no pending change left.
        Assert.Equal(c.Owner, Assert.Single(c.Notifications.Added).UserId); // Effect: owner told.
        Assert.Equal([AuditActions.CalendarShareEnded], c.Audit.Actions); // Effect.
        Assert.Empty(c.ShareLog.Entries); // Log: N/A.
    }

    [Fact(DisplayName = "CalendarSharingService.EndAsync/UTC003 Pending share is not ended")]
    [Trait("UTC", "CalendarSharingService.EndAsync/UTC003")]
    [Trait("Trait", "UTC")]
    public async Task End_UTC003()
    {
        var c = new Cal();
        var share = c.Shares.Share(c.Owner, c.Viewer, CalendarShareStatus.Pending,
            new DateOnly(2026, 11, 20), new DateOnly(2026, 12, 20),
            CalendarShareInitiator.Viewer, CalendarShareLevel.Level2);

        var outcome = await c.Sharing().EndAsync(c.Owner, share.Id);

        Assert.Equal(CalendarShareFailure.NotAllowed, outcome.Failure); // Return + Exception.
        Assert.Equal(CalendarShareStatus.Pending, share.Status); // Effect.
        Assert.Empty(c.Notifications.Added); // Effect.
        Assert.Empty(c.Audit.Actions); // Effect.
        Assert.Empty(c.ShareLog.Entries); // Log: N/A.
    }

    [Fact(DisplayName = "CalendarSharingService.EndAsync/UTC004 stranger end is refused silently")]
    [Trait("UTC", "CalendarSharingService.EndAsync/UTC004")]
    [Trait("Trait", "UTC")]
    public async Task End_UTC004()
    {
        var c = new Cal();
        var share = c.Shares.Share(c.Owner, c.Viewer, CalendarShareStatus.Effective, Today, End30);
        var stranger = c.Person("Người lạ");

        var outcome = await c.Sharing().EndAsync(stranger, share.Id);

        Assert.Equal(CalendarShareFailure.NotFound, outcome.Failure); // Return + Exception.
        Assert.Equal(CalendarShareStatus.Effective, share.Status); // Effect.
        Assert.Empty(c.Audit.Actions); // Effect.
        Assert.Empty(c.ShareLog.Entries); // Log: N/A.
    }

    [Fact(DisplayName = "CalendarSharingService.EndAsync/UTC005 share ending today still ends")]
    [Trait("UTC", "CalendarSharingService.EndAsync/UTC005")]
    [Trait("Trait", "UTC")]
    public async Task End_UTC005()
    {
        var c = new Cal();
        var share = c.Shares.Share(c.Owner, c.Viewer, CalendarShareStatus.Effective,
            Today.AddDays(-30), Today);

        var outcome = await c.Sharing().EndAsync(c.Owner, share.Id);

        Assert.Equal(CalendarShareFailure.None, outcome.Failure); // Return + Exception.
        Assert.Equal(CalendarShareSuccess.Ended, outcome.Success); // Return.
        Assert.Equal((CalendarShareStatus.Ended, c.Owner), (share.Status, share.ClosedById)); // Effect.
        Assert.Equal(c.Viewer, Assert.Single(c.Notifications.Added).UserId); // Effect.
        Assert.Equal([AuditActions.CalendarShareEnded], c.Audit.Actions); // Effect.
        Assert.Empty(c.ShareLog.Entries); // Log: N/A.
    }

    [Fact(DisplayName = "CalendarSharingService.EndAsync/UTC006 save failure keeps the share Effective")]
    [Trait("UTC", "CalendarSharingService.EndAsync/UTC006")]
    [Trait("Trait", "UTC")]
    public async Task End_UTC006()
    {
        var c = new Cal();
        var share = c.Shares.Share(c.Owner, c.Viewer, CalendarShareStatus.Effective, Today, End30);
        c.Audit.Fail = new PersistenceUnavailableException();

        var outcome = await c.Sharing().EndAsync(c.Owner, share.Id);

        Assert.Equal(CalendarShareFailure.SaveFailed, outcome.Failure); // Return + Exception: converted.
        // No share-state assert: the hand fake stages in memory without rollback (real EF discards
        // via the UoW); Discards == 1 below is the rollback evidence.
        Assert.Equal(1, c.Uow.Discards); // Effect.
        Assert.Contains(c.ShareLog.Entries, e => e.Level == LogLevel.Error
            && e.Message.Contains("Could not save the calendar share")); // Log.
    }

    // ---- CalendarSharingService.WithdrawAsync ----

    [Fact(DisplayName = "CalendarSharingService.WithdrawAsync/UTC002 asked side cannot withdraw")]
    [Trait("UTC", "CalendarSharingService.WithdrawAsync/UTC002")]
    [Trait("Trait", "UTC")]
    public async Task Withdraw_UTC002()
    {
        var c = new Cal();
        var share = c.Shares.Share(c.Owner, c.Viewer, CalendarShareStatus.Pending,
            new DateOnly(2026, 11, 20), new DateOnly(2026, 12, 20),
            CalendarShareInitiator.Viewer, CalendarShareLevel.Level2);

        var outcome = await c.Sharing().WithdrawAsync(c.Owner, share.Id);

        Assert.Equal(CalendarShareFailure.NotAllowed, outcome.Failure); // Return + Exception.
        Assert.Equal(CalendarShareStatus.Pending, share.Status); // Effect.
        Assert.Empty(c.Notifications.Added); // Effect.
        Assert.Empty(c.Audit.Actions); // Effect.
        Assert.Empty(c.ShareLog.Entries); // Log: N/A.
    }

    [Fact(DisplayName = "CalendarSharingService.WithdrawAsync/UTC003 stranger withdraw is refused silently")]
    [Trait("UTC", "CalendarSharingService.WithdrawAsync/UTC003")]
    [Trait("Trait", "UTC")]
    public async Task Withdraw_UTC003()
    {
        var c = new Cal();
        var share = c.Shares.Share(c.Owner, c.Viewer, CalendarShareStatus.Pending,
            new DateOnly(2026, 11, 20), new DateOnly(2026, 12, 20),
            CalendarShareInitiator.Viewer, CalendarShareLevel.Level2);
        var stranger = c.Person("Người lạ");

        var outcome = await c.Sharing().WithdrawAsync(stranger, share.Id);

        Assert.Equal(CalendarShareFailure.NotFound, outcome.Failure); // Return + Exception.
        Assert.Equal(CalendarShareStatus.Pending, share.Status); // Effect.
        Assert.Empty(c.Audit.Actions); // Effect.
        Assert.Empty(c.ShareLog.Entries); // Log: N/A.
    }

    [Fact(DisplayName = "CalendarSharingService.WithdrawAsync/UTC004 save failure keeps the share Pending")]
    [Trait("UTC", "CalendarSharingService.WithdrawAsync/UTC004")]
    [Trait("Trait", "UTC")]
    public async Task Withdraw_UTC004()
    {
        var c = new Cal();
        var share = c.Shares.Share(c.Owner, c.Viewer, CalendarShareStatus.Pending,
            new DateOnly(2026, 11, 20), new DateOnly(2026, 12, 20),
            CalendarShareInitiator.Viewer, CalendarShareLevel.Level2);
        c.Audit.Fail = new PersistenceUnavailableException();

        var outcome = await c.Sharing().WithdrawAsync(c.Viewer, share.Id);

        Assert.Equal(CalendarShareFailure.SaveFailed, outcome.Failure); // Return + Exception: converted.
        // No share-state assert: the hand fake stages in memory without rollback (real EF discards
        // via the UoW); Discards == 1 below is the rollback evidence.
        Assert.Equal(1, c.Uow.Discards); // Effect.
        Assert.Contains(c.ShareLog.Entries, e => e.Level == LogLevel.Error
            && e.Message.Contains("Could not save the calendar share")); // Log.
    }

    // ---- MeetingSlotService.CreateDraftAsync ----

    [Fact(DisplayName = "MeetingSlotService.CreateDraftAsync/UTC001 free slot becomes a draft meeting")]
    [Trait("UTC", "MeetingSlotService.CreateDraftAsync/UTC001")]
    [Trait("Trait", "UTC")]
    public async Task CreateDraft_UTC001()
    {
        var c = new Cal();
        var an = c.Person("An");
        var binh = c.Person("Bình");
        var cuong = c.Person("Cường");
        c.Shares.Share(binh, an, CalendarShareStatus.Effective, Today, End30);
        c.Shares.Share(cuong, an, CalendarShareStatus.Effective, Today, End30);
        var (slot, meetings) = c.SlotSvc();

        var outcome = await slot.CreateDraftAsync(an, Draft([binh, cuong], At(24, 9, 0), At(24, 10, 0), 30, []));

        Assert.Equal(MeetingSlotFailure.None, outcome.Failure); // Return + Exception: answered, not thrown.
        Assert.Equal(MeetingOutcome.Success, outcome.Meeting!.Outcome); // Return: draft saved.
        Assert.Equal(MeetingStatus.Draft, outcome.Meeting.Meeting!.Status); // Return: a Draft.
        Assert.Equal(binh, outcome.Meeting.Meeting.Chair!.UserId); // Return: first invitee chairs.
        Assert.Contains(an, outcome.Meeting.Meeting.Secretaries!.Select(s => s.UserId)); // Return: finder is Secretary.
        var saved = Assert.Single(meetings.Added); // Effect: one meeting kept.
        Assert.Equal([MeetingRole.Chair, MeetingRole.Member, MeetingRole.Secretary],
            saved.Participants.Select(p => p.Role).ToArray()); // Effect: roster Binh Chair, Cuong Member, An Secretary.
        var history = Assert.Single(saved.StatusHistory); // Effect: creation history.
        Assert.Equal((MeetingHistoryAction.Create, MeetingStatus.Draft), (history.Action, history.ToStatus));
        Assert.Single(c.Busy.Ranges); // Effect: the slot rechecked against the shared calendars.
        Assert.Empty(c.SlotLog.Entries); // Log: N/A.
        Assert.Empty(c.MeetingLog.Entries); // Log: N/A.
    }

    [Fact(DisplayName = "MeetingSlotService.CreateDraftAsync/UTC003 duplicate invitee names the field")]
    [Trait("UTC", "MeetingSlotService.CreateDraftAsync/UTC003")]
    [Trait("Trait", "UTC")]
    public async Task CreateDraft_UTC003()
    {
        var c = new Cal();
        var an = c.Person("An");
        var binh = c.Person("Bình");
        var (slot, _) = c.SlotSvc();

        var outcome = await slot.CreateDraftAsync(an, Draft([binh, binh], At(24, 9, 0), At(24, 10, 0), 30, []));

        Assert.Equal(MeetingSlotFailure.InvalidInvitees, outcome.Failure); // Return + Exception.
        Assert.Equal("Chọn từ 1 đến 10 người, không trùng và không chọn chính mình.",
            Assert.Single(outcome.Errors!).Value); // Return: errors.userIds.
        Assert.Null(outcome.Meeting); // Return: no meeting.
        Assert.Empty(c.Busy.Ranges); // Effect: never rechecked.
        Assert.Empty(c.SlotLog.Entries); // Log: N/A.
    }

    [Fact(DisplayName = "MeetingSlotService.CreateDraftAsync/UTC005 newly busy invitee takes the slot")]
    [Trait("UTC", "MeetingSlotService.CreateDraftAsync/UTC005")]
    [Trait("Trait", "UTC")]
    public async Task CreateDraft_UTC005()
    {
        var c = new Cal();
        var an = c.Person("An");
        var binh = c.Person("Bình");
        var cuong = c.Person("Cường");
        c.Shares.Share(binh, an, CalendarShareStatus.Effective, Today, End30);
        c.Shares.Share(cuong, an, CalendarShareStatus.Effective, Today, End30);
        c.BusyRow(cuong, At(24, 9, 0), At(24, 10, 0));
        var (slot, meetings) = c.SlotSvc();

        var outcome = await slot.CreateDraftAsync(an, Draft([binh, cuong], At(24, 9, 0), At(24, 10, 0), 30, []));

        Assert.Equal(MeetingSlotFailure.SlotTaken, outcome.Failure); // Return + Exception.
        Assert.Null(outcome.Meeting); // Return: no meeting created.
        Assert.Empty(meetings.Added); // Effect.
        Assert.Empty(c.SlotLog.Entries); // Log: N/A.
        Assert.Empty(c.MeetingLog.Entries); // Log: N/A.
    }

    [Fact(DisplayName = "MeetingSlotService.CreateDraftAsync/UTC006 meeting save failure answers save failed")]
    [Trait("UTC", "MeetingSlotService.CreateDraftAsync/UTC006")]
    [Trait("Trait", "UTC")]
    public async Task CreateDraft_UTC006()
    {
        var c = new Cal();
        var an = c.Person("An");
        var binh = c.Person("Bình");
        var cuong = c.Person("Cường");
        c.Shares.Share(binh, an, CalendarShareStatus.Effective, Today, End30);
        c.Shares.Share(cuong, an, CalendarShareStatus.Effective, Today, End30);
        var (slot, _) = c.SlotSvc();
        c.Uow.FailNextSave = new PersistenceUnavailableException();

        var outcome = await slot.CreateDraftAsync(an, Draft([binh, cuong], At(24, 9, 0), At(24, 10, 0), 30, []));

        Assert.Equal(MeetingSlotFailure.None, outcome.Failure); // Return: the slot itself was fine.
        Assert.Equal(MeetingOutcome.SaveFailed, outcome.Meeting!.Outcome); // Return: meeting not kept.
        Assert.Contains(c.MeetingLog.Entries, e => e.Level == LogLevel.Error
            && e.Message.Contains("Could not create a meeting")); // Log.
        // Exception: N/A — the store error is converted into SaveFailed, not thrown.
    }

    [Fact(DisplayName = "MeetingSlotService.CreateDraftAsync/UTC007 recheck failure answers read failed")]
    [Trait("UTC", "MeetingSlotService.CreateDraftAsync/UTC007")]
    [Trait("Trait", "UTC")]
    public async Task CreateDraft_UTC007()
    {
        var c = new Cal();
        var an = c.Person("An");
        var binh = c.Person("Bình");
        var cuong = c.Person("Cường");
        var (slot, _) = c.SlotSvc();
        c.Shares.Fail = new PersistenceUnavailableException();

        var outcome = await slot.CreateDraftAsync(an, Draft([binh, cuong], At(24, 9, 0), At(24, 10, 0), 30, []));

        Assert.Equal(MeetingSlotFailure.ReadFailed, outcome.Failure); // Return + Exception: converted.
        Assert.Null(outcome.Meeting); // Return: no meeting created.
        Assert.Contains(c.SlotLog.Entries, e => e.Level == LogLevel.Error
            && e.Message.Contains("Could not recheck a meeting time slot")); // Log.
    }

    [Fact(DisplayName = "MeetingSlotService.CreateDraftAsync/UTC008 longest meeting with largest buffer passes")]
    [Trait("UTC", "MeetingSlotService.CreateDraftAsync/UTC008")]
    [Trait("Trait", "UTC")]
    public async Task CreateDraft_UTC008()
    {
        var c = new Cal();
        var an = c.Person("An");
        var users = Enumerable.Range(0, 10).Select(i => c.Person($"Mời {i}")).ToArray();
        foreach (var u in users) c.Shares.Share(u, an, CalendarShareStatus.Effective, Today, End30);
        var (slot, meetings) = c.SlotSvc();

        var outcome = await slot.CreateDraftAsync(an, Draft(users, At(24, 8, 0), At(24, 16, 0), 240, []));

        Assert.Equal(MeetingSlotFailure.None, outcome.Failure); // Return + Exception.
        Assert.Equal(MeetingOutcome.Success, outcome.Meeting!.Outcome); // Return: 8-hour draft saved.
        Assert.Single(meetings.Added); // Effect.
        Assert.Single(c.Busy.Ranges); // Effect: rechecked against the shared calendars.
        Assert.Empty(c.SlotLog.Entries); // Log: N/A.
        Assert.Empty(c.MeetingLog.Entries); // Log: N/A.
    }

    // ---- MeetingSlotService.FindAsync ----

    [Fact(DisplayName = "MeetingSlotService.FindAsync/UTC001 free Monday lists 14 slots in order")]
    [Trait("UTC", "MeetingSlotService.FindAsync/UTC001")]
    [Trait("Trait", "UTC")]
    public async Task Find_UTC001()
    {
        var c = new Cal();
        var an = c.Person("An");
        var binh = c.Person("Bình");
        c.Shares.Share(binh, an, CalendarShareStatus.Effective, Today, End30);
        var (slot, _) = c.SlotSvc();
        var monday = new DateOnly(2026, 11, 23);

        var outcome = await slot.FindAsync(an, Search([binh], monday));

        Assert.Equal(MeetingSlotFailure.None, outcome.Failure); // Return + Exception: answered, not thrown.
        Assert.Equal(SlotResultKind.AllFree, outcome.Result!.Kind); // Return.
        var slots = Assert.Single(outcome.Result.Days).Slots; // Return: one day.
        Assert.Equal(14, slots.Count); // Return: 08:00-11:00 and 13:00-16:00.
        Assert.Equal(At(23, 8, 0), slots.First().Start); // Return: time order.
        Assert.Equal(At(23, 16, 0), slots.Last().Start); // Return.
        var person = Assert.Single(outcome.Result.People); // Return: people.
        Assert.Equal((binh, SlotPersonStatus.Shared, "Level1"), (person.Id, person.Status, person.Level));
        Assert.Equal((Today, End30), (person.StartDate, person.EndDate));
        Assert.False(outcome.Result.Truncated); // Return.
        Assert.Equal([TransactionMode.None], c.Uow.Modes); // Effect: read-only.
        Assert.Empty(c.SlotLog.Entries); // Log: N/A.
    }

    [Fact(DisplayName = "MeetingSlotService.FindAsync/UTC002 no common slot shows the most-free ones")]
    [Trait("UTC", "MeetingSlotService.FindAsync/UTC002")]
    [Trait("Trait", "UTC")]
    public async Task Find_UTC002()
    {
        var c = new Cal();
        var an = c.Person("An");
        var binh = c.Person("Bình");
        var cuong = c.Person("Cường");
        c.Shares.Share(binh, an, CalendarShareStatus.Effective, Today, End30);
        c.Shares.Share(cuong, an, CalendarShareStatus.Effective, Today, End30);
        c.BusyRow(binh, At(23, 8, 0), At(23, 12, 0));
        c.BusyRow(cuong, At(23, 13, 0), At(23, 17, 0));
        var (slot, _) = c.SlotSvc();
        var monday = new DateOnly(2026, 11, 23);

        var outcome = await slot.FindAsync(an, Search([binh, cuong], monday));

        Assert.Equal(MeetingSlotFailure.None, outcome.Failure); // Return + Exception.
        Assert.Equal(SlotResultKind.MostFree, outcome.Result!.Kind); // Return.
        var slots = outcome.Result.Days.SelectMany(d => d.Slots).ToList();
        Assert.NotEmpty(slots); // Return.
        Assert.All(slots, s => Assert.Equal(1, s.FreeCount)); // Return: one of two free.
        Assert.All(slots, s => Assert.Equal(2, s.CountedCount)); // Return.
        Assert.Equal(2, outcome.Result.People.Count); // Return.
        Assert.Empty(c.SlotLog.Entries); // Log: N/A.
    }

    [Fact(DisplayName = "MeetingSlotService.FindAsync/UTC003 invitee without a share is NotShared")]
    [Trait("UTC", "MeetingSlotService.FindAsync/UTC003")]
    [Trait("Trait", "UTC")]
    public async Task Find_UTC003()
    {
        var c = new Cal();
        var an = c.Person("An");
        var binh = c.Person("Bình");
        var (slot, _) = c.SlotSvc();
        var monday = new DateOnly(2026, 11, 23);

        var outcome = await slot.FindAsync(an, Search([binh], monday));

        Assert.Equal(MeetingSlotFailure.None, outcome.Failure); // Return + Exception.
        Assert.Equal(SlotResultKind.None, outcome.Result!.Kind); // Return.
        Assert.Empty(outcome.Result.Days); // Return: days empty.
        var person = Assert.Single(outcome.Result.People); // Return.
        Assert.Equal((binh, SlotPersonStatus.NotShared), (person.Id, person.Status)); // Return.
        Assert.Null(person.Level); // Return: no level or dates.
        Assert.Null(person.StartDate);
        Assert.Empty(c.Busy.Ranges); // Effect: nothing read.
        Assert.Empty(c.SlotLog.Entries); // Log: N/A.
    }

    [Fact(DisplayName = "MeetingSlotService.FindAsync/UTC006 ten invitees are accepted with their slots")]
    [Trait("UTC", "MeetingSlotService.FindAsync/UTC006")]
    [Trait("Trait", "UTC")]
    public async Task Find_UTC006()
    {
        var c = new Cal();
        var an = c.Person("An");
        var users = Enumerable.Range(0, 10).Select(i => c.Person($"Mời {i}")).ToArray();
        foreach (var u in users) c.Shares.Share(u, an, CalendarShareStatus.Effective, Today, End30);
        var (slot, _) = c.SlotSvc();
        var monday = new DateOnly(2026, 11, 23);

        var outcome = await slot.FindAsync(an, new SlotSearchInput(users, false, 15, monday, monday,
            null, "08:00", "17:00", null, null, null, 0, 5));

        Assert.Equal(MeetingSlotFailure.None, outcome.Failure); // Return + Exception.
        Assert.Equal(10, outcome.Result!.People.Count); // Return: 10 invitees accepted.
        var slots = outcome.Result.Days.SelectMany(d => d.Slots).ToList();
        Assert.Equal(92, slots.Count); // Return: the found slots.
        Assert.False(outcome.Result.Truncated); // Return.
        Assert.Empty(c.SlotLog.Entries); // Log: N/A.
    }

    [Fact(DisplayName = "MeetingSlotService.FindAsync/UTC007 long range truncates at 200 slots")]
    [Trait("UTC", "MeetingSlotService.FindAsync/UTC007")]
    [Trait("Trait", "UTC")]
    public async Task Find_UTC007()
    {
        var c = new Cal();
        var an = c.Person("An");
        var binh = c.Person("Bình");
        c.Shares.Share(binh, an, CalendarShareStatus.Effective, Today, End30);
        var (slot, _) = c.SlotSvc();

        var outcome = await slot.FindAsync(an, new SlotSearchInput([binh], false, 15,
            new DateOnly(2026, 11, 23), new DateOnly(2026, 12, 23), null,
            "08:00", "17:00", false, null, null, null, 5));

        Assert.Equal(MeetingSlotFailure.None, outcome.Failure); // Return + Exception.
        Assert.Equal(SlotResultKind.AllFree, outcome.Result!.Kind); // Return.
        var slots = outcome.Result.Days.SelectMany(d => d.Slots).ToList();
        Assert.Equal(200, slots.Count); // Return: truncated.
        Assert.True(outcome.Result.Truncated); // Return.
        Assert.True(slots.First().Start < slots.Last().Start); // Return: time order.
        Assert.Empty(c.SlotLog.Entries); // Log: N/A.
    }

    [Fact(DisplayName = "MeetingSlotService.FindAsync/UTC008 read failure answers read failed")]
    [Trait("UTC", "MeetingSlotService.FindAsync/UTC008")]
    [Trait("Trait", "UTC")]
    public async Task Find_UTC008()
    {
        var c = new Cal();
        var an = c.Person("An");
        var binh = c.Person("Bình");
        var (slot, _) = c.SlotSvc();
        c.Shares.Fail = new PersistenceUnavailableException();
        var monday = new DateOnly(2026, 11, 23);

        var outcome = await slot.FindAsync(an, Search([binh], monday));

        Assert.Equal(MeetingSlotFailure.ReadFailed, outcome.Failure); // Return + Exception: converted.
        Assert.Null(outcome.Result); // Return: nothing returned.
        Assert.Contains(c.SlotLog.Entries, e => e.Level == LogLevel.Error
            && e.Message.Contains("Could not search meeting time slots")); // Log.
    }

    // ---- SharedCalendarService.ReadWeekAsync ----

    [Fact(DisplayName = "SharedCalendarService.ReadWeekAsync/UTC002 stopped share shows status without hours")]
    [Trait("UTC", "SharedCalendarService.ReadWeekAsync/UTC002")]
    [Trait("Trait", "UTC")]
    public async Task ReadWeek_UTC002()
    {
        var c = new Cal();
        c.Shares.Share(c.Owner, c.Viewer, CalendarShareStatus.Stopped,
            new DateOnly(2026, 11, 9), new DateOnly(2026, 12, 9));
        c.BusyRow(c.Owner, At(16, 9, 0), At(16, 11, 0));
        var monday = new DateOnly(2026, 11, 16);

        var outcome = await c.Week().ReadWeekAsync(c.Viewer, null);

        Assert.Equal(CalendarShareFailure.None, outcome.Failure); // Return + Exception.
        Assert.Equal((monday, monday.AddDays(6)), (outcome.Value!.WeekStart, outcome.Value.WeekEnd)); // Return.
        var person = Assert.Single(outcome.Value.People); // Return.
        Assert.Equal(nameof(CalendarShareStatus.Stopped), person.Status); // Return: stopped.
        Assert.Equal("Level1", person.Level); // Return: level and dates kept.
        Assert.Equal((new DateOnly(2026, 11, 9), new DateOnly(2026, 12, 9)),
            (person.StartDate, person.EndDate));
        Assert.Empty(person.Days); // Return: no busy shown.
        Assert.Empty(c.WeekLog.Entries); // Log: N/A.
    }

    [Fact(DisplayName = "SharedCalendarService.ReadWeekAsync/UTC004 read failure answers read failed")]
    [Trait("UTC", "SharedCalendarService.ReadWeekAsync/UTC004")]
    [Trait("Trait", "UTC")]
    public async Task ReadWeek_UTC004()
    {
        var c = new Cal();
        c.Shares.Fail = new PersistenceUnavailableException();

        var outcome = await c.Week().ReadWeekAsync(c.Viewer, null);

        Assert.Equal(CalendarShareFailure.ReadFailed, outcome.Failure); // Return + Exception: converted.
        Assert.Null(outcome.Value); // Return: nothing returned.
        Assert.Contains(c.WeekLog.Entries, e => e.Level == LogLevel.Error
            && e.Message.Contains("Could not read the shared week")); // Log.
    }

    [Fact(DisplayName = "SharedCalendarService.ReadWeekAsync/UTC005 Sunday belongs to the starting Monday week")]
    [Trait("UTC", "SharedCalendarService.ReadWeekAsync/UTC005")]
    [Trait("Trait", "UTC")]
    public async Task ReadWeek_UTC005()
    {
        var c = new Cal();
        c.Shares.Share(c.Owner, c.Viewer, CalendarShareStatus.Effective, Today, End30);
        var monday = new DateOnly(2026, 11, 16);

        var outcome = await c.Week().ReadWeekAsync(c.Viewer, new DateOnly(2026, 11, 22));

        Assert.Equal(CalendarShareFailure.None, outcome.Failure); // Return + Exception.
        Assert.Equal((monday, new DateOnly(2026, 11, 22)),
            (outcome.Value!.WeekStart, outcome.Value.WeekEnd)); // Return: Monday to Sunday.
        Assert.Equal(("00:00", "24:00"), (outcome.Value.DayStart, outcome.Value.DayEnd)); // Return.
        Assert.Empty(c.WeekLog.Entries); // Log: N/A.
    }

    // ---- helpers (private to this wave-2 file) ----

    private static SlotSearchInput Search(IReadOnlyList<Guid> userIds, DateOnly day)
        => new(userIds, false, 60, day, day, null, "08:00", "17:00", null, "12:00", "13:00", 0, 30);

    private static SlotDraftInput Draft(IReadOnlyList<Guid> userIds, DateTimeOffset start,
        DateTimeOffset end, int buffer, IReadOnlyList<Guid> accepted)
        => new("Họp triển khai quý IV", Iso(start), Iso(end), "InPerson", "Phòng 301", null,
            "Normal", "Triển khai kế hoạch quý IV", userIds, false, buffer, accepted);

    /// <summary>Fresh fakes plus the two people of the JSON cases (owner An, viewer Binh):
    /// the viewer holds the Meeting Organizer right. Private to this wave-2 file.</summary>
    private sealed class Cal
    {
        public readonly FakeCalendarShareRepository Shares = new();
        public readonly FakeNotificationRepository Notifications = new();
        public readonly FakeUnitOfWork Uow = new();
        public readonly FakeAuditService Audit = new();
        public readonly FakeCalendarBusyQuery Busy = new();
        public readonly Wave2Logger<CalendarSharingService> ShareLog = new();
        public readonly Wave2Logger<SharedCalendarService> WeekLog = new();
        public readonly Wave2Logger<MeetingSlotService> SlotLog = new();
        public readonly Wave2Logger<MeetingService> MeetingLog = new();
        public readonly Guid Owner;
        public readonly Guid Viewer;

        public Cal()
        {
            Owner = Shares.Person("An", organizer: false);
            Viewer = Shares.Person("Bình", organizer: true);
        }

        public CalendarSharingService Sharing() => new(Shares, Notifications, Uow, Audit,
            new CalendarShareCloser(Notifications, Audit), new FixedClock(Now), ShareLog);

        public SharedCalendarService Week() => new(Shares, Uow,
            new CalendarBusyReader(Busy, new FixedClock(Now)), new FixedClock(Now), WeekLog);

        public (MeetingSlotService Slot, FakeMeetingRepository Meetings) SlotSvc()
        {
            var meetings = new FakeMeetingRepository();
            var inner = new MeetingService(meetings, new FakeMeetingInvitationStager(), Uow,
                MeetingLog, new FixedClock(Now));
            return (new(Shares, Uow, new CalendarBusyReader(Busy, new FixedClock(Now)), inner,
                new FixedClock(Now), SlotLog), meetings);
        }

        public Guid Person(string name, bool organizer = true) => Shares.Person(name, organizer);

        public void Deactivate(Guid id) => Shares.People[id] = Shares.People[id] with { IsActive = false };

        public void BusyRow(Guid user, DateTimeOffset start, DateTimeOffset end)
            => Busy.Rows.Add(new BusyAssignment(user, AssignmentStatus.Current, MeetingRole.Member,
                InvitationResponse.Accepted, null, MeetingStatus.Published, Sensitivity.Normal,
                start, end, null, null));
    }

    /// <summary>Captures log records so a test can assert the Log Message line of a UTC.
    /// Private to this wave-2 file.</summary>
    private sealed class Wave2Logger<T> : ILogger<T>
    {
        public List<(LogLevel Level, string Message, Exception? Error)> Entries { get; } = [];
        public IDisposable? BeginScope<TState>(TState state) where TState : notnull => null;
        public bool IsEnabled(LogLevel level) => true;
        public void Log<TState>(LogLevel level, EventId id, TState state, Exception? error,
            Func<TState, Exception?, string> format)
            => Entries.Add((level, format(state, error), error));
    }
}
