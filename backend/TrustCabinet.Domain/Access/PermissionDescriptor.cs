using TrustCabinet.Domain.Enums;

namespace TrustCabinet.Domain.Access;

/// <summary>
/// One capability, described for a human reader — the diagram's <c>Permission</c> entity.
///
/// A value object read from the <see cref="Capability"/> enum, not a database row. The story is
/// explicit that an administrator edits the *mapping* and cannot invent a capability: a capability
/// only means anything because code checks for it, so a row an administrator could add would be a
/// permission that grants nothing. That is also why there is no <c>permissionId</c> — the enum
/// name is the identity, and it is what <c>role_capabilities</c> stores.
/// </summary>
/// <param name="Code">The enum name, e.g. <c>AdminUsers</c>. What the API and the table speak.</param>
/// <param name="Name">Vietnamese label for the column header.</param>
/// <param name="Description">What holding it actually allows, for the tooltip.</param>
/// <param name="FunctionalGroup">Which area of the product it belongs to, for grouping columns.</param>
public sealed record PermissionDescriptor(
    string Code,
    string Name,
    string Description,
    string FunctionalGroup);
