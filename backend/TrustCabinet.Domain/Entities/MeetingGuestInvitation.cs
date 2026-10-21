using TrustCabinet.Domain.Entities.Common;
using TrustCabinet.Domain.Enums;

namespace TrustCabinet.Domain.Entities;

/// <summary>Invitation evidence; verified credentials and sessions are owned by UC-04.</summary>
public class MeetingGuestInvitation : Entity
{
    public Guid MeetingId { get; set; }
    public Meeting Meeting { get; set; } = null!;
    public string InvitedName { get; set; } = string.Empty;
    public string Email { get; set; } = string.Empty;
    public Guid InvitedById { get; set; }
    public Sensitivity SensitivityAtInvitation { get; set; }
    public InvitationResponse Response { get; set; } = InvitationResponse.Pending;
    public DateTimeOffset? RespondedAt { get; set; }
    public DateTimeOffset? RevokedAt { get; set; }
    public Guid? RevokedById { get; set; }
    public AttendanceStatus Attendance { get; set; } = AttendanceStatus.Unknown;
    public Guid? AttendanceRecordedById { get; set; }
    public DateTimeOffset? AttendanceRecordedAt { get; set; }
    public string InitialInvitationDeliveryKey { get; set; } = string.Empty;
}
