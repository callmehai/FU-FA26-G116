using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using TrustCabinet.Domain.Entities;
using TrustCabinet.Domain.Enums;
using TrustCabinet.Domain.Summaries;
using static TrustCabinet.Web.Tests.Agenda.AgendaEndpointTests;

namespace TrustCabinet.Web.Tests.Summaries;

/// <summary>SCRUM-36 — the AI draft: GET/POST/PUT /api/meetings/{id}/summary and POST …/summary/review.</summary>
public sealed class SummaryEndpointTests(SummaryFixture f) : IClassFixture<SummaryFixture>
{
    private static object Edit(string objectives = "Mục tiêu đã sửa.", string discussion = "1. Báo cáo tiến độ\nĐã sửa.",
        string conclusions = "1. Kết luận của chủ trì\nĐã sửa.") => new { objectives, discussion, conclusions };

    private static readonly JsonSerializerOptions Web = new(JsonSerializerDefaults.Web);

    private static SummaryDraft Stored(string content) => JsonSerializer.Deserialize<SummaryDraft>(content, Web)!;

    /// <summary>Ordinary Vietnamese prose, cut to <paramref name="length"/> characters.</summary>
    private static string Prose(int length)
        => string.Concat(Enumerable.Repeat("Đồng chí Chủ trì đề nghị các sở, ngành rà soát tiến độ và báo cáo kết quả. ", length / 40 + 1))[..length];

    private static Task<JsonElement> State(HttpClient client, SummaryFixture.Scenario s)
        => client.GetFromJsonAsync<JsonElement>(s.Summary);

    /// <summary>A meeting whose draft has been written by the (fake) AI and is Ready.</summary>
    private async Task<SummaryFixture.Scenario> Drafted(MeetingStatus status = MeetingStatus.InProgress)
    {
        f.Model.Reset();
        var s = await f.Seed(status);
        using var secretary = f.Client(s.Secretary);
        await AssertCode(await secretary.PostAsync(s.Summary, null), 202, "MSG384");
        await f.ProcessAll();
        return s;
    }

    [Fact]
    public async Task Secretary_asks_for_a_draft_which_is_queued_then_written_per_agenda_item()
    {
        f.Model.Reset();
        var s = await f.Seed();
        using var secretary = f.Client(s.Secretary);

        var before = await State(secretary, s);
        Assert.Equal(JsonValueKind.Null, before.GetProperty("summary").ValueKind);
        Assert.True(before.GetProperty("canManage").GetBoolean());
        Assert.True(before.GetProperty("aiReady").GetBoolean());
        var readiness = before.GetProperty("readiness");
        Assert.Equal(2, readiness.GetProperty("sentences").GetInt32());
        Assert.Equal(2, readiness.GetProperty("agendaItemsTimed").GetInt32());
        Assert.Equal(1, readiness.GetProperty("closedVotes").GetInt32());

        var queued = await AssertCode(await secretary.PostAsync(s.Summary, null), 202, "MSG384");
        Assert.Equal("Queued", queued.GetProperty("summary").GetProperty("status").GetString());
        Assert.Equal(s.Secretary, queued.GetProperty("summary").GetProperty("requestedBy").GetProperty("id").GetGuid());
        await using (var db = f.Db())
        {
            var row = await db.MeetingSummaries.SingleAsync(x => x.MeetingId == s.MeetingId);
            Assert.Equal(SummaryStatus.Queued, row.Status);
            Assert.Equal(s.Secretary, row.RequestedById);
            Assert.Equal("", row.Content);
        }

        await f.ProcessAll();
        var ready = (await State(secretary, s)).GetProperty("summary");
        Assert.Equal("Ready", ready.GetProperty("status").GetString());
        Assert.Equal("fake-llm@test", ready.GetProperty("model").GetString());
        var draft = ready.GetProperty("draft");
        Assert.Equal(SummaryFixture.FakeLanguageModel.Objectives, draft.GetProperty("objectives").GetString());
        var discussion = draft.GetProperty("discussion").GetString()!;
        Assert.StartsWith($"1. {SummaryFixture.FirstItemTitle}\n", discussion);
        Assert.Contains($"2. {SummaryFixture.SecondItemTitle}\n", discussion);
        var sources = draft.GetProperty("sources").EnumerateArray().ToArray();
        Assert.Equal([s.FirstItem, s.SecondItem], sources.Select(x => x.GetProperty("agendaItemId").GetGuid()));
        Assert.All(sources, x => Assert.Equal(1, x.GetProperty("lines").GetInt32()));

        // Each part is written from what was said while its item was open, and nothing else.
        var parts = f.Model.Prompts.Where(p => p.Contains("BẢN GHI LỜI THOẠI", StringComparison.Ordinal)).ToArray();
        Assert.Equal(2, parts.Length);
        Assert.Contains(parts, p => p.Contains($"NỘI DUNG: {SummaryFixture.FirstItemTitle}") && p.Contains("Tiến độ đạt")
            && !p.Contains(SummaryFixture.SecondClip));
        Assert.Contains(parts, p => p.Contains($"NỘI DUNG: {SummaryFixture.SecondItemTitle}") && p.Contains(SummaryFixture.SecondClip)
            && !p.Contains("Tiến độ đạt"));

        await using (var db = f.Db())
        {
            var row = await db.MeetingSummaries.SingleAsync(x => x.MeetingId == s.MeetingId);
            Assert.Equal(SummaryStatus.Ready, row.Status);
            Assert.Equal("fake-llm@test", row.Model);
            Assert.Null(row.ReviewedAt);
            Assert.Equal(SummaryFixture.FakeLanguageModel.Objectives, Stored(row.Content).Objectives);
        }
    }

