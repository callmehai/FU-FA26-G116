using TrustCabinet.Application.Tests.Fakes;
using TrustCabinet.Domain.Entities;
using TrustCabinet.Domain.Enums;
using TrustCabinet.Domain.Participants;

namespace TrustCabinet.Application.Tests.Recordings;

/// <summary>A meeting with a Secretary and a member on its roster, both active, held in the fake meeting repository.</summary>
internal sealed class RecordingTestMeeting
{
    public Guid Secretary { get; } = Guid.CreateVersion7();
    public Guid Member { get; } = Guid.CreateVersion7();
    public Meeting Meeting { get; }

    public RecordingTestMeeting(FakeMeetingRepository meetings, MeetingMode mode, DateTimeOffset now)
    {
        Meeting = new Meeting
        {
            Title = "Họp giao ban", Mode = mode, Status = MeetingStatus.InProgress, CreatedById = Guid.CreateVersion7(),
            PlannedStartTime = now.AddHours(-1), PlannedEndTime = now.AddHours(1), ActualStartedAt = now.AddHours(-1),
        };
        foreach (var (userId, role) in new[] { (Secretary, MeetingRole.Secretary), (Member, MeetingRole.Member) })
        {
            Meeting.Participants.Add(new MeetingParticipant
            {
                MeetingId = Meeting.Id, UserId = userId, Role = role, AssignmentStatus = AssignmentStatus.Current,
                Response = InvitationResponse.Accepted,
                User = new User { Id = userId, FullName = $"{role}", Email = $"{userId}@trustcabinet.local", IsActive = true },
            });
            meetings.ActiveUsers.Add(userId);
        }
        meetings.Meetings.Add(Meeting);
    }
}
