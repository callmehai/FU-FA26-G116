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

        // The sender picks up whatever is still queued, oldest first.
        builder.HasIndex(e => new { e.Status, e.CreatedAt });

        // SetNull: the record that an invitation was sent outlives the meeting being deleted.
        builder.HasOne(e => e.Meeting)
            .WithMany()
            .HasForeignKey(e => e.MeetingId)
            .OnDelete(DeleteBehavior.SetNull);
    }
}
