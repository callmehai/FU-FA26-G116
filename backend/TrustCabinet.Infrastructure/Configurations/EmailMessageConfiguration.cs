using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using TrustCabinet.Domain.Entities;

namespace TrustCabinet.Infrastructure.Configurations;

public class EmailMessageConfiguration : IEntityTypeConfiguration<EmailMessage>
{
    public void Configure(EntityTypeBuilder<EmailMessage> builder)
    {
        builder.Property(e => e.MessageType).HasConversion<string>().HasMaxLength(64);
        builder.Property(e => e.PayloadSnapshot).HasMaxLength(50000);
        builder.HasOne(e => e.RecipientUser).WithMany().HasForeignKey(e => e.RecipientUserId)
            .OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<MeetingStatusHistory>().WithMany().HasForeignKey(e => e.OriginHistoryId)
            .OnDelete(DeleteBehavior.Restrict);
        builder.HasIndex(e => new { e.OriginHistoryId, e.RecipientUserId, e.MessageType })
            .IsUnique().HasFilter("origin_history_id IS NOT NULL");
        builder.Property(e => e.DeduplicationKey).HasMaxLength(512).IsRequired();
        builder.Property(e => e.ToAddress)
            .HasColumnType("citext")
            .HasMaxLength(256)
            .IsRequired();
        builder.Property(e => e.Subject).HasMaxLength(300).IsRequired();
        builder.Property(e => e.Body).HasMaxLength(50000);
        builder.Property(e => e.LastError).HasMaxLength(2000);
        builder.Property(e => e.Status).HasConversion<string>().HasMaxLength(32).IsRequired();

        // The sender picks up due messages and expired leases, oldest first.
        builder.HasIndex(e => new { e.Status, e.NextAttemptAt, e.CreatedAt });
        builder.HasIndex(e => e.LeaseExpiresAt);

        // One business event creates at most one message. A later reschedule, cancellation or
        // minutes event for the same meeting and recipient uses another key and remains allowed.
        builder.HasIndex(e => e.DeduplicationKey).IsUnique();
        builder.HasIndex(e => e.MeetingId);

        // SetNull: the record that an invitation was sent outlives the meeting being deleted.
        builder.HasOne(e => e.Meeting)
            .WithMany()
            .HasForeignKey(e => e.MeetingId)
            .OnDelete(DeleteBehavior.SetNull);
    }
}
