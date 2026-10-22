using System.Net;
using System.Net.Http.Json;
using System.Text;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using TrustCabinet.Domain.Entities;
using TrustCabinet.Domain.Enums;
using TrustCabinet.Domain.Participants;
using static TrustCabinet.Web.Tests.Participants.ParticipantEndpointTests;

namespace TrustCabinet.Web.Tests.Participants;

public sealed class ChairHandoverEndpointTests(ParticipantFixture f) : IClassFixture<ParticipantFixture>
{
    private const string Candidates = "/participants/chair-handover-candidates";
    private const string Handover = "/participants/chair-handover";
    private static object Body(Guid id) => new { newChairParticipantId = id, confirmed = true };
    private static Task<HttpResponseMessage> Post(HttpClient client, ParticipantFixture.Scenario s, Guid? id = null, string? tag = "\"participants-0\"")
        => Write(client, s.Url + Handover, HttpMethod.Post, Body(id ?? s.MemberAssignment), tag);

    // Serialize scalar properties only, so navigation fixup cannot hide changes to evidence.
    private static string Scalars<T>(T entity) where T : class => JsonSerializer.Serialize(
        typeof(T).GetProperties().Where(p => p.PropertyType.IsValueType || p.PropertyType == typeof(string))
            .OrderBy(p => p.Name).ToDictionary(p => p.Name, p => p.GetValue(entity)));

