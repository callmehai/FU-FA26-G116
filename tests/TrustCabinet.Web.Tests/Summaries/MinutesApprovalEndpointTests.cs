using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using TrustCabinet.Domain.Enums;
using static TrustCabinet.Web.Tests.Agenda.AgendaEndpointTests;

namespace TrustCabinet.Web.Tests.Summaries;

/// <summary>
/// SCRUM-33 — submit (UC-61), approve and issue (UC-64), return (UC-65) and correct (UC-83) the minutes, and what a reader
/// of the meeting sees of them (UC-58, BR-412): POST /api/minutes/{id}/submit|approve|reject|corrections, GET /api/minutes/{id}.
/// </summary>
public sealed class MinutesApprovalEndpointTests(SummaryFixture f) : IClassFixture<SummaryFixture>
{
    private static async Task<JsonElement> Build(HttpClient client, SummaryFixture.Scenario s)
        => (await AssertCode(await client.PostAsJsonAsync(s.Minutes, new { rebuild = false }), 201, "MSG395")).GetProperty("minutes");

    private static Task<HttpResponseMessage> Act(HttpClient client, Guid id, string action, string? updatedAt)
        => client.PostAsJsonAsync($"/api/minutes/{id}/{action}", new { updatedAt });

    private static Task<HttpResponseMessage> Return(HttpClient client, Guid id, string? reason, string? updatedAt)
        => client.PostAsJsonAsync($"/api/minutes/{id}/reject", new { reason, updatedAt });

    private static Task<HttpResponseMessage> Correct(HttpClient client, Guid id)
        => client.PostAsync($"/api/minutes/{id}/corrections", null);

    private static Task<JsonElement> State(HttpClient client, SummaryFixture.Scenario s)
        => client.GetFromJsonAsync<JsonElement>(s.Minutes);

    private static string Stamp(JsonElement minutes) => minutes.GetProperty("updatedAt").GetString()!;

    private static Guid Id(JsonElement minutes) => minutes.GetProperty("id").GetGuid();

    private static string[] History(JsonElement state) => state.GetProperty("history").EnumerateArray()
        .Select(r => $"{r.GetProperty("revisionNumber").GetInt32()}:{r.GetProperty("status").GetString()}").ToArray();

    private void Tick() => f.Clock.Now = f.Clock.Now.AddMinutes(1);

    private async Task<Guid[]> Notified(Guid meetingId, NotificationType type)
    {
        await using var db = f.Db();
        return await db.Notifications.Where(n => n.MeetingId == meetingId && n.Type == type)
            .Select(n => n.UserId).OrderBy(id => id).ToArrayAsync();
    }

    /// <summary>An ended meeting whose minutes the Secretary has built, sent and the Chair has issued.</summary>
    private async Task<(SummaryFixture.Scenario S, JsonElement Issued)> IssuedMinutes(Sensitivity sensitivity = Sensitivity.Normal)
    {
        var s = await f.Seed(MeetingStatus.Ended, sensitivity);
        using var secretary = f.Client(s.Secretary);
        using var chair = f.Client(s.Chair);
        var draft = await Build(secretary, s);
        Tick();
        var sent = await AssertCode(await Act(secretary, Id(draft), "submit", Stamp(draft)), 200, "MSG424");
        Tick();
        var issued = await AssertCode(await Act(chair, Id(draft), "approve", Stamp(sent.GetProperty("minutes"))), 200, "MSG427");
        return (s, issued.GetProperty("minutes"));
    }

