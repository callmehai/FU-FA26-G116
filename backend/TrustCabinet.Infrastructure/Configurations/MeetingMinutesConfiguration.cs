using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using TrustCabinet.Domain.Entities;

namespace TrustCabinet.Infrastructure.Configurations;

public class MeetingMinutesConfiguration : IEntityTypeConfiguration<MeetingMinutes>
{
    public void Configure(EntityTypeBuilder<MeetingMinutes> builder)
    {
        builder.Property(m => m.Content).HasMaxLength(100000);
        builder.Property(m => m.RejectionReason).HasMaxLength(2000);
        builder.Property(m => m.Status).HasConversion<string>().HasMaxLength(32).IsRequired();

        // A meeting can have several revisions, but only one of each number.
        builder.HasIndex(m => new { m.MeetingId, m.RevisionNumber }).IsUnique();

        builder.HasOne(m => m.Meeting)
            .WithMany(m => m.Minutes)
            .HasForeignKey(m => m.MeetingId)
            .OnDelete(DeleteBehavior.Cascade);

        // Restrict on the self-reference: a correction must never be able to erase the revision it
        // corrects, because the issued text is the thing people relied on.
        builder.HasOne(m => m.Supersedes)
            .WithMany()
            .HasForeignKey(m => m.SupersedesId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne(m => m.CreatedBy)
            .WithMany()
            .HasForeignKey(m => m.CreatedById)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne(m => m.ApprovedBy)
            .WithMany()
            .HasForeignKey(m => m.ApprovedById)
            .OnDelete(DeleteBehavior.Restrict);
    }
}