    [Theory]
    [InlineData(MeetingStatus.Published)]
    [InlineData(MeetingStatus.InProgress)]
    public async Task Handover_preserves_evidence_and_commits_exactly_one_set_of_side_effects(MeetingStatus status)
    {
        var s = await f.Seed(status);
        await using var db = f.Db();
        var pair = await db.MeetingParticipants.Where(p => p.Id == s.ChairAssignment || p.Id == s.MemberAssignment).OrderBy(p => p.Id).ToArrayAsync();
        foreach (var p in pair)
        {
            p.Response = InvitationResponse.Declined;
            p.RespondedAt = f.Clock.Now.AddHours(-2);
            p.Attendance = AttendanceStatus.Absent;
            p.AttendanceRecordedAt = f.Clock.Now.AddHours(-1);
            p.AttendanceRecordedById = s.Secretary;
        }
        var vote = new VotingSession { MeetingId = s.MeetingId, Title = "Existing ballot", CreatedById = s.Chair, EligibleCount = 3 };
        var option = new VotingOption { VotingSessionId = vote.Id, Label = "Agree", Position = 1 };
        var receipt = new VoteReceipt { VotingSessionId = vote.Id, VoterId = s.Chair };
        var choice = new VoteChoice { VotingSessionId = vote.Id, VotingOptionId = option.Id, VoterId = s.Chair };
        var transition = new MeetingStatusHistory { MeetingId = s.MeetingId, ToStatus = status,
            Action = MeetingHistoryAction.Publish, ActorId = s.Organizer, OccurredAt = f.Clock.Now };
        db.AddRange(vote, option, receipt, choice, transition);
        await db.SaveChangesAsync();
        foreach (var entity in new object[] { vote, option, receipt, choice, transition })
            await db.Entry(entity).ReloadAsync();
        var evidence = pair.Select(Scalars).ToArray();
        var users = await db.Users.Where(u => u.Id == s.Chair || u.Id == s.Member).OrderBy(u => u.Id).ToArrayAsync();
        var accounts = users.Select(Scalars).ToArray();
        var votes = new[] { Scalars(vote), Scalars(option), Scalars(receipt), Scalars(choice) };
        var history = Scalars(transition);
        using var chair = f.Client(s.Chair);
        var listing = await chair.GetAsync(s.Url + Candidates);
        Assert.Equal("\"participants-0\"", listing.Headers.ETag!.ToString());
        Assert.True(listing.Headers.CacheControl!.NoStore);
        var result = await Post(chair, s, tag: listing.Headers.ETag.ToString());
        var json = await Code(result, 200, "MSG257");
        Assert.Equal("1", json.GetProperty("revision").GetString());
        Assert.Equal("\"participants-1\"", result.Headers.ETag!.ToString());
        Assert.True(result.Headers.CacheControl!.NoStore);
        db.ChangeTracker.Clear();
        var after = await db.MeetingParticipants.Where(p => p.Id == s.ChairAssignment || p.Id == s.MemberAssignment).OrderBy(p => p.Id).ToArrayAsync();
        Assert.Equal(MeetingRole.Member, after.Single(p => p.Id == s.ChairAssignment).Role);
        Assert.Equal(MeetingRole.Chair, after.Single(p => p.Id == s.MemberAssignment).Role);
        for (var i = 0; i < after.Length; i++)
        {
            after[i].Role = pair[i].Role;
            after[i].UpdatedAt = pair[i].UpdatedAt;
        }
        Assert.Equal(evidence, after.Select(Scalars).ToArray());
        Assert.Equal(accounts, (await db.Users.Where(u => u.Id == s.Chair || u.Id == s.Member).OrderBy(u => u.Id).ToArrayAsync()).Select(Scalars).ToArray());
        Assert.Equal(votes, new[] { Scalars((await db.VotingSessions.FindAsync(vote.Id))!), Scalars((await db.VotingOptions.FindAsync(option.Id))!),
            Scalars((await db.VoteReceipts.FindAsync(receipt.Id))!), Scalars((await db.VoteChoices.FindAsync(choice.Id))!) });
        Assert.Equal(history, Scalars(await db.MeetingStatusHistory.SingleAsync(h => h.MeetingId == s.MeetingId)));
        Assert.Equal(1, await db.Meetings.Where(m => m.Id == s.MeetingId).Select(m => m.ParticipantsRevision).SingleAsync());
        Assert.Equal(1, await db.MeetingParticipants.CountAsync(p => p.MeetingId == s.MeetingId && p.Role == MeetingRole.Chair));
        Assert.Equal(1, await db.MeetingParticipants.CountAsync(p => p.MeetingId == s.MeetingId && p.Role == MeetingRole.Secretary));
        var notifications = await db.Notifications.Where(n => n.MeetingId == s.MeetingId).ToArrayAsync();
        Assert.Equal(2, notifications.Length);
        Assert.All(notifications, n => Assert.Equal(NotificationType.MeetingRoleChanged, n.Type));
        var dispatch = await db.MeetingInvitationDispatches.Where(d => d.MeetingId == s.MeetingId).ToArrayAsync();
        Assert.Equal(2, dispatch.Length);
        Assert.All(dispatch, d => Assert.Equal(InvitationEventKind.RoleChanged, d.EventKind));
        var audit = await db.AuditLog.SingleAsync(a => a.ObjectId == s.MeetingId);
        Assert.Equal("meeting.participants.HandOverChair", audit.Action);
        Assert.Equal(2, JsonDocument.Parse(audit.Details!).RootElement.GetProperty("changes").GetArrayLength());
        var changed = Assert.Single(f.Changes.Events, e => e.Change.MeetingId == s.MeetingId);
        Assert.Equal("1", changed.Change.Revision);
        Assert.Contains(s.Chair, changed.Users); Assert.Contains(s.Member, changed.Users);
        using var next = f.Client(s.Member);
        var oldContext = await chair.GetFromJsonAsync<JsonElement>(s.Url + "/participant-context");
        var nextContext = await next.GetFromJsonAsync<JsonElement>(s.Url + "/participant-context");
        Assert.Equal("Member", oldContext.GetProperty("myRole").GetString());
        Assert.False(oldContext.GetProperty("permissions").GetProperty("canManageParticipants").GetBoolean());
        Assert.Equal("Chair", nextContext.GetProperty("myRole").GetString());
        Assert.True(nextContext.GetProperty("permissions").GetProperty("canReplaceChair").GetBoolean());
        Assert.Equal(HttpStatusCode.OK, (await chair.GetAsync(s.Url)).StatusCode);
        await Code(await Post(chair, s), 409, "MSG260");
        Assert.Equal(2, await db.MeetingInvitationDispatches.CountAsync(d => d.MeetingId == s.MeetingId));
    }

