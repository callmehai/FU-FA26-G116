using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using TrustCabinet.Domain.Entities;

namespace TrustCabinet.Infrastructure.Configurations;

public class FolderConfiguration : IEntityTypeConfiguration<Folder>
{
    public void Configure(EntityTypeBuilder<Folder> builder)
    {
        builder.Property(f => f.Name).HasMaxLength(400).IsRequired();

        // One upload folder per person per meeting (BR-226, BR-227).
        builder.HasIndex(f => new { f.OwnerId, f.MeetingId }).IsUnique().HasFilter("meeting_id IS NOT NULL");

        builder.HasOne(f => f.Owner)
            .WithMany()
            .HasForeignKey(f => f.OwnerId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne(f => f.Parent)
            .WithMany()
            .HasForeignKey(f => f.ParentId)
            .OnDelete(DeleteBehavior.Restrict);

        // The folder belongs to its owner; the meeting only named it.
        builder.HasOne(f => f.Meeting)
            .WithMany()
            .HasForeignKey(f => f.MeetingId)
            .OnDelete(DeleteBehavior.SetNull);
    }
}
