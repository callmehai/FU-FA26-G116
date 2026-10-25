using System.Net;
using System.Net.Http.Json;
using System.Text;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using TrustCabinet.Domain.Agenda;
using TrustCabinet.Domain.Entities;
using TrustCabinet.Domain.Enums;
using TrustCabinet.Domain.Participants;
using TrustCabinet.Infrastructure.Agenda;
using static TrustCabinet.Web.Tests.Agenda.AgendaEndpointTests;

namespace TrustCabinet.Web.Tests.Agenda;

public sealed class AgendaPoRequirementsTests(AgendaFixture fixture) : IClassFixture<AgendaFixture>
{
    internal static MeetingGuestInvitation Guest(AgendaFixture.Scenario s) => new()
    {
        MeetingId = s.MeetingId, InvitedName = "Khách trình bày", Email = $"{Guid.NewGuid():N}@agenda.test",
        InvitedById = s.Secretary, Response = InvitationResponse.Declined
    };

    [Theory]
    [InlineData(AgendaItemStatus.Pending)]
    [InlineData(AgendaItemStatus.InProgress)]
    [InlineData(AgendaItemStatus.Done)]
    public async Task Every_item_status_accepts_presenter_and_shared_document_changes(AgendaItemStatus status)
    {
        var s = await fixture.Seed();
        var guest = Guest(s);
        await using (var db = fixture.Db())
        {
            db.MeetingGuestInvitations.Add(guest);
            var item = await db.AgendaItems.SingleAsync(i => i.Id == s.Items[0]);
            item.Status = status;
            item.ActualStartedAt = status == AgendaItemStatus.Pending ? null : fixture.Clock.Now.AddMinutes(-1);
            item.ActualEndedAt = status == AgendaItemStatus.Done ? fixture.Clock.Now : null;
            await db.SaveChangesAsync();
        }
        using var client = fixture.Client(s.Secretary);
        var input = new { title = "New", presenterGuestInvitationId = guest.Id, documentIds = new[] { s.DocumentId } };
        foreach (var id in s.Items.Take(2))
            await AssertCode(await client.PutAsJsonAsync(s.Url + $"/{id}", input), 200, "MSG61");
        var snapshot = await client.GetFromJsonAsync<JsonElement>(s.Url);
        foreach (var item in snapshot.GetProperty("items").EnumerateArray().Take(2))
        {
            Assert.Equal(guest.Id, item.GetProperty("presenterGuestInvitationId").GetGuid());
            Assert.Equal(guest.InvitedName, item.GetProperty("presenterName").GetString());
            Assert.Single(item.GetProperty("documents").EnumerateArray());
            Assert.True(item.GetProperty("actions").GetProperty("canEditPlanningFields").GetBoolean());
        }
        var options = await client.GetFromJsonAsync<JsonElement>(s.Url + "/editor-options");
        Assert.Contains(options.GetProperty("presenters").EnumerateArray(), p => p.GetProperty("kind").GetString() == "Guest" && p.GetProperty("id").GetGuid() == guest.Id);
        Assert.Equal(2, Assert.Single(options.GetProperty("documents").EnumerateArray()).GetProperty("agendaItemIds").GetArrayLength());
        await AssertCode(await client.PutAsJsonAsync(s.Url + $"/{s.Items[0]}", new { title = "Only title" }), 200, "MSG61");
        await using var check = fixture.Db();
        var updated = await check.AgendaItems.SingleAsync(i => i.Id == s.Items[0]);
        Assert.Equal(status, updated.Status);
        Assert.Equal(status == AgendaItemStatus.Pending ? (DateTimeOffset?)null : fixture.Clock.Now.AddMinutes(-1), updated.ActualStartedAt);
        Assert.Equal(status == AgendaItemStatus.Done ? (DateTimeOffset?)fixture.Clock.Now : null, updated.ActualEndedAt);
        Assert.Null(updated.PresenterGuestInvitationId);
        Assert.Equal(s.Items[1], await AgendaFixture.LinkedItem(check, s.DocumentId));
        await AssertCode(await client.DeleteAsync(s.Url + $"/{s.Items[1]}"), 200, "MSG62");
        Assert.True(await check.MeetingDocuments.AnyAsync(d => d.Id == s.DocumentId));
        var fileId = await check.MeetingDocuments.Where(d => d.Id == s.DocumentId).Select(d => d.StoredFileId).SingleAsync();
        Assert.True(await check.StoredFiles.AnyAsync(f => f.Id == fileId));
    }

