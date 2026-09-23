using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using TrustCabinet.Domain.Entities;

namespace TrustCabinet.Infrastructure.Configurations;

public class RoleConfiguration : IEntityTypeConfiguration<Role>
{
    public void Configure(EntityTypeBuilder<Role> builder)
    {
        // citext, like users.email: the unique index then refuses a second role that differs only
        // in case, in the database rather than only in the service's check — two administrators
        // creating "Thư ký phó" at once both pass the check, and only the index stops the second.
        builder.Property(r => r.Name).HasColumnType("citext").HasMaxLength(64).IsRequired();
        builder.Property(r => r.Description).HasMaxLength(256);

        builder.HasIndex(r => r.Name).IsUnique();

        // No HasData. The four built-in roles are inserted by the migration that created the table
        // (it has to fill users.role_id from them in the same step) and topped up by
        // ReferenceDataSeeder — for the reason ADR-0007 gives for role_capabilities.
    }
}
