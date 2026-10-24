using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using TrustCabinet.Domain.Entities;

namespace TrustCabinet.Infrastructure.Configurations;

public class DocumentCommentConfiguration : IEntityTypeConfiguration<DocumentComment>
{
    public void Configure(EntityTypeBuilder<DocumentComment> builder)
    {
        builder.Property(c => c.Body).HasMaxLength(4000).IsRequired();

        builder.HasIndex(c => new { c.MeetingDocumentId, c.CreatedAt });

        // A comment is written either by an internal user or by a Guest, never both or neither.
        builder.ToTable(t => t.HasCheckConstraint(
            "ck_document_comments_one_author", "num_nonnulls(author_id, guest_invitation_id) = 1"));

        builder.HasOne(c => c.MeetingDocument)
            .WithMany(d => d.Comments)
            .HasForeignKey(c => c.MeetingDocumentId)
            .OnDelete(DeleteBehavior.Cascade);

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

        builder.HasOne(c => c.GuestInvitation)
            .WithMany()
            .HasForeignKey(c => c.GuestInvitationId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}
