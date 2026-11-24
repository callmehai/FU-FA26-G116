using System.Net;
using System.Text;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using TrustCabinet.Application.Transcription;
using TrustCabinet.Application.VoiceId;
using TrustCabinet.Infrastructure.Transcription;

namespace TrustCabinet.Web.Tests.Recordings;

/// <summary>
/// ADR-0081 — speech runs on the Mac first and on the server's worker when the Mac cannot be reached. Only a worker that
/// cannot be reached hands the request over; one that answered, even with an error, has given the answer.
/// </summary>
public sealed class SpeechWorkerBackupTests
{
    private const string Primary = "http://100.1.1.1:8090";
    private const string Backup = "http://speech:8090";

    [Fact]
    public async Task An_unreachable_primary_hands_the_audio_to_the_backup_and_is_skipped_for_a_while()
    {
        var net = new Network { PrimaryMode = Mode.Down };
        var clock = new Clock();
        var logs = new Logs();
        var client = NewClient(net, clock, logs: logs);

        var result = await Transcribe(client);

        Assert.Equal("dự phòng", result.Segments.Single().Text);
        Assert.Equal(["primary", "backup"], net.Calls);
        Assert.Equal([1, 2, 3], net.Bodies["backup"]);
        Assert.Equal("Bearer k", net.Keys["backup"]);
        Assert.Single(logs.Lines, line => line.StartsWith("Speech primary unreachable", StringComparison.Ordinal));

        // The next sentence goes straight to the backup: no second connect timeout on the Mac.
        net.Calls.Clear();
        net.PrimaryMode = Mode.Ok;
        await Transcribe(client);
        Assert.Equal(["backup"], net.Calls);

        // Two minutes on, the Mac is asked first again.
        net.Calls.Clear();
        clock.Now += SpeechWorkerClient.SkipPrimaryFor;
        Assert.Equal("chính", (await Transcribe(client)).Segments.Single().Text);
        Assert.Equal(["primary"], net.Calls);
    }

    [Theory]
    [InlineData(HttpStatusCode.BadRequest, "unreadable_audio")]
    [InlineData(HttpStatusCode.Unauthorized, "unauthorized")]
    [InlineData(HttpStatusCode.InternalServerError, "internal")]
    [InlineData(HttpStatusCode.ServiceUnavailable, "busy")]
    public async Task A_primary_that_answers_with_an_error_is_not_retried_on_the_backup(HttpStatusCode status, string error)
    {
        var net = new Network { PrimaryMode = Mode.Status, PrimaryStatus = status, PrimaryError = error };
        await Assert.ThrowsAsync<SpeechEngineException>(() => Transcribe(NewClient(net, new Clock())));
        Assert.Equal(["primary"], net.Calls);
    }

    [Fact]
    public async Task A_voice_sample_follows_the_same_rule_and_a_turned_down_sample_is_not_retried()
    {
        var down = new Network { PrimaryMode = Mode.Down };
        var embedding = await NewClient(down, new Clock()).EmbedAsync(new MemoryStream([1, 2, 3]), "audio/webm", default);
        Assert.Equal("eres2net-base-200k@e2d2048292e0", embedding.Model);
        Assert.Equal(["primary", "backup"], down.Calls);
        Assert.Equal([1, 2, 3], down.Bodies["backup"]);

        var rejecting = new Network { PrimaryMode = Mode.Status, PrimaryStatus = HttpStatusCode.UnprocessableEntity, PrimaryError = "too_short" };
        var rejected = await Assert.ThrowsAsync<VoiceSampleRejectedException>(
            () => NewClient(rejecting, new Clock()).EmbedAsync(new MemoryStream([1, 2, 3]), "audio/webm", default));
        Assert.Equal("too_short", rejected.Reason);
        Assert.Equal(["primary"], rejecting.Calls);
    }

    [Fact]
    public async Task A_primary_that_drops_mid_run_hands_the_rewound_audio_to_the_backup()
    {
        var net = new Network { PrimaryMode = Mode.DropAfterBody };
        var result = await Transcribe(NewClient(net, new Clock()));
        Assert.Equal("dự phòng", result.Segments.Single().Text);
        Assert.Equal(["primary", "backup"], net.Calls);
        Assert.Equal([1, 2, 3], net.Bodies["primary"]);
        Assert.Equal([1, 2, 3], net.Bodies["backup"]);
    }

