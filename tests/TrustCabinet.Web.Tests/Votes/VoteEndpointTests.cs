using System.Net.Http.Json;
using System.Text;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using TrustCabinet.Domain.Enums;
using TrustCabinet.Web.Tests.Participants;
using static TrustCabinet.Web.Tests.Attendance.AttendanceEndpointTests;
using static TrustCabinet.Web.Tests.Participants.ParticipantEndpointTests;

namespace TrustCabinet.Web.Tests.Votes;

public sealed class VoteEndpointTests(ParticipantFixture f) : IClassFixture<ParticipantFixture>
{
    private static readonly string[] AllRoles = ["Chair", "Secretary", "Member", "Guest"];

    internal static Dictionary<string, object?> Body(string title = "Thông qua kế hoạch Q4", bool anonymous = false,
        string[]? roles = null, string[]? options = null, bool? openImmediately = null)
    {
        var body = new Dictionary<string, object?>
        {
            ["title"] = title, ["description"] = "", ["options"] = options ?? ["Đồng ý", "Không đồng ý", "Không ý kiến"],
            ["isAnonymous"] = anonymous, ["eligibleRoles"] = roles ?? AllRoles
        };
        if (openImmediately is { } open) body["openImmediately"] = open;
        return body;
    }

    internal static async Task<Guid> Draft(HttpClient client, ParticipantFixture.Scenario s, Dictionary<string, object?>? body = null)
        => (await Code(await client.PostAsJsonAsync(s.Url + "/votes", body ?? Body()), 201, "MSG210")).GetProperty("voteId").GetGuid();

    internal static async Task<Guid> Opened(HttpClient chair, ParticipantFixture.Scenario s, Dictionary<string, object?>? body = null)
    {
        var id = await Draft(chair, s, body);
        await Code(await chair.PostAsync($"{s.Url}/votes/{id}/open", null), 200, "MSG217");
        return id;
    }

    internal static Task<JsonElement> Detail(HttpClient client, ParticipantFixture.Scenario s, Guid id)
        => client.GetFromJsonAsync<JsonElement>($"{s.Url}/votes/{id}");

    internal static async Task<Guid[]> Options(HttpClient client, ParticipantFixture.Scenario s, Guid id)
        => (await Detail(client, s, id)).GetProperty("options").EnumerateArray().Select(o => o.GetProperty("id").GetGuid()).ToArray();

    internal static Task<HttpResponseMessage> Ballot(HttpClient client, ParticipantFixture.Scenario s, Guid id, Guid option)
        => client.PostAsJsonAsync($"{s.Url}/votes/{id}/ballots", new { optionId = option });

    private static async Task<string> Outcome(HttpResponseMessage response)
        => $"{(int)response.StatusCode} {JsonDocument.Parse(await response.Content.ReadAsStringAsync()).RootElement.GetProperty("code").GetString()}";

    [Fact]
    public async Task Secretary_prepares_drafts_that_only_the_chair_and_secretary_see()
    {
        var s = await f.Seed(MeetingStatus.Published);
        using var secretary = f.Client(s.Secretary); using var chair = f.Client(s.Chair); using var member = f.Client(s.Member);
        var id = await Draft(secretary, s);
        var vote = Assert.Single((await chair.GetFromJsonAsync<JsonElement>(s.Url + "/votes")).GetProperty("votes").EnumerateArray());
        Assert.Equal("Draft", vote.GetProperty("status").GetString());
        Assert.True(vote.GetProperty("actions").GetProperty("canEdit").GetBoolean());
        // Opening waits for the sitting (BR-160).
        Assert.False(vote.GetProperty("actions").GetProperty("canOpen").GetBoolean());
        Assert.Empty((await member.GetFromJsonAsync<JsonElement>(s.Url + "/votes")).GetProperty("votes").EnumerateArray());
        await Code(await member.GetAsync($"{s.Url}/votes/{id}"), 404, "MSG205");
        await Code(await member.DeleteAsync($"{s.Url}/votes/{id}"), 404, "MSG205");

        await Code(await secretary.PutAsJsonAsync($"{s.Url}/votes/{id}",
            Body("Đã sửa", anonymous: true, roles: ["Member"], options: ["Có", "Không"])), 200, "MSG211");
        var detail = await Detail(secretary, s, id);
        Assert.Equal("Đã sửa", detail.GetProperty("title").GetString());
        Assert.True(detail.GetProperty("isAnonymous").GetBoolean());
        Assert.Equal(new[] { "Member" }, detail.GetProperty("eligibleRoles").EnumerateArray().Select(r => r.GetString()!).ToArray());
        Assert.Equal(new[] { "1 Có", "2 Không" }, detail.GetProperty("options").EnumerateArray()
            .Select(o => $"{o.GetProperty("position").GetInt32()} {o.GetProperty("label").GetString()}").ToArray());
        // Only the Chair opens from the form, and only during the sitting (UC-53 AF-3).
        await Code(await secretary.PutAsJsonAsync($"{s.Url}/votes/{id}", Body(openImmediately: true)), 409, "MSG218");
        Assert.Equal("Đã sửa", (await Detail(secretary, s, id)).GetProperty("title").GetString());

        await Code(await chair.DeleteAsync($"{s.Url}/votes/{id}"), 200, "MSG212");
        await Code(await chair.GetAsync($"{s.Url}/votes/{id}"), 404, "MSG205");
        await using var db = f.Db();
        Assert.False(await db.VotingOptions.AnyAsync(o => o.VotingSessionId == id));
    }

