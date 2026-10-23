using System.Net;
using System.Security.Cryptography;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using TrustCabinet.Domain.Documents;
using TrustCabinet.Domain.Entities;
using TrustCabinet.Domain.Enums;
using TrustCabinet.Domain.Participants;
using static TrustCabinet.Web.Tests.Agenda.AgendaEndpointTests;

namespace TrustCabinet.Web.Tests.Documents;

/// <summary>SCRUM-107 — POST /api/meetings/{id}/documents/upload against PostgreSQL.</summary>
[Collection("Documents")]
public sealed class MeetingDocumentEndpointsTests(DocumentFixture fixture) : IAsyncLifetime
{
    public Task InitializeAsync() { fixture.Storage.Reset(); fixture.SaveFailure.Enabled = false; return Task.CompletedTask; }
    public Task DisposeAsync() { fixture.Storage.Reset(); fixture.SaveFailure.Enabled = false; return Task.CompletedTask; }

    [Fact]
    public async Task Secretary_uploads_three_pdfs_and_a_docx_into_library_folder_and_meeting()
    {
        var s = await fixture.Seed();
        using var client = fixture.Client(s.Secretary);
        var files = new[] { ("Báo cáo Q3.pdf", SampleFiles.Pdf("a"), "12"), ("Tờ trình.pdf", SampleFiles.Pdf("b"), "3"),
            ("Phụ lục.pdf", SampleFiles.Pdf("c"), null), ("Dự thảo.docx", SampleFiles.Docx(), "7") };

        var body = await AssertCode(await client.PostAsync(s.Url, DocumentFixture.Form(files)), 201, "MSG161");

        Assert.Equal(4, body.GetProperty("addedCount").GetInt32());
        await using var db = fixture.Db();
        var meetingDocuments = await db.MeetingDocuments.Include(d => d.StoredFile).Include(d => d.Document)
            .Where(d => d.MeetingId == s.MeetingId).ToListAsync();
        Assert.Equal(4, meetingDocuments.Count);
        Assert.Equal(4, meetingDocuments.Select(d => d.StoredFileId).Distinct().Count());
        var folder = await db.Folders.SingleAsync(f => f.OwnerId == s.Secretary);
        Assert.Equal("Họp giao ban – 24/10/2026", folder.Name);
        Assert.Equal(s.MeetingId, folder.MeetingId);
        Assert.Null(folder.ParentId);
        foreach (var (name, bytes, pages) in files)
        {
            var row = meetingDocuments.Single(d => d.FileName == name);
            // Library row and meeting row share one stored file: no copy (ADR-0018, BR-210).
            Assert.Equal(row.StoredFileId, row.Document!.StoredFileId);
            Assert.Equal(folder.Id, row.Document.FolderId);
            Assert.Equal(s.Secretary, row.Document.OwnerId);
            Assert.Empty(await db.MeetingDocumentAgendaItems.Where(l => l.MeetingDocumentId == row.Id).ToListAsync());
            var file = row.StoredFile;
            Assert.Equal($"stored-files/{file.Id:D}", file.StorageKey);
            Assert.Equal(name, file.OriginalFileName);
            Assert.Equal(bytes.Length, file.ByteSize);
            Assert.Equal(Convert.ToHexStringLower(SHA256.HashData(bytes)), file.ContentSha256);
            Assert.Equal(s.Secretary, file.UploadedById);
            Assert.Null(file.PurgedAt);
            // Stored exactly as received (BR-183, BR-216).
            Assert.Equal(bytes, fixture.Storage.Objects[file.StorageKey]);
            var docx = name.EndsWith(".docx");
            Assert.Equal(docx ? DocumentFileRules.DocxContentType : DocumentFileRules.PdfContentType, file.ContentType);
            Assert.Equal(docx || pages is null ? null : int.Parse(pages), file.PageCount);
        }
        Assert.Equal(meetingDocuments.Select(d => d.Id).Order(),
            body.GetProperty("documents").EnumerateArray().Select(d => d.GetProperty("id").GetGuid()).Order());
    }

    [Fact]
    public async Task Chair_may_upload_too()
    {
        var s = await fixture.Seed(MeetingStatus.Draft);
        using var client = fixture.Client(s.Chair);
        await AssertCode(await client.PostAsync(s.Url, DocumentFixture.Form(("a.pdf", SampleFiles.Pdf(), "1"))), 201, "MSG161");
    }

