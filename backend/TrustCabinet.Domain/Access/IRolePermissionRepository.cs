using TrustCabinet.Domain.Enums;

namespace TrustCabinet.Domain.Access;

/// <summary>
/// The <c>:Role Permission Repository</c> participant of UC-09 and UC-10 — the only way
/// <see cref="IRolePermissionService"/> reaches <c>role_capabilities</c>.
///
/// Queries and writes only; every rule lives in the service, as in <c>IUserAccountRepository</c>.
/// </summary>
public interface IRolePermissionRepository
{
    /// <summary>
    /// The whole matrix — UC-09's "Retrieve role and permission matrix". Every role appears, even
    /// one with no capabilities: the screen draws a grid, and a role missing from the answer would
    /// be a missing row rather than an empty one.
    /// </summary>
    Task<IReadOnlyDictionary<SystemRole, IReadOnlyCollection<Capability>>> GetMatrixAsync(
        CancellationToken ct = default);

    /// <summary>
    /// Replaces the capabilities of one role with exactly <paramref name="capabilities"/> —
    /// UC-10's "Update role permission configuration".
    ///
    /// Replace rather than add/remove deltas: the screen submits the state of a row of
    /// checkboxes, so the request already describes the end state. Computing a delta in the
    /// browser would let two administrators saving at once each apply their own half.
    ///
    /// Does not save; the caller decides when, so the read-modify-write runs in one transaction.
    /// </summary>
    Task ReplaceCapabilitiesAsync(
        SystemRole role, IReadOnlyCollection<Capability> capabilities, CancellationToken ct = default);

    /// <summary>
    /// Persists the pending change. Separate from <see cref="ReplaceCapabilitiesAsync"/> so the
    /// service can decide the transaction boundary, and so a refusal to save is a branch the
    /// service can answer (MSG18) rather than an exception reaching the endpoint.
    /// </summary>
    Task SaveChangesAsync(CancellationToken ct = default);

    /// <summary>
    /// The ids of every active account currently holding <paramref name="role"/> — whose sessions
    /// have to end when the role's capabilities change.
    ///
    /// Ids rather than entities: the only thing done with them is revoking refresh tokens, and
    /// loading whole accounts to read one column each would be a query the screen never uses.
    /// </summary>
    Task<IReadOnlyCollection<Guid>> FindActiveUserIdsInRoleAsync(
        SystemRole role, CancellationToken ct = default);
}
