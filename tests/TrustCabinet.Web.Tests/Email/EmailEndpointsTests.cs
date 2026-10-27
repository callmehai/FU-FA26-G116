using System.Net;
using System.Text;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using TrustCabinet.Infrastructure.Email;

namespace TrustCabinet.Web.Tests.Email;

/// <summary>The HTTP side of Resend's delivery webhook (ADR-0031): anonymous, signed, answered with bare status codes.</summary>
public sealed class EmailEndpointsTests
{
    private const string WebhookPath = "/api/email/resend/webhook";

    [Fact]
    public async Task Without_a_webhook_secret_the_webhook_is_503()
    {
        using var factory = new TrustCabinetApiFactory();

        var response = await factory.CreateClient().PostAsync(WebhookPath, new StringContent("{}"));

        Assert.Equal(HttpStatusCode.ServiceUnavailable, response.StatusCode);
    }

    [Fact]
    public async Task An_unsigned_request_is_401_without_a_sign_in()
    {
        using var factory = new TrustCabinetApiFactory().WithWebHostBuilder(builder =>
            builder.UseSetting("Email:Resend:WebhookSecret", ResendWebhookReceiverTests.Secret));

        var response = await factory.CreateClient().PostAsync(WebhookPath, new StringContent("{}"));

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Theory]
    [InlineData(EmailWebhookOutcome.Recorded, HttpStatusCode.OK)]
    [InlineData(EmailWebhookOutcome.Ignored, HttpStatusCode.OK)]
    [InlineData(EmailWebhookOutcome.Malformed, HttpStatusCode.BadRequest)]
    public async Task The_raw_body_and_svix_headers_reach_the_receiver(
        EmailWebhookOutcome outcome, HttpStatusCode expected)
    {
        var receiver = new RecordingReceiver(outcome);
        using var factory = WithReceiver(receiver);
        using var request = new HttpRequestMessage(HttpMethod.Post, WebhookPath)
        {
            Content = new StringContent("""{ "type" : "email.delivered" }""", Encoding.UTF8, "application/json"),
        };
        request.Headers.Add("svix-id", "msg_1");
        request.Headers.Add("svix-timestamp", "1793088000");
        request.Headers.Add("svix-signature", "v1,abc");

        var response = await factory.CreateClient().SendAsync(request);

        Assert.Equal(expected, response.StatusCode);
        // Byte for byte, spacing included: the signature covers exactly what was sent.
        Assert.Equal("""{ "type" : "email.delivered" }""", Encoding.UTF8.GetString(receiver.Body!));
        Assert.Equal(("msg_1", "1793088000", "v1,abc"), (receiver.Id, receiver.Timestamp, receiver.Signature));
    }

    [Fact]
    public async Task A_receiver_error_is_500_so_resend_retries()
    {
        using var factory = WithReceiver(new RecordingReceiver(EmailWebhookOutcome.Recorded) { Throws = true });

        var response = await factory.CreateClient().PostAsync(WebhookPath, new StringContent("{}"));

        Assert.Equal(HttpStatusCode.InternalServerError, response.StatusCode);
    }

    private static WebApplicationFactory<Program> WithReceiver(IEmailWebhookReceiver receiver)
        => new TrustCabinetApiFactory().WithWebHostBuilder(builder => builder.ConfigureServices(services =>
        {
            services.RemoveAll<IEmailWebhookReceiver>();
            services.AddSingleton(receiver);
        }));

    private sealed class RecordingReceiver(EmailWebhookOutcome outcome) : IEmailWebhookReceiver
    {
        public bool Throws { get; init; }

        public byte[]? Body { get; private set; }

        public string? Id { get; private set; }

        public string? Timestamp { get; private set; }

        public string? Signature { get; private set; }

        public Task<EmailWebhookOutcome> ReceiveResendAsync(
            byte[] body, string? id, string? timestamp, string? signature, CancellationToken ct = default)
        {
            if (Throws)
            {
                throw new InvalidOperationException("database down");
            }

            (Body, Id, Timestamp, Signature) = (body, id, timestamp, signature);
            return Task.FromResult(outcome);
        }
    }
}
