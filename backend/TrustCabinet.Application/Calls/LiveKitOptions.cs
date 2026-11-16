using System.Text;

namespace TrustCabinet.Application.Calls;

/// <summary>The <c>LiveKit</c> configuration section. The secret only ever comes from user-secrets or the environment.</summary>
public sealed class LiveKitOptions
{
    public const string SectionName = "LiveKit";

    /// <summary>What the browser connects to — ws:// or wss://.</summary>
    public string PublicUrl { get; set; } = "";

    /// <summary>What the backend calls the server API on — http:// or https://, same port.</summary>
    public string ApiUrl { get; set; } = "";

    public string ApiKey { get; set; } = "";

    public string ApiSecret { get; set; } = "";

    public int TokenTtlMinutes { get; set; } = 10;

    /// <summary>Remove room members who are not allowed in. Off only to load test with synthetic identities.</summary>
    public bool EnforceRoster { get; set; } = true;

    /// <summary>The SDK refuses a secret shorter than 32 bytes, so a short one counts as not configured.</summary>
    public bool IsConfigured =>
        PublicUrl.Length > 0 && ApiUrl.Length > 0 && ApiKey.Length > 0
        && Encoding.UTF8.GetByteCount(ApiSecret) >= 32;
}