    [Theory]
    [InlineData(MeetingStatus.InProgress, AgendaItemStatus.InProgress, false, 200)]
    [InlineData(MeetingStatus.InProgress, AgendaItemStatus.Done, false, 200)]
    [InlineData(MeetingStatus.Ended, AgendaItemStatus.Done, false, 200)]
    [InlineData(MeetingStatus.InProgress, AgendaItemStatus.Pending, false, 409)]
    [InlineData(MeetingStatus.Published, AgendaItemStatus.Done, false, 409)]
    [InlineData(MeetingStatus.Ended, AgendaItemStatus.Done, true, 409)]
    public async Task Conclusion_gate_and_metadata_are_server_authoritative(MeetingStatus meetingStatus, AgendaItemStatus status, bool minutes, int expected)
    {
        var s = await fixture.Seed(meetingStatus);
        await using (var db = fixture.Db())
        {
            var item = await db.AgendaItems.SingleAsync(i => i.Id == s.Items[0]);
            item.Status = status;
            if (minutes) db.MeetingMinutes.Add(new() { MeetingId = s.MeetingId, CreatedById = s.Secretary });
            await db.SaveChangesAsync();
        }
        foreach (var actor in new[] { s.Chair, s.Secretary })
        {
            using var client = fixture.Client(actor);
            var snapshot = await client.GetFromJsonAsync<JsonElement>(s.Url);
            Assert.Equal(expected == 200, snapshot.GetProperty("items")[0].GetProperty("actions").GetProperty("canRecordConclusion").GetBoolean());
            await AssertCode(await client.PutAsJsonAsync(s.Url + $"/{s.Items[0]}/conclusion", new { conclusion = "  Thống nhất\nphương án.  " }), expected, expected == 200 ? "MSG271" : "MSG273");
            await using var check = fixture.Db();
            var item = await check.AgendaItems.SingleAsync(i => i.Id == s.Items[0]);
            if (expected == 200)
            {
                Assert.Equal("Thống nhất\nphương án.", item.Conclusion);
                Assert.Equal(actor, item.ConclusionUpdatedById);
                Assert.Equal(fixture.Clock.Now, item.ConclusionUpdatedAt);
                Assert.Contains(fixture.Events, e => e.MeetingId == s.MeetingId && e.ChangeType == "ConclusionUpdated");
            }
            else Assert.Null(item.Conclusion);
            Assert.Equal(status, item.Status);
        }
    }

    [Theory]
    [InlineData("{}")]
    [InlineData("{\"conclusion\":null}")]
    [InlineData("{\"conclusion\":\"  \"}")]
    [InlineData("{\"conclusion\":3}")]
    [InlineData("{\"Conclusion\":\"X\"}")]
    [InlineData("{\"conclusion\":\"X\",\"conclusion\":\"Y\"}")]
    [InlineData("{\"conclusion\":\"X\",\"conclusionUpdatedById\":null}")]
    [InlineData("{\"conclusion\":\"X\",\"title\":\"Y\"}")]
    [InlineData("{")]
    public async Task Conclusion_parser_rejects_invalid_or_forged_fields(string json)
    {
        var s = await Started();
        using var client = fixture.Client(s.Chair);
        await AssertCode(await client.PutAsync(s.Url + $"/{s.Items[0]}/conclusion", new StringContent(json, Encoding.UTF8, "application/json")), 400, "MSG272");
        await using var db = fixture.Db();
        Assert.Null((await db.AgendaItems.SingleAsync(i => i.Id == s.Items[0])).Conclusion);
    }

