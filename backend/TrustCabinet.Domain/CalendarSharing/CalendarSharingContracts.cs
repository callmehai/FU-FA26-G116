using TrustCabinet.Domain.Enums;

namespace TrustCabinet.Domain.CalendarSharing;

/// <summary>One side of a share, or a person to choose: never more than a name and an email (BR-427).</summary>
public sealed record SharePerson(Guid Id, string FullName, string Email);

/// <summary>A change the viewer asked for (UC-84d), waiting for the owner (UC-84c AF-1).</summary>
public sealed record PendingChangeView(string Level, DateOnly EndDate, DateTimeOffset RequestedAt);

/// <summary>
/// A share as one of its two people sees it. A share past its end date is never listed (BR-425);
/// <see cref="Actions"/> is what the caller may do now. <see cref="SenderDeactivated"/> is true for an invitation whose
/// sender's account was deactivated: the asked side may only decline it (UC-84c EX-5).
/// </summary>
public sealed record ShareView(
    Guid Id,
    string InitiatedBy,
    string Level,
    SharePerson Owner,
    SharePerson Viewer,
    DateOnly StartDate,
    DateOnly EndDate,
    string Status,
    PendingChangeView? PendingChange,
    DateTimeOffset CreatedAt,
    DateTimeOffset? DecidedAt,
    DateTimeOffset? ClosedAt,
    bool SenderDeactivated,
    IReadOnlyList<string> Actions);

/// <summary>UC-84f: the caller's own calendar shared with others, and other calendars shared with the caller.</summary>
public sealed record ShareLists(IReadOnlyList<ShareView> AsOwner, IReadOnlyList<ShareView> AsViewer);

/// <summary>What a request body may carry; every field is optional and checked by the service (MSG443).</summary>
public sealed record ShareValues(string? Level, DateOnly? StartDate, DateOnly? EndDate);

public enum CalendarShareFailure
{
    None,
    /// <summary>Not one of the two people, an unknown share, or an owner that cannot be reached — nothing disclosed.</summary>
    NotFound,
    /// <summary>MSG448: the caller is not a Meeting Organizer (BR-430).</summary>
    NotOrganizer,
    /// <summary>MSG443: a level or date range that breaks BR-414/BR-431.</summary>
    InvalidValues,
    /// <summary>MSG449: a Pending or Effective share of this pair already exists (BR-423).</summary>
    AlreadyShared,
    /// <summary>MSG455: the recipient of a direct share is not an active Meeting Organizer.</summary>
    CannotReceive,
    /// <summary>MSG450: the action does not fit the share's state.</summary>
    NotAllowed,
    /// <summary>MSG468: the invitation's sender was deactivated, so it can only be declined (UC-84c EX-5).</summary>
    SenderDeactivated,
    /// <summary>MSG447.</summary>
    SaveFailed,
    /// <summary>MSG453.</summary>
    ReadFailed,
    /// <summary>MSG467: the list of people to choose could not be read (UC-84a/UC-84b EX-6).</summary>
    CandidatesReadFailed,
    /// <summary>A query string the endpoint does not understand (unknown purpose, bad paging): 400 without a code.</summary>
    BadQuery,
}

/// <summary>Which success message answers a write; the endpoint maps each to its code.</summary>
public enum CalendarShareSuccess
{
    None,
    Requested,
    Shared,
    Accepted,
    Declined,
    Withdrawn,
    Updated,
    ChangeRequested,
    ChangeDeclined,
    Ended,
}

public sealed record CalendarShareOutcome(
    CalendarShareFailure Failure = CalendarShareFailure.None,
    CalendarShareSuccess Success = CalendarShareSuccess.None,
    ShareView? Share = null);

public sealed record CalendarShareRead<T>(T? Value, CalendarShareFailure Failure = CalendarShareFailure.None) where T : class;

/// <summary>Which list of people to choose from: owners for a request (UC-84a), recipients for a direct share (UC-84b).</summary>
public enum CandidatePurpose
{
    Request,
    Direct,
}

/// <summary>
/// A person to choose. <see cref="HasOpenShare"/> says a Pending or Effective share of the same direction already
/// exists, so the page can grey it out; the server checks again when the share is sent (BR-423).
/// </summary>
public sealed record ShareCandidate(Guid Id, string FullName, string Email, bool HasOpenShare);

