using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using TrustCabinet.Domain.Entities;

namespace TrustCabinet.Infrastructure.Configurations;

public class AgendaItemConfiguration : IEntityTypeConfiguration<AgendaItem>
{
    public void Configure(EntityTypeBuilder<AgendaItem> builder)
    {
        builder.Property(a => a.Title).HasMaxLength(300).IsRequired();
        builder.Property(a => a.Description).HasMaxLength(4000);
        builder.Property(a => a.PresenterName).HasMaxLength(200);
        builder.Property(a => a.Status).HasConversion<string>().HasMaxLength(32).IsRequired();

        // Deliberately not unique. Drag-and-drop reordering rewrites several positions at once,
        // and a unique index would reject the intermediate state where two items briefly share a
        // number unless every reorder were written as a careful multi-step dance.
        builder.HasIndex(a => new { a.MeetingId, a.Position });

        builder.HasOne(a => a.Meeting)
            .WithMany(m => m.AgendaItems)
            .HasForeignKey(a => a.MeetingId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasOne(a => a.Presenter)
            .WithMany()
            .HasForeignKey(a => a.PresenterId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}
