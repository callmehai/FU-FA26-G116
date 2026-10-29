using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using TrustCabinet.Domain.Entities;

namespace TrustCabinet.Infrastructure.Configurations;

public class VoteEligibleVoterConfiguration : IEntityTypeConfiguration<VoteEligibleVoter>
{
    public void Configure(EntityTypeBuilder<VoteEligibleVoter> builder)
    {
        // An internal user or a Guest, never both or neither — the same rule as a ballot's voter.
        builder.ToTable(t => t.HasCheckConstraint(
            "ck_vote_eligible_voters_one_voter", "num_nonnulls(user_id, guest_invitation_id) = 1"));

        builder.HasIndex(e => new { e.VotingSessionId, e.UserId }).IsUnique().HasFilter("user_id IS NOT NULL");
        builder.HasIndex(e => new { e.VotingSessionId, e.GuestInvitationId })
            .IsUnique().HasFilter("guest_invitation_id IS NOT NULL");

        builder.HasOne(e => e.VotingSession)
            .WithMany(v => v.EligibleVoters)
            .HasForeignKey(e => e.VotingSessionId)
            .OnDelete(DeleteBehavior.Cascade);

        // Users are deactivated, never deleted; Restrict keeps the fixed list intact if one ever were.
        builder.HasOne(e => e.User)
            .WithMany()
            .HasForeignKey(e => e.UserId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne(e => e.GuestInvitation)
            .WithMany()
            .HasForeignKey(e => e.GuestInvitationId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}
