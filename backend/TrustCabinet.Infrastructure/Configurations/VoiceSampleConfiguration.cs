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

        // ADR-0060: a sample the Secretary of a meeting took for the person names both; one the person took names neither.
        builder.HasOne(s => s.CollectedBy).WithMany().HasForeignKey(s => s.CollectedById).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne(s => s.Meeting).WithMany().HasForeignKey(s => s.MeetingId).OnDelete(DeleteBehavior.Restrict);
        builder.HasIndex(s => s.MeetingId);
        builder.ToTable(t => t.HasCheckConstraint("ck_voice_samples_collected_in_meeting",
            "(collected_by_id IS NULL) = (meeting_id IS NULL)"));
    }
}
