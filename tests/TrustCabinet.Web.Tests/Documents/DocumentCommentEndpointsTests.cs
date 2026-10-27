using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using TrustCabinet.Domain.Documents;
using TrustCabinet.Domain.Entities;
using TrustCabinet.Domain.Enums;
using static TrustCabinet.Web.Tests.Documents.PersonalLibraryEndpointsTests;

namespace TrustCabinet.Web.Tests.Documents;

/// <summary>SCRUM-23 — UC-35 comments on a meeting document against PostgreSQL.</summary>
[Collection("Documents")]
public sealed class DocumentCommentEndpointsTests(DocumentFixture fixture) : IAsyncLifetime
{
    public Task InitializeAsync() { Reset(); return Task.CompletedTask; }
    public Task DisposeAsync() { Reset(); return Task.CompletedTask; }

    private void Reset()
    {
        fixture.Storage.Reset();
        fixture.SaveFailure.Enabled = false;
        fixture.SaveFailure.FailAudit = false;
        fixture.SaveFailure.When = null;
        fixture.ReadFailure.Table = null;
    }

    /// <summary>A meeting document of the given file; a PDF of 5 pages unless <paramref name="docx"/>.</summary>
    private async Task<Guid> AddMeetingDocument(Guid meetingId, Guid uploader, bool docx = false, Guid? storedFileId = null)
    {
        await using var db = fixture.Db();
        var fileId = storedFileId ?? Guid.CreateVersion7();
        if (storedFileId is null)
            db.StoredFiles.Add(new StoredFile { Id = fileId, StorageKey = DocumentFileRules.StorageKey(fileId), OriginalFileName = "x",
                ContentType = docx ? DocumentFileRules.DocxContentType : DocumentFileRules.PdfContentType, ByteSize = 10,
                ContentSha256 = new string('c', 64), PageCount = docx ? null : 5, UploadedById = uploader, UploadedAt = DateTimeOffset.UtcNow });
        var document = new MeetingDocument { MeetingId = meetingId, StoredFileId = fileId, FileName = docx ? "x.docx" : "x.pdf",
            AddedById = uploader, AddedAt = DateTimeOffset.UtcNow };
        db.MeetingDocuments.Add(document);
        await db.SaveChangesAsync();
        return document.Id;
    }

    private static string Url(DocumentFixture.Scenario s, Guid document) => $"/api/meetings/{s.MeetingId}/documents/{document}/comments";

    private static (int, string?) Pick((int Status, string? Code, JsonElement Body) result) => (result.Status, result.Code);

    [Fact]
    public async Task Member_comments_on_a_pdf_page_and_a_docx_comment_has_no_page()
    {
        var s = await fixture.Seed();
        var pdf = await AddMeetingDocument(s.MeetingId, s.Secretary);
        var docx = await AddMeetingDocument(s.MeetingId, s.Secretary, docx: true);
        using var client = fixture.Client(s.Member);

        var (status, code, body) = await Read(await client.PostAsJsonAsync(Url(s, pdf), new { body = "  Xem lại số liệu  ", pageNumber = 3 }));
        Assert.Equal((201, "MSG199"), (status, code));
        var comment = body.GetProperty("comment");
        Assert.Equal("Xem lại số liệu", comment.GetProperty("body").GetString());
        Assert.Equal(3, comment.GetProperty("pageNumber").GetInt32());
        Assert.Equal(s.Member, comment.GetProperty("author").GetProperty("userId").GetGuid());
        Assert.False(comment.GetProperty("author").GetProperty("isGuest").GetBoolean());

        var (_, _, onDocx) = await Read(await client.PostAsJsonAsync(Url(s, docx), new { body = "Ý kiến", pageNumber = 3 }));
        Assert.Equal(JsonValueKind.Null, onDocx.GetProperty("comment").GetProperty("pageNumber").ValueKind);
        // A page the PDF does not have is not kept either.
        var (_, _, beyond) = await Read(await client.PostAsJsonAsync(Url(s, pdf), new { body = "Trang 9", pageNumber = 9 }));
        Assert.Equal(JsonValueKind.Null, beyond.GetProperty("comment").GetProperty("pageNumber").ValueKind);

        var (listStatus, _, list) = await Read(await client.GetAsync(Url(s, pdf)));
        Assert.Equal(200, listStatus);
        Assert.True(list.GetProperty("canComment").GetBoolean());
        Assert.Equal(2, list.GetProperty("comments").GetArrayLength());
        var (_, _, documents) = await Read(await client.GetAsync($"/api/meetings/{s.MeetingId}/documents"));
        Assert.Equal(2, documents.GetProperty("general").EnumerateArray()
            .Single(d => d.GetProperty("id").GetGuid() == pdf).GetProperty("commentCount").GetInt32());
    }

