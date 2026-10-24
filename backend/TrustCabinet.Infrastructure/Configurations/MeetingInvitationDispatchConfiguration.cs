using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using TrustCabinet.Domain.Entities;

namespace TrustCabinet.Infrastructure.Configurations;

public sealed class MeetingInvitationDispatchConfiguration : IEntityTypeConfiguration<MeetingInvitationDispatch>
{
    public void Configure(EntityTypeBuilder<MeetingInvitationDispatch> b)
    {
        b.Property(x => x.EventKind).HasConversion<string>().HasMaxLength(32);
        b.Property(x => x.State).HasConversion<string>().HasMaxLength(32);
        b.Property(x => x.DeduplicationKey).HasMaxLength(256).IsRequired();
        b.Property(x => x.PayloadJson).HasColumnType("jsonb").IsRequired();
        b.Property(x => x.LastError).HasMaxLength(1000);
        b.HasIndex(x => x.DeduplicationKey).IsUnique();
        b.HasIndex(x => new { x.State, x.NextAttemptAt });
        b.HasOne<Meeting>().WithMany().HasForeignKey(x => x.MeetingId).OnDelete(DeleteBehavior.Restrict);
        b.HasOne<MeetingParticipant>().WithMany().HasForeignKey(x => x.ParticipantId).OnDelete(DeleteBehavior.Restrict);
        b.HasOne<MeetingGuestInvitation>().WithMany().HasForeignKey(x => x.GuestInvitationId).OnDelete(DeleteBehavior.Restrict);
        b.HasOne<EmailMessage>().WithMany().HasForeignKey(x => x.EmailMessageId).OnDelete(DeleteBehavior.Restrict);
        b.ToTable(t => t.HasCheckConstraint("ck_dispatch_owner", "(participant_id IS NULL) <> (guest_invitation_id IS NULL)"));
    }
}