    [Fact]
    public async Task Candidates_filter_before_search_and_paging_without_guest_profile_dependency()
    {
        var s = await f.Seed(); var other = await f.Seed();
        await using var db = f.Db();
        var users = Enumerable.Range(0, 6).Select(i => new User { FullName = i < 2 ? "same" : "Excluded " + i,
            Email = $"{Guid.NewGuid():N}@candidate.test", PasswordHash = "test-only", IsActive = i != 2 }).ToArray();
        db.Users.AddRange(users);
        var rows = users.Select((u, i) => new MeetingParticipant { MeetingId = s.MeetingId, UserId = u.Id,
            AssignmentStatus = i == 3 ? AssignmentStatus.Removed : AssignmentStatus.Current,
            RemovedAt = i == 3 ? f.Clock.Now : null, RemovedById = i == 3 ? s.Secretary : null,
            Response = i == 4 ? InvitationResponse.Delegated : InvitationResponse.Declined,
            DelegatedToUserId = i == 5 ? s.Outsider : null }).ToArray();
        db.MeetingParticipants.AddRange(rows); await db.SaveChangesAsync();
        using var chair = f.Client(s.Chair);
        f.Guests.FailProfiles = true;
        try
        {
            var all = await chair.GetFromJsonAsync<JsonElement>(s.Url + Candidates);
            Assert.Equal(3, all.GetProperty("eligibleTotal").GetInt32());
            var ids = all.GetProperty("items").EnumerateArray().Select(p => p.GetProperty("participantId").GetGuid()).ToArray();
            Assert.Equal(new[] { rows[0].Id, rows[1].Id, s.MemberAssignment }.Order(), ids.Order());
            Assert.DoesNotContain(other.MemberAssignment, ids);
            var search = await chair.GetFromJsonAsync<JsonElement>(s.Url + Candidates + "?q=%20SAME%20&pageSize=1&page=2");
            Assert.Equal(3, search.GetProperty("eligibleTotal").GetInt32());
            Assert.Equal(2, search.GetProperty("total").GetInt32());
            Assert.Equal(new[] { rows[0].Id, rows[1].Id }.Max(), search.GetProperty("items")[0].GetProperty("participantId").GetGuid());
            Assert.Equal(users.Single(u => u.Id == search.GetProperty("items")[0].GetProperty("userId").GetGuid()).Email,
                search.GetProperty("items")[0].GetProperty("email").GetString());
            foreach (var query in new[] { "?q=missing", "?page=2147483647&pageSize=100" })
            {
                var empty = await chair.GetFromJsonAsync<JsonElement>(s.Url + Candidates + query);
                Assert.Empty(empty.GetProperty("items").EnumerateArray());
                Assert.Equal(3, empty.GetProperty("eligibleTotal").GetInt32());
                Assert.Equal(JsonValueKind.Null, empty.GetProperty("code").ValueKind);
                Assert.Equal(JsonValueKind.Null, empty.GetProperty("message").ValueKind);
            }
        }
        finally { f.Guests.FailProfiles = false; }
    }

    [Fact]
    public async Task No_eligible_members_is_successful_empty_state_and_has_no_side_effects()
    {
        var s = await f.Seed(); await using var db = f.Db();
        await db.Users.Where(u => u.Id == s.Member).ExecuteUpdateAsync(set => set.SetProperty(u => u.IsActive, false));
        using var chair = f.Client(s.Chair);
        var empty = await Code(await chair.GetAsync(s.Url + Candidates + "?q=missing"), 200, "MSG258");
        Assert.Empty(empty.GetProperty("items").EnumerateArray());
        Assert.Equal(0, empty.GetProperty("total").GetInt32());
        Assert.Equal(0, empty.GetProperty("eligibleTotal").GetInt32());
        await Unchanged(s);
    }

    [Theory]
    [InlineData(MeetingStatus.Draft)]
    [InlineData(MeetingStatus.WaitingApproval)]
    [InlineData(MeetingStatus.Rejected)]
    [InlineData(MeetingStatus.Approved)]
    [InlineData(MeetingStatus.Ended)]
    public async Task Status_gate_precedes_validation(MeetingStatus status)
    {
        var s = await f.Seed(status); using var chair = f.Client(s.Chair);
        await Code(await chair.GetAsync(s.Url + Candidates + "?unknown=x"), 409, "MSG260");
        await Code(await Write(chair, s.Url + Handover, HttpMethod.Post, new { invalid = true }, null), 409, "MSG260");
    }

    [Fact]
    public async Task Access_gate_does_not_leak_resources_or_allow_non_chairs()
    {
        var s = await f.Seed();
        foreach (var id in new Guid?[] { null, s.Outsider, s.Member, s.Secretary, s.Organizer })
        {
            using var client = f.Client(id);
            var responses = new[] { await client.GetAsync(s.Url + Candidates + "?bad=1"), await Post(client, s, tag: null) };
            foreach (var response in responses)
            {
                if (id is null) Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
                else if (id == s.Outsider) { Assert.Equal(HttpStatusCode.NotFound, response.StatusCode); Assert.Empty(await response.Content.ReadAsStringAsync()); }
                else await Code(response, 409, "MSG260");
            }
        }
        await using var db = f.Db();
        var invitation = new MeetingGuestInvitation { MeetingId = s.MeetingId, Email = "guest@candidate.test", InvitedName = "Guest", InvitedById = s.Secretary };
        db.MeetingGuestInvitations.Add(invitation); await db.SaveChangesAsync();
        using var guest = f.Guest(s.MeetingId, invitation.Id);
        await Code(await guest.GetAsync(s.Url + Candidates), 403, "MSG85");
        await Code(await Post(guest, s), 403, "MSG85");
        using var chair = f.Client(s.Chair);
        Assert.Equal(HttpStatusCode.NotFound, (await chair.GetAsync($"/api/meetings/{Guid.NewGuid()}" + Candidates)).StatusCode);
        await db.Users.Where(u => u.Id == s.Chair).ExecuteUpdateAsync(set => set.SetProperty(u => u.IsActive, false));
        Assert.Equal(HttpStatusCode.Forbidden, (await chair.GetAsync(s.Url + Candidates)).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, (await Post(chair, s)).StatusCode);
    }

