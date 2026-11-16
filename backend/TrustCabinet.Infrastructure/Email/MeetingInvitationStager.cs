using TrustCabinet.Application.Email;
using TrustCabinet.Domain.Entities;

namespace TrustCabinet.Infrastructure.Email;

public sealed class MeetingInvitationStager(TrustCabinetDbContext db) : IMeetingInvitationStager
{
    public void Stage(Meeting meeting, MeetingParticipant? participant, MeetingGuestInvitation? guest,
        InvitationEventKind kind, DateTimeOffset now, bool notify)
        => MeetingInvitationEvents.Stage(db, meeting, participant, guest, kind, now, notify);
}
