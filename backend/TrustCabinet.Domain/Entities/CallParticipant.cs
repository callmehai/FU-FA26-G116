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

    public Guid UserId { get; set; }
    public User User { get; set; } = null!;

    public DateTimeOffset JoinedAt { get; set; } = DateTimeOffset.UtcNow;

    public DateTimeOffset? LeftAt { get; set; }

    /// <summary>
    /// Whether they declined the invitation instead of joining. Only meaningful on a direct call.
    /// </summary>
    public bool Declined { get; set; }
}
