using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using TrustCabinet.Domain.Entities;
using TrustCabinet.Infrastructure.Email;

namespace TrustCabinet.Web.Tests.Email;

/// <summary>
/// UC-07's <c>:Email Service</c>: what the email carries, and that every way of failing to send it
/// becomes the MSG15 branch rather than an exception.
/// </summary>
public sealed class PasswordResetEmailSenderTests
{
    private const string Token = "AbCdEf0123456789_-AbCdEf0123456789_-AbCdEfG";

    private readonly RecordingEmailSender _smtp = new();

    private PasswordResetEmailSender NewSender(bool enabled = true, int timeoutSeconds = 5) => new(
        _smtp,
        Options.Create(new EmailDeliveryOptions
        {
            ApplicationBaseUrl = "https://trustcabinet.example/",
            SendAttemptTimeoutSeconds = timeoutSeconds,
            Smtp = new SmtpOptions { Enabled = enabled, Host = "smtp.example", FromAddress = "no-reply@trustcabinet.example" },
        }),
        NullLogger<PasswordResetEmailSender>.Instance);

    [Fact]
    public async Task Sends_the_link_to_the_registered_address()
    {
        var sent = await NewSender().SendAsync(
            "thanhvien@trustcabinet.local", "Lê Thành Viên", Token, TimeSpan.FromMinutes(60));

        Assert.True(sent);
        var message = Assert.Single(_smtp.Sent);
        Assert.Equal("thanhvien@trustcabinet.local", message.ToAddress);
        Assert.Contains("Lê Thành Viên", message.Body);
        Assert.Contains("https://trustcabinet.example/reset-password#token=" + Token, message.Body);
        Assert.Contains("60 phút", message.Body);
    }

    [Fact]
    public void The_token_travels_in_the_fragment_not_the_query_string()
    {
        // A fragment never reaches a server, an access log or a Referer header.
        var link = PasswordResetEmailSender.BuildLink("https://trustcabinet.example", Token);

        Assert.Equal("https://trustcabinet.example/reset-password#token=" + Token, link);
        Assert.DoesNotContain("?", link);
    }

    [Fact]
    public async Task Disabled_email_delivery_is_a_failed_send()
    {
        var sent = await NewSender(enabled: false).SendAsync(
            "thanhvien@trustcabinet.local", "Lê Thành Viên", Token, TimeSpan.FromMinutes(60));

        Assert.False(sent);
        Assert.Empty(_smtp.Sent);
    }

    [Fact]
    public async Task A_server_error_is_a_failed_send_not_an_exception()
    {
        _smtp.Throws = new IOException("Connection refused");

        var sent = await NewSender().SendAsync(
            "thanhvien@trustcabinet.local", "Lê Thành Viên", Token, TimeSpan.FromMinutes(60));

        Assert.False(sent);
    }

    [Fact]
    public async Task A_server_that_never_answers_is_a_failed_send_after_the_timeout()
    {
        // A hung server must answer MSG15, not keep the administrator's request open forever.
        _smtp.Hangs = true;

        var sent = await NewSender(timeoutSeconds: 1).SendAsync(
            "thanhvien@trustcabinet.local", "Lê Thành Viên", Token, TimeSpan.FromMinutes(60));

        Assert.False(sent);
    }

    [Fact]
    public async Task A_cancelled_request_is_not_reported_as_a_failed_send()
    {
        // The administrator went away; there is nobody to show MSG15 to, and swallowing the
        // cancellation would hide it from the server.
        using var cancelled = new CancellationTokenSource();
        await cancelled.CancelAsync();
        _smtp.Throws = new OperationCanceledException(cancelled.Token);

        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => NewSender().SendAsync(
            "thanhvien@trustcabinet.local", "Lê Thành Viên", Token, TimeSpan.FromMinutes(60), cancelled.Token));
    }

    private sealed class RecordingEmailSender : IEmailSender
    {
        public Exception? Throws { get; set; }

        /// <summary>Never completes until cancelled, like a server that accepted the connection and went quiet.</summary>
        public bool Hangs { get; set; }

        public List<EmailMessage> Sent { get; } = [];

        public async Task SendAsync(EmailMessage message, CancellationToken ct = default)
        {
            if (Hangs)
            {
                await Task.Delay(Timeout.Infinite, ct);
            }

            if (Throws is { } exception)
            {
                throw exception;
            }

            Sent.Add(message);
        }
    }
}
