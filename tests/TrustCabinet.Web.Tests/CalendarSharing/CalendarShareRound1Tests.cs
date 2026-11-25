using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using TrustCabinet.Domain.Access;
using TrustCabinet.Domain.Enums;
using static TrustCabinet.Web.Tests.CalendarSharing.CalendarSharingFixture;

namespace TrustCabinet.Web.Tests.CalendarSharing;

/// <summary>
/// Round 1 gap coverage for SCRUM-142 (UC-84a → UC-84f, UC-85c): the Manual-only cases of
/// docs/08-reports/test-reports/cases/FE-16/api.json — 401s, empty-body 400/403/404s, every MSG443
/// refusal, every MSG450 state refusal, the Vietnam current week and fault injection for MSG467.
/// </summary>
public sealed class CalendarShareRound1Tests(CalendarSharingFixture f) : IClassFixture<CalendarSharingFixture>
{
    private static Task<HttpResponseMessage> Post(HttpClient client, Guid id, string action, object? body = null)
        => client.PostAsJsonAsync($"/api/calendar/shares/{id}/{action}", body ?? new { });

    private static Task<HttpResponseMessage> Patch(HttpClient client, Guid id, object body)
        => client.PatchAsJsonAsync($"/api/calendar/shares/{id}", body);

    private static async Task AssertEmpty(HttpResponseMessage response, HttpStatusCode status)
    {
        var raw = await response.Content.ReadAsStringAsync();
        Assert.True(response.StatusCode == status, $"Expected {(int)status}, got {(int)response.StatusCode}: {raw}");
        Assert.Equal("", raw);
    }

    private static async Task<Guid> Requested(HttpClient viewer, Guid ownerId, string level = "Level1",
        DateOnly? start = null, DateOnly? end = null)
    {
        var body = await AssertCode(
            await viewer.PostAsJsonAsync("/api/calendar/shares",
                new { ownerId, level, startDate = Day(start ?? Today), endDate = Day(end ?? Today.AddDays(5)) }),
            201, "MSG435");
        return body.GetProperty("share").GetProperty("id").GetGuid();
    }

    [Fact]
    public async Task Without_a_token_the_share_list_is_refused()
    {
        using var anonymous = f.Client();
        Assert.Equal(HttpStatusCode.Unauthorized, (await anonymous.GetAsync("/api/calendar/shares")).StatusCode);
    }

    [Fact]
    public async Task A_deactivated_account_gets_an_empty_403_on_the_share_list()
    {
        var p = await f.Seed();
        using var locked = f.Client(p.Locked);
        await AssertEmpty(await locked.GetAsync("/api/calendar/shares"), HttpStatusCode.Forbidden);
    }