    [Fact]
    public async Task Readiness_says_when_the_transcript_last_changed_so_a_draft_made_before_reads_as_behind()
    {
        var s = await Drafted();
        using var secretary = f.Client(s.Secretary);
        var before = (await State(secretary, s)).GetProperty("readiness").GetProperty("transcriptChangedAt").GetDateTimeOffset();
        DateTimeOffset corrected;
        await using (var db = f.Db())
        {
            var clips = await db.SpeechClips.Where(c => c.MeetingId == s.MeetingId).ToListAsync();
            Assert.Equal(clips.Max(c => c.UpdatedAt), before);
            // A line corrected after the draft was written.
            corrected = before.AddMinutes(5);
            clips[0].UpdatedAt = corrected;
            await db.SaveChangesAsync();
        }
        var after = (await State(secretary, s)).GetProperty("readiness").GetProperty("transcriptChangedAt").GetDateTimeOffset();
        Assert.Equal(corrected, after);
    }

    /// <summary>The seeded sentences are stamped with the machine's clock; dates them on the test clock instead.</summary>
    private async Task Settle(SummaryFixture.Scenario s)
    {
        await using var db = f.Db();
        await db.SpeechClips.Where(c => c.MeetingId == s.MeetingId).ExecuteUpdateAsync(u => u.SetProperty(c => c.UpdatedAt, f.Clock.Now));
    }

    [Fact]
    public async Task A_line_corrected_while_the_AI_writes_is_not_in_the_draft_so_the_draft_reads_as_behind()
    {
        f.Model.Reset();
        var s = await f.Seed();
        await Settle(s);
        using var secretary = f.Client(s.Secretary);
        await AssertCode(await secretary.PostAsync(s.Summary, null), 202, "MSG384");
        var read = f.Clock.Now;
        // The model takes minutes on a laptop; meanwhile the Secretary corrects a sentence it has already read.
        f.Model.While = async () =>
        {
            f.Clock.Now = f.Clock.Now.AddMinutes(2);
            await using var db = f.Db();
            await db.SpeechClips.Where(c => c.MeetingId == s.MeetingId).Take(1)
                .ExecuteUpdateAsync(u => u.SetProperty(c => c.UpdatedAt, f.Clock.Now));
            f.Clock.Now = f.Clock.Now.AddMinutes(2);
        };
        await f.ProcessAll();

        var state = await State(secretary, s);
        var generated = state.GetProperty("summary").GetProperty("generatedAt").GetDateTimeOffset();
        var changed = state.GetProperty("readiness").GetProperty("transcriptChangedAt").GetDateTimeOffset();
        // Dated when the record was read, not when the model finished, so the correction is later than the draft.
        Assert.Equal(read, generated);
        Assert.Equal(read.AddMinutes(2), changed);
        Assert.True(changed > generated);
    }

