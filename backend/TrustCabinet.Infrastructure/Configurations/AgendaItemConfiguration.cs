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
        builder.Property(a => a.LegacyPresenterName).HasColumnName("presenter_name").HasMaxLength(200);
        builder.Property(a => a.Conclusion).HasMaxLength(2000);
        builder.HasOne(a => a.PresenterGuestInvitation).WithMany()
            .HasForeignKey(a => a.PresenterGuestInvitationId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne(a => a.ConclusionUpdatedBy).WithMany()
            .HasForeignKey(a => a.ConclusionUpdatedById).OnDelete(DeleteBehavior.Restrict);
        builder.ToTable(t =>
        {
            t.HasCheckConstraint("ck_agenda_items_presenter", "presenter_id IS NULL OR presenter_guest_invitation_id IS NULL");
            t.HasCheckConstraint("ck_agenda_items_conclusion", "(conclusion IS NULL AND conclusion_updated_by_id IS NULL AND conclusion_updated_at IS NULL) OR (conclusion IS NOT NULL AND conclusion_updated_by_id IS NOT NULL AND conclusion_updated_at IS NOT NULL)");
        });
        builder.Property(a => a.Status).HasConversion<string>().HasMaxLength(32).IsRequired();

        // Deliberately not unique. Drag-and-drop reordering rewrites several positions at once,
        // and a unique index would reject the intermediate state where two items briefly share a
        // number unless every reorder were written as a careful multi-step dance.
        builder.HasIndex(a => new { a.MeetingId, a.Position });
        builder.HasIndex(a => a.MeetingId, "ix_agenda_items_one_current")
            .IsUnique().HasFilter("status = 'InProgress'");
        builder.ToTable(t => t.HasCheckConstraint("ck_agenda_items_status",
            "status IN ('Pending', 'InProgress', 'Done')"));

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
