using TrustCabinet.Domain.Enums;

namespace TrustCabinet.Domain.Meetings;

/// <summary>The fields a meeting creator may set or change through the meeting form.</summary>
public sealed record MeetingInput(
    string? Title,
    string? PlannedStartTime,
    string? PlannedEndTime,
    string? Mode,
    string? Location,
    string? OnlineLink,
    string? Sensitivity,
    string? Description);

/// <summary>A validated and normalized form value, ready to persist.</summary>
public sealed record ValidatedMeetingInput(
    string Title,
    DateTimeOffset PlannedStartTime,
    DateTimeOffset PlannedEndTime,
    MeetingMode Mode,
    string Location,
    string? OnlineLink,
    Sensitivity Sensitivity,
    string Description);
