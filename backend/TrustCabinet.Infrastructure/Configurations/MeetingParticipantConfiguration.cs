using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using TrustCabinet.Domain.Entities;

namespace TrustCabinet.Infrastructure.Configurations;

public class MeetingParticipantConfiguration : IEntityTypeConfiguration<MeetingParticipant>
{
    public void Configure(EntityTypeBuilder<MeetingParticipant> builder)
    {
        builder.Property(p => p.Role).HasConversion<string>().HasMaxLength(32).IsRequired();
        builder.Property(p => p.Response).HasConversion<string>().HasMaxLength(32).IsRequired();

        // One row per person per meeting: inviting the same person twice is a bug, not a feature.
        builder.HasIndex(p => new { p.MeetingId, p.UserId }).IsUnique();

        builder.HasOne(p => p.Meeting)
            .WithMany(m => m.Participants)
            .HasForeignKey(p => p.MeetingId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasOne(p => p.User)
            .WithMany(u => u.Participations)
            .HasForeignKey(p => p.UserId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne(p => p.DelegatedToUser)
            .WithMany()
            .HasForeignKey(p => p.DelegatedToUserId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}
