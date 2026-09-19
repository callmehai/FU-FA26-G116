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

        builder.HasOne(p => p.CallSession)
            .WithMany(c => c.Participants)
            .HasForeignKey(p => p.CallSessionId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasOne(p => p.User)
            .WithMany()
            .HasForeignKey(p => p.UserId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}
