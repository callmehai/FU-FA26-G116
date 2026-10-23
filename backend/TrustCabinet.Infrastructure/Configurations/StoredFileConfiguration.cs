using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using TrustCabinet.Domain.Entities;

namespace TrustCabinet.Infrastructure.Configurations;

public class StoredFileConfiguration : IEntityTypeConfiguration<StoredFile>
{
    public void Configure(EntityTypeBuilder<StoredFile> builder)
    {
        builder.Property(f => f.StorageKey).HasMaxLength(200).IsRequired();
        builder.Property(f => f.OriginalFileName).HasMaxLength(300).IsRequired();
        builder.Property(f => f.ContentType).HasMaxLength(150).IsRequired();
        builder.Property(f => f.ContentSha256).HasMaxLength(64).IsRequired();

        builder.HasIndex(f => f.StorageKey).IsUnique();

        // The orphan purge only ever looks at files whose object is still there.
        builder.HasIndex(f => f.UploadedAt).HasFilter("purged_at IS NULL");

        builder.HasOne(f => f.UploadedBy)
            .WithMany()
            .HasForeignKey(f => f.UploadedById)
            .OnDelete(DeleteBehavior.Restrict);
    }
}
