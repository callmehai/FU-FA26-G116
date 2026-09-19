using TrustCabinet.Domain.Entities.Common;

namespace TrustCabinet.Domain.Entities;

/// <summary>
/// An organisational unit — department, board, division. Units nest: a unit may have a parent and
/// any number of children, which is how "invite everyone in this unit" will resolve later.
/// </summary>
public class OrgUnit : Entity
{
    public required string Name { get; set; }

    /// <summary>Short code used in listings and invitations, e.g. "P.KHDT". Unique.</summary>
    public required string Code { get; set; }

    public Guid? ParentId { get; set; }
    public OrgUnit? Parent { get; set; }
    public ICollection<OrgUnit> Children { get; set; } = [];

    public ICollection<User> Users { get; set; } = [];

    /// <summary>
    /// Units are deactivated, never deleted: meetings from a unit that was dissolved must still
    /// render their history correctly.
    /// </summary>
    public bool IsActive { get; set; } = true;
}
