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
            || meeting.Participants.Any(participant => participant.MeetingId == meeting.Id
                && participant.UserId == userId
                && participant.Response != InvitationResponse.Delegated
                && participant.DelegatedToUserId == null
                && (participant.Role == MeetingRole.Secretary
                    || meeting.Status == MeetingStatus.Published
                    || meeting.Status == MeetingStatus.InProgress
                    || meeting.Status == MeetingStatus.Ended
                    || meeting.Status == MeetingStatus.MinutesIssued
                    || meeting.Status == MeetingStatus.Archived));

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
        => CanRead(meeting, userId) || IsAssignedSecretary(meeting, userId);

    public static bool CanReadRejectionReason(Meeting meeting, Guid userId)
        => CanReadFullHistory(meeting, userId) || IsAssignedSecretary(meeting, userId);

    public static bool IsAssignedSecretary(Meeting meeting, Guid userId)
        => meeting.Participants.Any(participant => participant.UserId == userId
            && IsActiveAssignment(meeting, participant, MeetingRole.Secretary));

    /// <summary>BR-63: only the assigned Chair starts and ends Meeting Sessions.</summary>
    public static bool IsAssignedChair(Meeting meeting, Guid userId)
        => meeting.Participants.Any(participant => participant.UserId == userId
            && IsActiveAssignment(meeting, participant, MeetingRole.Chair));

    /// <summary>BR-89: a session starts only while the meeting still has an active Secretary.</summary>
    public static bool HasActiveSecretary(Meeting meeting)
        => meeting.Participants.Any(participant => IsActiveAssignment(meeting, participant, MeetingRole.Secretary));

    private static bool IsActiveAssignment(Meeting meeting, MeetingParticipant participant, MeetingRole role)
        => participant.MeetingId == meeting.Id
            && participant.Role == role
            && participant.Response != InvitationResponse.Delegated
            && participant.DelegatedToUserId is null
            && participant.User is { IsActive: true };

    private sealed class BindUser(ParameterExpression user, Expression value) : ExpressionVisitor
    {
        protected override Expression VisitParameter(ParameterExpression node)
            => node == user ? value : base.VisitParameter(node);
    }
}
