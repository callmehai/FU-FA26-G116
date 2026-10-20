using TrustCabinet.Domain.Entities;
using TrustCabinet.Domain.Enums;
using TrustCabinet.Domain.Participants;

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

[System.Text.Json.Serialization.JsonUnmappedMemberHandling(System.Text.Json.Serialization.JsonUnmappedMemberHandling.Disallow)]
public sealed record ParticipantSeed(Guid UserId, string? Role);
public sealed record MeetingPerson(Guid UserId, string FullName);

public sealed record MeetingApproverOption(Guid Id, string FullName);

public sealed record MeetingApproverListResult(
    MeetingOutcome Outcome,
    IReadOnlyList<MeetingApproverOption>? Approvers = null)
{
    public static MeetingApproverListResult Success(IReadOnlyList<MeetingApproverOption> approvers)
        => new(MeetingOutcome.Success, approvers);

    public static MeetingApproverListResult ReadFailed()
        => new(MeetingOutcome.ReadFailed);
}

/// <summary>A safe, flat representation returned by the meeting service.</summary>
public sealed record MeetingDetails(
    Guid Id,
    string Title,
    string Description,
    DateTimeOffset PlannedStartTime,
    DateTimeOffset PlannedEndTime,
    DateTimeOffset? ActualStartedAt,
    DateTimeOffset? ActualEndedAt,
    MeetingMode Mode,
    string Location,
    string? OnlineLink,
    Sensitivity Sensitivity,
    MeetingStatus Status,
    Guid CreatedById,
    Guid? AssignedApproverId,
    DateTimeOffset CreatedAt,
    DateTimeOffset UpdatedAt,
    MeetingPerson? Chair = null,
    IReadOnlyList<MeetingPerson>? Secretaries = null,
    string? MyMeetingRole = null)
{
    public static MeetingDetails From(Meeting meeting, Guid? actorId = null) => new(
        meeting.Id,
        meeting.Title,
        meeting.Description,
        meeting.PlannedStartTime,
        meeting.PlannedEndTime,
        meeting.ActualStartedAt,
        meeting.ActualEndedAt,
        meeting.Mode,
        meeting.Location,
        meeting.OnlineLink,
        meeting.Sensitivity,
        meeting.Status,
        meeting.CreatedById,
        meeting.Status == MeetingStatus.Draft ? null : meeting.AssignedApproverId,
        meeting.CreatedAt,
        meeting.UpdatedAt,
        meeting.Participants.Where(p => ParticipantRules.Current(p) && p.Role == MeetingRole.Chair)
            .Select(p => new MeetingPerson(p.UserId, p.User.FullName)).SingleOrDefault(),
        meeting.Participants.Where(p => ParticipantRules.Current(p) && p.Role == MeetingRole.Secretary)
            .OrderBy(p => p.User.FullName).ThenBy(p => p.UserId).Select(p => new MeetingPerson(p.UserId, p.User.FullName)).ToArray(),
        meeting.Participants.SingleOrDefault(p => ParticipantRules.Current(p) && p.UserId == actorId)?.Role.ToString());
}

public enum MeetingOutcome
{
    Success,
    InvalidInformation,
    InvalidParticipants,
    NotFound,
    NotEditable,
    SaveFailed,
    ReadFailed,
}

/// <param name="Rescheduled">True when a Published meeting's schedule changed and invitees were notified.</param>
public sealed record MeetingResult(
    MeetingOutcome Outcome,
    MeetingDetails? Meeting = null,
    IReadOnlyDictionary<string, string>? Errors = null,
    bool Rescheduled = false)
{
    public bool Succeeded => Outcome == MeetingOutcome.Success;

    public static MeetingResult Success(MeetingDetails meeting, bool rescheduled = false)
        => new(MeetingOutcome.Success, meeting, Rescheduled: rescheduled);

    public static MeetingResult Failure(MeetingOutcome outcome) => new(outcome);

    public static MeetingResult Invalid(IReadOnlyDictionary<string, string> errors)
        => new(MeetingOutcome.InvalidInformation, Errors: errors);
}
