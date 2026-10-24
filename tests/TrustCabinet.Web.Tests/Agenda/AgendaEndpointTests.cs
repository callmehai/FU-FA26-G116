using System.Net;
using System.Net.Http.Json;
using System.Text;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using TrustCabinet.Domain.Enums;

namespace TrustCabinet.Web.Tests.Agenda;

public sealed class AgendaEndpointTests(AgendaFixture fixture) : IClassFixture<AgendaFixture>
{
    internal static object Input(string title = "Tiêu đề", Guid? presenter = null, Guid[]? documents = null)
        => new { title, description = "Dòng 1\nDòng 2", presenterId = presenter, documentIds = documents ?? [] };

    internal static async Task<JsonElement> AssertCode(HttpResponseMessage response, int status, string code)
    {
        var raw = await response.Content.ReadAsStringAsync();
        Assert.True((int)response.StatusCode == status, $"Expected {status}, got {(int)response.StatusCode}: {raw}");
        var json = JsonDocument.Parse(raw).RootElement.Clone();
        Assert.Equal(code, json.GetProperty("code").GetString());
        return json;
    }

    [Theory]
    [InlineData(MeetingStatus.Draft)]
    [InlineData(MeetingStatus.WaitingApproval)]
    [InlineData(MeetingStatus.Rejected)]
    [InlineData(MeetingStatus.Approved)]
    [InlineData(MeetingStatus.Published)]
    [InlineData(MeetingStatus.InProgress)]
    public async Task Secretary_can_plan_in_all_six_states(MeetingStatus status)
    {
        var s = await fixture.Seed(status, 0);
        using var client = fixture.Client(s.Secretary);
        var created = await client.PostAsJsonAsync(s.Url, Input("  Báo cáo  ", presenter: s.Member, documents: [s.DocumentId]));
        var body = await AssertCode(created, 201, "MSG60");
        Assert.Equal(s.Url, created.Headers.Location!.ToString());
        var snapshot = await client.GetFromJsonAsync<JsonElement>(s.Url);
        var item = Assert.Single(snapshot.GetProperty("items").EnumerateArray());
        Assert.Equal("Báo cáo", item.GetProperty("title").GetString());
        Assert.Equal("Pending", item.GetProperty("status").GetString());
        Assert.Equal(1, item.GetProperty("position").GetInt32());
        Assert.Equal(body.GetProperty("agendaItemId").GetGuid(), item.GetProperty("id").GetGuid());
        Assert.Single(item.GetProperty("documents").EnumerateArray());
        Assert.True(item.GetProperty("actions").GetProperty("canEditPlanningFields").GetBoolean());
        var raw = snapshot.GetRawText();
        Assert.DoesNotContain("@agenda.test", raw);
        Assert.DoesNotContain("private/test", raw);
        var options = await client.GetFromJsonAsync<JsonElement>(s.Url + "/editor-options");
        Assert.Equal(3, options.GetProperty("presenters").GetArrayLength());
    }