    [Theory]
    [InlineData("""{"title":" ","options":["A","B"],"isAnonymous":false,"eligibleRoles":["Member"]}""")]
    [InlineData("""{"title":"T","options":["A"],"isAnonymous":false,"eligibleRoles":["Member"]}""")]
    [InlineData("""{"title":"T","options":["A"," a "],"isAnonymous":false,"eligibleRoles":["Member"]}""")]
    [InlineData("""{"title":"T","options":["A",""],"isAnonymous":false,"eligibleRoles":["Member"]}""")]
    [InlineData("""{"title":"T","options":["A",null],"isAnonymous":false,"eligibleRoles":["Member"]}""")]
    [InlineData("""{"title":"T","options":["A","B"],"eligibleRoles":["Member"]}""")]
    [InlineData("""{"title":"T","options":["A","B"],"isAnonymous":"false","eligibleRoles":["Member"]}""")]
    [InlineData("""{"title":"T","options":["A","B"],"isAnonymous":false,"eligibleRoles":[]}""")]
    [InlineData("""{"title":"T","options":["A","B"],"isAnonymous":false,"eligibleRoles":["member"]}""")]
    [InlineData("""{"title":"T","options":["A","B"],"isAnonymous":false,"eligibleRoles":["Member","Member"]}""")]
    [InlineData("""{"title":"T","options":["A","B"],"isAnonymous":false,"eligibleRoles":["Member"],"kind":"Open"}""")]
    [InlineData("""{"title":"T","title":"U","options":["A","B"],"isAnonymous":false,"eligibleRoles":["Member"]}""")]
    [InlineData("[]")]
    public async Task Invalid_configuration_is_rejected_and_saves_nothing(string body)
    {
        var s = await f.Seed(MeetingStatus.Approved); using var secretary = f.Client(s.Secretary);
        await Code(await secretary.PostAsync(s.Url + "/votes", new StringContent(body, Encoding.UTF8, "application/json")), 400, "MSG209");
        await using var db = f.Db();
        Assert.False(await db.VotingSessions.AnyAsync(v => v.MeetingId == s.MeetingId));
    }

    [Fact]
    public async Task Body_must_be_json_and_at_most_64_KiB()
    {
        var s = await f.Seed(MeetingStatus.Approved); using var secretary = f.Client(s.Secretary);
        await Code(await secretary.PostAsync(s.Url + "/votes", new StringContent("{}", Encoding.UTF8, "text/plain")), 415, "MSG34");
        await Code(await secretary.PostAsync(s.Url + "/votes",
            new StringContent(new string(' ', 65 * 1024) + "{}", Encoding.UTF8, "application/json")), 413, "MSG87");
    }

    [Fact]
    public async Task Only_the_current_chair_or_secretary_manage_drafts_and_only_before_or_during_the_sitting()
    {
        var notApproved = await f.Seed(MeetingStatus.Draft);
        using (var secretaryOfDraft = f.Client(notApproved.Secretary))
            await Code(await secretaryOfDraft.PostAsJsonAsync(notApproved.Url + "/votes", Body()), 409, "MSG213");
        var s = await f.Seed(MeetingStatus.InProgress);
        using var member = f.Client(s.Member); using var outsider = f.Client(s.Outsider); using var chair = f.Client(s.Chair);
        await Code(await member.PostAsJsonAsync(s.Url + "/votes", Body()), 409, "MSG213");
        await Code(await outsider.PostAsJsonAsync(s.Url + "/votes", Body()), 404, "MSG205");
        await Code(await outsider.GetAsync(s.Url + "/votes"), 404, "MSG205");
        // An Open vote keeps its configuration and is never deleted (BR-254, BR-189).
        var open = await Opened(chair, s);
        await Code(await chair.PutAsJsonAsync($"{s.Url}/votes/{open}", Body()), 409, "MSG213");
        await Code(await chair.DeleteAsync($"{s.Url}/votes/{open}"), 409, "MSG213");
    }