public sealed record ShareCandidatePage(IReadOnlyList<ShareCandidate> Items, int Total, int Page, int PageSize);

/// <summary>A candidate search as sent: <c>q</c> is matched on name and email, paging is 1-based.</summary>
public sealed record CandidateQuery(CandidatePurpose Purpose, string? Q, int Page, int PageSize)
{
    public const int DefaultPageSize = 20;
    public const int MaxPageSize = 100;
    public const int MaxQueryLength = 200;
}

/// <summary>SCRUM-142 — UC-84a → UC-84f. Every caller id comes from the validated token.</summary>
public interface ICalendarSharingService
{
    /// <summary>UC-84f. The "shared with me" half needs MeetingCreator (BR-430); the caller's own shares do not.</summary>
    Task<CalendarShareRead<ShareLists>> ListAsync(Guid actorId, CancellationToken ct = default);

    /// <summary>
    /// UC-84a/UC-84b step 1, filtered on the server. Request: every active account but the caller, for a Meeting Organizer
    /// only (MSG448). Direct: every active Meeting Organizer but the caller.
    /// </summary>
    Task<CalendarShareRead<ShareCandidatePage>> CandidatesAsync(Guid actorId, CandidateQuery query,
        CancellationToken ct = default);

    Task<CalendarShareOutcome> RequestAsync(Guid actorId, Guid? ownerId, ShareValues values, CancellationToken ct = default);

    Task<CalendarShareOutcome> ShareDirectlyAsync(Guid actorId, Guid? viewerId, DateOnly? startDate, DateOnly? endDate,
        CancellationToken ct = default);

    Task<CalendarShareOutcome> AcceptAsync(Guid actorId, Guid shareId, ShareValues values, CancellationToken ct = default);

    Task<CalendarShareOutcome> DeclineAsync(Guid actorId, Guid shareId, CancellationToken ct = default);

    /// <summary>UC-84d: a viewer asks for a change; an owner applies one at once.</summary>
    Task<CalendarShareOutcome> ChangeAsync(Guid actorId, Guid shareId, ShareValues values, CancellationToken ct = default);

    Task<CalendarShareOutcome> WithdrawAsync(Guid actorId, Guid shareId, CancellationToken ct = default);

    Task<CalendarShareOutcome> EndAsync(Guid actorId, Guid shareId, CancellationToken ct = default);
}

/// <summary>
/// BR-424 — stops every Effective share touching <c>userIds</c> that is no longer live: the owner or viewer deactivated,
/// or the viewer without MeetingCreator. A Pending invitation is kept (PO 09/11/2026, Q-C). Runs inside the caller's
/// transaction, after the caller's change is saved.
/// </summary>
public interface ICalendarShareStopper
{
    Task<int> StopDeadSharesAsync(IReadOnlyCollection<Guid> userIds, Guid? actorId, CalendarShareStopReason reason,
        CancellationToken ct = default);
}

/// <summary>A day of a shared week: unknown outside the share's range (BR-415), otherwise its busy blocks.</summary>
public sealed record SharedDay(DateOnly Date, bool Known, IReadOnlyList<BusyInterval> Busy);

/// <summary>One person who shared with the caller. A stopped share shows its status and no day (BR-424).</summary>
public sealed record SharedPerson(
    Guid ShareId,
    SharePerson Owner,
    string Status,
    string Level,
    DateOnly StartDate,
    DateOnly EndDate,
    IReadOnlyList<SharedDay> Days);

public sealed record SharedWeek(
    DateOnly WeekStart,
    DateOnly WeekEnd,
    string DayStart,
    string DayEnd,
    IReadOnlyList<SharedPerson> People);

/// <summary>SCRUM-142 — UC-85c, the week of busy hours people shared with the caller.</summary>
public interface ISharedCalendarService
{
    /// <summary><paramref name="week"/> is any day of the week; null is the current week in Vietnam.</summary>
    Task<CalendarShareRead<SharedWeek>> ReadWeekAsync(Guid actorId, DateOnly? week, CancellationToken ct = default);
}
