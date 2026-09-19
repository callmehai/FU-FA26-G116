using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using TrustCabinet.Domain.Entities;

namespace TrustCabinet.Infrastructure.Configurations;

public class VoteReceiptConfiguration : IEntityTypeConfiguration<VoteReceipt>
{
    public void Configure(EntityTypeBuilder<VoteReceipt> builder)
    {
        // One person, one vote — enforced by the database, not by a check in application code that
        // two simultaneous requests could both slip past.
        builder.HasIndex(r => new { r.VotingSessionId, r.VoterId }).IsUnique();

        builder.HasOne(r => r.VotingSession)
            .WithMany(v => v.Receipts)
            .HasForeignKey(r => r.VotingSessionId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasOne(r => r.Voter)
            .WithMany()
            .HasForeignKey(r => r.VoterId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}
