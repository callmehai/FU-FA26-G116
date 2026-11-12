using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using TrustCabinet.Domain.Audit;
using TrustCabinet.Domain.Enums;
using static TrustCabinet.Web.Tests.CalendarSharing.CalendarSharingFixture;

namespace TrustCabinet.Web.Tests.CalendarSharing;

/// <summary>SCRUM-142 — UC-84a → UC-84f over HTTP against PostgreSQL: every code, every refusal, notifications and audit.</summary>
public sealed class CalendarShareEndpointTests(CalendarSharingFixture f) : IClassFixture<CalendarSharingFixture>
{
    private static Guid Id(JsonElement body) => body.GetProperty("share").GetProperty("id").GetGuid();

    private static string[] Actions(JsonElement share)
        => [.. share.GetProperty("actions").EnumerateArray().Select(a => a.GetString()!)];

    private static Task<HttpResponseMessage> Post(HttpClient client, Guid id, string action, object? body = null)
        => client.PostAsJsonAsync($"/api/calendar/shares/{id}/{action}", body ?? new { });

    private static Task<HttpResponseMessage> Patch(HttpClient client, Guid id, object body)
        => client.PatchAsJsonAsync($"/api/calendar/shares/{id}", body);

    // ------------------------------------------------------------------ UC-84a Request Calendar Share

    [Fact]
    public async Task An_organizer_asks_to_see_a_calendar_and_the_owner_is_told()
    {
        var p = await f.Seed();
        using var organizer = f.Client(p.Organizer);
        var body = await AssertCode(await Request(organizer, p.Owner, "Level2", Today, Today.AddDays(29)), 201, "MSG435");

        var share = body.GetProperty("share");
        Assert.Equal("Pending", share.GetProperty("status").GetString());
        Assert.Equal("Viewer", share.GetProperty("initiatedBy").GetString());
        Assert.Equal("Level2", share.GetProperty("level").GetString());
        Assert.Equal(Day(Today.AddDays(29)), share.GetProperty("endDate").GetString());
        Assert.Equal(p.Owner, share.GetProperty("owner").GetProperty("id").GetGuid());
        Assert.Equal(["withdraw"], Actions(share));

        var notice = Assert.Single(await f.NotificationsOf(p.Owner));
        Assert.Equal(NotificationType.CalendarShareRequested, notice.Type);
        Assert.Equal("CalendarShare", notice.TargetType);
        Assert.Equal(Id(body), notice.TargetId);
        Assert.Null(notice.MeetingId);
        // The level reads as on the Chia sẻ lịch screen, not "Mốc 2".
        Assert.Contains("(Thường và Nội bộ, ", notice.Body);
        Assert.DoesNotContain("Mốc", notice.Body);
        var audit = Assert.Single(await f.AuditOf(Id(body)));
        Assert.Equal(AuditActions.CalendarShareRequested, audit.Action);
        Assert.Equal(p.Organizer, audit.ActorId);

        // The owner opens it from the bell; nobody else can.
        using var owner = f.Client(p.Owner);
        var target = await owner.GetFromJsonAsync<JsonElement>($"/api/notifications/{notice.Id}/target");
        Assert.Equal($"/calendar/sharing?share={Id(body):D}", target.GetProperty("url").GetString());
        using var outsider = f.Client(p.Owner2);
        Assert.Equal(HttpStatusCode.NotFound, (await outsider.GetAsync($"/api/notifications/{notice.Id}/target")).StatusCode);
    }

    [Fact]
    public async Task Only_a_meeting_organizer_may_ask()
    {
        var p = await f.Seed();
        using var member = f.Client(p.Owner);
        await AssertCode(await Request(member, p.Owner2, "Level1", Today, Today.AddDays(5)), 403, "MSG448");
        await AssertCode(await member.GetAsync("/api/calendar/share-candidates?purpose=request"), 403, "MSG448");
        Assert.Empty(await f.NotificationsOf(p.Owner2));
    }

    [Fact]
    public async Task Asking_yourself_an_unknown_or_a_locked_owner_discloses_nothing()
    {
        var p = await f.Seed();
        using var organizer = f.Client(p.Organizer);
        await AssertNotFound(await Request(organizer, p.Organizer, "Level1", Today, Today.AddDays(5)));
        await AssertNotFound(await Request(organizer, Guid.NewGuid(), "Level1", Today, Today.AddDays(5)));
        await AssertNotFound(await Request(organizer, p.Locked, "Level1", Today, Today.AddDays(5)));
    }

