using TrustCabinet.Domain.Enums;

namespace TrustCabinet.Domain.Entities;

/// <summary>
/// Which capabilities a role carries. Reference data, seeded by the migration rather than entered
/// by an administrator, so that a fresh database is immediately consistent with the code.
///
/// It is a table rather than a hard-coded switch so an administrator screen can adjust it later
/// without a deployment (SCRUM-60). Note the tension that story has to resolve first: the rows
/// below ship via HasData, so a later migration would overwrite whatever an administrator changed.
/// </summary>
public class RoleCapability
{
    public SystemRole Role { get; set; }
    public Capability Capability { get; set; }
}
