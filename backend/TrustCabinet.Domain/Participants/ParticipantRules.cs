using System.Linq.Expressions;
using TrustCabinet.Domain.Entities;
using TrustCabinet.Domain.Enums;

namespace TrustCabinet.Domain.Participants;

public static class ParticipantRules
{
    public static Expression<Func<MeetingParticipant, bool>> CurrentExpression { get; } = p =>
        p.AssignmentStatus == AssignmentStatus.Current && p.Role != MeetingRole.Guest
        && p.Response != InvitationResponse.Delegated && p.DelegatedToUserId == null;
    public static Expression<Func<MeetingParticipant, bool>> ActiveExpression { get; } = BuildActiveExpression();
    private static readonly Func<MeetingParticipant, bool> IsCurrent = CurrentExpression.Compile();
    private static readonly Func<MeetingParticipant, bool> IsActive = ActiveExpression.Compile();
    public static bool Current(MeetingParticipant p) => IsCurrent(p);
    public static bool Active(MeetingParticipant p) => IsActive(p);

    private static Expression<Func<MeetingParticipant, bool>> BuildActiveExpression()
    {
        var participant = CurrentExpression.Parameters[0];
        var user = Expression.Property(participant, nameof(MeetingParticipant.User));
        var active = Expression.AndAlso(Expression.NotEqual(user, Expression.Constant(null, typeof(User))),
            Expression.Property(user, nameof(User.IsActive)));
        return Expression.Lambda<Func<MeetingParticipant, bool>>(
            Expression.AndAlso(CurrentExpression.Body, active), participant);
    }
    public static bool Editable(MeetingStatus status) => status is MeetingStatus.Draft or MeetingStatus.Rejected
        or MeetingStatus.Approved or MeetingStatus.Published or MeetingStatus.InProgress;
    public static bool Preparation(MeetingStatus status) => status is MeetingStatus.Draft or MeetingStatus.Rejected or MeetingStatus.Approved;
    public static bool Published(MeetingStatus status) => status is MeetingStatus.Published or MeetingStatus.InProgress or MeetingStatus.Ended;
    public static bool Respondable(MeetingStatus status) => status is MeetingStatus.Published or MeetingStatus.InProgress;
    public static bool IsSecretary(Meeting meeting, Guid actor) => meeting.Participants.Any(p => p.UserId == actor && Active(p) && p.Role == MeetingRole.Secretary);
    public static bool IsManager(Meeting meeting, Guid actor) => meeting.Participants.Any(p =>
        p.UserId == actor && Active(p) && p.Role is MeetingRole.Chair or MeetingRole.Secretary);
    public static bool CanReplaceChair(Meeting meeting, Guid actor)
    {
        if (!Editable(meeting.Status) || !IsManager(meeting, actor)) return false;
        return Preparation(meeting.Status) || meeting.Participants.Any(p => p.UserId == actor && Active(p) && p.Role == MeetingRole.Chair)
            || meeting.Participants.Any(p => Current(p) && p.Role == MeetingRole.Chair && !p.User.IsActive);
    }
    public static bool ValidRoles(MeetingStatus status, IEnumerable<MeetingRole> roles)
    {
        var list = roles.ToArray();
        return list.Count(r => r == MeetingRole.Chair) == 1 && (!Respondable(status)
            || (list.Count(r => r == MeetingRole.Chair) == 1 && list.Contains(MeetingRole.Secretary)));
    }
    public static bool TryRole(string? value, out MeetingRole role) => Enum.TryParse(value, false, out role)
        && value == role.ToString() && role is MeetingRole.Chair or MeetingRole.Secretary or MeetingRole.Member;
}