    [Fact]
    public async Task Audio_that_cannot_be_rewound_moves_only_when_none_of_it_was_sent()
    {
        var neverSent = new Network { PrimaryMode = Mode.Down };
        await NewClient(neverSent, new Clock()).TranscribeAsync(new OneWay([1, 2, 3]), "audio/wav", false, true, default);
        Assert.Equal(["primary", "backup"], neverSent.Calls);
        Assert.Equal([1, 2, 3], neverSent.Bodies["backup"]);

        var halfSent = new Network { PrimaryMode = Mode.DropAfterBody };
        await Assert.ThrowsAsync<SpeechEngineException>(
            () => NewClient(halfSent, new Clock()).TranscribeAsync(new OneWay([1, 2, 3]), "audio/wav", false, true, default));
        Assert.Equal(["primary"], halfSent.Calls);
    }

    [Fact]
    public async Task A_connect_timeout_moves_but_the_request_timeout_of_a_working_primary_does_not()
    {
        var connect = new Network { PrimaryMode = Mode.TimeoutBeforeBody };
        await Transcribe(NewClient(connect, new Clock()));
        Assert.Equal(["primary", "backup"], connect.Calls);

        var working = new Network { PrimaryMode = Mode.TimeoutAfterBody };
        await Assert.ThrowsAsync<SpeechEngineException>(() => Transcribe(NewClient(working, new Clock())));
        Assert.Equal(["primary"], working.Calls);
    }

    [Fact]
    public async Task Progress_is_asked_of_the_worker_running_the_run()
    {
        var net = new Network { PrimaryMode = Mode.Down, HoldBackup = new TaskCompletionSource() };
        var client = NewClient(net, new Clock());
        var run = Guid.CreateVersion7();

        var transcribing = client.TranscribeAsync(new MemoryStream([1, 2, 3]), "audio/wav", true, false, default, run: run);
        await net.BackupReached.Task.WaitAsync(TimeSpan.FromSeconds(5));
        var progress = await client.ProgressAsync(run, default);
        Assert.Equal(new SpeechProgress(SpeechRunState.Running, 40), progress);
        Assert.Equal("backup", net.Calls[^1]);

        net.HoldBackup.SetResult();
        await transcribing;

        // Once the run is over this client no longer knows it: the main worker is asked, as before.
        net.PrimaryMode = Mode.Ok;
        await client.ProgressAsync(run, default);
        Assert.Equal("primary", net.Calls[^1]);
    }

    [Fact]
    public async Task Both_workers_down_says_so_and_without_a_backup_nothing_changes()
    {
        var both = new Network { PrimaryMode = Mode.Down, BackupMode = Mode.Down };
        var failure = await Assert.ThrowsAsync<SpeechEngineException>(() => Transcribe(NewClient(both, new Clock())));
        Assert.Equal("Không kết nối được máy chuyển chữ chính lẫn máy dự phòng.", failure.UserMessage);

        var alone = new Network { PrimaryMode = Mode.Down };
        var single = await Assert.ThrowsAsync<SpeechEngineException>(() => Transcribe(NewClient(alone, new Clock(), backup: "")));
        Assert.Equal("Không kết nối được máy chủ nhận dạng giọng nói.", single.UserMessage);
        Assert.Equal(["primary"], alone.Calls);
    }

    [Fact]
    public async Task A_run_the_caller_cancels_is_not_handed_to_the_backup()
    {
        var net = new Network { PrimaryMode = Mode.CancelCaller };
        using var stop = new CancellationTokenSource();
        net.Cancel = stop;
        await Assert.ThrowsAnyAsync<OperationCanceledException>(
            () => NewClient(net, new Clock()).TranscribeAsync(new MemoryStream([1, 2, 3]), "audio/wav", true, false, stop.Token));
        Assert.Equal(["primary"], net.Calls);
    }

    private static Task<SpeechTranscription> Transcribe(SpeechWorkerClient client)
        => client.TranscribeAsync(new MemoryStream([1, 2, 3]), "audio/wav", diarize: false, clip: true, CancellationToken.None);

    private static SpeechWorkerClient NewClient(Network net, Clock clock, string backup = Backup, Logs? logs = null)
        => new(new Factory(net), Options.Create(new SpeechOptions { BaseUrl = Primary, BackupBaseUrl = backup, ApiKey = "k" }),
            clock, logs ?? new Logs());

