using System.Collections.Concurrent;
using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using TrustCabinet.Application.Transcription;
using TrustCabinet.Application.VoiceId;

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

    /// <summary>
    /// A second worker running the same pinned models, tried when <see cref="BaseUrl"/> cannot be reached (ADR-0081): the
    /// main worker is a team member's Mac over Tailscale, the backup the worker on the server. Empty means none. It gets the
    /// same <see cref="ApiKey"/>; a worker without a key of its own accepts any request.
    /// </summary>
    public string BackupBaseUrl { get; set; } = "";

    /// <summary>
    /// How long to wait for a worker to accept the connection. A Mac that sleeps or left the tailnet never refuses: without
    /// this a sentence would wait the whole request timeout before the backup is tried. A running worker accepts at once,
    /// even while busy. Short, because a live mic sentence waits on it.
    /// </summary>
    public int ConnectTimeoutSeconds { get; set; } = 5;

    public bool IsConfigured => Uri.TryCreate(BaseUrl, UriKind.Absolute, out _);

    public bool HasBackup => IsConfigured && Uri.TryCreate(BackupBaseUrl, UriKind.Absolute, out _);
}

/// <summary>
/// HTTP client for workers/speech — the contract is in workers/speech/README.md. With <c>Speech:BackupBaseUrl</c> set
/// (ADR-0081), a request the main worker cannot take goes to the backup, and the backup is then asked first for
/// <see cref="SkipPrimaryFor"/>, so each sentence of a live mic does not wait the connect timeout again. Only a failure to
/// reach a worker moves a request; an answer a worker gave, error or not, is the answer.
/// </summary>
public sealed class SpeechWorkerClient(IHttpClientFactory clients, IOptions<SpeechOptions> options, TimeProvider clock,
    ILogger<SpeechWorkerClient> logger) : ISpeechEngine, IVoiceEmbedder
{
    public const string HttpClientName = "speech";
    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web);

    internal static readonly TimeSpan SkipPrimaryFor = TimeSpan.FromMinutes(2);

    /// <summary>When the main worker is asked first again, as UTC ticks; 0 when it is up.</summary>
    private long primaryDownUntil;

    /// <summary>The worker each named transcription runs on while it runs, so its progress is asked of that worker.</summary>
    private readonly ConcurrentDictionary<Guid, string> runs = new();

    public bool IsConfigured => options.Value.IsConfigured;

    /// <summary>How long a progress question may take: the worker answers it from memory, so a slow answer means trouble.</summary>
    internal static readonly TimeSpan ProgressTimeout = TimeSpan.FromSeconds(3);

    public async Task<SpeechTranscription> TranscribeAsync(Stream audio, string contentType, bool diarize, bool clip,
        CancellationToken ct, string? context = null, Guid? run = null)
    {
        var ahead = string.IsNullOrWhiteSpace(context) ? "" : $"&context={Uri.EscapeDataString(context)}";
        var named = run is { } id ? $"&run={id:D}" : "";
        TranscribeBody body;
        try
        {
            using var response = await SendAsync(
                $"v1/transcribe?diarize={(diarize ? "true" : "false")}&profile={(clip ? "clip" : "room")}{ahead}{named}", audio,
                contentType, ct, run: run);
            body = await ReadBodyAsync<TranscribeBody>(response, ct);
        }
        finally
        {
            if (run is { } finished) runs.TryRemove(finished, out _);
        }
        return new(body.DurationMs, body.Language ?? "vi", body.AsrModel ?? "", body.EmbeddingModel ?? "",
            [.. (body.Segments ?? []).Select(s => new SpeechSegment(s.StartMs, s.EndMs, s.Speaker, s.Text ?? "", s.LevelDbfs, s.WordStartsMs,
                s.WordConfidence))],
            [.. (body.Speakers ?? []).Select(s => new SpeechSpeaker(s.Label ?? "", s.SpeechMs, s.Embedding ?? []))],
            body.Context);
    }

    /// <summary>
    /// GET /v1/progress, of the worker running <paramref name="run"/> (the main one when this client is not running it).
    /// Any failure — not configured, unreachable, slow, an older worker without the route, an answer that does not parse —
    /// is null: the caller shows the bar where it was and the run goes on.
    /// </summary>
    public async Task<SpeechProgress?> ProgressAsync(Guid run, CancellationToken ct)
    {
        if (!IsConfigured) return null;
        using var timeout = CancellationTokenSource.CreateLinkedTokenSource(ct);
        timeout.CancelAfter(ProgressTimeout);
        try
        {
            var client = clients.CreateClient(HttpClientName);
            var machine = runs.TryGetValue(run, out var running) ? running : options.Value.BaseUrl;
            using var request = new HttpRequestMessage(HttpMethod.Get,
                new Uri(new Uri(machine.TrimEnd('/') + "/"), $"v1/progress?run={run:D}"));
            if (!string.IsNullOrEmpty(options.Value.ApiKey))
                request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", options.Value.ApiKey);
            using var response = await client.SendAsync(request, timeout.Token);
            if (!response.IsSuccessStatusCode) return null;
            var body = await response.Content.ReadFromJsonAsync<ProgressBody>(Json, timeout.Token);
            return body?.State switch
            {
                "running" => new(SpeechRunState.Running, Math.Clamp(body.Percent, 0, 100)),
                "queued" => new(SpeechRunState.Queued, 0),
                "unknown" => new(SpeechRunState.Unknown, 0),
                _ => null,
            };
        }
        catch (Exception ex) when (ex is HttpRequestException or IOException or JsonException or NotSupportedException
            || ex is OperationCanceledException && !ct.IsCancellationRequested)
        {
            return null;
        }
    }

    /// <summary>SCRUM-35 — the embedding of one enrolment sample, after the worker's quality checks.</summary>
    public async Task<SpeechEmbedding> EmbedAsync(Stream audio, string contentType, CancellationToken ct)
    {
        using var response = await SendAsync("v1/embed", audio, contentType, ct, sample: true);
        var body = await ReadBodyAsync<EmbedBody>(response, ct);
        return new(body.Embedding ?? [], body.Model ?? "", body.SpeechMs);
    }

    /// <summary>
    /// Sends the audio to the main worker, or to the backup when the main one cannot be reached (ADR-0081). A worker that
    /// answered — 4xx or 5xx included — is never asked again elsewhere: its error is about the request or its own
    /// configuration, and the backup would say the same or hide what needs fixing. What moves the request:
    /// <list type="bullet">
    /// <item>no connection (refused, unreachable, connect timeout): the worker never saw the request;</item>
    /// <item>a connection that broke before the answer (the Mac slept or left the tailnet mid-run): the worker stops on
    /// the dropped connection, and the same audio with the same parameters gives the same result on the other one.</item>
    /// </list>
    /// Not the request timeout once audio went out: that worker was reached and working, and the backup is slower. The
    /// audio is rewound for the second try; a stream that cannot seek is tried again only if no byte of it was read.
    /// </summary>
    private async Task<HttpResponseMessage> SendAsync(string path, Stream audio, string contentType, CancellationToken ct,
        bool sample = false, Guid? run = null)
    {
        var settings = options.Value;
        if (!settings.IsConfigured) throw new SpeechEngineException("Máy chủ nhận dạng giọng nói chưa được cấu hình.");
        var body = new ReadMeter(audio);
        if (!settings.HasBackup)
        {
            try
            {
                return await SendToAsync(settings.BaseUrl, path, body, contentType, sample, run, ct);
            }
            catch (WorkerUnreachable failure)
            {
                throw new SpeechEngineException("Không kết nối được máy chủ nhận dạng giọng nói.", failure.InnerException);
            }
        }
        var start = audio.CanSeek ? audio.Position : -1;
        var primaryFirst = clock.GetUtcNow().UtcTicks >= Interlocked.Read(ref primaryDownUntil);
        try
        {
            return await SendToAsync(primaryFirst ? settings.BaseUrl : settings.BackupBaseUrl, path, body, contentType, sample,
                run, ct);
        }
        catch (WorkerUnreachable first) when (first.MayMove && (start >= 0 || !body.Started))
        {
            if (primaryFirst)
            {
                SkipPrimary();
                logger.LogWarning("Speech primary unreachable ({Reason}), using backup; primary skipped for {Minutes} minutes",
                    first.InnerException?.GetType().Name, SkipPrimaryFor.TotalMinutes);
            }
            else
            {
                logger.LogWarning("Speech backup unreachable ({Reason}), trying primary", first.InnerException?.GetType().Name);
            }
            if (start >= 0) audio.Position = start;
            body.Reset();
            try
            {
                var answer = await SendToAsync(primaryFirst ? settings.BackupBaseUrl : settings.BaseUrl, path, body, contentType,
                    sample, run, ct);
                if (!primaryFirst) Interlocked.Exchange(ref primaryDownUntil, 0);
                return answer;
            }
            catch (WorkerUnreachable second)
            {
                if (!primaryFirst) SkipPrimary();
                throw new SpeechEngineException("Không kết nối được máy chuyển chữ chính lẫn máy dự phòng.", second.InnerException);
            }
        }
        catch (WorkerUnreachable failure)
        {
            throw new SpeechEngineException("Không kết nối được máy chủ nhận dạng giọng nói.", failure.InnerException);
        }
    }

    private void SkipPrimary() => Interlocked.Exchange(ref primaryDownUntil, (clock.GetUtcNow() + SkipPrimaryFor).UtcTicks);

    /// <summary>
    /// One worker's answer. Failing to reach it is <see cref="WorkerUnreachable"/>; an error it sent is mapped here, as the
    /// caller's to see. The caller's own cancellation goes through untouched.
    /// </summary>
    private async Task<HttpResponseMessage> SendToAsync(string baseUrl, string path, ReadMeter audio, string contentType,
        bool sample, Guid? run, CancellationToken ct)
    {
        var client = clients.CreateClient(HttpClientName);
        client.Timeout = TimeSpan.FromMinutes(options.Value.TimeoutMinutes);
        using var request = new HttpRequestMessage(HttpMethod.Post, new Uri(new Uri(baseUrl.TrimEnd('/') + "/"), path))
        {
            Content = new StreamContent(audio) { Headers = { ContentType = MediaTypeHeaderValue.Parse(contentType) } },
        };
        if (!string.IsNullOrEmpty(options.Value.ApiKey))
            request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", options.Value.ApiKey);
        if (run is { } id) runs[id] = baseUrl;
        HttpResponseMessage response;
        try
        {
            response = await client.SendAsync(request, HttpCompletionOption.ResponseHeadersRead, ct);
        }
        catch (HttpRequestException ex) when (!ct.IsCancellationRequested)
        {
            throw new WorkerUnreachable(ex, mayMove: true);
        }
        catch (TaskCanceledException ex) when (!ct.IsCancellationRequested)
        {
            // Before any audio went out this is the connect timeout; after, the request timeout of a worker that was working.
            throw new WorkerUnreachable(ex, mayMove: !audio.Started);
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

    private sealed class WorkerUnreachable(Exception inner, bool mayMove) : Exception(inner.Message, inner)
    {
        /// <summary>False for the request timeout after the audio went out: that worker was reached and working.</summary>
        public bool MayMove { get; } = mayMove;
    }

    /// <summary>
    /// The caller's audio, passed through unchanged, noting whether the HTTP stack began to read it: a connection that never
    /// opened read nothing, so even a stream that cannot seek may be sent again then. Disposing it leaves the caller's
    /// stream open, since the request content is disposed after the first try.
    /// </summary>
    private sealed class ReadMeter(Stream inner) : Stream
    {
        public bool Started { get; private set; }

        public void Reset() => Started = false;

        public override bool CanRead => inner.CanRead;
        public override bool CanSeek => inner.CanSeek;
        public override bool CanWrite => false;
        public override long Length => inner.Length;

        public override long Position
        {
            get => inner.Position;
            set => inner.Position = value;
        }

        public override int Read(byte[] buffer, int offset, int count)
        {
            Started = true;
            return inner.Read(buffer, offset, count);
        }

        public override int Read(Span<byte> buffer)
        {
            Started = true;
            return inner.Read(buffer);
        }

        public override Task<int> ReadAsync(byte[] buffer, int offset, int count, CancellationToken cancellationToken)
        {
            Started = true;
            return inner.ReadAsync(buffer, offset, count, cancellationToken);
        }

        public override ValueTask<int> ReadAsync(Memory<byte> buffer, CancellationToken cancellationToken = default)
        {
            Started = true;
            return inner.ReadAsync(buffer, cancellationToken);
        }

        public override long Seek(long offset, SeekOrigin origin) => inner.Seek(offset, origin);
        public override void Flush() { }
        public override void SetLength(long value) => throw new NotSupportedException();
        public override void Write(byte[] buffer, int offset, int count) => throw new NotSupportedException();

        protected override void Dispose(bool disposing) { }
    }

    /// <summary>
    /// The answer's body, read while it streams in: the connection can still drop, time out or carry something that
    /// is not the expected JSON. Each is the worker failing, told the caller the same way as one it reported.
    /// </summary>
    private static async Task<T> ReadBodyAsync<T>(HttpResponseMessage response, CancellationToken ct) where T : class
    {
        T? body;
        try
        {
            body = await response.Content.ReadFromJsonAsync<T>(Json, ct);
        }
        catch (Exception ex) when (ex is HttpRequestException or IOException or JsonException or NotSupportedException
            || ex is TaskCanceledException && !ct.IsCancellationRequested)
        {
            throw new SpeechEngineException("Máy chủ nhận dạng giọng nói trả kết quả không đọc được.", ex);
        }
        return body ?? throw new SpeechEngineException("Máy chủ nhận dạng giọng nói trả kết quả rỗng.");
    }

    private static async Task<string?> ReadErrorAsync(HttpResponseMessage response, CancellationToken ct)
    {
        try { return (await response.Content.ReadFromJsonAsync<ErrorBody>(Json, ct))?.Error; }
        catch (Exception ex) when (ex is JsonException or NotSupportedException) { return null; }
    }

    private sealed record ErrorBody(string? Error);
    private sealed record ProgressBody(string? State, int Percent);
    private sealed record EmbedBody(float[]? Embedding, string? Model, long SpeechMs);
    private sealed record SegmentBody(long StartMs, long EndMs, string? Speaker, string? Text, double? LevelDbfs,
        long[]? WordStartsMs, int[]? WordConfidence);
    private sealed record SpeakerBody(string? Label, long SpeechMs, float[]? Embedding);
    private sealed record TranscribeBody(long DurationMs, string? Language, string? AsrModel, string? EmbeddingModel,
        SegmentBody[]? Segments, SpeakerBody[]? Speakers, string? Context);
}
