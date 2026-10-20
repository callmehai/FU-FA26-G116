using TrustCabinet.Domain.Entities.Common;

namespace TrustCabinet.Domain.Entities;

public class MeetingParticipantPermission : Entity
{
    public Guid? MeetingParticipantId { get; set; }
    public MeetingParticipant? MeetingParticipant { get; set; }
    public Guid? GuestInvitationId { get; set; }
    public MeetingGuestInvitation? GuestInvitation { get; set; }
    public string PermissionKey { get; set; } = string.Empty;
    public Guid GrantedById { get; set; }
    public DateTimeOffset GrantedAt { get; set; }
}
