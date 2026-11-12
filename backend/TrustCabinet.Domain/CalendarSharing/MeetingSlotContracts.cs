using TrustCabinet.Domain.Meetings;

namespace TrustCabinet.Domain.CalendarSharing;

/// <summary>A meeting-time search as sent (UC-85a step 1); every field is optional and checked by <see cref="MeetingSlotRules"/>.</summary>
public sealed record SlotSearchInput(
    IReadOnlyList<Guid>? UserIds,
    bool? IncludeMe,
    int? DurationMinutes,
    DateOnly? FromDate,
    DateOnly? ToDate,
    IReadOnlyList<int>? Weekdays,
    string? WorkStart,
    string? WorkEnd,
    bool? LunchBreak,
    string? LunchStart,
    string? LunchEnd,
    int? BufferMinutes,
    int? StepMinutes);

/// <summary>
/// A search after defaults and checks (BR-421, BR-422). Times are Vietnam wall-clock; a null lunch means no lunch break.
/// </summary>
public sealed record SlotSearch(
    IReadOnlyList<Guid> UserIds,
    bool IncludeMe,
    int DurationMinutes,
    DateOnly FromDate,
    DateOnly ToDate,
    IReadOnlyList<DayOfWeek> Weekdays,
    TimeOnly WorkStart,
    TimeOnly WorkEnd,
    TimeOnly? LunchStart,
    TimeOnly? LunchEnd,
    int BufferMinutes,
    int StepMinutes);

/// <summary>The days a person's calendar is known on, both ends included (BR-415).</summary>
public readonly record struct KnownDays(DateOnly Start, DateOnly End)
{
    public bool Contains(DateOnly date) => Start <= date && date <= End;
}

/// <summary>
/// One person whose calendar is counted: their busy periods and the days known. A null <see cref="Window"/> is the finder's
/// own calendar, known on every day (BR-418).
/// </summary>
public sealed record SlotPerson(Guid Id, KnownDays? Window, IReadOnlyList<BusyInterval> Busy);

/// <summary>A candidate slot and, among the people counted, who is busy and whose day is unknown.</summary>
public sealed record FoundSlot(
    DateOnly Date,
    DateTimeOffset Start,
    DateTimeOffset End,
    int FreeCount,
    int CountedCount,
    IReadOnlyList<Guid> BusyUserIds,
    IReadOnlyList<Guid> UnknownUserIds);

/// <summary>What the finder chose: common slots, the slots with the most people free (MSG445), or nothing.</summary>
public enum SlotResultKind
{
    None,
    AllFree,
    MostFree,
}

public sealed record SlotFinding(SlotResultKind Kind, IReadOnlyList<FoundSlot> Slots, bool Truncated);

/// <summary>How a person of the search is counted: the finder's own calendar, a shared one, or none (BR-416).</summary>
public static class SlotPersonStatus
{
    public const string Own = "Own";
    public const string Shared = "Shared";
    public const string NotShared = "NotShared";
}

/// <summary>
/// A person of the search as the finder may see them: a name from the finder's own invite list and the share the finder
/// is one side of — nothing about any meeting (BR-427).
/// </summary>
public sealed record SlotPersonView(
    Guid Id,
    string FullName,
    string Email,
    string Status,
    string? Level,
    DateOnly? StartDate,
    DateOnly? EndDate);

public sealed record SlotView(
    DateTimeOffset Start,
    DateTimeOffset End,
    int FreeCount,
    int CountedCount,
    IReadOnlyList<Guid> BusyUserIds,
    IReadOnlyList<Guid> UnknownUserIds);

public sealed record SlotDayView(DateOnly Date, IReadOnlyList<SlotView> Slots);

public sealed record SlotSearchResult(
    SlotResultKind Kind,
    bool Truncated,
    IReadOnlyList<SlotPersonView> People,
    IReadOnlyList<SlotDayView> Days);

public enum MeetingSlotFailure
{
    None,
    /// <summary>MSG448: the caller is not a Meeting Organizer (BR-430).</summary>
    NotOrganizer,
    /// <summary>MSG444: search parameters break BR-421/BR-422 (UC-85a EX-1).</summary>
    InvalidSearch,
    /// <summary>MSG453: shares, people or meetings could not be read (UC-85a EX-3, UC-85b EX-3).</summary>
    ReadFailed,
    /// <summary>MSG446: someone free when the slot was found is now busy or unknown (UC-85b EX-1).</summary>
    SlotTaken,
    /// <summary>MSG31: the slot's times, buffer or accepted list are not valid (UC-85b through UC-14 EX-1).</summary>
    InvalidSlot,
    /// <summary>MSG277: the invitee list is not valid (UC-85b through UC-14 EX-3).</summary>
    InvalidInvitees,
}

public sealed record SlotSearchOutcome(
    MeetingSlotFailure Failure,
    SlotSearchResult? Result = null,
    IReadOnlyDictionary<string, string>? Errors = null);

/// <summary>
/// UC-85b: the meeting fields of UC-14, the invitees in order (the first becomes Chair, BR-429) and what the slot looked
/// like when it was found: the buffer used and who was already busy or unknown then.
/// </summary>
public sealed record SlotDraftInput(
    string? Title,
    string? PlannedStartTime,
    string? PlannedEndTime,
    string? Mode,
    string? Location,
    string? OnlineLink,
    string? Sensitivity,
    string? Description,
    IReadOnlyList<Guid>? UserIds,
    bool? IncludeMe,
    int? BufferMinutes,
    IReadOnlyList<Guid>? AcceptedBusyUserIds);

/// <summary>A failure before the meeting was created, or the result of UC-14's creation.</summary>
public sealed record SlotDraftOutcome(
    MeetingSlotFailure Failure,
    MeetingResult? Meeting = null,
    IReadOnlyDictionary<string, string>? Errors = null);

/// <summary>SCRUM-137 — UC-85a find meeting time slots, UC-85b create a draft from one. The caller id comes from the token.</summary>
public interface IMeetingSlotService
{
    Task<SlotSearchOutcome> FindAsync(Guid actorId, SlotSearchInput input, CancellationToken ct = default);

    Task<SlotDraftOutcome> CreateDraftAsync(Guid actorId, SlotDraftInput input, CancellationToken ct = default);
}
