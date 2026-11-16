using TrustCabinet.Domain.Entities;

namespace TrustCabinet.Application.Email;

/// <summary>
/// The outgoing email queue, for the use cases already moved to Application that queue a message of their own
/// (the agenda presenter notice). The Email module itself moves at CA-17 (SCRUM-182), which may replace this port.
/// </summary>
public interface IEmailMessageRepository
{
    /// <summary>Whether a message with this deduplication key has already been queued.</summary>
    Task<bool> ExistsWithDeduplicationKeyAsync(string deduplicationKey, CancellationToken ct = default);

    /// <summary>Stages the message; it is written by the next <c>SaveChangesAsync</c>.</summary>
    void Add(EmailMessage message);
}
