using TrustCabinet.Domain.Enums;

namespace TrustCabinet.Domain.Meetings;

public enum MeetingSortBy { Title, PlannedStartTime, CreatedAt }

public enum MeetingSortDirection { Asc, Desc }

/// <summary>Where a meeting's planned start falls against now, as AI-eCabinet's list filters it.</summary>
public enum MeetingTiming { Upcoming, Past }

/// <summary>
/// Which slice of the caller's visible meetings to list. Todo is AI-eCabinet's "Cần xử lý": the
/// meetings waiting on the caller — assigned to them for approval, or their own approved ones to
/// publish. A scope only narrows the visibility rule; it never widens it.
/// </summary>
public enum MeetingListScope { All, Todo }

/// <summary>Validated criteria for an authorized meeting page.</summary>
public sealed record MeetingListQuery(
    MeetingStatus? Status,
    Sensitivity? Sensitivity,
    string? Query,
    DateTimeOffset? From,
    DateTimeOffset? To,
    int Page,
    int PageSize,
    MeetingSortBy SortBy,
    MeetingSortDirection SortDirection,
    bool ExcludeDeclined = false,
    MeetingTiming? Timing = null,
    // The "now" Timing compares against; the service sets it from its clock.
    DateTimeOffset? Now = null,
    MeetingListScope Scope = MeetingListScope.All);

public sealed record MeetingListItem(
    Guid Id,
    string Title,
    DateTimeOffset PlannedStartTime,
    DateTimeOffset PlannedEndTime,
    MeetingMode Mode,
    string Location,
    DateTimeOffset CreatedAt,
    MeetingStatus Status,
    Sensitivity Sensitivity,
    string? MyResponse = null,
    bool CanRespond = false);

public sealed record MeetingListPage(
    IReadOnlyList<MeetingListItem> Items,
    int Page,
    int PageSize,
    long TotalCount);

public sealed record MeetingListResult(MeetingOutcome Outcome, MeetingListPage? Page = null)
{
    public static MeetingListResult Success(MeetingListPage page) => new(MeetingOutcome.Success, page);

    public static MeetingListResult ReadFailed() => new(MeetingOutcome.ReadFailed);
}
