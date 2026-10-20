using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using TrustCabinet.Domain.Entities;
using TrustCabinet.Domain.Enums;
using TrustCabinet.Domain.Meetings;
using TrustCabinet.Infrastructure.Meetings;
using static TrustCabinet.Web.Tests.Participants.ParticipantEndpointTests;

namespace TrustCabinet.Web.Tests.Participants;

public sealed class ParticipantRolePolicyTests(ParticipantFixture f) : IClassFixture<ParticipantFixture>
{
    private static object Swap(ParticipantFixture.Scenario s, Guid recipient) => new
    {
        changes = new[] { new { participantId = s.ChairAssignment, role = "Member" },
            new { participantId = recipient, role = "Chair" } },
        chairReplacement = new { outgoingParticipantId = s.ChairAssignment, incomingParticipantId = recipient, confirmed = true }
    };

    [Theory]
    [InlineData(MeetingStatus.Draft)]
    [InlineData(MeetingStatus.Rejected)]
    [InlineData(MeetingStatus.Approved)]
    [InlineData(MeetingStatus.Published)]
    [InlineData(MeetingStatus.InProgress)]
    public async Task Chair_can_never_be_removed_or_demoted_without_a_replacement(MeetingStatus status)
    {
        var s = await f.Seed(status);
        foreach (var actor in new[] { s.Chair, s.Secretary })
        {
            using var client = f.Client(actor);
            await Code(await Write(client, s.Url + $"/participants/{s.ChairAssignment}", HttpMethod.Delete), 409, "MSG109");
        }
        using var chair = f.Client(s.Chair);
        await Code(await Write(chair, s.Url + "/participants/assignments", HttpMethod.Put,
            new { changes = new[] { new { participantId = s.ChairAssignment, role = "Member" } } }), 409, "MSG109");
    }

    [Theory]
    [InlineData(MeetingStatus.Published)]
    [InlineData(MeetingStatus.InProgress)]
    public async Task Secretary_cannot_touch_active_chair_and_chair_can_hand_over(MeetingStatus status)
    {
        var s = await f.Seed(status);
        using var secretary = f.Client(s.Secretary);
        using var chair = f.Client(s.Chair);
        await Code(await Write(secretary, s.Url + "/participants/assignments", HttpMethod.Put, Swap(s, s.MemberAssignment)), 409, "MSG256");
        await Code(await Write(secretary, s.Url + "/participants/assignments", HttpMethod.Put,
            new { changes = new[] { new { participantId = s.ChairAssignment, role = "Member" } } }), 409, "MSG256");
        await Code(await Write(secretary, s.Url + "/participants/assignments", HttpMethod.Put,
            new { changes = new[] { new { participantId = s.MemberAssignment, role = "Chair" } } }), 409, "MSG256");
        await Code(await Write(chair, s.Url + "/participants/assignments", HttpMethod.Put, Swap(s, s.MemberAssignment)), 200, "MSG257");
        await using var db = f.Db();
        Assert.Equal(MeetingRole.Member, (await db.MeetingParticipants.FindAsync(s.ChairAssignment))!.Role);
        Assert.Equal(MeetingRole.Chair, (await db.MeetingParticipants.FindAsync(s.MemberAssignment))!.Role);
        Assert.Equal(2, await db.Notifications.CountAsync(n => n.MeetingId == s.MeetingId && n.Type == NotificationType.MeetingRoleChanged));
        Assert.Equal(2, await db.MeetingInvitationDispatches.CountAsync(d => d.MeetingId == s.MeetingId && d.EventKind == InvitationEventKind.RoleChanged));
    }

    [Theory]
    [InlineData("Secretary")]
    [InlineData("Inactive")]
    [InlineData("Removed")]
    [InlineData("Missing")]
    public async Task Invalid_chair_recipient_is_rejected(string kind)
    {
        var s = await f.Seed();
        using var chair = f.Client(s.Chair);
        var recipient = kind == "Secretary" ? s.SecretaryAssignment : kind == "Missing" ? Guid.NewGuid() : s.MemberAssignment;
        await using (var db = f.Db())
        {
            if (kind == "Inactive") (await db.Users.FindAsync(s.Member))!.IsActive = false;
            if (kind == "Removed")
            {
                var p = (await db.MeetingParticipants.FindAsync(s.MemberAssignment))!;
                p.AssignmentStatus = Domain.Participants.AssignmentStatus.Removed;
                p.RemovedAt = f.Clock.Now;
                p.RemovedById = s.Chair;
            }
            await db.SaveChangesAsync();
        }
        await Code(await Write(chair, s.Url + "/participants/assignments", HttpMethod.Put, Swap(s, recipient)), 400, "MSG259");
    }

