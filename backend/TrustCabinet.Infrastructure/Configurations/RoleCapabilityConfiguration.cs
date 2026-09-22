using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using TrustCabinet.Domain.Entities;

namespace TrustCabinet.Infrastructure.Configurations;

public class RoleCapabilityConfiguration : IEntityTypeConfiguration<RoleCapability>
{
    public void Configure(EntityTypeBuilder<RoleCapability> builder)
    {
        builder.HasKey(rc => new { rc.Role, rc.Capability });

        builder.Property(rc => rc.Role).HasConversion<string>().HasMaxLength(32);
        builder.Property(rc => rc.Capability).HasConversion<string>().HasMaxLength(64);

        // No HasData here, deliberately — see ADR-0007. These rows used to ship inside the
        // migration, which was right while the matrix was fixed in code. SCRUM-60 lets an
        // administrator edit it, and HasData turns the next migration into an UPDATE/DELETE over
        // exactly the rows they changed: EF compares the model's seed data against the previous
        // migration's, not against the database, so it would quietly restore the defaults on
        // deploy.
        //
        // ReferenceData.RoleCapabilities is now the *initial* value, applied by
        // ReferenceDataSeeder only when the table is empty.
    }
}
