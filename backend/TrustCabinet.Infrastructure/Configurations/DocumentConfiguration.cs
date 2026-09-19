using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using TrustCabinet.Domain.Entities;

namespace TrustCabinet.Infrastructure.Configurations;

public class DocumentConfiguration : IEntityTypeConfiguration<Document>
{
    public void Configure(EntityTypeBuilder<Document> builder)
    {
        builder.Property(d => d.Title).HasMaxLength(300).IsRequired();
        builder.Property(d => d.Category).HasConversion<string>().HasMaxLength(32).IsRequired();

        builder.HasIndex(d => d.MeetingId);

        builder.HasOne(d => d.Meeting)
            .WithMany(m => m.Documents)
            .HasForeignKey(d => d.MeetingId)
            .OnDelete(DeleteBehavior.Cascade);

        // SetNull, not Cascade: dropping an agenda item must not take the document with it. The
        // file was uploaded to the meeting and stays attached to it.
        builder.HasOne(d => d.AgendaItem)
            .WithMany(a => a.Documents)
            .HasForeignKey(d => d.AgendaItemId)
            .OnDelete(DeleteBehavior.SetNull);

        // Document and DocumentVersion point at each other. This side is Restrict and the pointer
        // is nullable, so there is exactly one cascade path — from the document down to its
        // versions — and no cycle for the database to resolve.
        builder.HasOne(d => d.CurrentVersion)
            .WithMany()
            .HasForeignKey(d => d.CurrentVersionId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne(d => d.UploadedBy)
            .WithMany()
            .HasForeignKey(d => d.UploadedById)
            .OnDelete(DeleteBehavior.Restrict);
    }
}
