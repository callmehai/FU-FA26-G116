namespace TrustCabinet.Domain.Authentication;

/// <summary>
/// Why a Google sign-in ended the way it did — the branches of UC-02's inner alt block.
///
/// Separate from <see cref="SignInOutcome"/> rather than shared, because UC-02 numbers its
/// messages differently from UC-01 for the same situations: a deactivated account is MSG03 in
/// UC-01 but MSG04 here. Merging the two enums would mean one of the two use cases quietly getting
/// the other's codes.
///
/// No "no system access" (MSG05): dropped with UC-01's MSG04 for the same reason — see
/// <see cref="SignInOutcome"/>.
/// </summary>
public enum GoogleSignInOutcome
{
    /// <summary>
    /// The ID token did not verify — missing, malformed, expired, signed by someone else, or
    /// issued for another application. UC-02's outer alt treats a cancelled Google sign-in the
    /// same way, except that case never reaches the backend at all.
    /// MSG01 in UC-02's numbering.
    /// </summary>
    InvalidGoogleToken,

    /// <summary>
    /// Google verified the person, but no internal account is linked to that Google identity.
    /// Not an invitation to create one: accounts are provisioned by an administrator, so this is
    /// "you are not one of ours". MSG03.
    /// </summary>
    NotLinked,

    /// <summary>The linked account exists but was deactivated (SCRUM-13). MSG04.</summary>
    AccountDeactivated,

    /// <summary>Verified, linked, active. The only branch that issues tokens.</summary>
    Success,
}
