using TrustCabinet.Domain.Entities;
using TrustCabinet.Domain.Enums;

namespace TrustCabinet.Domain.Users;

/// <summary>
/// The <c>:User Repository</c> participant of UC-04 … UC-07 — the only way
/// <see cref="IUserService"/> reaches the database.
///
/// Separate from <c>Authentication.IUserRepository</c> on purpose. That one says of itself that it
/// is "not a general-purpose user repository, and a screen that needs to list users gets its own
/// query"; this is that query. Keeping them apart also keeps the authentication path free of write
/// methods, so nothing on a sign-in code path can create or modify an account.
/// </summary>
public interface IUserAccountRepository
{
    /// <summary>
    /// Every account, sorted by name — UC-04's "Sorted user account list". Sorted in the database
    /// rather than in memory, so PostgreSQL's collation decides the order of Vietnamese names
    /// instead of the ordinal comparison .NET would default to.
    /// </summary>
    /// <remarks>Returned with <see cref="User.Role"/> loaded, as is <see cref="FindByIdAsync"/>:
    /// every screen that shows an account shows its role's name.</remarks>
    Task<IReadOnlyCollection<User>> ListAsync(CancellationToken ct = default);

    Task<User?> FindByIdAsync(Guid id, CancellationToken ct = default);

    /// <summary>
    /// The role with this id, or null — UC-05 and UC-06 check the chosen role exists, now that a
    /// role is a row an administrator can delete rather than an enum value (ADR-0008).
    /// </summary>
    Task<Role?> FindRoleAsync(Guid roleId, CancellationToken ct = default);

    /// <summary>
    /// Whether any account other than <paramref name="exceptId"/> already uses this email —
    /// UC-05's "Check Email availability", and the same check on UC-06's edit, where the account
    /// keeping its own email is not a duplicate.
    ///
    /// The unique index is what actually guarantees it; this is the check that produces MSG08
    /// instead of a 500. See <c>UserService.CreateAsync</c> for why both are needed.
    /// </summary>
    Task<bool> EmailExistsAsync(string email, Guid? exceptId = null, CancellationToken ct = default);

    /// <summary>
    /// What this role may do, read from <c>role_capabilities</c>. Same query as the
    /// authentication repository's, needed here so the last-administrator rule can ask
    /// "does this role still administer?" without reaching into the sign-in path.
    /// </summary>
    Task<IReadOnlyCollection<Capability>> GetCapabilitiesAsync(
        Guid roleId, CancellationToken ct = default);

    /// <summary>
    /// How many active accounts hold <paramref name="capability"/>, not counting
    /// <paramref name="exceptId"/> — "if I go through with this, is anyone left who can still
    /// administer?".
    ///
    /// Counted through <c>role_capabilities</c> rather than by comparing against the Admin role
    /// name, because that table is what actually decides who may do what and it is meant to be
    /// re-pointed without a deployment — business code never compares roles, it asks for
    /// capabilities.
    /// </summary>
    Task<int> CountActiveWithCapabilityAsync(
        Capability capability, Guid exceptId, CancellationToken ct = default);

    Task AddAsync(User user, CancellationToken ct = default);

    /// <summary>
    /// Persists pending changes to entities this repository handed out. There is no
    /// <c>Update(user)</c>: the entity is tracked, so mutating it and saving is the whole of it.
    /// </summary>
    Task SaveChangesAsync(CancellationToken ct = default);

    /// <summary>
    /// Throws away the in-memory changes to this entity, restoring it to what the database holds.
    ///
    /// Needed after a rejected save: the entity is still tracked and still carries the values the
    /// database refused, and the audit write that follows runs on the same context — so its
    /// <see cref="SaveChangesAsync"/> would flush them. Discarding closes that in code rather
    /// than relying on nobody ever saving again.
    /// </summary>
    Task DiscardChangesAsync(User user, CancellationToken ct = default);
}
