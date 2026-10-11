using Microsoft.EntityFrameworkCore;
using TrustCabinet.Domain.Entities;
using TrustCabinet.Domain.Enums;
using TrustCabinet.Domain.Meetings;
using TrustCabinet.Infrastructure;

namespace TrustCabinet.Web.Tests.Meetings;

public sealed class MeetingReadPolicyTests
{
    public static TheoryData<MeetingStatus, bool, bool> ReadStates => new()
    {
        { MeetingStatus.Draft, false, false },
        { MeetingStatus.WaitingApproval, true, false },
        { MeetingStatus.Rejected, true, false },
        { MeetingStatus.Approved, true, false },
        { MeetingStatus.Published, true, true },
        { MeetingStatus.InProgress, true, true },
        { MeetingStatus.Ended, true, true },
    };

    [Theory]
    [MemberData(nameof(ReadStates))]
    public void In_memory_and_query_policies_keep_relationship_permissions_for_every_state(
        MeetingStatus status, bool assignedApproverCanRead, bool memberCanRead)
    {
        var organizer = Guid.CreateVersion7();
        var approver = Guid.CreateVersion7();
        var participant = Guid.CreateVersion7();
        var outsider = Guid.CreateVersion7();
        var meeting = new Meeting
        {
            Title = "Read policy meeting",
            Status = status,
            CreatedById = organizer,
            // Includes legacy drafts that still hold an assignment: that alone grants no access.
            AssignedApproverId = approver,
        };
        meeting.Participants.Add(new MeetingParticipant
        {
            MeetingId = meeting.Id,
            UserId = participant,
            Role = MeetingRole.Member,
            Response = InvitationResponse.Accepted,
        });

        foreach (var (userId, canRead, canReadFullHistory) in new[]
        {
            (organizer, true, true),
            (approver, assignedApproverCanRead, assignedApproverCanRead),
            (participant, memberCanRead, false),
            (outsider, false, false),
        })
        {
            Assert.Equal(canRead, MeetingReadPolicy.CanRead(meeting, userId));
            Assert.Equal(canRead, MeetingReadPolicy.ForUser(userId).Compile()(meeting));
            Assert.Equal(canReadFullHistory, MeetingReadPolicy.CanReadFullHistory(meeting, userId));
        }
    }

    [Fact]
    public void Reassignment_revokes_the_old_approver_without_caching_the_previous_caller()
    {
        var oldApprover = Guid.CreateVersion7();
        var newApprover = Guid.CreateVersion7();
        var meeting = new Meeting
        {
            Title = "Reassigned meeting",
            Status = MeetingStatus.WaitingApproval,
            CreatedById = Guid.CreateVersion7(),
            AssignedApproverId = oldApprover,
        };
        var oldApproverQuery = MeetingReadPolicy.ForUser(oldApprover).Compile();
        var newApproverQuery = MeetingReadPolicy.ForUser(newApprover).Compile();

        Assert.True(MeetingReadPolicy.CanRead(meeting, oldApprover));
        Assert.False(MeetingReadPolicy.CanRead(meeting, newApprover));

        meeting.AssignedApproverId = newApprover;

        Assert.False(MeetingReadPolicy.CanRead(meeting, oldApprover));
        Assert.False(oldApproverQuery(meeting));
        Assert.False(MeetingReadPolicy.CanReadFullHistory(meeting, oldApprover));
        Assert.True(MeetingReadPolicy.CanRead(meeting, newApprover));
        Assert.True(newApproverQuery(meeting));
        Assert.True(MeetingReadPolicy.CanReadFullHistory(meeting, newApprover));
    }

    [Theory]
    [MemberData(nameof(ReadStates))]
    public void Assigned_secretary_can_read_content_and_rejection_evidence_without_transfer_metadata(
        MeetingStatus status, bool assignedApproverCanRead, bool memberCanRead)
    {
        var secretary = new User { FullName = "Assigned secretary", Email = "secretary@example.test" };
        var meeting = new Meeting { Title = "Secretary evidence", Status = status };
        meeting.Participants.Add(new MeetingParticipant
        {
            MeetingId = meeting.Id,
            UserId = secretary.Id,
            User = secretary,
            Role = MeetingRole.Secretary,
        });

        Assert.True(MeetingReadPolicy.CanReadHistory(meeting, secretary.Id));
        Assert.True(MeetingReadPolicy.CanReadRejectionReason(meeting, secretary.Id));
        Assert.False(MeetingReadPolicy.CanReadFullHistory(meeting, secretary.Id));
        Assert.True(MeetingReadPolicy.CanRead(meeting, secretary.Id));
        Assert.True(MeetingReadPolicy.ForUser(secretary.Id).Compile()(meeting));
        // The data row already covers assigned approver access in the shared policy test above.
        _ = assignedApproverCanRead;
        _ = memberCanRead;
    }