    private async Task<(DateTimeOffset Generated, DateTimeOffset Changed)> Stamps(HttpClient client, SummaryFixture.Scenario s)
    {
        var state = await State(client, s);
        return (state.GetProperty("summary").GetProperty("generatedAt").GetDateTimeOffset(),
            state.GetProperty("readiness").GetProperty("transcriptChangedAt").GetDateTimeOffset());
    }

    [Fact]
    public async Task Deleting_a_sentence_after_the_draft_makes_the_draft_read_as_behind()
    {
        var s = await Drafted();
        await Settle(s);
        using var secretary = f.Client(s.Secretary);
        var (generated, changed) = await Stamps(secretary, s);
        Assert.True(changed <= generated);

        // The sentence goes outright, leaving only the audit entry of its deletion to date it.
        Guid clip;
        await using (var db = f.Db())
            clip = await db.SpeechClips.Where(c => c.MeetingId == s.MeetingId).Select(c => c.Id).FirstAsync();
        Assert.True((await secretary.DeleteAsync($"/api/speech-clips/{clip}")).IsSuccessStatusCode);
        (_, changed) = await Stamps(secretary, s);
        Assert.True(changed > generated);
    }

    [Fact]
    public async Task Erasing_a_recording_after_the_draft_makes_the_draft_read_as_behind()
    {
        var s = await Drafted();
        await Settle(s);
        using var secretary = f.Client(s.Secretary);
        var (generated, changed) = await Stamps(secretary, s);
        Assert.True(changed <= generated);

        // Erased with its transcript, a recording keeps its row and the time it was erased.
        var erased = generated.AddMinutes(3);
        await using (var db = f.Db())
        {
            db.Recordings.Add(new Recording
            {
                MeetingId = s.MeetingId, FileName = "da-xoa.wav", StoragePath = "", ContentType = "audio/wav",
                Status = RecordingStatus.Deleted, DeletedAt = erased, CreatedById = s.Secretary,
                CreatedAt = generated, UpdatedAt = erased,
            });
            await db.SaveChangesAsync();
        }
        (_, changed) = await Stamps(secretary, s);
        Assert.Equal(erased, changed);
    }

    [Fact]
    public async Task Vote_results_are_copied_from_the_data_and_never_pass_through_the_model()
    {
        var s = await Drafted();
        using var secretary = f.Client(s.Secretary);
        var conclusions = (await State(secretary, s)).GetProperty("summary").GetProperty("draft").GetProperty("conclusions").GetString()!;
        var lines = conclusions.Split('\n');
        Assert.Equal("1. Kết luận của chủ trì", lines[0]);
        Assert.Contains(SummaryFixture.FakeLanguageModel.Conclusion, lines);
        Assert.Contains("Kết quả biểu quyết:", lines);
        Assert.Contains($"- {SummaryFixture.VoteLine}", lines);
        Assert.Contains("2. Nhiệm vụ, sản phẩm và thời hạn", lines);
        Assert.Contains("Đồng ý 3 phiếu", conclusions);
        Assert.Contains("Kết luận: Thông qua", conclusions);
        Assert.DoesNotContain(f.Model.Prompts, p => p.Contains("Đồng ý 3 phiếu", StringComparison.Ordinal));
    }

    [Fact]
    public async Task Numbers_read_aloud_reach_the_model_as_digits()
    {
        await Drafted();
        Assert.Contains(f.Model.Prompts, p => p.Contains("Tiến độ đạt 18 trên 20 nhiệm vụ.", StringComparison.Ordinal));
    }