    [Fact]
    public async Task Opening_fixes_who_may_vote_from_the_selected_roles_regardless_of_attendance()
    {
        var s = await f.Seed(MeetingStatus.InProgress); var guest = await Guest(f, s);
        using var chair = f.Client(s.Chair); using var secretary = f.Client(s.Secretary); using var member = f.Client(s.Member);
        var id = await Draft(secretary, s, Body(roles: ["Member", "Guest"]));
        await Code(await secretary.PostAsync($"{s.Url}/votes/{id}/open", null), 409, "MSG218");
        await Code(await chair.PostAsync($"{s.Url}/votes/{id}/open", null), 200, "MSG217");
        await using var db = f.Db();
        var vote = await db.VotingSessions.Include(v => v.EligibleVoters).AsNoTracking().SingleAsync(v => v.Id == id);
        Assert.Equal(VotingStatus.Open, vote.Status);
        Assert.Equal(f.Clock.Now, vote.OpenedAt);
        Assert.Equal(2, vote.EligibleCount);
        Assert.Equal(s.Member, Assert.Single(vote.EligibleVoters, e => e.UserId is not null).UserId);
        Assert.Equal(guest.Id, Assert.Single(vote.EligibleVoters, e => e.GuestInvitationId is not null).GuestInvitationId);

        // A role change after opening moves nobody in or out (BR-268).
        await db.Database.ExecuteSqlInterpolatedAsync($"UPDATE meeting_participants SET role = 'Secretary' WHERE id = {s.MemberAssignment}");
        Assert.Equal(2, (await Detail(chair, s, id)).GetProperty("eligibleCount").GetInt32());
        Assert.True((await Detail(member, s, id)).GetProperty("actions").GetProperty("canCast").GetBoolean());

        // One Open vote per meeting (BR-266).
        var second = await Draft(secretary, s);
        Assert.False((await Detail(chair, s, second)).GetProperty("actions").GetProperty("canOpen").GetBoolean());
        await Code(await chair.PostAsync($"{s.Url}/votes/{second}/open", null), 409, "MSG219");
        Assert.Equal(VotingStatus.Draft, (await db.VotingSessions.AsNoTracking().SingleAsync(v => v.Id == second)).Status);
    }

    [Fact]
    public async Task A_vote_with_nobody_eligible_does_not_open()
    {
        var s = await f.Seed(MeetingStatus.InProgress); using var chair = f.Client(s.Chair);
        var id = await Draft(chair, s, Body(roles: ["Guest"]));
        await Code(await chair.PostAsync($"{s.Url}/votes/{id}/open", null), 409, "MSG220");
        await using var db = f.Db();
        Assert.Equal(VotingStatus.Draft, (await db.VotingSessions.SingleAsync(v => v.Id == id)).Status);
        Assert.False(await db.VoteEligibleVoters.AnyAsync(e => e.VotingSessionId == id));
    }

    [Fact]
    public async Task Opening_from_the_form_opens_at_once_or_leaves_no_draft()
    {
        var published = await f.Seed(MeetingStatus.Published);
        using (var chairBeforeSitting = f.Client(published.Chair))
            await Code(await chairBeforeSitting.PostAsJsonAsync(published.Url + "/votes", Body(openImmediately: true)), 409, "MSG218");
        var s = await f.Seed(MeetingStatus.InProgress);
        using var chair = f.Client(s.Chair); using var secretary = f.Client(s.Secretary);
        await Code(await secretary.PostAsJsonAsync(s.Url + "/votes", Body(openImmediately: true)), 409, "MSG218");
        var opened = await Code(await chair.PostAsJsonAsync(s.Url + "/votes", Body(openImmediately: true)), 200, "MSG217");
        await Code(await chair.PostAsJsonAsync(s.Url + "/votes", Body("Thứ hai", openImmediately: true)), 409, "MSG219");
        await using var db = f.Db();
        var vote = Assert.Single(await db.VotingSessions.Where(v => v.MeetingId == s.MeetingId).ToArrayAsync());
        Assert.Equal(opened.GetProperty("voteId").GetGuid(), vote.Id);
        Assert.Equal(VotingStatus.Open, vote.Status);
        Assert.Equal(3, vote.EligibleCount);
        Assert.False(await db.VotingSessions.AnyAsync(v => v.MeetingId == published.MeetingId));
    }

