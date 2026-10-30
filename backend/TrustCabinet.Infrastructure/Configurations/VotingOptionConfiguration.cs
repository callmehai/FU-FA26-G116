using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using TrustCabinet.Domain.Entities;

namespace TrustCabinet.Infrastructure.Configurations;

public class VotingOptionConfiguration : IEntityTypeConfiguration<VotingOption>
{
    public void Configure(EntityTypeBuilder<VotingOption> builder)
    {
        builder.Property(o => o.Label).HasMaxLength(200).IsRequired();
        builder.ToTable(t => t.HasCheckConstraint("ck_voting_options_ballot_count", "ballot_count >= 0"));

        // Options are written once when the session is created, so unlike agenda items there is no
        // reordering to trip over the uniqueness.
        builder.HasIndex(o => new { o.VotingSessionId, o.Position }).IsUnique();

        builder.HasOne(o => o.VotingSession)
            .WithMany(v => v.Options)
            .HasForeignKey(o => o.VotingSessionId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}
