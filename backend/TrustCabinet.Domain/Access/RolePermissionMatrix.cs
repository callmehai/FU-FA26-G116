using TrustCabinet.Domain.Enums;

namespace TrustCabinet.Domain.Access;

/// <summary>
/// The whole role ↔ capability matrix as UC-09 shows it: every role, every capability it carries,
/// plus the catalogue of capabilities that exist at all so the screen can draw the columns.
///
/// Both halves are needed because the screen is a grid. Sending only the granted pairs would make
/// an empty role indistinguishable from a role the frontend has never heard of, and would leave
/// the columns to be hard-coded in the browser — which is how a capability added to the enum
/// silently stops appearing on the screen.
/// </summary>
public sealed record RolePermissionMatrix(
    IReadOnlyCollection<PermissionDescriptor> Capabilities,
    IReadOnlyCollection<RolePermissions> Roles);

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

/// <summary>
/// One row of the matrix: a role and the capabilities currently granted to it. Carries the role's
/// name and description too, since roles are rows (ADR-0008) and the screen has no other place to
/// learn them from.
/// </summary>
/// <param name="IsBuiltIn">
/// One of the four roles the system ships with. The screen shows its name as fixed text; its
/// capabilities stay editable like any other role's.
/// </param>
public sealed record RolePermissions(
    Guid Id,
    string Name,
    string? Description,
    bool IsBuiltIn,
    IReadOnlyCollection<Capability> Capabilities);
