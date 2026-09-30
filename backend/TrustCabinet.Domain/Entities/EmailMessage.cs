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

    /// <summary>The meeting that caused this email, for tracing.</summary>
    public Guid? MeetingId { get; set; }
    public Meeting? Meeting { get; set; }
}
