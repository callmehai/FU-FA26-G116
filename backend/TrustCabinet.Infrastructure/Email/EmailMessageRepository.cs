using Microsoft.EntityFrameworkCore;
using TrustCabinet.Application.Email;
using TrustCabinet.Domain.Entities;

namespace TrustCabinet.Infrastructure.Email;

public sealed class EmailMessageRepository(TrustCabinetDbContext db) : IEmailMessageRepository
{
    public Task<bool> ExistsWithDeduplicationKeyAsync(string deduplicationKey, CancellationToken ct = default)
        => db.EmailMessages.AnyAsync(e => e.DeduplicationKey == deduplicationKey, ct);

    public void Add(EmailMessage message) => db.EmailMessages.Add(message);
}
