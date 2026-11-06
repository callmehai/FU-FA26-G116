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

    /// <summary>
    /// The same for an API (ADR-0056). Hosted reasoning models such as DeepSeek V4.1 count their thinking against it, and a
    /// long meeting's part can use 2,000 thinking tokens before a word of the answer: at the local limit the answer came
    /// back empty.
    /// </summary>
    public int ApiMaxTokens { get; set; } = 8192;

    /// <summary>
    /// Sent to an API as <c>reasoning_effort</c>. "none" turns DeepSeek V4.1's thinking off: a draft restates what was
    /// said, and measured on the eight bench meetings the thinking only made each call 2–3 times slower and could use the
    /// whole token budget before the answer began. Empty sends nothing, for an API that rejects the field.
    /// </summary>
    public string ApiReasoningEffort { get; set; } = "none";

    /// <summary>
    /// How a draft is written through an API: "parts" — the in-house prompts, each agenda item's talk in one call and
    /// <see cref="ApiParallelism"/> items at once; "oneshot" — the whole meeting in one call, as AI-eCabinet does, falling
    /// back to "parts" when the answer cannot be cut back into the agenda.
    /// </summary>
    public string ApiWriter { get; set; } = "parts";

    /// <summary>Agenda items written at the same time through an API. One writes them in turn.</summary>
    public int ApiParallelism { get; set; } = 3;

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
    string apiKey, int timeoutMinutes, int maxTokens, bool remote, string? reasoningEffort = null) : ILanguageModel
{
    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web)
    {
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
    };

    private string Machine => remote ? "API AI" : "Máy AI";

    public bool IsConfigured => Uri.TryCreate(baseUrl, UriKind.Absolute, out _) && model.Length > 0;

    public string Model => model;

    /// <summary>A hosted API (ADR-0056) rather than the in-house model: the draft then uses the hosted harness.</summary>
    public bool Remote => remote;

    /// <summary>
    /// An API's 429 is tried again after these waits (or its Retry-After, up to 20 seconds): parts written side by side
    /// can trip a shared plan's rate limit for a moment. The in-house model is never retried.
    /// </summary>
    private static readonly TimeSpan[] RateLimitWaits = [TimeSpan.FromSeconds(2), TimeSpan.FromSeconds(5)];

    private sealed class RateLimited(TimeSpan? retryAfter) : Exception
    {
        internal TimeSpan? RetryAfter { get; } = retryAfter;
    }

    /// <summary>
    /// The provider's own words for a refusal (OpenAI shape <c>{"error":{"message":…}}</c>, or <c>{"error":"…"}</c>,
    /// or <c>{"message":…}</c>), so "402" can say whether the account lacks credit or the model is not on the plan.
    /// Cut to 200 characters; an unreadable body adds nothing. The request's key never appears in a response body.
    /// </summary>
    private static async Task<string> ReasonAsync(HttpResponseMessage response, CancellationToken ct)
    {
        try
        {
            var text = await response.Content.ReadAsStringAsync(ct);
            using var doc = JsonDocument.Parse(text);
            var root = doc.RootElement;
            if (root.ValueKind != JsonValueKind.Object) return "";
            JsonElement found = default;
            if (root.TryGetProperty("error", out var error))
            {
                if (error.ValueKind == JsonValueKind.String) found = error;
                else if (error.ValueKind == JsonValueKind.Object) error.TryGetProperty("message", out found);
            }
            else root.TryGetProperty("message", out found);
            if (found.ValueKind != JsonValueKind.String) return "";
            var reason = found.GetString()!.Trim();
            if (reason.Length == 0) return "";
            return " Nhà cung cấp: " + (reason.Length > 200 ? reason[..200] + "…" : reason);
        }
        catch (Exception ex) when (ex is JsonException or HttpRequestException or InvalidOperationException)
        {
            return "";
        }
    }

    public async Task<string> CompleteAsync(string system, string user, CancellationToken ct)
    {
        if (!remote) return await SendAsync(system, user, lastTry: true, ct);
        for (var attempt = 0; ; attempt++)
        {
            try
            {
                return await SendAsync(system, user, lastTry: attempt >= RateLimitWaits.Length, ct);
            }
            catch (RateLimited limited)
            {
                var wait = limited.RetryAfter is { } after && after > TimeSpan.Zero && after <= TimeSpan.FromSeconds(20)
                    ? after : RateLimitWaits[attempt];
                await Task.Delay(wait, ct);
            }
        }
    }

    private async Task<string> SendAsync(string system, string user, bool lastTry, CancellationToken ct)
    {
        if (!IsConfigured) throw new LanguageModelException($"{Machine} chưa được cấu hình.");
        var client = clients.CreateClient(clientName);
        client.Timeout = TimeSpan.FromMinutes(timeoutMinutes);
        using var request = new HttpRequestMessage(HttpMethod.Post,
            new Uri(new Uri(baseUrl.TrimEnd('/') + "/"), "chat/completions"))
        {
            // Temperature 0: a draft restates what was said; it must not invent (AI-eCabinet uses 0 too).
            Content = JsonContent.Create(new ChatRequest(model,
                [new("system", system), new("user", user)], 0.0, maxTokens, false,
                string.IsNullOrWhiteSpace(reasoningEffort) ? null : reasoningEffort), options: Json),
        };
        if (!string.IsNullOrEmpty(apiKey))
            request.Headers.Authorization = new("Bearer", apiKey);
        // OpenCode Go routes by a session id ("one per conversation") and asks callers to name themselves; each call
        // here is its own one-shot conversation. Other OpenAI-compatible servers ignore both headers.
        request.Headers.UserAgent.ParseAdd("TrustCabinet/1.0");
        request.Headers.TryAddWithoutValidation("x-opencode-session", Guid.NewGuid().ToString());
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
            if (remote && !lastTry && (int)response.StatusCode == 429)
                throw new RateLimited(response.Headers.RetryAfter?.Delta);
            if (!response.IsSuccessStatusCode)
                throw new LanguageModelException(((int)response.StatusCode, remote) switch
                {
                    (401 or 403, true) => $"API AI từ chối key ({(int)response.StatusCode}).",
                    // 402 Payment Required: a paid model on an account without credit — free models usually end in "-free".
                    (402, true) => "API AI đòi thanh toán (402): model này trả phí hoặc tài khoản hết tiền. Thử model miễn phí (thường có đuôi -free).",
                    (429, true) => "API AI đang giới hạn số lần gọi (429). Thử lại sau ít phút.",
                    var (status, _) => $"{Machine} trả lỗi {status}.",
                } + await ReasonAsync(response, ct));
            ChatResponse? body;
            try
            {
                body = await response.Content.ReadFromJsonAsync<ChatResponse>(Json, ct);
            }
            catch (JsonException ex)
            {
                throw new LanguageModelException($"{Machine} trả về dữ liệu không đọc được.", ex);
            }
            var choice = body?.Choices?.FirstOrDefault();
            var content = choice?.Message?.Content;
            if (string.IsNullOrWhiteSpace(content))
                throw new LanguageModelException(choice?.FinishReason == "length"
                    // A reasoning model that spent the whole budget thinking: the answer was cut before it began.
                    ? $"{Machine} dùng hết {maxTokens} token khi đang suy luận, chưa kịp trả lời."
                    : $"{Machine} không trả lời.");
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
        [property: JsonPropertyName("max_tokens")] int MaxTokens, bool Stream,
        [property: JsonPropertyName("reasoning_effort")] string? ReasoningEffort);
    private sealed record Choice(ChatMessage? Message, [property: JsonPropertyName("finish_reason")] string? FinishReason);
    private sealed record ChatResponse(IReadOnlyList<Choice>? Choices);
}
