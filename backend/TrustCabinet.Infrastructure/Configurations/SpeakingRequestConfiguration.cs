using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using TrustCabinet.Domain.Entities;

namespace TrustCabinet.Infrastructure.Configurations;

public class SpeakingRequestConfiguration : IEntityTypeConfiguration<SpeakingRequest>
{
    /// <summary>BR-337: one open request per member. PostgreSQL cuts names at 63 bytes, so keep them short.</summary>
    public const string OpenUserIndex = "ux_speaking_requests_open_user";
    public const string OpenGuestIndex = "ux_speaking_requests_open_guest";
    /// <summary>BR-338: one person holds the floor at a time.</summary>
    public const string FloorIndex = "ux_speaking_requests_floor";

    private const string Open = "status IN ('Queued', 'Invited', 'Speaking')";

    public void Configure(EntityTypeBuilder<SpeakingRequest> builder)
    {
        builder.Property(r => r.Status).HasConversion<string>().HasMaxLength(32).IsRequired();
        builder.ToTable(t =>
        {
            t.HasCheckConstraint("ck_speaking_requests_one_speaker", "(user_id IS NULL) <> (guest_invitation_id IS NULL)");
            t.HasCheckConstraint("ck_speaking_requests_status",
                "status IN ('Queued', 'Invited', 'Speaking', 'Finished', 'Cancelled')");
        });

        // The chair reads the queue in order, filtered to the requests still waiting.
        builder.HasIndex(r => new { r.MeetingId, r.Status, r.QueuePosition });
        builder.HasIndex(r => new { r.MeetingId, r.UserId }, OpenUserIndex).IsUnique()
            .HasFilter($"user_id IS NOT NULL AND {Open}").HasDatabaseName(OpenUserIndex);
        builder.HasIndex(r => new { r.MeetingId, r.GuestInvitationId }, OpenGuestIndex).IsUnique()
            .HasFilter($"guest_invitation_id IS NOT NULL AND {Open}").HasDatabaseName(OpenGuestIndex);
        builder.HasIndex(r => r.MeetingId, FloorIndex).IsUnique()
            .HasFilter("status IN ('Invited', 'Speaking')").HasDatabaseName(FloorIndex);

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

        // An invitation is revoked, never deleted: the record of who spoke stays with the meeting (BR-339).
        builder.HasOne(r => r.GuestInvitation)
            .WithMany()
            .HasForeignKey(r => r.GuestInvitationId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}