    [Theory]
    [InlineData("anh.pdf")]       // a PNG renamed to .pdf
    [InlineData("macro.docx")]    // a macro-enabled document renamed to .docx
    [InlineData("broken.docx")]   // a zip without word/document.xml
    public async Task A_file_that_is_not_really_pdf_or_docx_rejects_the_whole_submission(string badName)
    {
        var s = await fixture.Seed();
        using var client = fixture.Client(s.Secretary);
        var bad = badName switch
        {
            "anh.pdf" => SampleFiles.Png(),
            "macro.docx" => SampleFiles.Docx("application/vnd.ms-word.document.macroEnabled.main+xml"),
            _ => SampleFiles.Docx(withDocument: false),
        };
        var objects = fixture.Storage.Objects.Count;

        var body = await AssertCode(await client.PostAsync(s.Url,
            DocumentFixture.Form(("ok.pdf", SampleFiles.Pdf(), "1"), (badName, bad, null))), 400, "MSG164");

        var error = Assert.Single(body.GetProperty("errors").EnumerateObject());
        Assert.Equal("files[1]", error.Name);
        Assert.Contains(badName, error.Value[0].GetString());
        await AssertNothingAdded(s, objects);
    }

    [Fact]
    public async Task Eleven_files_is_over_the_submission_limit()
    {
        var s = await fixture.Seed();
        using var client = fixture.Client(s.Secretary);
        var objects = fixture.Storage.Objects.Count;
        var files = Enumerable.Range(0, 11).Select(i => ($"{i}.pdf", SampleFiles.Pdf(), (string?)"1")).ToArray();

        await AssertCode(await client.PostAsync(s.Url, DocumentFixture.Form(files)), 400, "MSG288");
        await AssertNothingAdded(s, objects);
    }

    [Fact]
    public async Task A_request_declaring_more_than_200_MB_is_refused_before_it_is_read()
    {
        var s = await fixture.Seed();
        using var client = fixture.Client(s.Secretary);
        var form = new MultipartFormDataContent();
        var big = new StreamContent(new ZeroStream(DocumentFileRules.MaxTotalBytes + 2 * 1024 * 1024));
        form.Add(big, "files", "big.pdf");

        await AssertCode(await client.PostAsync(s.Url, form), 400, "MSG288");
        await AssertNothingAdded(s, fixture.Storage.Objects.Count);
    }

    [Fact]
    public async Task A_file_over_50_MB_is_invalid()
    {
        var s = await fixture.Seed();
        using var client = fixture.Client(s.Secretary);
        var bytes = new byte[DocumentFileRules.MaxFileBytes + 1];
        SampleFiles.Pdf().CopyTo(bytes, 0);

        var body = await AssertCode(await client.PostAsync(s.Url, DocumentFixture.Form(("lon.pdf", bytes, "1"))), 400, "MSG164");
        Assert.Contains("50 MB", body.GetProperty("errors").GetProperty("files[0]")[0].GetString());
    }

    [Fact]
    public async Task No_file_or_a_body_that_is_not_multipart_is_invalid()
    {
        var s = await fixture.Seed();
        using var client = fixture.Client(s.Secretary);
        await AssertCode(await client.PostAsync(s.Url, new MultipartFormDataContent { { new StringContent("1"), "pageCounts" } }), 400, "MSG164");
        await AssertCode(await client.PostAsync(s.Url, new StringContent("{}", System.Text.Encoding.UTF8, "application/json")), 400, "MSG164");
    }

    [Fact]
    public async Task Member_and_organizer_without_a_managing_role_are_refused_outsiders_get_404()
    {
        var s = await fixture.Seed();
        var form = () => DocumentFixture.Form(("a.pdf", SampleFiles.Pdf(), "1"));
        using (var member = fixture.Client(s.Member))
            await AssertCode(await member.PostAsync(s.Url, form()), 403, "MSG162");
        // The organizer can see the meeting, but adding documents belongs to its Chair and Secretary (ADR-0018 §6).
        using (var organizer = fixture.Client(s.Organizer))
            await AssertCode(await organizer.PostAsync(s.Url, form()), 403, "MSG162");
        using var outsider = fixture.Client(s.Outsider);
        var hidden = await AssertCode(await outsider.PostAsync(s.Url, form()), 404, "MSG174");
        var missing = await AssertCode(await outsider.PostAsync($"/api/meetings/{Guid.CreateVersion7()}/documents/upload", form()), 404, "MSG174");
        // An existing meeting and a missing one look the same from outside (SCRUM-22).
        Assert.Equal(missing.GetRawText(), hidden.GetRawText());
        await AssertNothingAdded(s, fixture.Storage.Objects.Count);
    }

