namespace TrustCabinet.Domain.Authentication;

/// <summary>
/// Why a Google sign-in ended the way it did — the branches of UC-02's inner alt block.
///
/// Separate from <see cref="SignInOutcome"/> rather than shared, because the branches differ:
/// UC-02 has an invalid Google token and an unlinked identity, UC-01 has wrong credentials. The
/// one situation they share, a deactivated account, answers the same code (MSG02) on both.
///
/// No "no system access" branch: dropped with UC-01's for the same reason — see
/// <see cref="SignInOutcome"/>.
/// </summary>
public enum GoogleSignInOutcome
{
    /// <summary>
    /// The ID token did not verify — missing, malformed, expired, signed by someone else, or
    /// issued for another application. UC-02's outer alt treats a cancelled Google sign-in the
    /// same way, except that case never reaches the backend at all.
    /// MSG03. The catalogue's MSG04 ("unable to verify the Google account") is not emitted: the
    /// verifier cannot tell that case apart from this one.
    /// </summary>
    InvalidGoogleToken,

    /// <summary>
    /// Google verified the person, but no internal account is linked to that Google identity.
    /// Not an invitation to create one: accounts are provisioned by an administrator, so this is
    /// "you are not one of ours". MSG05.
    /// </summary>
    NotLinked,

    /// <summary>The linked account exists but was deactivated (SCRUM-13). MSG02.</summary>
    AccountDeactivated,

    /// <summary>Verified, linked, active. The only branch that issues tokens.</summary>
    Success,
}