    [Fact]
    public async Task Conclusion_lengths_failures_permissions_and_not_found_contract()
    {
        var s = await Started();
        var url = s.Url + $"/{s.Items[0]}/conclusion";
        using var chair = fixture.Client(s.Chair);
        foreach (var value in new[] { "A", new string('x', 2000), string.Concat(Enumerable.Repeat("😀", 1000)) })
            await AssertCode(await chair.PutAsJsonAsync(url, new { conclusion = " " + value + " " }), 200, "MSG271");
        await AssertCode(await chair.PutAsJsonAsync(url, new { conclusion = new string('x', 2001) }), 400, "MSG272");
        await AssertCode(await chair.PutAsync(url, new StringContent("{}")), 415, "MSG34");
        await AssertCode(await chair.PutAsync(url, new StringContent(new string(' ', 65537), Encoding.UTF8, "application/json")), 413, "MSG87");
        using var member = fixture.Client(s.Member);
        await AssertCode(await member.PutAsJsonAsync(url, new { conclusion = "X" }), 403, "MSG273");
        using var organizer = fixture.Client(s.Organizer);
        await AssertCode(await organizer.PutAsJsonAsync(url, new { conclusion = "X" }), 403, "MSG273");
        using var anonymous = fixture.Client();
        Assert.Equal(HttpStatusCode.Unauthorized, (await anonymous.PutAsJsonAsync(url, new { conclusion = "X" })).StatusCode);
        using var outsider = fixture.Client(s.Outsider);
        var hidden = await AssertCode(await outsider.PutAsJsonAsync(url, new { conclusion = "X" }), 404, "MSG174");
        var missing = await AssertCode(await outsider.PutAsJsonAsync($"/api/meetings/{Guid.NewGuid()}/agenda/{Guid.NewGuid()}/conclusion", new { conclusion = "X" }), 404, "MSG174");
        Assert.Equal(hidden.GetRawText(), missing.GetRawText());
        var events = fixture.Events.Count;
        fixture.SaveFailure.Enabled = true;
        try { await AssertCode(await chair.PutAsJsonAsync(url, new { conclusion = "Failure" }), 503, "MSG274"); }
        finally { fixture.SaveFailure.Enabled = false; }
        await using var check = fixture.Db();
        Assert.NotEqual("Failure", (await check.AgendaItems.SingleAsync(i => i.Id == s.Items[0])).Conclusion);
        Assert.Equal(events, fixture.Events.Count);
    }

    [Fact]
    public async Task Any_pending_can_start_and_legacy_names_are_not_assignments()
    {
        var s = await fixture.Seed();
        await using (var db = fixture.Db())
        {
            var item = await db.AgendaItems.SingleAsync(i => i.Id == s.Items[2]);
            item.LegacyPresenterName = "Historical name";
            await db.SaveChangesAsync();
        }
        using var chair = fixture.Client(s.Chair);
        var before = await chair.GetFromJsonAsync<JsonElement>(s.Url);
        Assert.All(before.GetProperty("items").EnumerateArray(), i => Assert.True(i.GetProperty("actions").GetProperty("canSetCurrent").GetBoolean()));
        await AssertCode(await chair.PostAsync(s.Url + $"/{s.Items[2]}/current", null), 200, "MSG64");
        var itemView = (await chair.GetFromJsonAsync<JsonElement>(s.Url)).GetProperty("items")[2];
        Assert.Equal("Historical name", itemView.GetProperty("legacyPresenterName").GetString());
        Assert.Equal(JsonValueKind.Null, itemView.GetProperty("presenterId").ValueKind);
        Assert.Equal(JsonValueKind.Null, itemView.GetProperty("presenterName").ValueKind);
        using var secretary = fixture.Client(s.Secretary);
        await AssertCode(await secretary.PutAsJsonAsync(s.Url + $"/{s.Items[2]}", new { title = "X" }), 200, "MSG61");
        await using var check = fixture.Db();
        Assert.Empty((await check.AgendaItems.SingleAsync(i => i.Id == s.Items[2])).LegacyPresenterName);
        Assert.Empty(await check.Notifications.Where(n => n.MeetingId == s.MeetingId).ToListAsync());
    }