    [Fact]
    public async Task Empty_snapshot_errors_visibility_and_current_role_are_authoritative()
    {
        var s = await fixture.Seed(count: 0);
        using var anonymous = fixture.Client();
        Assert.Equal(HttpStatusCode.Unauthorized, (await anonymous.GetAsync(s.Url)).StatusCode);
        using var outsider = fixture.Client(s.Outsider);
        var hidden = await outsider.GetAsync(s.Url);
        var missing = await outsider.GetAsync($"/api/meetings/{Guid.NewGuid()}/agenda");
        Assert.Equal(HttpStatusCode.NotFound, hidden.StatusCode);
        Assert.Equal(await missing.Content.ReadAsStringAsync(), await hidden.Content.ReadAsStringAsync());
        using var secretary = fixture.Client(s.Secretary);
        var snapshot = await secretary.GetFromJsonAsync<JsonElement>(s.Url);
        Assert.Empty(snapshot.GetProperty("items").EnumerateArray());
        using var member = fixture.Client(s.Member);
        await AssertCode(await member.PostAsJsonAsync(s.Url, Input()), 403, "MSG67");
        await AssertCode(await member.GetAsync(s.Url + "/editor-options"), 403, "MSG67");
        using var organizer = fixture.Client(s.Organizer);
        await AssertCode(await organizer.PostAsJsonAsync(s.Url, Input()), 403, "MSG67");
        await using var db = fixture.Db();
        var assignment = await db.MeetingParticipants.SingleAsync(p => p.MeetingId == s.MeetingId && p.UserId == s.Secretary);
        assignment.Role = MeetingRole.Member;
        await db.SaveChangesAsync();
        await AssertCode(await secretary.PostAsJsonAsync(s.Url, Input()), 403, "MSG67");
        (await db.Users.SingleAsync(u => u.Id == s.Secretary)).IsActive = false;
        await db.SaveChangesAsync();
        Assert.Equal(HttpStatusCode.Forbidden, (await secretary.GetAsync(s.Url)).StatusCode);
    }

    [Theory]
    [InlineData("{\"title\":\"X\",\"presenterId\":\"bad\"}")]
    [InlineData("{\"Title\":\"X\"}")]
    [InlineData("{\"title\":\"X\",\"title\":\"Y\"}")]
    [InlineData("{\"title\":\"X\",\"status\":\"Done\"}")]
    [InlineData("{\"title\":\"X\",\"actualStartedAt\":null}")]
    [InlineData("{\"title\":\"X\",\"presenterName\":\"Legacy\"}")]
    [InlineData("{}") ]
    [InlineData("null")]
    [InlineData("[]")]
    [InlineData("{")]
    [InlineData("{\"title\":\" \",\"plannedDurationMinutes\":20}")]
    [InlineData("{\"title\":\"X\",\"plannedDurationMinutes\":null}")]
    [InlineData("{\"title\":\"X\",\"plannedDurationMinutes\":0}")]
    [InlineData("{\"title\":\"X\",\"plannedDurationMinutes\":-1}")]
    [InlineData("{\"title\":\"X\",\"plannedDurationMinutes\":1.5}")]
    [InlineData("{\"title\":\"X\",\"plannedDurationMinutes\":\"20\"}")]
    [InlineData("{\"title\":\"X\",\"plannedDurationMinutes\":2147483648}")]
    [InlineData("{\"title\":\"X\",\"plannedDurationMinutes\":20,\"presenterId\":\"bad\"}")]
    [InlineData("{\"title\":\"X\",\"plannedDurationMinutes\":20,\"status\":\"Done\"}")]
    [InlineData("{\"title\":\"X\",\"title\":\"Y\",\"plannedDurationMinutes\":20}")]
    public async Task Invalid_json_never_writes_partial_data(string json)
    {
        var s = await fixture.Seed(count: 0);
        using var client = fixture.Client(s.Secretary);
        var body = await AssertCode(await client.PostAsync(s.Url, new StringContent(json, Encoding.UTF8, "application/json")), 400, "MSG66");
        Assert.NotEqual(JsonValueKind.Null, body.GetProperty("errors").ValueKind);
        await using var db = fixture.Db();
        Assert.Empty(await db.AgendaItems.Where(i => i.MeetingId == s.MeetingId).ToListAsync());
    }

