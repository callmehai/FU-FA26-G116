using Microsoft.EntityFrameworkCore;
using TrustCabinet.Domain.Access;
using TrustCabinet.Domain.Entities;
using TrustCabinet.Domain.Enums;
using TrustCabinet.Infrastructure.Authentication;

namespace TrustCabinet.Infrastructure.Seed;

/// <summary>
/// Demo rows for local development — NOT reference data, and never seeded outside Development.
///
/// The meetings once mirrored the frontend's hard-coded sampleData.ts, so screens built against it
/// looked the same on the real endpoint. SCRUM-18 deleted that file; this is now the only copy.
/// </summary>
public static class DevelopmentDataSeeder
{
    /// <summary>
    /// The password every demo account shares locally. Publishing it is fine precisely because it
    /// only ever exists in a Development database full of made-up meetings.
    /// </summary>
    public const string DemoPassword = "TrustCabinet@2026";

    // Stable development identity prevents reseeding a meeting a tester has already edited.
    private static readonly Guid WorkflowDemoMeetingId = Guid.Parse("0199ac54-d000-7000-8000-000000000019");

    /// <summary>
    /// Fills an empty database with demo rows, repairs old placeholder passwords, and adds the
    /// workflow demo once when the original demo accounts are present.
    ///
    /// Idempotent either way. It never duplicates rows, and the only rows it ever writes to on a
    /// populated database are demo accounts still holding the pre-SCRUM-11 placeholder hash.
    /// The workflow demo is inserted once, never reset, including after it has been published.
    /// </summary>
    public static async Task SeedAsync(TrustCabinetDbContext db, CancellationToken ct = default)
    {
        // Demo accounts all share one well-known password, hashed here at seed time rather than
        // pasted in as a literal — a hash in the source would still be a credential in a public
        // repository, and it would silently stop matching if the hasher's parameters changed.
        //
        // Development only: this seeder never runs outside it (see MigrateAndSeedDevelopmentAsync),
        // and nothing in a real deployment has a known password.
        var demoPasswordHash = new PasswordHasher().Hash(DemoPassword);

        if (await db.Users.AnyAsync(ct))
        {
            await RepairPlaceholderPasswordsAsync(db, demoPasswordHash, ct);
            await SeedWorkflowDemoAsync(db, ct);
            await EnsureCallRoomDemoParticipantsAsync(db, ct);
            return;
        }

        var chair = new User
        {
            FullName = "Trần Chủ Toạ",
            Email = "chutoa@trustcabinet.local",
            PasswordHash = demoPasswordHash,
            RoleId = BuiltInRoles.Chair,
        };
        var secretary = new User
        {
            FullName = "Nguyễn Thư Ký",
            Email = "thuky@trustcabinet.local",
            PasswordHash = demoPasswordHash,
            RoleId = BuiltInRoles.Secretary,
        };
        var member = new User
        {
            FullName = "Lê Thành Viên",
            Email = "thanhvien@trustcabinet.local",
            PasswordHash = demoPasswordHash,
            RoleId = BuiltInRoles.Member,
        };
        var administrator = new User
        {
            FullName = "Phạm Quản Trị",
            Email = "quantri@trustcabinet.local",
            PasswordHash = demoPasswordHash,
            RoleId = BuiltInRoles.Admin,
        };

        db.AddRange(chair, secretary, member, administrator);

        var meetings = new[]
        {
            NewMeeting("Giao ban tuần 38", MeetingStatus.Published, Sensitivity.Internal,
                MeetingMode.Hybrid, "Phòng họp A — Tầng 4",
                "2026-09-21T02:00:00Z", "2026-09-21T03:30:00Z", secretary),
            NewMeeting("Phiên họp Hội đồng quản trị quý III", MeetingStatus.WaitingApproval, Sensitivity.Secret,
                MeetingMode.InPerson, "Phòng họp Hội đồng",
                "2026-09-24T07:00:00Z", "2026-09-24T10:00:00Z", secretary),
            NewMeeting("Rà soát kế hoạch chuyển đổi số", MeetingStatus.InProgress, Sensitivity.Confidential,
                MeetingMode.Online, string.Empty,
                "2026-09-18T01:30:00Z", "2026-09-18T03:00:00Z", secretary),
            NewMeeting("Lấy ý kiến dự thảo quy chế chi tiêu nội bộ", MeetingStatus.Draft, Sensitivity.Normal,
                MeetingMode.Online, string.Empty,
                "2026-09-29T02:00:00Z", "2026-09-29T03:00:00Z", secretary),
            NewMeeting("Tổng kết công tác quý II", MeetingStatus.Ended, Sensitivity.Internal,
                MeetingMode.InPerson, "Hội trường lớn",
                "2026-09-08T07:00:00Z", "2026-09-08T09:30:00Z", secretary),
        };

        meetings[0].AssignedApprover = chair;
        meetings[1].AssignedApprover = chair;

        db.AddRange(meetings);

        var published = meetings[0];

        db.AddRange(
            new MeetingParticipant { Meeting = published, User = chair, Role = MeetingRole.Chair, Response = InvitationResponse.Accepted, RespondedAt = At("2026-09-16T10:00:00Z") },
            new MeetingParticipant { Meeting = published, User = secretary, Role = MeetingRole.Secretary, Response = InvitationResponse.Accepted, RespondedAt = At("2026-09-16T09:25:00Z") },
            new MeetingParticipant { Meeting = published, User = member, Role = MeetingRole.Member });

        // The in-progress online meeting is where the call room is tried out (SCRUM-41), and only
        // invited participants may enter it.
        var inProgress = meetings[2];
        db.AddRange(
            new MeetingParticipant { Meeting = inProgress, User = chair, Role = MeetingRole.Chair, Response = InvitationResponse.Accepted },
            new MeetingParticipant { Meeting = inProgress, User = secretary, Role = MeetingRole.Secretary, Response = InvitationResponse.Accepted },
            new MeetingParticipant { Meeting = inProgress, User = member, Role = MeetingRole.Member, Response = InvitationResponse.Accepted });

        // The same four steps the detail screen's timeline shows today.
        db.AddRange(
            NewHistory(published, null, MeetingStatus.Draft, secretary, "2026-09-16T08:12:00Z"),
            NewHistory(published, MeetingStatus.Draft, MeetingStatus.WaitingApproval, secretary, "2026-09-16T08:40:00Z"),
            NewHistory(published, MeetingStatus.WaitingApproval, MeetingStatus.Approved, chair, "2026-09-16T09:15:00Z"),
            NewHistory(published, MeetingStatus.Approved, MeetingStatus.Published, secretary, "2026-09-16T09:20:00Z"));

        // Two inbox rows make the SCRUM-14 list/read/read-all APIs testable in Postman on a fresh
        // development database without exposing an arbitrary notification-creation endpoint.
        db.AddRange(
            new Notification
            {
                User = member,
                Meeting = published,
                Type = NotificationType.MeetingInvited,
                TargetType = "Meeting",
                TargetId = published.Id,
                Title = "Bạn được mời họp",
                Body = $"Bạn được mời tham dự cuộc họp \"{published.Title}\".",
                DeduplicationKey = $"demo:meeting-invited:{published.Id:D}:{member.Id:D}",
                PushedAt = published.CreatedAt,
            },
            new Notification
            {
                User = member,
                Meeting = published,
                Type = NotificationType.MeetingPublished,
                TargetType = "Meeting",
                TargetId = published.Id,
                Title = "Cuộc họp đã công bố",
                Body = $"Cuộc họp \"{published.Title}\" đã được công bố.",
                DeduplicationKey = $"demo:meeting-published:{published.Id:D}:{member.Id:D}",
                PushedAt = published.CreatedAt,
            });

        await db.SaveChangesAsync(ct);
        await SeedWorkflowDemoAsync(db, ct);
    }

