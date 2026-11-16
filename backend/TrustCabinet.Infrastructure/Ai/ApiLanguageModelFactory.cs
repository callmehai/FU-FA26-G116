using TrustCabinet.Application.Ai;
using TrustCabinet.Application.Summaries;
using TrustCabinet.Infrastructure.Summaries;

namespace TrustCabinet.Infrastructure.Ai;

/// <summary>The API an administrator chose (ADR-0056), called through <see cref="LanguageModelProvider.ApiHttpClientName"/>.</summary>
public sealed class ApiLanguageModelFactory(IHttpClientFactory clients) : IApiLanguageModelFactory
{
    public ILanguageModel Create(string baseUrl, string model, string apiKey, int timeoutMinutes, int maxTokens,
        string? reasoningEffort)
        => new OpenAiCompatibleModel(clients, LanguageModelProvider.ApiHttpClientName, baseUrl, model, apiKey, timeoutMinutes,
            maxTokens, remote: true, reasoningEffort);
}
