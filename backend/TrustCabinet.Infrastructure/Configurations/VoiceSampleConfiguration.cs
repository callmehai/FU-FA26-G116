using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using TrustCabinet.Domain.Entities;

namespace TrustCabinet.Infrastructure.Configurations;

public class VoiceSampleConfiguration : IEntityTypeConfiguration<VoiceSample>
{
    public void Configure(EntityTypeBuilder<VoiceSample> builder)
    {
        builder.Property(s => s.Ciphertext).IsRequired();
        builder.Property(s => s.EmbeddingModel).HasMaxLength(100).IsRequired();

        builder.HasIndex(s => s.UserId);

        builder.HasOne(s => s.User).WithMany().HasForeignKey(s => s.UserId).OnDelete(DeleteBehavior.Restrict);

        // A sample cannot outlive the consent it was given under.
        builder.HasOne(s => s.Consent).WithMany().HasForeignKey(s => s.ConsentId).OnDelete(DeleteBehavior.Cascade);
    }
}