    private static async Task SeedWorkflowDemoAsync(TrustCabinetDbContext db, CancellationToken ct)
    {
        if (await db.Meetings.AnyAsync(meeting => meeting.Id == WorkflowDemoMeetingId, ct))
        {
            return;
        }

        var accounts = await db.Users
            .Where(user => user.IsActive && (
                user.Email == "thuky@trustcabinet.local"
                || user.Email == "chutoa@trustcabinet.local"
                || user.Email == "thanhvien@trustcabinet.local"))
            .ToListAsync(ct);
        var organizer = accounts.SingleOrDefault(user => user.Email == "thuky@trustcabinet.local");
        var chair = accounts.SingleOrDefault(user => user.Email == "chutoa@trustcabinet.local");
        var member = accounts.SingleOrDefault(user => user.Email == "thanhvien@trustcabinet.local");
        if (organizer is null || chair is null || member is null)
        {
            return;
        }

        var now = DateTimeOffset.UtcNow;
        var meeting = new Meeting
        {
            Id = WorkflowDemoMeetingId,
            Title = "SCRUM-19 — Luồng duyệt và công bố (demo)",
            Description = "Dữ liệu Development để kiểm thử readiness, gửi duyệt, từ chối và công bố qua API.",
            Status = MeetingStatus.Draft,
            Sensitivity = Sensitivity.Internal,
            Mode = MeetingMode.InPerson,
            Location = "Phòng họp demo SCRUM-19",
            PlannedStartTime = now.AddDays(1),
            PlannedEndTime = now.AddDays(1).AddHours(1),
            CreatedBy = organizer,
        };
        meeting.AgendaItems.Add(new AgendaItem
        {
            Meeting = meeting,
            Position = 1,
            Title = "Rà soát kế hoạch triển khai",
            Description = "Agenda mẫu đã đủ thông tin để gửi duyệt.",
            PlannedDurationMinutes = 30,
            Presenter = member,
            Status = AgendaItemStatus.Pending,
        });
        meeting.Participants.Add(new MeetingParticipant { Meeting = meeting, User = organizer, Role = MeetingRole.Secretary });
        meeting.Participants.Add(new MeetingParticipant { Meeting = meeting, User = chair, Role = MeetingRole.Chair });
        meeting.Participants.Add(new MeetingParticipant { Meeting = meeting, User = member, Role = MeetingRole.Member });
        meeting.StatusHistory.Add(new MeetingStatusHistory
        {
            Meeting = meeting,
            ToStatus = MeetingStatus.Draft,
            Action = MeetingHistoryAction.Create,
            Actor = organizer,
            OccurredAt = now,
        });
        db.Meetings.Add(meeting);
        await db.SaveChangesAsync(ct);
    }

