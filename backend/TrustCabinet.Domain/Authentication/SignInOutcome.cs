namespace TrustCabinet.Domain.Authentication;

/// <summary>
/// Why a sign-in attempt ended the way it did — the four branches of UC-01's alt block, and
/// nothing else. The endpoint maps each to a message code (MSG01/MSG03/MSG04); the service only
/// decides which one happened.
///
/// Deliberately coarse on failure: <see cref="InvalidCredentials"/> covers both "no such email"
/// and "wrong password", because telling the two apart tells an attacker which emails exist
/// (SCRUM-11 acceptance criteria).
/// </summary>
public enum SignInOutcome
{
    /// <summary>Email unknown, or the password did not match. MSG01.</summary>
    InvalidCredentials,

    /// <summary>The account exists but was deactivated (SCRUM-13 deactivates, never deletes). MSG03.</summary>
    AccountDeactivated,

    /// <summary>
    /// Authenticated, but the account carries no way into the system at all. MSG04.
    /// See <see cref="Enums.SystemRole"/>: a plain Member has no capability by design and still
    /// signs in, so this fires only for a role that grants nothing and is not Member.
    /// </summary>
    NoSystemAccess,

    /// <summary>Credentials good, account usable. The only branch that issues tokens.</summary>
    Success,
}
