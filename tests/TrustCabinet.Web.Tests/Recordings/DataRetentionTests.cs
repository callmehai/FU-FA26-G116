using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using TrustCabinet.Domain.Audit;
using TrustCabinet.Domain.Entities;
using TrustCabinet.Domain.Enums;
using TrustCabinet.Application.Retention;

namespace TrustCabinet.Web.Tests.Recordings;

/// <summary>
/// ADR-0067 — the retention job: audio files no row points at, old notifications, expired guest codes and old email
/// bodies. Vote drafts of cancelled meetings are covered with the meeting lifecycle (MeetingLifecycleVoteDraftTests).
/// </summary>
public sealed class DataRetentionTests(RecordingFixture fixture) : IClassFixture<RecordingFixture>
{
    private DateTimeOffset Now => fixture.Clock.Now;

    private async Task<T> Run<T>(Func<IDataRetentionService, Task<T>> rule)
    {
        using var scope = fixture.Host.Services.CreateScope();
        return await rule(scope.ServiceProvider.GetRequiredService<IDataRetentionService>());
    }

    private async Task<int> AuditCount(string action)
    {
        await using var db = fixture.Db();
        return await db.AuditLog.CountAsync(a => a.Action == action);
    }

    /// <summary>
    /// PO 19/11/2026 — the run just made wrote exactly one summary row for <paramref name="action"/>: no actor, no object
    /// id, the count it removed and the cutoff in the details.
    /// </summary>
    private async Task<JsonElement> AssertOneSummaryRow(string action, string objectType, int before, int removed)
    {
        await using var db = fixture.Db();
        Assert.Equal(before + 1, await db.AuditLog.CountAsync(a => a.Action == action));
        var row = await db.AuditLog.AsNoTracking().Where(a => a.Action == action).OrderByDescending(a => a.OccurredAt).FirstAsync();
        Assert.Null(row.ActorId);
        Assert.Null(row.ObjectId);
        Assert.Equal(objectType, row.ObjectType);
        Assert.True(AuditEventCatalog.Contains(action));
        var details = JsonDocument.Parse(row.Details).RootElement;
        Assert.Equal(removed, details.GetProperty("rows").GetInt32());
        Assert.Equal(JsonValueKind.String, details.GetProperty("cutoff").ValueKind);
        return details;
    }

    /// <summary>A file under the root (or anywhere, for <paramref name="full"/>), last written <paramref name="age"/> ago.</summary>
    private string File(string relative, TimeSpan age, string? full = null)
    {
        var path = full ?? Path.Combine(fixture.StorageRoot, relative);
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        System.IO.File.WriteAllBytes(path, new byte[100]);
        System.IO.File.SetLastWriteTimeUtc(path, (Now - age).UtcDateTime);
        return path;
    }

    private Recording RecordingAt(RecordingFixture.Scenario s, string path, RecordingStatus status) => new()
    {
        MeetingId = s.MeetingId, Source = RecordingSource.Upload, Status = status, FileName = "a.wav", StoragePath = path,
        ContentType = "audio/wav", ByteSize = 100, CreatedById = s.Secretary, CreatedAt = Now, UpdatedAt = Now,
    };