    /// <summary>
    /// The placeholder that stood in for a password hash before SCRUM-11 introduced the hasher.
    /// </summary>
    private const string PlaceholderPasswordHash = "SET-BY-SCRUM-11";

    /// <summary>
    /// Gives a usable password to demo accounts seeded before SCRUM-11 existed.
    ///
    /// Without this, anyone whose development database predates this story keeps four accounts
    /// whose password_hash is the literal "SET-BY-SCRUM-11" — the correct demo password is
    /// rejected, indistinguishably from a wrong one, and sign-in looks broken. The main seeder
    /// cannot help: it stops as soon as any user exists.
    ///
    /// Narrow on purpose. It only ever touches rows holding exactly that placeholder, so a
    /// password someone set by hand is never overwritten, and it is a no-op on a fresh database.
    /// Development only, like everything else here.
    /// </summary>
    private static async Task RepairPlaceholderPasswordsAsync(
        TrustCabinetDbContext db, string demoPasswordHash, CancellationToken ct)
    {
        var stale = await db.Users
            .Where(u => u.PasswordHash == PlaceholderPasswordHash)
            .ToArrayAsync(ct);

        if (stale.Length == 0)
        {
            return;
        }

        foreach (var user in stale)
        {
            user.PasswordHash = demoPasswordHash;
            user.UpdatedAt = DateTimeOffset.UtcNow;
        }

        await db.SaveChangesAsync(ct);
    }

