using TrustCabinet.Domain.Entities.Common;
using TrustCabinet.Domain.Enums;

namespace TrustCabinet.Domain.Entities;

/// <summary>
/// Where the AI draft of a meeting is written (ADR-0056): the in-house model the server is configured with
/// (<c>Ai:BaseUrl</c>, ADR-0054), or an OpenAI-compatible API an administrator set up here. One row at most; without it
/// the in-house model is used.
/// <para>
/// The API sees the transcript, so only a meeting of <see cref="Sensitivity.Normal"/> ever goes to it; every other one
/// stays on the in-house model whatever this row says.
/// </para>
/// </summary>
public class AiProviderSetting : Entity
{
    public AiProviderMode Mode { get; set; } = AiProviderMode.Local;

    /// <summary>The API's base URL, e.g. https://opencode.ai/zen/v1; <c>/chat/completions</c> is appended.</summary>
    public string ApiBaseUrl { get; set; } = string.Empty;

    /// <summary>The model name the API knows, e.g. deepseek-v4.1-flash.</summary>
    public string ApiModel { get; set; } = string.Empty;

    /// <summary>
    /// The API key, encrypted (AES-256-GCM, a key derived for this purpose only). Never sent to a browser, never
    /// logged, never in the audit log; null when none was given.
    /// </summary>
    public byte[]? ApiKeyCiphertext { get; set; }

    /// <summary>The key's last four characters, so the administrator can tell which key is stored.</summary>
    public string ApiKeyHint { get; set; } = string.Empty;

    public Guid? UpdatedById { get; set; }
    public User? UpdatedBy { get; set; }
}
