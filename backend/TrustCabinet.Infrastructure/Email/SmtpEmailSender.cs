using System.Text;
using MailKit.Security;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using MimeKit;
using TrustCabinet.Domain.Entities;

namespace TrustCabinet.Infrastructure.Email;

internal sealed class SmtpEmailSender(
    IOptions<EmailDeliveryOptions> options,
    ILogger<SmtpEmailSender> logger) : IEmailSender
{
    public async Task<string?> SendAsync(EmailMessage queued, CancellationToken ct = default)
    {
        var smtpOptions = options.Value.Smtp;
        var message = BuildMessage(queued, smtpOptions);

        using var smtp = new MailKit.Net.Smtp.SmtpClient
        {
            Timeout = checked(smtpOptions.TimeoutSeconds * 1000),
        };

        await smtp.ConnectAsync(
            smtpOptions.Host,
            smtpOptions.Port,
            ToSecureSocketOptions(smtpOptions.Security),
            ct);

        if (!string.IsNullOrWhiteSpace(smtpOptions.Username))
        {
            await smtp.AuthenticateAsync(smtpOptions.Username, smtpOptions.Password, ct);
        }

        await smtp.SendAsync(message, ct);

        // The server has accepted the message; a failed QUIT does not unsend it. Reported as a
        // failure, it would make the queue send the invitation again and UC-07 answer MSG15 for a
        // reset link that is already in the inbox. Disposing the client closes the socket anyway.
        try
        {
            await smtp.DisconnectAsync(quit: true, ct);
        }
        catch (Exception exception)
        {
            // Warning, not error: nothing was lost. Kept so a server that keeps dropping the
            // connection at QUIT can still be noticed.
            logger.LogWarning(
                exception, "The SMTP server accepted message {MessageId} but QUIT failed.", queued.Id);
        }

        // SMTP hands back no id that a later report could be matched on.
        return null;
    }

    internal static MimeMessage BuildMessage(EmailMessage queued, SmtpOptions options)
    {
        var from = new MailboxAddress(options.FromName, options.FromAddress);
        var domainSeparator = from.Address.LastIndexOf('@');
        var messageIdDomain = from.Address[(domainSeparator + 1)..];
        var message = new MimeMessage
        {
            MessageId = $"{queued.Id:N}@{messageIdDomain}",
            Subject = queued.Subject,
        };
        message.From.Add(from);
        message.To.Add(MailboxAddress.Parse(queued.ToAddress));

        var body = new BodyBuilder { TextBody = queued.Body };
        if (!string.IsNullOrEmpty(queued.CalendarAttachment))
        {
            var calendar = new MimePart("text", "calendar")
            {
                Content = new MimeContent(
                    new MemoryStream(Encoding.UTF8.GetBytes(queued.CalendarAttachment), writable: false)),
                ContentDisposition = new ContentDisposition(ContentDisposition.Attachment),
                ContentTransferEncoding = ContentEncoding.Base64,
                FileName = "thu-moi-hop.ics",
            };
            calendar.ContentType.Charset = Encoding.UTF8.WebName;
            calendar.ContentType.Parameters.Add("method", "REQUEST");
            body.Attachments.Add(calendar);
        }

        message.Body = body.ToMessageBody();
        return message;
    }

    private static SecureSocketOptions ToSecureSocketOptions(SmtpSecurityMode mode)
        => mode switch
        {
            SmtpSecurityMode.None => SecureSocketOptions.None,
            SmtpSecurityMode.StartTls => SecureSocketOptions.StartTls,
            SmtpSecurityMode.SslOnConnect => SecureSocketOptions.SslOnConnect,
            _ => throw new InvalidOperationException($"Unsupported SMTP security mode: {mode}."),
        };
}
