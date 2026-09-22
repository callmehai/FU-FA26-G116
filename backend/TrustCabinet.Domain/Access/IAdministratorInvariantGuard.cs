namespace TrustCabinet.Domain.Access;

/// <summary>
/// Serialises every write that could leave the organisation with no active account able to
/// administer users.
///
/// The rule — "at least one active account holds <c>AdminUsers</c>" — is decided by two tables at
/// once: <c>users</c> says who is active and what role they hold, <c>role_capabilities</c> says
/// which roles carry the capability. So it belongs to neither table, and locking either one alone
/// leaves the other side free to invalidate the decision between the check and the commit:
///
/// <code>
/// Matrix: Admin → {AdminUsers}, Secretary → {AdminUsers}
/// Alice = Admin (active), Bob = Secretary (active)
///
/// A: deactivate Alice            → Bob still administers  → allowed
/// B: remove AdminUsers from      → Alice still administers → allowed
///    Secretary
///
/// both commit → nobody can administer
/// </code>
///
/// Each request on its own is legal; only together are they not. A lock on
/// <c>role_capabilities</c> does not stop A, which never touches that table, and a lock on
/// <c>users</c> does not stop B.
///
/// The fix is for both sides to queue behind the same name, which is what a PostgreSQL advisory
/// lock is for — a lock on an agreed label rather than on rows. Held to the end of the
/// transaction, so the check and the write it authorises cannot be split apart.
///
/// Implemented in Infrastructure, but declared here because the rule is a domain rule; the
/// services that depend on it are what make it hold.
/// </summary>
public interface IAdministratorInvariantGuard
{
    /// <summary>
    /// Runs <paramref name="write"/> in a transaction, holding the shared lock for its whole
    /// length, and commits when it returns.
    ///
    /// Everything that reads the invariant and then acts on it has to be inside — a caller that
    /// checks first and calls this afterwards has re-opened the same window.
    /// </summary>
    Task<T> WriteAsync<T>(Func<CancellationToken, Task<T>> write, CancellationToken ct = default);
}
