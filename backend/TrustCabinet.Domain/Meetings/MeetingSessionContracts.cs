using TrustCabinet.Domain.Entities;
using TrustCabinet.Domain.Enums;

namespace TrustCabinet.Domain.Meetings;

/// <summary>The fields a Secretary sets when creating or rescheduling a Meeting Session.</summary>
public sealed record MeetingSessionInput(string? Title, string? PlannedStartsAt, string? PlannedEndsAt);

public sealed record ValidatedMeetingSessionInput(
    string Title, DateTimeOffset PlannedStartsAt, DateTimeOffset PlannedEndsAt);

/// <summary>Session-level data only; Agenda Items are read through UC-54.</summary>
public sealed record MeetingSessionDetails(
    Guid Id,
    Guid MeetingId,
    string Title,
    DateTimeOffset PlannedStartsAt,
    DateTimeOffset PlannedEndsAt,
    MeetingSessionStatus Status,
    DateTimeOffset? ActualStartedAt,
    DateTimeOffset? ActualEndedAt)
{
    public static MeetingSessionDetails From(MeetingSession session) => new(
        session.Id,
        session.MeetingId,
        session.Title,
        session.PlannedStartsAt,
        session.PlannedEndsAt,
        session.Status,
        session.ActualStartedAt,
        session.ActualEndedAt);
}

public enum MeetingSessionOutcome
{
    Success,
    InvalidInformation,
    NotFound,
    /// <summary>The caller can read the meeting but is not its assigned Secretary (or Chair, for UC-29/30).</summary>
    Forbidden,
    /// <summary>The meeting or session status no longer allows the action.</summary>
    StatusConflict,
    /// <summary>BR-59: another session of the meeting is already InProgress.</summary>
    AnotherSessionInProgress,
    SaveFailed,
    ReadFailed,
}

public sealed record MeetingSessionResult(
    MeetingSessionOutcome Outcome,
    MeetingSessionDetails? Session = null,
    IReadOnlyDictionary<string, string>? Errors = null)
{
    public static MeetingSessionResult Success(MeetingSessionDetails session)
        => new(MeetingSessionOutcome.Success, session);

    public static MeetingSessionResult Failure(MeetingSessionOutcome outcome) => new(outcome);

    public static MeetingSessionResult Invalid(IReadOnlyDictionary<string, string> errors)
        => new(MeetingSessionOutcome.InvalidInformation, Errors: errors);
}

public sealed record MeetingSessionListResult(
    MeetingSessionOutcome Outcome,
    IReadOnlyList<MeetingSessionDetails>? Sessions = null);