    [Fact]
    public async Task Opening_from_the_edit_form_keeps_the_edits_only_when_the_vote_opens()
    {
        var s = await f.Seed(MeetingStatus.InProgress);
        using var chair = f.Client(s.Chair); using var secretary = f.Client(s.Secretary);
        var id = await Draft(secretary, s, Body("Bản cũ"));
        var other = await Opened(chair, s, Body("Đang mở"));

        async Task Unchanged()
        {
            await using var db = f.Db();
            var vote = await db.VotingSessions.Include(v => v.Options).AsNoTracking().SingleAsync(v => v.Id == id);
            Assert.Equal(("Bản cũ", VotingStatus.Draft, 3), (vote.Title, vote.Status, vote.Options.Count));
            Assert.False(await db.VoteEligibleVoters.AnyAsync(e => e.VotingSessionId == id));
        }

        await Code(await chair.PutAsJsonAsync($"{s.Url}/votes/{id}", Body("Bản mới", openImmediately: true)), 409, "MSG219");
        await Unchanged();
        // Another Open vote is reported before the form is validated, as in UC-54 — for creating and editing alike.
        await Code(await chair.PutAsJsonAsync($"{s.Url}/votes/{id}", Body(" ", openImmediately: true)), 409, "MSG219");
        await Code(await chair.PostAsJsonAsync(s.Url + "/votes", Body(" ", openImmediately: true)), 409, "MSG219");
        await Unchanged();

        await Code(await chair.PostAsync($"{s.Url}/votes/{other}/close", null), 200, "MSG229");
        await Code(await secretary.PutAsJsonAsync($"{s.Url}/votes/{id}", Body("Bản mới", openImmediately: true)), 409, "MSG218");
        await Code(await chair.PutAsJsonAsync($"{s.Url}/votes/{id}", Body("Bản mới", roles: ["Guest"], openImmediately: true)), 409, "MSG220");
        await Unchanged();

        await Code(await chair.PutAsJsonAsync($"{s.Url}/votes/{id}",
            Body("Bản mới", options: ["Có", "Không"], openImmediately: true)), 200, "MSG217");
        await using var check = f.Db();
        var opened = await check.VotingSessions.Include(v => v.Options).AsNoTracking().SingleAsync(v => v.Id == id);
        Assert.Equal(("Bản mới", VotingStatus.Open, 3), (opened.Title, opened.Status, opened.EligibleCount));
        Assert.Equal(new[] { "Có", "Không" }, opened.Options.OrderBy(o => o.Position).Select(o => o.Label).ToArray());
        Assert.Equal(f.Clock.Now, opened.OpenedAt);
    }

    [Fact]
    public async Task One_person_one_ballot_and_a_closed_public_vote_shows_each_choice()
    {
        var s = await f.Seed(MeetingStatus.InProgress);
        using var chair = f.Client(s.Chair); using var secretary = f.Client(s.Secretary);
        using var member = f.Client(s.Member); using var outsider = f.Client(s.Outsider);
        var id = await Opened(chair, s);
        var options = await Options(chair, s, id);
        await Code(await chair.PostAsJsonAsync($"{s.Url}/votes/{id}/ballots", new { }), 400, "MSG222");
        await Code(await Ballot(chair, s, id, Guid.CreateVersion7()), 400, "MSG223");
        await Code(await Ballot(outsider, s, id, options[0]), 404, "MSG205");
        await Code(await Ballot(member, s, id, options[1]), 200, "MSG227");
        // Sent once, never changed or withdrawn (BR-265).
        await Code(await Ballot(member, s, id, options[0]), 409, "MSG226");
        await Code(await Ballot(secretary, s, id, options[0]), 200, "MSG227");

        // While open: turnout only (BR-260).
        var open = await Detail(member, s, id);
        Assert.Equal(2, open.GetProperty("receiptCount").GetInt32());
        Assert.Equal(3, open.GetProperty("eligibleCount").GetInt32());
        Assert.True(open.GetProperty("hasVoted").GetBoolean());
        Assert.False(open.GetProperty("actions").GetProperty("canCast").GetBoolean());
        Assert.Equal(JsonValueKind.Null, open.GetProperty("result").ValueKind);

        await Code(await secretary.PostAsync($"{s.Url}/votes/{id}/close", null), 409, "MSG230");
        var result = (await Code(await chair.PostAsync($"{s.Url}/votes/{id}/close", null), 200, "MSG229")).GetProperty("result");
        Assert.Equal(2, result.GetProperty("ballotCount").GetInt32());
        Assert.Equal(new[] { 50d, 50d, 0d }, result.GetProperty("options").EnumerateArray()
            .Select(o => o.GetProperty("percentage").GetDouble()).ToArray());
        await Code(await Ballot(chair, s, id, options[0]), 409, "MSG225");

        // A closed public vote names each eligible voter's choice, or that they did not vote (BR-273).
        var voters = (await Detail(member, s, id)).GetProperty("result").GetProperty("voters").EnumerateArray()
            .ToDictionary(v => v.GetProperty("id").GetGuid(), v => v.GetProperty("optionId"));
        Assert.Equal(options[1], voters[s.Member].GetGuid());
        Assert.Equal(options[0], voters[s.Secretary].GetGuid());
        Assert.Equal(JsonValueKind.Null, voters[s.Chair].ValueKind);
        await using var db = f.Db();
        Assert.Equal(options[1], (await db.VoteChoices.SingleAsync(c => c.VotingSessionId == id && c.VoterId == s.Member)).VotingOptionId);
    }

