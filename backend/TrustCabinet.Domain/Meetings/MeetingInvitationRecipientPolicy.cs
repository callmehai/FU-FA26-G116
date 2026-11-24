using TrustCabinet.Domain.Entities;
using TrustCabinet.Domain.Enums;

namespace TrustCabinet.Domain.Meetings;

/// <summary>Shared recipient selection contract for meeting invitation flows.</summary>
public static class MeetingInvitationRecipientPolicy
{
    /// <summary>
    /// Selects the distinct, active participant rows represented by the supplied
    /// transaction snapshot.
    /// </summary>
    public static IReadOnlyList<MeetingParticipant> SelectSnapshot(
        IEnumerable<MeetingParticipant> participants)
        => participants
            .Where(Participants.ParticipantRules.Active)
            .DistinctBy(participant => participant.UserId)
            .ToArray();
}
