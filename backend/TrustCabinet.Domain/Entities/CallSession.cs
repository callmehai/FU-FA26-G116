using TrustCabinet.Domain.Entities.Common;
using TrustCabinet.Domain.Enums;

namespace TrustCabinet.Domain.Entities;

/// <summary>
/// A voice or video call — either person to person, or the room belonging to a meeting
/// (SCRUM-40, SCRUM-41).
///
/// Only the fact of the call is stored. The media itself never touches the database: it travels
/// between browsers over WebRTC, and the backend only carries the signalling.
/// </summary>
public class CallSession : Entity
{
    public CallKind Kind { get; set; }

    /// <summary>Set for a meeting room, null for a direct call.</summary>
    public Guid? MeetingId { get; set; }
    public Meeting? Meeting { get; set; }

    public CallStatus Status { get; set; } = CallStatus.Ringing;

    public Guid StartedById { get; set; }
    public User StartedBy { get; set; } = null!;

    public DateTimeOffset StartedAt { get; set; } = DateTimeOffset.UtcNow;

    public DateTimeOffset? EndedAt { get; set; }

    /// <summary>
    /// The recording made of this call, when one was made (SCRUM-44). Same consent and retention
    /// duties as recording a sitting in person.
    /// </summary>
    public Guid? RecordingId { get; set; }
    public Recording? Recording { get; set; }

    public ICollection<CallParticipant> Participants { get; set; } = [];
}
