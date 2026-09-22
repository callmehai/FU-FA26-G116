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

/// <summary>One row of the matrix: a role and the capabilities currently granted to it.</summary>
/// <param name="IsBuiltIn">
/// Whether the role is part of the system's definition rather than something anyone created. All
/// four are, today — <see cref="SystemRole"/> is an enum (ADR-0004), so a role cannot be added
/// without a deployment. The flag is on the wire because the diagram asks for it and because the
/// screen needs a reason to show the role name as fixed text rather than as an editable field.
/// </param>
public sealed record RolePermissions(
    SystemRole Role,
    IReadOnlyCollection<Capability> Capabilities,
    bool IsBuiltIn = true);
