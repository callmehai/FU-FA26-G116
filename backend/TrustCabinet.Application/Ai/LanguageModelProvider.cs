using System.Security.Cryptography;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using TrustCabinet.Application.Abstractions;
using TrustCabinet.Application.Summaries;
using TrustCabinet.Domain.Enums;

namespace TrustCabinet.Application.Ai;

/// <summary>
/// ADR-0056 — the API an administrator chose writes the drafts of ordinary meetings; every other meeting, and every
/// meeting while the in-house model is chosen, stays on the in-house model (ADR-0054). The transcript of a meeting
/// marked Internal or Secret never leaves the server's own network.
/// </summary>
public sealed class LanguageModelProvider(IAiProviderSettingRepository settings, IUnitOfWork unitOfWork, ILanguageModel local,
    IAiKeyProtector protector, IApiLanguageModelFactory apis, IOptions<LanguageModelOptions> options,
    ILogger<LanguageModelProvider> logger)
    : ILanguageModelProvider
{
    /// <summary>The named HTTP client the infrastructure project calls the API through.</summary>
    public const string ApiHttpClientName = "ai-api";

    public async Task<ILanguageModel> ForAsync(Sensitivity sensitivity, CancellationToken ct)
    {
        if (sensitivity != Sensitivity.Normal) return local;
        var setting = await unitOfWork.ExecuteAsync(settings.FindCurrentAsync, TransactionMode.None, ct);
        if (setting is not { Mode: AiProviderMode.Api, ApiKeyCiphertext: { } ciphertext }) return local;
        string key;
        try
        {
            key = protector.Unprotect(ciphertext, setting.Id);
        }
        catch (CryptographicException error)
        {
            // The encryption key changed since it was stored: nothing is sent anywhere until the administrator enters it again.
            logger.LogWarning(error, "The stored AI API key cannot be decrypted; using the in-house model");
            return local;
        }
        return Api(setting.ApiBaseUrl, setting.ApiModel, key, options.Value.TimeoutMinutes, options.Value.ApiMaxTokens,
            options.Value.ApiReasoningEffort);
    }

    internal ILanguageModel Api(string baseUrl, string model, string key, int timeoutMinutes, int maxTokens,
        string reasoningEffort)
        => apis.Create(baseUrl, model, key, timeoutMinutes, maxTokens, reasoningEffort);
}
