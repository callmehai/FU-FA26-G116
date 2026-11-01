using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.ChangeTracking;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using TrustCabinet.Domain.Entities;

namespace TrustCabinet.Infrastructure.Configurations;

public class RecordingConfiguration : IEntityTypeConfiguration<Recording>
{
    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web);

    public void Configure(EntityTypeBuilder<Recording> builder)
    {
        builder.Property(r => r.FileName).HasMaxLength(300).IsRequired();
        builder.Property(r => r.StoragePath).HasMaxLength(500).IsRequired();
        builder.Property(r => r.ContentType).HasMaxLength(150).IsRequired();
        builder.Property(r => r.ConsentPurpose).HasMaxLength(1000);
        builder.Property(r => r.Source).HasConversion<string>().HasMaxLength(32).IsRequired();
        builder.Property(r => r.Status).HasConversion<string>().HasMaxLength(32).IsRequired();
        // jsonb: read whole with its recording, never queried inside.
        builder.Property(r => r.Takes).HasColumnType("jsonb").HasConversion(
            takes => takes == null ? null : JsonSerializer.Serialize(takes, Json),
            json => json == null ? null : JsonSerializer.Deserialize<List<RecordingTake>>(json, Json),
            new ValueComparer<List<RecordingTake>?>(
                (a, b) => a == null ? b == null : b != null && a.SequenceEqual(b),
                list => list == null ? 0 : list.Aggregate(0, (hash, t) => HashCode.Combine(hash, t.GetHashCode())),
                list => list == null ? null : list.ToList()));

        builder.HasIndex(r => r.MeetingId);

        // ADR-0041: the room has one mic, so a meeting streams at most one take at a time.
        builder.HasIndex(r => r.MeetingId, "ix_recordings_one_open_take").HasDatabaseName("ix_recordings_one_open_take")
            .IsUnique().HasFilter("source = 'BrowserCapture' AND status = 'Pending'");

        // A scheduled job will need to find recordings whose retention has run out.
        builder.HasIndex(r => r.RetainUntil);

        builder.HasOne(r => r.Meeting)
            .WithMany(m => m.Recordings)
            .HasForeignKey(r => r.MeetingId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasOne(r => r.CreatedBy)
            .WithMany()
            .HasForeignKey(r => r.CreatedById)
            .OnDelete(DeleteBehavior.Restrict);
    }
}
