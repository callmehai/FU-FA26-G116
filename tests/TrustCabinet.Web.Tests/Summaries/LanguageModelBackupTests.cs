using System.Net;
using System.Text;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using TrustCabinet.Application.Summaries;
using TrustCabinet.Infrastructure.Summaries;

namespace TrustCabinet.Web.Tests.Summaries;

/// <summary>ADR-0063 — the backup in-house machine takes a draft the main machine cannot.</summary>
public sealed class LanguageModelBackupTests
{
    private const string Main = "http://100.1.1.1:11434/v1";
    private const string Backup = "http://100.2.2.2:11434/v1";

    [Fact]
    public async Task An_unreachable_main_machine_hands_the_call_to_the_backup_and_is_skipped_for_a_while()
    {
        var net = new Network { MainDown = true };
        var clock = new Clock();
        var model = NewClient(net, clock);

        Assert.Equal("từ máy dự phòng", await model.CompleteAsync("s", "u", default));
        Assert.Equal(["main", "backup"], net.Calls);

        // The next part goes straight to the backup: no second wait for the main machine's connect timeout.
        net.Calls.Clear();
        net.MainDown = false;
        await model.CompleteAsync("s", "u", default);
        Assert.Equal(["backup"], net.Calls);

        // Two minutes on, the main machine is asked first again.
        net.Calls.Clear();
        clock.Now += LanguageModelClient.SkipMainFor;
        Assert.Equal("từ máy chính", await model.CompleteAsync("s", "u", default));
        Assert.Equal(["main"], net.Calls);
    }

    [Fact]
    public async Task A_machine_that_answers_with_an_error_is_not_retried_on_the_backup()
    {
        var net = new Network { MainStatus = HttpStatusCode.InternalServerError };
        var model = NewClient(net, new Clock());

        var error = await Assert.ThrowsAsync<LanguageModelException>(() => model.CompleteAsync("s", "u", default));

        Assert.Equal("Máy AI trả lỗi 500.", error.Message);
        Assert.Equal(["main"], net.Calls);
    }

    [Fact]
    public async Task Both_machines_down_says_so_and_without_a_backup_nothing_changes()
    {
        var net = new Network { MainDown = true, BackupDown = true };
        var error = await Assert.ThrowsAsync<LanguageModelException>(() => NewClient(net, new Clock()).CompleteAsync("s", "u", default));
        Assert.Equal("Không kết nối được máy AI chính lẫn máy dự phòng. Cả hai có thể đang tắt.", error.Message);
        Assert.True(error.Unreachable);

        var alone = new Network { MainDown = true };
        var single = NewClient(alone, new Clock(), backup: "");
        Assert.False(single.HasBackup);
        var only = await Assert.ThrowsAsync<LanguageModelException>(() => single.CompleteAsync("s", "u", default));
        Assert.Equal("Không kết nối được máy AI. Máy chạy AI có thể đang tắt.", only.Message);
        Assert.Equal(["main"], alone.Calls);
    }

    private static LanguageModelClient NewClient(Network net, Clock clock, string backup = Backup)
        => new(new Factory(net), Options.Create(new LanguageModelOptions { BaseUrl = Main, BackupBaseUrl = backup, Model = "m" }),
            clock, NullLogger<LanguageModelClient>.Instance);

    private sealed class Network : HttpMessageHandler
    {
        public bool MainDown { get; set; }
        public bool BackupDown { get; set; }
        public HttpStatusCode MainStatus { get; set; } = HttpStatusCode.OK;
        public List<string> Calls { get; } = [];

        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken ct)
        {
            var main = request.RequestUri!.Host == "100.1.1.1";
            Calls.Add(main ? "main" : "backup");
            if (main ? MainDown : BackupDown) throw new HttpRequestException("Connection timed out");
            var status = main ? MainStatus : HttpStatusCode.OK;
            var text = main ? "từ máy chính" : "từ máy dự phòng";
            return Task.FromResult(new HttpResponseMessage(status)
            {
                Content = new StringContent($$$"""{"choices":[{"message":{"role":"assistant","content":"{{{text}}}"}}]}""",
                    Encoding.UTF8, "application/json"),
            });
        }
    }

    private sealed class Factory(HttpMessageHandler handler) : IHttpClientFactory
    {
        public HttpClient CreateClient(string name) => new(handler, disposeHandler: false);
    }

    private sealed class Clock : TimeProvider
    {
        public DateTimeOffset Now { get; set; } = new(2026, 11, 16, 2, 0, 0, TimeSpan.Zero);

        public override DateTimeOffset GetUtcNow() => Now;
    }
}