    private enum Mode { Ok, Down, DropAfterBody, TimeoutBeforeBody, TimeoutAfterBody, Status, CancelCaller }

    private sealed class Network : HttpMessageHandler
    {
        public Mode PrimaryMode { get; set; }
        public Mode BackupMode { get; set; }
        public HttpStatusCode PrimaryStatus { get; set; }
        public string PrimaryError { get; set; } = "";
        public TaskCompletionSource? HoldBackup { get; init; }
        public TaskCompletionSource BackupReached { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);
        public CancellationTokenSource? Cancel { get; set; }
        public List<string> Calls { get; } = [];
        public Dictionary<string, byte[]> Bodies { get; } = [];
        public Dictionary<string, string?> Keys { get; } = [];

        protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken ct)
        {
            var machine = request.RequestUri!.Host == "100.1.1.1" ? "primary" : "backup";
            lock (Calls) Calls.Add(machine);
            if (request.Method == HttpMethod.Get)
                return Json(HttpStatusCode.OK, """{"state":"running","percent":40}""");
            Keys[machine] = request.Headers.Authorization?.ToString();
            switch (machine == "primary" ? PrimaryMode : BackupMode)
            {
                case Mode.Down:
                    // Nothing read: a connection that never opened.
                    throw new HttpRequestException(HttpRequestError.ConnectionError, "No route to host");
                case Mode.TimeoutBeforeBody:
                    throw new TaskCanceledException("The connection attempt timed out", new TimeoutException());
                case Mode.DropAfterBody:
                    Bodies[machine] = await request.Content!.ReadAsByteArrayAsync(ct);
                    throw new HttpRequestException(HttpRequestError.ResponseEnded, "Connection reset by peer");
                case Mode.TimeoutAfterBody:
                    Bodies[machine] = await request.Content!.ReadAsByteArrayAsync(ct);
                    throw new TaskCanceledException("The request was canceled due to the configured HttpClient.Timeout",
                        new TimeoutException());
                case Mode.CancelCaller:
                    await Cancel!.CancelAsync();
                    throw new TaskCanceledException("cancelled", null, Cancel.Token);
                case Mode.Status:
                    return Json(PrimaryStatus, $$"""{"error":"{{PrimaryError}}"}""");
            }
            Bodies[machine] = await request.Content!.ReadAsByteArrayAsync(ct);
            if (machine == "backup" && HoldBackup is { } hold)
            {
                BackupReached.TrySetResult();
                await hold.Task;
            }
            var text = machine == "primary" ? "chính" : "dự phòng";
            return request.RequestUri.AbsolutePath == "/v1/embed"
                ? Json(HttpStatusCode.OK, """{"embedding":[0.1,0.2],"model":"eres2net-base-200k@e2d2048292e0","speechMs":9000}""")
                : Json(HttpStatusCode.OK, $$"""
                    {"durationMs":1000,"language":"vi","asrModel":"a","embeddingModel":"e",
                     "segments":[{"startMs":0,"endMs":900,"speaker":null,"text":"{{text}}"}],"speakers":[]}
                    """);
        }

        private static HttpResponseMessage Json(HttpStatusCode status, string body)
            => new(status) { Content = new StringContent(body, Encoding.UTF8, "application/json") };
    }

    /// <summary>Audio that can only be read once, front to back.</summary>
    private sealed class OneWay(byte[] bytes) : MemoryStream(bytes)
    {
        public override bool CanSeek => false;
    }

    private sealed class Factory(HttpMessageHandler handler) : IHttpClientFactory
    {
        public HttpClient CreateClient(string name) => new(handler, disposeHandler: false);
    }

    private sealed class Clock : TimeProvider
    {
        public DateTimeOffset Now { get; set; } = new(2026, 11, 24, 2, 0, 0, TimeSpan.Zero);

        public override DateTimeOffset GetUtcNow() => Now;
    }

    private sealed class Logs : ILogger<SpeechWorkerClient>
    {
        public List<string> Lines { get; } = [];

        public IDisposable? BeginScope<TState>(TState state) where TState : notnull => null;

        public bool IsEnabled(LogLevel logLevel) => true;

        public void Log<TState>(LogLevel logLevel, EventId eventId, TState state, Exception? exception,
            Func<TState, Exception?, string> formatter) => Lines.Add(formatter(state, exception));
    }
}
