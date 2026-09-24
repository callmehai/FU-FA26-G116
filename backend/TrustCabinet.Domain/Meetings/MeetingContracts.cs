using TrustCabinet.Domain.Entities;
using TrustCabinet.Domain.Enums;

namespace TrustCabinet.Domain.Meetings;

/// <summary>The fields a meeting creator may set or change through the meeting form.</summary>
public sealed record MeetingInput(
    string? Title,
    string? StartsAt,
    string? EndsAt,
    string? Mode,
    string? Location,
    string? Sensitivity,
    string? Description,
    Guid? AssignedApproverId = null);

/// <summary>A validated and normalized form value, ready to persist.</summary>
public sealed record ValidatedMeetingInput(
    string Title,
    DateTimeOffset StartsAt,
    DateTimeOffset? EndsAt,
    MeetingMode Mode,
    string Location,
    Sensitivity Sensitivity,
    string Description,
    Guid AssignedApproverId);

public sealed record MeetingApproverOption(Guid Id, string FullName);

/// <summary>A safe, flat representation returned by the meeting service.</summary>
public sealed record MeetingDetails(
    Guid Id,
    string Title,
    string Description,
    DateTimeOffset StartsAt,
    DateTimeOffset? EndsAt,
    MeetingMode Mode,
    string Location,
    Sensitivity Sensitivity,
    MeetingStatus Status,
    Guid CreatedById,
    Guid? AssignedApproverId,
    DateTimeOffset CreatedAt,
    DateTimeOffset UpdatedAt)
{
    public static MeetingDetails From(Meeting meeting) => new(
        meeting.Id,
        meeting.Title,
        meeting.Description,
        meeting.StartsAt,
        meeting.EndsAt,
        meeting.Mode,
        meeting.Location,
        meeting.Sensitivity,
        meeting.Status,
        meeting.CreatedById,
        meeting.AssignedApproverId,
        meeting.CreatedAt,
        meeting.UpdatedAt);
}

public enum MeetingOutcome
{
    Success,
    InvalidInformation,
    NotFound,
    NotEditable,
    SaveFailed,
    ReadFailed,
}

public sealed record MeetingResult(
    MeetingOutcome Outcome,
    MeetingDetails? Meeting = null,
    IReadOnlyDictionary<string, string>? Errors = null)
{
    public bool Succeeded => Outcome == MeetingOutcome.Success;

    public static MeetingResult Success(MeetingDetails meeting) => new(MeetingOutcome.Success, meeting);

    public static MeetingResult Failure(MeetingOutcome outcome) => new(outcome);

    public static MeetingResult Invalid(IReadOnlyDictionary<string, string> errors)
        => new(MeetingOutcome.InvalidInformation, Errors: errors);
}
