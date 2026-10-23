using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using TrustCabinet.Domain.Entities;

namespace TrustCabinet.Infrastructure.Configurations;

public class MeetingDocumentAgendaItemConfiguration : IEntityTypeConfiguration<MeetingDocumentAgendaItem>
{
    public void Configure(EntityTypeBuilder<MeetingDocumentAgendaItem> builder)
    {
        builder.HasKey(l => new { l.MeetingDocumentId, l.AgendaItemId });
        builder.HasIndex(l => l.AgendaItemId);

        builder.HasOne(l => l.MeetingDocument)
            .WithMany(d => d.AgendaItemLinks)
            .HasForeignKey(l => l.MeetingDocumentId)
            .OnDelete(DeleteBehavior.Cascade);

        // Deleting an agenda item drops only the link: the document stays in the meeting.
        builder.HasOne(l => l.AgendaItem)
            .WithMany(a => a.DocumentLinks)
            .HasForeignKey(l => l.AgendaItemId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}