    [Theory]
    [InlineData("\\ud800")]
    [InlineData("\\udc00")]
    public async Task Malformed_unicode_property_names_return_validation_without_mutation(string name)
    {
        var s = await fixture.Seed();
        using var client = fixture.Client(s.Secretary);
        var before = await client.GetFromJsonAsync<JsonElement>(s.Url);
        var events = fixture.Events.Count;
        foreach (var action in new[] { "create", "update", "order" })
        {
            using var content = new StringContent("{\"" + name + "\":1}", Encoding.UTF8, "application/json");
            using var response = action == "create" ? await client.PostAsync(s.Url, content)
                : await client.PutAsync(s.Url + (action == "order" ? "/order" : $"/{s.Items[0]}"), content);
            var body = await AssertCode(response, 400, action == "order" ? "MSG143" : "MSG66");
            Assert.True(body.GetProperty("errors").TryGetProperty("body", out _));
        }
        var after = await client.GetFromJsonAsync<JsonElement>(s.Url);
        Assert.Equal(before.GetProperty("items").GetRawText(), after.GetProperty("items").GetRawText());
        Assert.Equal(before.GetProperty("orderVersion").GetString(), after.GetProperty("orderVersion").GetString());
        Assert.Equal(events, fixture.Events.Count);
    }

    [Fact]
    public async Task Limits_duplicate_ids_presenter_and_document_validation()
    {
        var s = await fixture.Seed();
        var other = await fixture.Seed();
        using var client = fixture.Client(s.Secretary);
        var bad = new object[] {
            Input(new string('x', 301)),
            new { title = "X", description = new string('x', 4001) },
            new { title = "X", presenterGuestInvitationId = Guid.Empty },
            new { title = "X", presenterGuestInvitationId = Guid.NewGuid(), presenterId = s.Member },
            Input(presenter: s.Outsider), Input(presenter: Guid.Empty),
            Input(documents: [s.DocumentId, s.DocumentId]), Input(documents: [Guid.Empty]), Input(documents: [other.DocumentId]) };
        foreach (var input in bad) await AssertCode(await client.PostAsJsonAsync(s.Url, input), 400, "MSG66");
        await AssertCode(await client.PostAsync(s.Url, new StringContent("{}")), 415, "MSG34");
        await AssertCode(await client.PostAsync(s.Url, new StringContent(new string(' ', 65537), Encoding.UTF8, "application/json")), 413, "MSG87");
        using var chunked = new UnknownLengthContent(new byte[65537]);
        chunked.Headers.ContentType = new("application/json");
        await AssertCode(await client.PostAsync(s.Url, chunked), 413, "MSG87");
        var foreign = await client.PutAsJsonAsync(s.Url + $"/{other.Items[0]}", Input());
        Assert.Equal(HttpStatusCode.NotFound, foreign.StatusCode);
        Assert.Empty(await foreign.Content.ReadAsStringAsync());
    }

    private sealed class UnknownLengthContent(byte[] bytes) : HttpContent
    {
        protected override bool TryComputeLength(out long length) { length = 0; return false; }
        protected override Task SerializeToStreamAsync(Stream stream, TransportContext? context) => stream.WriteAsync(bytes).AsTask();
    }

