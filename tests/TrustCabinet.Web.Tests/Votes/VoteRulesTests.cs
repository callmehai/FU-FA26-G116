using TrustCabinet.Domain.Entities;
using TrustCabinet.Domain.Enums;
using TrustCabinet.Domain.Meetings;
using TrustCabinet.Domain.Participants;
using TrustCabinet.Domain.Votes;

namespace TrustCabinet.Web.Tests.Votes;

/// <summary>
/// The pure vote rules: draft validation, the frozen eligible-voter list and the percentage display.
/// No database, no HTTP: every function under test runs in-process.
/// </summary>
public sealed class VoteRulesTests
{
    private static readonly Guid Chair = Guid.NewGuid();
    private static readonly Guid Secretary = Guid.NewGuid();
    private static readonly Guid Member = Guid.NewGuid();

    private static VoteInput Draft(
        string? title = "Chọn địa điểm họp",
        string? description = "",
        IReadOnlyList<string?>? options = null,
        bool? isAnonymous = false,
        IReadOnlyList<string?>? eligibleRoles = null) => new(
        Title: title,
        Description: description,
        Options: options ?? ["Hà Nội", "Đà Nẵng"],
        IsAnonymous: isAnonymous,
        EligibleRoles: eligibleRoles ?? ["Chair", "Member"]);

    [Fact]
    public void A_valid_draft_passes_with_its_roles_parsed()
    {
        var errors = VoteRules.Validate(Draft(), out var roles);

        Assert.Empty(errors);
        Assert.Equal([MeetingRole.Chair, MeetingRole.Member], roles);
    }

    [Fact]
    public void A_blank_title_is_rejected()
    {
        var errors = VoteRules.Validate(Draft(title: ""), out _);

        Assert.Equal(["Nội dung biểu quyết phải có từ 1 đến 300 ký tự."], errors["title"]);
    }

    [Fact]
    public void A_301_character_title_is_rejected_while_300_passes()
    {
        var tooLong = VoteRules.Validate(Draft(title: new string('x', 301)), out _);
        var atLimit = VoteRules.Validate(Draft(title: new string('x', 300)), out _);

        Assert.True(tooLong.ContainsKey("title"));
        Assert.False(atLimit.ContainsKey("title"));
    }

    [Fact]
    public void A_4001_character_description_is_rejected_while_4000_passes()
    {
        var tooLong = VoteRules.Validate(Draft(description: new string('x', 4001)), out _);
        var atLimit = VoteRules.Validate(Draft(description: new string('x', 4000)), out _);

        Assert.Equal(["Mô tả không quá 4000 ký tự."], tooLong["description"]);
        Assert.False(atLimit.ContainsKey("description"));
    }

    [Fact]
    public void A_single_option_is_rejected()
    {
        var errors = VoteRules.Validate(Draft(options: ["Hà Nội"]), out _);

        Assert.Equal(["Cần ít nhất 2 phương án khác nhau, mỗi phương án từ 1 đến 200 ký tự."], errors["options"]);
    }

    [Fact]
    public void Duplicate_options_that_differ_only_by_case_are_rejected()
    {
        var errors = VoteRules.Validate(Draft(options: ["Hà Nội", "hà nội"]), out _);

        Assert.True(errors.ContainsKey("options"));
    }

    [Fact]
    public void A_201_character_option_is_rejected_while_200_passes()
    {
        var tooLong = VoteRules.Validate(Draft(options: ["Hà Nội", new string('x', 201)]), out _);
        var atLimit = VoteRules.Validate(Draft(options: ["Hà Nội", new string('x', 200)]), out _);

        Assert.True(tooLong.ContainsKey("options"));
        Assert.False(atLimit.ContainsKey("options"));
    }

    [Fact]
    public void A_missing_anonymity_choice_is_rejected()
    {
        var errors = VoteRules.Validate(Draft(isAnonymous: null), out _);

        Assert.Equal(["Chọn biểu quyết công khai hoặc kín."], errors["isAnonymous"]);
    }

    [Fact]
    public void An_empty_eligible_role_list_is_rejected()
    {
        var errors = VoteRules.Validate(Draft(eligibleRoles: []), out var roles);

        Assert.Equal(["Chọn ít nhất một vai trò được bỏ phiếu trong Chủ toạ, Thư ký, Thành viên, Khách mời."], errors["eligibleRoles"]);
        Assert.Empty(roles);
    }

    private static MeetingParticipant Part(Guid userId, MeetingRole role, bool active = true,
        InvitationResponse response = InvitationResponse.Accepted) => new()
        {
            UserId = userId,
            Role = role,
            Response = response,
            AssignmentStatus = AssignmentStatus.Current,
            User = new User { FullName = "Người dùng", Email = "nguoi.dung@trustcabinet.local", IsActive = active },
        };

    private static Meeting NewMeeting(MeetingStatus status, Guid creator, params MeetingParticipant[] participants)
    {
        var meeting = new Meeting { Title = "Họp giao ban", Status = status, CreatedById = creator };
        foreach (var p in participants)
        {
            p.MeetingId = meeting.Id;
            meeting.Participants.Add(p);
        }
        return meeting;
    }

    [Fact]
    public void Eligible_users_are_the_active_readable_holders_of_the_selected_roles()
    {
        var meeting = NewMeeting(MeetingStatus.Published, Chair,
            Part(Chair, MeetingRole.Chair), Part(Secretary, MeetingRole.Secretary), Part(Member, MeetingRole.Member));

        var eligible = VoteRules.EligibleUsers(meeting, [MeetingRole.Chair, MeetingRole.Member]);

        Assert.Equal(new[] { Chair, Member }.Order().ToArray(), eligible);
    }

    [Fact]
    public void Attendance_plays_no_part_in_eligibility()
    {
        // Pending: never answered the invitation, still on the frozen list.
        var meeting = NewMeeting(MeetingStatus.Published, Chair,
            Part(Chair, MeetingRole.Chair), Part(Member, MeetingRole.Member, response: InvitationResponse.Pending));

        var eligible = VoteRules.EligibleUsers(meeting, [MeetingRole.Member]);

        Assert.Equal([Member], eligible);
    }

    [Fact]
    public void A_deactivated_holder_is_excluded()
    {
        var meeting = NewMeeting(MeetingStatus.Published, Chair,
            Part(Chair, MeetingRole.Chair), Part(Member, MeetingRole.Member, active: false));

        var eligible = VoteRules.EligibleUsers(meeting, [MeetingRole.Chair, MeetingRole.Member]);

        Assert.Equal([Chair], eligible);
    }

    [Fact]
    public void Nobody_eligible_means_an_empty_list()
    {
        var meeting = NewMeeting(MeetingStatus.Published, Chair, Part(Chair, MeetingRole.Chair));

        var eligible = VoteRules.EligibleUsers(meeting, [MeetingRole.Guest]);

        Assert.Empty(eligible);
    }

    [Fact]
    public void One_of_three_is_33_33_percent()
    {
        Assert.Equal(33.33, VoteRules.Percentage(1, 3));
    }

    [Fact]
    public void No_ballot_means_no_percentage()
    {
        Assert.Null(VoteRules.Percentage(0, 0));
    }

    [Fact]
    public void A_unanimous_vote_is_100_percent()
    {
        Assert.Equal(100, VoteRules.Percentage(3, 3));
    }

    [Fact]
    public void Rounding_is_away_from_zero_with_two_decimals()
    {
        Assert.Equal(16.67, VoteRules.Percentage(1, 6));
    }
}
