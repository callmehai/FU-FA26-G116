using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using TrustCabinet.Domain.Entities;

namespace TrustCabinet.Infrastructure.Configurations;

public class VoiceConsentConfiguration : IEntityTypeConfiguration<VoiceConsent>
{
    public void Configure(EntityTypeBuilder<VoiceConsent> builder)
    {
        builder.Property(c => c.NoticeVersion).HasMaxLength(32).IsRequired();
        builder.Property(c => c.Purpose).HasMaxLength(1000).IsRequired();

        // One standing consent per person, held by the database rather than by a check two requests could both pass.
        builder.HasIndex(c => c.UserId).IsUnique().HasFilter("withdrawn_at IS NULL");

        builder.HasOne(c => c.User).WithMany().HasForeignKey(c => c.UserId).OnDelete(DeleteBehavior.Restrict);
    }
}