    [Fact]
    public async Task Only_holders_of_the_selected_roles_at_opening_may_vote()
    {
        var s = await f.Seed(MeetingStatus.InProgress); using var chair = f.Client(s.Chair); using var member = f.Client(s.Member);
        var id = await Opened(chair, s, Body(roles: ["Chair"]));
        await Code(await Ballot(member, s, id, (await Options(chair, s, id))[0]), 409, "MSG224");
        Assert.False((await Detail(member, s, id)).GetProperty("actions").GetProperty("canCast").GetBoolean());
    }

    [Fact]
    public async Task A_secret_ballot_leaves_nothing_that_pairs_a_choice_with_its_voter()
    {
        var s = await f.Seed(MeetingStatus.InProgress);
        using var chair = f.Client(s.Chair); using var secretary = f.Client(s.Secretary); using var member = f.Client(s.Member);
        var id = await Opened(chair, s, Body(anonymous: true));
        var options = await Options(chair, s, id);
        await using var db = f.Db();
        var optionsBefore = await db.VotingOptions.AsNoTracking().Where(o => o.VotingSessionId == id)
            .ToDictionaryAsync(o => o.Id, o => o.UpdatedAt);
        // Lộc's probe from the review of PR #200: three voters, three different options, so any row order or
        // timestamp that followed the ballots would show who chose what.
        foreach (var (client, option) in new[] { (member, options[2]), (chair, options[0]), (secretary, options[1]) })
        {
            f.Clock.Now = f.Clock.Now.AddSeconds(7);
            await Code(await Ballot(client, s, id, option), 200, "MSG227");
        }

        // Nothing is stored per secret choice (BR-259): a plain SELECT has nothing to line up with the receipts.
        Assert.False(await db.VoteChoices.AnyAsync(c => c.VotingSessionId == id));
        // The counters moved, every option row was rewritten in the same statement — one row version for all of
        // them — and none of their timestamps tells when.
        var rows = await db.Database.SqlQuery<OptionRow>($"""
            SELECT id, ballot_count, updated_at, xmin::text AS row_version
            FROM voting_options WHERE voting_session_id = {id}
            """).ToArrayAsync();
        Assert.Equal(new[] { 1, 1, 1 }, rows.OrderBy(r => Array.IndexOf(options, r.Id)).Select(r => r.BallotCount).ToArray());
        Assert.Single(rows.Select(r => r.RowVersion).Distinct());
        Assert.All(rows, r => Assert.Equal(optionsBefore[r.Id], r.UpdatedAt));
        // The receipts still say who voted and when.
        var receipts = await db.VoteReceipts.Where(r => r.VotingSessionId == id).ToArrayAsync();
        Assert.Equal(new[] { s.Secretary, s.Chair, s.Member }.Order(), receipts.Select(r => r.VoterId!.Value).Order());

        await Code(await chair.PostAsync($"{s.Url}/votes/{id}/close", null), 200, "MSG229");
        var result = (await Detail(member, s, id)).GetProperty("result");
        Assert.Equal(JsonValueKind.Null, result.GetProperty("voters").ValueKind);
        Assert.Equal(3, result.GetProperty("ballotCount").GetInt32());
        Assert.Equal(new[] { 1, 1, 1 }, result.GetProperty("options").EnumerateArray().Select(o => o.GetProperty("count").GetInt32()).ToArray());
    }

