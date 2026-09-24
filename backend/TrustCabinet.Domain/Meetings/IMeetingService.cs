namespace TrustCabinet.Domain.Meetings;

public interface IMeetingService
{
    Task<MeetingResult> CreateAsync(MeetingInput input, Guid actorId, CancellationToken ct = default);

    Task<MeetingResult> GetAsync(Guid id, Guid actorId, CancellationToken ct = default);

    Task<IReadOnlyList<MeetingApproverOption>> ListEligibleApproversAsync(CancellationToken ct = default);

    Task<MeetingResult> CheckUpdateAsync(Guid id, Guid actorId, CancellationToken ct = default);

    Task<MeetingResult> UpdateAsync(
        Guid id, MeetingInput input, Guid actorId, CancellationToken ct = default);

    Task<MeetingResult> UpdateAsync(
        Guid id,
        Func<CancellationToken, Task<MeetingInput>> inputFactory,
        Guid actorId,
        CancellationToken ct = default);
}
