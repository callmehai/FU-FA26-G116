using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using TrustCabinet.Domain.Entities;

namespace TrustCabinet.Infrastructure.Configurations;

public class MeetingNoteConfiguration : IEntityTypeConfiguration<MeetingNote>
{
    public void Configure(EntityTypeBuilder<MeetingNote> builder)
    {
        builder.Property(n => n.Body).HasMaxLength(20000);

        // One note per author per agenda item. AreNullsDistinct(false) makes the constraint also
        // hold for the meeting-wide note, where AgendaItemId is null — PostgreSQL's default treats
        // two NULLs as different values, which would quietly allow duplicates there.
        builder.HasIndex(n => new { n.MeetingId, n.AgendaItemId, n.AuthorId })
            .IsUnique()
            .AreNullsDistinct(false);

        builder.HasOne(n => n.Meeting)
            .WithMany(m => m.Notes)
            .HasForeignKey(n => n.MeetingId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasOne(n => n.AgendaItem)
            .WithMany()
            .HasForeignKey(n => n.AgendaItemId)
            .OnDelete(DeleteBehavior.SetNull);

        builder.HasOne(n => n.Author)
            .WithMany()
            .HasForeignKey(n => n.AuthorId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}
