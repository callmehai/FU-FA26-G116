using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using TrustCabinet.Domain.Entities;

namespace TrustCabinet.Infrastructure.Configurations;

public class SpeakingRequestConfiguration : IEntityTypeConfiguration<SpeakingRequest>
{
    public void Configure(EntityTypeBuilder<SpeakingRequest> builder)
    {
        builder.Property(r => r.Status).HasConversion<string>().HasMaxLength(32).IsRequired();

        // The chair reads the queue in order, filtered to the requests still waiting.
        builder.HasIndex(r => new { r.MeetingId, r.Status, r.QueuePosition });

        builder.HasOne(r => r.Meeting)
            .WithMany(m => m.SpeakingRequests)
            .HasForeignKey(r => r.MeetingId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasOne(r => r.AgendaItem)
            .WithMany()
            .HasForeignKey(r => r.AgendaItemId)
            .OnDelete(DeleteBehavior.SetNull);

        builder.HasOne(r => r.User)
            .WithMany()
            .HasForeignKey(r => r.UserId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}
