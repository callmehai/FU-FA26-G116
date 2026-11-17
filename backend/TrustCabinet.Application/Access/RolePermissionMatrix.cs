using TrustCabinet.Domain.Access;
using TrustCabinet.Domain.Enums;

namespace TrustCabinet.Application.Access;

/// <summary>
/// The whole role ↔ capability matrix as UC-10 shows it: every role, every capability it carries,
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
