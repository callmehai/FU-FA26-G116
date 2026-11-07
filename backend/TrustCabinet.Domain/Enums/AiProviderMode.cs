namespace TrustCabinet.Domain.Enums;

/// <summary>Which model writes the AI draft of an ordinary meeting (ADR-0056).</summary>
public enum AiProviderMode
{
    /// <summary>The in-house model the server is configured with (ADR-0054).</summary>
    Local,

    /// <summary>The API an administrator set up; only for meetings of <see cref="Sensitivity.Normal"/>.</summary>
    Api,
}
