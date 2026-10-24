using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using TrustCabinet.Domain.Entities;

namespace TrustCabinet.Infrastructure.Configurations;

public class MeetingDocumentConfiguration : IEntityTypeConfiguration<MeetingDocument>
{
    public void Configure(EntityTypeBuilder<MeetingDocument> builder)
    {
        builder.Property(d => d.FileName).HasMaxLength(300).IsRequired();

        // At most one active copy of a file per meeting (ERD rule 10).
        builder.HasIndex(d => new { d.MeetingId, d.StoredFileId }).IsUnique().HasFilter("removed_at IS NULL");

        builder.HasOne(d => d.Meeting)
            .WithMany(m => m.Documents)
            .HasForeignKey(d => d.MeetingId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasOne(d => d.StoredFile)
            .WithMany()
            .HasForeignKey(d => d.StoredFileId)
            .OnDelete(DeleteBehavior.Restrict);

        // Deleting the library row leaves the meeting copy readable (ADR-0018 §4).
        builder.HasOne(d => d.Document)
            .WithMany()
            .HasForeignKey(d => d.DocumentId)
            .OnDelete(DeleteBehavior.SetNull);

        builder.HasOne(d => d.AddedBy)
            .WithMany()
            .HasForeignKey(d => d.AddedById)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne(d => d.RemovedBy)
            .WithMany()
            .HasForeignKey(d => d.RemovedById)
            .OnDelete(DeleteBehavior.Restrict);
    }
}
