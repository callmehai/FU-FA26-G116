using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using TrustCabinet.Domain.Entities;

namespace TrustCabinet.Infrastructure.Configurations;

public class VoteChoiceConfiguration : IEntityTypeConfiguration<VoteChoice>
{
    public void Configure(EntityTypeBuilder<VoteChoice> builder)
    {
        // Counting the result reads every choice in a session.
        builder.HasIndex(c => new { c.VotingSessionId, c.VotingOptionId });

        builder.HasOne(c => c.VotingSession)
            .WithMany(v => v.Choices)
            .HasForeignKey(c => c.VotingSessionId)
            .OnDelete(DeleteBehavior.Cascade);

        // Restrict, because deleting the session already cascades here; a second cascade path
        // through the option would be redundant and harder to reason about.
        builder.HasOne(c => c.VotingOption)
            .WithMany(o => o.Choices)
            .HasForeignKey(c => c.VotingOptionId)
            .OnDelete(DeleteBehavior.Restrict);

        // Null in a secret vote; in a public one exactly one is set so the result can name each
        // voter's choice. The database can only rule out both at once.
        builder.ToTable(t => t.HasCheckConstraint(
            "ck_vote_choices_at_most_one_voter", "num_nonnulls(voter_id, guest_invitation_id) <= 1"));

        builder.HasOne(c => c.Voter)
            .WithMany()
            .HasForeignKey(c => c.VoterId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne(c => c.GuestInvitation)
            .WithMany()
            .HasForeignKey(c => c.GuestInvitationId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}
