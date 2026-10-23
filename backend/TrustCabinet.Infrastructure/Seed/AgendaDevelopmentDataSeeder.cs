using Microsoft.EntityFrameworkCore;
using TrustCabinet.Domain.Documents;
using TrustCabinet.Domain.Entities;
using TrustCabinet.Domain.Enums;

namespace TrustCabinet.Infrastructure.Seed;

/// <summary>Insert-only SCRUM-25 fixtures. Existing meetings and user edits are never reset.</summary>
public static class AgendaDevelopmentDataSeeder
{
    public static readonly Guid PlanningMeetingId = Guid.Parse("25000000-0000-7000-8000-000000000001");
    public static readonly Guid RunningMeetingId = Guid.Parse("25000000-0000-7000-8000-000000000002");
    public static readonly Guid EndedMeetingId = Guid.Parse("25000000-0000-7000-8000-000000000003");

    public static async Task SeedAsync(TrustCabinetDbContext db, CancellationToken ct = default)
    {
        var secretary = await db.Users.SingleOrDefaultAsync(u => u.Email == "thuky@trustcabinet.local", ct);
        var chair = await db.Users.SingleOrDefaultAsync(u => u.Email == "chutoa@trustcabinet.local", ct);
        var member = await db.Users.SingleOrDefaultAsync(u => u.Email == "thanhvien@trustcabinet.local", ct);
        if (secretary is null || chair is null || member is null) return;
        foreach (var (id, status, label) in new[] {
            (PlanningMeetingId, MeetingStatus.Draft, "Soạn chương trình"),
            (RunningMeetingId, MeetingStatus.InProgress, "Điều hành chương trình"),
            (EndedMeetingId, MeetingStatus.Ended, "Cuộc họp đã kết thúc") })
        {
            if (await db.Meetings.AnyAsync(m => m.Id == id, ct)) continue;
            var start = new DateTimeOffset(2026, 10, 12, 1, 0, 0, TimeSpan.Zero);
            var meeting = new Meeting { Id = id, Title = $"[SCRUM-25] {label}", CreatedById = secretary.Id,
                Status = status, PlannedStartTime = start, PlannedEndTime = start.AddHours(1),
                ActualStartedAt = status == MeetingStatus.Draft ? null : start,
                ActualEndedAt = status == MeetingStatus.Ended ? start.AddHours(1) : null };
            db.Meetings.Add(meeting);
            foreach (var (user, role) in new[] { (secretary, MeetingRole.Secretary), (chair, MeetingRole.Chair), (member, MeetingRole.Member) })
                db.MeetingParticipants.Add(new() { MeetingId = id, UserId = user.Id, Role = role });
            var first = new AgendaItem { MeetingId = id, Position = 1, Title = "Báo cáo đầu kỳ", PlannedDurationMinutes = 10,
                PresenterId = member.Id, Status = status == MeetingStatus.Draft ? AgendaItemStatus.Pending : AgendaItemStatus.Done,
                ActualStartedAt = status == MeetingStatus.Draft ? null : start,
                ActualEndedAt = status == MeetingStatus.Draft ? null : start.AddMinutes(5) };
            db.AgendaItems.AddRange(first, new() { MeetingId = id, Position = 4, Title = "Thảo luận kế hoạch", PlannedDurationMinutes = 20 },
                new() { MeetingId = id, Position = 8, Title = "Kết luận", PlannedDurationMinutes = 10, PresenterName = "Báo cáo viên khách mời" });
            // Rows only: these fixtures have no object in the file store, so they list but do not open.
            var unlinked = Fixture(id, "Tài liệu chưa gắn mục (fixture)", secretary.Id);
            var linked = Fixture(id, "Tài liệu đã gắn mục (fixture)", secretary.Id);
            db.MeetingDocuments.AddRange(unlinked, linked);
            db.MeetingDocumentAgendaItems.Add(new() { MeetingDocumentId = linked.Id, AgendaItemId = first.Id });
        }
        await db.SaveChangesAsync(ct);
    }

    private static MeetingDocument Fixture(Guid meetingId, string fileName, Guid uploaderId)
    {
        var fileId = Guid.CreateVersion7();
        var now = DateTimeOffset.UtcNow;
        return new MeetingDocument { MeetingId = meetingId, FileName = fileName, AddedById = uploaderId, AddedAt = now,
            StoredFile = new StoredFile { Id = fileId, StorageKey = DocumentFileRules.StorageKey(fileId), OriginalFileName = fileName,
                ContentType = "application/pdf", ContentSha256 = new string('0', 64), UploadedById = uploaderId, UploadedAt = now } };
    }
}