    [Fact]
    public async Task One_open_share_per_direction_of_a_pair()
    {
        var p = await f.Seed();
        using var organizer = f.Client(p.Organizer);
        await AssertCode(await Request(organizer, p.Organizer2, "Level1", Today, Today.AddDays(5)), 201, "MSG435");
        await AssertCode(await Request(organizer, p.Organizer2, "Level2", Today, Today.AddDays(9)), 409, "MSG449");

        // BR-423 is directed: the other organizer may still ask for the first one's calendar.
        using var other = f.Client(p.Organizer2);
        await AssertCode(await Request(other, p.Organizer, "Level1", Today, Today.AddDays(5)), 201, "MSG435");
        // And the owner may not offer the same pair a direct share while the request is open.
        await AssertCode(await Direct(other, p.Organizer, Today, Today.AddDays(5)), 409, "MSG449");
    }

    [Theory]
    [InlineData("Level1", -1, 5)] // starts yesterday
    [InlineData("Level1", 5, 4)] // start after end
    [InlineData("Level1", 0, 365)] // 366 days
    [InlineData("Level3", 0, 5)] // no such level
    [InlineData("level1", 0, 5)]
    public async Task An_invalid_level_or_range_is_refused(string level, int start, int end)
    {
        var p = await f.Seed();
        using var organizer = f.Client(p.Organizer);
        await AssertCode(await Request(organizer, p.Owner, level, Today.AddDays(start), Today.AddDays(end)), 400, "MSG443");
        await AssertCode(await organizer.PostAsJsonAsync("/api/calendar/shares", new { ownerId = p.Owner, level = "Level1" }),
            400, "MSG443");
        Assert.Empty(await f.NotificationsOf(p.Owner));
    }

    [Fact]
    public async Task A_share_of_the_full_365_days_is_accepted()
    {
        var p = await f.Seed();
        using var organizer = f.Client(p.Organizer);
        await AssertCode(await Request(organizer, p.Owner, "Level1", Today, Today.AddDays(364)), 201, "MSG435");
    }

    [Fact]
    public async Task Candidates_are_filtered_and_paged_on_the_server()
    {
        var p = await f.Seed();
        await f.AddShare(p.Owner, p.Organizer, CalendarShareStatus.Pending);
        using var organizer = f.Client(p.Organizer);

        // UC-84a: every active account but the caller, flagged when a share of that direction is already open.
        var request = await organizer.GetFromJsonAsync<JsonElement>("/api/calendar/share-candidates?purpose=request&pageSize=100");
        var owners = request.GetProperty("items").EnumerateArray()
            .ToDictionary(o => o.GetProperty("id").GetGuid(), o => o.GetProperty("hasOpenShare").GetBoolean());
        Assert.True(owners[p.Owner]);
        Assert.False(owners[p.Organizer2]);
        Assert.DoesNotContain(p.Organizer, owners.Keys);
        Assert.DoesNotContain(p.Locked, owners.Keys);
        Assert.True(request.GetProperty("total").GetInt32() >= owners.Count);

        // q matches the name; paging cuts the list.
        await using (var db = f.Db())
        {
            var owner2 = await db.Users.FindAsync(p.Owner2);
            var found = await organizer.GetFromJsonAsync<JsonElement>(
                $"/api/calendar/share-candidates?purpose=request&q={Uri.EscapeDataString(owner2!.FullName)}");
            Assert.Equal(p.Owner2, Assert.Single(found.GetProperty("items").EnumerateArray()).GetProperty("id").GetGuid());
        }

        var first = await organizer.GetFromJsonAsync<JsonElement>("/api/calendar/share-candidates?purpose=request&page=1&pageSize=1");
        Assert.Equal(1, first.GetProperty("items").GetArrayLength());
        Assert.Equal(1, first.GetProperty("pageSize").GetInt32());

        // UC-84b: a Member may look, and sees only Meeting Organizers.
        using var member = f.Client(p.Owner);
        var direct = await member.GetFromJsonAsync<JsonElement>("/api/calendar/share-candidates?purpose=direct&pageSize=100");
        var viewers = direct.GetProperty("items").EnumerateArray().Select(o => o.GetProperty("id").GetGuid()).ToList();
        Assert.Contains(p.Organizer, viewers);
        Assert.Contains(p.Organizer2, viewers);
        Assert.DoesNotContain(p.Owner2, viewers);
        Assert.DoesNotContain(p.Owner, viewers);

        foreach (var bad in new[] { "", "?purpose=other", "?purpose=request&pageSize=101", "?purpose=request&page=0",
            "?purpose=request&purpose=direct", "?purpose=request&sort=name" })
            Assert.Equal(HttpStatusCode.BadRequest, (await organizer.GetAsync($"/api/calendar/share-candidates{bad}")).StatusCode);
    }

