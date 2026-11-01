using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using TrustCabinet.Domain.Entities;

namespace TrustCabinet.Infrastructure.Configurations;

public class DocumentReadConfirmationConfiguration : IEntityTypeConfiguration<DocumentReadConfirmation>
{
    public void Configure(EntityTypeBuilder<DocumentReadConfirmation> builder)
    {
        // Confirmed by an internal user or by a Guest, never both or neither.
        builder.ToTable(t => t.HasCheckConstraint(
            "ck_document_read_confirmations_one_reader", "num_nonnulls(user_id, guest_invitation_id) = 1"));

        // One confirmation per person and document (BR-291): two simultaneous ticks keep the first.
        builder.HasIndex(c => new { c.MeetingDocumentId, c.UserId }).IsUnique().HasFilter("user_id IS NOT NULL");
        builder.HasIndex(c => new { c.MeetingDocumentId, c.GuestInvitationId })
            .IsUnique().HasFilter("guest_invitation_id IS NOT NULL");

        builder.HasOne(c => c.MeetingDocument)
            .WithMany(d => d.ReadConfirmations)
            .HasForeignKey(c => c.MeetingDocumentId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasOne(c => c.User)
            .WithMany()
            .HasForeignKey(c => c.UserId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne(c => c.GuestInvitation)
            .WithMany()
            .HasForeignKey(c => c.GuestInvitationId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}
