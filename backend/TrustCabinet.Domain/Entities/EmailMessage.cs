using TrustCabinet.Domain.Entities.Common;
using TrustCabinet.Domain.Enums;

namespace TrustCabinet.Domain.Entities;

/// <summary>
/// An email queued for sending (SCRUM-15). Stored rather than sent inline so that publishing a
/// meeting does not block on SMTP, a failed send can be retried, and someone can later answer
/// "was the invitation actually delivered?".
/// </summary>
public class EmailMessage : Entity
{
    /// <summary>
    /// Identifies the business event that created this message. Repeating the same event is
    /// idempotent, while a later event for the same meeting and recipient gets a different key.
    /// </summary>
    public required string DeduplicationKey { get; set; }

    public required string ToAddress { get; set; }

    public required string Subject { get; set; }

    public string Body { get; set; } = string.Empty;

    /// <summary>The calendar invitation (.ics) sent with a meeting invitation, when there is one.</summary>
    public string? CalendarAttachment { get; set; }

    public EmailStatus Status { get; set; } = EmailStatus.Queued;

    public int AttemptCount { get; set; }

    public string LastError { get; set; } = string.Empty;

    /// <summary>Earliest time at which a failed message may be attempted again.</summary>
    public DateTimeOffset? NextAttemptAt { get; set; }

    /// <summary>Identifies the worker attempt currently allowed to finalize this message.</summary>
    public Guid? LeaseToken { get; set; }

    /// <summary>Allows another worker to recover this message after a crash or forced shutdown.</summary>
    public DateTimeOffset? LeaseExpiresAt { get; set; }

    public DateTimeOffset? SentAt { get; set; }

    /// <summary>The provider's id for the accepted message (Resend's <c>email_id</c>); empty for SMTP.</summary>
    public string? ProviderMessageId { get; set; }

    /// <summary>The provider's latest report after sending, or null while none has arrived (ADR-0031).</summary>
    public EmailDeliveryState? DeliveryState { get; set; }

    /// <summary>When the provider produced <see cref="DeliveryState"/>; an older report never overwrites a newer one.</summary>
    public DateTimeOffset? DeliveryStateAt { get; set; }

    /// <summary>The receiving server's reason for a bounce or failure, as the provider passed it on.</summary>
    public string DeliveryDetail { get; set; } = string.Empty;

    /// <summary>The meeting that caused this email, for tracing.</summary>
    public Guid? MeetingId { get; set; }
    public Meeting? Meeting { get; set; }

}
