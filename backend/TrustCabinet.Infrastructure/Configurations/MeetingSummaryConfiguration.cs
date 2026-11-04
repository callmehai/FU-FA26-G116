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
        builder.Property(s => s.Status).HasConversion<string>().HasMaxLength(32).IsRequired();
        builder.Property(s => s.ErrorMessage).HasMaxLength(2000);
        // The worker claims the oldest queued draft.
        builder.HasIndex(s => new { s.Status, s.RequestedAt });

        builder.HasOne(s => s.RequestedBy)
            .WithMany()
            .HasForeignKey(s => s.RequestedById)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne(s => s.EditedBy)
            .WithMany()
            .HasForeignKey(s => s.EditedById)
            .OnDelete(DeleteBehavior.Restrict);

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