    [Fact]
    public async Task The_orphan_sweep_deletes_old_unreferenced_audio_only_and_never_leaves_the_folder()
    {
        var s = await fixture.Seed(MeetingStatus.Ended);
        var old = TimeSpan.FromHours(25);
        var orphan = File("2026-09/orphan.wav", old);
        var erasedButLeft = File("2026-09/erased.webm", old);
        var live = File("2026-09/live.wav", old);
        var sentence = File("2026-09/sentence.ogg", old);
        var young = File("2026-10/young.wav", TimeSpan.FromHours(2));
        var notAudio = File("2026-09/notes.txt", old);
        // Outside the root, reached through a link inside it: neither the link nor its target is touched.
        var outsideDir = Path.Combine(Path.GetTempPath(), $"tc-outside-{Guid.NewGuid():N}");
        var outside = File("", old, Path.Combine(outsideDir, "outside.wav"));
        Directory.CreateSymbolicLink(Path.Combine(fixture.StorageRoot, "linked-dir"), outsideDir);
        var linkedFile = Path.Combine(fixture.StorageRoot, "2026-09", "linked.wav");
        System.IO.File.CreateSymbolicLink(linkedFile, outside);
        try
        {
            await using (var db = fixture.Db())
            {
                db.Recordings.AddRange(RecordingAt(s, "2026-09/live.wav", RecordingStatus.Available),
                    RecordingAt(s, "2026-09/erased.webm", RecordingStatus.Deleted));
                db.SpeechClips.Add(new SpeechClip
                {
                    MeetingId = s.MeetingId, SpeakerId = s.Member, ClientClipId = Guid.NewGuid(), StartedAt = Now, DurationMs = 1000,
                    Text = "x", AudioPath = "2026-09/sentence.ogg", AudioContentType = "audio/ogg", CreatedAt = Now, UpdatedAt = Now,
                });
                await db.SaveChangesAsync();
            }

            var (files, bytes) = await Run(r => r.DeleteOrphanAudioAsync());

            Assert.Equal(2, files);
            Assert.Equal(200, bytes);
            Assert.False(System.IO.File.Exists(orphan));
            Assert.False(System.IO.File.Exists(erasedButLeft));
            Assert.True(System.IO.File.Exists(live));
            Assert.True(System.IO.File.Exists(sentence));
            Assert.True(System.IO.File.Exists(young));
            Assert.True(System.IO.File.Exists(notAudio));
            Assert.True(System.IO.File.Exists(outside));
            Assert.True(System.IO.File.Exists(linkedFile));
            await using var check = fixture.Db();
            var audit = await check.AuditLog.AsNoTracking().Where(a => a.Action == AuditActions.RecordingOrphanFilesDeleted)
                .OrderByDescending(a => a.OccurredAt).FirstAsync();
            Assert.Null(audit.ActorId);
            Assert.Equal(AuditActions.RecordingStorageObjectType, audit.ObjectType);
            var details = JsonDocument.Parse(audit.Details).RootElement;
            Assert.Equal(2, details.GetProperty("files").GetInt32());
            Assert.Equal(200, details.GetProperty("bytes").GetInt64());

            // A second run finds nothing more and writes no second row.
            Assert.Equal((0, 0L), await Run(r => r.DeleteOrphanAudioAsync()));
            Assert.Equal(1, await check.AuditLog.CountAsync(a => a.Action == AuditActions.RecordingOrphanFilesDeleted));
        }
        finally
        {
            System.IO.File.Delete(linkedFile);
            Directory.Delete(Path.Combine(fixture.StorageRoot, "linked-dir"));
            Directory.Delete(outsideDir, true);
        }
    }

    [Fact]
    public async Task Notifications_go_90_days_after_being_read_and_180_days_in_any_case()
    {
        var s = await fixture.Seed();
        Notification At(int daysAgo, bool read) => new()
        {
            UserId = s.Member, Type = NotificationType.MeetingInvited, Title = $"{daysAgo}-{read}",
            CreatedAt = Now.AddDays(-daysAgo), UpdatedAt = Now.AddDays(-daysAgo), ReadAt = read ? Now.AddDays(-daysAgo) : null,
        };
        await using (var db = fixture.Db())
        {
            db.Notifications.AddRange(At(91, read: true), At(89, read: true), At(170, read: false), At(181, read: false));
            await db.SaveChangesAsync();
        }

        var before = await AuditCount(AuditActions.NotificationsPurged);
        Assert.Equal(2, await Run(r => r.PruneNotificationsAsync()));

        await using var check = fixture.Db();
        var left = await check.Notifications.Where(n => n.UserId == s.Member).Select(n => n.Title).ToListAsync();
        Assert.Equal(new[] { "170-False", "89-True" }, left.Order());
        var details = await AssertOneSummaryRow(AuditActions.NotificationsPurged, AuditActions.NotificationObjectType, before, 2);
        Assert.Equal(JsonValueKind.String, details.GetProperty("readCutoff").ValueKind);

        // A run with nothing left to delete writes no row.
        Assert.Equal(0, await Run(r => r.PruneNotificationsAsync()));
        Assert.Equal(before + 1, await AuditCount(AuditActions.NotificationsPurged));
    }

