namespace TrustCabinet.Domain.Authentication;

/// <summary>
/// Why a sign-in attempt ended the way it did — the branches of UC-01's alt block, and nothing
/// else. The endpoint maps each to a message code (MSG01/MSG03); the service only decides which
/// one happened.
///
/// Deliberately coarse on failure: <see cref="InvalidCredentials"/> covers both "no such email"
/// and "wrong password", because telling the two apart tells an attacker which emails exist
/// (SCRUM-11 acceptance criteria).
///
/// There is no "no system access" (MSG04) any more. It fired for a role with no capabilities that
/// was not Member; since roles became data (ADR-0008, SCRUM-63) a role without capabilities is a
/// legitimate configuration that works exactly like Member, so the branch would lock out people who
/// did nothing wrong. Pending BA confirmation that UC-01 drops MSG04 — see
/// docs/03-api/04-quan-ly-vai-tro.md.
/// </summary>
public enum SignInOutcome
{
    /// <summary>Email unknown, or the password did not match. MSG01.</summary>
    InvalidCredentials,

    /// <summary>The account exists but was deactivated (SCRUM-13 deactivates, never deletes). MSG03.</summary>
    AccountDeactivated,

    /// <summary>Credentials good, account usable. The only branch that issues tokens.</summary>
    Success,
}
