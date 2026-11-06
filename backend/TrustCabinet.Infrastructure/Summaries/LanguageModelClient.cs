using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Serialization;
using Microsoft.Extensions.Options;
using TrustCabinet.Domain.Summaries;

namespace TrustCabinet.Infrastructure.Summaries;

public sealed class LanguageModelOptions
{
    public const string SectionName = "Ai";

    /// <summary>
    /// An OpenAI-compatible server — Ollama (http://host:11434/v1), llama.cpp's llama-server (http://host:8080/v1),
    /// LM Studio or vLLM. It runs on a team member's machine, not the test server (ADR-0054). Empty means not deployed:
    /// a draft then fails with a message saying so, and the rest of the app is unaffected.
    /// </summary>
    public string BaseUrl { get; set; } = "";

    /// <summary>The model name the server knows, e.g. "qwen2.5:7b-instruct" on Ollama.</summary>
    public string Model { get; set; } = "";

    /// <summary>Optional bearer key, for a tunnel or proxy in front of the server that asks for one.</summary>
    public string ApiKey { get; set; } = "";

    /// <summary>One agenda item of a long meeting on a laptop takes a minute or two; leave room for a slow machine.</summary>
    public int TimeoutMinutes { get; set; } = 15;

    /// <summary>
    /// Encrypts the API key an administrator stores (ADR-0056): 32 bytes, base64. Empty derives one from the token signing
    /// key.
    /// </summary>
    public string EncryptionKey { get; set; } = "";

    /// <summary>The longest answer asked for, in tokens.</summary>
    public int MaxTokens { get; set; } = 2048;

    public bool IsConfigured => Uri.TryCreate(BaseUrl, UriKind.Absolute, out _) && Model.Length > 0;
}

/// <summary>
/// The in-house model (ADR-0054), as the server is configured (<c>Ai:BaseUrl</c>, <c>Ai:Model</c>). The API an administrator
/// may set up instead (ADR-0056) is the same kind of endpoint, built by <see cref="Ai.LanguageModelProvider"/>.
/// </summary>
public sealed class LanguageModelClient(IHttpClientFactory clients, IOptions<LanguageModelOptions> options) : ILanguageModel
{
    public const string HttpClientName = "ai";

    public bool IsConfigured => options.Value.IsConfigured;

    public string Model => options.Value.Model;

    public Task<string> CompleteAsync(string system, string user, CancellationToken ct)
    {
        var settings = options.Value;
        return new OpenAiCompatibleModel(clients, HttpClientName, settings.BaseUrl, settings.Model, settings.ApiKey,
            settings.TimeoutMinutes, settings.MaxTokens, remote: false).CompleteAsync(system, user, ct);
    }
}

/// <summary>
/// Calls <c>POST {BaseUrl}/chat/completions</c>, the API every local runtime speaks (as AI-eCabinet's worker does) and
/// the hosted ones too, so the team can swap Ollama for llama.cpp, or for an API, by configuration alone.
/// </summary>
public sealed class OpenAiCompatibleModel(IHttpClientFactory clients, string clientName, string baseUrl, string model,
    string apiKey, int timeoutMinutes, int maxTokens, bool remote) : ILanguageModel
{
    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web)
    {
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
    };

    private string Machine => remote ? "API AI" : "Máy AI";

    public bool IsConfigured => Uri.TryCreate(baseUrl, UriKind.Absolute, out _) && model.Length > 0;

    public string Model => model;

    public async Task<string> CompleteAsync(string system, string user, CancellationToken ct)
    {
        if (!IsConfigured) throw new LanguageModelException($"{Machine} chưa được cấu hình.");
        var client = clients.CreateClient(clientName);
        client.Timeout = TimeSpan.FromMinutes(timeoutMinutes);
        using var request = new HttpRequestMessage(HttpMethod.Post,
            new Uri(new Uri(baseUrl.TrimEnd('/') + "/"), "chat/completions"))
        {
            // Temperature 0: a draft restates what was said; it must not invent (AI-eCabinet uses 0 too).
            Content = JsonContent.Create(new ChatRequest(model,
                [new("system", system), new("user", user)], 0.0, maxTokens, false), options: Json),
        };
        if (!string.IsNullOrEmpty(apiKey))
            request.Headers.Authorization = new("Bearer", apiKey);
        HttpResponseMessage response;
        try
        {
            response = await client.SendAsync(request, ct);
        }
        catch (Exception ex) when (ex is HttpRequestException or TaskCanceledException && !ct.IsCancellationRequested)
        {
            throw new LanguageModelException(remote
                ? "Không kết nối được API AI. Kiểm tra địa chỉ API hoặc mạng của server."
                : "Không kết nối được máy AI. Máy chạy AI có thể đang tắt.", ex);
        }
        using (response)
        {
            if (!response.IsSuccessStatusCode)
                throw new LanguageModelException((int)response.StatusCode is 401 or 403 && remote
                    ? $"API AI từ chối key ({(int)response.StatusCode})."
                    : $"{Machine} trả lỗi {(int)response.StatusCode}.");
            ChatResponse? body;
            try
            {
                body = await response.Content.ReadFromJsonAsync<ChatResponse>(Json, ct);
            }
            catch (JsonException ex)
            {
                throw new LanguageModelException($"{Machine} trả về dữ liệu không đọc được.", ex);
            }
            var content = body?.Choices?.FirstOrDefault()?.Message?.Content;
            if (string.IsNullOrWhiteSpace(content)) throw new LanguageModelException($"{Machine} không trả lời.");
            return StripThinking(content).Trim();
        }
    }

    /// <summary>Reasoning models (Qwen 3, DeepSeek R1) may put their thinking in the answer; only the answer is kept.</summary>
    private static string StripThinking(string text)
    {
        var end = text.LastIndexOf("</think>", StringComparison.Ordinal);
        return end >= 0 ? text[(end + "</think>".Length)..] : text;
    }

    private sealed record ChatMessage(string Role, string Content);
    private sealed record ChatRequest(string Model, IReadOnlyList<ChatMessage> Messages, double Temperature,
        [property: JsonPropertyName("max_tokens")] int MaxTokens, bool Stream);
    private sealed record Choice(ChatMessage? Message);
    private sealed record ChatResponse(IReadOnlyList<Choice>? Choices);
}