    // ------------------------------------------------------------------ UC-84b Share Calendar Directly
    [Fact]
    public async Task An_owner_shares_directly_at_level_1_and_the_recipient_answers()
    {
        var p = await f.Seed();
        using var owner = f.Client(p.Owner);
        var body = await AssertCode(await Direct(owner, p.Organizer, Today, Today.AddDays(10)), 201, "MSG441");
        var share = body.GetProperty("share");
        Assert.Equal("Level1", share.GetProperty("level").GetString());
        Assert.Equal("Owner", share.GetProperty("initiatedBy").GetString());
        var received = Assert.Single(await f.NotificationsOf(p.Organizer));
        Assert.Equal(NotificationType.CalendarShareReceived, received.Type);
        Assert.Contains("(Họp Thường, ", received.Body);

        // The recipient takes it as offered; asking for more is UC-84d.
        using var organizer = f.Client(p.Organizer);
        await AssertCode(await Post(organizer, Id(body), "accept", new { level = "Level2" }), 400, "MSG443");
        var accepted = await AssertCode(await Post(organizer, Id(body), "accept"), 200, "MSG436");
        Assert.Equal("Effective", accepted.GetProperty("share").GetProperty("status").GetString());
        Assert.Equal(NotificationType.CalendarShareAccepted, Assert.Single(await f.NotificationsOf(p.Owner)).Type);
    }

    [Fact]
    public async Task Only_an_active_meeting_organizer_can_receive_a_direct_share()
    {
        var p = await f.Seed();
        using var owner = f.Client(p.Owner);
        await AssertCode(await Direct(owner, p.Owner2, Today, Today.AddDays(5)), 400, "MSG455");
        await AssertCode(await Direct(owner, p.Owner, Today, Today.AddDays(5)), 400, "MSG455");
        await AssertCode(await Direct(owner, p.Locked, Today, Today.AddDays(5)), 400, "MSG455");
        await AssertCode(await Direct(owner, Guid.NewGuid(), Today, Today.AddDays(5)), 400, "MSG455");
        await AssertCode(await Direct(owner, p.Organizer, Today.AddDays(-2), Today.AddDays(5)), 400, "MSG443");
    }

    // ------------------------------------------------------------------ UC-84c Respond to Calendar Share

    [Fact]
    public async Task The_owner_accepts_at_a_lower_level_and_a_shorter_range()
    {
        var p = await f.Seed();
        using var organizer = f.Client(p.Organizer);
        using var owner = f.Client(p.Owner);
        var id = Id(await AssertCode(await Request(organizer, p.Owner, "Level2", Today, Today.AddDays(30)), 201, "MSG435"));

        // Wider than asked is refused.
        await AssertCode(await Post(owner, id, "accept", new { endDate = Day(Today.AddDays(31)) }), 400, "MSG443");
        var body = await AssertCode(await Post(owner, id, "accept",
            new { level = "Level1", startDate = Day(Today.AddDays(1)), endDate = Day(Today.AddDays(10)) }), 200, "MSG436");
        var share = body.GetProperty("share");
        Assert.Equal("Effective", share.GetProperty("status").GetString());
        Assert.Equal("Level1", share.GetProperty("level").GetString());
        Assert.Equal(Day(Today.AddDays(10)), share.GetProperty("endDate").GetString());
        Assert.Equal(["update", "end"], Actions(share));

        var notice = Assert.Single(await f.NotificationsOf(p.Organizer));
        Assert.Equal(NotificationType.CalendarShareAccepted, notice.Type);
        var audit = await f.AuditOf(id);
        Assert.Equal([AuditActions.CalendarShareRequested, AuditActions.CalendarShareAccepted], audit.Select(a => a.Action));
        Assert.Contains("Level2", audit[1].Details); // what was asked stays in the evidence

        await AssertCode(await Post(owner, id, "accept"), 409, "MSG450");
    }

