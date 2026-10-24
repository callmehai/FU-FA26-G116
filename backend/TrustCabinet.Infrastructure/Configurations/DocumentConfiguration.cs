using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using TrustCabinet.Domain.Entities;

namespace TrustCabinet.Infrastructure.Configurations;

public class DocumentConfiguration : IEntityTypeConfiguration<Document>
{
    public void Configure(EntityTypeBuilder<Document> builder)
    {
        builder.Property(d => d.FileName).HasMaxLength(300).IsRequired();

        builder.HasIndex(d => new { d.OwnerId, d.FolderId });

        builder.HasOne(d => d.Owner)
            .WithMany()
            .HasForeignKey(d => d.OwnerId)
            .OnDelete(DeleteBehavior.Restrict);

        // Restrict: a folder is deleted only once it is empty (UC-72d), so the database refusing a
        // non-empty one is the backstop, not a cascade that silently takes the files with it.
        builder.HasOne(d => d.Folder)
            .WithMany()
            .HasForeignKey(d => d.FolderId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne(d => d.StoredFile)
            .WithMany()
            .HasForeignKey(d => d.StoredFileId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}
