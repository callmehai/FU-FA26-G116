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

    public SmtpOptions Smtp { get; set; } = new();

    public bool IsUndeliverable(string address)
    {
        var at = address.LastIndexOf('@');
        if (at < 0) return false;
        var domain = address[(at + 1)..].Trim().TrimEnd('.');
        return UndeliverableDomains.Any(blocked =>
            string.Equals(domain, blocked.Trim().TrimEnd('.'), StringComparison.OrdinalIgnoreCase));
    }
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