    [Fact]
    public async Task A_reply_to_a_reply_joins_the_thread_of_the_top_level_comment()
    {
        var s = await fixture.Seed();
        var document = await AddMeetingDocument(s.MeetingId, s.Secretary);
        using var member = fixture.Client(s.Member);
        using var chair = fixture.Client(s.Chair);
        var (_, _, top) = await Read(await member.PostAsJsonAsync(Url(s, document), new { body = "Gốc", pageNumber = 2 }));
        var topId = top.GetProperty("comment").GetProperty("id").GetGuid();
        var (_, _, reply) = await Read(await chair.PostAsJsonAsync(Url(s, document), new { body = "Trả lời", parentCommentId = topId }));
        var replyId = reply.GetProperty("comment").GetProperty("id").GetGuid();

        var (status, code, _) = await Read(await member.PostAsJsonAsync(Url(s, document), new { body = "Trả lời tiếp", parentCommentId = replyId }));

        Assert.Equal((201, "MSG199"), (status, code));
        var (_, _, list) = await Read(await member.GetAsync(Url(s, document)));
        var thread = Assert.Single(list.GetProperty("comments").EnumerateArray());
        Assert.Equal(topId, thread.GetProperty("id").GetGuid());
        Assert.Equal(["Trả lời", "Trả lời tiếp"], thread.GetProperty("replies").EnumerateArray().Select(r => r.GetProperty("body").GetString()));
        Assert.All(thread.GetProperty("replies").EnumerateArray(), r => Assert.Equal(0, r.GetProperty("replies").GetArrayLength()));
        Assert.Equal((404, "MSG156"), Pick(await Read(await member.PostAsJsonAsync(Url(s, document),
            new { body = "Lạc", parentCommentId = Guid.NewGuid() }))));
    }

    [Fact]
    public async Task A_comment_must_have_text_within_the_limit()
    {
        var s = await fixture.Seed();
        var document = await AddMeetingDocument(s.MeetingId, s.Secretary);
        using var client = fixture.Client(s.Member);

        Assert.Equal((400, "MSG200"), Pick(await Read(await client.PostAsJsonAsync(Url(s, document), new { body = "   " }))));
        Assert.Equal((400, "MSG348"), Pick(await Read(await client.PostAsJsonAsync(Url(s, document),
            new { body = new string('x', DocumentFileRules.MaxCommentLength + 1) }))));
    }

    [Fact]
    public async Task Comments_are_read_only_once_the_meeting_has_ended()
    {
        var s = await fixture.Seed(MeetingStatus.Ended);
        var document = await AddMeetingDocument(s.MeetingId, s.Secretary);
        using var client = fixture.Client(s.Member);

        Assert.Equal((409, "MSG251"), Pick(await Read(await client.PostAsJsonAsync(Url(s, document), new { body = "Muộn" }))));
        var (status, _, list) = await Read(await client.GetAsync(Url(s, document)));
        Assert.Equal(200, status);
        Assert.False(list.GetProperty("canComment").GetBoolean());
    }

    [Fact]
    public async Task Comments_are_read_only_once_the_meeting_is_cancelled()
    {
        var s = await fixture.Seed(MeetingStatus.Cancelled);
        await using (var db = fixture.Db())
            await db.Meetings.Where(m => m.Id == s.MeetingId)
                .ExecuteUpdateAsync(m => m.SetProperty(x => x.CancelledFromStatus, MeetingStatus.Published));
        var document = await AddMeetingDocument(s.MeetingId, s.Secretary);
        using var client = fixture.Client(s.Member);

        Assert.Equal((409, "MSG349"), Pick(await Read(await client.PostAsJsonAsync(Url(s, document), new { body = "Đã huỷ" }))));
        var (status, _, list) = await Read(await client.GetAsync(Url(s, document)));
        Assert.Equal(200, status);
        Assert.False(list.GetProperty("canComment").GetBoolean());
    }

