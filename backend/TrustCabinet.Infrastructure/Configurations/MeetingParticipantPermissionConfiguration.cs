using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using TrustCabinet.Domain.Entities;

namespace TrustCabinet.Infrastructure.Configurations;

public sealed class MeetingParticipantPermissionConfiguration : IEntityTypeConfiguration<MeetingParticipantPermission>
{
    public void Configure(EntityTypeBuilder<MeetingParticipantPermission> b)
    {
        b.Property(x => x.PermissionKey).HasMaxLength(100).IsRequired();
        b.HasOne(x => x.MeetingParticipant).WithMany().HasForeignKey(x => x.MeetingParticipantId).OnDelete(DeleteBehavior.Restrict);
        b.HasOne(x => x.GuestInvitation).WithMany().HasForeignKey(x => x.GuestInvitationId).OnDelete(DeleteBehavior.Restrict);
        b.HasOne<User>().WithMany().HasForeignKey(x => x.GrantedById).OnDelete(DeleteBehavior.Restrict);
        b.HasIndex(x => new { x.MeetingParticipantId, x.PermissionKey }).IsUnique().HasFilter("meeting_participant_id IS NOT NULL");
        b.HasIndex(x => new { x.GuestInvitationId, x.PermissionKey }).IsUnique().HasFilter("guest_invitation_id IS NOT NULL");
        b.ToTable(t => t.HasCheckConstraint("ck_permission_owner", "(meeting_participant_id IS NULL) <> (guest_invitation_id IS NULL)"));
    }
}
