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
        // BR-59 backstop: the meeting row lock already serializes UC-29, this catches any other writer.
        builder.HasIndex(session => session.MeetingId)
            .IsUnique()
            .HasFilter("status = 'InProgress'")
            .HasDatabaseName("ix_meeting_sessions_meeting_id_in_progress");
        builder.HasOne(session => session.Meeting)
            .WithMany(meeting => meeting.Sessions)
            .HasForeignKey(session => session.MeetingId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}