    [Fact]
    public async Task Secretary_submits_and_the_chair_issues_which_everyone_reading_the_meeting_then_sees()
    {
        var s = await f.Seed(MeetingStatus.Ended);
        using var secretary = f.Client(s.Secretary);
        using var chair = f.Client(s.Chair);
        using var member = f.Client(s.Member);
        using var organizer = f.Client(s.Organizer);
        var draft = await Build(secretary, s);
        Tick();
        // The screen learns from the server which officer the caller is: the Secretary sends, the Chair approves.
        foreach (var (client, submit, approve) in new[] { (secretary, true, false), (chair, false, true), (member, false, false) })
        {
            var flags = await State(client, s);
            Assert.Equal(submit, flags.GetProperty("canSubmit").GetBoolean());
            Assert.Equal(approve, flags.GetProperty("canApprove").GetBoolean());
        }

        var sent = await AssertCode(await Act(secretary, Id(draft), "submit", Stamp(draft)), 200, "MSG424");
        Assert.True(sent.GetProperty("canSubmit").GetBoolean());
        var waiting = sent.GetProperty("minutes");
        Assert.Equal("WaitingApproval", waiting.GetProperty("status").GetString());
        Assert.Equal(s.Secretary, waiting.GetProperty("submittedBy").GetProperty("id").GetGuid());
        Assert.Equal(f.Clock.Now, waiting.GetProperty("submittedAt").GetDateTimeOffset());
        Assert.Equal([s.Chair], await Notified(s.MeetingId, NotificationType.MinutesWaitingApproval));
        // Waiting for approval is still the two officers' alone: no text, no history line, no file.
        var hidden = await State(member, s);
        Assert.Equal(JsonValueKind.Null, hidden.GetProperty("minutes").ValueKind);
        Assert.Empty(History(hidden));
        Assert.Equal(HttpStatusCode.NotFound, (await member.GetAsync($"/api/minutes/{Id(draft)}")).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await member.GetAsync($"/api/minutes/{Id(draft)}/export")).StatusCode);
        // Locked once sent (BR-388).
        await AssertCode(await secretary.PutAsJsonAsync($"/api/minutes/{Id(draft)}", new { content = "<p>Sửa.</p>", updatedAt = Stamp(waiting) }),
            409, "MSG400");
        await AssertCode(await Act(secretary, Id(draft), "submit", Stamp(waiting)), 409, "MSG400");

        Tick();
        var issued = (await AssertCode(await Act(chair, Id(draft), "approve", Stamp(waiting)), 200, "MSG427")).GetProperty("minutes");
        Assert.Equal("Issued", issued.GetProperty("status").GetString());
        Assert.Equal(s.Chair, issued.GetProperty("approvedBy").GetProperty("id").GetGuid());
        Assert.Equal(f.Clock.Now, issued.GetProperty("issuedAt").GetDateTimeOffset());
        await using (var db = f.Db())
        {
            // The meeting keeps Ended: being issued is the minutes' state (BR-408).
            Assert.Equal(MeetingStatus.Ended, (await db.Meetings.SingleAsync(m => m.Id == s.MeetingId)).Status);
            var row = await db.MeetingMinutes.SingleAsync(m => m.Id == Id(draft));
            Assert.Equal(MinutesStatus.Issued, row.Status);
            Assert.Equal(s.Secretary, row.SubmittedById);
        }
        // Everyone who may read the meeting is told, never the Chair who issued it (BR-411); the outsider is not.
        Assert.Equal(new[] { s.Secretary, s.Member, s.Organizer }.Order().ToArray(),
            await Notified(s.MeetingId, NotificationType.MinutesIssued));

