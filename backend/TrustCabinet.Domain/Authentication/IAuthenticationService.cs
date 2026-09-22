namespace TrustCabinet.Domain.Authentication;

/// <summary>
/// The <c>:Authentication Service</c> participant of UC-01. Owns the decision of whether someone
/// may in, and the issuing of tokens once they may.
/// </summary>
public interface IAuthenticationService
{
    /// <summary>
    /// Verifies credentials and, on success, issues an access/refresh token pair.
    /// Returns which of UC-01's four branches happened; it never throws for a failed attempt,
    /// because a rejected sign-in is an expected outcome rather than an error.
    /// </summary>
    Task<SignInResult> SignInAsync(string email, string password, CancellationToken ct = default);

    /// <summary>
    /// UC-02: verifies a Google ID token, finds the internal account linked to that Google
    /// identity, and issues the same token pair as a password sign-in.
    ///
    /// Never creates an account. A Google identity nobody has linked is refused
    /// (<see cref="GoogleSignInOutcome.NotLinked"/>) — accounts are provisioned by an
    /// administrator, and self-registration would let anyone with a Google account in.
    /// </summary>
    Task<GoogleSignInResult> SignInWithGoogleAsync(string idToken, CancellationToken ct = default);

    /// <summary>
    /// Exchanges a valid refresh token for a new pair, re-reading the account each time so a
    /// deactivation or a role change takes effect at the next refresh instead of lingering for as
    /// long as someone keeps refreshing.
    /// </summary>
    Task<SignInResult> RefreshAsync(string refreshToken, CancellationToken ct = default);

    /// <summary>Destroys the session server-side by dropping its refresh token.</summary>
    Task SignOutAsync(string refreshToken, CancellationToken ct = default);
}