    [Fact]
    public async Task A_model_failure_leaves_the_draft_failed_with_its_message()
    {
        f.Model.Reset();
        var s = await f.Seed();
        using var secretary = f.Client(s.Secretary);
        f.Model.Failure = new LanguageModelException("Máy AI không phản hồi.");
        await AssertCode(await secretary.PostAsync(s.Summary, null), 202, "MSG384");
        await f.ProcessAll();
        var failed = (await State(secretary, s)).GetProperty("summary");
        Assert.Equal("Failed", failed.GetProperty("status").GetString());
        Assert.Equal("Máy AI không phản hồi.", failed.GetProperty("errorMessage").GetString());
        Assert.Equal(JsonValueKind.Null, failed.GetProperty("draft").ValueKind);

        // Any other error is reported in words the Secretary can act on, never the exception text.
        f.Model.Failure = new InvalidOperationException("stack trace detail");
        await AssertCode(await secretary.PostAsync(s.Summary, null), 202, "MSG384");
        await f.ProcessAll();
        await using var db = f.Db();
        var row = await db.MeetingSummaries.SingleAsync(x => x.MeetingId == s.MeetingId);
        Assert.Equal(SummaryStatus.Failed, row.Status);
        Assert.Equal("Không viết được dự thảo. Vui lòng thử lại.", row.ErrorMessage);
        f.Model.Reset();
    }

    [Fact]
    public async Task A_second_request_while_one_is_queued_is_busy()
    {
        f.Model.Reset();
        var s = await f.Seed();
        using var chair = f.Client(s.Chair);
        await AssertCode(await chair.PostAsync(s.Summary, null), 202, "MSG384");
        await AssertCode(await chair.PostAsync(s.Summary, null), 409, "MSG387");
        await using (var db = f.Db())
            await db.MeetingSummaries.Where(x => x.MeetingId == s.MeetingId)
                .ExecuteUpdateAsync(u => u.SetProperty(x => x.Status, SummaryStatus.Running));
        await AssertCode(await chair.PostAsync(s.Summary, null), 409, "MSG387");
        await using (var db = f.Db())
            Assert.Equal(1, await db.MeetingSummaries.CountAsync(x => x.MeetingId == s.MeetingId));

        // An interrupted run goes back to the queue, and is then written.
        using (var scope = f.Host.Services.CreateScope())
            Assert.True(await scope.ServiceProvider.GetRequiredService<ISummaryService>().RequeueInterruptedAsync() >= 1);
        await f.ProcessAll();
        Assert.Equal("Ready", (await State(chair, s)).GetProperty("summary").GetProperty("status").GetString());
    }

    [Fact]
    public async Task Without_an_ai_server_a_request_is_refused_and_nothing_is_queued()
    {
        f.Model.Reset();
        var s = await f.Seed();
        using var secretary = f.Client(s.Secretary);
        f.Model.Configured = false;
        try
        {
            Assert.False((await State(secretary, s)).GetProperty("aiReady").GetBoolean());
            await AssertCode(await secretary.PostAsync(s.Summary, null), 503, "MSG394");
            await using var db = f.Db();
            Assert.False(await db.MeetingSummaries.AnyAsync(x => x.MeetingId == s.MeetingId));
        }
        finally { f.Model.Reset(); }
    }

    [Theory]
    [InlineData(MeetingStatus.Draft)]
    [InlineData(MeetingStatus.Approved)]
    [InlineData(MeetingStatus.Published)]
    [InlineData(MeetingStatus.Cancelled)]
    public async Task A_draft_is_asked_for_only_once_the_meeting_has_started(MeetingStatus status)
    {
        f.Model.Reset();
        var s = await f.Seed(status);
        using var secretary = f.Client(s.Secretary);
        await AssertCode(await secretary.PostAsync(s.Summary, null), 409, "MSG386");
        await using var db = f.Db();
        Assert.False(await db.MeetingSummaries.AnyAsync(x => x.MeetingId == s.MeetingId));
    }