        foreach (var reader in new[] { member, organizer })
        {
            var seen = await State(reader, s);
            Assert.False(seen.GetProperty("canManage").GetBoolean());
            Assert.Equal("Issued", seen.GetProperty("minutes").GetProperty("status").GetString());
            Assert.Equal(["1:Issued"], History(seen));
            // A reader sees who issued it, not the officers' working facts.
            Assert.Equal(JsonValueKind.Null, seen.GetProperty("minutes").GetProperty("submittedBy").ValueKind);
            Assert.Equal(s.Chair, seen.GetProperty("minutes").GetProperty("approvedBy").GetProperty("id").GetGuid());
        }
        // PO 08/11/2026 (Q7): issued minutes download for every reader, in Word and in PDF.
        var word = await member.GetAsync($"/api/minutes/{Id(draft)}/export");
        Assert.Equal(HttpStatusCode.OK, word.StatusCode);
        Assert.Equal("PK"u8.ToArray(), (await word.Content.ReadAsByteArrayAsync())[..2]);
        Assert.Equal(HttpStatusCode.OK, (await member.GetAsync($"/api/minutes/{Id(draft)}/export?format=pdf")).StatusCode);
        // Someone who may not read the meeting learns nothing.
        using var outsider = f.Client(s.Outsider);
        Assert.Equal(HttpStatusCode.NotFound, (await outsider.GetAsync(s.Minutes)).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await outsider.GetAsync($"/api/minutes/{Id(draft)}/export")).StatusCode);
    }

    [Fact]
    public async Task Only_the_secretary_submits_and_only_once_the_meeting_has_ended()
    {
        var s = await f.Seed();
        using var secretary = f.Client(s.Secretary);
        using var chair = f.Client(s.Chair);
        using var member = f.Client(s.Member);
        using var outsider = f.Client(s.Outsider);
        var draft = await Build(secretary, s);

        // The Chair is told it is the Secretary's to send; anyone else is answered as if there were nothing (EX-1, EX-6).
        await AssertCode(await Act(chair, Id(draft), "submit", Stamp(draft)), 403, "MSG425");
        Assert.Equal(HttpStatusCode.NotFound, (await Act(member, Id(draft), "submit", Stamp(draft))).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await Act(outsider, Id(draft), "submit", Stamp(draft))).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await Act(secretary, Guid.NewGuid(), "submit", Stamp(draft))).StatusCode);
        // Still InProgress (EX-2).
        await AssertCode(await Act(secretary, Id(draft), "submit", Stamp(draft)), 409, "MSG426");

        await using (var db = f.Db())
            await db.Meetings.Where(m => m.Id == s.MeetingId).ExecuteUpdateAsync(u => u.SetProperty(m => m.Status, MeetingStatus.Ended));
        // A copy older than the stored one is refused (EX-4, BR-389).
        Tick();
        var saved = (await AssertCode(await secretary.PutAsJsonAsync($"/api/minutes/{Id(draft)}",
            new { content = "<p>Bản cuối.</p>", updatedAt = Stamp(draft) }), 200, "MSG396")).GetProperty("minutes");
        await AssertCode(await Act(secretary, Id(draft), "submit", Stamp(draft)), 409, "MSG401");
        await AssertCode(await Act(secretary, Id(draft), "submit", null), 409, "MSG401");

        // The save fails: nothing is sent and nobody is told (EX-5).
        f.Failures.FailSave = true;
        try
        {
            await AssertCode(await Act(secretary, Id(draft), "submit", Stamp(saved)), 503, "MSG404");
        }
        finally
        {
            f.Failures.FailSave = false;
        }
        await using (var db = f.Db())
            Assert.Equal(MinutesStatus.Draft, (await db.MeetingMinutes.SingleAsync(m => m.Id == Id(draft))).Status);
        Assert.Empty(await Notified(s.MeetingId, NotificationType.MinutesWaitingApproval));

        Tick();
        await AssertCode(await Act(secretary, Id(draft), "submit", Stamp(saved)), 200, "MSG424");
    }

    [Fact]
    public async Task Only_the_chair_approves_or_returns_and_only_what_is_waiting()
    {
        var s = await f.Seed(MeetingStatus.Ended);
        using var secretary = f.Client(s.Secretary);
        using var chair = f.Client(s.Chair);
        using var member = f.Client(s.Member);
        var draft = await Build(secretary, s);

        // A draft is not waiting for approval (UC-64 EX-2, UC-65 EX-2).
        await AssertCode(await Act(chair, Id(draft), "approve", Stamp(draft)), 409, "MSG429");
        await AssertCode(await Return(chair, Id(draft), "Bổ sung kết luận.", Stamp(draft)), 409, "MSG429");

        Tick();
        var waiting = (await AssertCode(await Act(secretary, Id(draft), "submit", Stamp(draft)), 200, "MSG424")).GetProperty("minutes");
        // The Secretary is told it is the Chair's to approve or return; anyone else learns nothing (EX-1, EX-5).
        await AssertCode(await Act(secretary, Id(draft), "approve", Stamp(waiting)), 403, "MSG428");
        await AssertCode(await Return(secretary, Id(draft), "Tự trả lại.", Stamp(waiting)), 403, "MSG428");
        Assert.Equal(HttpStatusCode.NotFound, (await Act(member, Id(draft), "approve", Stamp(waiting))).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await Return(member, Id(draft), "Không đồng ý.", Stamp(waiting))).StatusCode);
        // The copy the Chair read is not the stored one (UC-64 EX-3, UC-65 EX-6).
        await AssertCode(await Act(chair, Id(draft), "approve", Stamp(draft)), 409, "MSG401");
        await AssertCode(await Return(chair, Id(draft), "Bổ sung kết luận.", Stamp(draft)), 409, "MSG401");
        // The reason is required and at most 2,000 characters (UC-65 EX-3).
        await AssertCode(await Return(chair, Id(draft), "   ", Stamp(waiting)), 400, "MSG431");
        await AssertCode(await Return(chair, Id(draft), null, Stamp(waiting)), 400, "MSG431");
        await AssertCode(await Return(chair, Id(draft), new string('x', 2001), Stamp(waiting)), 400, "MSG431");

        Tick();
        var returned = (await AssertCode(await Return(chair, Id(draft), "  Bổ sung kết luận mục 2.  ", Stamp(waiting)), 200, "MSG430"))
            .GetProperty("minutes");
        Assert.Equal("Rejected", returned.GetProperty("status").GetString());
        Assert.Equal("Bổ sung kết luận mục 2.", returned.GetProperty("rejectionReason").GetString());
        Assert.Equal(s.Chair, returned.GetProperty("returnedBy").GetProperty("id").GetGuid());
        Assert.Equal([s.Secretary], await Notified(s.MeetingId, NotificationType.MinutesReturned));
        Assert.Empty(await Notified(s.MeetingId, NotificationType.MinutesIssued));
        // Returned minutes stay the officers' alone, and are corrected and sent again (BR-407).
        Assert.Equal(JsonValueKind.Null, (await State(member, s)).GetProperty("minutes").ValueKind);
        var seen = (await State(secretary, s)).GetProperty("minutes");
        Assert.Equal("Bổ sung kết luận mục 2.", seen.GetProperty("rejectionReason").GetString());
        Tick();
        var corrected = (await AssertCode(await secretary.PutAsJsonAsync($"/api/minutes/{Id(draft)}",
            new { content = "<p>Đã bổ sung kết luận.</p>", updatedAt = Stamp(returned) }), 200, "MSG396")).GetProperty("minutes");
        Assert.Equal("Rejected", corrected.GetProperty("status").GetString());
        Tick();
        var again = (await AssertCode(await Act(secretary, Id(draft), "submit", Stamp(corrected)), 200, "MSG424")).GetProperty("minutes");
        Tick();
        await AssertCode(await Act(chair, Id(draft), "approve", Stamp(again)), 200, "MSG427");
        await AssertCode(await Act(chair, Id(draft), "approve", Stamp(again)), 409, "MSG429");
    }

    [Fact]
    public async Task A_correction_is_a_new_revision_that_replaces_the_issued_one_only_once_issued()
    {
        var (s, issued) = await IssuedMinutes();
        using var secretary = f.Client(s.Secretary);
        using var chair = f.Client(s.Chair);
        using var member = f.Client(s.Member);
        var first = Id(issued);

        // A reader may read the issued text but not write it (UC-83 EX-1).
        await AssertCode(await Correct(member, first), 403, "MSG397");
        Tick();
        var created = await AssertCode(await Correct(chair, first), 201, "MSG432");
        var correction = created.GetProperty("minutes");
        var second = Id(correction);
        Assert.Equal(2, correction.GetProperty("revisionNumber").GetInt32());
        Assert.Equal("Draft", correction.GetProperty("status").GetString());
        Assert.Equal(first, correction.GetProperty("supersedesId").GetGuid());
        Assert.Equal(issued.GetProperty("content").GetString(), correction.GetProperty("content").GetString());
        Assert.Equal(["2:Draft", "1:Issued"], History(created));
        // Nobody is told until the correction is issued (UC-83 POST-3).
        Assert.Single(await Notified(s.MeetingId, NotificationType.MinutesWaitingApproval));

        // One correction at a time (EX-3), and only of the issued revision (EX-2).
        await AssertCode(await Correct(secretary, first), 409, "MSG434");
        await AssertCode(await Correct(secretary, second), 409, "MSG433");
        // A correction is not rebuilt from the template (BR-413); asking without rebuild is the existing draft.
        await AssertCode(await secretary.PostAsJsonAsync(s.Minutes, new { rebuild = true }), 409, "MSG456");
        await AssertCode(await secretary.PostAsJsonAsync(s.Minutes, new { rebuild = false }), 409, "MSG399");
        // The issued revision is untouched and stays the one readers see; the correction is not there for them.
        await AssertCode(await secretary.PutAsJsonAsync($"/api/minutes/{first}", new { content = "<p>Sửa bản cũ.</p>", updatedAt = Stamp(issued) }),
            409, "MSG400");
        var reading = await State(member, s);
        Assert.Equal(first, Id(reading.GetProperty("minutes")));
        Assert.Equal(["1:Issued"], History(reading));
        Assert.Equal(HttpStatusCode.NotFound, (await member.GetAsync($"/api/minutes/{second}")).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await member.GetAsync($"/api/minutes/{second}/export")).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await Correct(member, second)).StatusCode);

        Tick();
        var edited = (await AssertCode(await secretary.PutAsJsonAsync($"/api/minutes/{second}",
            new { content = "<p>Đính chính: sửa tên dự án.</p>", updatedAt = Stamp(correction) }), 200, "MSG396")).GetProperty("minutes");
        Tick();
        var sent = (await AssertCode(await Act(secretary, second, "submit", Stamp(edited)), 200, "MSG424")).GetProperty("minutes");
        Tick();
        await AssertCode(await Act(chair, second, "approve", Stamp(sent)), 200, "MSG427");

        await using (var db = f.Db())
        {
            Assert.Equal(MinutesStatus.Superseded, (await db.MeetingMinutes.SingleAsync(m => m.Id == first)).Status);
            Assert.Equal(issued.GetProperty("content").GetString(), (await db.MeetingMinutes.SingleAsync(m => m.Id == first)).Content);
            Assert.Equal(MinutesStatus.Issued, (await db.MeetingMinutes.SingleAsync(m => m.Id == second)).Status);
        }
        var after = await State(member, s);
        Assert.Equal(second, Id(after.GetProperty("minutes")));
        Assert.Equal(["2:Issued", "1:Superseded"], History(after));
        // The superseded text stays readable, and downloadable, from the history (BR-409).
        var old = await AssertOk(await member.GetAsync($"/api/minutes/{first}"));
        Assert.Equal("Superseded", old.GetProperty("minutes").GetProperty("status").GetString());
        Assert.Equal(issued.GetProperty("content").GetString(), old.GetProperty("minutes").GetProperty("content").GetString());
        Assert.Equal(HttpStatusCode.OK, (await member.GetAsync($"/api/minutes/{first}/export")).StatusCode);
        // A superseded revision is not the issued one, so it is not corrected again.
        await AssertCode(await Correct(secretary, first), 409, "MSG433");
    }

    [Fact]
    public async Task Issued_minutes_of_a_secret_meeting_are_read_on_screen_only()
    {
        var (s, issued) = await IssuedMinutes(Sensitivity.Secret);
        using var member = f.Client(s.Member);
        using var secretary = f.Client(s.Secretary);
        Assert.Equal("Issued", (await State(member, s)).GetProperty("minutes").GetProperty("status").GetString());
        await AssertCode(await member.GetAsync($"/api/minutes/{Id(issued)}/export"), 403, "MSG405");
        await AssertCode(await secretary.GetAsync($"/api/minutes/{Id(issued)}/export?format=pdf"), 403, "MSG405");
    }

    private static async Task<JsonElement> AssertOk(HttpResponseMessage response)
    {
        var raw = await response.Content.ReadAsStringAsync();
        Assert.True(response.StatusCode == HttpStatusCode.OK, $"Expected 200, got {(int)response.StatusCode}: {raw}");
        return JsonDocument.Parse(raw).RootElement.Clone();
    }
}