    [Theory]
    [InlineData("self")]
    [InlineData("secretary")]
    [InlineData("inactive")]
    [InlineData("removed")]
    [InlineData("missing")]
    [InlineData("guest")]
    [InlineData("other")]
    [InlineData("delegated")]
    public async Task Invalid_recipient_precedes_stale_revision(string kind)
    {
        var s = await f.Seed(); await using var db = f.Db();
        var id = s.MemberAssignment;
        switch (kind)
        {
            case "self": id = s.ChairAssignment; break;
            case "secretary": id = s.SecretaryAssignment; break;
            case "missing": id = Guid.NewGuid(); break;
            case "other": id = (await f.Seed()).MemberAssignment; break;
            case "inactive": await db.Users.Where(u => u.Id == s.Member).ExecuteUpdateAsync(set => set.SetProperty(u => u.IsActive, false)); break;
            case "removed": await db.MeetingParticipants.Where(p => p.Id == id).ExecuteUpdateAsync(set => set.SetProperty(p => p.AssignmentStatus, AssignmentStatus.Removed)
                .SetProperty(p => p.RemovedAt, f.Clock.Now).SetProperty(p => p.RemovedById, s.Secretary)); break;
            case "delegated": await db.MeetingParticipants.Where(p => p.Id == id).ExecuteUpdateAsync(set => set.SetProperty(p => p.Response, InvitationResponse.Delegated)); break;
            case "guest":
                var guest = new MeetingGuestInvitation { MeetingId = s.MeetingId, Email = "guest@recipient.test", InvitedName = "Guest", InvitedById = s.Secretary };
                db.Add(guest); await db.SaveChangesAsync(); id = guest.Id; break;
        }
        using var chair = f.Client(s.Chair);
        await Code(await Post(chair, s, id, "\"participants-99\""), 400, "MSG259");
        await Unchanged(s);
    }

    [Theory]
    [InlineData("{")]
    [InlineData("null")]
    [InlineData("[]")]
    [InlineData("{}")]
    [InlineData("{\"newChairParticipantId\":null,\"confirmed\":true}")]
    [InlineData("{\"newChairParticipantId\":\"bad\",\"confirmed\":true}")]
    [InlineData("{\"newChairParticipantId\":\"00000000-0000-0000-0000-000000000000\",\"confirmed\":true}")]
    [InlineData("{\"newChairParticipantId\":\"$id\"}")]
    [InlineData("{\"newChairParticipantId\":\"$id\",\"confirmed\":false}")]
    [InlineData("{\"newChairParticipantId\":\"$id\",\"confirmed\":null}")]
    [InlineData("{\"newChairParticipantId\":\"$id\",\"confirmed\":\"true\"}")]
    [InlineData("{\"newChairParticipantId\":\"$id\",\"confirmed\":true,\"actor\":\"$id\"}")]
    [InlineData("{\"newChairParticipantId\":\"$id\",\"confirmed\":true,\"confirmed\":true}")]
    [InlineData("{\"newChairParticipantId\":\"$id\",\"NewChairParticipantId\":\"$id\",\"confirmed\":true}")]
    public async Task Strict_body_validation_precedes_missing_header(string body)
    {
        var s = await f.Seed(); using var chair = f.Client(s.Chair);
        using var request = new HttpRequestMessage(HttpMethod.Post, s.Url + Handover)
        { Content = new StringContent(body.Replace("$id", s.MemberAssignment.ToString()), Encoding.UTF8, "application/json") };
        await Code(await chair.SendAsync(request), 400, "MSG101");
        await Unchanged(s);
    }