    [Fact]
    public async Task The_owner_declines_and_the_requester_is_told()
    {
        var p = await f.Seed();
        using var organizer = f.Client(p.Organizer);
        using var owner = f.Client(p.Owner);
        var id = Id(await AssertCode(await Request(organizer, p.Owner, "Level1", Today, Today.AddDays(5)), 201, "MSG435"));

        var body = await AssertCode(await Post(owner, id, "decline"), 200, "MSG437");
        Assert.Equal("Declined", body.GetProperty("share").GetProperty("status").GetString());
        Assert.Equal(NotificationType.CalendarShareDeclined, Assert.Single(await f.NotificationsOf(p.Organizer)).Type);
        await AssertCode(await Post(owner, id, "decline"), 409, "MSG450");
        // A declined share blocks nothing: the organizer may ask again.
        await AssertCode(await Request(organizer, p.Owner, "Level1", Today, Today.AddDays(5)), 201, "MSG435");
    }

    [Fact]
    public async Task Only_the_asked_side_answers_and_strangers_learn_nothing()
    {
        var p = await f.Seed();
        using var organizer = f.Client(p.Organizer);
        using var stranger = f.Client(p.Organizer2);
        var id = Id(await AssertCode(await Request(organizer, p.Owner, "Level1", Today, Today.AddDays(5)), 201, "MSG435"));

        await AssertNotFound(await Post(organizer, id, "accept")); // the sender is not the asked side (EX-1)
        await AssertNotFound(await Post(organizer, id, "decline"));
        foreach (var action in new[] { "accept", "decline", "withdraw", "end" })
            await AssertNotFound(await Post(stranger, id, action));
        await AssertNotFound(await Patch(stranger, id, new { level = "Level2" }));
        await AssertNotFound(await Post(stranger, Guid.NewGuid(), "accept"));
        using var anonymous = f.Client();
        Assert.Equal(HttpStatusCode.Unauthorized, (await Post(anonymous, id, "accept")).StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized, (await anonymous.GetAsync("/api/calendar/shares")).StatusCode);
        using var locked = f.Client(p.Locked);
        Assert.Equal(HttpStatusCode.Forbidden, (await locked.GetAsync("/api/calendar/shares")).StatusCode);
        Assert.Equal("Pending", (await f.Share(id)).Status.ToString());
    }

    // ------------------------------------------------------------------ UC-84d Change Calendar Share

    [Fact]
    public async Task The_viewer_asks_for_more_and_the_owner_approves()
    {
        var p = await f.Seed();
        var id = await f.AddShare(p.Owner, p.Organizer, end: Today.AddDays(10));
        using var organizer = f.Client(p.Organizer);
        using var owner = f.Client(p.Owner);

        await AssertCode(await Patch(organizer, id, new { level = "Level1" }), 400, "MSG443"); // nothing grows
        await AssertCode(await Patch(organizer, id, new { startDate = Day(Today.AddDays(1)), level = "Level2" }), 400, "MSG443");
        var asked = await AssertCode(await Patch(organizer, id, new { level = "Level2", endDate = Day(Today.AddDays(60)) }),
            201, "MSG454");
        var pending = asked.GetProperty("share").GetProperty("pendingChange");
        Assert.Equal("Level2", pending.GetProperty("level").GetString());
        Assert.Equal(["withdraw", "end"], Actions(asked.GetProperty("share")));
        Assert.Equal(NotificationType.CalendarShareChangeRequested, Assert.Single(await f.NotificationsOf(p.Owner)).Type);
        // One pending change at a time.
        await AssertCode(await Patch(organizer, id, new { endDate = Day(Today.AddDays(90)) }), 409, "MSG450");

        var approved = await AssertCode(await Post(owner, id, "accept"), 200, "MSG442");
        var share = approved.GetProperty("share");
        Assert.Equal("Level2", share.GetProperty("level").GetString());
        Assert.Equal(Day(Today.AddDays(60)), share.GetProperty("endDate").GetString());
        Assert.Equal(JsonValueKind.Null, share.GetProperty("pendingChange").ValueKind);
        Assert.Equal(NotificationType.CalendarShareUpdated, Assert.Single(await f.NotificationsOf(p.Organizer)).Type);
    }

