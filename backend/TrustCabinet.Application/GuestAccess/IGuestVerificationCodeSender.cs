namespace TrustCabinet.Application.GuestAccess;

/// <summary>
/// UC-04's one-time code, sent straight through the transport rather than through the email queue. The queue
/// stores the body, and this body carries a live credential that would then sit in the database long after
/// the code expired; and the "could not send" answer has to come back in the same request, which a queue
/// cannot do. See <see cref="Users.IPasswordResetEmailSender"/> for the same shape on UC-07.
/// </summary>
public interface IGuestVerificationCodeSender
{
    /// <summary>
    /// True once the mail server accepted the message. False when it could not be sent — email delivery
    /// switched off, the server unreachable or refusing — never an exception, because a failed send is one
    /// of the use case's expected branches.
    /// </summary>
    Task<bool> SendAsync(
        string toAddress,
        string invitedName,
        string meetingTitle,
        string code,
        TimeSpan validFor,
        CancellationToken ct = default);
}
