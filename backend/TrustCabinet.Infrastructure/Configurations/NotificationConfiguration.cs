using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using TrustCabinet.Domain.Entities;

namespace TrustCabinet.Infrastructure.Configurations;

public class NotificationConfiguration : IEntityTypeConfiguration<Notification>
{
    public void Configure(EntityTypeBuilder<Notification> builder)
    {
        builder.Property(n => n.Title).HasMaxLength(300).IsRequired();
        builder.Property(n => n.Body).HasMaxLength(2000);
        builder.Property(n => n.Type).HasConversion<string>().HasMaxLength(64).IsRequired();
        builder.Property(n => n.DeduplicationKey).HasMaxLength(150);
        builder.Property(n => n.TargetType).HasMaxLength(64);
        builder.Ignore(n => n.IsRead);

        // The bell reads "my unread, newest first" on every page load, so it gets its own index.
        builder.HasIndex(n => new { n.UserId, n.ReadAt, n.CreatedAt });
        builder.HasIndex(n => n.DeduplicationKey).IsUnique();
        builder.HasIndex(n => new { n.PushedAt, n.CreatedAt });

        builder.HasOne(n => n.User)
            .WithMany()
            .HasForeignKey(n => n.UserId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne(n => n.Meeting)
            .WithMany()
            .HasForeignKey(n => n.MeetingId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasOne<MeetingStatusHistory>()
            .WithMany()
            .HasForeignKey(n => n.OriginHistoryId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasIndex(n => new { n.OriginHistoryId, n.UserId, n.Type })
            .IsUnique()
            .HasFilter("origin_history_id IS NOT NULL");
    }
}