    [Theory]
    [InlineData(MeetingStatus.InProgress)]
    [InlineData(MeetingStatus.Ended)]
    public async Task Chair_and_secretary_may_ask_while_in_progress_and_after_the_end(MeetingStatus status)
    {
        f.Model.Reset();
        var s = await f.Seed(status);
        using var chair = f.Client(s.Chair);
        await AssertCode(await chair.PostAsync(s.Summary, null), 202, "MSG384");
        await f.ProcessAll();
    }

    [Theory]
    [InlineData("POST", "")]
    [InlineData("PUT", "")]
    [InlineData("POST", "/review")]
    public async Task A_member_or_organizer_who_is_not_chair_or_secretary_gets_403(string method, string path)
    {
        var s = await Drafted();
        foreach (var actor in new[] { s.Member, s.Organizer })
        {
            using var client = f.Client(actor);
            var request = new HttpRequestMessage(new HttpMethod(method), s.Summary + path);
            if (method == "PUT") request.Content = JsonContent.Create(Edit());
            await AssertCode(await client.SendAsync(request), 403, "MSG385");
        }
        await using var db = f.Db();
        var row = await db.MeetingSummaries.SingleAsync(x => x.MeetingId == s.MeetingId);
        Assert.Null(row.ReviewedAt);
        Assert.Null(row.EditedAt);
        Assert.Equal(SummaryStatus.Ready, row.Status);
    }

    [Theory]
    [InlineData("GET", "")]
    [InlineData("POST", "")]
    [InlineData("PUT", "")]
    [InlineData("POST", "/review")]
    public async Task Outsider_gets_404_not_403_for_a_meeting_they_are_not_in(string method, string path)
    {
        var s = await Drafted();
        using var outsider = f.Client(s.Outsider);
        var request = new HttpRequestMessage(new HttpMethod(method), s.Summary + path);
        if (method == "PUT") request.Content = JsonContent.Create(Edit());
        Assert.Equal(HttpStatusCode.NotFound, (await outsider.SendAsync(request)).StatusCode);
        using var unknown = f.Client(s.Secretary);
        Assert.Equal(HttpStatusCode.NotFound, (await unknown.GetAsync($"/api/meetings/{Guid.NewGuid()}/summary")).StatusCode);
    }

    [Theory]
    [InlineData("GET", "")]
    [InlineData("POST", "")]
    [InlineData("POST", "/review")]
    public async Task Not_signed_in_gets_401(string method, string path)
    {
        var s = await f.Seed();
        using var anonymous = f.Client();
        Assert.Equal(HttpStatusCode.Unauthorized,
            (await anonymous.SendAsync(new HttpRequestMessage(new HttpMethod(method), s.Summary + path))).StatusCode);
    }

    [Fact]
    public async Task A_deactivated_secretary_is_refused()
    {
        var s = await f.Seed();
        await using (var db = f.Db())
            await db.Users.Where(u => u.Id == s.Secretary).ExecuteUpdateAsync(u => u.SetProperty(x => x.IsActive, false));
        using var secretary = f.Client(s.Secretary);
        Assert.Equal(HttpStatusCode.Forbidden, (await secretary.PostAsync(s.Summary, null)).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, (await secretary.GetAsync(s.Summary)).StatusCode);
        await using var check = f.Db();
        Assert.False(await check.MeetingSummaries.AnyAsync(x => x.MeetingId == s.MeetingId));
        // Deactivated, not deleted.
        Assert.True(await check.Users.AnyAsync(u => u.Id == s.Secretary));
    }

