namespace TrustCabinet.Domain.Entities.Common;

/// <summary>
/// Shared identity and timestamps for every stored entity.
/// </summary>
public abstract class Entity
{
    /// <summary>
    /// Version 7 GUID: random enough not to be guessable from the outside, but time-ordered, so
    /// rows inserted in sequence land next to each other in the clustered index instead of
    /// fragmenting it the way a random v4 GUID would.
    /// </summary>
    public Guid Id { get; set; } = Guid.CreateVersion7();

    public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;

    public DateTimeOffset UpdatedAt { get; set; } = DateTimeOffset.UtcNow;
}
