using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using TrustCabinet.Domain.Entities;
using TrustCabinet.Domain.Enums;

namespace TrustCabinet.Infrastructure.Configurations;

public sealed class MeetingGuestInvitationConfiguration : IEntityTypeConfiguration<MeetingGuestInvitation>
{
    public void Configure(EntityTypeBuilder<MeetingGuestInvitation> b)
    {
        b.Property(x => x.InvitedName).HasMaxLength(200).IsRequired();
        b.Property(x => x.Email).HasColumnType("citext").HasMaxLength(256).IsRequired();
        b.Property(x => x.Response).HasConversion<string>().HasMaxLength(32);
        b.Property(x => x.Attendance).HasConversion<string>().HasMaxLength(32).HasDefaultValue(AttendanceStatus.Unknown);
        b.Property(x => x.SensitivityAtInvitation).HasConversion<string>().HasMaxLength(32);
        b.Property(x => x.InitialInvitationDeliveryKey).HasMaxLength(256);
        b.HasOne(x => x.Meeting).WithMany().HasForeignKey(x => x.MeetingId).OnDelete(DeleteBehavior.Restrict);
        b.HasOne<User>().WithMany().HasForeignKey(x => x.InvitedById).OnDelete(DeleteBehavior.Restrict);
        b.HasOne<User>().WithMany().HasForeignKey(x => x.RevokedById).OnDelete(DeleteBehavior.Restrict);
        b.HasOne<User>().WithMany().HasForeignKey(x => x.AttendanceRecordedById).OnDelete(DeleteBehavior.Restrict);
        b.HasIndex(x => new { x.MeetingId, x.Email }).IsUnique().HasFilter("revoked_at IS NULL");
        b.ToTable(t => t.HasCheckConstraint("ck_guest_revocation", "(revoked_at IS NULL) = (revoked_by_id IS NULL)"));
    }
}
