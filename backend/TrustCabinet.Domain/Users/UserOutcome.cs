namespace TrustCabinet.Domain.Users;

/// <summary>
/// Why a user-management operation ended the way it did — the alt branches of UC-06a, UC-06b and
/// UC-06c, and nothing else. The endpoint maps each to a message code and a status code; the
/// service only decides which one happened.
///
/// One enum for the three use cases because they share their branches: all three can fail
/// validation, and all three answer "that account does not exist" the same way. UC-01 and UC-02
/// each got their own enum only because their specifications number the same situations
/// differently; here they do not.
/// </summary>
public enum UserOutcome
{
    /// <summary>A field is missing, malformed or too long. Carries the offending fields.</summary>
    InvalidInformation,

    /// <summary>UC-06a's second branch — the email is already taken. MSG08.</summary>
    DuplicateEmail,

    /// <summary>
    /// No account with that id. Not in any diagram's alt block: the diagrams assume the
    /// administrator picked a row off a list that was just rendered. It happens anyway, when two
    /// administrators work at once or a stale tab posts an id that has since gone.
    /// </summary>
    NotFound,

    /// <summary>
    /// UC-06c's "Status change is not allowed". The account is already in the requested state, or
    /// the administrator is aiming at their own account — see
    /// <c>IUserService.ChangeStatusAsync</c>.
    /// </summary>
    StatusChangeNotAllowed,

    /// <summary>
    /// UC-07's EX-1 — the account cannot receive a reset link: it is deactivated, or it has no
    /// password to reset (Google sign-in only). MSG14.
    /// </summary>
    PasswordResetNotAvailable,

    /// <summary>UC-07's EX-2 — the reset email could not be sent. MSG15.</summary>
    PasswordResetEmailFailed,

    /// <summary>
    /// Completing UC-07 — the reset link is unknown, expired, already used, superseded by a newer
    /// one, or its account was deactivated since. One outcome for all of them: telling them apart
    /// would only help someone probing links.
    /// </summary>
    PasswordResetLinkInvalid,

    /// <summary>The operation went through.</summary>
    Success,
}