    [Fact]
    public async Task The_owner_declines_a_change_and_the_share_stays_as_it_was()
    {
        var p = await f.Seed();
        var id = await f.AddShare(p.Owner, p.Organizer, end: Today.AddDays(10));
        using var organizer = f.Client(p.Organizer);
        using var owner = f.Client(p.Owner);
        await AssertCode(await Patch(organizer, id, new { level = "Level2" }), 201, "MSG454");

        var body = await AssertCode(await Post(owner, id, "decline"), 200, "MSG452");
        Assert.Equal("Level1", body.GetProperty("share").GetProperty("level").GetString());
        Assert.Equal("Effective", body.GetProperty("share").GetProperty("status").GetString());
        Assert.Equal(NotificationType.CalendarShareChangeDeclined, Assert.Single(await f.NotificationsOf(p.Organizer)).Type);
        await AssertCode(await Post(owner, id, "decline"), 409, "MSG450"); // nothing is pending any more
    }

    [Fact]
    public async Task The_owner_lowers_the_level_at_once_even_while_a_change_is_pending()
    {
        var p = await f.Seed();
        var id = await f.AddShare(p.Owner, p.Organizer, level: CalendarShareLevel.Level2, end: Today.AddDays(10));
        using var organizer = f.Client(p.Organizer);
        using var owner = f.Client(p.Owner);
        await AssertCode(await Patch(organizer, id, new { endDate = Day(Today.AddDays(40)) }), 201, "MSG454");

        await AssertCode(await Patch(owner, id, new { level = "Level2" }), 400, "MSG443"); // no change at all
        var body = await AssertCode(await Patch(owner, id, new { level = "Level1", endDate = Day(Today.AddDays(5)) }),
            200, "MSG442");
        var share = body.GetProperty("share");
        Assert.Equal("Level1", share.GetProperty("level").GetString());
        Assert.NotEqual(JsonValueKind.Null, share.GetProperty("pendingChange").ValueKind); // still waiting for the owner
        Assert.Contains(await f.NotificationsOf(p.Organizer), n => n.Type == NotificationType.CalendarShareUpdated);
    }

    [Fact]
    public async Task A_change_the_owner_moved_the_dates_past_can_only_be_declined()
    {
        var p = await f.Seed();
        var id = await f.AddShare(p.Owner, p.Organizer, end: Today.AddDays(10));
        using var organizer = f.Client(p.Organizer);
        using var owner = f.Client(p.Owner);
        await AssertCode(await Patch(organizer, id, new { endDate = Day(Today.AddDays(40)) }), 201, "MSG454");
        // UC-84d AF-1: the owner moves the whole share past the end the viewer asked for.
        var moved = await AssertCode(await Patch(owner, id,
            new { startDate = Day(Today.AddDays(50)), endDate = Day(Today.AddDays(55)) }), 200, "MSG442");
        Assert.Equal(["decline", "update", "end"], Actions(moved.GetProperty("share")));

        var audit = (await f.AuditOf(id)).Count;
        var notices = (await f.NotificationsOf(p.Organizer)).Count;
        // A state the owner can only decline, not a save failure to retry (it used to break the range check: 503 MSG447).
        await AssertCode(await Post(owner, id, "accept"), 409, "MSG450");
        var share = await f.Share(id);
        Assert.Equal(Today.AddDays(50), share.StartDate);
        Assert.Equal(Today.AddDays(40), share.PendingEndDate);
        Assert.Equal(audit, (await f.AuditOf(id)).Count);
        Assert.Equal(notices, (await f.NotificationsOf(p.Organizer)).Count);

        await AssertCode(await Post(owner, id, "decline"), 200, "MSG452");
        Assert.Null((await f.Share(id)).PendingEndDate);
    }

