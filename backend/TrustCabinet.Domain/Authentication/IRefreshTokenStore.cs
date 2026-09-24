namespace TrustCabinet.Domain.Authentication;

/// <summary>
/// Server-side home of the refresh tokens.
///
/// The access token is a self-contained JWT: fast to check, impossible to revoke before it
/// expires — which is why it is short-lived. The refresh token is the opposite: an opaque random
/// string that means nothing on its own and is only valid while this store still holds it. That
/// is what makes "logout destroys the session server-side, not just in the browser" (SCRUM-11)
/// true rather than a claim.
///
/// Implementations store a hash of the token, never the token itself: a dump of the store must not
/// be enough to impersonate anybody.
/// </summary>
public interface IRefreshTokenStore
{
    /// <summary>Records an issued token and the account it belongs to, expiring after <paramref name="lifetime"/>.</summary>
    Task StoreAsync(string refreshToken, Guid userId, TimeSpan lifetime, CancellationToken ct = default);

    /// <summary>
    /// The account a live token belongs to, or null — without consuming it.
    ///
    /// Lets a refresh do everything that can fail (reading the account from the database) before
    /// it spends the token. A token consumed by a refresh that then fails is left in the browser as
    /// a spent token, which the next attempt presents and <see cref="ConsumeAsync"/> reads as
    /// theft. Not a validity check on its own: only <see cref="ConsumeAsync"/> decides.
    /// </summary>
    Task<Guid?> PeekAsync(string refreshToken, CancellationToken ct = default);

    /// <summary>
    /// Consumes the token, returning the account it belonged to, or null if it is unknown,
    /// expired or already used.
    ///
    /// Single-use on purpose (rotation): each refresh invalidates the token it was called with, so
    /// a stolen refresh token stops working as soon as the legitimate client next refreshes.
    ///
    /// Presenting an already-used token is treated as evidence of theft: besides returning null,
    /// it revokes every session of the account the token belonged to — unless it was spent only
    /// moments ago, which is an interrupted refresh rather than a theft.
    /// </summary>
    Task<Guid?> ConsumeAsync(string refreshToken, CancellationToken ct = default);

    /// <summary>
    /// Drops the token without issuing anything, returning the account it belonged to, or null if
    /// it was already gone. Sign-out; harmless if already gone.
    /// </summary>
    Task<Guid?> RevokeAsync(string refreshToken, CancellationToken ct = default);

    /// <summary>
    /// Drops every refresh token belonging to an account — "sign me out everywhere", and what an
    /// administrator triggers when deactivating someone.
    /// </summary>
    Task RevokeAllForUserAsync(Guid userId, CancellationToken ct = default);
}
