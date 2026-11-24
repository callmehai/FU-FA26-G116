using TrustCabinet.Domain.Entities;
using TrustCabinet.Domain.Enums;

namespace TrustCabinet.Domain.CalendarSharing;

/// <summary>
/// The rules of SCRUM-142 that need no database: levels (BR-140, BR-141, BR-144, BR-145), valid date ranges (UC-80a), the end date (BR-142, BR-143)
/// and who may do what to a share (UC-80c → UC-80e). Every date is Vietnam's calendar (<see cref="VietnamTime"/>).
/// </summary>
public static class CalendarShareRules
{
    /// <summary>UC-80a (PO 08/11/2026, Q10): a share covers at most 365 days counting both ends.</summary>
    public const int MaxDays = 365;

    public const string Accept = "accept";
    public const string Decline = "decline";
    public const string Withdraw = "withdraw";
    /// <summary>The viewer asks for a higher level or a later end date (UC-80d Normal Flow).</summary>
    public const string RequestChange = "requestChange";
    /// <summary>The owner lowers the level or changes the dates at once (UC-80d AF-1).</summary>
    public const string Update = "update";
    public const string End = "end";

    /// <summary>Pending and Effective block a second share of the same pair (BR-142, BR-143); nothing else does.</summary>
    public static bool IsOpen(CalendarShareStatus status) => status is CalendarShareStatus.Pending or CalendarShareStatus.Effective;

    /// <summary>Days covered, counting both ends.</summary>
    public static int Length(DateOnly start, DateOnly end) => end.DayNumber - start.DayNumber + 1;

    /// <summary>
    /// BR-142, BR-143 (PO 09/11/2026, Q-B): a share — and any invitation or change still waiting on it — ends by itself once its
    /// end date has passed. Nothing runs at midnight and no state records it: such a row is left out of every read and
    /// refuses every action (MSG450).
    /// </summary>
    public static bool IsLapsed(CalendarShare share, DateOnly today) => share.EndDate < today;

    /// <summary>
    /// BR-140, BR-141, BR-144, BR-145: whether a meeting of this sensitivity shows as busy at this level. A null level is the finder's own
    /// calendar, which counts at every level (BR-144, BR-145). A Secret meeting is never shared.
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

    /// <summary>UC-80a for a new share (UC-80a, UC-80b): it starts today or later and covers at most 365 days.</summary>
    public static bool ValidNewRange(DateOnly start, DateOnly end, DateOnly today)
        => today <= start && start <= end && Length(start, end) <= MaxDays;

    /// <summary>
    /// UC-80c, accepting a UC-80a request: the owner may lower the level and shorten the range, within what was asked
    /// (BR-140, BR-141, UC-80a). The end must not have passed.
    /// </summary>
    public static bool ValidNarrowing(CalendarShare share, CalendarShareLevel level, DateOnly start, DateOnly end, DateOnly today)
        => level <= share.Level && share.StartDate <= start && start <= end && end <= share.EndDate && end >= today;

    /// <summary>
    /// UC-80d Normal Flow: the viewer raises the level and/or moves the end date later; the start date stays (UC-80a).
    /// At least one of the two must grow.
    /// </summary>
    public static bool ValidViewerChange(CalendarShare share, CalendarShareLevel level, DateOnly end, DateOnly today)
        => level >= share.Level && end >= share.EndDate && (level > share.Level || end > share.EndDate)
            && end >= today && Length(share.StartDate, end) <= MaxDays;

    /// <summary>
    /// UC-80d AF-1: the owner lowers the level and/or sets new dates. The start may stay where it was (even in the past)
    /// or move to today or later (UC-80a). Something must change.
    /// </summary>
    public static bool ValidOwnerChange(CalendarShare share, CalendarShareLevel level, DateOnly start, DateOnly end, DateOnly today)
        => level <= share.Level && (start == share.StartDate || start >= today) && start <= end && end >= today
            && Length(start, end) <= MaxDays
            && (level != share.Level || start != share.StartDate || end != share.EndDate);

    /// <summary>
    /// UC-80c AF-1: the pending change is checked again when the owner approves it. Time has passed since, and the owner
    /// may have moved the dates meanwhile (UC-80d AF-1), so the asked end date must still be today or later, on or after
    /// the current start, and within 365 days of it (UC-80a).
    /// </summary>
    public static bool ValidPendingApproval(CalendarShare share, DateOnly today)
        => share.PendingEndDate is { } end && end >= today && end >= share.StartDate
            && Length(share.StartDate, end) <= MaxDays;

    /// <summary>The side that sent the share: the viewer for UC-80a, the owner for UC-80b.</summary>
    public static Guid Sender(CalendarShare share)
        => share.InitiatedBy == CalendarShareInitiator.Viewer ? share.ViewerId : share.OwnerId;

    /// <summary>The side that answers a Pending share (UC-80c).</summary>
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
    /// one of the two people, and for a share that is final or past its end date. A pending change that no longer fits the
    /// share (<see cref="ValidPendingApproval"/>) can only be declined. <paramref name="acceptBlocked"/>: a
    /// Pending share that can no longer become Effective — its sender was deactivated (UC-80c EX-5, PO 09/11/2026 Q-C) or its
    /// viewer lost the Meeting Organizer right (BR-140, BR-141) — so the asked side may only decline it.
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
            CalendarShareStatus.Effective when isOwner && share.HasPendingChange && !ValidPendingApproval(share, today)
                => [Decline, Update, End],
            CalendarShareStatus.Effective when isOwner && share.HasPendingChange => [Accept, Decline, Update, End],
            CalendarShareStatus.Effective when isOwner => [Update, End],
            CalendarShareStatus.Effective when share.HasPendingChange => [Withdraw, End],
            CalendarShareStatus.Effective => [RequestChange, End],
            _ => [],
        };
    }
}
