using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using TrustCabinet.Domain.Entities;

namespace TrustCabinet.Infrastructure.Configurations;

public class CalendarShareConfiguration : IEntityTypeConfiguration<CalendarShare>
{
    /// <summary>The open states, as the filtered unique index spells them.</summary>
    internal const string OpenFilter = "status IN ('Pending', 'Effective')";

    public void Configure(EntityTypeBuilder<CalendarShare> builder)
    {
        builder.Property(s => s.InitiatedBy).HasConversion<string>().HasMaxLength(16).IsRequired();
        builder.Property(s => s.Level).HasConversion<string>().HasMaxLength(16).IsRequired();
        builder.Property(s => s.Status).HasConversion<string>().HasMaxLength(16).IsRequired();
        builder.Property(s => s.PendingLevel).HasConversion<string>().HasMaxLength(16);
        builder.Ignore(s => s.HasPendingChange);

        // BR-423: at most one Pending or Effective share per owner → viewer pair. Directed: A viewing B's calendar does not
        // stop B asking for A's. Closed rows (declined, withdrawn, ended, stopped) stay as history and block nothing.
        builder.HasIndex(s => new { s.OwnerId, s.ViewerId })
            .IsUnique()
            .HasFilter(OpenFilter)
            .HasDatabaseName("ux_calendar_shares_open_pair");
        builder.HasIndex(s => new { s.ViewerId, s.Status });
        builder.HasIndex(s => new { s.OwnerId, s.Status });

        builder.ToTable(table =>
        {
            table.HasCheckConstraint("ck_calendar_shares_not_self", "owner_id <> viewer_id");
            table.HasCheckConstraint("ck_calendar_shares_range", "start_date <= end_date");
            // A pending change is the three columns together, or none of them.
            table.HasCheckConstraint("ck_calendar_shares_pending_change",
                "(pending_requested_at IS NULL) = (pending_level IS NULL) "
                + "AND (pending_requested_at IS NULL) = (pending_end_date IS NULL)");
        });

        // Accounts are deactivated, never deleted (SCRUM-13): a share keeps both people for as long as it exists.
        builder.HasOne(s => s.Owner)
            .WithMany()
            .HasForeignKey(s => s.OwnerId)
            .OnDelete(DeleteBehavior.Restrict);
        builder.HasOne(s => s.Viewer)
            .WithMany()
            .HasForeignKey(s => s.ViewerId)
            .OnDelete(DeleteBehavior.Restrict);
        builder.HasOne(s => s.ClosedBy)
            .WithMany()
            .HasForeignKey(s => s.ClosedById)
            .OnDelete(DeleteBehavior.Restrict);
    }
}
