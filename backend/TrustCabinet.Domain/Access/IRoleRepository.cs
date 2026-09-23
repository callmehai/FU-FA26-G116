using TrustCabinet.Domain.Entities;
using TrustCabinet.Domain.Enums;

namespace TrustCabinet.Domain.Access;

/// <summary>
/// The <c>:Role Repository</c> participant of UC-11 … UC-13 — the only way
/// <see cref="IRoleService"/> reaches <c>roles</c>. Queries and writes only; every rule lives in
/// the service.
/// </summary>
public interface IRoleRepository
{
    /// <summary>
    /// Every role with its account count, in display order: built-in first
    /// (<see cref="BuiltInRoles.All"/>), then the rest by creation time.
    /// </summary>
    Task<IReadOnlyCollection<RoleSummary>> ListAsync(CancellationToken ct = default);

    /// <summary>The role, tracked, so an update is "change it and save".</summary>
    Task<Role?> FindByIdAsync(Guid id, CancellationToken ct = default);

    /// <summary>
    /// Whether a role other than <paramref name="exceptId"/> already uses this name, ignoring case —
    /// the diagrams' "Find role by name (case-insensitive)". The unique index is what actually
    /// guarantees it; this is the check that answers MSG21 instead of a 500.
    /// </summary>
    Task<bool> NameExistsAsync(string name, Guid? exceptId = null, CancellationToken ct = default);

    /// <summary>Every account holding the role, deactivated ones included.</summary>
    Task<int> CountUsersAsync(Guid roleId, CancellationToken ct = default);

    /// <summary>What the role carries — recorded when it is deleted, before the cascade.</summary>
    Task<IReadOnlyCollection<Capability>> GetCapabilitiesAsync(
        Guid roleId, CancellationToken ct = default);

    /// <summary>Stages a new role and its capabilities. Does not save.</summary>
    Task AddAsync(Role role, IReadOnlyCollection<Capability> capabilities, CancellationToken ct = default);

    /// <summary>Stages the role for deletion; its capabilities go with it (cascade). Does not save.</summary>
    void Remove(Role role);

    /// <summary>
    /// Persists what is staged. Separate so the service decides the transaction boundary, and so
    /// a refused save is a branch it can answer rather than an exception reaching the endpoint.
    /// </summary>
    Task SaveChangesAsync(CancellationToken ct = default);
}
