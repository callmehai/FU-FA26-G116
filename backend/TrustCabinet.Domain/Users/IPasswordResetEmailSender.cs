namespace TrustCabinet.Domain.Users;

/// <summary>
/// UC-08's <c>:Email Service</c> — delivers the reset link to the account's registered address.
///
/// Sends while the administrator waits rather than through the <c>email_messages</c> queue. The
/// queue stores the body, and this body carries a live credential that would then sit in the
/// database long after the link expired; and UC-08's MSG15 ("unable to send") has to be answered
/// in the same request, which a queue cannot do.
/// </summary>
public interface IPasswordResetEmailSender
{
    /// <summary>
    /// True once the mail server accepted the message. False when it could not be sent — email
    /// delivery switched off, the server unreachable or refusing — never an exception, because a
    /// failed send is one of the use case's expected branches.
    /// </summary>
    Task<bool> SendAsync(
        string toAddress,
        string fullName,
        string resetToken,
        TimeSpan validFor,
        CancellationToken ct = default);
}
