using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using TrustCabinet.Domain.Entities;

namespace TrustCabinet.Infrastructure.Configurations;

public class MeetingConfiguration : IEntityTypeConfiguration<Meeting>
{
    public void Configure(EntityTypeBuilder<Meeting> builder)
    {
        builder.Property(m => m.ParticipantsRevision).HasDefaultValue(0L);
        builder.ToTable(t => t.HasCheckConstraint("ck_participants_revision", "participants_revision >= 0"));
        builder.Property(m => m.Title).HasMaxLength(300).IsRequired();
        builder.Property(m => m.Description).HasMaxLength(4000);
        builder.Property(m => m.Location).HasMaxLength(300);
        builder.Property(m => m.OnlineLink).HasMaxLength(1000);

        builder.Property(m => m.Status).HasConversion<string>().HasMaxLength(32).IsRequired();
        builder.Property(m => m.Sensitivity).HasConversion<string>().HasMaxLength(32).IsRequired();
        builder.Property(m => m.Mode).HasConversion<string>().HasMaxLength(32).IsRequired();

        // The list filters by status and sorts by start; the calendar and reminders read by start.
        builder.HasIndex(m => new { m.Status, m.PlannedStartTime });
        builder.HasIndex(m => m.PlannedStartTime);

        builder.HasOne(m => m.CreatedBy)
            .WithMany()
            .HasForeignKey(m => m.CreatedById)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne(m => m.AssignedApprover)
            .WithMany()
            .HasForeignKey(m => m.AssignedApproverId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}
