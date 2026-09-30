using TrustCabinet.Domain.Entities;

namespace TrustCabinet.Infrastructure.Email;

internal interface IEmailSender
{
    Task SendAsync(EmailMessage message, CancellationToken ct = default);
}
