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
    /// Creates a reset token for the account and returns the raw token — the only time it exists
    /// outside the email it is about to be put in.
    ///
    /// The token does not work yet, and the account's current link keeps working: nothing changes
    /// until <see cref="ActivateAsync"/>. The email has to carry the token, so it exists before the
    /// email is sent — but superseding the old link at that point would kill a link already in the
    /// person's inbox whenever the new email then fails to go out.
    /// </summary>
    Task<string> CreateAsync(Guid userId, TimeSpan lifetime, CancellationToken ct = default);

    /// <summary>
    /// Makes a token from <see cref="CreateAsync"/> the account's one working link, superseding
    /// any earlier one. Called once the email carrying it has been sent. A token never activated
    /// stays unusable and expires on its own.
    /// </summary>
    Task ActivateAsync(Guid userId, string token, TimeSpan lifetime, CancellationToken ct = default);

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

    /// <summary>
    /// Kills whatever link the account has outstanding — when it is deactivated, or its email or
    /// role changes. Harmless when there is none.
    /// </summary>
    Task RevokeAsync(Guid userId, CancellationToken ct = default);
}
