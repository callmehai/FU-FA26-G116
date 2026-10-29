using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using TrustCabinet.Domain.Entities;

namespace TrustCabinet.Infrastructure.Configurations;

public class VoteReceiptConfiguration : IEntityTypeConfiguration<VoteReceipt>
{
    public void Configure(EntityTypeBuilder<VoteReceipt> builder)
    {
        // A ballot is cast by an internal user or by a Guest, never both or neither.
        builder.ToTable(t => t.HasCheckConstraint(
            "ck_vote_receipts_one_voter", "num_nonnulls(voter_id, guest_invitation_id) = 1"));

        // One person, one vote — enforced by the database, not by a check in application code that
        // two simultaneous requests could both slip past. One index per kind of voter.
        builder.HasIndex(r => new { r.VotingSessionId, r.VoterId }).IsUnique().HasFilter("voter_id IS NOT NULL");
        builder.HasIndex(r => new { r.VotingSessionId, r.GuestInvitationId })
            .IsUnique().HasFilter("guest_invitation_id IS NOT NULL");

        builder.HasOne(r => r.VotingSession)
            .WithMany(v => v.Receipts)
            .HasForeignKey(r => r.VotingSessionId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasOne(r => r.Voter)
            .WithMany()
            .HasForeignKey(r => r.VoterId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne(r => r.GuestInvitation)
            .WithMany()
            .HasForeignKey(r => r.GuestInvitationId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}
