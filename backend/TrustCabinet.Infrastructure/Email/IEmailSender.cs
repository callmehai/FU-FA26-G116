using TrustCabinet.Domain.Entities;

namespace TrustCabinet.Infrastructure.Email;

internal interface IEmailSender
{
    /// <summary>Hands the message to the transport and returns the provider's id for it, when the transport has one.</summary>
    Task<string?> SendAsync(EmailMessage message, CancellationToken ct = default);
}

/// <summary>
/// The provider refused the message itself (an invalid address or field), so sending it again would be refused the same
/// way. Anything else — a timeout, a 5xx, a rate limit, a bad key — is worth retrying once it is fixed.
/// </summary>
internal sealed class PermanentEmailFailureException(string message) : Exception(message);
