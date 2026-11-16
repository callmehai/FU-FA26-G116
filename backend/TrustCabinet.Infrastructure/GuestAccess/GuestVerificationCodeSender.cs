using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using TrustCabinet.Application.Email;
using TrustCabinet.Application.GuestAccess;
using TrustCabinet.Domain.Entities;
using TrustCabinet.Infrastructure.Email;

namespace TrustCabinet.Infrastructure.GuestAccess;

/// <summary>
/// UC-04's one-time code, sent straight through the transport: the only copy at rest is the hash.
/// See <see cref="IGuestVerificationCodeSender"/> for why it never goes through the email queue.
/// </summary>
internal sealed class GuestVerificationCodeSender(
    IEmailSender sender,
    IOptions<EmailDeliveryOptions> options,
    ILogger<GuestVerificationCodeSender> logger) : IGuestVerificationCodeSender
{
    public Task<bool> SendAsync(
        string toAddress,
        string invitedName,
        string meetingTitle,
        string code,
        TimeSpan validFor,
        CancellationToken ct = default)
        => PasswordResetEmailSender.SendNowAsync(sender, options.Value, logger, new EmailMessage
        {
            // Never stored — the field is required by the type.
            DeduplicationKey = "guest-verification",
            ToAddress = toAddress,
            Subject = "Mã xác minh truy cập cuộc họp TrustCabinet",
            Body = $"""
                Xin chào {invitedName},

                Mã xác minh để vào cuộc họp "{meetingTitle}" là: {code}

                Mã có hiệu lực trong {validFor.TotalMinutes:0} phút và chỉ dùng được một lần. Nếu bạn không yêu cầu mã này, hãy bỏ qua email.
                """,
        }, "guest verification", ct);
}
