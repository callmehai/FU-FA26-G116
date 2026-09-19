using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using TrustCabinet.Domain.Entities;

namespace TrustCabinet.Infrastructure.Configurations;

public class OrgUnitConfiguration : IEntityTypeConfiguration<OrgUnit>
{
    public void Configure(EntityTypeBuilder<OrgUnit> builder)
    {
        builder.Property(u => u.Name).HasMaxLength(200).IsRequired();
        builder.Property(u => u.Code).HasMaxLength(50).IsRequired();

        builder.HasIndex(u => u.Code).IsUnique();

        // Restrict, not Cascade: deleting a parent unit must never silently take its children and
        // everyone in them with it. Reparent first, then delete.
        builder.HasOne(u => u.Parent)
            .WithMany(u => u.Children)
            .HasForeignKey(u => u.ParentId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}
