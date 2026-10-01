using System.Linq.Expressions;
using TrustCabinet.Domain.Entities;
using TrustCabinet.Domain.Enums;

namespace TrustCabinet.Domain.Meetings;

/// <summary>The shared relationship-based read policy for meeting details, lists, history and actions.</summary>
public static class MeetingReadPolicy
{
    private static readonly Expression<Func<Meeting, Guid, bool>> ReadExpression
        = (meeting, userId) => meeting.CreatedById == userId
            || (meeting.Status != MeetingStatus.Draft && meeting.AssignedApproverId == userId)
            || ((meeting.Status == MeetingStatus.Published
                    || meeting.Status == MeetingStatus.InProgress
                    || meeting.Status == MeetingStatus.Ended
                    || meeting.Status == MeetingStatus.MinutesIssued
                    || meeting.Status == MeetingStatus.Archived)
                && meeting.Participants.Any(participant => participant.UserId == userId));

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

    private sealed class BindUser(ParameterExpression user, Expression value) : ExpressionVisitor
    {
        protected override Expression VisitParameter(ParameterExpression node)
            => node == user ? value : base.VisitParameter(node);
    }
}
