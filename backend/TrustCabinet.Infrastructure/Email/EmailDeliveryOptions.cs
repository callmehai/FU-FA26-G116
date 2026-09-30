namespace TrustCabinet.Infrastructure.Email;

public sealed class EmailDeliveryOptions
{
    public const string SectionName = "Email";

    /// <summary>Public frontend origin used to build links inside invitations.</summary>
    public string ApplicationBaseUrl { get; set; } = string.Empty;

    public int MaxAttempts { get; set; } = 3;

    public int RetryDelaySeconds { get; set; } = 5;

    public int PollingIntervalSeconds { get; set; } = 5;

    public int ProcessingLeaseSeconds { get; set; } = 120;

    public SmtpOptions Smtp { get; set; } = new();
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
