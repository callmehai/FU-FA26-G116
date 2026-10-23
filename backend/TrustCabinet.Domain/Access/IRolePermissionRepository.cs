using TrustCabinet.Domain.Entities;
using TrustCabinet.Domain.Enums;

namespace TrustCabinet.Domain.Access;

/// <summary>
/// The <c>:Role Permission Repository</c> participant of UC-10 and UC-11 — the only way
/// <see cref="IRolePermissionService"/> reaches <c>role_capabilities</c>.
///
/// Queries and writes only; every rule lives in the service, as in <c>IUserAccountRepository</c>.
/// </summary>
public interface IRolePermissionRepository
{
    /// <summary>
    /// The whole matrix — UC-10's "Retrieve role and permission matrix". Every role appears, even
    /// one with no capabilities: the screen draws a grid, and a role missing from the answer would
    /// be a missing row rather than an empty one.
    /// </summary>
    Task<IReadOnlyDictionary<Guid, IReadOnlyCollection<Capability>>> GetMatrixAsync(
        CancellationToken ct = default);

    /// <summary>
    /// Every role, in display order: the built-in ones first (<see cref="BuiltInRoles.All"/>), then
    /// the rest by creation time. The rows of the grid — a role with no capabilities still has one.
    /// </summary>
    Task<IReadOnlyCollection<Role>> ListRolesAsync(CancellationToken ct = default);

    /// <summary>
    /// Replaces the capabilities of one role with exactly <paramref name="capabilities"/> —
    /// UC-11's "Update role permission configuration".
    ///
    /// Replace rather than add/remove deltas: the screen submits the state of a row of
    /// checkboxes, so the request already describes the end state. Computing a delta in the
    /// browser would let two administrators saving at once each apply their own half.
    ///
    /// Does not save; the caller decides when, so the read-modify-write runs in one transaction.
    /// </summary>
    Task ReplaceCapabilitiesAsync(
        Guid roleId, IReadOnlyCollection<Capability> capabilities, CancellationToken ct = default);

    /// <summary>
    /// Persists the pending change. Separate from <see cref="ReplaceCapabilitiesAsync"/> so the
    /// service can decide the transaction boundary, and so a refusal to save is a branch the
    /// service can answer (MSG18) rather than an exception reaching the endpoint.
    /// </summary>
    Task SaveChangesAsync(CancellationToken ct = default);

    /// <summary>
    /// The ids of every active account currently holding <paramref name="roleId"/> — who are told
    /// to refresh their token when the role's capabilities change.
    ///
    /// Ids rather than entities: the only thing done with them is addressing a realtime push, and
    /// loading whole accounts to read one column each would be a query the screen never uses.
    /// </summary>
    Task<IReadOnlyCollection<Guid>> FindActiveUserIdsInRoleAsync(
        Guid roleId, CancellationToken ct = default);

    /// <summary>
    /// How many active accounts would still be able to administer if
    /// <paramref name="rolesWithCapability"/> were the complete set of roles holding
    /// <c>AdminUsers</c> — "if I save this, is anyone left who can undo it?".
    ///
    /// Takes the proposed roles rather than reading the current matrix, because the question is
    /// about the state *after* the write. Counting against the stored matrix would answer for the
    /// state being replaced, which is exactly the state that is about to stop being true.
    ///
    /// Counts **accounts**, not roles. A role that holds the capability but has no active member
    /// leaves nobody able to administer, so checking "some role still holds it" is not the
    /// invariant — <c>UserService.WouldRemoveTheLastAdministratorAsync</c> guards the same rule
    /// from the user-management side and counts accounts too.
    ///
    /// The two answering alike is necessary but was not sufficient: they also have to be unable to
    /// run at the same time, or each removes the last administrator while the other's is still
    /// standing. That is what <c>IAdministratorInvariantGuard</c> is for.
    /// </summary>
    Task<int> CountActiveUsersInRolesAsync(
        IReadOnlyCollection<Guid> rolesWithCapability, CancellationToken ct = default);
}
