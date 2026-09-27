using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using TrustCabinet.Domain.Entities;

namespace TrustCabinet.Infrastructure.Configurations;

public class MeetingStatusHistoryConfiguration : IEntityTypeConfiguration<MeetingStatusHistory>
{
    public void Configure(EntityTypeBuilder<MeetingStatusHistory> builder)
    {
        builder.Property(h => h.FromStatus).HasConversion<string>().HasMaxLength(32);
        builder.Property(h => h.ToStatus).HasConversion<string>().HasMaxLength(32).IsRequired();
        builder.Property(h => h.Action).HasConversion<string>().HasMaxLength(64).IsRequired();
        builder.Property(h => h.Reason).HasMaxLength(1000);

        // The detail screen reads a meeting's timeline in order.
        builder.HasIndex(h => new { h.MeetingId, h.OccurredAt });

        builder.HasOne(h => h.Meeting)
            .WithMany(m => m.StatusHistory)
            .HasForeignKey(h => h.MeetingId)
            .OnDelete(DeleteBehavior.Restrict);

        // Restrict: the actor of a recorded transition must stay resolvable. Users are deactivated
        // rather than deleted precisely so this evidence keeps its attribution.
        builder.HasOne(h => h.Actor)
            .WithMany()
            .HasForeignKey(h => h.ActorId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne(h => h.AssignedApprover)
            .WithMany()
            .HasForeignKey(h => h.AssignedApproverId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}
