using TrustCabinet.Domain.Enums;

namespace TrustCabinet.Domain.Entities;

/// <summary>
/// Which capabilities a role carries — the role ↔ capability matrix an administrator edits at
/// runtime (SCRUM-60). The initial rows come from <c>ReferenceDataSeeder</c>, never from HasData
/// (ADR-0007).
///
/// Deleting a role takes its rows with it (<c>ON DELETE CASCADE</c>): they are that role's own
/// data, not anybody else's.
/// </summary>
public class RoleCapability
{
    public Guid RoleId { get; set; }
    public Capability Capability { get; set; }
}
