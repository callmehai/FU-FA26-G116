using Microsoft.EntityFrameworkCore;
using TrustCabinet.Domain.Access;
using TrustCabinet.Domain.Entities;
using TrustCabinet.Domain.Enums;
using TrustCabinet.Infrastructure.Authentication;

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
    /// The password every demo account shares locally. Publishing it is fine precisely because it
    /// only ever exists in a Development database full of made-up meetings.
    /// </summary>
    public const string DemoPassword = "TrustCabinet@2026";

    /// <summary>
    /// Fills an empty database with demo rows, and on a database that already has users does one
    /// narrow repair: see <see cref="RepairPlaceholderPasswordsAsync"/>.
    ///
    /// Idempotent either way. It never duplicates rows, and the only rows it ever writes to on a
    /// populated database are demo accounts still holding the pre-SCRUM-11 placeholder hash — so
    /// data you were working on, including a password set by hand, is left alone.
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
                MeetingMode.Online, "Trực tuyến",
                "2026-09-18T01:30:00Z", "2026-09-18T03:00:00Z", secretary),
            NewMeeting("Lấy ý kiến dự thảo quy chế chi tiêu nội bộ", MeetingStatus.Draft, Sensitivity.Normal,
                MeetingMode.Online, "Trực tuyến",
                "2026-09-29T02:00:00Z", "2026-09-29T03:00:00Z", secretary),
            NewMeeting("Tổng kết công tác quý II", MeetingStatus.MinutesIssued, Sensitivity.Internal,
                MeetingMode.InPerson, "Hội trường lớn",
                "2026-09-08T07:00:00Z", "2026-09-08T09:30:00Z", secretary),
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

    private static Meeting NewMeeting(
        string title, MeetingStatus status, Sensitivity sensitivity, MeetingMode mode,
        string location, string startsAt, string endsAt, User createdBy) => new()
        {
            Title = title,
            Status = status,
            Sensitivity = sensitivity,
            Mode = mode,
            Location = location,
            StartsAt = At(startsAt),
            EndsAt = At(endsAt),
            CreatedBy = createdBy,
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
