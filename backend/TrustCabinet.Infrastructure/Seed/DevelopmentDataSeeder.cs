using Microsoft.EntityFrameworkCore;
using TrustCabinet.Domain.Entities;
using TrustCabinet.Domain.Enums;

namespace TrustCabinet.Infrastructure.Seed;

/// <summary>
/// Demo rows for local development — NOT reference data, and never seeded outside Development.
///
/// The meetings mirror frontend/src/features/meetings/sampleData.ts, so screens that were built
/// against the hard-coded array look the same once they read the real endpoint. When SCRUM-46
/// deletes that file, this becomes the only copy.
/// </summary>
public static class DevelopmentDataSeeder
{
    /// <summary>
    /// Fills an empty database with demo rows. Idempotent: it does nothing when users already
    /// exist, so restarting the app never duplicates or overwrites what you were working on.
    /// </summary>
    public static async Task SeedAsync(TrustCabinetDbContext db, CancellationToken ct = default)
    {
        if (await db.Users.AnyAsync(ct))
        {
            return;
        }

        var board = new OrgUnit { Name = "Ban Giám đốc", Code = "BGD" };
        var planning = new OrgUnit { Name = "Phòng Kế hoạch - Đầu tư", Code = "P.KHDT", Parent = board };
        var admin = new OrgUnit { Name = "Phòng Hành chính - Tổng hợp", Code = "P.HCTH", Parent = board };

        // PasswordHash is a placeholder, not a hash of anything: local sign-in does not exist yet.
        // SCRUM-11 introduces the hasher and must replace these values — until then no seeded
        // account can be logged into, which is the safe failure for a public repository.
        const string noLoginYet = "SET-BY-SCRUM-11";

        var chair = new User
        {
            FullName = "Trần Chủ Toạ",
            Email = "chutoa@trustcabinet.local",
            PasswordHash = noLoginYet,
            Role = SystemRole.Chair,
            OrgUnit = board,
        };
        var secretary = new User
        {
            FullName = "Nguyễn Thư Ký",
            Email = "thuky@trustcabinet.local",
            PasswordHash = noLoginYet,
            Role = SystemRole.Secretary,
            OrgUnit = admin,
        };
        var member = new User
        {
            FullName = "Lê Thành Viên",
            Email = "thanhvien@trustcabinet.local",
            PasswordHash = noLoginYet,
            Role = SystemRole.Member,
            OrgUnit = planning,
        };
        var administrator = new User
        {
            FullName = "Phạm Quản Trị",
            Email = "quantri@trustcabinet.local",
            PasswordHash = noLoginYet,
            Role = SystemRole.Admin,
            OrgUnit = admin,
        };

        db.AddRange(board, planning, admin, chair, secretary, member, administrator);

        var meetings = new[]
        {
            NewMeeting("Giao ban tuần 38", MeetingStatus.Published, Sensitivity.Internal,
                MeetingMode.Hybrid, "Phòng họp A — Tầng 4",
                "2026-09-21T02:00:00Z", "2026-09-21T03:30:00Z", secretary, board),
            NewMeeting("Phiên họp Hội đồng quản trị quý III", MeetingStatus.WaitingApproval, Sensitivity.Secret,
                MeetingMode.InPerson, "Phòng họp Hội đồng",
                "2026-09-24T07:00:00Z", "2026-09-24T10:00:00Z", secretary, board),
            NewMeeting("Rà soát kế hoạch chuyển đổi số", MeetingStatus.InProgress, Sensitivity.Confidential,
                MeetingMode.Online, "Trực tuyến",
                "2026-09-18T01:30:00Z", "2026-09-18T03:00:00Z", secretary, planning),
            NewMeeting("Lấy ý kiến dự thảo quy chế chi tiêu nội bộ", MeetingStatus.Draft, Sensitivity.Normal,
                MeetingMode.Online, "Trực tuyến",
                "2026-09-29T02:00:00Z", "2026-09-29T03:00:00Z", secretary, admin),
            NewMeeting("Tổng kết công tác quý II", MeetingStatus.MinutesIssued, Sensitivity.Internal,
                MeetingMode.InPerson, "Hội trường lớn",
                "2026-09-08T07:00:00Z", "2026-09-08T09:30:00Z", secretary, board),
        };

        db.AddRange(meetings);

        var published = meetings[0];

        db.AddRange(
            new MeetingParticipant { Meeting = published, User = chair, Role = MeetingRole.Chair, Response = InvitationResponse.Accepted, RespondedAt = At("2026-09-16T10:00:00Z") },
            new MeetingParticipant { Meeting = published, User = secretary, Role = MeetingRole.Secretary, Response = InvitationResponse.Accepted, RespondedAt = At("2026-09-16T09:25:00Z") },
            new MeetingParticipant { Meeting = published, User = member, Role = MeetingRole.Member });

        // The same four steps the detail screen's timeline shows today.
        db.AddRange(
            NewHistory(published, null, MeetingStatus.Draft, secretary, "2026-09-16T08:12:00Z"),
            NewHistory(published, MeetingStatus.Draft, MeetingStatus.WaitingApproval, secretary, "2026-09-16T08:40:00Z"),
            NewHistory(published, MeetingStatus.WaitingApproval, MeetingStatus.Approved, chair, "2026-09-16T09:15:00Z"),
            NewHistory(published, MeetingStatus.Approved, MeetingStatus.Published, secretary, "2026-09-16T09:20:00Z"));

        await db.SaveChangesAsync(ct);
    }

    private static Meeting NewMeeting(
        string title, MeetingStatus status, Sensitivity sensitivity, MeetingMode mode,
        string location, string startsAt, string endsAt, User createdBy, OrgUnit unit) => new()
        {
            Title = title,
            Status = status,
            Sensitivity = sensitivity,
            Mode = mode,
            Location = location,
            StartsAt = At(startsAt),
            EndsAt = At(endsAt),
            CreatedBy = createdBy,
            OrgUnit = unit,
        };

    private static MeetingStatusHistory NewHistory(
        Meeting meeting, MeetingStatus? from, MeetingStatus to, User actor, string occurredAt) => new()
        {
            Meeting = meeting,
            FromStatus = from,
            ToStatus = to,
            Actor = actor,
            OccurredAt = At(occurredAt),
        };

    private static DateTimeOffset At(string iso) => DateTimeOffset.Parse(iso, System.Globalization.CultureInfo.InvariantCulture);
}
