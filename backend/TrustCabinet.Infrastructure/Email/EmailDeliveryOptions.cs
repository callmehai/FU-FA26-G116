namespace TrustCabinet.Infrastructure.Email;

public sealed class EmailDeliveryOptions
{
    public const string SectionName = "Email";

    /// <summary>Public frontend origin used to build links inside invitations.</summary>
    public string ApplicationBaseUrl { get; set; } = string.Empty;

    public int MaxAttempts { get; set; } = 8;

    public int RetryDelaySeconds { get; set; } = 60;

    public int PollingIntervalSeconds { get; set; } = 5;

    public int ProcessingLeaseSeconds { get; set; } = 120;

    /// <summary>Upper bound for one complete SMTP attempt, including connect and disconnect.</summary>
    public int SendAttemptTimeoutSeconds { get; set; } = 60;

    /// <summary>
    /// Recipient domains that never receive mail, such as the demo accounts' <c>trustcabinet.local</c>. A message to
    /// one fails at once instead of reaching the SMTP server, which would only bounce it back to the sending mailbox.
    /// </summary>
    public string[] UndeliverableDomains { get; set; } = [];

    /// <summary>Which transport sends the mail; SMTP stays for local development (ADR-0031).</summary>
    public EmailProvider Provider { get; set; } = EmailProvider.Smtp;

    public SmtpOptions Smtp { get; set; } = new();

    public ResendOptions Resend { get; set; } = new();

    /// <summary>Whether the chosen transport is switched on. Off, nothing is sent and invitations wait.</summary>
    public bool Enabled => Provider switch
    {
        EmailProvider.Smtp => Smtp.Enabled,
        EmailProvider.Resend => Resend.Enabled,
        _ => false,
    };

    /// <summary>The sender of the chosen transport, which a calendar invitation names as its organizer.</summary>
    public string FromAddress => Provider == EmailProvider.Resend ? Resend.FromAddress : Smtp.FromAddress;

    public string FromName => Provider == EmailProvider.Resend ? Resend.FromName : Smtp.FromName;

    public bool IsUndeliverable(string address)
    {
        var at = address.LastIndexOf('@');
        if (at < 0) return false;
        var domain = address[(at + 1)..].Trim().TrimEnd('.');
        return UndeliverableDomains.Any(blocked =>
            string.Equals(domain, blocked.Trim().TrimEnd('.'), StringComparison.OrdinalIgnoreCase));
    }
}

public enum EmailProvider
{
    Smtp,
    Resend,
}

public enum SmtpSecurityMode
{
    None,
    StartTls,
    SslOnConnect,
}

public sealed class SmtpOptions
{
    public bool Enabled { get; set; }

    public string Host { get; set; } = string.Empty;

    public int Port { get; set; } = 587;

    public SmtpSecurityMode Security { get; set; } = SmtpSecurityMode.StartTls;

    public string Username { get; set; } = string.Empty;

    public string Password { get; set; } = string.Empty;

    public string FromAddress { get; set; } = string.Empty;

    public string FromName { get; set; } = "TrustCabinet";

    public int TimeoutSeconds { get; set; } = 30;
}

/// <summary>Resend's HTTP API (ADR-0031). The key and the webhook secret are secrets: environment variables only.</summary>
public sealed class ResendOptions
{
    public bool Enabled { get; set; }

    public string ApiUrl { get; set; } = "https://api.resend.com/";

    /// <summary>A sending-only key, restricted to the verified domain.</summary>
    public string ApiKey { get; set; } = string.Empty;

    /// <summary>An address on the domain verified in Resend.</summary>
    public string FromAddress { get; set; } = string.Empty;

    public string FromName { get; set; } = "TrustCabinet";

    /// <summary>
    /// The <c>whsec_…</c> signing secret of the webhook that reports delivery. Empty, the webhook answers 503 and
    /// messages stay at <see cref="TrustCabinet.Domain.Enums.EmailStatus.Sent"/> with no delivery state.
    /// </summary>
    public string WebhookSecret { get; set; } = string.Empty;
}
