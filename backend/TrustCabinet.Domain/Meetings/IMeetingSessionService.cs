namespace TrustCabinet.Domain.Meetings;

/// <summary>UC-53 and UC-26 to UC-28. None of these change the parent meeting lifecycle (BR-92).</summary>
public interface IMeetingSessionService
{
    Task<MeetingSessionListResult> ListAsync(Guid meetingId, Guid actorId, CancellationToken ct = default);

    Task<MeetingSessionResult> CreateAsync(
        Guid meetingId, MeetingSessionInput input, Guid actorId, CancellationToken ct = default);

    Task<MeetingSessionResult> UpdateAsync(
        Guid meetingId, Guid sessionId, MeetingSessionInput input, Guid actorId, CancellationToken ct = default);

    Task<MeetingSessionResult> CancelAsync(
        Guid meetingId, Guid sessionId, Guid actorId, CancellationToken ct = default);
}
