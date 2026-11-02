using System.Net;
using System.Text;
using Microsoft.Extensions.Options;
using TrustCabinet.Domain.Transcription;
using TrustCabinet.Infrastructure.Transcription;

namespace TrustCabinet.Web.Tests.Recordings;

/// <summary>
/// An answer that arrives but cannot be read is the worker failing, told the caller like one it reported: otherwise a
/// stray JsonException or a connection dropped mid-body escapes the callers, which only expect SpeechEngineException.
/// </summary>
public sealed class SpeechWorkerClientTests
{
    private sealed class Handler(Func<HttpResponseMessage> answer) : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
            => Task.FromResult(answer());
    }

    private sealed class Clients(HttpMessageHandler handler) : IHttpClientFactory
    {
        public HttpClient CreateClient(string name) => new(handler, disposeHandler: false);
    }

    /// <summary>A body that breaks off after its first bytes, as when the worker dies mid-answer.</summary>
    private sealed class BrokenStream() : MemoryStream("{\"durationMs\":"u8.ToArray())
    {
        public override ValueTask<int> ReadAsync(Memory<byte> buffer, CancellationToken cancellationToken = default)
            => Position < Length ? base.ReadAsync(buffer, cancellationToken) : throw new IOException("Connection reset by peer");
    }

    private static SpeechWorkerClient Client(Func<HttpResponseMessage> answer)
        => new(new Clients(new Handler(answer)), Options.Create(new SpeechOptions { BaseUrl = "http://speech.test" }));

    private static Task<SpeechTranscription> Transcribe(SpeechWorkerClient client)
        => client.TranscribeAsync(new MemoryStream([1, 2, 3]), "audio/wav", diarize: true, clip: false, CancellationToken.None);

    [Fact]
    public async Task An_answer_that_is_not_json_is_a_speech_engine_failure()
    {
        var client = Client(() => new HttpResponseMessage(HttpStatusCode.OK)
        {
            Content = new StringContent("<html>502 Bad Gateway</html>", Encoding.UTF8, "application/json"),
        });
        var failure = await Assert.ThrowsAsync<SpeechEngineException>(() => Transcribe(client));
        Assert.Contains("không đọc được", failure.UserMessage);
    }

    [Fact]
    public async Task An_answer_cut_off_mid_body_is_a_speech_engine_failure()
    {
        var client = Client(() => new HttpResponseMessage(HttpStatusCode.OK)
        {
            Content = new StreamContent(new BrokenStream()) { Headers = { { "Content-Type", "application/json" } } },
        });
        await Assert.ThrowsAsync<SpeechEngineException>(() => Transcribe(client));
        await Assert.ThrowsAsync<SpeechEngineException>(
            () => client.EmbedAsync(new MemoryStream([1, 2, 3]), "audio/webm", CancellationToken.None));
    }
}
