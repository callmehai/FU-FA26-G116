using TrustCabinet.Domain.Entities;

namespace TrustCabinet.Application.Email;

/// <summary>
/// Stages an invitation dispatch (and, unless the actor acted on themselves, the in-app notice) for a roster
/// change. Runs inside the caller's unit of work; the caller saves. A thin adapter over the Email module's
/// helper (rule R9): the Email module itself moves at CA-17 (SCRUM-182), which may replace this port.
/// </summary>
public interface IMeetingInvitationStager
{
    /// <param name="notify">Whether to also queue the in-app notice for the participant.</param>
    void Stage(Meeting meeting, MeetingParticipant? participant, MeetingGuestInvitation? guest,
        InvitationEventKind kind, DateTimeOffset now, bool notify);
}
