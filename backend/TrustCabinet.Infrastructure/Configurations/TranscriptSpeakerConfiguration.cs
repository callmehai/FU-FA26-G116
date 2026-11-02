using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using TrustCabinet.Domain.Entities;

namespace TrustCabinet.Infrastructure.Configurations;

public class TranscriptSpeakerConfiguration : IEntityTypeConfiguration<TranscriptSpeaker>
{
    public void Configure(EntityTypeBuilder<TranscriptSpeaker> builder)
    {
        builder.Property(s => s.Label).HasMaxLength(100).IsRequired();

        builder.HasIndex(s => new { s.TranscriptId, s.Label }).IsUnique();
        builder.HasIndex(s => s.SuggestedUserId);

        builder.HasOne(s => s.Transcript).WithMany(t => t.Speakers).HasForeignKey(s => s.TranscriptId)
            .OnDelete(DeleteBehavior.Cascade);
        builder.HasOne(s => s.SuggestedUser).WithMany().HasForeignKey(s => s.SuggestedUserId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne(s => s.AssignedUser).WithMany().HasForeignKey(s => s.AssignedUserId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne(s => s.AssignedGuest).WithMany().HasForeignKey(s => s.AssignedGuestId).OnDelete(DeleteBehavior.Restrict);
        builder.ToTable(t => t.HasCheckConstraint("ck_transcript_speakers_one_assignee",
            "assigned_user_id IS NULL OR assigned_guest_id IS NULL"));
        builder.HasOne(s => s.AssignedBy).WithMany().HasForeignKey(s => s.AssignedById).OnDelete(DeleteBehavior.Restrict);
    }
}
