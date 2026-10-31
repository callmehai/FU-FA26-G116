using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using TrustCabinet.Domain.Entities;

namespace TrustCabinet.Infrastructure.Configurations;

public class VotingSessionConfiguration : IEntityTypeConfiguration<VotingSession>
{
    public void Configure(EntityTypeBuilder<VotingSession> builder)
    {
        builder.Property(v => v.Title).HasMaxLength(300).IsRequired();
        builder.Property(v => v.Description).HasMaxLength(4000);
        builder.Property(v => v.Status).HasConversion<string>().HasMaxLength(32).IsRequired();
        builder.Property(v => v.Conclusion).HasConversion<string>().HasMaxLength(32);
        builder.Property(v => v.ConclusionNote).HasMaxLength(2000);

        // A text[] of role names, so the stored values read the same as every other enum column.
        builder.PrimitiveCollection(v => v.EligibleRoles)
            .IsRequired()
            .ElementType(e => e.HasConversion<string>());

        builder.HasIndex(v => new { v.MeetingId, v.CreatedAt });

        // At most one Open vote per meeting (BR-266) — enforced by the database, so two Chairs'
        // simultaneous Open requests cannot both succeed.
        builder.HasIndex(v => v.MeetingId).IsUnique().HasFilter("status = 'Open'");

        builder.HasOne(v => v.Meeting)
            .WithMany(m => m.VotingSessions)
            .HasForeignKey(v => v.MeetingId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasOne(v => v.CreatedBy)
            .WithMany()
            .HasForeignKey(v => v.CreatedById)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne(v => v.ConcludedBy)
            .WithMany()
            .HasForeignKey(v => v.ConcludedById)
            .OnDelete(DeleteBehavior.Restrict);
    }
}
