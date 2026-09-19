using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using TrustCabinet.Domain.Entities;

namespace TrustCabinet.Infrastructure.Configurations;

public class DocumentCommentConfiguration : IEntityTypeConfiguration<DocumentComment>
{
    public void Configure(EntityTypeBuilder<DocumentComment> builder)
    {
        builder.Property(c => c.Body).HasMaxLength(4000).IsRequired();

        builder.HasIndex(c => new { c.DocumentId, c.CreatedAt });

        builder.HasOne(c => c.Document)
            .WithMany(d => d.Comments)
            .HasForeignKey(c => c.DocumentId)
            .OnDelete(DeleteBehavior.Cascade);

        // Restrict: a comment written against version 2 keeps pointing at version 2.
        builder.HasOne(c => c.DocumentVersion)
            .WithMany()
            .HasForeignKey(c => c.DocumentVersionId)
            .OnDelete(DeleteBehavior.Restrict);

        // Restrict on the self-reference: deleting a comment that has replies must be a deliberate
        // decision in code, not something the database does silently to a whole thread.
        builder.HasOne(c => c.ParentComment)
            .WithMany(c => c.Replies)
            .HasForeignKey(c => c.ParentCommentId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne(c => c.Author)
            .WithMany()
            .HasForeignKey(c => c.AuthorId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}