    [Fact]
    public async Task Planning_update_delete_reorder_preserve_fixed_positions_and_documents()
    {
        var s = await fixture.Seed();
        using var client = fixture.Client(s.Secretary);
        await AssertCode(await client.PutAsJsonAsync(s.Url + $"/{s.Items[1]}", Input(documents: [s.DocumentId])), 200, "MSG61");
        await AssertCode(await client.PutAsJsonAsync(s.Url + $"/{s.Items[0]}", Input(documents: [s.DocumentId])), 200, "MSG61");
        await AssertCode(await client.DeleteAsync(s.Url + $"/{s.Items[1]}"), 200, "MSG62");
        await using (var db = fixture.Db())
        {
            Assert.Equal(s.Items[0], await AgendaFixture.LinkedItem(db, s.DocumentId));
            Assert.True(await db.MeetingDocuments.AnyAsync(d => d.Id == s.DocumentId && d.RemovedAt == null));
        }
        var created = await AssertCode(await client.PostAsJsonAsync(s.Url, Input()), 201, "MSG60");
        var last = created.GetProperty("agendaItemId").GetGuid();
        var snapshot = await client.GetFromJsonAsync<JsonElement>(s.Url);
        Assert.Equal(new[] { 1, 7, 8 }, snapshot.GetProperty("items").EnumerateArray().Select(i => i.GetProperty("position").GetInt32()));
        var order = new { orderVersion = snapshot.GetProperty("orderVersion").GetString(), orderedItemIds = new[] { last, s.Items[2], s.Items[0] } };
        await AssertCode(await client.PutAsJsonAsync(s.Url + "/order", order), 200, "MSG63");
        await AssertCode(await client.PutAsJsonAsync(s.Url + "/order", order), 409, "MSG143");
        snapshot = await client.GetFromJsonAsync<JsonElement>(s.Url);
        await AssertCode(await client.PutAsJsonAsync(s.Url + "/order", new { orderVersion = snapshot.GetProperty("orderVersion").GetString(), orderedItemIds = new[] { last } }), 400, "MSG143");
        Assert.Equal(last, snapshot.GetProperty("items")[0].GetProperty("id").GetGuid());
        using var organizer = fixture.Client(s.Organizer);
        await AssertCode(await client.PutAsJsonAsync(s.Url + $"/{last}", Input(documents: [s.DocumentId])), 200, "MSG61");
        var hidden = await organizer.GetFromJsonAsync<JsonElement>(s.Url);
        Assert.True(hidden.GetProperty("items")[0].GetProperty("canReadDocuments").GetBoolean());
        Assert.Single(hidden.GetProperty("items")[0].GetProperty("documents").EnumerateArray());
    }

    [Fact]
    public async Task Execution_and_locked_fields_obey_role_state_and_server_clock()
    {
        var s = await fixture.Seed();
        using var chair = fixture.Client(s.Chair);
        using var secretary = fixture.Client(s.Secretary);
        await AssertCode(await secretary.PostAsync(s.Url + $"/{s.Items[0]}/current", null), 403, "MSG73");
        await AssertCode(await secretary.PostAsync(s.Url + $"/{s.Items[0]}/complete", null), 403, "MSG74");
        await AssertCode(await chair.PostAsync(s.Url + $"/{s.Items[0]}/current", null), 200, "MSG64");
        await AssertCode(await chair.PostAsync(s.Url + $"/{s.Items[1]}/current", null), 409, "MSG72");
        await AssertCode(await chair.PostAsync(s.Url + $"/{s.Items[0]}/current", null), 409, "MSG73");
        await AssertCode(await chair.PostAsync(s.Url + $"/{s.Items[1]}/complete", null), 409, "MSG74");
        await AssertCode(await secretary.PutAsJsonAsync(s.Url + $"/{s.Items[0]}", Input("Sửa tiêu đề")), 200, "MSG61");
        await AssertCode(await secretary.PutAsJsonAsync(s.Url + $"/{s.Items[0]}", Input("Sửa tiêu đề", presenter: s.Member, documents: [s.DocumentId])), 200, "MSG61");
        await AssertCode(await secretary.DeleteAsync(s.Url + $"/{s.Items[0]}"), 409, "MSG142");
        await AssertCode(await chair.PostAsync(s.Url + $"/{s.Items[0]}/complete", null), 200, "MSG65");
        await AssertCode(await chair.PostAsync(s.Url + $"/{s.Items[0]}/complete", null), 409, "MSG74");
        var snapshot = await secretary.GetFromJsonAsync<JsonElement>(s.Url);
        var item = snapshot.GetProperty("items")[0];
        Assert.Equal("Done", item.GetProperty("status").GetString());
        Assert.Equal("Sửa tiêu đề", item.GetProperty("title").GetString());
        Assert.False(item.TryGetProperty("actualDurationSeconds", out _));
        Assert.False(item.TryGetProperty("plannedDurationMinutes", out _));
        Assert.Equal(item.GetProperty("actualStartedAt").GetDateTimeOffset(), item.GetProperty("actualEndedAt").GetDateTimeOffset());
        Assert.Equal("InProgress", snapshot.GetProperty("meetingStatus").GetString());
        Assert.Equal("Pending", snapshot.GetProperty("items")[1].GetProperty("status").GetString());
        var count = fixture.Events.Count;
        await AssertCode(await secretary.PutAsJsonAsync(s.Url + $"/{s.Items[0]}", Input("Sửa tiêu đề", presenter: s.Member, documents: [s.DocumentId])), 200, "MSG61");
        Assert.Equal(count, fixture.Events.Count);
    }

