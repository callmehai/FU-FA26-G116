using System.Linq.Expressions;
using TrustCabinet.Domain.Entities;
using TrustCabinet.Domain.Enums;
using TrustCabinet.Domain.Participants;

namespace TrustCabinet.Domain.Meetings;

/// <summary>The shared relationship-based read policy for meeting details, lists, history and actions.</summary>
public static class MeetingReadPolicy
{
    private static readonly Expression<Func<Meeting, Guid, bool>> ReadExpression
        = (meeting, userId) => meeting.CreatedById == userId
            || (meeting.Status != MeetingStatus.Draft && meeting.AssignedApproverId == userId)
            || meeting.Participants.AsQueryable().Where(ParticipantRules.CurrentExpression).Any(participant => participant.MeetingId == meeting.Id
                && participant.UserId == userId
                && ((participant.Role == MeetingRole.Secretary || participant.Role == MeetingRole.Chair)
                    || meeting.Status == MeetingStatus.Published
                    || meeting.Status == MeetingStatus.InProgress
                    || meeting.Status == MeetingStatus.Ended
                    // Announced and then called off: the invitees were told, so they may still open it (BR-300).
                    || (meeting.Status == MeetingStatus.Cancelled
                        && meeting.CancelledFromStatus == MeetingStatus.Published)));

    private static readonly Func<Meeting, Guid, bool> Read = ReadExpression.Compile();

    public static Expression<Func<Meeting, bool>> ForUser(Guid userId)
    {
        // Keep the caller captured so EF parameterizes it, while sharing the policy with CanRead.
        Expression<Func<Guid>> caller = () => userId;
        var body = new BindUser(ReadExpression.Parameters[1], caller.Body).Visit(ReadExpression.Body)!;
        return Expression.Lambda<Func<Meeting, bool>>(body, ReadExpression.Parameters[0]);
    }

    public static bool CanRead(Meeting meeting, Guid userId)
        => Read(meeting, userId);

    public static bool CanReadFullHistory(Meeting meeting, Guid userId)
        => meeting.CreatedById == userId
            || (meeting.Status != MeetingStatus.Draft && meeting.AssignedApproverId == userId);

    public static bool CanReadHistory(Meeting meeting, Guid userId)
        => CanRead(meeting, userId) || IsAssignedManager(meeting, userId);

    public static bool CanReadRejectionReason(Meeting meeting, Guid userId)
        => CanReadFullHistory(meeting, userId) || IsAssignedManager(meeting, userId);

    private static bool IsAssignedManager(Meeting meeting, Guid userId)
        => meeting.Participants.Any(participant => participant.MeetingId == meeting.Id
            && participant.UserId == userId && ParticipantRules.Active(participant)
            && (participant.Role == MeetingRole.Secretary || participant.Role == MeetingRole.Chair));

    private sealed class BindUser(ParameterExpression user, Expression value) : ExpressionVisitor
    {
        protected override Expression VisitParameter(ParameterExpression node)
            => node == user ? value : base.VisitParameter(node);
    }
}
