using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using TrustCabinet.Domain.Entities;
using TrustCabinet.Domain.Enums;
using TrustCabinet.Domain.Participants;

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

        builder.Property(p => p.AssignmentStatus).HasConversion<string>().HasMaxLength(32).HasDefaultValue(AssignmentStatus.Current);
        builder.Property(p => p.InitialInvitationDeliveryKey).HasMaxLength(256);
        builder.HasIndex(p => new { p.MeetingId, p.UserId }).IsUnique().HasFilter("assignment_status = 'Current'");
        builder.HasIndex(p => p.MeetingId).IsUnique().HasDatabaseName("ux_current_meeting_chair")
            .HasFilter("assignment_status = 'Current' AND role = 'Chair'");
        builder.HasIndex(p => new { p.MeetingId, p.AssignmentStatus });
        builder.HasOne<User>().WithMany().HasForeignKey(p => p.RemovedById).OnDelete(DeleteBehavior.Restrict);
        builder.ToTable(t => t.HasCheckConstraint("ck_participant_removal", "(assignment_status = 'Current' AND removed_at IS NULL AND removed_by_id IS NULL) OR (assignment_status = 'Removed' AND removed_at IS NOT NULL AND removed_by_id IS NOT NULL)"));

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
