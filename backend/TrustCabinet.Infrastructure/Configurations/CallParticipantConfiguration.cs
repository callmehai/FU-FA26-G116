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