    [Fact]
    public async Task Ended_is_readonly_and_non_running_meeting_cannot_execute()
    {
        var ended = await fixture.Seed(MeetingStatus.Ended);
        using var secretary = fixture.Client(ended.Secretary);
        await AssertCode(await secretary.PostAsJsonAsync(ended.Url, Input()), 409, "MSG67");
        await AssertCode(await secretary.GetAsync(ended.Url + "/editor-options"), 409, "MSG67");
        using var chair = fixture.Client(ended.Chair);
        await AssertCode(await chair.PostAsync(ended.Url + $"/{ended.Items[0]}/current", null), 409, "MSG73");
        await AssertCode(await chair.PostAsync(ended.Url + $"/{ended.Items[0]}/complete", null), 409, "MSG74");
    }

    [Theory]
    [InlineData("create", "MSG68")]
    [InlineData("update", "MSG69")]
    [InlineData("delete", "MSG70")]
    [InlineData("order", "MSG71")]
    [InlineData("current", "MSG75")]
    [InlineData("complete", "MSG76")]
    public async Task Persistence_failures_rollback_and_emit_no_event(string action, string code)
    {
        var s = await fixture.Seed();
        if (action == "complete")
        {
            await using var db = fixture.Db();
            var item = await db.AgendaItems.SingleAsync(i => i.Id == s.Items[0]);
            item.Status = AgendaItemStatus.InProgress;
            item.ActualStartedAt = fixture.Clock.Now;
            await db.SaveChangesAsync();
        }
        using var client = fixture.Client(action is "current" or "complete" ? s.Chair : s.Secretary);
        var snapshot = await client.GetFromJsonAsync<JsonElement>(s.Url);
        var events = fixture.Events.Count;
        fixture.SaveFailure.Enabled = true;
        try
        {
            var response = action switch {
                "create" => await client.PostAsJsonAsync(s.Url, Input(documents: [s.DocumentId])),
                "update" => await client.PutAsJsonAsync(s.Url + $"/{s.Items[0]}", Input()),
                "delete" => await client.DeleteAsync(s.Url + $"/{s.Items[0]}"),
                "order" => await client.PutAsJsonAsync(s.Url + "/order", new { orderVersion = snapshot.GetProperty("orderVersion").GetString(), orderedItemIds = s.Items.Reverse().ToArray() }),
                _ => await client.PostAsync(s.Url + $"/{s.Items[0]}/{action}", null) };
            await AssertCode(response, 503, code);
        }
        finally { fixture.SaveFailure.Enabled = false; }
        Assert.Equal(events, fixture.Events.Count);
        await using var check = fixture.Db();
        Assert.Equal(3, await check.AgendaItems.CountAsync(i => i.MeetingId == s.MeetingId));
        Assert.Null(await AgendaFixture.LinkedItem(check, s.DocumentId));
    }

    [Fact]
    public async Task Read_failure_and_push_failure_have_different_commit_semantics()
    {
        var s = await fixture.Seed();
        using var client = fixture.Client(s.Secretary);
        fixture.FailRead = true;
        try { await AssertCode(await client.GetAsync(s.Url), 500, "MSG128"); }
        finally { fixture.FailRead = false; }
        fixture.FailPush = true;
        try { await AssertCode(await client.PostAsJsonAsync(s.Url, Input()), 201, "MSG60"); }
        finally { fixture.FailPush = false; }
        await using var db = fixture.Db();
        Assert.Equal(4, await db.AgendaItems.CountAsync(i => i.MeetingId == s.MeetingId));
    }
}
