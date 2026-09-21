using TrustCabinet.Domain.Entities;

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
    Task<IReadOnlyCollection<User>> ListAsync(CancellationToken ct = default);

    Task<User?> FindByIdAsync(Guid id, CancellationToken ct = default);

    /// <summary>
    /// Whether any account other than <paramref name="exceptId"/> already uses this email —
    /// UC-05's "Check Email availability", and the same check on UC-06's edit, where the account
    /// keeping its own email is not a duplicate.
    ///
    /// The unique index is what actually guarantees it; this is the check that produces MSG08
    /// instead of a 500. See <c>UserService.CreateAsync</c> for why both are needed.
    /// </summary>
    Task<bool> EmailExistsAsync(string email, Guid? exceptId = null, CancellationToken ct = default);

    Task AddAsync(User user, CancellationToken ct = default);

    /// <summary>
    /// Persists pending changes to entities this repository handed out. There is no
    /// <c>Update(user)</c>: the entity is tracked, so mutating it and saving is the whole of it.
    /// </summary>
    Task SaveChangesAsync(CancellationToken ct = default);
}
