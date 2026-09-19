using TrustCabinet.Domain.Enums;

namespace TrustCabinet.Domain.Entities;

/// <summary>
/// Which capabilities a role carries. Reference data, seeded by the migration rather than entered
/// by an administrator, so that a fresh database is immediately consistent with the code.
///
/// It is a table rather than a hard-coded switch so an administrator screen can adjust it later
/// without a deployment (SCRUM-12).
/// </summary>
public class RoleCapability
{
    public SystemRole Role { get; set; }
    public Capability Capability { get; set; }
}
