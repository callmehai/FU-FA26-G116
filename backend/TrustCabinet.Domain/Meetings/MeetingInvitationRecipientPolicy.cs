using TrustCabinet.Domain.Entities;
using TrustCabinet.Domain.Enums;

namespace TrustCabinet.Domain.Meetings;

/// <summary>Shared recipient selection contract for meeting invitation flows.</summary>
public static class MeetingInvitationRecipientPolicy
{
    /// <summary>
    /// Selects the distinct, active, non-delegated participant rows represented by the supplied
    /// transaction snapshot. A delegate is eligible only when they also have their own qualifying
    /// participant row.
    /// </summary>
    public static IReadOnlyList<MeetingParticipant> SelectSnapshot(
        IEnumerable<MeetingParticipant> participants)
        => participants
            .Where(participant => participant.User is { IsActive: true }
                && participant.Response != InvitationResponse.Delegated)
            .DistinctBy(participant => participant.UserId)
            .ToArray();
}
