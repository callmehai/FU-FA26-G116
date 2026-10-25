namespace TrustCabinet.Domain.Meetings;

public interface IMeetingService
{
    Task<MeetingResult> CreateAsync(MeetingInput input, Guid actorId, IReadOnlyList<ParticipantSeed>? participants = null, CancellationToken ct = default);

    Task<MeetingResult> GetAsync(Guid id, Guid actorId, CancellationToken ct = default);

    Task<MeetingListResult> ListAsync(
        MeetingListQuery query, Guid actorId, CancellationToken ct = default);

    Task<MeetingApproverListResult> ListEligibleApproversAsync(CancellationToken ct = default);

    Task<MeetingResult> CheckUpdateAsync(Guid id, Guid actorId, CancellationToken ct = default);

    Task<MeetingResult> UpdateAsync(
        Guid id, MeetingInput input, Guid actorId, CancellationToken ct = default);

    Task<MeetingResult> UpdateAsync(
        Guid id,
        Func<CancellationToken, Task<MeetingInput>> inputFactory,
        Guid actorId,
        CancellationToken ct = default);

    Task<MeetingSubmissionReadiness> GetSubmissionReadinessAsync(
        Guid id, Guid actorId, CancellationToken ct = default);

    Task<MeetingWorkflowResult> TransitionAsync(
        Guid id, MeetingTransitionCommand command, Guid actorId, CancellationToken ct = default);

    Task<MeetingHistoryResult> GetHistoryAsync(
        Guid id, Guid actorId, int page, int pageSize, CancellationToken ct = default);
}