    [Fact]
    public async Task Organizer_and_approver_comment_as_anyone_who_views_the_meeting()
    {
        var s = await fixture.Seed();
        await using (var db = fixture.Db())
            await db.Meetings.Where(m => m.Id == s.MeetingId).ExecuteUpdateAsync(m => m.SetProperty(x => x.AssignedApproverId, s.Outsider));
        var document = await AddMeetingDocument(s.MeetingId, s.Secretary);

        foreach (var actor in new[] { s.Organizer, s.Outsider })
        {
            using var client = fixture.Client(actor);
            Assert.Equal((201, "MSG199"), Pick(await Read(await client.PostAsJsonAsync(Url(s, document), new { body = "Ý kiến" }))));
        }
        await using var check = fixture.Db();
        Assert.Equal(2, await check.DocumentComments.CountAsync(c => c.MeetingDocumentId == document));
    }

    [Fact]
    public async Task Outsiders_removed_documents_and_other_meetings_get_the_same_answer()
    {
        var s = await fixture.Seed();
        var document = await AddMeetingDocument(s.MeetingId, s.Secretary);
        var other = await fixture.Seed();
        var elsewhere = await AddMeetingDocument(other.MeetingId, other.Secretary);
        using var member = fixture.Client(s.Member);
        await member.PostAsJsonAsync(Url(s, document), new { body = "Trước khi gỡ" });

        using (var outsider = fixture.Client(s.Outsider))
        {
            Assert.Equal((404, "MSG156"), Pick(await Read(await outsider.GetAsync(Url(s, document)))));
            Assert.Equal((404, "MSG156"), Pick(await Read(await outsider.PostAsJsonAsync(Url(s, document), new { body = "x" }))));
        }
        Assert.Equal((404, "MSG156"), Pick(await Read(await member.GetAsync(Url(s, elsewhere)))));
        await using (var db = fixture.Db())
            await db.MeetingDocuments.Where(d => d.Id == document)
                .ExecuteUpdateAsync(d => d.SetProperty(x => x.RemovedAt, DateTimeOffset.UtcNow).SetProperty(x => x.RemovedById, s.Secretary));
        // Kept as history, no longer shown (BR-284).
        Assert.Equal((404, "MSG156"), Pick(await Read(await member.GetAsync(Url(s, document)))));
        Assert.Equal((404, "MSG156"), Pick(await Read(await member.PostAsJsonAsync(Url(s, document), new { body = "Sau khi gỡ" }))));
        await using var check = fixture.Db();
        Assert.Equal(1, await check.DocumentComments.CountAsync(c => c.MeetingDocumentId == document));
    }

    [Fact]
    public async Task The_same_file_in_two_meetings_has_two_separate_discussions()
    {
        var s = await fixture.Seed();
        var first = await AddMeetingDocument(s.MeetingId, s.Secretary);
        Guid file;
        await using (var db = fixture.Db()) file = await db.MeetingDocuments.Where(d => d.Id == first).Select(d => d.StoredFileId).SingleAsync();
        var other = await fixture.Seed();
        var second = await AddMeetingDocument(other.MeetingId, other.Secretary, storedFileId: file);
        using var member = fixture.Client(s.Member);
        await member.PostAsJsonAsync(Url(s, first), new { body = "Ở cuộc họp A" });

        using var otherMember = fixture.Client(other.Member);
        var (_, _, list) = await Read(await otherMember.GetAsync(Url(other, second)));

        Assert.Equal(0, list.GetProperty("comments").GetArrayLength());
    }

    [Fact]
    public async Task Failures_to_save_or_load_answer_their_own_codes()
    {
        var s = await fixture.Seed();
        var document = await AddMeetingDocument(s.MeetingId, s.Secretary);
        using var client = fixture.Client(s.Member);

        fixture.SaveFailure.When = db => db.ChangeTracker.Entries<DocumentComment>().Any(e => e.State == EntityState.Added);
        Assert.Equal((503, "MSG202"), Pick(await Read(await client.PostAsJsonAsync(Url(s, document), new { body = "Lỗi" }))));
        fixture.SaveFailure.When = null;
        fixture.ReadFailure.Table = "document_comments";
        Assert.Equal((500, "MSG204"), Pick(await Read(await client.GetAsync(Url(s, document)))));
        fixture.ReadFailure.Table = null;
        await using var db = fixture.Db();
        Assert.False(await db.DocumentComments.AnyAsync(c => c.MeetingDocumentId == document));
    }
}
