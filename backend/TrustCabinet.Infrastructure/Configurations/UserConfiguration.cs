using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using TrustCabinet.Domain.Entities;

namespace TrustCabinet.Infrastructure.Configurations;

public class UserConfiguration : IEntityTypeConfiguration<User>
{
    public void Configure(EntityTypeBuilder<User> builder)
    {
        builder.Property(u => u.FullName).HasMaxLength(200).IsRequired();
        builder.Property(u => u.Email).HasMaxLength(256).IsRequired();
        builder.Property(u => u.PasswordHash).HasMaxLength(512).IsRequired();

        // SQL Server's default collation is case-insensitive, so this also stops "A@x" and "a@x"
        // from both being registered.
        builder.HasIndex(u => u.Email).IsUnique();

        builder.Property(u => u.Role).HasConversion<string>().HasMaxLength(32).IsRequired();

        builder.HasOne(u => u.OrgUnit)
            .WithMany(o => o.Users)
            .HasForeignKey(u => u.OrgUnitId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}