    [Fact]
    public async Task Guest_codes_go_a_day_after_they_expire_and_sessions_stay()
    {
        var s = await fixture.Seed();
        Guid invitationId, sessionId;
        await using (var db = fixture.Db())
        {
            var invitation = new MeetingGuestInvitation
            {
                MeetingId = s.MeetingId, InvitedById = s.Secretary, InvitedName = "Khách", Email = $"{Guid.NewGuid():N}@guest.test",
            };
            db.MeetingGuestInvitations.Add(invitation);
            db.GuestVerificationChallenges.AddRange(
                new GuestVerificationChallenge { InvitationId = invitation.Id, CodeHash = "old", ExpiresAt = Now.AddDays(-2), CreatedAt = Now.AddDays(-2) },
                new GuestVerificationChallenge { InvitationId = invitation.Id, CodeHash = "recent", ExpiresAt = Now.AddHours(-2), CreatedAt = Now.AddHours(-3) });
            var session = new GuestSession
            {
                InvitationId = invitation.Id, ExpiresAt = Now.AddDays(-30),
                CreatedAt = Now.AddDays(-31), UpdatedAt = Now.AddDays(-31),
            };
            db.GuestSessions.Add(session);
            await db.SaveChangesAsync();
            (invitationId, sessionId) = (invitation.Id, session.Id);
        }

        var before = await AuditCount(AuditActions.GuestCodesPurged);
        var deleted = await Run(r => r.PruneGuestChallengesAsync());
        Assert.True(deleted >= 1);
        await AssertOneSummaryRow(AuditActions.GuestCodesPurged, AuditActions.GuestVerificationCodeObjectType, before, deleted);
        Assert.Equal(0, await Run(r => r.PruneGuestChallengesAsync()));
        Assert.Equal(before + 1, await AuditCount(AuditActions.GuestCodesPurged));

        await using var check = fixture.Db();
        Assert.Equal(new[] { "recent" },
            await check.GuestVerificationChallenges.Where(c => c.InvitationId == invitationId).Select(c => c.CodeHash).ToListAsync());
        Assert.True(await check.GuestSessions.AnyAsync(x => x.Id == sessionId));
    }

    [Fact]
    public async Task Old_email_bodies_are_cleared_and_the_row_kept()
    {
        EmailMessage Email(EmailStatus status, int daysAgo) => new()
        {
            DeduplicationKey = $"retention-{Guid.NewGuid():N}", ToAddress = "nguoi.nhan@example.test", Subject = $"{status}-{daysAgo}",
            Body = "Nội dung", HtmlBody = "<p>Nội dung</p>", CalendarAttachment = "BEGIN:VCALENDAR", Status = status,
            SentAt = status == EmailStatus.Sent ? Now.AddDays(-daysAgo) : null,
            CreatedAt = Now.AddDays(-daysAgo), UpdatedAt = Now.AddDays(-daysAgo),
        };
        var sentOld = Email(EmailStatus.Sent, 31);
        var failedOld = Email(EmailStatus.Failed, 40);
        var sentRecent = Email(EmailStatus.Sent, 29);
        var queuedOld = Email(EmailStatus.Queued, 40);
        await using (var db = fixture.Db())
        {
            db.EmailMessages.AddRange(sentOld, failedOld, sentRecent, queuedOld);
            await db.SaveChangesAsync();
        }

        var before = await AuditCount(AuditActions.EmailBodiesCleared);
        Assert.Equal(2, await Run(r => r.ScrubEmailBodiesAsync()));
        await AssertOneSummaryRow(AuditActions.EmailBodiesCleared, AuditActions.EmailObjectType, before, 2);
        Assert.Equal(0, await Run(r => r.ScrubEmailBodiesAsync()));
        Assert.Equal(before + 1, await AuditCount(AuditActions.EmailBodiesCleared));

        await using var check = fixture.Db();
        var rows = await check.EmailMessages.AsNoTracking()
            .Where(e => new[] { sentOld.Id, failedOld.Id, sentRecent.Id, queuedOld.Id }.Contains(e.Id)).ToDictionaryAsync(e => e.Id);
        foreach (var scrubbed in new[] { rows[sentOld.Id], rows[failedOld.Id] })
        {
            Assert.Equal("", scrubbed.Body);
            Assert.Null(scrubbed.HtmlBody);
            Assert.Null(scrubbed.CalendarAttachment);
            // Who, what and when stay as the record that it was sent.
            Assert.Equal("nguoi.nhan@example.test", scrubbed.ToAddress);
            Assert.False(string.IsNullOrEmpty(scrubbed.Subject));
        }
        Assert.Equal(sentOld.SentAt, rows[sentOld.Id].SentAt);
        Assert.Equal(EmailStatus.Sent, rows[sentOld.Id].Status);
        Assert.Equal("Nội dung", rows[sentRecent.Id].Body);
        Assert.Equal("BEGIN:VCALENDAR", rows[sentRecent.Id].CalendarAttachment);
        // Still waiting to go out: its body is what will be sent.
        Assert.Equal("<p>Nội dung</p>", rows[queuedOld.Id].HtmlBody);
    }
}
