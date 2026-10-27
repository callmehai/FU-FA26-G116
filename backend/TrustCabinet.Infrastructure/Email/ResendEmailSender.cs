using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using Microsoft.Extensions.Options;
using TrustCabinet.Domain.Entities;

namespace TrustCabinet.Infrastructure.Email;

/// <summary>
/// Sends through Resend's HTTP API (ADR-0031). Unlike SMTP, the API returns an id that the delivery webhook later
/// reports on, and an idempotency key lets the queue retry an attempt whose answer was lost without sending twice.
/// </summary>
internal sealed class ResendEmailSender(
    IHttpClientFactory clients,
    IOptions<EmailDeliveryOptions> options) : IEmailSender
{
    public const string HttpClientName = "Resend";

    /// <summary>Resend rejects a request without one.</summary>
    private const string UserAgent = "TrustCabinet/0.1";

    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web)
    {
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
    };

    public async Task<string?> SendAsync(EmailMessage message, CancellationToken ct = default)
    {
        var resend = options.Value.Resend;
        using var request = new HttpRequestMessage(HttpMethod.Post, new Uri(new Uri(resend.ApiUrl), "emails"))
        {
            Content = JsonContent.Create(BuildRequest(message, resend), options: Json),
        };
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", resend.ApiKey);
        request.Headers.UserAgent.ParseAdd(UserAgent);
        // Resend remembers the key for 24 hours and answers a repeat with the first result instead of sending again,
        // which covers the queue retrying after a timeout that hid an accepted send. Retries end well inside that.
        request.Headers.Add("Idempotency-Key", IdempotencyKey(message));

        using var response = await clients.CreateClient(HttpClientName).SendAsync(request, ct);
        if (response.IsSuccessStatusCode)
        {
            var accepted = await response.Content.ReadFromJsonAsync<SendResponse>(Json, ct);
            return string.IsNullOrWhiteSpace(accepted?.Id)
                ? throw new InvalidOperationException("Resend accepted the email but returned no id.")
                : accepted.Id;
        }

        var error = await ReadErrorAsync(response, ct);
        var description = $"Resend answered {(int)response.StatusCode} {error.Name}: {error.Message}";
        throw IsPermanent(response.StatusCode)
            ? new PermanentEmailFailureException(description)
            : new HttpRequestException(description, inner: null, response.StatusCode);
    }

    internal static string IdempotencyKey(EmailMessage message) => $"email-message/{message.Id:N}";

    /// <summary>
    /// 400 and 422 say the message itself is wrong. 401/403 (a bad key, an unverified domain), 409, 429 (rate or daily
    /// quota) and 5xx are about the account or the moment, and the same message succeeds once that is fixed.
    /// </summary>
    internal static bool IsPermanent(HttpStatusCode status)
        => status is HttpStatusCode.BadRequest or HttpStatusCode.UnprocessableEntity;

    internal static SendRequest BuildRequest(EmailMessage message, ResendOptions resend) => new(
        From: $"{resend.FromName} <{resend.FromAddress}>",
        To: [message.ToAddress],
        Subject: message.Subject,
        Text: message.Body,
        Attachments: string.IsNullOrEmpty(message.CalendarAttachment)
            ? null
            :
            [
                new Attachment(
                    Filename: "thu-moi-hop.ics",
                    Content: Convert.ToBase64String(Encoding.UTF8.GetBytes(message.CalendarAttachment)),
                    ContentType: "text/calendar; charset=utf-8; method=REQUEST"),
            ],
        // Lets the webhook find the row even if storing the returned id was lost.
        Tags: [new Tag("email_message_id", message.Id.ToString("N"))]);

    private static async Task<ErrorResponse> ReadErrorAsync(HttpResponseMessage response, CancellationToken ct)
    {
        var body = await response.Content.ReadAsStringAsync(ct);
        try
        {
            var error = JsonSerializer.Deserialize<ErrorResponse>(body, Json);
            if (error is { Name: not null } or { Message: not null })
            {
                return error;
            }
        }
        catch (JsonException)
        {
            // Not Resend's JSON — a proxy's HTML page, say. The raw text below is all there is.
        }

        return new ErrorResponse("unknown_error", body.Length <= 500 ? body : body[..500]);
    }

    internal sealed record SendRequest(
        string From,
        string[] To,
        string Subject,
        string Text,
        Attachment[]? Attachments,
        Tag[] Tags);

    internal sealed record Attachment(
        string Filename,
        string Content,
        [property: JsonPropertyName("content_type")] string ContentType);

    internal sealed record Tag(string Name, string Value);

    private sealed record SendResponse(string? Id);

    private sealed record ErrorResponse(string? Name, string? Message);
}
