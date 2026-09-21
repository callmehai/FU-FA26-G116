namespace TrustCabinet.Domain.Authentication;

/// <summary>
/// Verifies the Google ID token the browser obtained, and reports who Google says the holder is.
///
/// Behind an interface for the same reason as the password hasher: the four branches of UC-02 are
/// what unit tests need to cover, and they should not require a network call to Google or a real
/// signed token to do it.
/// </summary>
public interface IGoogleTokenVerifier
{
    /// <summary>
    /// The verified identity, or null when the token is not acceptable for any reason — absent,
    /// malformed, expired, signed by a key that is not Google's, or issued for a different
    /// application.
    ///
    /// Implementations must never trust the token's contents without checking its signature,
    /// issuer, audience and expiry. An unverified ID token is just a string the caller supplied,
    /// and its `sub` claim can say anything.
    /// </summary>
    Task<GoogleIdentity?> VerifyAsync(string idToken, CancellationToken ct = default);
}

/// <summary>
/// What a verified Google ID token establishes. Only the fields this system actually uses.
/// </summary>
/// <param name="Subject">
/// Google's `sub` claim — stable and unique per Google account for its lifetime. This is what an
/// internal account is linked by; see <see cref="Entities.User.GoogleSubjectId"/>.
/// </param>
/// <param name="Email">
/// The account's email address, for logging and for an administrator linking an account by hand.
/// Never used to decide who may sign in: an email can change or be reassigned, the subject cannot.
/// </param>
public sealed record GoogleIdentity(string Subject, string Email);
