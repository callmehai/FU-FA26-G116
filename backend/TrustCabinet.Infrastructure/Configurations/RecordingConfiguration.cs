using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using TrustCabinet.Domain.Entities;

namespace TrustCabinet.Infrastructure.Configurations;

public class RecordingConfiguration : IEntityTypeConfiguration<Recording>
{
    public void Configure(EntityTypeBuilder<Recording> builder)
    {
        builder.Property(r => r.FileName).HasMaxLength(300).IsRequired();
        builder.Property(r => r.StoragePath).HasMaxLength(500).IsRequired();
        builder.Property(r => r.ContentType).HasMaxLength(150).IsRequired();
        builder.Property(r => r.ConsentPurpose).HasMaxLength(1000);
        builder.Property(r => r.Source).HasConversion<string>().HasMaxLength(32).IsRequired();
        builder.Property(r => r.Status).HasConversion<string>().HasMaxLength(32).IsRequired();

        builder.HasIndex(r => r.MeetingId);

        // A scheduled job will need to find recordings whose retention has run out.
        builder.HasIndex(r => r.RetainUntil);

        builder.HasOne(r => r.Meeting)
            .WithMany(m => m.Recordings)
            .HasForeignKey(r => r.MeetingId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasOne(r => r.CreatedBy)
            .WithMany()
            .HasForeignKey(r => r.CreatedById)
            .OnDelete(DeleteBehavior.Restrict);
    }
}