    [Fact]
    public async Task A_bad_candidate_query_is_refused_without_a_body()
    {
        var p = await f.Seed();
        using var organizer = f.Client(p.Organizer);
        foreach (var query in new[]
            {
                "?purpose=other", "?purpose=request&pageSize=101", "?purpose=request&page=0",
                "?purpose=request&unknown=1", "?purpose=request&purpose=direct", "?purpose=request&pageSize=1&pageSize=2",
            })
            await AssertEmpty(await organizer.GetAsync($"/api/calendar/share-candidates{query}"), HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task Without_a_token_the_candidate_list_is_refused()
    {
        using var anonymous = f.Client();
        Assert.Equal(HttpStatusCode.Unauthorized,
            (await anonymous.GetAsync("/api/calendar/share-candidates?purpose=request")).StatusCode);
    }

    [Fact]
    public async Task A_candidate_read_failure_answers_MSG467()
    {
        var p = await f.Seed();
        using var organizer = f.Client(p.Organizer);
        f.Failures.FailShareRead = true;
        try
        {
            var body = await AssertCode(
                await organizer.GetAsync("/api/calendar/share-candidates?purpose=request"), 500, "MSG467");
            Assert.Equal("Không tải được danh sách người.", body.GetProperty("message").GetString());
        }
        finally
        {
            f.Failures.FailShareRead = false;
        }
    }

    [Fact]
    public async Task A_range_longer_than_365_days_is_refused()
    {
        var p = await f.Seed();
        using var organizer = f.Client(p.Organizer);
        await AssertCode(await Request(organizer, p.Owner, "Level1", Today, Today.AddDays(365)), 400, "MSG443");
        Assert.Empty(await f.NotificationsOf(p.Owner));
    }

    [Fact]
    public async Task A_start_date_in_the_past_is_refused()
    {
        var p = await f.Seed();
        using var organizer = f.Client(p.Organizer);
        await AssertCode(await Request(organizer, p.Owner, "Level1", Today.AddDays(-1), Today.AddDays(10)), 400, "MSG443");
        Assert.Empty(await f.NotificationsOf(p.Owner));
    }

    [Fact]
    public async Task An_end_date_before_the_start_date_is_refused()
    {
        var p = await f.Seed();
        using var organizer = f.Client(p.Organizer);
        await AssertCode(await Request(organizer, p.Owner, "Level1", Today.AddDays(5), Today.AddDays(2)), 400, "MSG443");
        Assert.Empty(await f.NotificationsOf(p.Owner));
    }

    [Fact]
    public async Task A_missing_or_unknown_level_is_refused()
    {
        var p = await f.Seed();
        using var organizer = f.Client(p.Organizer);
        await AssertCode(await Request(organizer, p.Owner, "Level3", Today, Today.AddDays(5)), 400, "MSG443");
        await AssertCode(await organizer.PostAsJsonAsync("/api/calendar/shares",
            new { ownerId = p.Owner, startDate = Day(Today), endDate = Day(Today.AddDays(5)) }), 400, "MSG443");
        Assert.Empty(await f.NotificationsOf(p.Owner));
    }

    [Fact]
    public async Task Without_a_token_a_share_request_is_refused()
    {
        using var anonymous = f.Client();
        Assert.Equal(HttpStatusCode.Unauthorized,
            (await anonymous.PostAsJsonAsync("/api/calendar/shares", new { })).StatusCode);
    }

    [Fact]
    public async Task A_duplicate_direct_share_answers_MSG449()
    {
        var p = await f.Seed();
        using var owner = f.Client(p.Owner);
        await AssertCode(await Direct(owner, p.Organizer, Today, Today.AddDays(5)), 201, "MSG441");
        var body = await AssertCode(await Direct(owner, p.Organizer, Today, Today.AddDays(9)), 409, "MSG449");
        Assert.Equal("Đã có chia sẻ chờ hoặc hiệu lực với người này.", body.GetProperty("message").GetString());
    }

    [Fact]
    public async Task A_direct_share_with_a_bad_range_answers_MSG443()
    {
        var p = await f.Seed();
        using var owner = f.Client(p.Owner);
        await AssertCode(await Direct(owner, p.Organizer, Today.AddDays(-1), Today.AddDays(5)), 400, "MSG443");
        Assert.Empty(await f.NotificationsOf(p.Organizer));
    }

    [Fact]
    public async Task A_member_may_share_their_own_calendar_directly()
    {
        var p = await f.Seed();
        using var member = f.Client(p.Owner);
        var body = await AssertCode(await Direct(member, p.Organizer, Today, Today.AddDays(5)), 201, "MSG441");
        var share = body.GetProperty("share");
        Assert.Equal("Owner", share.GetProperty("initiatedBy").GetString());
        Assert.Equal("Level1", share.GetProperty("level").GetString());
        Assert.Equal("Pending", share.GetProperty("status").GetString());
    }

    [Fact]
    public async Task Without_a_token_a_direct_share_is_refused()
    {
        using var anonymous = f.Client();
        Assert.Equal(HttpStatusCode.Unauthorized,
            (await anonymous.PostAsJsonAsync("/api/calendar/shares/direct", new { })).StatusCode);
    }

    [Fact]
    public async Task Accepting_a_wider_range_or_higher_level_is_refused()
    {
        var p = await f.Seed();
        using var organizer = f.Client(p.Organizer);
        using var owner = f.Client(p.Owner);
        var id = await Requested(organizer, p.Owner, "Level1", Today, Today.AddDays(30));

        await AssertCode(await Post(owner, id, "accept", new { endDate = Day(Today.AddDays(31)) }), 400, "MSG443");
        await AssertCode(await Post(owner, id, "accept", new { level = "Level2" }), 400, "MSG443");

        Assert.Equal(CalendarShareStatus.Pending, (await f.Share(id)).Status);
        Assert.Empty(await f.NotificationsOf(p.Organizer));
    }

    [Fact]
    public async Task Accepting_a_closed_share_answers_MSG450()
    {
        var p = await f.Seed();
        using var organizer = f.Client(p.Organizer);
        using var owner = f.Client(p.Owner);
        var id = await Requested(organizer, p.Owner);
        await AssertCode(await Post(owner, id, "decline"), 200, "MSG437");
        await AssertCode(await Post(owner, id, "accept"), 409, "MSG450");
    }

    [Fact]
    public async Task Without_a_token_an_accept_is_refused()
    {
        using var anonymous = f.Client();
        Assert.Equal(HttpStatusCode.Unauthorized,
            (await Post(anonymous, Guid.NewGuid(), "accept")).StatusCode);
    }

    [Fact]
    public async Task Declining_a_closed_share_answers_MSG450()
    {
        var p = await f.Seed();
        using var organizer = f.Client(p.Organizer);
        using var owner = f.Client(p.Owner);
        var id = await Requested(organizer, p.Owner);
        await AssertCode(await Post(owner, id, "decline"), 200, "MSG437");
        await AssertCode(await Post(owner, id, "decline"), 409, "MSG450");
    }

    [Fact]
    public async Task Without_a_token_a_decline_is_refused()
    {
        using var anonymous = f.Client();
        Assert.Equal(HttpStatusCode.Unauthorized,
            (await Post(anonymous, Guid.NewGuid(), "decline")).StatusCode);
    }

    [Fact]
    public async Task A_viewer_request_that_lowers_or_changes_nothing_is_refused()
    {
        var p = await f.Seed();
        var id = await f.AddShare(p.Owner, p.Organizer, level: CalendarShareLevel.Level2, end: Today.AddDays(10));
        using var organizer = f.Client(p.Organizer);

        await AssertCode(await Patch(organizer, id, new { level = "Level1" }), 400, "MSG443");
        await AssertCode(await Patch(organizer, id, new { startDate = Day(Today.AddDays(1)) }), 400, "MSG443");
        await AssertCode(await Patch(organizer, id, new { }), 400, "MSG443");

        Assert.Null((await f.Share(id)).PendingEndDate);
    }

    [Fact]
    public async Task A_change_past_365_days_from_the_start_is_refused()
    {
        var p = await f.Seed();
        var id = await f.AddShare(p.Owner, p.Organizer, end: Today.AddDays(10));
        using var organizer = f.Client(p.Organizer);
        await AssertCode(await Patch(organizer, id, new { endDate = Day(Today.AddDays(365)) }), 400, "MSG443");
        Assert.Null((await f.Share(id)).PendingEndDate);
    }

    [Fact]
    public async Task A_second_change_while_one_is_pending_answers_MSG450()
    {
        var p = await f.Seed();
        var id = await f.AddShare(p.Owner, p.Organizer, end: Today.AddDays(10));
        using var organizer = f.Client(p.Organizer);
        await AssertCode(await Patch(organizer, id, new { level = "Level2" }), 201, "MSG454");
        await AssertCode(await Patch(organizer, id, new { endDate = Day(Today.AddDays(40)) }), 409, "MSG450");
    }

    [Fact]
    public async Task A_stranger_cannot_change_a_share()
    {
        var p = await f.Seed();
        var id = await f.AddShare(p.Owner, p.Organizer);
        using var stranger = f.Client(p.Organizer2);
        await AssertNotFound(await Patch(stranger, id, new { level = "Level2" }));
    }

    [Fact]
    public async Task Without_a_token_a_change_is_refused()
    {
        using var anonymous = f.Client();
        Assert.Equal(HttpStatusCode.Unauthorized,
            (await Patch(anonymous, Guid.NewGuid(), new { level = "Level2" })).StatusCode);
    }

    [Fact]
    public async Task Withdrawing_an_effective_share_with_nothing_pending_answers_MSG450()
    {
        var p = await f.Seed();
        var id = await f.AddShare(p.Owner, p.Organizer);
        using var organizer = f.Client(p.Organizer);
        await AssertCode(await Post(organizer, id, "withdraw"), 409, "MSG450");
        Assert.Equal(CalendarShareStatus.Effective, (await f.Share(id)).Status);
    }

    [Fact]
    public async Task A_stranger_cannot_withdraw_a_share()
    {
        var p = await f.Seed();
        var id = await f.AddShare(p.Owner, p.Organizer, CalendarShareStatus.Pending);
        using var stranger = f.Client(p.Organizer2);
        await AssertNotFound(await Post(stranger, id, "withdraw"));
        Assert.Equal(CalendarShareStatus.Pending, (await f.Share(id)).Status);
    }

    [Fact]
    public async Task Without_a_token_a_withdraw_is_refused()
    {
        using var anonymous = f.Client();
        Assert.Equal(HttpStatusCode.Unauthorized,
            (await Post(anonymous, Guid.NewGuid(), "withdraw")).StatusCode);
    }

    [Fact]
    public async Task Ending_a_share_that_is_not_effective_answers_MSG450()
    {
        var p = await f.Seed();
        using var owner = f.Client(p.Owner);
        var id = await f.AddShare(p.Owner, p.Organizer, CalendarShareStatus.Pending);
        await AssertCode(await Post(owner, id, "end"), 409, "MSG450");
        Assert.Equal(CalendarShareStatus.Pending, (await f.Share(id)).Status);
    }

    [Fact]
    public async Task Only_a_party_may_end_a_share()
    {
        var p = await f.Seed();
        var id = await f.AddShare(p.Owner, p.Organizer);
        using var stranger = f.Client(p.Organizer2);
        await AssertNotFound(await Post(stranger, id, "end"));
        using var anonymous = f.Client();
        Assert.Equal(HttpStatusCode.Unauthorized, (await Post(anonymous, id, "end")).StatusCode);
        Assert.Equal(CalendarShareStatus.Effective, (await f.Share(id)).Status);
    }

    [Fact]
    public async Task Without_a_query_the_current_Vietnam_week_is_shown()
    {
        var p = await f.Seed();
        var monday = new DateOnly(2026, 11, 2);
        await f.AddShare(p.Owner, p.Organizer, start: monday, end: monday.AddDays(6));
        using var organizer = f.Client(p.Organizer);
        var week = await organizer.GetFromJsonAsync<JsonElement>("/api/calendar/shared-week");
        // The fixture clock is Sunday 08/11/2026 in Vietnam: the week that began on Monday 02/11.
        Assert.Equal("2026-11-02", week.GetProperty("weekStart").GetString());
        Assert.Equal("2026-11-08", week.GetProperty("weekEnd").GetString());
        Assert.Equal("00:00", week.GetProperty("dayStart").GetString());
        Assert.Equal("24:00", week.GetProperty("dayEnd").GetString());
        Assert.Equal(7, week.GetProperty("people")[0].GetProperty("days").GetArrayLength());
    }

    [Fact]
    public async Task A_stopped_share_is_listed_without_days()
    {
        var p = await f.Seed();
        await f.AddShare(p.Owner, p.Organizer, start: Today, end: Today.AddDays(30));
        using var admin = f.Client(p.Admin, nameof(Capability.AdminUsers));
        await AssertCode(await admin.PutAsJsonAsync($"/api/users/{p.Owner}/status", new { isActive = false }),
            200, "MSG11");

        using var organizer = f.Client(p.Organizer);
        var week = await organizer.GetFromJsonAsync<JsonElement>(
            $"/api/calendar/shared-week?week={Day(new DateOnly(2026, 11, 9))}");
        var person = Assert.Single(week.GetProperty("people").EnumerateArray());
        Assert.Equal("Stopped", person.GetProperty("status").GetString());
        Assert.Equal(0, person.GetProperty("days").GetArrayLength());
    }

    [Fact]
    public async Task A_malformed_week_is_refused_without_a_body()
    {
        var p = await f.Seed();
        using var organizer = f.Client(p.Organizer);
        await AssertEmpty(await organizer.GetAsync("/api/calendar/shared-week?week=not-a-date"),
            HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task The_shared_week_needs_a_token_and_reports_a_read_failure()
    {
        using var anonymous = f.Client();
        Assert.Equal(HttpStatusCode.Unauthorized,
            (await anonymous.GetAsync("/api/calendar/shared-week")).StatusCode);

        var p = await f.Seed();
        using var organizer = f.Client(p.Organizer);
        f.Failures.FailShareRead = true;
        try
        {
            var body = await AssertCode(await organizer.GetAsync("/api/calendar/shared-week"), 500, "MSG453");
            Assert.Equal("Không tải được dữ liệu chia sẻ lịch. Vui lòng thử lại.",
                body.GetProperty("message").GetString());
        }
        finally
        {
            f.Failures.FailShareRead = false;
        }
    }
}
