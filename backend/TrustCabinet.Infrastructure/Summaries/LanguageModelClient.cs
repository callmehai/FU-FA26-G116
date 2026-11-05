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

    /// <summary>The longest answer asked for, in tokens.</summary>
    public int MaxTokens { get; set; } = 2048;

    public bool IsConfigured => Uri.TryCreate(BaseUrl, UriKind.Absolute, out _) && Model.Length > 0;
}

/// <summary>
/// Calls <c>POST {BaseUrl}/chat/completions</c>, the API every local runtime speaks (as AI-eCabinet's worker does), so
/// the team can swap Ollama for llama.cpp or another model by configuration alone.
/// </summary>
public sealed class LanguageModelClient(IHttpClientFactory clients, IOptions<LanguageModelOptions> options) : ILanguageModel
{
    public const string HttpClientName = "ai";
    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web)
    {
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
    };

    public bool IsConfigured => options.Value.IsConfigured;

    public string Model => options.Value.Model;

    public async Task<string> CompleteAsync(string system, string user, CancellationToken ct)
    {
        var settings = options.Value;
        if (!settings.IsConfigured) throw new LanguageModelException("Máy AI chưa được cấu hình.");
        var client = clients.CreateClient(HttpClientName);
        client.Timeout = TimeSpan.FromMinutes(settings.TimeoutMinutes);
        using var request = new HttpRequestMessage(HttpMethod.Post,
            new Uri(new Uri(settings.BaseUrl.TrimEnd('/') + "/"), "chat/completions"))
        {
            // Temperature 0: a draft restates what was said; it must not invent (AI-eCabinet uses 0 too).
            Content = JsonContent.Create(new ChatRequest(settings.Model,
                [new("system", system), new("user", user)], 0.0, settings.MaxTokens, false), options: Json),
        };
        if (!string.IsNullOrEmpty(settings.ApiKey))
            request.Headers.Authorization = new("Bearer", settings.ApiKey);
        HttpResponseMessage response;
        try
        {
            response = await client.SendAsync(request, ct);
        }
        catch (Exception ex) when (ex is HttpRequestException or TaskCanceledException && !ct.IsCancellationRequested)
        {
            throw new LanguageModelException("Không kết nối được máy AI. Máy chạy AI có thể đang tắt.", ex);
        }
        using (response)
        {
            if (!response.IsSuccessStatusCode)
                throw new LanguageModelException($"Máy AI trả lỗi {(int)response.StatusCode}.");
            ChatResponse? body;
            try
            {
                body = await response.Content.ReadFromJsonAsync<ChatResponse>(Json, ct);
            }
            catch (JsonException ex)
            {
                throw new LanguageModelException("Máy AI trả về dữ liệu không đọc được.", ex);
            }
            var content = body?.Choices?.FirstOrDefault()?.Message?.Content;
            if (string.IsNullOrWhiteSpace(content)) throw new LanguageModelException("Máy AI không trả lời.");
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