    [Fact]
    public async Task Accepting_while_the_sender_is_being_deactivated_waits_for_it_and_refuses()
    {
        var p = await f.Seed();
        var id = await f.AddShare(p.Owner, p.Organizer, CalendarShareStatus.Pending);
        using var owner = f.Client(p.Owner);
        // Warm the app up first, so the wait below measures the lock and not a first request.
        Assert.Equal(HttpStatusCode.OK, (await owner.GetAsync("/api/calendar/shares")).StatusCode);

        // UC-06c running at the same moment: the sender's row is updated but not committed yet.
        await using var connection = await f.Database.DataSource.OpenConnectionAsync();
        await using var deactivation = await connection.BeginTransactionAsync();
        await using (var update = connection.CreateCommand())
        {
            update.Transaction = deactivation;
            update.CommandText = "UPDATE users SET is_active = false WHERE id = @id";
            update.Parameters.AddWithValue("id", p.Organizer);
            await update.ExecuteNonQueryAsync();
        }

        // The stop of BR-424 only looks at Effective shares, so accepting must wait for the deactivation, not run past it
        // and leave an Effective share nobody stops.
        var accept = Post(owner, id, "accept");
        await Task.Delay(TimeSpan.FromSeconds(1));
        Assert.False(accept.IsCompleted, "accept did not wait for the deactivation of its sender");
        await deactivation.CommitAsync();

        await AssertCode(await accept, 409, "MSG468");
        Assert.Equal(CalendarShareStatus.Pending, (await f.Share(id)).Status);
    }

    [Fact]
    public async Task A_pending_share_cannot_be_changed()
    {
        var p = await f.Seed();
        var id = await f.AddShare(p.Owner, p.Organizer, CalendarShareStatus.Pending);
        using var organizer = f.Client(p.Organizer);
        await AssertCode(await Patch(organizer, id, new { level = "Level2" }), 409, "MSG450");
    }

    // ------------------------------------------------------------------ UC-84e Withdraw or End

    [Fact]
    public async Task The_sender_withdraws_a_request_or_a_change()
    {
        var p = await f.Seed();
        using var organizer = f.Client(p.Organizer);
        using var owner = f.Client(p.Owner);
        var id = Id(await AssertCode(await Request(organizer, p.Owner, "Level1", Today, Today.AddDays(5)), 201, "MSG435"));
        await AssertCode(await Post(owner, id, "withdraw"), 409, "MSG450"); // the asked side cannot withdraw
        var body = await AssertCode(await Post(organizer, id, "withdraw"), 200, "MSG438");
        Assert.Equal("Withdrawn", body.GetProperty("share").GetProperty("status").GetString());
        Assert.Contains(await f.NotificationsOf(p.Owner), n => n.Type == NotificationType.CalendarShareWithdrawn);

        var effective = await f.AddShare(p.Owner2, p.Organizer);
        await AssertCode(await Post(organizer, effective, "withdraw"), 409, "MSG450"); // nothing pending
        await AssertCode(await Patch(organizer, effective, new { level = "Level2" }), 201, "MSG454");
        var withdrawn = await AssertCode(await Post(organizer, effective, "withdraw"), 200, "MSG438");
        Assert.Equal("Effective", withdrawn.GetProperty("share").GetProperty("status").GetString());
        Assert.Equal(JsonValueKind.Null, withdrawn.GetProperty("share").GetProperty("pendingChange").ValueKind);
        Assert.Contains(await f.AuditOf(effective), a => a.Action == AuditActions.CalendarShareChangeWithdrawn);
    }

    [Fact]
    public async Task Either_side_ends_an_effective_share()
    {
        var p = await f.Seed();
        using var organizer = f.Client(p.Organizer);
        using var owner = f.Client(p.Owner);
        var first = await f.AddShare(p.Owner, p.Organizer);
        var body = await AssertCode(await Post(owner, first, "end"), 200, "MSG451");
        Assert.Equal("Ended", body.GetProperty("share").GetProperty("status").GetString());
        Assert.Contains(await f.NotificationsOf(p.Organizer), n => n.Type == NotificationType.CalendarShareEnded);
        await AssertCode(await Post(organizer, first, "end"), 409, "MSG450");

        var second = await f.AddShare(p.Owner2, p.Organizer);
        await AssertCode(await Post(organizer, second, "end"), 200, "MSG451");
        var pending = await f.AddShare(p.Owner, p.Organizer, CalendarShareStatus.Pending);
        await AssertCode(await Post(owner, pending, "end"), 409, "MSG450");
    }

    // ------------------------------------------------------------------ List, end date, a lost right, failures