    [Theory]
    [InlineData(MeetingStatus.Draft)]
    [InlineData(MeetingStatus.WaitingApproval)]
    public async Task Chair_reads_prepublication_and_member_does_not(MeetingStatus status)
    {
        var s = await f.Seed(status);
        using var chair = f.Client(s.Chair);
        using var member = f.Client(s.Member);
        Assert.Equal(HttpStatusCode.OK, (await chair.GetAsync(s.Url)).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await member.GetAsync(s.Url)).StatusCode);
        Assert.Equal(HttpStatusCode.OK, (await chair.GetAsync(s.Url + "/participants")).StatusCode);
    }

    [Fact]
    public async Task Chair_and_secretary_manage_before_publication()
    {
        var s = await f.Seed(MeetingStatus.Draft);
        using var chair = f.Client(s.Chair);
        using var secretary = f.Client(s.Secretary);
        var added = await Code(await Write(chair, s.Url + "/participants", HttpMethod.Post,
            new { userIds = new[] { s.Outsider }, role = "Secretary" }), 201, "MSG96");
        var assignment = added.GetProperty("createdIds")[0].GetGuid();
        await Code(await Write(chair, s.Url + "/participants/assignments", HttpMethod.Put,
            new { changes = new[] { new { participantId = assignment, role = "Member" } } }, await Tag(chair, s.Url)), 200, "MSG100");
        await Code(await Write(chair, s.Url + $"/participants/{assignment}", HttpMethod.Delete, etag: await Tag(chair, s.Url)), 200, "MSG103");
        await Code(await Write(secretary, s.Url + "/participants/assignments", HttpMethod.Put,
            Swap(s, s.MemberAssignment), await Tag(secretary, s.Url)), 200, "MSG100");
    }

    [Fact]
    public async Task Non_managers_read_roster_but_cannot_mutate_and_totals_ignore_search()
    {
        var s = await f.Seed();
        await using (var db = f.Db())
        {
            (await db.Meetings.FindAsync(s.MeetingId))!.AssignedApproverId = s.Outsider;
            await db.SaveChangesAsync();
        }
        foreach (var actor in new[] { s.Member, s.Organizer, s.Outsider })
        {
            using var client = f.Client(actor);
            var page = await client.GetFromJsonAsync<JsonElement>(s.Url + "/participants?q=no-match&page=2&pageSize=1");
            Assert.Equal(0, page.GetProperty("total").GetInt32());
            Assert.Equal(3, page.GetProperty("totals").GetProperty("total").GetInt32());
            await Code(await Write(client, s.Url + "/participants", HttpMethod.Post,
                new { userIds = new[] { s.Outsider }, role = "Member" }), 403, "MSG85");
            await Code(await client.GetAsync(s.Url + "/participants/editor-options"), 403, "MSG85");
        }
        using var member = f.Client(s.Member);
        await Code(await Write(member, s.Url + "/invitation-response", HttpMethod.Put, new { response = "Accepted" }), 200, "MSG105");
        var roster = await member.GetFromJsonAsync<JsonElement>(s.Url + "/participants");
        Assert.Equal(1, roster.GetProperty("totals").GetProperty("accepted").GetInt32());
        var row = roster.GetProperty("items").EnumerateArray().Single(r => r.GetProperty("userId").GetGuid() == s.Member);
        Assert.Equal("Accepted", row.GetProperty("response").GetString());
        Assert.Equal(f.Clock.Now, row.GetProperty("respondedAt").GetDateTimeOffset());
    }

    [Fact]
    public async Task Context_tracks_chair_authority_and_removed_contracts_are_rejected()
    {
        var s = await f.Seed();
        using var chair = f.Client(s.Chair);
        using var secretary = f.Client(s.Secretary);
        var context = await secretary.GetFromJsonAsync<JsonElement>(s.Url + "/participant-context");
        Assert.Equal("Secretary", context.GetProperty("myRole").GetString());
        Assert.False(context.GetProperty("permissions").GetProperty("canReplaceChair").GetBoolean());
        await using (var db = f.Db())
        {
            (await db.Users.FindAsync(s.Chair))!.IsActive = false;
            await db.SaveChangesAsync();
        }
        context = await secretary.GetFromJsonAsync<JsonElement>(s.Url + "/participant-context");
        Assert.True(context.GetProperty("permissions").GetProperty("canReplaceChair").GetBoolean());
        Assert.Equal(3, context.GetProperty("permissions").EnumerateObject().Count());
        await Code(await Write(secretary, s.Url + "/participants", HttpMethod.Post,
            new { userIds = new[] { s.Outsider }, role = "Member", additionalPermissions = Array.Empty<string>() }), 400, "MSG97");
        await Code(await Write(secretary, s.Url + "/guest-invitations", HttpMethod.Post,
            new { invitedName = "Guest", email = "guest@test.local", additionalPermissions = Array.Empty<string>() }), 400, "MSG97");
        await Code(await Write(secretary, s.Url + "/participants/assignments", HttpMethod.Put,
            new { changes = new[] { new { participantId = s.MemberAssignment, role = "Member", additionalPermissions = Array.Empty<string>() } } }), 400, "MSG101");
        Assert.Equal(HttpStatusCode.MethodNotAllowed, (await Write(secretary, s.Url + "/participants/secretary-recovery", HttpMethod.Post, new { userId = s.Outsider })).StatusCode);
        Assert.Equal(HttpStatusCode.MethodNotAllowed, (await Write(secretary, s.Url + $"/guest-invitations/{Guid.NewGuid()}/permissions", HttpMethod.Put, new { })).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await secretary.GetAsync(s.Url + "/invitation-responses")).StatusCode);
    }

    [Fact]
    public async Task Events_include_removed_reader_and_response_audit_only_contains_changed_row()
    {
        var s = await f.Seed();
        using var member = f.Client(s.Member);
        using var secretary = f.Client(s.Secretary);
        await Code(await Write(member, s.Url + "/invitation-response", HttpMethod.Put, new { response = "Accepted" }), 200, "MSG105");
        var responseEvent = Assert.Single(f.Changes.Events, e => e.Change.MeetingId == s.MeetingId);
        Assert.Contains(s.Member, responseEvent.Users);
        Assert.Contains(s.Organizer, responseEvent.Users);
        Assert.DoesNotContain(s.Outsider, responseEvent.Users);
        await using (var db = f.Db())
        {
            var audit = await db.AuditLog.SingleAsync(a => a.ObjectId == s.MeetingId);
            using var details = JsonDocument.Parse(audit.Details!);
            var change = Assert.Single(details.RootElement.GetProperty("changes").EnumerateArray());
            Assert.Equal(s.MemberAssignment, change.GetProperty("id").GetGuid());
        }
        await Code(await Write(secretary, s.Url + $"/participants/{s.MemberAssignment}", HttpMethod.Delete), 200, "MSG103");
        var removal = f.Changes.Events.Last(e => e.Change.MeetingId == s.MeetingId);
        Assert.Contains(s.Member, removal.Users);
        Assert.Equal("1", removal.Change.Revision);
        Assert.Equal(HttpStatusCode.NotFound, (await member.GetAsync(s.Url + "/participants")).StatusCode);
        f.Changes.FailMeeting = s.MeetingId;
        try
        {
            await Code(await Write(secretary, s.Url + "/participants", HttpMethod.Post,
                new { userIds = new[] { s.Outsider }, role = "Member" }, await Tag(secretary, s.Url)), 201, "MSG96");
        }
        finally { f.Changes.FailMeeting = null; }
    }

    [Theory]
    [InlineData("valid")]
    [InlineData("missing")]
    [InlineData("empty")]
    [InlineData("no-chair")]
    [InlineData("two-chairs")]
    [InlineData("duplicate")]
    [InlineData("inactive")]
    [InlineData("unknown")]
    [InlineData("bad-role")]
    public async Task Creation_validates_and_saves_roster_atomically(string kind)
    {
        var s = await f.Seed();
        await using var db = f.Db();
        if (kind == "inactive") { (await db.Users.FindAsync(s.Chair))!.IsActive = false; await db.SaveChangesAsync(); }
        IReadOnlyList<ParticipantSeed>? seeds = kind switch
        {
            "missing" => null,
            "empty" => [],
            "no-chair" => [new(s.Member, "Member")],
            "two-chairs" => [new(s.Chair, "Chair"), new(s.Member, "Chair")],
            "duplicate" => [new(s.Chair, "Chair"), new(s.Chair, "Member")],
            "unknown" => [new(Guid.NewGuid(), "Chair")],
            "bad-role" => [new(s.Chair, "chair")],
            _ => [new(s.Chair, "Chair"), new(s.Member, "Member")]
        };
        var title = Guid.NewGuid().ToString();
        var service = new MeetingService(new MeetingRepository(db), NullLogger<MeetingService>.Instance, f.Clock);
        var result = await service.CreateAsync(new(title, "2099-01-01T10:00:00Z", "2099-01-01T11:00:00Z",
            "InPerson", "Room", null, "Internal", "Test"), s.Organizer, seeds);
        if (kind != "valid")
        {
            Assert.Equal(MeetingOutcome.InvalidParticipants, result.Outcome);
            Assert.NotEmpty(result.Errors!);
            Assert.False(await db.Meetings.AnyAsync(m => m.Title == title));
            return;
        }
        Assert.True(result.Succeeded);
        var meeting = await db.Meetings.Include(m => m.Participants).Include(m => m.StatusHistory).SingleAsync(m => m.Title == title);
        Assert.Equal(0, meeting.ParticipantsRevision);
        Assert.Equal(2, meeting.Participants.Count);
        Assert.Single(meeting.StatusHistory);
        Assert.Empty(await db.MeetingInvitationDispatches.Where(d => d.MeetingId == meeting.Id).ToArrayAsync());
        Assert.Empty(await db.Notifications.Where(n => n.MeetingId == meeting.Id).ToArrayAsync());
        Assert.All(meeting.Participants, p =>
        {
            Assert.Equal(InvitationResponse.Pending, p.Response);
            Assert.Equal($"participant:{p.Id}:invited", p.InitialInvitationDeliveryKey);
        });
        Assert.Equal(s.Chair, result.Meeting!.Chair!.UserId);
        Assert.Empty(result.Meeting.Secretaries!);
        Assert.Null(result.Meeting.MyMeetingRole);
    }
}
