using TrustCabinet.Domain.Enums;

namespace TrustCabinet.Domain.Meetings;

public enum MeetingSortBy { Title, PlannedStartTime, CreatedAt }

public enum MeetingSortDirection { Asc, Desc }

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
    MeetingSortDirection SortDirection);

public sealed record MeetingListItem(
    Guid Id,
    string Title,
    DateTimeOffset PlannedStartTime,
    DateTimeOffset PlannedEndTime,
    MeetingMode Mode,
    string Location,
    DateTimeOffset CreatedAt,
    MeetingStatus Status,
    Sensitivity Sensitivity);

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
