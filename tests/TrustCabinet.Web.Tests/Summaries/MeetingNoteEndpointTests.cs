using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.AspNetCore.SignalR.Client;
using Microsoft.EntityFrameworkCore;
using TrustCabinet.Domain.Entities;
using TrustCabinet.Domain.Enums;
using TrustCabinet.Domain.Notes;
using TrustCabinet.Infrastructure.Summaries;
using static TrustCabinet.Web.Tests.Agenda.AgendaEndpointTests;

namespace TrustCabinet.Web.Tests.Summaries;

/// <summary>SCRUM-30, UC-82 — the Secretary's running notes: GET/PUT /api/meetings/{id}/notes, the notesChanged event,
/// and the notes flowing into the minutes draft (BR-402).</summary>
public sealed class MeetingNoteEndpointTests(SummaryFixture f) : IClassFixture<SummaryFixture>
{
    private static string Notes(SummaryFixture.Scenario s) => $"/api/meetings/{s.MeetingId}/notes";

    private static Task<HttpResponseMessage> Put(HttpClient client, SummaryFixture.Scenario s, Guid? agendaItemId, string? body,
        string? updatedAt)
        => client.PutAsJsonAsync(Notes(s), new { agendaItemId, body, updatedAt });

    private static async Task<JsonElement> Saved(HttpClient client, SummaryFixture.Scenario s, Guid? agendaItemId, string body,
        string? updatedAt = null)
        => (await AssertCode(await Put(client, s, agendaItemId, body, updatedAt), 200, "MSG415")).GetProperty("note");

    private async Task SetInProgress(Guid itemId)
    {
        await using var db = f.Db();
        var item = await db.AgendaItems.SingleAsync(a => a.Id == itemId);
        item.Status = AgendaItemStatus.InProgress;
        item.ActualEndedAt = null;
        await db.SaveChangesAsync();
    }

    [Fact]
    public async Task Secretary_writes_notes_and_the_chair_reads_them_read_only()
    {
        var s = await f.Seed();
        await SetInProgress(s.SecondItem);
        using var secretary = f.Client(s.Secretary);
        using var chair = f.Client(s.Chair);

        var empty = await secretary.GetFromJsonAsync<JsonElement>(Notes(s));
        Assert.Equal(0, empty.GetProperty("notes").GetArrayLength());
        Assert.Equal(s.SecondItem, empty.GetProperty("currentAgendaItemId").GetGuid());
        Assert.True(empty.GetProperty("canWrite").GetBoolean());

        var item = await Saved(secretary, s, s.SecondItem, "Ý kiến: Cần thêm kinh phí.\r\nKết luận: Giao Văn phòng rà soát.");
        Assert.Equal(s.SecondItem, item.GetProperty("agendaItemId").GetGuid());
        Assert.Equal("Ý kiến: Cần thêm kinh phí.\nKết luận: Giao Văn phòng rà soát.", item.GetProperty("body").GetString());
        Assert.Equal(s.Secretary, item.GetProperty("author").GetProperty("id").GetGuid());
        var general = await Saved(secretary, s, null, "Họp đúng giờ.");
        Assert.Equal(JsonValueKind.Null, general.GetProperty("agendaItemId").ValueKind);

        // Edited again from the copy just saved.
        f.Clock.Now = f.Clock.Now.AddSeconds(5);
        var edited = await Saved(secretary, s, null, "Họp đúng giờ, đủ thành phần.", general.GetProperty("updatedAt").GetString());
        Assert.Equal(f.Clock.Now, edited.GetProperty("updatedAt").GetDateTimeOffset());

        var read = await chair.GetFromJsonAsync<JsonElement>(Notes(s));
        Assert.False(read.GetProperty("canWrite").GetBoolean());
        var notes = read.GetProperty("notes").EnumerateArray().ToList();
        Assert.Equal(2, notes.Count);
        // The meeting-wide note first, then the items in agenda order.
        Assert.Equal("Họp đúng giờ, đủ thành phần.", notes[0].GetProperty("body").GetString());
        Assert.Equal(s.SecondItem, notes[1].GetProperty("agendaItemId").GetGuid());
        Assert.All(notes, n => Assert.Equal("Nguyễn Thư Ký", n.GetProperty("author").GetProperty("fullName").GetString()));

        await using var db = f.Db();
        var rows = await db.MeetingNotes.Where(n => n.MeetingId == s.MeetingId).ToListAsync();
        Assert.Equal(2, rows.Count);
        Assert.All(rows, r => Assert.Equal(s.Secretary, r.AuthorId));
    }

