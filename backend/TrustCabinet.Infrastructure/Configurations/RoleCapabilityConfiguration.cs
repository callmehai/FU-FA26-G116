using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using TrustCabinet.Domain.Entities;
using TrustCabinet.Infrastructure.Seed;

namespace TrustCabinet.Infrastructure.Configurations;

public class RoleCapabilityConfiguration : IEntityTypeConfiguration<RoleCapability>
{
    public void Configure(EntityTypeBuilder<RoleCapability> builder)
    {
        builder.HasKey(rc => new { rc.Role, rc.Capability });

        builder.Property(rc => rc.Role).HasConversion<string>().HasMaxLength(32);
        builder.Property(rc => rc.Capability).HasConversion<string>().HasMaxLength(64);

        // Reference data, so it ships inside the migration: a database that has been migrated is
        // immediately consistent with the code, with no seeding step to remember.
        builder.HasData(ReferenceData.RoleCapabilities);
    }
}
