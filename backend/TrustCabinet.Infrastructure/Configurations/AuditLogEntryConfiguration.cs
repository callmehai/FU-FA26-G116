using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using TrustCabinet.Domain.Entities;

namespace TrustCabinet.Infrastructure.Configurations;

public class AuditLogEntryConfiguration : IEntityTypeConfiguration<AuditLogEntry>
{
    public void Configure(EntityTypeBuilder<AuditLogEntry> builder)
    {
        builder.Property(a => a.Action).HasMaxLength(100).IsRequired();
        builder.Property(a => a.ObjectType).HasMaxLength(100).IsRequired();
        builder.Property(a => a.IpAddress).HasMaxLength(64);

        // jsonb rather than text: PostgreSQL can then query inside the payload when someone asks a
        // question the columns do not answer.
        builder.Property(a => a.Details).HasColumnType("jsonb").IsRequired();

        // The admin screen reads newest first; investigations read everything about one object.
        builder.HasIndex(a => a.OccurredAt);
        builder.HasIndex(a => new { a.ObjectType, a.ObjectId });

        // Restrict, so a user row can never be removed while it is the actor on recorded evidence.
        builder.HasOne(a => a.Actor)
            .WithMany()
            .HasForeignKey(a => a.ActorId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}
