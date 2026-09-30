using System.Text;
using MailKit.Security;
using Microsoft.Extensions.Options;
using MimeKit;
using TrustCabinet.Domain.Entities;

namespace TrustCabinet.Infrastructure.Email;

internal sealed class SmtpEmailSender(IOptions<EmailDeliveryOptions> options) : IEmailSender
{
    public async Task SendAsync(EmailMessage queued, CancellationToken ct = default)
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
        await smtp.DisconnectAsync(quit: true, ct);
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
