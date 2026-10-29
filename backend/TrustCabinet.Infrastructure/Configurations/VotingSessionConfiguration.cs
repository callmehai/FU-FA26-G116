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
        builder.Property(v => v.Conclusion).HasMaxLength(4000);
        builder.Property(v => v.Kind).HasConversion<string>().HasMaxLength(32).IsRequired();
        builder.Property(v => v.Status).HasConversion<string>().HasMaxLength(32).IsRequired();

        builder.HasIndex(v => new { v.MeetingId, v.CreatedAt });

        builder.HasOne(v => v.Meeting)
            .WithMany(m => m.VotingSessions)
            .HasForeignKey(v => v.MeetingId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasOne(v => v.AgendaItem)
            .WithMany(a => a.VotingSessions)
            .HasForeignKey(v => v.AgendaItemId)
            .OnDelete(DeleteBehavior.SetNull);

        builder.HasOne(v => v.CreatedBy)
            .WithMany()
            .HasForeignKey(v => v.CreatedById)
            .OnDelete(DeleteBehavior.Restrict);
    }
}