    private sealed record OptionRow(Guid Id, int BallotCount, DateTimeOffset UpdatedAt, string RowVersion);

    [Fact]
    public async Task The_database_refuses_a_choice_that_names_no_voter()
    {
        var s = await f.Seed(MeetingStatus.InProgress); using var chair = f.Client(s.Chair);
        var id = await Opened(chair, s);
        var option = (await Options(chair, s, id))[0];
        await using var db = f.Db();
        var ex = await Assert.ThrowsAsync<Npgsql.PostgresException>(() => db.Database.ExecuteSqlInterpolatedAsync($"""
            INSERT INTO vote_choices (id, voting_session_id, voting_option_id, created_at, updated_at)
            VALUES ({Guid.NewGuid()}, {id}, {option}, now(), now())
            """));
        Assert.Equal("ck_vote_choices_one_voter", ex.ConstraintName);
    }

    [Fact]
    public async Task A_verified_guest_votes_once_by_invitation()
    {
        var s = await f.Seed(MeetingStatus.InProgress); var invitation = await Guest(f, s);
        using var chair = f.Client(s.Chair);
        var id = await Opened(chair, s);
        var options = await Options(chair, s, id);
        using var guest = f.Guest(s.MeetingId, invitation.Id);
        Assert.Single((await guest.GetFromJsonAsync<JsonElement>(s.Url + "/votes")).GetProperty("votes").EnumerateArray());
        await Code(await Ballot(guest, s, id, options[2]), 200, "MSG227");
        // Back in the room with a new session, still the same invitation (BR-181).
        using var again = f.Guest(s.MeetingId, invitation.Id);
        await Code(await Ballot(again, s, id, options[0]), 409, "MSG226");
        using var unverified = f.Guest(s.MeetingId, invitation.Id, verified: false);
        await Code(await Ballot(unverified, s, id, options[0]), 404, "MSG205");
        // A Guest has a member's rights, so manages nothing.
        await Code(await guest.PostAsync($"{s.Url}/votes/{id}/close", null), 409, "MSG230");
        await using var db = f.Db();
        var receipt = await db.VoteReceipts.SingleAsync(r => r.VotingSessionId == id);
        Assert.Equal(invitation.Id, receipt.GuestInvitationId);
        Assert.Null(receipt.VoterId);
        Assert.Equal(invitation.Id, (await db.VoteChoices.SingleAsync(c => c.VotingSessionId == id)).GuestInvitationId);

        // A Guest loses the votes with the meeting once it ends (PO 30/10/2026), unlike internal readers.
        await db.Meetings.Where(m => m.Id == s.MeetingId).ExecuteUpdateAsync(m => m.SetProperty(x => x.Status, MeetingStatus.Ended));
        await Code(await guest.GetAsync(s.Url + "/votes"), 404, "MSG205");
        Assert.Single((await chair.GetFromJsonAsync<JsonElement>(s.Url + "/votes")).GetProperty("votes").EnumerateArray());
    }

    [Fact]
    public async Task A_guest_invited_while_the_vote_is_open_may_still_vote()
    {
        var s = await f.Seed(MeetingStatus.InProgress);
        using var chair = f.Client(s.Chair); using var secretary = f.Client(s.Secretary);
        var id = await Opened(chair, s);
        var options = await Options(chair, s, id);
        Assert.Equal(3, (await Detail(chair, s, id)).GetProperty("eligibleCount").GetInt32());

        // PO 30/10/2026: unlike internal participants, a Guest invited after opening joins the list.
        var created = await Code(await Write(secretary, s.Url + "/guest-invitations", HttpMethod.Post,
            new { invitedName = "Khách đến muộn", email = $"{Guid.NewGuid():N}@guests.test" }, await Tag(secretary, s.Url)), 201, "MSG96");
        var invitation = created.GetProperty("createdIds")[0].GetGuid();
        Assert.Equal(4, (await Detail(chair, s, id)).GetProperty("eligibleCount").GetInt32());
        using var guest = f.Guest(s.MeetingId, invitation);
        await Code(await Ballot(guest, s, id, options[1]), 200, "MSG227");

        // A vote that does not admit Guests is left as it was.
        await Code(await chair.PostAsync($"{s.Url}/votes/{id}/close", null), 200, "MSG229");
        var membersOnly = await Opened(chair, s, Body("Chỉ thành viên", roles: ["Member"]));
        await Code(await Write(secretary, s.Url + "/guest-invitations", HttpMethod.Post,
            new { invitedName = "Khách khác", email = $"{Guid.NewGuid():N}@guests.test" }, await Tag(secretary, s.Url)), 201, "MSG96");
        Assert.Equal(1, (await Detail(chair, s, membersOnly)).GetProperty("eligibleCount").GetInt32());
        await using var db = f.Db();
        Assert.False(await db.VoteEligibleVoters.AnyAsync(e => e.VotingSessionId == membersOnly && e.GuestInvitationId != null));
    }

