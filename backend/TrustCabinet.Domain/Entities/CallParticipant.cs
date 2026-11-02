using TrustCabinet.Domain.Entities.Common;

namespace TrustCabinet.Domain.Entities;

/// <summary>
/// Who was in a call and when. Someone who joins, drops out and rejoins gets more than one row,
/// which is what makes it possible to say how long each person was actually connected.
/// </summary>
public class CallParticipant : Entity
{
    public Guid CallSessionId { get; set; }
    public CallSession CallSession { get; set; } = null!;

    /// <summary>Set for an internal user; exactly one of this and <see cref="GuestSessionId"/> is set.</summary>
    public Guid? UserId { get; set; }
    public User? User { get; set; }

    /// <summary>
    /// Set for a verified guest (SCRUM-59). A guest is never written as a user or a meeting participant; the session
    /// leads to the invitation, its self-declared name and email. Deleted with the session.
    /// </summary>
    public Guid? GuestSessionId { get; set; }
    public GuestSession? GuestSession { get; set; }

    public DateTimeOffset JoinedAt { get; set; } = DateTimeOffset.UtcNow;

    public DateTimeOffset? LeftAt { get; set; }

    /// <summary>
    /// Whether they declined the invitation instead of joining. Only meaningful on a direct call.
    /// </summary>
    public bool Declined { get; set; }
}
