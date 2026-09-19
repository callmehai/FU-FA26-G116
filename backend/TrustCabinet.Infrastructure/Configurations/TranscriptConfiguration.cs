using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using TrustCabinet.Domain.Entities;

namespace TrustCabinet.Infrastructure.Configurations;

public class TranscriptConfiguration : IEntityTypeConfiguration<Transcript>
{
    public void Configure(EntityTypeBuilder<Transcript> builder)
    {
        builder.Property(t => t.Provider).HasMaxLength(100);
        builder.Property(t => t.Language).HasMaxLength(16).IsRequired();
        builder.Property(t => t.ErrorMessage).HasMaxLength(2000);
        builder.Property(t => t.Status).HasConversion<string>().HasMaxLength(32).IsRequired();

        // One transcript per recording. Re-running replaces this row rather than adding a second.
        builder.HasIndex(t => t.RecordingId).IsUnique();

        builder.HasOne(t => t.Recording)
            .WithOne(r => r.Transcript)
            .HasForeignKey<Transcript>(t => t.RecordingId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}
