using TrustCabinet.Domain.Entities;
using TrustCabinet.Domain.Enums;

namespace TrustCabinet.Domain.CalendarSharing;

/// <summary>
/// The rules of SCRUM-142 that need no database: levels (BR-414, BR-417), valid date ranges (BR-431), the end date (BR-425)
/// and who may do what to a share (UC-84c → UC-84e). Every date is Vietnam's calendar (<see cref="VietnamTime"/>).
/// </summary>
public static class CalendarShareRules
{
    /// <summary>BR-431 (PO 08/11/2026, Q10): a share covers at most 365 days counting both ends.</summary>
    public const int MaxDays = 365;

    public const string Accept = "accept";
    public const string Decline = "decline";
    public const string Withdraw = "withdraw";
    /// <summary>The viewer asks for a higher level or a later end date (UC-84d Normal Flow).</summary>
    public const string RequestChange = "requestChange";
    /// <summary>The owner lowers the level or changes the dates at once (UC-84d AF-1).</summary>
    public const string Update = "update";
    public const string End = "end";

    /// <summary>Pending and Effective block a second share of the same pair (BR-423); nothing else does.</summary>
    public static bool IsOpen(CalendarShareStatus status) => status is CalendarShareStatus.Pending or CalendarShareStatus.Effective;

    /// <summary>Days covered, counting both ends.</summary>
    public static int Length(DateOnly start, DateOnly end) => end.DayNumber - start.DayNumber + 1;

    /// <summary>
    /// BR-425 (PO 09/11/2026, Q-B): a share — and any invitation or change still waiting on it — ends by itself once its
    /// end date has passed. Nothing runs at midnight and no state records it: such a row is left out of every read and
    /// refuses every action (MSG450).
    /// </summary>
    public static bool IsLapsed(CalendarShare share, DateOnly today) => share.EndDate < today;

    /// <summary>
    /// BR-414/BR-417: whether a meeting of this sensitivity shows as busy at this level. A null level is the finder's own
    /// calendar, which counts at every level (BR-418). A Secret meeting is never shared.
    /// </summary>
    public static bool Covers(CalendarShareLevel? level, Sensitivity sensitivity) => level switch
    {
        null => true,
        CalendarShareLevel.Level1 => sensitivity == Sensitivity.Normal,
        CalendarShareLevel.Level2 => sensitivity is Sensitivity.Normal or Sensitivity.Internal,
        _ => false,
    };

    /// <summary>Exactly "Level1" or "Level2"; anything else (another case, a number) is refused.</summary>
    public static bool TryParseLevel(string? value, out CalendarShareLevel level)
    {
        level = default;
        return value is not null && Enum.TryParse(value, ignoreCase: false, out level)
            && Enum.IsDefined(level) && value == level.ToString();
    }

    /// <summary>BR-431 for a new share (UC-84a, UC-84b): it starts today or later and covers at most 365 days.</summary>
    public static bool ValidNewRange(DateOnly start, DateOnly end, DateOnly today)
        => today <= start && start <= end && Length(start, end) <= MaxDays;

    /// <summary>
    /// UC-84c, accepting a UC-84a request: the owner may lower the level and shorten the range, within what was asked
    /// (BR-414, BR-431). The end must not have passed.
    /// </summary>
    public static bool ValidNarrowing(CalendarShare share, CalendarShareLevel level, DateOnly start, DateOnly end, DateOnly today)
        => level <= share.Level && share.StartDate <= start && start <= end && end <= share.EndDate && end >= today;

    /// <summary>
    /// UC-84d Normal Flow: the viewer raises the level and/or moves the end date later; the start date stays (BR-431).
    /// At least one of the two must grow.
    /// </summary>
    public static bool ValidViewerChange(CalendarShare share, CalendarShareLevel level, DateOnly end, DateOnly today)
        => level >= share.Level && end >= share.EndDate && (level > share.Level || end > share.EndDate)
            && end >= today && Length(share.StartDate, end) <= MaxDays;

    /// <summary>
    /// UC-84d AF-1: the owner lowers the level and/or sets new dates. The start may stay where it was (even in the past)
    /// or move to today or later (BR-431). Something must change.
    /// </summary>
    public static bool ValidOwnerChange(CalendarShare share, CalendarShareLevel level, DateOnly start, DateOnly end, DateOnly today)
        => level <= share.Level && (start == share.StartDate || start >= today) && start <= end && end >= today
            && Length(start, end) <= MaxDays
            && (level != share.Level || start != share.StartDate || end != share.EndDate);

    /// <summary>UC-84c AF-1: the pending change is checked again when the owner approves it — time has passed since.</summary>
    public static bool ValidPendingApproval(CalendarShare share, DateOnly today)
        => share.PendingEndDate is { } end && end >= today && Length(share.StartDate, end) <= MaxDays;

    /// <summary>The side that sent the share: the viewer for UC-84a, the owner for UC-84b.</summary>
    public static Guid Sender(CalendarShare share)
        => share.InitiatedBy == CalendarShareInitiator.Viewer ? share.ViewerId : share.OwnerId;

    /// <summary>The side that answers a Pending share (UC-84c).</summary>
    public static Guid AskedSide(CalendarShare share)
        => share.InitiatedBy == CalendarShareInitiator.Viewer ? share.OwnerId : share.ViewerId;

    public static bool IsParty(CalendarShare share, Guid userId) => share.OwnerId == userId || share.ViewerId == userId;

    /// <summary>
    /// The person who answers what is pending: the asked side of a Pending share, the owner for a pending change, nobody
    /// when nothing is pending.
    /// </summary>
    public static Guid? Answerer(CalendarShare share) => share.Status switch
    {
        CalendarShareStatus.Pending => AskedSide(share),
        CalendarShareStatus.Effective when share.HasPendingChange => share.OwnerId,
        _ => null,
    };

    /// <summary>
    /// What <paramref name="actorId"/> may do to the share now (the table of the plan, §2.5). Empty for anyone who is not
    /// one of the two people, and for a share that is final or past its end date. <paramref name="acceptBlocked"/>: a
    /// Pending share that can no longer become Effective — its sender was deactivated (UC-84c EX-5, PO 09/11/2026 Q-C) or its
    /// viewer lost the Meeting Organizer right (BR-430) — so the asked side may only decline it.
    /// </summary>
    public static IReadOnlyList<string> Actions(CalendarShare share, Guid actorId, DateOnly today, bool acceptBlocked)
    {
        if (!IsParty(share, actorId) || IsLapsed(share, today)) return [];
        var isOwner = share.OwnerId == actorId;
        return share.Status switch
        {
            CalendarShareStatus.Pending when AskedSide(share) == actorId && acceptBlocked => [Decline],
            CalendarShareStatus.Pending when AskedSide(share) == actorId => [Accept, Decline],
            CalendarShareStatus.Pending => [Withdraw],
            CalendarShareStatus.Effective when isOwner && share.HasPendingChange => [Accept, Decline, Update, End],
            CalendarShareStatus.Effective when isOwner => [Update, End],
            CalendarShareStatus.Effective when share.HasPendingChange => [Withdraw, End],
            CalendarShareStatus.Effective => [RequestChange, End],
            _ => [],
        };
    }
}
