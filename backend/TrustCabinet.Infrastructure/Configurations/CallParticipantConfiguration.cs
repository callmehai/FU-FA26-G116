using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using TrustCabinet.Domain.Entities;

namespace TrustCabinet.Infrastructure.Configurations;

public class CallParticipantConfiguration : IEntityTypeConfiguration<CallParticipant>
{
    public void Configure(EntityTypeBuilder<CallParticipant> builder)
    {
        // Not unique on (call, user): dropping out and rejoining is normal, and each stint is its
        // own row so connected time adds up correctly.
        builder.HasIndex(p => new { p.CallSessionId, p.UserId });

        // ...but at most one open stint per person: the webhook and the access reconciliation can record the same
        // join at once, and only one may win (RecordJoinAsync treats the loser as already recorded).
        builder.HasIndex(p => new { p.CallSessionId, p.UserId }, Calls.CallRepository.OpenUserStintIndex)
            .IsUnique()
            .HasFilter("left_at IS NULL")
            .HasDatabaseName(Calls.CallRepository.OpenUserStintIndex);
        builder.HasIndex(p => new { p.CallSessionId, p.GuestSessionId }, Calls.CallRepository.OpenGuestStintIndex)
            .IsUnique()
            .HasFilter("left_at IS NULL")
            .HasDatabaseName(Calls.CallRepository.OpenGuestStintIndex);

        // A stint is one user's or one guest session's, never both or neither (SCRUM-59).
        builder.ToTable(t => t.HasCheckConstraint(
            "ck_call_participants_one_member", "(user_id IS NULL) <> (guest_session_id IS NULL)"));

        builder.HasOne(p => p.CallSession)
            .WithMany(c => c.Participants)
            .HasForeignKey(p => p.CallSessionId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasOne(p => p.User)
            .WithMany()
            .HasForeignKey(p => p.UserId)
            .OnDelete(DeleteBehavior.Restrict);

        // A guest's stints are their personal data and go with the session (and the invitation above it).
        builder.HasOne(p => p.GuestSession)
            .WithMany()
            .HasForeignKey(p => p.GuestSessionId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}