    [Fact]
    public async Task Members_see_nothing_until_the_draft_is_reviewed_then_read_it()
    {
        var s = await Drafted();
        using var member = f.Client(s.Member);
        using var organizer = f.Client(s.Organizer);
        using var secretary = f.Client(s.Secretary);

        foreach (var client in new[] { member, organizer })
        {
            var hidden = await State(client, s);
            Assert.Equal(JsonValueKind.Null, hidden.GetProperty("summary").ValueKind);
            Assert.False(hidden.GetProperty("canManage").GetBoolean());
            Assert.Equal(JsonValueKind.Null, hidden.GetProperty("readiness").ValueKind);
            // Absent from the response, not just flagged.
            Assert.DoesNotContain(SummaryFixture.FakeLanguageModel.Objectives, await client.GetStringAsync(s.Summary));
        }

        var reviewed = await AssertCode(await secretary.PostAsync(s.Summary + "/review", null), 200, "MSG391");
        Assert.Equal(s.Secretary, reviewed.GetProperty("summary").GetProperty("reviewedBy").GetProperty("id").GetGuid());
        await using (var db = f.Db())
        {
            var row = await db.MeetingSummaries.SingleAsync(x => x.MeetingId == s.MeetingId);
            Assert.Equal(s.Secretary, row.ReviewedById);
            Assert.Equal(f.Clock.Now, row.ReviewedAt);
        }
        var seen = (await State(member, s)).GetProperty("summary");
        Assert.Equal(SummaryFixture.FakeLanguageModel.Objectives, seen.GetProperty("draft").GetProperty("objectives").GetString());
    }

    [Fact]
    public async Task Editing_saves_the_three_sections_and_takes_the_review_back()
    {
        var s = await Drafted();
        using var chair = f.Client(s.Chair);
        using var member = f.Client(s.Member);
        await AssertCode(await chair.PostAsync(s.Summary + "/review", null), 200, "MSG391");
        Assert.NotEqual(JsonValueKind.Null, (await State(member, s)).GetProperty("summary").ValueKind);

        var saved = await AssertCode(await chair.PutAsJsonAsync(s.Summary,
            Edit("  Mục tiêu đã sửa.  ", discussion: "1. Báo cáo tiến độ\n\nĐã sửa.\n\n\n")), 200, "MSG388");
        var summary = saved.GetProperty("summary");
        Assert.Equal("Mục tiêu đã sửa.", summary.GetProperty("draft").GetProperty("objectives").GetString());
        // Blank lines are gaps in the textarea and nothing in the minutes: they are not kept.
        Assert.Equal("1. Báo cáo tiến độ\nĐã sửa.", summary.GetProperty("draft").GetProperty("discussion").GetString());
        Assert.Equal(s.Chair, summary.GetProperty("editedBy").GetProperty("id").GetGuid());
        Assert.Equal(JsonValueKind.Null, summary.GetProperty("reviewedAt").ValueKind);
        // The sources of the AI's parts survive a correction.
        Assert.Equal(2, summary.GetProperty("draft").GetProperty("sources").GetArrayLength());

        await using (var db = f.Db())
        {
            var row = await db.MeetingSummaries.SingleAsync(x => x.MeetingId == s.MeetingId);
            Assert.Null(row.ReviewedAt);
            Assert.Null(row.ReviewedById);
            Assert.Equal(s.Chair, row.EditedById);
            Assert.Equal("Mục tiêu đã sửa.", Stored(row.Content).Objectives);
            Assert.Equal(SummaryStatus.Ready, row.Status);
        }
        // An unreviewed text never reaches the other participants.
        Assert.Equal(JsonValueKind.Null, (await State(member, s)).GetProperty("summary").ValueKind);
    }

    [Fact]
    public async Task An_edit_with_an_empty_or_too_long_section_is_refused_and_saves_nothing()
    {
        var s = await Drafted();
        using var secretary = f.Client(s.Secretary);
        var empty = await AssertCode(await secretary.PutAsJsonAsync(s.Summary, Edit(objectives: "  ")), 400, "MSG389");
        Assert.True(empty.GetProperty("errors").TryGetProperty("objectives", out _));
        var tooLong = await AssertCode(await secretary.PutAsJsonAsync(s.Summary,
            Edit(discussion: new string('a', SummaryLimits.Discussion + 1))), 400, "MSG389");
        Assert.True(tooLong.GetProperty("errors").TryGetProperty("discussion", out _));
        Assert.False(tooLong.GetProperty("errors").TryGetProperty("objectives", out _));
        await AssertCode(await secretary.PutAsJsonAsync(s.Summary, new { objectives = "x", discussion = "y" }), 400, "MSG389");
        // At the limit is fine.
        await AssertCode(await secretary.PutAsJsonAsync(s.Summary,
            Edit(conclusions: new string('k', SummaryLimits.Conclusions))), 200, "MSG388");
        await using var db = f.Db();
        Assert.Equal(new string('k', SummaryLimits.Conclusions), Stored((await db.MeetingSummaries.SingleAsync(x => x.MeetingId == s.MeetingId)).Content).Conclusions);
    }

