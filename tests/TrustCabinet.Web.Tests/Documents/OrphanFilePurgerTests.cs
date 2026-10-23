using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using TrustCabinet.Domain.Entities;
using TrustCabinet.Infrastructure.Documents;

namespace TrustCabinet.Web.Tests.Documents;

/// <summary>BR-213 — the orphan purge, against PostgreSQL.</summary>
[Collection("Documents")]
public sealed class OrphanFilePurgerTests(DocumentFixture fixture) : IAsyncLifetime
{
    private static readonly DateTimeOffset Now = new(2026, 10, 24, 12, 0, 0, TimeSpan.Zero);
    private static readonly DateTimeOffset Old = Now.AddHours(-25);

    public async Task InitializeAsync()
    {
        fixture.Storage.Reset();
        // Purge candidates are global, so start each test from no stored files at all.
        await using var db = fixture.Db();
        await db.Database.ExecuteSqlRawAsync(
            "DELETE FROM document_comments; DELETE FROM meeting_document_agenda_items; DELETE FROM meeting_documents; DELETE FROM documents; DELETE FROM folders; DELETE FROM stored_files;");
    }

    public Task DisposeAsync() { fixture.Storage.Reset(); return Task.CompletedTask; }

    [Fact]
    public async Task Only_unreferenced_files_past_the_grace_period_are_purged()
    {
        var s = await fixture.Seed();
        await using var db = fixture.Db();
        var orphan = File(s.Secretary, Old);
        var removedFromMeeting = File(s.Secretary, Old);
        var inMeeting = File(s.Secretary, Old);
        var inLibrary = File(s.Secretary, Old);
        var recent = File(s.Secretary, Now.AddHours(-23));
        db.StoredFiles.AddRange(orphan, removedFromMeeting, inMeeting, inLibrary, recent);
        db.MeetingDocuments.AddRange(
            Attach(s, removedFromMeeting, removedAt: Now.AddHours(-1)),
            Attach(s, inMeeting, removedAt: null));
        db.Documents.Add(new Document { OwnerId = s.Secretary, StoredFileId = inLibrary.Id, FileName = "x.pdf" });
        await db.SaveChangesAsync();
        foreach (var file in new[] { orphan, removedFromMeeting, inMeeting, inLibrary, recent })
            fixture.Storage.Objects[file.StorageKey] = [1];

        Assert.Equal(2, await Purger(db).PurgeAsync(default));

        var rows = await fixture.Db().StoredFiles.AsNoTracking().ToDictionaryAsync(f => f.Id);
        // The row stays, stamped; only the object goes.
        Assert.Equal(Now, rows[orphan.Id].PurgedAt);
        Assert.Equal(Now, rows[removedFromMeeting.Id].PurgedAt);
        Assert.False(fixture.Storage.Objects.ContainsKey(orphan.StorageKey));
        Assert.False(fixture.Storage.Objects.ContainsKey(removedFromMeeting.StorageKey));
        foreach (var kept in new[] { inMeeting, inLibrary, recent })
        {
            Assert.Null(rows[kept.Id].PurgedAt);
            Assert.True(fixture.Storage.Objects.ContainsKey(kept.StorageKey));
        }

        // Already purged files are not picked up again.
        Assert.Equal(0, await Purger(db).PurgeAsync(default));
    }

    [Fact]
    public async Task A_failed_delete_leaves_the_file_for_the_next_run()
    {
        var s = await fixture.Seed();
        await using var db = fixture.Db();
        var orphan = File(s.Secretary, Old);
        db.StoredFiles.Add(orphan);
        await db.SaveChangesAsync();
        fixture.Storage.FailDelete = true;

        Assert.Equal(0, await Purger(db).PurgeAsync(default));
        Assert.Null((await fixture.Db().StoredFiles.SingleAsync(f => f.Id == orphan.Id)).PurgedAt);

        fixture.Storage.FailDelete = false;
        Assert.Equal(1, await Purger(db).PurgeAsync(default));
    }

    private OrphanFilePurger Purger(Infrastructure.TrustCabinetDbContext db) => new(db, fixture.Storage, new FixedClock(),
        Options.Create(new FileStorageOptions { OrphanGracePeriodHours = 24 }), NullLogger<OrphanFilePurger>.Instance);

    private static StoredFile File(Guid uploader, DateTimeOffset uploadedAt)
    {
        var id = Guid.CreateVersion7();
        return new StoredFile { Id = id, StorageKey = $"stored-files/{id:D}", OriginalFileName = "x.pdf",
            ContentType = "application/pdf", ContentSha256 = new string('a', 64), ByteSize = 1,
            UploadedById = uploader, UploadedAt = uploadedAt };
    }

    private static MeetingDocument Attach(DocumentFixture.Scenario s, StoredFile file, DateTimeOffset? removedAt)
        => new() { MeetingId = s.MeetingId, StoredFileId = file.Id, FileName = "x.pdf", AddedById = s.Secretary,
            AddedAt = Old, RemovedAt = removedAt, RemovedById = removedAt is null ? null : s.Secretary };

    private sealed class FixedClock : TimeProvider
    {
        public override DateTimeOffset GetUtcNow() => Now;
    }
}