    [Theory]
    [InlineData(MeetingStatus.Draft, false)]
    [InlineData(MeetingStatus.Approved, false)]
    [InlineData(MeetingStatus.Published, true)]
    [InlineData(MeetingStatus.InProgress, true)]
    public async Task Presenter_assignment_uses_correct_channel_and_only_identity_changes_notify(MeetingStatus status, bool notify)
    {
        var s = await fixture.Seed(status);
        var guest = Guest(s);
        await using (var db = fixture.Db()) { db.Add(guest); await db.SaveChangesAsync(); }
        using var client = fixture.Client(s.Secretary);
        var url = s.Url + $"/{s.Items[0]}";
        await AssertCode(await client.PutAsJsonAsync(url, Input(presenter: s.Member)), 200, "MSG61");
        await AssertCode(await client.PutAsJsonAsync(url, Input("Title change", presenter: s.Member)), 200, "MSG61");
        await AssertCode(await client.PutAsJsonAsync(url, new { title = "Guest", presenterGuestInvitationId = guest.Id }), 200, "MSG61");
        await AssertCode(await client.PutAsJsonAsync(url, Input(presenter: s.Member)), 200, "MSG61");
        await AssertCode(await client.PutAsJsonAsync(url, Input()), 200, "MSG61");
        await using var check = fixture.Db();
        Assert.Equal(notify ? 2 : 0, await check.Notifications.CountAsync(n => n.MeetingId == s.MeetingId && n.Type == NotificationType.AgendaPresenterAssigned));
        var emails = await check.EmailMessages.Where(e => e.MeetingId == s.MeetingId).ToListAsync();
        Assert.Equal(notify ? 1 : 0, emails.Count);
        var meetingTitle = await check.Meetings.Where(m => m.Id == s.MeetingId).Select(m => m.Title).SingleAsync();
        Assert.All(emails, e =>
        {
            Assert.Equal(guest.Email, e.ToAddress); Assert.Null(e.CalendarAttachment); Assert.Equal(EmailStatus.Queued, e.Status);
            // The guest learns which meeting, not just which item.
            Assert.Contains(meetingTitle, e.Body);
        });
    }

    [Fact]
    public async Task A_secretary_who_assigns_themselves_is_not_notified()
    {
        var s = await fixture.Seed(MeetingStatus.Published);
        using var client = fixture.Client(s.Secretary);
        await AssertCode(await client.PutAsJsonAsync(s.Url + $"/{s.Items[0]}", Input(presenter: s.Secretary)), 200, "MSG61");
        await using var check = fixture.Db();
        Assert.Equal(s.Secretary, (await check.AgendaItems.SingleAsync(i => i.Id == s.Items[0])).PresenterId);
        Assert.False(await check.Notifications.AnyAsync(n => n.MeetingId == s.MeetingId && n.Type == NotificationType.AgendaPresenterAssigned));
    }

    [Fact]
    public async Task Notifier_deduplicates_rechecks_recipient_and_failure_does_not_undo_commit()
    {
        var s = await fixture.Seed();
        await using var scope = fixture.Host.Services.CreateAsyncScope();
        var service = ActivatorUtilities.CreateInstance<AgendaService>(scope.ServiceProvider, new FailingNotifier());
        Assert.Equal(AgendaFailure.None, (await service.UpdateAsync(s.MeetingId, s.Items[0], s.Secretary, new("Assigned", PresenterId: s.Member))).Failure);
        var notifier = scope.ServiceProvider.GetRequiredService<IAgendaPresenterNotifier>();
        var assignment = new AgendaPresenterAssignment(Guid.NewGuid(), s.MeetingId, s.Items[0], s.Member, null, fixture.Clock.Now);
        await Task.WhenAll(notifier.NotifyAsync(assignment, default), notifier.NotifyAsync(assignment, default));
        await using var db = fixture.Db();
        Assert.Single(await db.Notifications.Where(n => n.MeetingId == s.MeetingId).ToListAsync());
        (await db.Users.SingleAsync(u => u.Id == s.Member)).IsActive = false;
        await db.SaveChangesAsync();
        await notifier.NotifyAsync(assignment with { EventId = Guid.NewGuid() }, default);
        Assert.Single(await db.Notifications.Where(n => n.MeetingId == s.MeetingId).ToListAsync());
        Assert.Equal(s.Member, (await db.AgendaItems.SingleAsync(i => i.Id == s.Items[0])).PresenterId);
    }