    [Fact]
    public async Task An_edit_of_vietnamese_text_within_the_documented_limits_is_saved()
    {
        var s = await Drafted();
        using var secretary = f.Client(s.Secretary);
        var edit = Edit(Prose(SummaryLimits.Objectives), Prose(SummaryLimits.Discussion), Prose(SummaryLimits.Conclusions));
        await AssertCode(await secretary.PutAsJsonAsync(s.Summary, edit), 200, "MSG388");
        await using var db = f.Db();
        Assert.Equal(Prose(SummaryLimits.Discussion).Trim(),
            Stored((await db.MeetingSummaries.SingleAsync(x => x.MeetingId == s.MeetingId)).Content).Discussion);
    }

    [Fact]
    public async Task A_draft_too_long_to_store_ends_failed_rather_than_stuck_running()
    {
        f.Model.Reset();
        var s = await f.Seed();
        using var secretary = f.Client(s.Secretary);
        f.Model.PartAnswer = Prose(SummaryLimits.Discussion);
        try
        {
            await AssertCode(await secretary.PostAsync(s.Summary, null), 202, "MSG384");
            await Record.ExceptionAsync(f.ProcessAll);
            await using var db = f.Db();
            Assert.NotEqual(SummaryStatus.Running, (await db.MeetingSummaries.SingleAsync(x => x.MeetingId == s.MeetingId)).Status);
        }
        finally
        {
            f.Model.Reset();
            await using var db = f.Db();
            await db.MeetingSummaries.Where(x => x.MeetingId == s.MeetingId)
                .ExecuteUpdateAsync(u => u.SetProperty(x => x.Status, SummaryStatus.Failed));
        }
    }

    [Fact]
    public async Task Edit_and_review_need_a_written_draft()
    {
        f.Model.Reset();
        var s = await f.Seed();
        using var secretary = f.Client(s.Secretary);
        await AssertCode(await secretary.PutAsJsonAsync(s.Summary, Edit()), 409, "MSG390");
        await AssertCode(await secretary.PostAsync(s.Summary + "/review", null), 409, "MSG390");
        // Queued but not written yet: still nothing to edit or review.
        await AssertCode(await secretary.PostAsync(s.Summary, null), 202, "MSG384");
        await AssertCode(await secretary.PutAsJsonAsync(s.Summary, Edit()), 409, "MSG390");
        await AssertCode(await secretary.PostAsync(s.Summary + "/review", null), 409, "MSG390");
        await using (var db = f.Db())
            Assert.Null((await db.MeetingSummaries.SingleAsync(x => x.MeetingId == s.MeetingId)).ReviewedAt);
        await f.ProcessAll();
    }

