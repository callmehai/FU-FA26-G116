using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Serialization;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using TrustCabinet.Application.Summaries;

namespace TrustCabinet.Infrastructure.Summaries;

/// <summary>
/// The in-house model (ADR-0054), as the server is configured (<c>Ai:BaseUrl</c>, <c>Ai:Model</c>).
/// With <c>Ai:BackupBaseUrl</c> set, a call the main machine cannot take goes to the backup machine (ADR-0063); after
/// that the backup is asked first for <see cref="SkipMainFor"/>, so each part of a long draft does not wait for the
/// main machine's connect timeout again. Both are the team's own machines on the tailnet: the transcript still never
/// leaves it.
/// </summary>
public sealed class LanguageModelClient(IHttpClientFactory clients, IOptions<LanguageModelOptions> options,
    TimeProvider clock, ILogger<LanguageModelClient> logger) : ILanguageModel
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
            settings.ApiKey, settings.TimeoutMinutes, settings.MaxTokens);
    }
}

/// <summary>
/// Calls <c>POST {BaseUrl}/chat/completions</c>, the API every local runtime speaks (as AI-eCabinet's worker does), so
/// the team can swap Ollama for llama.cpp by configuration alone.
/// </summary>
public sealed class OpenAiCompatibleModel(IHttpClientFactory clients, string clientName, string baseUrl, string model,
    string apiKey, int timeoutMinutes, int maxTokens) : ILanguageModel
{
    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web)
    {
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
    };

    public bool IsConfigured => Uri.TryCreate(baseUrl, UriKind.Absolute, out _) && model.Length > 0;

    public string Model => model;

    /// <summary>
    /// The provider's own words for a refusal (OpenAI shape <c>{"error":{"message":…}}</c>, or <c>{"error":"…"}</c>,
    /// or <c>{"message":…}</c>), so a 404 can say the model is not pulled on that machine.
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
            return " Máy AI báo: " + (reason.Length > 200 ? reason[..200] + "…" : reason);
        }
        catch (Exception ex) when (ex is JsonException or HttpRequestException or InvalidOperationException)
        {
            return "";
        }
    }

    public async Task<string> CompleteAsync(string system, string user, CancellationToken ct)
    {
        if (!IsConfigured) throw new LanguageModelException("Máy AI chưa sẵn sàng.");
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
            throw new LanguageModelException("Không kết nối được máy AI. Máy chạy AI có thể đang tắt.", ex, unreachable: true);
        }
        using (response)
        {
            if (!response.IsSuccessStatusCode)
                throw new LanguageModelException($"Máy AI trả lỗi {(int)response.StatusCode}." + await ReasonAsync(response, ct));
            ChatResponse? body;
            try
            {
                body = await response.Content.ReadFromJsonAsync<ChatResponse>(Json, ct);
            }
            catch (JsonException ex)
            {
                throw new LanguageModelException("Máy AI trả về dữ liệu không đọc được.", ex);
            }
            var choice = body?.Choices?.FirstOrDefault();
            var content = choice?.Message?.Content;
            if (string.IsNullOrWhiteSpace(content))
                throw new LanguageModelException(choice?.FinishReason == "length"
                    // A reasoning model that spent the whole budget thinking: the answer was cut before it began.
                    ? $"Máy AI dùng hết {maxTokens} token khi đang suy luận, chưa kịp trả lời."
                    : "Máy AI không trả lời.");
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
    private sealed record Choice(ChatMessage? Message, [property: JsonPropertyName("finish_reason")] string? FinishReason);
    private sealed record ChatResponse(IReadOnlyList<Choice>? Choices);
}
