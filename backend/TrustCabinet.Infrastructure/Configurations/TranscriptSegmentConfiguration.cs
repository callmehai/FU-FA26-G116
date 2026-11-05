using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using TrustCabinet.Domain.Entities;

namespace TrustCabinet.Infrastructure.Configurations;

public class TranscriptSegmentConfiguration : IEntityTypeConfiguration<TranscriptSegment>
{
    public void Configure(EntityTypeBuilder<TranscriptSegment> builder)
    {
        builder.Property(s => s.Text).HasMaxLength(8000);
        builder.Property(s => s.SpeakerLabel).HasMaxLength(100);

        builder.HasIndex(s => new { s.TranscriptId, s.Position }).IsUnique();

        builder.HasOne(s => s.Transcript)
            .WithMany(t => t.Segments)
            .HasForeignKey(s => s.TranscriptId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasOne(s => s.SpeakerUser)
            .WithMany()
            .HasForeignKey(s => s.SpeakerUserId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne(s => s.SpeakerGuest)
            .WithMany()
            .HasForeignKey(s => s.SpeakerGuestId)
            .OnDelete(DeleteBehavior.Restrict);
        builder.ToTable(t => t.HasCheckConstraint("ck_transcript_segments_one_speaker",
            "speaker_user_id IS NULL OR speaker_guest_id IS NULL"));

        builder.HasOne(s => s.EditedBy)
            .WithMany()
            .HasForeignKey(s => s.EditedById)
            .OnDelete(DeleteBehavior.Restrict);
    }
}
