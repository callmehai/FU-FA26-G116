using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using TrustCabinet.Domain.Entities;

namespace TrustCabinet.Infrastructure.Configurations;

public class CallSessionConfiguration : IEntityTypeConfiguration<CallSession>
{
    public void Configure(EntityTypeBuilder<CallSession> builder)
    {
        builder.Property(c => c.Kind).HasConversion<string>().HasMaxLength(32).IsRequired();
        builder.Property(c => c.Status).HasConversion<string>().HasMaxLength(32).IsRequired();

        builder.HasIndex(c => new { c.MeetingId, c.StartedAt });

        builder.HasOne(c => c.Meeting)
            .WithMany()
            .HasForeignKey(c => c.MeetingId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasOne(c => c.StartedBy)
            .WithMany()
            .HasForeignKey(c => c.StartedById)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne(c => c.Recording)
            .WithMany()
            .HasForeignKey(c => c.RecordingId)
            .OnDelete(DeleteBehavior.SetNull);
    }
}