    [Fact]
    public async Task An_out_of_date_copy_never_overwrites_a_newer_note()
    {
        var s = await f.Seed();
        using var secretary = f.Client(s.Secretary);
        // A note the caller says it read does not exist yet: the copy is stale.
        await AssertCode(await Put(secretary, s, s.FirstItem, "x", f.Clock.Now.ToString("O")), 409, "MSG418");

        var first = await Saved(secretary, s, s.FirstItem, "Bản từ khung trong phòng họp.");
        var loaded = first.GetProperty("updatedAt").GetString();
        f.Clock.Now = f.Clock.Now.AddSeconds(3);
        await Saved(secretary, s, s.FirstItem, "Bản từ tab mới.", loaded);

        // The room panel saves from the copy it read before the tab did.
        f.Clock.Now = f.Clock.Now.AddSeconds(3);
        await AssertCode(await Put(secretary, s, s.FirstItem, "Bản cũ ghi đè.", loaded), 409, "MSG418");
        // And a window that never saw the note cannot create it a second time.
        await AssertCode(await Put(secretary, s, s.FirstItem, "Bản mới tinh.", null), 409, "MSG418");

        await using var db = f.Db();
        Assert.Equal("Bản từ tab mới.", (await db.MeetingNotes.SingleAsync(n => n.MeetingId == s.MeetingId)).Body);
    }