    [Theory]
    [MemberData(nameof(ReadStates))]
    public void Assigned_chair_reads_content_and_redacted_history_only_after_publication(
        MeetingStatus status, bool assignedApproverCanRead, bool memberCanRead)
    {
        var chair = Guid.CreateVersion7();
        var meeting = new Meeting { Title = "Chair access", Status = status };
        meeting.Participants.Add(new MeetingParticipant
        {
            MeetingId = meeting.Id,
            UserId = chair,
            Role = MeetingRole.Chair,
            Response = InvitationResponse.Accepted,
        });

        Assert.Equal(memberCanRead, MeetingReadPolicy.CanRead(meeting, chair));
        Assert.Equal(memberCanRead, MeetingReadPolicy.ForUser(chair).Compile()(meeting));
        Assert.Equal(memberCanRead, MeetingReadPolicy.CanReadHistory(meeting, chair));
        Assert.False(MeetingReadPolicy.CanReadFullHistory(meeting, chair));
        Assert.False(MeetingReadPolicy.CanReadRejectionReason(meeting, chair));
        _ = assignedApproverCanRead;
        _ = memberCanRead;
    }

    [Theory]
    [InlineData("member")]
    [InlineData("delegated")]
    [InlineData("delegateReference")]
    [InlineData("otherMeeting")]
    [InlineData("otherUser")]
    public void Unassigned_or_ineligible_secretary_cannot_read_prepublication_rejection_evidence(string scenario)
    {
        var user = new User { FullName = "Caller", Email = "caller@example.test" };
        var meeting = new Meeting { Title = "Rejected meeting", Status = MeetingStatus.Rejected };
        var participant = new MeetingParticipant
        {
            MeetingId = meeting.Id,
            UserId = user.Id,
            User = user,
            Role = MeetingRole.Secretary,
        };
        switch (scenario)
        {
            case "member": participant.Role = MeetingRole.Member; break;
            case "delegated": participant.Response = InvitationResponse.Delegated; break;
            case "delegateReference": participant.DelegatedToUserId = Guid.CreateVersion7(); break;
            case "otherMeeting": participant.MeetingId = Guid.CreateVersion7(); break;
            case "otherUser": participant.UserId = Guid.CreateVersion7(); break;
        }
        meeting.Participants.Add(participant);

        Assert.False(MeetingReadPolicy.CanReadHistory(meeting, user.Id));
        Assert.False(MeetingReadPolicy.CanReadRejectionReason(meeting, user.Id));
    }

    [Theory]
    [InlineData(MeetingRole.Chair, "delegated")]
    [InlineData(MeetingRole.Secretary, "delegateReference")]
    [InlineData(MeetingRole.Member, "otherMeeting")]
    [InlineData(MeetingRole.Member, "otherUser")]
    public void Invalid_participant_assignment_never_grants_content_access(MeetingRole role, string scenario)
    {
        var userId = Guid.CreateVersion7();
        var meeting = new Meeting { Title = "Assignment validation", Status = MeetingStatus.Published };
        var participant = new MeetingParticipant
        {
            MeetingId = meeting.Id,
            UserId = userId,
            Role = role,
            Response = InvitationResponse.Accepted,
        };
        switch (scenario)
        {
            case "delegated": participant.Response = InvitationResponse.Delegated; break;
            case "delegateReference": participant.DelegatedToUserId = Guid.CreateVersion7(); break;
            case "otherMeeting": participant.MeetingId = Guid.CreateVersion7(); break;
            case "otherUser": participant.UserId = Guid.CreateVersion7(); break;
        }
        meeting.Participants.Add(participant);

        Assert.False(MeetingReadPolicy.CanRead(meeting, userId));
        Assert.False(MeetingReadPolicy.ForUser(userId).Compile()(meeting));
    }

    [Fact]
    public void Query_policy_translates_to_parameterized_PostgreSql_for_different_callers()
    {
        using var db = new TrustCabinetDbContext(new DbContextOptionsBuilder<TrustCabinetDbContext>()
            .UseNpgsql("Host=none;Database=none")
            .UseSnakeCaseNamingConvention()
            .Options);
        var firstCaller = Guid.CreateVersion7();
        var secondCaller = Guid.CreateVersion7();

        var firstSql = db.Meetings.Where(MeetingReadPolicy.ForUser(firstCaller)).ToQueryString();
        var secondSql = db.Meetings.Where(MeetingReadPolicy.ForUser(secondCaller)).ToQueryString();
        var firstQuery = firstSql[firstSql.IndexOf("SELECT", StringComparison.Ordinal)..];
        var secondQuery = secondSql[secondSql.IndexOf("SELECT", StringComparison.Ordinal)..];

        Assert.Contains(firstCaller.ToString(), firstSql);
        Assert.Contains(secondCaller.ToString(), secondSql);
        Assert.DoesNotContain(firstCaller.ToString(), firstQuery);
        Assert.DoesNotContain(secondCaller.ToString(), secondQuery);
        Assert.Equal(firstQuery, secondQuery);
        Assert.Contains("WHERE", firstQuery);
        Assert.Contains("created_by_id", firstQuery);
        Assert.Contains("assigned_approver_id", firstQuery);
        Assert.Contains("EXISTS", firstQuery);
        Assert.Contains("meeting_participants", firstQuery);
    }
}