    [Fact]
    public async Task After_the_meeting_ends_the_chair_still_closes_and_concludes_once()
    {
        var s = await f.Seed(MeetingStatus.InProgress);
        using var chair = f.Client(s.Chair); using var member = f.Client(s.Member);
        var id = await Opened(chair, s);
        var options = await Options(chair, s, id);
        await Code(await chair.PostAsJsonAsync($"{s.Url}/votes/{id}/conclusion", new { conclusion = "Passed" }), 409, "MSG234");
        await using (var db = f.Db())
            await db.Meetings.Where(m => m.Id == s.MeetingId).ExecuteUpdateAsync(m => m.SetProperty(x => x.Status, MeetingStatus.Ended));

        // Ending the meeting stopped new ballots but did not close the vote (ADR-0017, BR-159).
        await Code(await Ballot(member, s, id, options[0]), 409, "MSG225");
        await Code(await chair.PostAsync($"{s.Url}/votes/{id}/close", null), 200, "MSG229");
        await Code(await member.PostAsJsonAsync($"{s.Url}/votes/{id}/conclusion", new { conclusion = "Passed" }), 409, "MSG234");
        await Code(await chair.PostAsJsonAsync($"{s.Url}/votes/{id}/conclusion", new { conclusion = "passed" }), 400, "MSG232");
        await Code(await chair.PostAsJsonAsync($"{s.Url}/votes/{id}/conclusion",
            new { conclusion = "Passed", note = new string('x', 2001) }), 400, "MSG232");
        await Code(await chair.PostAsJsonAsync($"{s.Url}/votes/{id}/conclusion",
            new { conclusion = "Deferred", note = "  Chờ ý kiến bộ  " }), 200, "MSG235");
        await Code(await chair.PostAsJsonAsync($"{s.Url}/votes/{id}/conclusion", new { conclusion = "Passed" }), 409, "MSG233");
        var conclusion = (await Detail(member, s, id)).GetProperty("conclusion");
        Assert.Equal("Deferred", conclusion.GetProperty("value").GetString());
        Assert.Equal("Chờ ý kiến bộ", conclusion.GetProperty("note").GetString());
        Assert.Equal(s.Chair, conclusion.GetProperty("concludedById").GetGuid());
    }

    [Fact]
    public async Task Simultaneous_ballots_from_one_person_record_exactly_one()
    {
        var s = await f.Seed(MeetingStatus.InProgress); using var chair = f.Client(s.Chair);
        var id = await Opened(chair, s);
        var options = await Options(chair, s, id);
        using var first = f.Client(s.Member); using var second = f.Client(s.Member);
        var responses = await Task.WhenAll(Ballot(first, s, id, options[0]), Ballot(second, s, id, options[1]));
        Assert.Equal(new[] { "200 MSG227", "409 MSG226" }, (await Task.WhenAll(responses.Select(Outcome))).Order().ToArray());
        await using var db = f.Db();
        Assert.Equal(1, await db.VoteReceipts.CountAsync(r => r.VotingSessionId == id));
        Assert.Equal(1, await db.VoteChoices.CountAsync(c => c.VotingSessionId == id));
        // The refused ballot's count was rolled back with its receipt.
        Assert.Equal(1, await db.VotingOptions.Where(o => o.VotingSessionId == id).SumAsync(o => o.BallotCount));
    }

    [Fact]
    public async Task Simultaneous_opens_leave_one_vote_open()
    {
        var s = await f.Seed(MeetingStatus.InProgress); using var chair = f.Client(s.Chair);
        var a = await Draft(chair, s); var b = await Draft(chair, s, Body("Thứ hai"));
        var responses = await Task.WhenAll(chair.PostAsync($"{s.Url}/votes/{a}/open", null), chair.PostAsync($"{s.Url}/votes/{b}/open", null));
        Assert.Equal(new[] { "200 MSG217", "409 MSG219" }, (await Task.WhenAll(responses.Select(Outcome))).Order().ToArray());
        await using var db = f.Db();
        Assert.Equal(1, await db.VotingSessions.CountAsync(v => v.MeetingId == s.MeetingId && v.Status == VotingStatus.Open));
    }