    [Theory]
    [InlineData("?page=0")]
    [InlineData("?page=-1")]
    [InlineData("?page=abc")]
    [InlineData("?page=1&page=2")]
    [InlineData("?pageSize=0")]
    [InlineData("?pageSize=101")]
    [InlineData("?unknown=1")]
    [InlineData("?Page=1")]
    [InlineData("?q=x&q=y")]
    public async Task Query_validation_is_strict(string query)
    {
        var s = await f.Seed(); using var chair = f.Client(s.Chair);
        await Code(await chair.GetAsync(s.Url + Candidates + query), 400, "MSG276");
        await Code(await Write(chair, s.Url + Handover + query, HttpMethod.Post, Body(s.MemberAssignment)), 400, "MSG276");
    }

    [Fact]
    public async Task Header_media_length_and_query_limits_are_enforced()
    {
        var s = await f.Seed(); using var chair = f.Client(s.Chair);
        foreach (var tag in new[] { null, "*", "participants-0", "W/\"participants-0\"", "\"participants--1\"", "\"participants-0\", \"participants-1\"" })
            await Code(await Post(chair, s, tag: tag), 400, "MSG276");
        await Code(await chair.GetAsync(s.Url + Candidates + "?q=" + new string('a', 201)), 400, "MSG276");
        using var wrong = new HttpRequestMessage(HttpMethod.Post, s.Url + Handover) { Content = new StringContent("{}") };
        await Code(await chair.SendAsync(wrong), 415, "MSG34");
        using var large = new HttpRequestMessage(HttpMethod.Post, s.Url + Handover)
        { Content = new StringContent(new string(' ', 65537), Encoding.UTF8, "application/json") };
        await Code(await chair.SendAsync(large), 413, "MSG87");
        await Unchanged(s);
    }

    [Fact]
    public async Task Missing_secretary_blocks_swap_and_read_failures_have_standard_mapping()
    {
        var s = await f.Seed(); using var chair = f.Client(s.Chair); await using var db = f.Db();
        await db.MeetingParticipants.Where(p => p.Id == s.SecretaryAssignment).ExecuteUpdateAsync(set => set.SetProperty(p => p.Role, MeetingRole.Member));
        await Code(await Post(chair, s), 409, "MSG109");
        await Unchanged(s);
        f.Queries.FailMeetingRead = true;
        try { await Code(await chair.GetAsync(s.Url + Candidates), 500, "MSG95"); }
        finally { f.Queries.FailMeetingRead = false; }
    }

    [Theory]
    [InlineData(1)]
    [InlineData(2)]
    public async Task Failure_at_either_save_rolls_back_roles_revision_and_all_side_effects(int save)
    {
        var s = await f.Seed(); using var chair = f.Client(s.Chair);
        f.Failure.MeetingId = s.MeetingId; f.Failure.FailOnSave = save;
        try { await Code(await Post(chair, s), 503, "MSG102"); }
        finally { f.Failure.Reset(); }
        await Unchanged(s);
        await Code(await Post(chair, s), 200, "MSG257");
    }

    [Fact]
    public async Task Transport_failure_after_commit_does_not_report_false_save_failure()
    {
        var s = await f.Seed(); using var chair = f.Client(s.Chair);
        f.Changes.FailMeeting = s.MeetingId;
        try { await Code(await Post(chair, s), 200, "MSG257"); }
        finally { f.Changes.FailMeeting = null; }
        await using var db = f.Db();
        Assert.Equal(MeetingRole.Chair, (await db.MeetingParticipants.FindAsync(s.MemberAssignment))!.Role);
        Assert.Equal(2, await db.MeetingInvitationDispatches.CountAsync(d => d.MeetingId == s.MeetingId));
        await Code(await Post(chair, s), 409, "MSG260");
    }

    private async Task Unchanged(ParticipantFixture.Scenario s)
    {
        await using var db = f.Db();
        Assert.Equal(MeetingRole.Chair, (await db.MeetingParticipants.FindAsync(s.ChairAssignment))!.Role);
        Assert.Equal(0, (await db.Meetings.FindAsync(s.MeetingId))!.ParticipantsRevision);
        Assert.False(await db.Notifications.AnyAsync(n => n.MeetingId == s.MeetingId));
        Assert.False(await db.MeetingInvitationDispatches.AnyAsync(d => d.MeetingId == s.MeetingId));
        Assert.False(await db.AuditLog.AnyAsync(a => a.ObjectId == s.MeetingId));
        Assert.DoesNotContain(f.Changes.Events, e => e.Change.MeetingId == s.MeetingId);
    }
}
