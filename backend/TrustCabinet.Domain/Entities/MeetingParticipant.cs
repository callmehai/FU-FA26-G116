using TrustCabinet.Domain.Entities.Common;
using TrustCabinet.Domain.Enums;

namespace TrustCabinet.Domain.Entities;

/// <summary>
/// One invited person on one meeting, plus how they answered the invitation.
/// </summary>
public class MeetingParticipant : Entity
{
    public Guid MeetingId { get; set; }
    public Meeting Meeting { get; set; } = null!;

    public Guid UserId { get; set; }
    public User User { get; set; } = null!;

    /// <summary>Their role in this meeting, which need not match their system role.</summary>
    public MeetingRole Role { get; set; } = MeetingRole.Member;

    public InvitationResponse Response { get; set; } = InvitationResponse.Pending;

    /// <summary>Set only when <see cref="Response"/> is Delegated: who attends in their place.</summary>
    public Guid? DelegatedToUserId { get; set; }
    public User? DelegatedToUser { get; set; }

    public DateTimeOffset? RespondedAt { get; set; }

    /// <summary>
    /// Whether they actually turned up (SCRUM-26) — a different question from
    /// <see cref="Response"/>, which is only what they said beforehand. Someone can accept and not
    /// come, and the quorum count depends on this one.
    ///
    /// It lives on this row rather than in its own table because it is one fact about one person
    /// at one meeting, and a separate table would just duplicate the same pair of keys.
    /// </summary>
    public AttendanceStatus Attendance { get; set; } = AttendanceStatus.Unknown;

    /// <summary>Who marked the attendance — the person themselves, or the secretary on their behalf.</summary>
    public Guid? AttendanceRecordedById { get; set; }
    public User? AttendanceRecordedBy { get; set; }

    public DateTimeOffset? AttendanceRecordedAt { get; set; }
}
