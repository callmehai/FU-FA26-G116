using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using TrustCabinet.Domain.Entities;
using TrustCabinet.Domain.Meetings;

namespace TrustCabinet.Infrastructure.Configurations;

public class MeetingSessionConfiguration : IEntityTypeConfiguration<MeetingSession>
{
    public void Configure(EntityTypeBuilder<MeetingSession> builder)
    {
        builder.Property(session => session.Title)
            .HasMaxLength(MeetingSessionRules.MaximumTitleLength).IsRequired();
        builder.Property(session => session.Status).HasConversion<string>().HasMaxLength(32).IsRequired();
        builder.Property(session => session.PlannedStartsAt).IsRequired();
        builder.Property(session => session.PlannedEndsAt).IsRequired();

        builder.HasIndex(session => new { session.MeetingId, session.Status });
        builder.HasOne(session => session.Meeting)
            .WithMany(meeting => meeting.Sessions)
            .HasForeignKey(session => session.MeetingId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}