    [Fact]
    public async Task A_secretary_who_was_removed_from_the_meeting_loses_access_and_an_inactive_account_gets_403()
    {
        var s = await fixture.Seed(MeetingStatus.Draft);
        await using (var db = fixture.Db())
        {
            var participant = await db.MeetingParticipants.SingleAsync(p => p.MeetingId == s.MeetingId && p.UserId == s.Secretary);
            participant.AssignmentStatus = AssignmentStatus.Removed;
            participant.RemovedAt = DateTimeOffset.UtcNow;
            participant.RemovedById = s.Organizer;
            (await db.Users.SingleAsync(u => u.Id == s.Chair)).IsActive = false;
            await db.SaveChangesAsync();
        }
        using (var secretary = fixture.Client(s.Secretary))
            // No longer a participant of a Draft meeting, so it is not visible either.
            await AssertCode(await secretary.PostAsync(s.Url, DocumentFixture.Form(("a.pdf", SampleFiles.Pdf(), "1"))), 404, "MSG174");
        using var chair = fixture.Client(s.Chair);
        var response = await chair.PostAsync(s.Url, DocumentFixture.Form(("a.pdf", SampleFiles.Pdf(), "1")));
        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
        Assert.Empty(await response.Content.ReadAsStringAsync());
    }

    [Fact]
    public async Task Ended_meeting_refuses_uploads()
    {
        var s = await fixture.Seed(MeetingStatus.Ended);
        using var client = fixture.Client(s.Secretary);
        await AssertCode(await client.PostAsync(s.Url, DocumentFixture.Form(("a.pdf", SampleFiles.Pdf(), "1"))), 409, "MSG172");
        await AssertNothingAdded(s, fixture.Storage.Objects.Count);
    }

    [Fact]
    public async Task Meeting_that_ends_while_files_upload_is_refused_and_its_objects_deleted()
    {
        var s = await fixture.Seed();
        var objects = fixture.Storage.Objects.Count;
        fixture.Storage.BeforeWrite = async _ =>
        {
            await using var db = fixture.Db();
            await db.Meetings.Where(m => m.Id == s.MeetingId).ExecuteUpdateAsync(u => u.SetProperty(m => m.Status, MeetingStatus.Ended));
        };
        using var client = fixture.Client(s.Secretary);

        await AssertCode(await client.PostAsync(s.Url, DocumentFixture.Form(("a.pdf", SampleFiles.Pdf(), "1"), ("b.pdf", SampleFiles.Pdf(), "1"))), 409, "MSG172");
        await AssertNothingAdded(s, objects);
    }

    [Fact]
    public async Task Store_failure_on_the_second_file_deletes_the_first_and_adds_nothing()
    {
        var s = await fixture.Seed();
        var objects = fixture.Storage.Objects.Count;
        fixture.Storage.BeforeWrite = n => n == 2 ? throw new IOException("Injected store failure.") : Task.CompletedTask;
        using var client = fixture.Client(s.Secretary);

        await AssertCode(await client.PostAsync(s.Url, DocumentFixture.Form(("a.pdf", SampleFiles.Pdf(), "1"), ("b.pdf", SampleFiles.Pdf(), "1"))), 503, "MSG166");
        await AssertNothingAdded(s, objects);
    }

    [Fact]
    public async Task Database_failure_after_the_objects_are_up_deletes_every_one_of_them()
    {
        var s = await fixture.Seed();
        var objects = fixture.Storage.Objects.Count;
        fixture.SaveFailure.Enabled = true;
        using var client = fixture.Client(s.Secretary);

        await AssertCode(await client.PostAsync(s.Url, DocumentFixture.Form(("a.pdf", SampleFiles.Pdf(), "1"), ("b.docx", SampleFiles.Docx(), null))), 503, "MSG166");
        fixture.SaveFailure.Enabled = false;
        await AssertNothingAdded(s, objects);
    }

    [Fact]
    public async Task Cleanup_that_cannot_delete_still_answers_MSG166()
    {
        var s = await fixture.Seed();
        fixture.SaveFailure.Enabled = true;
        fixture.Storage.FailDelete = true;
        using var client = fixture.Client(s.Secretary);

        await AssertCode(await client.PostAsync(s.Url, DocumentFixture.Form(("a.pdf", SampleFiles.Pdf(), "1"))), 503, "MSG166");
        fixture.SaveFailure.Enabled = false;
        await using var db = fixture.Db();
        Assert.False(await db.MeetingDocuments.AnyAsync(d => d.MeetingId == s.MeetingId));
    }

    [Fact]
    public async Task Without_a_configured_store_uploads_answer_MSG166()
    {
        var s = await fixture.Seed();
        fixture.Storage.Configured = false;
        using var client = fixture.Client(s.Secretary);
        await AssertCode(await client.PostAsync(s.Url, DocumentFixture.Form(("a.pdf", SampleFiles.Pdf(), "1"))), 503, "MSG166");
    }

