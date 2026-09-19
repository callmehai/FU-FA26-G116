using TrustCabinet.Domain.Entities.Common;
using TrustCabinet.Domain.Enums;

namespace TrustCabinet.Domain.Entities;

/// <summary>
/// A meeting, from first draft to archived record. <see cref="Status"/> is the spine of the whole
/// system: it decides who may edit, when voting opens, and when minutes can be issued.
/// </summary>
public class Meeting : Entity
{
    public required string Title { get; set; }

    public string Description { get; set; } = string.Empty;

    /// <summary>
    /// No database default on purpose. The application must state the status explicitly, so a code
    /// path that forgets fails loudly instead of silently creating a meeting in some default state.
    /// </summary>
    public MeetingStatus Status { get; set; }

    /// <summary>
    /// Also deliberately without a default: the caller names the sensitivity, because silently
    /// defaulting to the most permissive value is how a confidential meeting leaks.
    /// </summary>
    public Sensitivity Sensitivity { get; set; }

    public MeetingMode Mode { get; set; }

    /// <summary>Room name, address, or how to join when the meeting is online.</summary>
    public string Location { get; set; } = string.Empty;

    public DateTimeOffset StartsAt { get; set; }

    /// <summary>Planned end. Must be after <see cref="StartsAt"/> — enforced in the domain (SCRUM-17).</summary>
    public DateTimeOffset EndsAt { get; set; }

    public Guid CreatedById { get; set; }
    public User CreatedBy { get; set; } = null!;

    /// <summary>The unit that owns the meeting. Used for filtering and for unit-wide invitations.</summary>
    public Guid? OrgUnitId { get; set; }
    public OrgUnit? OrgUnit { get; set; }

    public ICollection<MeetingParticipant> Participants { get; set; } = [];
    public ICollection<MeetingStatusHistory> StatusHistory { get; set; } = [];
}
