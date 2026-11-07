using System.Security.Cryptography;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using TrustCabinet.Domain.Ai;
using TrustCabinet.Domain.Enums;
using TrustCabinet.Domain.Summaries;
using TrustCabinet.Infrastructure.Summaries;

namespace TrustCabinet.Infrastructure.Ai;

/// <summary>
/// ADR-0056 — the API an administrator chose writes the drafts of ordinary meetings; every other meeting, and every
/// meeting while the in-house model is chosen, stays on the in-house model (ADR-0054). The transcript of a meeting
/// marked Internal or Secret never leaves the server's own network.
/// </summary>
public sealed class LanguageModelProvider(TrustCabinetDbContext db, ILanguageModel local, AiKeyProtector protector,
    IHttpClientFactory clients, IOptions<LanguageModelOptions> options, ILogger<LanguageModelProvider> logger)
    : ILanguageModelProvider
{
    public const string ApiHttpClientName = "ai-api";

    public async Task<ILanguageModel> ForAsync(Sensitivity sensitivity, CancellationToken ct)
    {
        if (sensitivity != Sensitivity.Normal) return local;
        var setting = await db.AiProviderSettings.AsNoTracking().FirstOrDefaultAsync(ct);
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

    internal OpenAiCompatibleModel Api(string baseUrl, string model, string key, int timeoutMinutes, int maxTokens,
        string reasoningEffort)
        => new(clients, ApiHttpClientName, baseUrl, model, key, timeoutMinutes, maxTokens, remote: true, reasoningEffort);
}
