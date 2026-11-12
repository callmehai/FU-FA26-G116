namespace TrustCabinet.Domain.Meetings;

public interface IMeetingLifecycleService
{
    Task<MeetingWorkflowResult> TransitionAsync(
        Guid meetingId, MeetingTransitionCommand command, Guid actorId, CancellationToken ct = default);

    /// <summary>
    /// Ends a meeting left running, as the system (ADR-0062): the same end as the Chair's — agenda, speaking queue, vote
    /// drafts, call room — with an <c>AutoEnd</c> history row and no actor. Re-checked under the meeting's lock, so a
    /// Chair who ends it at the same moment wins.
    /// </summary>
    /// <returns>False when the meeting no longer qualifies (<see cref="MeetingRules.IsOverrun"/>) or could not be saved.</returns>
    Task<bool> EndOverrunAsync(Guid meetingId, DateTimeOffset cutoff, string reason, CancellationToken ct = default);
}
