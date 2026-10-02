namespace TrustCabinet.Domain.Authentication;

/// <summary>
/// UC-08's "secure password reset request" — the <c>:Authentication Service</c> step "Create
/// password reset request" in the sequence diagram.
///
/// A reset link is a credential: whoever holds it can choose the account's password. So it gets
/// the same treatment as a refresh token — an opaque random string, stored only as a hash, valid
/// for a short time and spendable once.
///
/// One live link per account. Issuing a new one supersedes the previous one, so an administrator
/// pressing "Reset Password" twice leaves exactly one working link in the mailbox, and a link
/// spent on a successful reset cannot be followed by an older one still sitting in the inbox.
/// </summary>
public interface IPasswordResetTokenStore
{
    /// <summary>
    /// Creates a reset token for the account, superseding any earlier one, and returns the raw
    /// token — the only time it exists outside the email it is about to be put in.
    /// </summary>
    Task<string> CreateAsync(Guid userId, TimeSpan lifetime, CancellationToken ct = default);

    /// <summary>
    /// The account a live, current token belongs to, or null — without spending it. Lets the reset
    /// reject a bad new password without burning the link the person will retry with.
    /// </summary>
    Task<Guid?> PeekAsync(string token, CancellationToken ct = default);

    /// <summary>
    /// Spends the token, returning its account, or null when it is unknown, expired, already used
    /// or superseded. Single-use: of two simultaneous submissions only one gets an account back.
    /// </summary>
    Task<Guid?> ConsumeAsync(string token, CancellationToken ct = default);
}