    [Fact]
    public async Task Later_uploads_reuse_the_folder_even_renamed_and_moved_and_recreate_it_once_deleted()
    {
        var s = await fixture.Seed();
        using var client = fixture.Client(s.Secretary);
        var same = SampleFiles.Pdf("same");
        await AssertCode(await client.PostAsync(s.Url, DocumentFixture.Form(("a.pdf", same, "1"))), 201, "MSG161");
        Guid first;
        await using (var db = fixture.Db())
        {
            var folder = await db.Folders.SingleAsync(f => f.OwnerId == s.Secretary);
            first = folder.Id;
            var parent = new Folder { OwnerId = s.Secretary, Name = "Lưu trữ" };
            db.Folders.Add(parent);
            folder.Name = "Đã đổi tên";
            folder.ParentId = parent.Id;
            await db.SaveChangesAsync();
        }

        // Same name, same bytes: still a second document (BR-229).
        await AssertCode(await client.PostAsync(s.Url, DocumentFixture.Form(("a.pdf", same, "1"))), 201, "MSG161");
        await using (var db = fixture.Db())
        {
            var documents = await db.Documents.Where(d => d.OwnerId == s.Secretary).ToListAsync();
            Assert.Equal(2, documents.Count);
            Assert.All(documents, d => Assert.Equal(first, d.FolderId));
            Assert.Equal(2, await db.MeetingDocuments.CountAsync(d => d.MeetingId == s.MeetingId));
            Assert.Equal(2, documents.Select(d => d.StoredFileId).Distinct().Count());

            // Deleting the folder is UC-72d (SCRUM-58); emulate it: library rows go, meeting rows stay.
            db.Documents.RemoveRange(documents);
            await db.SaveChangesAsync();
            await db.Folders.Where(f => f.Id == first).ExecuteDeleteAsync();
        }

        await AssertCode(await client.PostAsync(s.Url, DocumentFixture.Form(("c.pdf", SampleFiles.Pdf(), "1"))), 201, "MSG161");
        await using var check = fixture.Db();
        var recreated = await check.Folders.SingleAsync(f => f.OwnerId == s.Secretary && f.MeetingId == s.MeetingId);
        Assert.NotEqual(first, recreated.Id);
        Assert.Null(recreated.ParentId);
        Assert.Equal("Họp giao ban – 24/10/2026", recreated.Name);
        Assert.Equal(3, await check.MeetingDocuments.CountAsync(d => d.MeetingId == s.MeetingId));
    }

    [Fact]
    public async Task Agenda_lists_uploaded_documents_for_linking()
    {
        var s = await fixture.Seed(MeetingStatus.Draft);
        using var client = fixture.Client(s.Secretary);
        var upload = await AssertCode(await client.PostAsync(s.Url, DocumentFixture.Form(("a.pdf", SampleFiles.Pdf(), "1"))), 201, "MSG161");
        var id = upload.GetProperty("documents")[0].GetProperty("id").GetGuid();

        var options = JsonDocument.Parse(await client.GetStringAsync($"/api/meetings/{s.MeetingId}/agenda/editor-options")).RootElement;
        var document = Assert.Single(options.GetProperty("documents").EnumerateArray());
        Assert.Equal(id, document.GetProperty("id").GetGuid());
        Assert.Equal("a.pdf", document.GetProperty("title").GetString());
    }

    private async Task AssertNothingAdded(DocumentFixture.Scenario s, int objectsBefore)
    {
        await using var db = fixture.Db();
        Assert.False(await db.MeetingDocuments.AnyAsync(d => d.MeetingId == s.MeetingId));
        Assert.False(await db.StoredFiles.AnyAsync(f => f.UploadedById == s.Secretary));
        Assert.False(await db.Documents.AnyAsync(d => d.OwnerId == s.Secretary));
        Assert.False(await db.Folders.AnyAsync(f => f.MeetingId == s.MeetingId));
        Assert.Equal(objectsBefore, fixture.Storage.Objects.Count);
    }

    /// <summary>A long run of zero bytes that is never held in memory.</summary>
    private sealed class ZeroStream(long length) : Stream
    {
        private long _position;
        public override bool CanRead => true;
        public override bool CanSeek => true;
        public override bool CanWrite => false;
        public override long Length => length;
        public override long Position { get => _position; set => _position = value; }
        public override int Read(byte[] buffer, int offset, int count)
        {
            var n = (int)Math.Min(count, length - _position);
            Array.Clear(buffer, offset, n);
            _position += n;
            return n;
        }
        public override long Seek(long offset, SeekOrigin origin) => _position = origin switch
        {
            SeekOrigin.Begin => offset, SeekOrigin.Current => _position + offset, _ => length + offset,
        };
        public override void Flush() { }
        public override void SetLength(long value) => throw new NotSupportedException();
        public override void Write(byte[] buffer, int offset, int count) => throw new NotSupportedException();
    }
}
