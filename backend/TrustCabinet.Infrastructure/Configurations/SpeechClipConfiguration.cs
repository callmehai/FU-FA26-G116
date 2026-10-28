using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using TrustCabinet.Domain.Entities;

namespace TrustCabinet.Infrastructure.Configurations;

public class SpeechClipConfiguration : IEntityTypeConfiguration<SpeechClip>
{
    public void Configure(EntityTypeBuilder<SpeechClip> builder)
    {
        builder.Property(c => c.Text).HasMaxLength(8000);
        builder.ToTable(t => t.HasCheckConstraint("ck_speech_clips_duration", "duration_ms > 0"));

        builder.HasIndex(c => new { c.MeetingId, c.SpeakerId, c.ClientClipId }).IsUnique();
        // The live view reads what changed since its last read; the transcript reads in spoken order.
        builder.HasIndex(c => new { c.MeetingId, c.UpdatedAt });
        builder.HasIndex(c => new { c.MeetingId, c.StartedAt });

        builder.HasOne(c => c.Meeting)
            .WithMany()
            .HasForeignKey(c => c.MeetingId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasOne(c => c.Speaker)
            .WithMany()
            .HasForeignKey(c => c.SpeakerId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne(c => c.EditedBy)
            .WithMany()
            .HasForeignKey(c => c.EditedById)
            .OnDelete(DeleteBehavior.Restrict);
    }
}
