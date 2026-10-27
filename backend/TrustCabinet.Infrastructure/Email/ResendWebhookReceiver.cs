using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using TrustCabinet.Domain.Enums;

namespace TrustCabinet.Infrastructure.Email;

public enum EmailWebhookOutcome
{
    /// <summary>No webhook secret is configured, so nothing can be trusted.</summary>
    NotConfigured,
    InvalidSignature,
    Malformed,
    Recorded,
    /// <summary>Genuine, but about nothing this app tracks: an event type it ignores, or a mail with no row.</summary>
    Ignored,
}

public interface IEmailWebhookReceiver
{
    Task<EmailWebhookOutcome> ReceiveResendAsync(
        byte[] body, string? id, string? timestamp, string? signature, CancellationToken ct = default);
}

/// <summary>
/// Records what Resend reports after a message was accepted — delivered, bounced, marked as spam (ADR-0031). The request
/// is anonymous at the HTTP layer; Resend proves itself with a Svix signature over the raw body.
/// </summary>
internal sealed class ResendWebhookReceiver(
    TrustCabinetDbContext db,
    IOptions<EmailDeliveryOptions> options,
    TimeProvider time,
    ILogger<ResendWebhookReceiver> logger) : IEmailWebhookReceiver
{
    /// <summary>Older than this, a signed request is treated as a replay. Svix's own libraries use the same window.</summary>
    internal static readonly TimeSpan Tolerance = TimeSpan.FromMinutes(5);

    public async Task<EmailWebhookOutcome> ReceiveResendAsync(
        byte[] body, string? id, string? timestamp, string? signature, CancellationToken ct = default)
    {
        var secret = options.Value.Resend.WebhookSecret;
        if (string.IsNullOrWhiteSpace(secret))
        {
            return EmailWebhookOutcome.NotConfigured;
        }

        if (!IsSigned(secret, id, timestamp, signature, body, time.GetUtcNow()))
        {
            return EmailWebhookOutcome.InvalidSignature;
        }

        ResendEvent? report;
        try
        {
            report = Parse(body);
        }
        catch (Exception exception) when (exception is JsonException or FormatException or InvalidOperationException)
        {
            logger.LogWarning(exception, "A signed Resend webhook {WebhookId} could not be read.", id);
            return EmailWebhookOutcome.Malformed;
        }

        if (report is null)
        {
            return EmailWebhookOutcome.Ignored;
        }

        // A report older than the one already stored arrived late (retries are not ordered) and is dropped, so a
        // delayed "delivery_delayed" never hides the "delivered" that followed it.
        var updated = await db.EmailMessages
            .Where(m => (m.ProviderMessageId == report.EmailId
                    || (report.RowId != null && m.Id == report.RowId))
                && (m.DeliveryStateAt == null || m.DeliveryStateAt <= report.OccurredAt))
            .ExecuteUpdateAsync(
                setters => setters
                    .SetProperty(m => m.DeliveryState, report.State)
                    .SetProperty(m => m.DeliveryStateAt, report.OccurredAt)
                    .SetProperty(m => m.DeliveryDetail, report.Detail)
                    .SetProperty(m => m.ProviderMessageId, report.EmailId)
                    .SetProperty(m => m.UpdatedAt, time.GetUtcNow()),
                ct);

        if (updated == 0)
        {
            // A password reset email has no row, and a report older than the stored one changes nothing.
            return EmailWebhookOutcome.Ignored;
        }

        if (report.State is EmailDeliveryState.Bounced or EmailDeliveryState.Complained or EmailDeliveryState.Failed)
        {
            logger.LogWarning(
                "Resend reported email {ProviderMessageId} as {DeliveryState}: {Detail}",
                report.EmailId, report.State, report.Detail);
        }

        return EmailWebhookOutcome.Recorded;
    }

    /// <summary>
    /// Svix's scheme: HMAC-SHA256, keyed with the base64 part of <c>whsec_…</c>, over <c>{id}.{timestamp}.{body}</c>.
    /// The header may carry several space-separated <c>v1,&lt;base64&gt;</c> entries during a secret rotation; one match
    /// is enough.
    /// </summary>
    internal static bool IsSigned(
        string secret, string? id, string? timestamp, string? signature, byte[] body, DateTimeOffset now)
    {
        if (string.IsNullOrEmpty(id) || string.IsNullOrEmpty(signature)
            || !long.TryParse(timestamp, out var seconds))
        {
            return false;
        }

        var sentAt = DateTimeOffset.FromUnixTimeSeconds(Math.Clamp(seconds, 0, 253402300799));
        if ((now - sentAt).Duration() > Tolerance)
        {
            return false;
        }

        byte[] key;
        try
        {
            key = Convert.FromBase64String(secret.StartsWith("whsec_", StringComparison.Ordinal) ? secret[6..] : secret);
        }
        catch (FormatException)
        {
            return false;
        }

        var prefix = Encoding.UTF8.GetBytes($"{id}.{timestamp}.");
        var signed = new byte[prefix.Length + body.Length];
        prefix.CopyTo(signed, 0);
        body.CopyTo(signed, prefix.Length);
        var expected = HMACSHA256.HashData(key, signed);

        Span<byte> given = stackalloc byte[64];
        foreach (var entry in signature.Split(' ', StringSplitOptions.RemoveEmptyEntries))
        {
            if (!entry.StartsWith("v1,", StringComparison.Ordinal))
            {
                continue;
            }

            if (Convert.TryFromBase64String(entry[3..], given, out var length)
                && CryptographicOperations.FixedTimeEquals(given[..length], expected))
            {
                return true;
            }
        }

        return false;
    }

    /// <summary>Null for the events this app does not track: sent (already known), opened, clicked, scheduled.</summary>
    internal static ResendEvent? Parse(byte[] body)
    {
        using var document = JsonDocument.Parse(body);
        var root = document.RootElement;
        EmailDeliveryState? state = root.GetProperty("type").GetString() switch
        {
            "email.delivered" => EmailDeliveryState.Delivered,
            "email.delivery_delayed" => EmailDeliveryState.Delayed,
            "email.bounced" => EmailDeliveryState.Bounced,
            "email.complained" => EmailDeliveryState.Complained,
            "email.suppressed" => EmailDeliveryState.Suppressed,
            "email.failed" => EmailDeliveryState.Failed,
            _ => null,
        };
        if (state is null)
        {
            return null;
        }

        var data = root.GetProperty("data");
        var emailId = data.GetProperty("email_id").GetString();
        if (string.IsNullOrWhiteSpace(emailId))
        {
            throw new FormatException("The event names no email_id.");
        }

        Guid? rowId = data.TryGetProperty("tags", out var tags)
            && tags.ValueKind == JsonValueKind.Object
            && tags.TryGetProperty("email_message_id", out var tag)
            && Guid.TryParseExact(tag.GetString(), "N", out var parsed)
                ? parsed
                : null;

        return new ResendEvent(
            emailId,
            rowId,
            state.Value,
            root.GetProperty("created_at").GetDateTimeOffset(),
            Detail(data));
    }

    private static string Detail(JsonElement data)
    {
        var detail = string.Empty;
        if (data.TryGetProperty("bounce", out var bounce) && bounce.ValueKind == JsonValueKind.Object)
        {
            detail = string.Join(" ", new[] { Text(bounce, "type"), Text(bounce, "subType"), Text(bounce, "message") }
                .Where(part => part.Length > 0));
        }
        else if (data.TryGetProperty("failed", out var failed) && failed.ValueKind == JsonValueKind.Object)
        {
            detail = Text(failed, "reason");
        }

        return detail.Length <= 2000 ? detail : detail[..2000];
    }

    private static string Text(JsonElement element, string name)
        => element.TryGetProperty(name, out var value) && value.ValueKind == JsonValueKind.String
            ? value.GetString()!.Trim()
            : string.Empty;

    internal sealed record ResendEvent(
        string EmailId, Guid? RowId, EmailDeliveryState State, DateTimeOffset OccurredAt, string Detail);
}