    private sealed class FailingNotifier : IAgendaPresenterNotifier
    {
        public Task NotifyAsync(AgendaPresenterAssignment assignment, CancellationToken ct) => throw new IOException("Injected enqueue outage");
    }

    [Theory]
    [InlineData("foreign")]
    [InlineData("revoked")]
    [InlineData("missing")]
    public async Task Invalid_guest_reference_rolls_back_information_and_links(string kind)
    {
        var s = await fixture.Seed();
        var other = await fixture.Seed();
        var guest = Guest(kind == "foreign" ? other : s);
        await using (var db = fixture.Db())
        {
            if (kind == "revoked") { guest.RevokedAt = fixture.Clock.Now; guest.RevokedById = s.Secretary; }
            if (kind != "missing") { db.Add(guest); await db.SaveChangesAsync(); }
        }
        using var client = fixture.Client(s.Secretary);
        await AssertCode(await client.PutAsJsonAsync(s.Url + $"/{s.Items[0]}", new
        {
            title = "Must roll back", presenterGuestInvitationId = guest.Id, documentIds = new[] { s.DocumentId }
        }), 400, "MSG66");
        await using var check = fixture.Db();
        Assert.Equal("Mục 1", (await check.AgendaItems.SingleAsync(i => i.Id == s.Items[0])).Title);
        Assert.Null(await AgendaFixture.LinkedItem(check, s.DocumentId));
        Assert.Empty(await check.EmailMessages.Where(e => e.MeetingId == s.MeetingId).ToListAsync());
    }

    [Fact]
    public async Task Reorder_requires_two_items_and_rejects_legacy_body()
    {
        var s = await fixture.Seed(count: 1);
        using var client = fixture.Client(s.Secretary);
        var snapshot = await client.GetFromJsonAsync<JsonElement>(s.Url);
        Assert.False(snapshot.GetProperty("permissions").GetProperty("canReorder").GetBoolean());
        var token = snapshot.GetProperty("orderVersion").GetString();
        await AssertCode(await client.PutAsJsonAsync(s.Url + "/order", new { orderVersion = token, orderedItemIds = s.Items }), 400, "MSG143");
        await AssertCode(await client.PutAsJsonAsync(s.Url + "/order", new { orderVersion = token, pendingItemIds = s.Items }), 400, "MSG143");
    }

    [Fact]
    public async Task Every_minutes_status_keeps_conclusion_locked_even_with_empty_content()
    {
        var s = await Started();
        using var client = fixture.Client(s.Secretary);
        await using var db = fixture.Db();
        var minutes = new MeetingMinutes { MeetingId = s.MeetingId, CreatedById = s.Secretary, Content = "" };
        db.Add(minutes);
        foreach (var status in Enum.GetValues<MinutesStatus>())
        {
            minutes.Status = status; await db.SaveChangesAsync();
            await AssertCode(await client.PutAsJsonAsync(s.Url + $"/{s.Items[0]}/conclusion", new { conclusion = "Locked" }), 409, "MSG273");
        }
        Assert.Null((await db.AgendaItems.SingleAsync(i => i.Id == s.Items[0])).Conclusion);
    }

    private async Task<AgendaFixture.Scenario> Started()
    {
        var s = await fixture.Seed();
        using var chair = fixture.Client(s.Chair);
        await AssertCode(await chair.PostAsync(s.Url + $"/{s.Items[0]}/current", null), 200, "MSG64");
        return s;
    }
}
