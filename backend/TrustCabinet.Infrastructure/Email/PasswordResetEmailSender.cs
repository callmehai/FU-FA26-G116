using MailKit.Net.Smtp;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using TrustCabinet.Domain.Entities;
using TrustCabinet.Domain.Users;

namespace TrustCabinet.Infrastructure.Email;

/// <summary>
/// UC-07's <c>:Email Service</c>. Sends straight through SMTP rather than the queue — see
/// <see cref="IPasswordResetEmailSender"/> for why.
/// </summary>
internal sealed class PasswordResetEmailSender(
    IEmailSender sender,
    IOptions<EmailDeliveryOptions> options,
    ILogger<PasswordResetEmailSender> logger) : IPasswordResetEmailSender
{
    public async Task<bool> SendAsync(
        string toAddress,
        string fullName,
        string resetToken,
        TimeSpan validFor,
        CancellationToken ct = default)
    {
        var emailOptions = options.Value;

        // Switched off means no mail can go out, which is EX-2 — not a success that silently sent
        // nothing.
        if (!emailOptions.Smtp.Enabled)
        {
            logger.LogWarning("Email delivery is disabled; the password reset email was not sent.");
            return false;
        }

        // Same bound as one attempt of the queue worker, so a hung server answers MSG15 instead of
        // keeping the administrator's request open indefinitely.
        using var timeout = CancellationTokenSource.CreateLinkedTokenSource(ct);
        timeout.CancelAfter(TimeSpan.FromSeconds(emailOptions.SendAttemptTimeoutSeconds));

        try
        {
            await sender.SendAsync(
                new EmailMessage
                {
                    // Never stored — the field is required by the type, and SmtpEmailSender builds
                    // the Message-ID from Id.
                    DeduplicationKey = "password-reset",
                    ToAddress = toAddress,
                    Subject = "Đặt lại mật khẩu TrustCabinet",
                    Body = BuildBody(fullName, BuildLink(emailOptions.ApplicationBaseUrl, resetToken), validFor),
                },
                timeout.Token);

            return true;
        }
        catch (SmtpCommandException exception) when (!ct.IsCancellationRequested)
        {
            // The server's reply usually quotes the recipient back, and the address is personal
            // data — so the status code, not the message. The caller logs which account this was.
            logger.LogError(
                "The password reset email was refused by the mail server: {StatusCode} ({ErrorCode}).",
                exception.StatusCode,
                exception.ErrorCode);
            return false;
        }
        catch (Exception exception) when (!ct.IsCancellationRequested)
        {
            // Connection, TLS, authentication or timeout — none of these carries the address.
            logger.LogError(exception, "The password reset email could not be sent.");
            return false;
        }
    }

    /// <summary>
    /// The token goes in the fragment, not the query string. A fragment never leaves the browser:
    /// it is not sent to the server, not written to access logs or proxies, and not passed on in a
    /// Referer header — places a query string would leave a live credential behind.
    /// </summary>
    internal static string BuildLink(string applicationBaseUrl, string resetToken)
        => $"{applicationBaseUrl.Trim().TrimEnd('/')}/reset-password#token={resetToken}";

    private static string BuildBody(string fullName, string link, TimeSpan validFor) => $"""
        Xin chào {fullName},

        Quản trị viên TrustCabinet vừa yêu cầu đặt lại mật khẩu cho tài khoản của bạn.

        Mở liên kết sau để đặt mật khẩu mới (hiệu lực trong {validFor.TotalMinutes:0} phút, chỉ dùng được một lần):
        {link}

        Mật khẩu hiện tại vẫn dùng được cho tới khi bạn đặt mật khẩu mới. Nếu bạn không chờ email này,
        hãy bỏ qua nó và báo cho quản trị viên.
        """;
}
