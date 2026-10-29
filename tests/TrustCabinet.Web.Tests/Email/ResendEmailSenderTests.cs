using System.Net;
using System.Text;
using System.Text.Json;
using Microsoft.Extensions.Options;
using TrustCabinet.Domain.Entities;
using TrustCabinet.Infrastructure.Email;

namespace TrustCabinet.Web.Tests.Email;

/// <summary>What goes to Resend's API, and which of its refusals the queue should retry (ADR-0031).</summary>
public sealed class ResendEmailSenderTests
{
    private readonly StubHandler _handler = new();

    private ResendEmailSender NewSender() => new(
        new StubClients(_handler),
        Options.Create(new EmailDeliveryOptions
        {
            Provider = EmailProvider.Resend,
            Resend = new ResendOptions
            {
                Enabled = true,
                ApiKey = "re_test_key",
                FromAddress = "no-reply@workspace-hub.space",
            },
        }));

    private static EmailMessage Invitation(string? calendar = null) => new()
    {
        DeduplicationKey = "test",
        ToAddress = "thanhvien@example.com",
        Subject = "Thư mời họp",
        Body = "Nội dung",
        CalendarAttachment = calendar,
    };

    [Fact]
    public async Task Posts_the_message_with_the_key_an_idempotency_key_and_a_user_agent()
    {
        _handler.Respond(HttpStatusCode.OK, """{"id":"4ef9a417-02e9-4d39-ad75-9611e0fcc33c"}""");
        var message = Invitation("BEGIN:VCALENDAR\r\nEND:VCALENDAR");

        var id = await NewSender().SendAsync(message);

        Assert.Equal("4ef9a417-02e9-4d39-ad75-9611e0fcc33c", id);
        var request = Assert.Single(_handler.Requests);
        Assert.Equal("https://api.resend.com/emails", request.Uri);
        Assert.Equal("Bearer re_test_key", request.Authorization);
        Assert.Equal($"email-message/{message.Id:N}", request.IdempotencyKey);
        Assert.False(string.IsNullOrWhiteSpace(request.UserAgent));

        using var body = JsonDocument.Parse(request.Body);
        var root = body.RootElement;
        Assert.Equal("TrustCabinet <no-reply@workspace-hub.space>", root.GetProperty("from").GetString());
        Assert.Equal("thanhvien@example.com", root.GetProperty("to")[0].GetString());
        Assert.Equal("Thư mời họp", root.GetProperty("subject").GetString());
        Assert.Equal("Nội dung", root.GetProperty("text").GetString());
        var attachment = root.GetProperty("attachments")[0];
        Assert.Equal("thu-moi-hop.ics", attachment.GetProperty("filename").GetString());
        Assert.StartsWith("text/calendar", attachment.GetProperty("content_type").GetString());
        Assert.Equal(
            "BEGIN:VCALENDAR\r\nEND:VCALENDAR",
            Encoding.UTF8.GetString(Convert.FromBase64String(attachment.GetProperty("content").GetString()!)));
        var tag = root.GetProperty("tags")[0];
        Assert.Equal("email_message_id", tag.GetProperty("name").GetString());
        Assert.Equal(message.Id.ToString("N"), tag.GetProperty("value").GetString());
    }

    [Fact]
    public async Task A_message_without_a_calendar_sends_no_attachments_field()
    {
        _handler.Respond(HttpStatusCode.OK, """{"id":"abc"}""");

        await NewSender().SendAsync(Invitation());

        using var body = JsonDocument.Parse(Assert.Single(_handler.Requests).Body);
        Assert.False(body.RootElement.TryGetProperty("attachments", out _));
    }

    [Theory]
    [InlineData(HttpStatusCode.BadRequest)]
    [InlineData(HttpStatusCode.UnprocessableEntity)]
    public async Task A_refused_message_is_a_permanent_failure(HttpStatusCode status)
    {
        _handler.Respond(status, """{"statusCode":422,"name":"validation_error","message":"Invalid `to` field."}""");

        var error = await Assert.ThrowsAsync<PermanentEmailFailureException>(() => NewSender().SendAsync(Invitation()));

        Assert.Contains("validation_error", error.Message, StringComparison.Ordinal);
    }

    [Theory]
    [InlineData(HttpStatusCode.Unauthorized)]
    [InlineData(HttpStatusCode.Forbidden)]
    [InlineData(HttpStatusCode.Conflict)]
    [InlineData(HttpStatusCode.TooManyRequests)]
    [InlineData(HttpStatusCode.InternalServerError)]
    [InlineData(HttpStatusCode.ServiceUnavailable)]
    public async Task An_account_or_service_problem_is_retried(HttpStatusCode status)
    {
        // A bad key or an unverified domain is fixed in configuration, and the same message then goes through.
        _handler.Respond(status, "<html>gateway</html>");

        var error = await Assert.ThrowsAsync<HttpRequestException>(() => NewSender().SendAsync(Invitation()));

        Assert.Equal(status, error.StatusCode);
    }

    private sealed record SeenRequest(
        string Uri, string? Authorization, string? IdempotencyKey, string? UserAgent, string Body);

    private sealed class StubHandler : HttpMessageHandler
    {
        private HttpStatusCode _status = HttpStatusCode.OK;
        private string _body = "{}";

        public List<SeenRequest> Requests { get; } = [];

        public void Respond(HttpStatusCode status, string body) => (_status, _body) = (status, body);

        protected override async Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request, CancellationToken cancellationToken)
        {
            Requests.Add(new SeenRequest(
                request.RequestUri!.ToString(),
                request.Headers.Authorization?.ToString(),
                request.Headers.TryGetValues("Idempotency-Key", out var keys) ? keys.Single() : null,
                request.Headers.UserAgent.ToString(),
                await request.Content!.ReadAsStringAsync(cancellationToken)));
            return new HttpResponseMessage(_status)
            {
                Content = new StringContent(_body, Encoding.UTF8, "application/json"),
            };
        }
    }

    private sealed class StubClients(HttpMessageHandler handler) : IHttpClientFactory
    {
        public HttpClient CreateClient(string name) => new(handler, disposeHandler: false);
    }
}
