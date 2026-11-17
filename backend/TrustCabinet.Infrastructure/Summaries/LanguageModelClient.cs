using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Serialization;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using TrustCabinet.Application.Summaries;

namespace TrustCabinet.Infrastructure.Summaries;

/// <summary>
/// The in-house model (ADR-0054), as the server is configured (<c>Ai:BaseUrl</c>, <c>Ai:Model</c>). The API an administrator
/// may set up instead (ADR-0056) is the same kind of endpoint, built by <see cref="Ai.ApiLanguageModelFactory"/>.
/// With <c>Ai:BackupBaseUrl</c> set, a call the main machine cannot take goes to the backup machine (ADR-0063); after
/// that the backup is asked first for <see cref="SkipMainFor"/>, so each part of a long draft does not wait for the
/// main machine's connect timeout again. Both are the team's own machines on the tailnet: the transcript still never
/// leaves it.
/// </summary>
public sealed class LanguageModelClient(IHttpClientFactory clients, IOptions<LanguageModelOptions> options,
    TimeProvider clock, ILogger<LanguageModelClient> logger) : ILanguageModel, ILocalModelMachines
{
    public const string HttpClientName = "ai";

    internal static readonly TimeSpan SkipMainFor = TimeSpan.FromMinutes(2);

    /// <summary>When the main machine is asked first again, as UTC ticks; 0 when it is up.</summary>
    private long mainDownUntil;

    public bool IsConfigured => options.Value.IsConfigured;

    public bool HasBackup => options.Value.HasBackup;

    public string Model => options.Value.Model;

    public async Task<string> CompleteAsync(string system, string user, CancellationToken ct)
    {
        if (!HasBackup) return await Machine(backup: false).CompleteAsync(system, user, ct);
        var mainFirst = clock.GetUtcNow().UtcTicks >= Interlocked.Read(ref mainDownUntil);
        try
        {
            return await Machine(backup: !mainFirst).CompleteAsync(system, user, ct);
        }
        catch (LanguageModelException first) when (first.Unreachable)
        {
            if (mainFirst) MarkMainDown(first);
            try
            {
                var answer = await Machine(backup: mainFirst).CompleteAsync(system, user, ct);
                if (!mainFirst) Interlocked.Exchange(ref mainDownUntil, 0);
                return answer;
            }
            catch (LanguageModelException second) when (second.Unreachable)
            {
                if (!mainFirst) MarkMainDown(second);
                throw new LanguageModelException(
                    "Không kết nối được máy AI chính lẫn máy dự phòng. Cả hai có thể đang tắt.", second, unreachable: true);
            }
        }
    }

    /// <summary>
    /// "Thử kết nối" for the in-house model: each configured machine asked on its own, side by side, so an administrator
    /// learns before a meeting whether the backup would really take over.
    /// </summary>
    public async Task<IReadOnlyList<LocalMachineCheck>> CheckAsync(TimeSpan timeout, CancellationToken ct)
    {
        var machines = HasBackup ? new[] { false, true } : [false];
        return await Task.WhenAll(machines.Select(async backup =>
        {
            using var limit = CancellationTokenSource.CreateLinkedTokenSource(ct);
            limit.CancelAfter(timeout);
            var started = clock.GetTimestamp();
            try
            {
                await Machine(backup).CompleteAsync("Bạn là trợ lý kiểm tra kết nối.", "Trả lời đúng một từ: OK", limit.Token);
                return new LocalMachineCheck(backup, (long)clock.GetElapsedTime(started).TotalMilliseconds, null);
            }
            catch (LanguageModelException failure)
            {
                return new LocalMachineCheck(backup, 0, failure.Message);
            }
            catch (OperationCanceledException) when (!ct.IsCancellationRequested)
            {
                return new LocalMachineCheck(backup, 0,
                    $"Không trả lời sau {(int)timeout.TotalSeconds} giây. Kiểm tra máy còn bật và Tailscale đang kết nối.");
            }
        }));
    }

    private void MarkMainDown(Exception error)
    {
        Interlocked.Exchange(ref mainDownUntil, (clock.GetUtcNow() + SkipMainFor).UtcTicks);
        logger.LogWarning(error, "The main AI machine cannot be reached; the backup machine takes the drafts for {Minutes} minutes",
            SkipMainFor.TotalMinutes);
    }

    private OpenAiCompatibleModel Machine(bool backup)
    {
        var settings = options.Value;
        return new(clients, HttpClientName, backup ? settings.BackupBaseUrl : settings.BaseUrl, settings.Model,
            settings.ApiKey, settings.TimeoutMinutes, settings.MaxTokens, remote: false);
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
                : "Không kết nối được máy AI. Máy chạy AI có thể đang tắt.", ex, unreachable: true);
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
