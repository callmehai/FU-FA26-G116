namespace TrustCabinet.Domain.Meetings;

public interface IMeetingLifecycleService
{
    Task<MeetingWorkflowResult> TransitionAsync(
        Guid meetingId, MeetingTransitionCommand command, Guid actorId, CancellationToken ct = default);
}
