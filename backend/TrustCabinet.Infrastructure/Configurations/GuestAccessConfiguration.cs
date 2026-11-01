using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using TrustCabinet.Domain.Entities;

namespace TrustCabinet.Infrastructure.Configurations;

public sealed class GuestVerificationChallengeConfiguration : IEntityTypeConfiguration<GuestVerificationChallenge>
{
    public void Configure(EntityTypeBuilder<GuestVerificationChallenge> b)
    {
        b.Property(x => x.CodeHash).HasMaxLength(64).IsRequired();
        b.HasOne<MeetingGuestInvitation>().WithMany().HasForeignKey(x => x.InvitationId).OnDelete(DeleteBehavior.Cascade);
        b.HasIndex(x => new { x.InvitationId, x.CreatedAt });
    }
}

public sealed class GuestSessionConfiguration : IEntityTypeConfiguration<GuestSession>
{
    public void Configure(EntityTypeBuilder<GuestSession> b)
    {
        b.HasOne(x => x.Invitation).WithMany().HasForeignKey(x => x.InvitationId).OnDelete(DeleteBehavior.Cascade);
        b.HasIndex(x => x.InvitationId);
    }
}
