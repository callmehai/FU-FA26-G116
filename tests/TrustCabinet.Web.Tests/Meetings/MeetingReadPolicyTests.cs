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
        { MeetingStatus.MinutesIssued, true, true },
        { MeetingStatus.Archived, true, true },
    };

    [Theory]
    [MemberData(nameof(ReadStates))]
    public void In_memory_and_query_policies_keep_relationship_permissions_for_every_state(
        MeetingStatus status, bool assignedApproverCanRead, bool participantCanRead)
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
            Participants = [new MeetingParticipant { UserId = participant }],
        };

        foreach (var (userId, canRead, canReadFullHistory) in new[]
        {
            (organizer, true, true),
            (approver, assignedApproverCanRead, assignedApproverCanRead),
            (participant, participantCanRead, false),
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
    public void Assigned_secretary_can_read_rejection_evidence_without_full_history_or_content_access(
        MeetingStatus status, bool assignedApproverCanRead, bool participantCanRead)
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
        Assert.Equal(participantCanRead, MeetingReadPolicy.CanRead(meeting, secretary.Id));
        Assert.Equal(participantCanRead, MeetingReadPolicy.ForUser(secretary.Id).Compile()(meeting));
        // The data row already covers assigned approver access in the shared policy test above.
        _ = assignedApproverCanRead;
    }

    [Theory]
    [InlineData("member")]
    [InlineData("chair")]
    [InlineData("inactive")]
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
            case "chair": participant.Role = MeetingRole.Chair; break;
            case "inactive": user.IsActive = false; break;
            case "delegated": participant.Response = InvitationResponse.Delegated; break;
            case "delegateReference": participant.DelegatedToUserId = Guid.CreateVersion7(); break;
            case "otherMeeting": participant.MeetingId = Guid.CreateVersion7(); break;
            case "otherUser": participant.UserId = Guid.CreateVersion7(); break;
        }
        meeting.Participants.Add(participant);

        Assert.False(MeetingReadPolicy.CanReadHistory(meeting, user.Id));
        Assert.False(MeetingReadPolicy.CanReadRejectionReason(meeting, user.Id));
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
