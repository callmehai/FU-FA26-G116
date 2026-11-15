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
