using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.ChangeTracking;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using TrustCabinet.Domain.Entities;
using TrustCabinet.Domain.Recordings;

namespace TrustCabinet.Infrastructure.Configurations;

public class SpeechClipConfiguration : IEntityTypeConfiguration<SpeechClip>
{
    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web);

    public void Configure(EntityTypeBuilder<SpeechClip> builder)
    {
        builder.Property(c => c.Text).HasMaxLength(8000);
        // jsonb: read whole with its sentence, never queried inside.
        builder.Property(c => c.Segments).HasColumnType("jsonb").HasConversion(
            segments => segments == null ? null : JsonSerializer.Serialize(segments, Json),
            json => json == null ? null : JsonSerializer.Deserialize<List<SpeechClipSegment>>(json, Json),
            new ValueComparer<List<SpeechClipSegment>?>(
                (a, b) => a == null ? b == null : b != null && a.SequenceEqual(b),
                list => list == null ? 0 : list.Aggregate(0, (hash, s) => HashCode.Combine(hash, s.GetHashCode())),
                list => list == null ? null : list.ToList()));
        builder.Property(c => c.ExclusionReason).HasConversion<string>().HasMaxLength(16);
        builder.Property(c => c.AudioPath).HasMaxLength(500);
        builder.Property(c => c.AudioContentType).HasMaxLength(64);
        builder.ToTable(t =>
        {
            t.HasCheckConstraint("ck_speech_clips_duration", "duration_ms > 0");
            // SCRUM-129: a sentence is a member's or a guest's, never both, never nobody's.
            t.HasCheckConstraint("ck_speech_clips_one_speaker", "(speaker_id IS NULL) <> (guest_invitation_id IS NULL)");
        });

        // A retry stores the sentence once, per speaker. NULLs never collide, so each index covers one kind of speaker.
        builder.HasIndex(c => new { c.MeetingId, c.SpeakerId, c.ClientClipId }).IsUnique();
        builder.HasIndex(c => new { c.MeetingId, c.GuestInvitationId, c.ClientClipId }).IsUnique()
            .HasFilter("guest_invitation_id IS NOT NULL");
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

        // An invitation is revoked, never deleted: what the guest said stays until the recordings' retention ends.
        builder.HasOne(c => c.GuestInvitation)
            .WithMany()
            .HasForeignKey(c => c.GuestInvitationId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne(c => c.EditedBy)
            .WithMany()
            .HasForeignKey(c => c.EditedById)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne(c => c.ExcludedBy)
            .WithMany()
            .HasForeignKey(c => c.ExcludedById)
            .OnDelete(DeleteBehavior.Restrict);
    }
}
