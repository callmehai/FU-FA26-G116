using System.Net;
using System.Net.Http;
using System.Net.Http.Json;
using System.Text;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using static TrustCabinet.Web.Tests.Agenda.AgendaEndpointTests;

namespace TrustCabinet.Web.Tests.Agenda;

/// <summary>
/// Round 1 gap tests for FE-05 (Agenda, UC-23 to UC-26): the cases the report marked "Manual
/// only" (401s, the 404s for items outside the meeting, the read-failure 500), plus the Expected
/// lines the cited tests did not pin (PUT with unchanged data, oversized or mistyped PUT bodies,
/// anonymous mutation calls). Same fixture and <c>AssertCode</c> helper as
/// <see cref="AgendaEndpointTests"/>, against a real PostgreSQL.
/// </summary>
public sealed class AgendaRound1Tests(AgendaFixture fixture) : IClassFixture<AgendaFixture>
{
    // AGENDA_API_IT07: reading the agenda without a token is refused before anything is read.
    [Fact]
    public async Task Get_agenda_requires_a_token()
    {
        var s = await fixture.Seed();

        Assert.Equal(HttpStatusCode.Unauthorized, (await fixture.Client().GetAsync(s.Url)).StatusCode);
    }

    // AGENDA_API_IT14: editor-options answers 401 without a token and 404 for a stranger.
    [Fact]
    public async Task Editor_options_answers_404_for_a_stranger_and_401_without_a_token()
    {
        var s = await fixture.Seed();

        Assert.Equal(HttpStatusCode.Unauthorized,
            (await fixture.Client().GetAsync(s.Url + "/editor-options")).StatusCode);

        using var outsider = fixture.Client(s.Outsider);
        var hidden = await outsider.GetAsync(s.Url + "/editor-options");
        Assert.Equal(HttpStatusCode.NotFound, hidden.StatusCode);
        Assert.Empty(await hidden.Content.ReadAsStringAsync());
    }

    // AGENDA_API_IT15: when the agenda cannot be read, the answer is 500 MSG128, not a 200.
    [Fact]
    public async Task Editor_options_answers_500_MSG128_when_it_cannot_be_read()
    {
        var s = await fixture.Seed();
        using var client = fixture.Client(s.Secretary);

        fixture.FailRead = true;
        try
        {
            await AssertCode(await client.GetAsync(s.Url), 500, "MSG128");
        }
        finally
        {
            fixture.FailRead = false;
        }
    }

    // AGENDA_API_IT42: updating an item that is not in the meeting answers 404, empty body.
    [Fact]
    public async Task Put_item_answers_404_for_an_item_that_is_not_in_the_meeting()
    {
        var s = await fixture.Seed();
        var other = await fixture.Seed();
        using var client = fixture.Client(s.Secretary);

        foreach (var id in new[] { other.Items[0], Guid.NewGuid() })
        {
            var response = await client.PutAsJsonAsync(s.Url + $"/{id}", Input("Sửa"));
            Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
            Assert.Empty(await response.Content.ReadAsStringAsync());
        }

        await using var check = fixture.Db();
        Assert.Equal(3, await check.AgendaItems.CountAsync(i => i.MeetingId == s.MeetingId));
    }

    // AGENDA_API_IT49: deleting without a token is 401; deleting an item outside the meeting
    // is 404 with an empty body.
    [Fact]
    public async Task Delete_item_answers_401_without_a_token_and_404_outside_the_meeting()
    {
        var s = await fixture.Seed();

        Assert.Equal(HttpStatusCode.Unauthorized,
            (await fixture.Client().DeleteAsync(s.Url + $"/{s.Items[0]}")).StatusCode);

        using var client = fixture.Client(s.Secretary);
        var missing = await client.DeleteAsync(s.Url + $"/{Guid.NewGuid()}");
        Assert.Equal(HttpStatusCode.NotFound, missing.StatusCode);
        Assert.Empty(await missing.Content.ReadAsStringAsync());
    }