    [Fact]
    public async Task Only_the_secretary_writes_and_only_the_chair_and_secretary_read()
    {
        var s = await f.Seed();
        using var secretary = f.Client(s.Secretary);
        using var chair = f.Client(s.Chair);
        using var member = f.Client(s.Member);
        using var organizer = f.Client(s.Organizer);
        using var outsider = f.Client(s.Outsider);
        await Saved(secretary, s, null, "Ghi chú của Thư ký.");

        await AssertCode(await Put(chair, s, null, "Chủ tọa ghi.", null), 403, "MSG416");
        // Readers of the meeting who are neither Chair nor Secretary learn nothing about the notes, reading or writing (EX-8).
        Assert.Equal(HttpStatusCode.NotFound, (await Put(member, s, null, "Thành viên ghi.", null)).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await Put(organizer, s, null, "Người tạo ghi.", null)).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await member.GetAsync(Notes(s))).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await organizer.GetAsync(Notes(s))).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await outsider.GetAsync(Notes(s))).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await Put(outsider, s, null, "x", null)).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await secretary.GetAsync($"/api/meetings/{Guid.NewGuid()}/notes")).StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized, (await f.Client().GetAsync(Notes(s))).StatusCode);

        await using var db = f.Db();
        Assert.Equal("Ghi chú của Thư ký.", (await db.MeetingNotes.SingleAsync(n => n.MeetingId == s.MeetingId)).Body);
    }

    [Fact]
    public async Task A_secretary_who_is_replaced_stops_writing_and_the_new_one_starts_blank()
    {
        var s = await f.Seed();
        using var secretary = f.Client(s.Secretary);
        await Saved(secretary, s, null, "Ghi chú trước khi đổi Thư ký.");
        await using (var db = f.Db())
        {
            var participants = await db.MeetingParticipants.Where(p => p.MeetingId == s.MeetingId).ToListAsync();
            participants.Single(p => p.UserId == s.Secretary).Role = MeetingRole.Member;
            participants.Single(p => p.UserId == s.Member).Role = MeetingRole.Secretary;
            await db.SaveChangesAsync();
        }
        // Now a member: answered as if the notes did not exist (EX-8).
        Assert.Equal(HttpStatusCode.NotFound, (await Put(secretary, s, null, "Vẫn cố ghi.", null)).StatusCode);
        using var successor = f.Client(s.Member);
        var state = await successor.GetFromJsonAsync<JsonElement>(Notes(s));
        Assert.Equal(0, state.GetProperty("notes").GetArrayLength());
        Assert.True(state.GetProperty("canWrite").GetBoolean());
    }

    [Theory]
    [InlineData(MeetingStatus.Published)]
    [InlineData(MeetingStatus.Cancelled)]
    public async Task Notes_are_not_written_before_the_sitting_starts_or_after_it_is_cancelled(MeetingStatus status)
    {
        var s = await f.Seed(status);
        using var secretary = f.Client(s.Secretary);
        Assert.False((await secretary.GetFromJsonAsync<JsonElement>(Notes(s))).GetProperty("canWrite").GetBoolean());
        await AssertCode(await Put(secretary, s, null, "Ghi chú sớm.", null), 409, "MSG417");
        await using var db = f.Db();
        Assert.False(await db.MeetingNotes.AnyAsync(n => n.MeetingId == s.MeetingId));
    }

    [Fact]
    public async Task After_the_meeting_ends_notes_are_written_until_the_minutes_draft_is_built()
    {
        var s = await f.Seed(MeetingStatus.Ended);
        using var secretary = f.Client(s.Secretary);
        var note = await Saved(secretary, s, s.FirstItem, "Bổ sung sau cuộc họp.");
        await AssertCode(await secretary.PostAsJsonAsync(s.Minutes, new { rebuild = false }), 201, "MSG395");

        Assert.False((await secretary.GetFromJsonAsync<JsonElement>(Notes(s))).GetProperty("canWrite").GetBoolean());
        await AssertCode(await Put(secretary, s, s.FirstItem, "Sửa sau khi có biên bản.", note.GetProperty("updatedAt").GetString()),
            409, "MSG417");
        await using var db = f.Db();
        Assert.Equal("Bổ sung sau cuộc họp.", (await db.MeetingNotes.SingleAsync(n => n.MeetingId == s.MeetingId)).Body);
    }

    [Fact]
    public async Task A_note_holds_at_most_twenty_thousand_characters_and_may_be_empty()
    {
        var s = await f.Seed();
        using var secretary = f.Client(s.Secretary);
        await AssertCode(await Put(secretary, s, null, new string('a', MeetingNoteRules.MaxLength + 1), null), 400, "MSG419");
        var full = await Saved(secretary, s, null, new string('a', MeetingNoteRules.MaxLength));
        f.Clock.Now = f.Clock.Now.AddSeconds(1);
        var cleared = await Saved(secretary, s, null, "", full.GetProperty("updatedAt").GetString());
        Assert.Equal("", cleared.GetProperty("body").GetString());
    }

    [Fact]
    public async Task A_note_on_an_item_of_another_meeting_or_a_removed_item_is_refused()
    {
        var s = await f.Seed();
        var other = await f.Seed();
        using var secretary = f.Client(s.Secretary);
        await AssertCode(await Put(secretary, s, other.FirstItem, "Nhầm cuộc họp.", null), 409, "MSG420");
        await AssertCode(await Put(secretary, s, Guid.NewGuid(), "Mục đã xoá.", null), 409, "MSG420");
        await using var db = f.Db();
        Assert.False(await db.MeetingNotes.AnyAsync(n => n.MeetingId == s.MeetingId));
    }

    [Fact]
    public async Task Database_failures_answer_with_their_own_codes()
    {
        var s = await f.Seed();
        using var secretary = f.Client(s.Secretary);
        var note = await Saved(secretary, s, null, "Trước sự cố.");
        try
        {
            f.Failures.FailNotesRead = true;
            await AssertCode(await secretary.GetAsync(Notes(s)), 500, "MSG421");
            f.Failures.FailNotesRead = false;

            f.Failures.FailSave = true;
            // A first save goes through SaveChanges, a later one through one UPDATE: both fail the same way.
            await AssertCode(await Put(secretary, s, s.FirstItem, "Mới.", null), 503, "MSG422");
            await AssertCode(await Put(secretary, s, null, "Sửa.", note.GetProperty("updatedAt").GetString()), 503, "MSG422");
        }
        finally
        {
            f.Failures.FailNotesRead = false;
            f.Failures.FailSave = false;
        }
        await using var db = f.Db();
        Assert.Equal("Trước sự cố.", (await db.MeetingNotes.SingleAsync(n => n.MeetingId == s.MeetingId)).Body);
    }

    [Fact]
    public async Task A_save_tells_the_chair_and_secretary_windows_but_nobody_else()
    {
        var s = await f.Seed();
        await using var chairHub = f.Hub(s.Chair);
        await using var memberHub = f.Hub(s.Member);
        var chairHeard = new TaskCompletionSource<JsonElement>(TaskCreationOptions.RunContinuationsAsynchronously);
        var memberHeard = new TaskCompletionSource<JsonElement>(TaskCreationOptions.RunContinuationsAsynchronously);
        chairHub.On<JsonElement>("notesChanged", e => chairHeard.TrySetResult(e));
        memberHub.On<JsonElement>("notesChanged", e => memberHeard.TrySetResult(e));
        await chairHub.StartAsync();
        await memberHub.StartAsync();
        await chairHub.InvokeAsync("JoinMeeting", s.MeetingId);
        await memberHub.InvokeAsync("JoinMeeting", s.MeetingId);

        using var secretary = f.Client(s.Secretary);
        var note = await Saved(secretary, s, s.FirstItem, "Nội dung không đi qua hub.");
        var change = await chairHeard.Task.WaitAsync(TimeSpan.FromSeconds(10));
        Assert.Equal(s.MeetingId, change.GetProperty("meetingId").GetGuid());
        Assert.Equal(s.FirstItem, change.GetProperty("agendaItemId").GetGuid());
        Assert.Equal(note.GetProperty("updatedAt").GetDateTimeOffset(), change.GetProperty("updatedAt").GetDateTimeOffset());
        Assert.DoesNotContain("Nội dung", change.GetRawText());

        await Task.Delay(500);
        Assert.False(memberHeard.Task.IsCompleted);
    }

    [Fact]
    public async Task The_secretarys_notes_fill_the_minutes_draft()
    {
        var s = await f.Seed();
        using var secretary = f.Client(s.Secretary);
        await Saved(secretary, s, null, "Các đại biểu có mặt đúng giờ.\n## Không thành tiêu đề");
        await Saved(secretary, s, s.FirstItem,
            "Ý kiến: Tiến độ đạt 18/20 nhiệm vụ.\nkết luận: Giao Văn phòng tổng hợp.\nNhiệm vụ: Báo cáo trước ngày 15/11.");
        // Only the current Secretary's notes count; a row from anyone else is not part of the minutes.
        await using (var db = f.Db())
        {
            db.MeetingNotes.Add(new MeetingNote { MeetingId = s.MeetingId, AgendaItemId = s.FirstItem, AuthorId = s.Chair,
                Body = "Ghi chú riêng của Chủ tọa." });
            await db.SaveChangesAsync();
        }

        var minutes = await AssertCode(await secretary.PostAsJsonAsync(s.Minutes, new { rebuild = false }), 201, "MSG395");
        var content = minutes.GetProperty("minutes").GetProperty("content").GetString()!;
        // The minutes hold HTML (ADR-0055): a template line is found as the HTML it becomes, a list line by its text.
        int At(string text)
        {
            var at = content.IndexOf(text, StringComparison.Ordinal);
            Assert.True(at >= 0, $"Missing from the minutes: {text}");
            return at;
        }
        var discussion = At(Html($"## {SummaryWriter.DiscussionHeading}"));
        var first = At(Html($"### 1. {SummaryFixture.FirstItemTitle}"));
        var conclusions = At(Html($"## {SummaryWriter.ConclusionsHeading}"));
        var tasks = At(Html($"### {SummaryWriter.TasksHeading}"));

        // Section II: the meeting-wide note first, then each item with its own lines; a typed mark is flattened.
        Assert.InRange(At(Html("Các đại biểu có mặt đúng giờ.")), discussion, first);
        Assert.InRange(At(Html("Không thành tiêu đề")), discussion, first);
        var presenter = At(Html("Lê Thành Viên trình bày."));
        Assert.InRange(presenter, first, conclusions);
        Assert.InRange(At(Html("Ý kiến: Tiến độ đạt 18/20 nhiệm vụ.")), presenter, conclusions);
        // Section III: the Chair's recorded conclusion, then the marked lines, each under its item's title.
        Assert.InRange(At($"{SummaryFixture.FirstItemTitle}: Thống nhất báo cáo."), conclusions, tasks);
        Assert.InRange(At($"{SummaryFixture.FirstItemTitle}: Giao Văn phòng tổng hợp."), conclusions, tasks);
        Assert.True(At($"{SummaryFixture.FirstItemTitle}: Báo cáo trước ngày 15/11.") > tasks);
        Assert.DoesNotContain("Ghi chú riêng của Chủ tọa.", content);
        Assert.DoesNotContain("kết luận:", content);
    }

    [Fact]
    public async Task An_ai_draft_takes_the_place_of_the_notes_in_the_minutes()
    {
        // BR-402: the notes fill the minutes only when the meeting has no AI draft, reviewed or not (#263).
        f.Model.Reset();
        var s = await f.Seed(MeetingStatus.Ended);
        using var secretary = f.Client(s.Secretary);
        await Saved(secretary, s, s.FirstItem, "Ý kiến: Chỉ có trong ghi chú.");
        await AssertCode(await secretary.PostAsync(s.Summary, null), 202, "MSG384");
        await f.ProcessAll();

        var minutes = await AssertCode(await secretary.PostAsJsonAsync(s.Minutes, new { rebuild = false }), 201, "MSG395");
        var content = minutes.GetProperty("minutes").GetProperty("content").GetString()!;
        Assert.Contains(Html(SummaryFixture.FakeLanguageModel.Objectives), content);
        Assert.DoesNotContain("Chỉ có trong ghi chú.", content);
    }

    /// <summary>A template line as the HTML the minutes hold (ADR-0055).</summary>
    private static string Html(string line) => MinutesHtml.FromLines(line);

    [Fact]
    public async Task Deleting_a_pending_item_moves_its_note_into_the_meeting_wide_note()
    {
        var s = await f.Seed();
        Guid pending;
        await using (var db = f.Db())
        {
            var item = new AgendaItem { MeetingId = s.MeetingId, Position = 3, Title = "Việc phát sinh" };
            db.AgendaItems.Add(item);
            await db.SaveChangesAsync();
            pending = item.Id;
        }
        using var secretary = f.Client(s.Secretary);
        var general = await Saved(secretary, s, null, "Ghi chú chung.");
        await Saved(secretary, s, pending, "Chuẩn bị trước cho việc phát sinh.");

        f.Clock.Now = f.Clock.Now.AddMinutes(1);
        await AssertCode(await secretary.DeleteAsync($"/api/meetings/{s.MeetingId}/agenda/{pending}"), 200, "MSG62");

        await using var check = f.Db();
        var note = await check.MeetingNotes.SingleAsync(n => n.MeetingId == s.MeetingId);
        Assert.Null(note.AgendaItemId);
        Assert.Equal("Ghi chú chung.\n\nViệc phát sinh:\nChuẩn bị trước cho việc phát sinh.", note.Body);
        // The window that still holds the old meeting-wide note must reload rather than overwrite the folded text.
        await AssertCode(await Put(secretary, s, null, "Ghi chú chung.", general.GetProperty("updatedAt").GetString()), 409, "MSG418");
        await AssertCode(await Put(secretary, s, pending, "x", null), 409, "MSG420");
    }

    [Fact]
    public void Marks_are_read_only_at_the_start_of_a_line()
    {
        Assert.Equal(("Kết luận", "Đồng ý."), MeetingNoteRules.Marked("  KẾT LUẬN: Đồng ý.")!.Value);
        Assert.Equal(("Nhiệm vụ", ""), MeetingNoteRules.Marked("Nhiệm vụ:")!.Value);
        Assert.Null(MeetingNoteRules.Marked("Đã có kết luận: đồng ý."));
        Assert.Null(MeetingNoteRules.Marked("Nhiệm vụ chưa rõ"));
        Assert.Equal(MeetingNoteRules.MaxLength,
            MeetingNoteRules.Fold(new string('a', 15_000), "Mục", new string('b', 10_000)).Length);
        Assert.Equal("Mục:\nNội dung", MeetingNoteRules.Fold("", "Mục", "Nội dung"));
    }
}