    [Fact]
    public async Task The_list_shows_both_directions_with_what_the_caller_may_do()
    {
        var p = await f.Seed();
        var mine = await f.AddShare(p.Organizer, p.Organizer2);
        var theirs = await f.AddShare(p.Owner, p.Organizer, CalendarShareStatus.Pending);
        using var organizer = f.Client(p.Organizer);
        var lists = await organizer.GetFromJsonAsync<JsonElement>("/api/calendar/shares");
        var asOwner = Assert.Single(lists.GetProperty("asOwner").EnumerateArray());
        Assert.Equal(mine, asOwner.GetProperty("id").GetGuid());
        Assert.Equal(["update", "end"], Actions(asOwner));
        var asViewer = Assert.Single(lists.GetProperty("asViewer").EnumerateArray());
        Assert.Equal(theirs, asViewer.GetProperty("id").GetGuid());
        Assert.Equal(["withdraw"], Actions(asViewer));

        using var stranger = f.Client(p.Owner2);
        var none = await stranger.GetFromJsonAsync<JsonElement>("/api/calendar/shares");
        Assert.Equal(0, none.GetProperty("asOwner").GetArrayLength());
        Assert.Equal(0, none.GetProperty("asViewer").GetArrayLength());
    }

    [Fact]
    public async Task A_share_past_its_end_date_is_gone_without_any_state_or_notice()
    {
        var p = await f.Seed();
        var id = await f.AddShare(p.Owner, p.Organizer, start: Today.AddDays(-10), end: Today.AddDays(-1));
        using var organizer = f.Client(p.Organizer);
        var lists = await organizer.GetFromJsonAsync<JsonElement>("/api/calendar/shares");
        Assert.Equal(0, lists.GetProperty("asViewer").GetArrayLength());

        // BR-425 (PO 09/11/2026, Q-B): acting on it is refused, and nothing is written or sent.
        await AssertCode(await Post(organizer, id, "end"), 409, "MSG450");
        using var owner = f.Client(p.Owner);
        await AssertCode(await Patch(owner, id, new { level = "Level1", endDate = Day(Today.AddDays(5)) }), 409, "MSG450");
        Assert.Equal(CalendarShareStatus.Effective, (await f.Share(id)).Status);
        Assert.Empty(await f.NotificationsOf(p.Owner));
        Assert.Empty(await f.AuditOf(id));
    }

    [Fact]
    public async Task A_new_request_for_the_pair_closes_the_share_past_its_end_date_silently()
    {
        var p = await f.Seed();
        var old = await f.AddShare(p.Owner, p.Organizer, CalendarShareStatus.Pending, start: Today.AddDays(-10),
            end: Today.AddDays(-1));
        using var organizer = f.Client(p.Organizer);
        await AssertCode(await Request(organizer, p.Owner, "Level1", Today, Today.AddDays(5)), 201, "MSG435");

        var closed = await f.Share(old);
        Assert.Equal(CalendarShareStatus.Ended, closed.Status);
        Assert.Null(closed.ClosedById);
        Assert.Equal(AuditActions.CalendarShareClosedAfterEndDate, Assert.Single(await f.AuditOf(old)).Action);
        // The owner hears only about the new request.
        Assert.Equal(NotificationType.CalendarShareRequested, Assert.Single(await f.NotificationsOf(p.Owner)).Type);
    }

    [Fact]
    public async Task An_invitation_whose_sender_was_deactivated_can_only_be_declined()
    {
        var p = await f.Seed();
        using var organizer = f.Client(p.Organizer);
        var requestId = Id(await AssertCode(await Request(organizer, p.Owner, "Level1", Today, Today.AddDays(5)), 201, "MSG435"));
        using var owner2 = f.Client(p.Owner2);
        var directId = Id(await AssertCode(await Direct(owner2, p.Organizer2, Today, Today.AddDays(5)), 201, "MSG441"));
        using var admin = f.Client(p.Admin, "AdminUsers");
        await AssertCode(await admin.PutAsJsonAsync($"/api/users/{p.Organizer}/status", new { isActive = false }), 200, "MSG11");
        await AssertCode(await admin.PutAsJsonAsync($"/api/users/{p.Owner2}/status", new { isActive = false }), 200, "MSG11");

        // PO 09/11/2026, Q-C: deactivation keeps a Pending invitation, and the asked side is told why it cannot accept.
        Assert.Equal(CalendarShareStatus.Pending, (await f.Share(requestId)).Status);
        using var owner = f.Client(p.Owner);
        var listed = Assert.Single((await owner.GetFromJsonAsync<JsonElement>("/api/calendar/shares"))
            .GetProperty("asOwner").EnumerateArray());
        Assert.True(listed.GetProperty("senderDeactivated").GetBoolean());
        Assert.Equal(["decline"], Actions(listed));
        await AssertCode(await Post(owner, requestId, "accept"), 409, "MSG468");
        await AssertCode(await Post(owner, requestId, "decline"), 200, "MSG437");

        using var organizer2 = f.Client(p.Organizer2);
        await AssertCode(await Post(organizer2, directId, "accept"), 409, "MSG468");
        Assert.Equal(CalendarShareStatus.Pending, (await f.Share(directId)).Status);
    }

