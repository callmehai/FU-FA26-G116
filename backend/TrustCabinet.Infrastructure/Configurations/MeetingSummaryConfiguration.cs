using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using TrustCabinet.Domain.Entities;

namespace TrustCabinet.Infrastructure.Configurations;

public class MeetingSummaryConfiguration : IEntityTypeConfiguration<MeetingSummary>
{
    public void Configure(EntityTypeBuilder<MeetingSummary> builder)
    {
        builder.Property(s => s.Content).HasMaxLength(50000);
        builder.Property(s => s.Model).HasMaxLength(100);

        builder.HasIndex(s => s.MeetingId).IsUnique();

        builder.HasOne(s => s.Meeting)
            .WithOne(m => m.Summary)
            .HasForeignKey<MeetingSummary>(s => s.MeetingId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasOne(s => s.ReviewedBy)
            .WithMany()
            .HasForeignKey(s => s.ReviewedById)
            .OnDelete(DeleteBehavior.Restrict);
    }
}
