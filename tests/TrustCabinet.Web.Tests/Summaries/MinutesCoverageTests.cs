using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using TrustCabinet.Domain.Audit;
using TrustCabinet.Domain.Enums;
using static TrustCabinet.Web.Tests.Agenda.AgendaEndpointTests;

namespace TrustCabinet.Web.Tests.Summaries;

/// <summary>Round 1 Report 5.2.2 FE-12: the minutes cases that had no test yet,
/// plus the content the issued/returned/waiting notifications and the issued
/// email carry that the older tests only counted.</summary>
public sealed class MinutesCoverageTests(SummaryFixture f) : IClassFixture<SummaryFixture>
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

    private static async Task<JsonElement> AssertOk(HttpResponseMessage response)
    {
        var raw = await response.Content.ReadAsStringAsync();
        Assert.True(response.StatusCode == HttpStatusCode.OK, $"Expected 200, got {(int)response.StatusCode}: {raw}");
        return JsonDocument.Parse(raw).RootElement.Clone();
    }

    private void Tick() => f.Clock.Now = f.Clock.Now.AddMinutes(1);

    private async Task<(SummaryFixture.Scenario S, JsonElement Issued)> IssuedMinutes()
    {
        var s = await f.Seed(MeetingStatus.Ended);
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
    public async Task Draft_state_tells_each_officer_what_they_may_do()
    {
        // The flags and the history around a draft. Before anything
        // is built the officers see an empty state; once built, the officers see the draft and its single history
        // line while a reader sees nothing; rebuilding replaces the text in place.
        var s = await f.Seed(MeetingStatus.Ended);
        using var secretary = f.Client(s.Secretary);
        using var chair = f.Client(s.Chair);
        using var member = f.Client(s.Member);

        var empty = await State(secretary, s);
        Assert.Equal(JsonValueKind.Null, empty.GetProperty("minutes").ValueKind);
        Assert.True(empty.GetProperty("canManage").GetBoolean());
        Assert.Equal(JsonValueKind.Null, empty.GetProperty("draftChangedAt").ValueKind);
        Assert.Empty(empty.GetProperty("history").EnumerateArray());
        Assert.True(empty.GetProperty("canSubmit").GetBoolean());
        Assert.False(empty.GetProperty("canApprove").GetBoolean());

        var draft = await Build(secretary, s);
        var id = Id(draft);
        foreach (var (client, submit, approve) in new[] { (secretary, true, false), (chair, false, true) })
        {
            var seen = await State(client, s);
            Assert.True(seen.GetProperty("canManage").GetBoolean());
            Assert.Equal(submit, seen.GetProperty("canSubmit").GetBoolean());
            Assert.Equal(approve, seen.GetProperty("canApprove").GetBoolean());
            Assert.Equal("Draft", seen.GetProperty("minutes").GetProperty("status").GetString());
            Assert.Equal(["1:Draft"], History(seen));
        }
        var hidden = await State(member, s);
        Assert.False(hidden.GetProperty("canManage").GetBoolean());
        Assert.False(hidden.GetProperty("canSubmit").GetBoolean());
        Assert.False(hidden.GetProperty("canApprove").GetBoolean());
        Assert.Equal(JsonValueKind.Null, hidden.GetProperty("minutes").ValueKind);
        Assert.Empty(hidden.GetProperty("history").EnumerateArray());
        Assert.Equal(JsonValueKind.Null, hidden.GetProperty("draftChangedAt").ValueKind);

        Tick();
        var rebuilt = await AssertCode(await secretary.PostAsJsonAsync(s.Minutes, new { rebuild = true }), 201, "MSG395");
        Assert.Equal(id, Id(rebuilt.GetProperty("minutes")));
        Assert.Equal(["1:Draft"], History(rebuilt));
    }

    private static string[] History(JsonElement state) => state.GetProperty("history").EnumerateArray()
        .Select(r => $"{r.GetProperty("revisionNumber").GetInt32()}:{r.GetProperty("status").GetString()}").ToArray();

    [Fact]
    public async Task Rebuilding_a_returned_revision_keeps_it_returned()
    {
        // UC-59, BR-407. A returned revision is edited again, so rebuilding it from the template
        // replaces the text in place without sending it anywhere.
        var s = await f.Seed(MeetingStatus.Ended);
        using var secretary = f.Client(s.Secretary);
        using var chair = f.Client(s.Chair);
        var draft = await Build(secretary, s);
        Tick();
        var sent = await AssertCode(await Act(secretary, Id(draft), "submit", Stamp(draft)), 200, "MSG424");
        Tick();
        var returned = (await AssertCode(await Return(chair, Id(draft), "Bổ sung kết luận mục 2.",
            Stamp(sent.GetProperty("minutes"))), 200, "MSG430")).GetProperty("minutes");

        Tick();
        var rebuilt = await AssertCode(await secretary.PostAsJsonAsync(s.Minutes, new { rebuild = true }), 201, "MSG395");
        var minutes = rebuilt.GetProperty("minutes");
        Assert.Equal(Id(returned), Id(minutes));
        Assert.Equal("Rejected", minutes.GetProperty("status").GetString());
        Assert.Equal("Bổ sung kết luận mục 2.", minutes.GetProperty("rejectionReason").GetString());
        Assert.Contains("<h1>BIÊN BẢN</h1>", minutes.GetProperty("content").GetString());

        await using var db = f.Db();
        var row = await db.MeetingMinutes.SingleAsync(m => m.Id == Id(returned));
        Assert.Equal(MinutesStatus.Rejected, row.Status);
        Assert.Equal("Bổ sung kết luận mục 2.", row.RejectionReason);
        Assert.Contains("<h1>BIÊN BẢN</h1>", row.Content);
    }

    [Fact]
    public async Task Deactivated_and_guest_callers_are_refused_the_minutes()
    {
        // UC-58, BR-412. A deactivated account and a Guest (UC-04) are refused with an empty 403:
        // the gate runs before the minutes are read.
        var (s, _) = await IssuedMinutes();
        await using (var db = f.Db())
            await db.Users.Where(u => u.Id == s.Member).ExecuteUpdateAsync(u => u.SetProperty(x => x.IsActive, false));

        using var deactivated = f.Client(s.Member);
        var refused = await deactivated.GetAsync(s.Minutes);
        Assert.Equal(HttpStatusCode.Forbidden, refused.StatusCode);
        Assert.Equal(string.Empty, await refused.Content.ReadAsStringAsync());

        using var guest = f.GuestClient();
        var guestRefused = await guest.GetAsync(s.Minutes);
        Assert.Equal(HttpStatusCode.Forbidden, guestRefused.StatusCode);
        Assert.Equal(string.Empty, await guestRefused.Content.ReadAsStringAsync());
    }

    [Fact]
    public async Task Chair_and_secretary_open_any_revision()
    {
        // UC-58, BR-386. Both officers read the correction in progress and the issued revision.
        var (s, issued) = await IssuedMinutes();
        using var secretary = f.Client(s.Secretary);
        using var chair = f.Client(s.Chair);
        Tick();
        var correction = (await AssertCode(await Correct(chair, Id(issued)), 201, "MSG432")).GetProperty("minutes");

        foreach (var officer in new[] { chair, secretary })
        {
            var draft = await AssertOk(await officer.GetAsync($"/api/minutes/{Id(correction)}"));
            Assert.Equal("Draft", draft.GetProperty("minutes").GetProperty("status").GetString());
            Assert.Equal(2, draft.GetProperty("minutes").GetProperty("revisionNumber").GetInt32());
            Assert.Equal(2, draft.GetProperty("history").GetArrayLength());

            var first = await AssertOk(await officer.GetAsync($"/api/minutes/{Id(issued)}"));
            Assert.Equal("Issued", first.GetProperty("minutes").GetProperty("status").GetString());
            Assert.Equal(1, first.GetProperty("minutes").GetProperty("revisionNumber").GetInt32());
        }
    }

    [Fact]
    public async Task Strangers_and_unknown_ids_learn_nothing_about_a_revision()
    {
        // UC-58 EX-3. An outsider asking for the issued revision, and an officer asking for a revision
        // that does not exist, both get an empty 404.
        var (s, issued) = await IssuedMinutes();
        using var outsider = f.Client(s.Outsider);
        var stranger = await outsider.GetAsync($"/api/minutes/{Id(issued)}");
        Assert.Equal(HttpStatusCode.NotFound, stranger.StatusCode);
        Assert.Equal(string.Empty, await stranger.Content.ReadAsStringAsync());

        using var secretary = f.Client(s.Secretary);
        var unknown = await secretary.GetAsync($"/api/minutes/{Guid.NewGuid()}");
        Assert.Equal(HttpStatusCode.NotFound, unknown.StatusCode);
        Assert.Equal(string.Empty, await unknown.Content.ReadAsStringAsync());
    }

    [Fact]
    public async Task Reading_a_revision_without_signing_in_is_refused()
    {
        // UC-58.
        var (s, issued) = await IssuedMinutes();
        using var anonymous = f.Client();
        Assert.Equal(HttpStatusCode.Unauthorized,
            (await anonymous.GetAsync($"/api/minutes/{Id(issued)}")).StatusCode);
    }

    [Fact]
    public async Task Submitting_without_signing_in_is_refused()
    {
        // UC-61.
        using var anonymous = f.Client();
        Assert.Equal(HttpStatusCode.Unauthorized,
            (await anonymous.PostAsync($"/api/minutes/{Guid.NewGuid()}/submit", null)).StatusCode);
    }

    [Fact]
    public async Task Approving_without_signing_in_is_refused()
    {
        // UC-64.
        using var anonymous = f.Client();
        Assert.Equal(HttpStatusCode.Unauthorized,
            (await anonymous.PostAsync($"/api/minutes/{Guid.NewGuid()}/approve", null)).StatusCode);
    }

    [Fact]
    public async Task Only_the_latest_return_reason_is_kept()
    {
        // UC-65, BR-407. Returning twice overwrites the first reason; the Secretary is told with the
        // exact title and body of the second return.
        var s = await f.Seed(MeetingStatus.Ended);
        using var secretary = f.Client(s.Secretary);
        using var chair = f.Client(s.Chair);
        var draft = await Build(secretary, s);
        Tick();
        var sent = await AssertCode(await Act(secretary, Id(draft), "submit", Stamp(draft)), 200, "MSG424");
        Tick();
        var returned = (await AssertCode(await Return(chair, Id(draft), "Bổ sung kết luận mục 2.",
            Stamp(sent.GetProperty("minutes"))), 200, "MSG430")).GetProperty("minutes");
        Tick();
        var corrected = (await AssertCode(await secretary.PutAsJsonAsync($"/api/minutes/{Id(draft)}",
            new { content = "<p>Đã bổ sung kết luận.</p>", updatedAt = Stamp(returned) }), 200, "MSG396"))
            .GetProperty("minutes");
        Tick();
        await AssertCode(await Act(secretary, Id(draft), "submit", Stamp(corrected)), 200, "MSG424");
        Tick();
        await AssertCode(await Return(chair, Id(draft), "Lần hai: sửa tên dự án.",
            (await State(secretary, s)).GetProperty("minutes").GetProperty("updatedAt").GetString()), 200, "MSG430");

        var seen = (await State(secretary, s)).GetProperty("minutes");
        Assert.Equal("Rejected", seen.GetProperty("status").GetString());
        Assert.Equal("Lần hai: sửa tên dự án.", seen.GetProperty("rejectionReason").GetString());

        await using var db = f.Db();
        var notice = await db.Notifications
            .Where(n => n.MeetingId == s.MeetingId && n.Type == NotificationType.MinutesReturned)
            .OrderByDescending(n => n.CreatedAt).FirstAsync();
        Assert.Equal(s.Secretary, notice.UserId);
        Assert.Equal("Biên bản bị trả lại", notice.Title);
        Assert.Contains("Xem lý do trên tab Biên bản.", notice.Body);
    }

    [Fact]
    public async Task Returning_without_signing_in_is_refused()
    {
        // UC-65.
        using var anonymous = f.Client();
        Assert.Equal(HttpStatusCode.Unauthorized,
            (await anonymous.PostAsync($"/api/minutes/{Guid.NewGuid()}/reject", null)).StatusCode);
    }

    [Fact]
    public async Task Correcting_without_signing_in_is_refused()
    {
        // UC-83.
        using var anonymous = f.Client();
        Assert.Equal(HttpStatusCode.Unauthorized,
            (await anonymous.PostAsync($"/api/minutes/{Guid.NewGuid()}/corrections", null)).StatusCode);
    }

    [Fact]
    public async Task A_two_thousand_character_return_reason_is_accepted()
    {
        // UC-65 EX-3. 2,001 characters are refused; exactly 2,000 are kept.
        var s = await f.Seed(MeetingStatus.Ended);
        using var secretary = f.Client(s.Secretary);
        using var chair = f.Client(s.Chair);
        var draft = await Build(secretary, s);
        Tick();
        var sent = await AssertCode(await Act(secretary, Id(draft), "submit", Stamp(draft)), 200, "MSG424");
        Tick();
        var returned = await AssertCode(await Return(chair, Id(draft), new string('x', 2000),
            Stamp(sent.GetProperty("minutes"))), 200, "MSG430");
        Assert.Equal(new string('x', 2000),
            returned.GetProperty("minutes").GetProperty("rejectionReason").GetString());
    }

    [Fact]
    public async Task Issued_minutes_tell_every_reader_with_the_exact_words()
    {
        // The waiting notification and the issued notification carry the exact title
        // and body, and the issued email points at the minutes tab and names the Word/PDF downloads.
        var s = await f.Seed(MeetingStatus.Ended);
        using var secretary = f.Client(s.Secretary);
        using var chair = f.Client(s.Chair);
        var draft = await Build(secretary, s);
        Tick();
        var sent = await AssertCode(await Act(secretary, Id(draft), "submit", Stamp(draft)), 200, "MSG424");
        await using (var db = f.Db())
        {
            var waiting = await db.Notifications.SingleAsync(n =>
                n.MeetingId == s.MeetingId && n.Type == NotificationType.MinutesWaitingApproval);
            Assert.Equal(s.Chair, waiting.UserId);
            Assert.Equal("Biên bản chờ duyệt", waiting.Title);
            Assert.Contains($"Thư ký đã gửi biên bản cuộc họp “{SummaryFixture.Title}” để Chủ toạ duyệt.", waiting.Body);
        }

        Tick();
        await AssertCode(await Act(chair, Id(draft), "approve", Stamp(sent.GetProperty("minutes"))), 200, "MSG427");
        await using (var db = f.Db())
        {
            var issued = await db.Notifications
                .Where(n => n.MeetingId == s.MeetingId && n.Type == NotificationType.MinutesIssued).ToListAsync();
            Assert.DoesNotContain(issued, n => n.UserId == s.Chair);
            Assert.All(issued, n =>
            {
                Assert.Equal("Biên bản đã ban hành", n.Title);
                Assert.Contains($"Biên bản cuộc họp “{SummaryFixture.Title}” đã được ban hành.", n.Body);
            });
            var emails = await db.EmailMessages
                .Where(e => e.MeetingId == s.MeetingId && e.DeduplicationKey.StartsWith("email-minutes-issued:")).ToListAsync();
            Assert.NotEmpty(emails);
            Assert.All(emails, e =>
            {
                Assert.StartsWith("Biên bản cuộc họp đã ban hành:", e.Subject);
                Assert.Contains("Xem biên bản", e.HtmlBody);
                Assert.Contains($"/mgmt/meetings/{s.MeetingId:D}?tab=minutes", e.HtmlBody);
                Assert.Contains("Trên trang biên bản có thể tải bản Word hoặc PDF.", e.HtmlBody);
            });
        }

        // A reader sees the issued minutes without the officers' working facts.
        using var member = f.Client(s.Member);
        var seen = (await State(member, s)).GetProperty("minutes");
        Assert.Equal("Issued", seen.GetProperty("status").GetString());
        Assert.Equal(JsonValueKind.Null, seen.GetProperty("submittedBy").ValueKind);
        Assert.Equal(JsonValueKind.Null, seen.GetProperty("submittedAt").ValueKind);
        Assert.Equal(JsonValueKind.Null, seen.GetProperty("returnedBy").ValueKind);
        Assert.Equal(JsonValueKind.Null, seen.GetProperty("returnedAt").ValueKind);
        Assert.Equal(JsonValueKind.Null, seen.GetProperty("rejectionReason").ValueKind);
        Assert.Equal(JsonValueKind.Null, seen.GetProperty("builtFromDraftAt").ValueKind);
        Assert.Equal(s.Chair, seen.GetProperty("approvedBy").GetProperty("id").GetGuid());
    }

    [Fact]
    public async Task Issuing_a_correction_tells_readers_it_is_a_correction()
    {
        // The correction issue tells readers it is a correction, by bell and by email, and the
        // superseded revision keeps the text it was issued with.
        var (s, issued) = await IssuedMinutes();
        using var secretary = f.Client(s.Secretary);
        using var chair = f.Client(s.Chair);
        var first = Id(issued);
        Tick();
        var correction = (await AssertCode(await Correct(chair, first), 201, "MSG432")).GetProperty("minutes");
        Tick();
        var edited = (await AssertCode(await secretary.PutAsJsonAsync($"/api/minutes/{Id(correction)}",
            new { content = "<p>Đính chính: sửa tên dự án.</p>", updatedAt = Stamp(correction) }), 200, "MSG396"))
            .GetProperty("minutes");
        Tick();
        var sent = await AssertCode(await Act(secretary, Id(edited), "submit", Stamp(edited)), 200, "MSG424");
        Tick();
        await AssertCode(await Act(chair, Id(sent.GetProperty("minutes")), "approve", Stamp(sent.GetProperty("minutes"))),
            200, "MSG427");

        await using var db = f.Db();
        Assert.Equal(MinutesStatus.Superseded, (await db.MeetingMinutes.SingleAsync(m => m.Id == first)).Status);
        Assert.Equal(issued.GetProperty("content").GetString(),
            (await db.MeetingMinutes.SingleAsync(m => m.Id == first)).Content);
        var notice = await db.Notifications
            .Where(n => n.MeetingId == s.MeetingId && n.Type == NotificationType.MinutesIssued)
            .OrderByDescending(n => n.CreatedAt).FirstAsync();
        Assert.Equal("Biên bản đã ban hành", notice.Title);
        Assert.Contains($"Bản đính chính biên bản cuộc họp “{SummaryFixture.Title}” đã được ban hành.", notice.Body);
        var email = await db.EmailMessages
            .Where(e => e.MeetingId == s.MeetingId && e.DeduplicationKey.StartsWith("email-minutes-issued:"))
            .OrderByDescending(e => e.CreatedAt).FirstAsync();
        Assert.StartsWith("Bản đính chính biên bản đã ban hành:", email.Subject);
        Assert.Contains("Đính chính lần 1", email.HtmlBody);
    }

    [Fact]
    public async Task A_revision_that_cannot_be_read_answers_MSG403()
    {
        // UC-58 EX-1,[chèn lỗi]. When the minutes cannot be read, the revision answers 500 MSG403.
        var s = await f.Seed(MeetingStatus.Ended);
        using var secretary = f.Client(s.Secretary);
        var id = Id(await Build(secretary, s));

        f.Failures.FailMinutesRead = true;
        try
        {
            await AssertCode(await secretary.GetAsync($"/api/minutes/{id}"), 500, "MSG403");
        }
        finally
        {
            f.Failures.FailMinutesRead = false;
        }
    }

    [Fact]
    public async Task Approving_when_the_save_fails_issues_nothing()
    {
        // UC-64 EX-4 [chèn lỗi]. A failed save issues nothing: no audit row, no bell, no email.
        var s = await f.Seed(MeetingStatus.Ended);
        using var secretary = f.Client(s.Secretary);
        using var chair = f.Client(s.Chair);
        var draft = await Build(secretary, s);
        Tick();
        var waiting = (await AssertCode(await Act(secretary, Id(draft), "submit", Stamp(draft)), 200, "MSG424"))
            .GetProperty("minutes");

        f.Failures.FailSave = true;
        try
        {
            await AssertCode(await Act(chair, Id(draft), "approve", Stamp(waiting)), 503, "MSG404");
        }
        finally
        {
            f.Failures.FailSave = false;
        }

        await using var db = f.Db();
        Assert.Equal(MinutesStatus.WaitingApproval, (await db.MeetingMinutes.SingleAsync(m => m.Id == Id(draft))).Status);
        Assert.False(await db.AuditLog.AnyAsync(a => a.Action == AuditActions.MinutesIssued && a.ObjectId == Id(draft)));
        Assert.Empty(await db.Notifications
            .Where(n => n.MeetingId == s.MeetingId && n.Type == NotificationType.MinutesIssued).ToListAsync());
        Assert.Empty(await db.EmailMessages
            .Where(e => e.MeetingId == s.MeetingId && e.DeduplicationKey.StartsWith("email-minutes-issued:")).ToListAsync());
    }

    [Fact]
    public async Task Returning_when_the_save_fails_changes_nothing()
    {
        // UC-65 EX-4 [chèn lỗi]. A failed save returns nothing and tells nobody.
        var s = await f.Seed(MeetingStatus.Ended);
        using var secretary = f.Client(s.Secretary);
        using var chair = f.Client(s.Chair);
        var draft = await Build(secretary, s);
        Tick();
        var waiting = (await AssertCode(await Act(secretary, Id(draft), "submit", Stamp(draft)), 200, "MSG424"))
            .GetProperty("minutes");

        f.Failures.FailSave = true;
        try
        {
            await AssertCode(await Return(chair, Id(draft), "Bổ sung kết luận.", Stamp(waiting)), 503, "MSG404");
        }
        finally
        {
            f.Failures.FailSave = false;
        }

        await using var db = f.Db();
        Assert.Equal(MinutesStatus.WaitingApproval, (await db.MeetingMinutes.SingleAsync(m => m.Id == Id(draft))).Status);
        Assert.Empty(await db.Notifications
            .Where(n => n.MeetingId == s.MeetingId && n.Type == NotificationType.MinutesReturned).ToListAsync());
    }

    [Fact]
    public async Task Correcting_when_the_save_fails_creates_nothing()
    {
        // UC-83 EX-4 [chèn lỗi]. A failed save creates no revision: the history still has only the issue.
        var (s, issued) = await IssuedMinutes();
        using var chair = f.Client(s.Chair);

        f.Failures.FailSave = true;
        try
        {
            await AssertCode(await Correct(chair, Id(issued)), 503, "MSG404");
        }
        finally
        {
            f.Failures.FailSave = false;
        }

        await using var db = f.Db();
        Assert.Equal([MinutesStatus.Issued],
            await db.MeetingMinutes.Where(m => m.MeetingId == s.MeetingId)
                .OrderBy(m => m.RevisionNumber).Select(m => m.Status).ToListAsync());
    }
}