    // AGENDA_API_IT64: starting without a token is 401; starting an item outside the meeting
    // is 404 with an empty body.
    [Fact]
    public async Task Start_answers_401_without_a_token_and_404_outside_the_meeting()
    {
        var s = await fixture.Seed();

        Assert.Equal(HttpStatusCode.Unauthorized,
            (await fixture.Client().PostAsync(s.Url + $"/{s.Items[0]}/current", null)).StatusCode);

        using var chair = fixture.Client(s.Chair);
        var missing = await chair.PostAsync(s.Url + $"/{Guid.NewGuid()}/current", null);
        Assert.Equal(HttpStatusCode.NotFound, missing.StatusCode);
        Assert.Empty(await missing.Content.ReadAsStringAsync());
    }

    // AGENDA_API_IT37: PUT with unchanged data succeeds without writing — updatedAt keeps its
    // value and no agenda event is pushed to viewers.
    [Fact]
    public async Task Put_unchanged_item_succeeds_without_writing()
    {
        var s = await fixture.Seed();
        using var client = fixture.Client(s.Secretary);
        await AssertCode(await client.PutAsJsonAsync(s.Url + $"/{s.Items[0]}", Input("Giữ nguyên")), 200, "MSG61");

        DateTimeOffset updatedAt;
        await using (var db = fixture.Db())
            updatedAt = (await db.AgendaItems.SingleAsync(i => i.Id == s.Items[0])).UpdatedAt;
        var events = fixture.Events.Count;

        // The exact same values again: still 200 MSG61, but nothing is written.
        await AssertCode(await client.PutAsJsonAsync(s.Url + $"/{s.Items[0]}", Input("Giữ nguyên")), 200, "MSG61");

        await using (var check = fixture.Db())
        {
            Assert.Equal(updatedAt, (await check.AgendaItems.SingleAsync(i => i.Id == s.Items[0])).UpdatedAt);
            Assert.Equal(events, fixture.Events.Count);
        }
    }

    // AGENDA_API_IT44, steps 3–4: a mistyped PUT body is 415 MSG34, an oversized one 413 MSG87.
    [Fact]
    public async Task Put_item_rejects_a_mistyped_and_an_oversized_body()
    {
        var s = await fixture.Seed();
        using var client = fixture.Client(s.Secretary);

        using var plain = new StringContent("{\"title\":\"X\"}", Encoding.UTF8, "text/plain");
        await AssertCode(await client.PutAsync(s.Url + $"/{s.Items[0]}", plain), 415, "MSG34");

        using var huge = new StringContent(
            new string(' ', 65537), Encoding.UTF8, "application/json");
        await AssertCode(await client.PutAsync(s.Url + $"/{s.Items[0]}", huge), 413, "MSG87");
    }

    // AGENDA_API_IT58, step 1: reordering without a token is 401.
    [Fact]
    public async Task Reorder_answers_401_without_a_token()
    {
        var s = await fixture.Seed();

        var response = await fixture.Client().PutAsJsonAsync(s.Url + "/order", new
        {
            orderVersion = "x",
            orderedItemIds = s.Items,
        });

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    // AGENDA_API_IT71, steps 1 and 3: completing without a token is 401; completing an item
    // outside the meeting is 404 with an empty body.
    [Fact]
    public async Task Complete_answers_401_without_a_token_and_404_outside_the_meeting()
    {
        var s = await fixture.Seed();

        Assert.Equal(HttpStatusCode.Unauthorized,
            (await fixture.Client().PostAsync(s.Url + $"/{s.Items[0]}/complete", null)).StatusCode);

        using var chair = fixture.Client(s.Chair);
        var missing = await chair.PostAsync(s.Url + $"/{Guid.NewGuid()}/complete", null);
        Assert.Equal(HttpStatusCode.NotFound, missing.StatusCode);
        Assert.Empty(await missing.Content.ReadAsStringAsync());
    }

    // AGENDA_API_IT31, steps 1–2: creating without a token is 401; an unrelated signed-in
    // account gets 404 with an empty body (a resource it may not read is hidden, not refused).
    [Fact]
    public async Task Create_answers_401_without_a_token_and_404_for_an_unrelated_account()
    {
        var s = await fixture.Seed();

        Assert.Equal(HttpStatusCode.Unauthorized,
            (await fixture.Client().PostAsJsonAsync(s.Url, Input())).StatusCode);

        using var outsider = fixture.Client(s.Outsider);
        var hidden = await outsider.PostAsJsonAsync(s.Url, Input());
        Assert.Equal(HttpStatusCode.NotFound, hidden.StatusCode);
        Assert.Empty(await hidden.Content.ReadAsStringAsync());
    }
}