    [Fact]
    public async Task A_ballot_that_fails_at_commit_leaves_neither_receipt_nor_count()
    {
        var s = await f.Seed(MeetingStatus.InProgress); using var chair = f.Client(s.Chair); using var member = f.Client(s.Member);
        var id = await Opened(chair, s, Body(anonymous: true));
        var options = await Options(chair, s, id);
        await using var db = f.Db();
        // A deferred constraint fails after the receipt and the count were written; this fixture's database is disposable.
        await db.Database.ExecuteSqlRawAsync("""
            CREATE FUNCTION vote_test_reject_commit() RETURNS trigger LANGUAGE plpgsql AS $$
            BEGIN RAISE EXCEPTION 'Injected ballot commit failure' USING ERRCODE = '23514'; END $$;
            CREATE CONSTRAINT TRIGGER vote_test_reject_commit AFTER INSERT ON vote_receipts
              DEFERRABLE INITIALLY DEFERRED FOR EACH ROW EXECUTE FUNCTION vote_test_reject_commit();
            """);
        try { await Code(await Ballot(member, s, id, options[0]), 503, "MSG228"); }
        finally
        {
            await db.Database.ExecuteSqlRawAsync("DROP TRIGGER vote_test_reject_commit ON vote_receipts; DROP FUNCTION vote_test_reject_commit();");
        }
        Assert.False(await db.VoteReceipts.AnyAsync(r => r.VotingSessionId == id));
        Assert.Equal(0, await db.VotingOptions.Where(o => o.VotingSessionId == id).SumAsync(o => o.BallotCount));
        await Code(await Ballot(member, s, id, options[0]), 200, "MSG227");
    }

    [Fact]
    public async Task Database_failures_answer_their_own_codes_and_change_nothing()
    {
        var s = await f.Seed(MeetingStatus.InProgress); using var chair = f.Client(s.Chair);
        var closed = await Opened(chair, s, Body("Đã đóng"));
        await Code(await chair.PostAsync($"{s.Url}/votes/{closed}/close", null), 200, "MSG229");
        var draft = await Draft(chair, s, Body("Nháp"));
        var open = await Opened(chair, s, Body("Đang mở"));
        var option = (await Options(chair, s, open))[0];

        f.Queries.FailMeetingRead = true;
        try
        {
            await Code(await chair.GetAsync(s.Url + "/votes"), 500, "MSG206");
            await Code(await chair.GetAsync($"{s.Url}/votes/{open}"), 500, "MSG208");
            await Code(await chair.PostAsJsonAsync(s.Url + "/votes", Body()), 503, "MSG214");
            await Code(await chair.PostAsJsonAsync(s.Url + "/votes", Body(openImmediately: true)), 503, "MSG221");
            await Code(await chair.PutAsJsonAsync($"{s.Url}/votes/{draft}", Body("Đổi")), 503, "MSG215");
            await Code(await chair.DeleteAsync($"{s.Url}/votes/{draft}"), 503, "MSG216");
            await Code(await chair.PostAsync($"{s.Url}/votes/{draft}/open", null), 503, "MSG221");
            await Code(await Ballot(chair, s, open, option), 503, "MSG228");
            await Code(await chair.PostAsync($"{s.Url}/votes/{open}/close", null), 503, "MSG231");
            await Code(await chair.PostAsJsonAsync($"{s.Url}/votes/{closed}/conclusion", new { conclusion = "Passed" }), 503, "MSG236");
        }
        finally { f.Queries.FailMeetingRead = false; }

        await using var db = f.Db();
        var votes = await db.VotingSessions.AsNoTracking().Where(v => v.MeetingId == s.MeetingId).ToDictionaryAsync(v => v.Id);
        Assert.Equal(3, votes.Count);
        Assert.Equal(("Nháp", VotingStatus.Draft), (votes[draft].Title, votes[draft].Status));
        Assert.Equal(VotingStatus.Open, votes[open].Status);
        Assert.Null(votes[closed].Conclusion);
        Assert.False(await db.VoteReceipts.AnyAsync(r => r.VotingSessionId == open));
    }
}
