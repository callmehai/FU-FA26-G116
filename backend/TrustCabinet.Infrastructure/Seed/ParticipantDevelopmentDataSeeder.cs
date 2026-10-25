using Microsoft.EntityFrameworkCore;
using TrustCabinet.Domain.Entities;
using TrustCabinet.Domain.Enums;

namespace TrustCabinet.Infrastructure.Seed;

/// <summary>Insert-only, local-development fixtures. Never reset edited scenarios or generate delivery jobs.</summary>
public static class ParticipantDevelopmentDataSeeder
{
    public static Guid MeetingId(int number) => Guid.Parse($"20000000-0000-7000-8000-{number:D12}");

    public static async Task SeedAsync(TrustCabinetDbContext db, CancellationToken ct = default)
    {
        var users = await db.Users.Where(u => u.Email == "thuky@trustcabinet.local" || u.Email == "chutoa@trustcabinet.local"
            || u.Email == "thanhvien@trustcabinet.local" || u.Email == "admin@trustcabinet.local").ToArrayAsync(ct);
        var secretary = users.SingleOrDefault(u => u.Email == "thuky@trustcabinet.local");
        var chair = users.SingleOrDefault(u => u.Email == "chutoa@trustcabinet.local");
        var member = users.SingleOrDefault(u => u.Email == "thanhvien@trustcabinet.local");
        if (secretary is null || chair is null || member is null) return;
        var states = new[] { MeetingStatus.Draft, MeetingStatus.Rejected, MeetingStatus.Approved, MeetingStatus.WaitingApproval,
            MeetingStatus.Published, MeetingStatus.InProgress, MeetingStatus.Ended };
        for (var index = 0; index < states.Length; index++)
        {
            var id = MeetingId(index + 1);
            if (await db.Meetings.AnyAsync(m => m.Id == id, ct)) continue;
            var state = states[index];
            var starts = new DateTimeOffset(2026, 10, 14, 2, 0, 0, TimeSpan.Zero);
            var meeting = new Meeting
            {
                Id = id,
                Title = $"[SCRUM20-DEV] {state}",
                CreatedById = secretary.Id,
                Status = state,
                Sensitivity = Sensitivity.Internal,
                Mode = MeetingMode.InPerson,
                Location = "Phòng thử nghiệm",
                PlannedStartTime = starts,
                PlannedEndTime = starts.AddHours(1),
                AssignedApproverId = state == MeetingStatus.Draft ? null : chair.Id,
                ActualStartedAt = state is MeetingStatus.InProgress or MeetingStatus.Ended ? starts : null,
                ActualEndedAt = state == MeetingStatus.Ended ? starts.AddHours(1) : null
            };
            db.Meetings.Add(meeting);
            db.MeetingParticipants.Add(new() { MeetingId = id, UserId = secretary.Id, Role = MeetingRole.Secretary });
            db.MeetingParticipants.AddRange(new MeetingParticipant { MeetingId = id, UserId = chair.Id, Role = MeetingRole.Chair },
                new MeetingParticipant { MeetingId = id, UserId = member.Id, Role = MeetingRole.Member });
        }
        await db.SaveChangesAsync(ct);
    }
}