    [Fact]
    public async Task A_rerun_keeps_the_last_draft_readable_then_replaces_it_and_its_review()
    {
        var s = await Drafted();
        using var secretary = f.Client(s.Secretary);
        using var member = f.Client(s.Member);
        await AssertCode(await secretary.PutAsJsonAsync(s.Summary, Edit()), 200, "MSG388");
        await AssertCode(await secretary.PostAsync(s.Summary + "/review", null), 200, "MSG391");

        f.Clock.Now = f.Clock.Now.AddMinutes(5);
        var queued = await AssertCode(await secretary.PostAsync(s.Summary, null), 202, "MSG384");
        Assert.Equal("Queued", queued.GetProperty("summary").GetProperty("status").GetString());
        Assert.Equal("Mục tiêu đã sửa.", queued.GetProperty("summary").GetProperty("draft").GetProperty("objectives").GetString());

        f.Model.OverviewAnswer = "MỤC TIÊU:\nMục tiêu lần hai.\nKẾT LUẬN:\nKhông có.\nNHIỆM VỤ:\nKhông có.";
        await f.ProcessAll();
        var rerun = (await State(secretary, s)).GetProperty("summary");
        Assert.Equal("Ready", rerun.GetProperty("status").GetString());
        Assert.Equal("Mục tiêu lần hai.", rerun.GetProperty("draft").GetProperty("objectives").GetString());
        Assert.Equal(JsonValueKind.Null, rerun.GetProperty("reviewedAt").ValueKind);
        Assert.Equal(JsonValueKind.Null, rerun.GetProperty("editedBy").ValueKind);
        Assert.Equal(JsonValueKind.Null, (await State(member, s)).GetProperty("summary").ValueKind);
        await using var db = f.Db();
        var row = await db.MeetingSummaries.SingleAsync(x => x.MeetingId == s.MeetingId);
        Assert.Null(row.ReviewedAt);
        Assert.Null(row.EditedAt);
        Assert.Equal(f.Clock.Now, row.GeneratedAt);
        f.Model.Reset();
    }

    [Fact]
    public async Task While_the_ai_writes_the_draft_again_it_is_neither_edited_nor_reviewed()
    {
        // The Chair's tab still shows the old draft when the Secretary asks for a new one. Saved now, the edit would be
        // replaced by the AI's text unannounced, and minutes built from it would date from after the new draft's reading.
        var s = await Drafted();
        using var secretary = f.Client(s.Secretary);
        using var chair = f.Client(s.Chair);
        f.Clock.Now = f.Clock.Now.AddMinutes(1);
        await AssertCode(await secretary.PostAsync(s.Summary, null), 202, "MSG384");
        await AssertCode(await chair.PutAsJsonAsync(s.Summary, Edit()), 409, "MSG387");
        await AssertCode(await chair.PostAsync(s.Summary + "/review", null), 409, "MSG387");

        // The same while it runs.
        HttpResponseMessage? edit = null, review = null;
        f.Model.While = async () =>
        {
            edit = await chair.PutAsJsonAsync(s.Summary, Edit());
            review = await chair.PostAsync(s.Summary + "/review", null);
        };
        await f.ProcessAll();
        await AssertCode(edit!, 409, "MSG387");
        await AssertCode(review!, 409, "MSG387");

        await using (var db = f.Db())
        {
            var row = await db.MeetingSummaries.SingleAsync(x => x.MeetingId == s.MeetingId);
            Assert.Equal(SummaryStatus.Ready, row.Status);
            Assert.Null(row.EditedAt);
            Assert.Null(row.ReviewedAt);
        }
        // Written, it can be corrected and vouched for again.
        await AssertCode(await chair.PutAsJsonAsync(s.Summary, Edit()), 200, "MSG388");
        await AssertCode(await chair.PostAsync(s.Summary + "/review", null), 200, "MSG391");
    }

    [Fact]
    public async Task Database_outages_have_their_codes()
    {
        var s = await Drafted();
        using var secretary = f.Client(s.Secretary);
        f.Failures.FailSummaryRead = true;
        try { await AssertCode(await secretary.GetAsync(s.Summary), 500, "MSG392"); }
        finally { f.Failures.FailSummaryRead = false; }

        f.Failures.FailSave = true;
        try
        {
            await AssertCode(await secretary.PutAsJsonAsync(s.Summary, Edit()), 503, "MSG393");
            await AssertCode(await secretary.PostAsync(s.Summary + "/review", null), 503, "MSG393");
        }
        finally { f.Failures.FailSave = false; }
        await using var db = f.Db();
        var row = await db.MeetingSummaries.SingleAsync(x => x.MeetingId == s.MeetingId);
        Assert.Null(row.EditedAt);
        Assert.Null(row.ReviewedAt);
    }
}
