using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.Extensions.Options;
using TrustCabinet.Domain.Transcription;
using TrustCabinet.Domain.VoiceId;

namespace TrustCabinet.Infrastructure.Transcription;

public sealed class SpeechOptions
{
    public const string SectionName = "Speech";

    /// <summary>The worker's address on the internal network, e.g. http://speech:8090. Empty means not deployed.</summary>
    public string BaseUrl { get; set; } = "";

    /// <summary>Optional shared key; the worker refuses requests without it when it has one set.</summary>
    public string ApiKey { get; set; } = "";

    /// <summary>A long meeting on two CPU cores takes a fifth of its length or so; leave generous room.</summary>
    public int TimeoutMinutes { get; set; } = 120;

    public bool IsConfigured => Uri.TryCreate(BaseUrl, UriKind.Absolute, out _);
}

/// <summary>HTTP client for workers/speech — the contract is in workers/speech/README.md.</summary>
public sealed class SpeechWorkerClient(IHttpClientFactory clients, IOptions<SpeechOptions> options) : ISpeechEngine, IVoiceEmbedder
{
    public const string HttpClientName = "speech";
    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web);

    public bool IsConfigured => options.Value.IsConfigured;

    public async Task<SpeechTranscription> TranscribeAsync(Stream audio, string contentType, bool diarize, bool clip,
        CancellationToken ct)
    {
        using var response = await SendAsync(
            $"v1/transcribe?diarize={(diarize ? "true" : "false")}&profile={(clip ? "clip" : "room")}", audio, contentType, ct);
        var body = await response.Content.ReadFromJsonAsync<TranscribeBody>(Json, ct)
            ?? throw new SpeechEngineException("Máy chủ nhận dạng giọng nói trả kết quả rỗng.");
        return new(body.DurationMs, body.Language ?? "vi", body.AsrModel ?? "", body.EmbeddingModel ?? "",
            [.. (body.Segments ?? []).Select(s => new SpeechSegment(s.StartMs, s.EndMs, s.Speaker, s.Text ?? "", s.LevelDbfs))],
            [.. (body.Speakers ?? []).Select(s => new SpeechSpeaker(s.Label ?? "", s.SpeechMs, s.Embedding ?? []))]);
    }

    /// <summary>SCRUM-35 — the embedding of one enrolment sample, after the worker's quality checks.</summary>
    public async Task<SpeechEmbedding> EmbedAsync(Stream audio, string contentType, CancellationToken ct)
    {
        using var response = await SendAsync("v1/embed", audio, contentType, ct, sample: true);
        var body = await response.Content.ReadFromJsonAsync<EmbedBody>(Json, ct)
            ?? throw new SpeechEngineException("Máy chủ nhận dạng giọng nói trả kết quả rỗng.");
        return new(body.Embedding ?? [], body.Model ?? "", body.SpeechMs);
    }

    private async Task<HttpResponseMessage> SendAsync(string path, Stream audio, string contentType, CancellationToken ct,
        bool sample = false)
    {
        if (!IsConfigured) throw new SpeechEngineException("Máy chủ nhận dạng giọng nói chưa được cấu hình.");
        var client = clients.CreateClient(HttpClientName);
        client.Timeout = TimeSpan.FromMinutes(options.Value.TimeoutMinutes);
        using var request = new HttpRequestMessage(HttpMethod.Post, new Uri(new Uri(options.Value.BaseUrl.TrimEnd('/') + "/"), path))
        {
            Content = new StreamContent(audio) { Headers = { ContentType = MediaTypeHeaderValue.Parse(contentType) } },
        };
        if (!string.IsNullOrEmpty(options.Value.ApiKey))
            request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", options.Value.ApiKey);
        HttpResponseMessage response;
        try
        {
            response = await client.SendAsync(request, HttpCompletionOption.ResponseHeadersRead, ct);
        }
        catch (Exception ex) when (ex is HttpRequestException or TaskCanceledException && !ct.IsCancellationRequested)
        {
            throw new SpeechEngineException("Không kết nối được máy chủ nhận dạng giọng nói.", ex);
        }
        if (response.IsSuccessStatusCode) return response;
        var status = response.StatusCode;
        var error = await ReadErrorAsync(response, ct);
        response.Dispose();
        // A sample the worker turned down is the person's to redo, not an outage.
        if (sample && (status == HttpStatusCode.UnprocessableEntity || error == "unreadable_audio"))
            throw new VoiceSampleRejectedException(error ?? "rejected");
        throw new SpeechEngineException(status switch
        {
            HttpStatusCode.BadRequest when error == "unreadable_audio" => "Không đọc được tệp âm thanh.",
            HttpStatusCode.RequestEntityTooLarge => "Tệp âm thanh quá lớn để chuyển thành chữ.",
            HttpStatusCode.Unauthorized => "Máy chủ nhận dạng giọng nói từ chối khoá truy cập.",
            _ => $"Máy chủ nhận dạng giọng nói báo lỗi (HTTP {(int)status}).",
        });
    }

    private static async Task<string?> ReadErrorAsync(HttpResponseMessage response, CancellationToken ct)
    {
        try { return (await response.Content.ReadFromJsonAsync<ErrorBody>(Json, ct))?.Error; }
        catch (Exception ex) when (ex is JsonException or NotSupportedException) { return null; }
    }

    private sealed record ErrorBody(string? Error);
    private sealed record EmbedBody(float[]? Embedding, string? Model, long SpeechMs);
    private sealed record SegmentBody(long StartMs, long EndMs, string? Speaker, string? Text, double? LevelDbfs);
    private sealed record SpeakerBody(string? Label, long SpeechMs, float[]? Embedding);
    private sealed record TranscribeBody(long DurationMs, string? Language, string? AsrModel, string? EmbeddingModel,
        SegmentBody[]? Segments, SpeakerBody[]? Speakers);
}
