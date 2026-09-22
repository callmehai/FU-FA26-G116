using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using TrustCabinet.Domain.Entities;
using TrustCabinet.Domain.Enums;

namespace TrustCabinet.Infrastructure.Configurations;

public class MeetingParticipantConfiguration : IEntityTypeConfiguration<MeetingParticipant>
{
    public void Configure(EntityTypeBuilder<MeetingParticipant> builder)
    {
        builder.Property(p => p.Role).HasConversion<string>().HasMaxLength(32).IsRequired();
        builder.Property(p => p.Response).HasConversion<string>().HasMaxLength(32).IsRequired();

        // The database default is spelled out because EF once invented one. Adding this column
        // NOT NULL to a table that already had rows made it emit `DEFAULT ''`, and an empty string
        // is not a name of AttendanceStatus — the column round-trips through Enum.Parse, so any
        // row carrying it throws on read rather than at the insert that caused it (SCRUM-61).
        //
        // 'Unknown' rather than no default at all: a raw INSERT that omits the column then lands
        // on the same value EF writes for a new participant, instead of failing the NOT NULL. The
        // enum's zero value, so it agrees with what the entity holds before anything is set.
        builder.Property(p => p.Attendance)
            .HasConversion<string>()
            .HasMaxLength(32)
            .HasDefaultValue(AttendanceStatus.Unknown)
            .IsRequired();

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

        builder.HasOne(p => p.AttendanceRecordedBy)
            .WithMany()
            .HasForeignKey(p => p.AttendanceRecordedById)
            .OnDelete(DeleteBehavior.Restrict);
    }
}
