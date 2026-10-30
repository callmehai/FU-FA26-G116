using TrustCabinet.Domain.Entities;
using TrustCabinet.Domain.Enums;
using TrustCabinet.Domain.Meetings;
using TrustCabinet.Domain.Participants;

namespace TrustCabinet.Domain.Votes;

public static class VoteRules
{
    public const int TitleMax = 300;
    public const int DescriptionMax = 4000;
    public const int OptionMax = 200;
    /// <summary>The conclusion note: the API contract, this check and the column all say 2000.</summary>
    public const int NoteMax = 2000;

    /// <summary>Drafts are prepared, edited and deleted while the meeting is Approved, Published or InProgress (BR-252, BR-189).</summary>
    public static bool Preparation(MeetingStatus status) => status is MeetingStatus.Approved
        or MeetingStatus.Published or MeetingStatus.InProgress;

    /// <summary>
    /// A Guest sees the votes while the meeting is Published or InProgress and loses access once it ends (PO
    /// 30/10/2026). The same rule as <c>ParticipantRules.GuestCanView</c> in PR #203; use that one once it merges.
    /// </summary>
    public static bool GuestCanView(MeetingStatus status) => status is MeetingStatus.Published or MeetingStatus.InProgress;

    /// <summary>Ending the meeting does not close its vote, so the Chair may still close and conclude it (ADR-0017, BR-270).</summary>
    public static bool Closable(MeetingStatus status) => status is MeetingStatus.InProgress or MeetingStatus.Ended;

    public static bool IsChair(Meeting meeting, VoteViewer viewer) => viewer.UserId is { } id
        && meeting.Participants.Any(p => p.UserId == id && ParticipantRules.Active(p) && p.Role == MeetingRole.Chair);

    public static bool IsManager(Meeting meeting, VoteViewer viewer)
        => viewer.UserId is { } id && ParticipantRules.IsManager(meeting, id);

    public static bool CanManageDraft(Meeting meeting, VoteViewer viewer)
        => IsManager(meeting, viewer) && Preparation(meeting.Status);

    public static VotePermissions Permissions(Meeting meeting, VoteViewer viewer)
    {
        var chair = IsChair(meeting, viewer);
        return new(CanManageDraft(meeting, viewer), chair && meeting.Status == MeetingStatus.InProgress,
            chair && Closable(meeting.Status), chair && Closable(meeting.Status));
    }

    public static VoteActions Actions(Meeting meeting, VoteViewer viewer, VotingStatus status, bool hasConclusion,
        bool isEligible, bool hasVoted, bool anotherOpen)
    {
        var draft = status == VotingStatus.Draft && CanManageDraft(meeting, viewer);
        var chair = IsChair(meeting, viewer);
        return new(draft, draft, draft,
            status == VotingStatus.Draft && chair && meeting.Status == MeetingStatus.InProgress && !anotherOpen,
            status == VotingStatus.Open && meeting.Status == MeetingStatus.InProgress && isEligible && !hasVoted,
            status == VotingStatus.Open && chair && Closable(meeting.Status),
            status == VotingStatus.Closed && !hasConclusion && chair && Closable(meeting.Status),
            status == VotingStatus.Closed);
    }

    public static VoteInput Normalize(VoteInput input) => input with
    {
        Title = input.Title?.Trim() ?? "",
        Description = input.Description ?? "",
        Options = input.Options?.Select(o => o?.Trim()).ToArray()
    };

    /// <summary>Checks a normalized draft configuration (BR-255, BR-256, BR-257, BR-272).</summary>
    public static IReadOnlyDictionary<string, string[]> Validate(VoteInput input, out List<MeetingRole> roles)
    {
        var errors = new Dictionary<string, string[]>();
        roles = [];
        if (input.Title is not { Length: > 0 and <= TitleMax })
            errors["title"] = [$"Nội dung biểu quyết phải có từ 1 đến {TitleMax} ký tự."];
        if (input.Description?.Length > DescriptionMax)
            errors["description"] = [$"Mô tả không quá {DescriptionMax} ký tự."];
        if (input.Options is not { Count: >= 2 } options || options.Any(o => o is not { Length: > 0 and <= OptionMax })
            || options.Distinct(StringComparer.OrdinalIgnoreCase).Count() != options.Count)
            errors["options"] = [$"Cần ít nhất 2 phương án khác nhau, mỗi phương án từ 1 đến {OptionMax} ký tự."];
        if (input.IsAnonymous is null)
            errors["isAnonymous"] = ["Chọn biểu quyết công khai hoặc kín."];
        foreach (var value in input.EligibleRoles ?? [])
        {
            if (!Enum.TryParse<MeetingRole>(value, false, out var role) || value != role.ToString() || roles.Contains(role))
            {
                roles = [];
                break;
            }
            roles.Add(role);
        }
        if (roles.Count == 0)
            errors["eligibleRoles"] = ["Chọn ít nhất một vai trò được bỏ phiếu trong Chủ toạ, Thư ký, Thành viên, Khách mời."];
        return errors;
    }

    /// <summary>
    /// The internal users entitled to vote once it opens: current, active holders of the selected roles who can read
    /// the meeting (BR-190). Attendance plays no part. Guests are added by invitation, outside this list.
    /// </summary>
    public static IReadOnlyList<Guid> EligibleUsers(Meeting meeting, IReadOnlyCollection<MeetingRole> roles)
        => meeting.Participants.Where(p => ParticipantRules.Active(p) && roles.Contains(p.Role)
                && MeetingReadPolicy.CanRead(meeting, p.UserId))
            .Select(p => p.UserId).Distinct().Order().ToArray();

    public static double? Percentage(int count, int ballots)
        => ballots == 0 ? null : Math.Round(count * 100d / ballots, 2, MidpointRounding.AwayFromZero);
}