    [Fact]
    public async Task A_request_whose_viewer_lost_the_right_offers_only_decline()
    {
        var p = await f.Seed();
        using var organizer = f.Client(p.Organizer);
        var id = Id(await AssertCode(await Request(organizer, p.Owner, "Level1", Today, Today.AddDays(5)), 201, "MSG435"));
        using var admin = f.Client(p.Admin, "AdminUsers");
        await using (var db = f.Db())
        {
            var viewer = await db.Users.FindAsync(p.Organizer);
            await AssertCode(await admin.PutAsJsonAsync($"/api/users/{p.Organizer}", new
            {
                fullName = viewer!.FullName, email = viewer.Email, roleId = TrustCabinet.Domain.Access.BuiltInRoles.Member.ToString(),
            }), 200, "MSG10");
        }

        // The invitation stays (only Effective shares stop), but it can no longer become Effective (BR-430, P3-A).
        using var owner = f.Client(p.Owner);
        var listed = Assert.Single((await owner.GetFromJsonAsync<JsonElement>("/api/calendar/shares"))
            .GetProperty("asOwner").EnumerateArray());
        Assert.False(listed.GetProperty("senderDeactivated").GetBoolean());
        Assert.Equal(["decline"], Actions(listed));
        await AssertCode(await Post(owner, id, "accept"), 409, "MSG450");
        await AssertCode(await Post(owner, id, "decline"), 200, "MSG437");
    }

    [Fact]
    public async Task A_member_lists_their_own_shares_but_never_shares_with_them()
    {
        var p = await f.Seed();
        var mine = await f.AddShare(p.Owner, p.Organizer);
        // A row where the Member is the viewer (stopped long ago): not for them to see without the right (BR-430).
        await f.AddShare(p.Owner2, p.Owner, CalendarShareStatus.Stopped);
        using var member = f.Client(p.Owner);
        var lists = await member.GetFromJsonAsync<JsonElement>("/api/calendar/shares");
        Assert.Equal(mine, Assert.Single(lists.GetProperty("asOwner").EnumerateArray()).GetProperty("id").GetGuid());
        Assert.Equal(0, lists.GetProperty("asViewer").GetArrayLength());
    }
    [Fact]
    public async Task A_share_whose_viewer_lost_the_right_without_a_hook_is_stopped_when_touched()
    {
        var p = await f.Seed();
        // The viewer is a Member: no MeetingCreator, and nothing stopped the share when that happened.
        var id = await f.AddShare(p.Owner2, p.Owner);
        using var owner2 = f.Client(p.Owner2);
        await AssertCode(await Post(owner2, id, "end"), 409, "MSG450");
        Assert.Equal(CalendarShareStatus.Stopped, (await f.Share(id)).Status);
        Assert.Contains(await f.AuditOf(id), a => a.Action == AuditActions.CalendarShareStopped);
    }

    [Fact]
    public async Task A_database_outage_is_reported_and_nobody_is_told()
    {
        var p = await f.Seed();
        using var organizer = f.Client(p.Organizer);
        f.Failures.FailSave = true;
        try
        {
            await AssertCode(await Request(organizer, p.Owner, "Level1", Today, Today.AddDays(5)), 503, "MSG447");
        }
        finally
        {
            f.Failures.FailSave = false;
        }

        Assert.Empty(await f.NotificationsOf(p.Owner));
        f.Failures.FailShareRead = true;
        try
        {
            await AssertCode(await organizer.GetAsync("/api/calendar/shares"), 500, "MSG453");
            await AssertCode(await organizer.GetAsync("/api/calendar/share-candidates?purpose=request"), 500, "MSG467");
        }
        finally
        {
            f.Failures.FailShareRead = false;
        }
    }
}
