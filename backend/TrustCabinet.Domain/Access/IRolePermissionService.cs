using TrustCabinet.Domain.Enums;

namespace TrustCabinet.Domain.Access;

/// <summary>
/// The <c>:Role Permission Service</c> participant of UC-09 and UC-10 (SCRUM-60). Owns every rule
/// about the role ↔ capability matrix: what a valid configuration is, and what gets written to
/// the audit log when one goes through.
///
/// The endpoint holds none of that, exactly as <c>UserEndpoints</c> holds none of UC-05's.
/// </summary>
public interface IRolePermissionService
{
    /// <summary>
    /// UC-09 — the matrix, plus the catalogue of capabilities that exist. No paging and no
    /// filtering: it is four roles by four capabilities, and it is meant to be read as a grid.
    /// </summary>
    Task<RolePermissionMatrix> GetMatrixAsync(CancellationToken ct = default);

    /// <summary>
    /// UC-10 — sets exactly which capabilities <paramref name="role"/> carries.
    /// <paramref name="actorId"/> is the administrator doing it, recorded in the audit entry.
    ///
    /// Refuses (<see cref="RolePermissionOutcome.InvalidConfiguration"/>) anything that would
    /// leave no role holding <see cref="Capability.AdminUsers"/> — the system would then have
    /// nobody able to reach this screen, and no way back except a database console.
    /// </summary>
    Task<RolePermissionResult> UpdateAsync(
        SystemRole role,
        IReadOnlyCollection<Capability> capabilities,
        Guid actorId,
        CancellationToken ct = default);
}
