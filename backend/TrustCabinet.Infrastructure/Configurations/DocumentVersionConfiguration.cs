using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using TrustCabinet.Domain.Entities;

namespace TrustCabinet.Infrastructure.Configurations;

public class DocumentVersionConfiguration : IEntityTypeConfiguration<DocumentVersion>
{
    public void Configure(EntityTypeBuilder<DocumentVersion> builder)
    {
        builder.Property(v => v.FileName).HasMaxLength(300).IsRequired();
        builder.Property(v => v.StoragePath).HasMaxLength(500).IsRequired();
        builder.Property(v => v.ContentType).HasMaxLength(150).IsRequired();
        builder.Property(v => v.ContentSha256).HasMaxLength(64).IsRequired();

        // Version numbers are per document and never reused.
        builder.HasIndex(v => new { v.DocumentId, v.VersionNumber }).IsUnique();

        builder.HasOne(v => v.Document)
            .WithMany(d => d.Versions)
            .HasForeignKey(v => v.DocumentId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasOne(v => v.UploadedBy)
            .WithMany()
            .HasForeignKey(v => v.UploadedById)
            .OnDelete(DeleteBehavior.Restrict);
    }
}
