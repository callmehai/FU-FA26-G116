using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using TrustCabinet.Domain.Entities;

namespace TrustCabinet.Infrastructure.Configurations;

public class EmailMessageConfiguration : IEntityTypeConfiguration<EmailMessage>
{
    public void Configure(EntityTypeBuilder<EmailMessage> builder)
    {
        builder.Property(e => e.ToAddress).HasMaxLength(256).IsRequired();
        builder.Property(e => e.Subject).HasMaxLength(300).IsRequired();
        builder.Property(e => e.Body).HasMaxLength(50000);
        builder.Property(e => e.LastError).HasMaxLength(2000);
        builder.Property(e => e.Status).HasConversion<string>().HasMaxLength(32).IsRequired();
        builder.Property(e => e.MessageType).HasConversion<string>().HasMaxLength(64);
        builder.Property(e => e.PayloadSnapshot).HasMaxLength(50000);

        // The sender picks up whatever is still queued, oldest first.
        builder.HasIndex(e => new { e.Status, e.CreatedAt });

        // SetNull: the record that an invitation was sent outlives the meeting being deleted.
        builder.HasOne(e => e.Meeting)
            .WithMany()
            .HasForeignKey(e => e.MeetingId)
            .OnDelete(DeleteBehavior.SetNull);

        builder.HasOne(e => e.RecipientUser)
            .WithMany()
            .HasForeignKey(e => e.RecipientUserId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne<MeetingStatusHistory>()
            .WithMany()
            .HasForeignKey(e => e.OriginHistoryId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasIndex(e => new { e.OriginHistoryId, e.RecipientUserId, e.MessageType })
            .IsUnique()
            .HasFilter("origin_history_id IS NOT NULL");
    }
}
