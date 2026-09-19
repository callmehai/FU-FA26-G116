using TrustCabinet.Domain.Entities.Common;

namespace TrustCabinet.Domain.Entities;

/// <summary>
/// A record of something someone did that matters (SCRUM-16): signing in, moving a meeting through
/// its lifecycle, reading a confidential document, issuing minutes, changing a person's rights.
///
/// This is business evidence, not an application log. It is append-only — the application never
/// updates or deletes a row — and it must not be mixed in with technical logging, which gets
/// rotated away and nobody treats as authoritative.
/// </summary>
public class AuditLogEntry : Entity
{
    /// <summary>Null only for an action with no signed-in actor, such as a failed sign-in attempt.</summary>
    public Guid? ActorId { get; set; }
    public User? Actor { get; set; }

    /// <summary>What happened, as a stable code such as "meeting.published" or "document.viewed".</summary>
    public required string Action { get; set; }

    /// <summary>What it happened to — "Meeting", "Document", "User".</summary>
    public required string ObjectType { get; set; }

    public Guid? ObjectId { get; set; }

    /// <summary>Where the request came from. Part of the evidence, so it is stored with the row.</summary>
    public string IpAddress { get; set; } = string.Empty;

    /// <summary>Anything else worth keeping about this action, as JSON.</summary>
    public string Details { get; set; } = "{}";

    public DateTimeOffset OccurredAt { get; set; } = DateTimeOffset.UtcNow;
}
