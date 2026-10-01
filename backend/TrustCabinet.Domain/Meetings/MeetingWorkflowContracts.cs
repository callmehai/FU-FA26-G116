using TrustCabinet.Domain.Enums;

namespace TrustCabinet.Domain.Meetings;

public sealed record MeetingTransitionCommand(
    MeetingTransitionAction Action,
    Guid? AssignedApproverId = null,
    string? Reason = null,
    Guid? OrganizerId = null);

public sealed record MeetingHistoryRecord(
    Guid Id,
    Guid MeetingId,
    MeetingHistoryAction Action,
    MeetingStatus? FromStatus,
    MeetingStatus ResultingStatus,
    Guid ActorId,
    string ActorName,
    DateTimeOffset OccurredAt,
    string? RejectionReason,
    Guid? AssignedApproverId,
    Guid? PreviousOrganizerId = null,
    Guid? NewOrganizerId = null);

public sealed record MeetingHistoryPage(
    IReadOnlyList<MeetingHistoryRecord> Items,
    int Page,
    int PageSize,
    long TotalCount);

public sealed record MeetingWorkflowResult(
    MeetingWorkflowOutcome Outcome,
    MeetingDetails? Meeting = null,
    MeetingHistoryRecord? History = null,
    IReadOnlyDictionary<string, string>? Errors = null)
{
    public bool Succeeded => Outcome == MeetingWorkflowOutcome.Success;

    public static MeetingWorkflowResult Success(MeetingDetails meeting, MeetingHistoryRecord? history = null)
        => new(MeetingWorkflowOutcome.Success, meeting, history);

    public static MeetingWorkflowResult Failure(MeetingWorkflowOutcome outcome) => new(outcome);

    public static MeetingWorkflowResult Invalid(
        MeetingWorkflowOutcome outcome, IReadOnlyDictionary<string, string> errors)
        => new(outcome, Errors: errors);
}

public enum MeetingWorkflowOutcome
{
    Success,
    NotFound,
    Forbidden,
    InvalidRequest,
    SubmissionNotReady,
    InvalidApprover,
    InvalidOrganizer,
    RejectionReasonRequired,
    RejectionReasonTooLong,
    StatusConflict,
    ReadFailed,
    SaveFailed,
}

public sealed record MeetingSubmissionReadiness(
    MeetingWorkflowOutcome Outcome,
    Guid MeetingId,
    MeetingStatus Status,
    IReadOnlyDictionary<string, string> Errors)
{
    public bool Ready => Outcome == MeetingWorkflowOutcome.Success && Errors.Count == 0;
}

public sealed record MeetingHistoryResult(
    MeetingWorkflowOutcome Outcome,
    MeetingHistoryPage? Page = null)
{
    public static MeetingHistoryResult Success(MeetingHistoryPage page)
        => new(MeetingWorkflowOutcome.Success, page);

    public static MeetingHistoryResult Failure(MeetingWorkflowOutcome outcome) => new(outcome);
}
