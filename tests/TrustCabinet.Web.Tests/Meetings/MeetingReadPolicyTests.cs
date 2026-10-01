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
