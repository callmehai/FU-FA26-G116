namespace TrustCabinet.Application.Participants;

public sealed class UnavailableMeetingParticipantAccessPublisher : IMeetingParticipantAccessPublisher
{
    // Replaced by the call room's publisher (SCRUM-59); left for hosts without it. Never part of authorization.
    public Task PublishAsync(MeetingAccessChange change, CancellationToken ct) => Task.CompletedTask;
}
