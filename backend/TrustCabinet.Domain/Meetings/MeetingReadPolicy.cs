using System.Linq.Expressions;
using TrustCabinet.Domain.Entities;
using TrustCabinet.Domain.Enums;

namespace TrustCabinet.Domain.Meetings;

/// <summary>The shared relationship-based read policy for meeting details, lists, history and actions.</summary>
public static class MeetingReadPolicy
{
    public static Expression<Func<Meeting, bool>> ForUser(Guid userId)
        => meeting => meeting.CreatedById == userId
            || (meeting.Status != MeetingStatus.Draft && meeting.AssignedApproverId == userId)
            || ((meeting.Status == MeetingStatus.Published
                    || meeting.Status == MeetingStatus.InProgress
                    || meeting.Status == MeetingStatus.Ended
                    || meeting.Status == MeetingStatus.MinutesIssued
                    || meeting.Status == MeetingStatus.Archived)
                && meeting.Participants.Any(participant => participant.UserId == userId));

    public static bool CanRead(Meeting meeting, Guid userId)
        => ForUser(userId).Compile()(meeting);

    public static bool CanReadFullHistory(Meeting meeting, Guid userId)
        => meeting.CreatedById == userId
            || (meeting.Status != MeetingStatus.Draft && meeting.AssignedApproverId == userId);
}
