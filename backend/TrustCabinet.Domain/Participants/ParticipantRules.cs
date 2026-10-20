using TrustCabinet.Domain.Entities;
using TrustCabinet.Domain.Enums;

namespace TrustCabinet.Domain.Participants;

public sealed record MeetingPermission(string Code, string Name);
public static class MeetingPermissionCatalog
{
    public const string ViewParticipants = "ViewMeetingParticipants";
    public const string ViewResponses = "ViewInvitationResponses";
    public static IReadOnlyList<MeetingPermission> All { get; } = [new(ViewParticipants, "Xem thành phần tham dự"), new(ViewResponses, "Xem phản hồi lời mời")];
    public static bool Valid(IReadOnlyList<string> keys) => keys.Distinct(StringComparer.Ordinal).Count() == keys.Count
        && keys.All(key => All.Any(permission => permission.Code == key));
}

public static class ParticipantRules
{
    public static bool Current(MeetingParticipant p) => p.AssignmentStatus == AssignmentStatus.Current
        && p.Role != MeetingRole.Guest && p.Response != InvitationResponse.Delegated && p.DelegatedToUserId is null;
    public static bool Active(MeetingParticipant p) => Current(p) && p.User.IsActive;
    public static bool Editable(MeetingStatus status) => status is MeetingStatus.Draft or MeetingStatus.Rejected
        or MeetingStatus.Approved or MeetingStatus.Published or MeetingStatus.InProgress;
    public static bool Preparation(MeetingStatus status) => status is MeetingStatus.Draft or MeetingStatus.Rejected or MeetingStatus.Approved;
    public static bool Published(MeetingStatus status) => status is MeetingStatus.Published or MeetingStatus.InProgress or MeetingStatus.Ended;
    public static bool Respondable(MeetingStatus status) => status is MeetingStatus.Published or MeetingStatus.InProgress;
    public static bool IsSecretary(Meeting meeting, Guid actor) => meeting.Participants.Any(p => p.UserId == actor && Active(p) && p.Role == MeetingRole.Secretary);
    public static bool CanRecover(Meeting meeting, Guid actor) => meeting.CreatedById == actor && Preparation(meeting.Status)
        && !meeting.Participants.Any(p => Current(p) && p.Role == MeetingRole.Secretary);
    public static bool ValidRoles(MeetingStatus status, IEnumerable<MeetingRole> roles)
    {
        var list = roles.ToArray();
        return list.Count(r => r == MeetingRole.Chair) <= 1 && (!Respondable(status)
            || (list.Count(r => r == MeetingRole.Chair) == 1 && list.Contains(MeetingRole.Secretary)));
    }
    public static bool TryRole(string? value, out MeetingRole role) => Enum.TryParse(value, false, out role)
        && value == role.ToString() && role is MeetingRole.Chair or MeetingRole.Secretary or MeetingRole.Member;
}