    private const string CallRoomDemoMeetingTitle = "Rà soát kế hoạch chuyển đổi số";

    /// <summary>
    /// Invites the three demo users to the in-progress demo meeting on databases seeded before
    /// SCRUM-41, which left it without participants — so nobody could enter its call room.
    ///
    /// Narrow like <see cref="RepairPlaceholderPasswordsAsync"/>: only that meeting, only while it
    /// is still in progress and still has nobody invited, only with demo accounts that exist.
    /// </summary>
    private static async Task EnsureCallRoomDemoParticipantsAsync(TrustCabinetDbContext db, CancellationToken ct)
    {
        var meeting = await db.Meetings.SingleOrDefaultAsync(
            m => m.Title == CallRoomDemoMeetingTitle
                && m.Status == MeetingStatus.InProgress
                && !m.Participants.Any(),
            ct);
        if (meeting is null)
        {
            return;
        }

        var invitations = new (string Email, MeetingRole Role)[]
        {
            ("chutoa@trustcabinet.local", MeetingRole.Chair),
            ("thuky@trustcabinet.local", MeetingRole.Secretary),
            ("thanhvien@trustcabinet.local", MeetingRole.Member),
        };
        foreach (var (email, role) in invitations)
        {
            var user = await db.Users.SingleOrDefaultAsync(u => u.Email == email, ct);
            if (user is not null)
            {
                db.MeetingParticipants.Add(new MeetingParticipant
                {
                    MeetingId = meeting.Id,
                    UserId = user.Id,
                    Role = role,
                    Response = InvitationResponse.Accepted,
                });
            }
        }

        await db.SaveChangesAsync(ct);
    }

    private static Meeting NewMeeting(
        string title, MeetingStatus status, Sensitivity sensitivity, MeetingMode mode,
        string location, string startsAt, string endsAt, User createdBy)
    {
        // A meeting that has started or ended carries the times the Chair would have recorded.
        var started = status is MeetingStatus.InProgress or MeetingStatus.Ended;
        return new Meeting
        {
            Title = title,
            Status = status,
            Sensitivity = sensitivity,
            Mode = mode,
            Location = location,
            PlannedStartTime = At(startsAt),
            PlannedEndTime = At(endsAt),
            ActualStartedAt = started ? At(startsAt) : null,
            ActualEndedAt = status == MeetingStatus.Ended ? At(endsAt) : null,
            CreatedBy = createdBy,
        };
    }

    private static MeetingStatusHistory NewHistory(
        Meeting meeting, MeetingStatus? from, MeetingStatus to, User actor, string occurredAt) => new()
        {
            Meeting = meeting,
            FromStatus = from,
            ToStatus = to,
            Action = (from, to) switch
            {
                (null, MeetingStatus.Draft) => MeetingHistoryAction.Create,
                (MeetingStatus.Draft, MeetingStatus.WaitingApproval) => MeetingHistoryAction.SubmitForApproval,
                (MeetingStatus.WaitingApproval, MeetingStatus.Approved) => MeetingHistoryAction.Approve,
                (MeetingStatus.WaitingApproval, MeetingStatus.Rejected) => MeetingHistoryAction.Reject,
                (MeetingStatus.Rejected, MeetingStatus.Draft) => MeetingHistoryAction.ReturnToDraft,
                (MeetingStatus.Approved, MeetingStatus.Published) => MeetingHistoryAction.Publish,
                _ => MeetingHistoryAction.LegacyTransition,
            },
            AssignedApproverId = to == MeetingStatus.WaitingApproval ? meeting.AssignedApproverId : null,
            Actor = actor,
            OccurredAt = At(occurredAt),
        };

    private static DateTimeOffset At(string iso) => DateTimeOffset.Parse(iso, System.Globalization.CultureInfo.InvariantCulture);
}
