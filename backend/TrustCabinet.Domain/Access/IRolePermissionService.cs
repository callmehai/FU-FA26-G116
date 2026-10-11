using TrustCabinet.Domain.Enums;

namespace TrustCabinet.Domain.Access;

/// <summary>
/// The <c>:Role Permission Service</c> participant of UC-10 and UC-11 (SCRUM-60). Owns every rule
/// about the role ↔ capability matrix: what a valid configuration is, and what gets written to
/// the audit log when one goes through.
///
/// The endpoint holds none of that, exactly as <c>UserEndpoints</c> holds none of UC-06a's.
/// </summary>
public interface IRolePermissionService
{
    /// <summary>
    /// UC-10 — the matrix, plus the catalogue of capabilities that exist. No paging and no
    /// filtering: it is a handful of roles by four capabilities, and it is meant to be read as a
    /// grid.
    /// </summary>
    Task<RolePermissionMatrix> GetMatrixAsync(CancellationToken ct = default);

    /// <summary>
    /// UC-11 — sets exactly which capabilities <paramref name="roleId"/> carries.
    /// <paramref name="actorId"/> is the administrator doing it, recorded in the audit entry.
    ///
    /// Refuses (<see cref="RolePermissionOutcome.InvalidConfiguration"/>) a role that does not
    /// exist, and anything that would leave no active account holding
    /// <see cref="Capability.AdminUsers"/> — the system would then have
    /// nobody able to reach this screen, and no way back except a database console.
    /// </summary>
    Task<RolePermissionResult> UpdateAsync(
        Guid roleId,
        IReadOnlyCollection<Capability> capabilities,
        Guid actorId,
        CancellationToken ct = default);
}
