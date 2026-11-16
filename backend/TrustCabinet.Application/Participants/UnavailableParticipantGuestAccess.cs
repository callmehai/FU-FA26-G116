using TrustCabinet.Domain.Meetings;

namespace TrustCabinet.Application.Participants;

public sealed class UnavailableParticipantGuestAccess : IParticipantGuestAccess
{
    public Task<bool> CanAccessAsync(MeetingGuestSession session, CancellationToken ct) => Task.FromResult(false);
    public Task<IReadOnlyDictionary<Guid, GuestDisplayProfile>> GetProfilesAsync(Guid meetingId, IReadOnlyCollection<Guid> invitationIds, CancellationToken ct)
        => Task.FromResult<IReadOnlyDictionary<Guid, GuestDisplayProfile>>(new Dictionary<Guid, GuestDisplayProfile>());
}
