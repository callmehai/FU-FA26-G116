using System.IdentityModel.Tokens.Jwt;
using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Security.Claims;
using System.Text;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;
using TrustCabinet.Application.GuestAccess;
using TrustCabinet.Domain.Access;
using TrustCabinet.Domain.Documents;
using TrustCabinet.Domain.Entities;
using TrustCabinet.Domain.Enums;
using TrustCabinet.Web.Tests.Agenda;
using static TrustCabinet.Web.Tests.Agenda.AgendaEndpointTests;
using static TrustCabinet.Web.Tests.Documents.PersonalLibraryEndpointsTests;

namespace TrustCabinet.Web.Tests.Documents;

/// <summary>Round 1 gap tests for FE-07 Documents: the cases of cases/FE-07/api.json
/// that had no automated test (DOC_API_IT05, IT10–IT11, IT21, IT23, IT27–IT28, IT33, IT36,
/// IT42, IT46, IT48, IT56, IT62, IT72, IT79, IT84, IT88, IT90, IT101, IT114, IT131,
/// IT142–IT143, IT152, IT161, IT168, IT172, IT176–IT177, IT184, IT192, IT207, IT217).</summary>
[Collection("Documents")]
public sealed class DocumentRound1GapTests(DocumentFixture fixture) : IAsyncLifetime
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

    private HttpClient Anonymous() => fixture.Host.CreateClient();

    private static string GuestToken()
    {
        var session = Guid.CreateVersion7();
        return new JwtSecurityTokenHandler().WriteToken(new JwtSecurityToken("TrustCabinet", "TrustCabinet",
            [new Claim("sub", GuestTokenClaims.SubjectPrefix + session.ToString("D")),
                new Claim(GuestTokenClaims.Meeting, Guid.CreateVersion7().ToString()),
                new Claim(GuestTokenClaims.Invitation, Guid.CreateVersion7().ToString()),
                new Claim(GuestTokenClaims.Session, session.ToString("D"))],
            expires: DateTime.UtcNow.AddMinutes(10),
            signingCredentials: new SigningCredentials(
                new SymmetricSecurityKey(Encoding.UTF8.GetBytes(AgendaFixture.SigningKey)),
                SecurityAlgorithms.HmacSha256)));
    }

    private async Task<Guid> AddItem(Guid meetingId, int position = 1)
    {
        await using var db = fixture.Db();
        var item = new AgendaItem { MeetingId = meetingId, Title = $"Mục {position}", Position = position };
        db.AgendaItems.Add(item);
        await db.SaveChangesAsync();
        return item.Id;
    }

    /// <summary>A meeting document with its stored file, like the read tests keep it.</summary>
    private async Task<Guid> AddMeetingDocument(Guid meetingId, Guid uploader, string name)
    {
        await using var db = fixture.Db();
        var bytes = SampleFiles.Pdf(name);
        var fileId = Guid.CreateVersion7();
        var document = new MeetingDocument
        {
            MeetingId = meetingId, FileName = name, AddedById = uploader, AddedAt = DateTimeOffset.UtcNow,
            StoredFile = new StoredFile { Id = fileId, StorageKey = DocumentFileRules.StorageKey(fileId),
                OriginalFileName = name, ContentType = DocumentFileRules.PdfContentType, ByteSize = bytes.Length,
                ContentSha256 = new string('a', 64), PageCount = 1, UploadedById = uploader,
                UploadedAt = DateTimeOffset.UtcNow },
        };
        db.MeetingDocuments.Add(document);
        await db.SaveChangesAsync();
        fixture.Storage.Objects[document.StoredFile.StorageKey] = bytes;
        return document.Id;
    }

    private async Task<HttpClient> AdminClient()
    {
        var admin = await NewUser(fixture, role: BuiltInRoles.Admin);
        await using var db = fixture.Db();
        if (!await db.RoleCapabilities.AnyAsync(c => c.RoleId == BuiltInRoles.Admin && c.Capability == Capability.AdminUsers))
            db.RoleCapabilities.Add(new RoleCapability { RoleId = BuiltInRoles.Admin, Capability = Capability.AdminUsers });
        await db.SaveChangesAsync();
        var token = new JwtSecurityTokenHandler().WriteToken(new JwtSecurityToken("TrustCabinet", "TrustCabinet",
            [new Claim("sub", admin.ToString()), new Claim("cap", nameof(Capability.AdminUsers))],
            expires: DateTime.UtcNow.AddMinutes(10),
            signingCredentials: new SigningCredentials(
                new SymmetricSecurityKey(Encoding.UTF8.GetBytes(AgendaFixture.SigningKey)),
                SecurityAlgorithms.HmacSha256)));
        var client = fixture.Host.CreateClient();
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);
        return client;
    }

    // ---- GET /api/documents ----

    [Fact]
    public async Task Empty_folder_lists_no_documents_and_no_error()
    {
        var owner = await NewUser(fixture);
        var folder = await AddFolder(fixture, owner, "Trống");
        using var client = fixture.Client(owner);

        var body = await client.GetFromJsonAsync<JsonElement>($"/api/documents?folderId={folder}");

        Assert.Empty(body.GetProperty("documents").EnumerateArray());
        Assert.False(body.TryGetProperty("code", out _));
    }

    [Fact]
    public async Task Library_without_a_token_answers_401()
    {
        using var anonymous = Anonymous();
        Assert.Equal(HttpStatusCode.Unauthorized, (await anonymous.GetAsync("/api/documents")).StatusCode);
    }

    [Fact]
    public async Task Guest_token_is_refused_on_the_library_with_an_empty_403()
    {
        using var guest = Anonymous();
        guest.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", GuestToken());

        using var denied = await guest.GetAsync("/api/documents");
        Assert.Equal(HttpStatusCode.Forbidden, denied.StatusCode);
        Assert.Empty(await denied.Content.ReadAsStringAsync());
    }

    // ---- POST /api/documents/upload ----

    [Fact]
    public async Task Library_upload_without_a_token_answers_401()
    {
        using var anonymous = Anonymous();
        Assert.Equal(HttpStatusCode.Unauthorized,
            (await anonymous.PostAsync("/api/documents/upload", new MultipartFormDataContent())).StatusCode);
    }

    // ---- GET /api/documents/{id}/content ----

    [Fact]
    public async Task Owner_downloads_their_own_file_with_the_same_bytes()
    {
        var owner = await NewUser(fixture);
        var document = await AddDocument(fixture, owner, "Bàn giao.pdf");
        using var client = fixture.Client(owner);

        using var response = await client.GetAsync($"/api/documents/{document}/content?download=true");
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        await using var db = fixture.Db();
        var key = await db.Documents.Where(d => d.Id == document).Select(d => d.StoredFile!.StorageKey).SingleAsync();
        Assert.Equal(fixture.Storage.Objects[key], await response.Content.ReadAsByteArrayAsync());
        Assert.Contains("attachment", response.Content.Headers.ContentDisposition?.DispositionType);
        Assert.Equal("no-store, private", response.Headers.CacheControl?.ToString());
        Assert.Equal("nosniff", string.Join(",", response.Headers.GetValues("X-Content-Type-Options")));
    }

    [Fact]
    public async Task Download_answers_MSG160_when_the_store_is_not_configured()
    {
        var owner = await NewUser(fixture);
        var document = await AddDocument(fixture, owner, "Bàn giao.pdf");
        fixture.Storage.Configured = false;
        try
        {
            using var client = fixture.Client(owner);
            var body = await AssertCode(
                await client.GetAsync($"/api/documents/{document}/content?download=true"), 503, "MSG160");
            Assert.Equal("Không tải về được tài liệu. Vui lòng thử lại.", body.GetProperty("message").GetString());
        }
        finally
        {
            fixture.Storage.Reset();
        }
    }

    [Fact]
    public async Task Library_content_without_a_token_answers_401()
    {
        using var anonymous = Anonymous();
        Assert.Equal(HttpStatusCode.Unauthorized,
            (await anonymous.GetAsync($"/api/documents/{Guid.CreateVersion7()}/content")).StatusCode);
    }

    // ---- PUT /api/documents/{id}/name ----

    [Fact]
    public async Task Rename_rejects_256_characters_with_MSG347_and_accepts_255()
    {
        var owner = await NewUser(fixture);
        var document = await AddDocument(fixture, owner, "Bàn giao.pdf");
        using var client = fixture.Client(owner);

        await AssertCode(await client.PutAsJsonAsync($"/api/documents/{document}/name",
            new { fileName = new string('a', 252) + ".pdf" }), 400, "MSG347");
        var body = await AssertCode(await client.PutAsJsonAsync($"/api/documents/{document}/name",
            new { fileName = new string('a', 251) + ".pdf" }), 200, "MSG183");
        Assert.Equal(new string('a', 251) + ".pdf", body.GetProperty("document").GetProperty("fileName").GetString());
    }

    [Fact]
    public async Task Rename_without_a_token_answers_401()
    {
        using var anonymous = Anonymous();
        Assert.Equal(HttpStatusCode.Unauthorized,
            (await anonymous.PutAsJsonAsync($"/api/documents/{Guid.CreateVersion7()}/name",
                new { fileName = "x.pdf" })).StatusCode);
    }

    // ---- PUT /api/documents/{id}/folder ----

    [Fact]
    public async Task Move_without_a_token_answers_401()
    {
        using var anonymous = Anonymous();
        Assert.Equal(HttpStatusCode.Unauthorized,
            (await anonymous.PutAsJsonAsync($"/api/documents/{Guid.CreateVersion7()}/folder",
                new { folderId = (Guid?)null })).StatusCode);
    }

    // ---- DELETE /api/documents/{id} ----

    [Fact]
    public async Task Deleting_twice_answers_MSG175_the_second_time()
    {
        var owner = await NewUser(fixture);
        var document = await AddDocument(fixture, owner, "Bàn giao.pdf");
        using var client = fixture.Client(owner);

        await AssertCode(await client.DeleteAsync($"/api/documents/{document}"), 200, "MSG180");
        await AssertCode(await client.DeleteAsync($"/api/documents/{document}"), 404, "MSG175");
    }

    [Fact]
    public async Task Delete_without_a_token_answers_401()
    {
        using var anonymous = Anonymous();
        Assert.Equal(HttpStatusCode.Unauthorized,
            (await anonymous.DeleteAsync($"/api/documents/{Guid.CreateVersion7()}")).StatusCode);
    }

    // ---- /api/folders ----

    [Fact]
    public async Task Create_folder_without_a_token_answers_401()
    {
        using var anonymous = Anonymous();
        Assert.Equal(HttpStatusCode.Unauthorized,
            (await anonymous.PostAsJsonAsync("/api/folders", new { name = "Mới" })).StatusCode);
    }

    [Fact]
    public async Task Rename_folder_without_a_token_answers_401()
    {
        using var anonymous = Anonymous();
        Assert.Equal(HttpStatusCode.Unauthorized,
            (await anonymous.PutAsJsonAsync($"/api/folders/{Guid.CreateVersion7()}/name",
                new { name = "Mới" })).StatusCode);
    }

    [Fact]
    public async Task Move_folder_without_a_token_answers_401()
    {
        using var anonymous = Anonymous();
        Assert.Equal(HttpStatusCode.Unauthorized,
            (await anonymous.PutAsJsonAsync($"/api/folders/{Guid.CreateVersion7()}/parent",
                new { parentId = (Guid?)null })).StatusCode);
    }

    [Fact]
    public async Task Delete_folder_without_a_token_answers_401()
    {
        using var anonymous = Anonymous();
        Assert.Equal(HttpStatusCode.Unauthorized,
            (await anonymous.DeleteAsync($"/api/folders/{Guid.CreateVersion7()}")).StatusCode);
    }

    // ---- GET /api/users/{id}/library ----

    [Fact]
    public async Task Admin_library_without_a_token_answers_401()
    {
        using var anonymous = Anonymous();
        var user = Guid.CreateVersion7();
        Assert.Equal(HttpStatusCode.Unauthorized,
            (await anonymous.GetAsync($"/api/users/{user}/library")).StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized,
            (await anonymous.GetAsync($"/api/users/{user}/library/documents/{Guid.CreateVersion7()}/content"))
            .StatusCode);
    }

    [Fact]
    public async Task Missing_object_in_a_deactivated_library_is_MSG382_without_audit()
    {
        var former = await NewUser(fixture, active: false);
        var document = await AddDocument(fixture, former, "Bàn giao.pdf");
        await using (var db = fixture.Db())
        {
            var storedId = await db.Documents.Where(d => d.Id == document).Select(d => d.StoredFileId).SingleAsync();
            fixture.Storage.Objects.TryRemove(DocumentFileRules.StorageKey(storedId), out _);
        }
        using var admin = await AdminClient();

        var body = await AssertCode(
            await admin.GetAsync($"/api/users/{former}/library/documents/{document}/content"), 410, "MSG382");
        Assert.Equal("Tệp của tài liệu này không còn trên kho lưu trữ nên không mở được. Hãy báo quản trị viên.",
            body.GetProperty("message").GetString());
        await using var check = fixture.Db();
        Assert.False(await check.AuditLog.AnyAsync(a => a.ObjectId == document));
    }

    // ---- GET /api/meetings/{id}/documents ----

    [Fact]
    public async Task Meeting_documents_without_a_token_answers_401()
    {
        using var anonymous = Anonymous();
        Assert.Equal(HttpStatusCode.Unauthorized,
            (await anonymous.GetAsync($"/api/meetings/{Guid.CreateVersion7()}/documents")).StatusCode);
    }

    // ---- POST /api/meetings/{id}/documents ----

    [Fact]
    public async Task Add_from_library_without_a_token_answers_401()
    {
        using var anonymous = Anonymous();
        Assert.Equal(HttpStatusCode.Unauthorized,
            (await anonymous.PostAsJsonAsync($"/api/meetings/{Guid.CreateVersion7()}/documents",
                new { documentIds = Array.Empty<Guid>() })).StatusCode);
    }

    // ---- POST /api/meetings/{id}/documents/upload ----

    [Fact]
    public async Task Meeting_upload_without_a_token_answers_401()
    {
        using var anonymous = Anonymous();
        Assert.Equal(HttpStatusCode.Unauthorized,
            (await anonymous.PostAsync($"/api/meetings/{Guid.CreateVersion7()}/documents/upload",
                new MultipartFormDataContent())).StatusCode);
    }

    // ---- GET meeting document content ----

    [Fact]
    public async Task Download_flag_must_be_true_or_false()
    {
        var s = await fixture.Seed();
        var document = await AddMeetingDocument(s.MeetingId, s.Secretary, "Tờ trình.pdf");
        using var client = fixture.Client(s.Member);

        using var response = await client.GetAsync(
            $"/api/meetings/{s.MeetingId}/documents/{document}/content?download=maybe");
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        var text = await response.Content.ReadAsStringAsync();
        Assert.False(text.Length > 0
            && JsonDocument.Parse(text).RootElement.TryGetProperty("code", out _));
    }

    [Fact]
    public async Task Meeting_content_without_a_token_answers_401()
    {
        using var anonymous = Anonymous();
        Assert.Equal(HttpStatusCode.Unauthorized,
            (await anonymous.GetAsync(
                $"/api/meetings/{Guid.CreateVersion7()}/documents/{Guid.CreateVersion7()}/content")).StatusCode);
    }

    // ---- DELETE meeting document ----

    [Fact]
    public async Task Remove_meeting_document_without_a_token_answers_401()
    {
        using var anonymous = Anonymous();
        Assert.Equal(HttpStatusCode.Unauthorized,
            (await anonymous.DeleteAsync(
                $"/api/meetings/{Guid.CreateVersion7()}/documents/{Guid.CreateVersion7()}")).StatusCode);
    }

    // ---- agenda item documents ----

    [Fact]
    public async Task Link_documents_without_a_token_answers_401()
    {
        using var anonymous = Anonymous();
        Assert.Equal(HttpStatusCode.Unauthorized,
            (await anonymous.PostAsJsonAsync(
                $"/api/meetings/{Guid.CreateVersion7()}/agenda/{Guid.CreateVersion7()}/documents",
                new { documentIds = Array.Empty<Guid>() })).StatusCode);
    }

    [Fact]
    public async Task Link_from_library_without_a_token_answers_401()
    {
        using var anonymous = Anonymous();
        Assert.Equal(HttpStatusCode.Unauthorized,
            (await anonymous.PostAsJsonAsync(
                $"/api/meetings/{Guid.CreateVersion7()}/agenda/{Guid.CreateVersion7()}/documents/from-library",
                new { documentIds = Array.Empty<Guid>() })).StatusCode);
    }

    [Fact]
    public async Task Uploading_11_files_to_an_item_is_MSG288()
    {
        var s = await fixture.Seed();
        var item = await AddItem(s.MeetingId);
        using var client = fixture.Client(s.Chair);
        var files = Enumerable.Range(0, 11).Select(i => ($"{i}.pdf", SampleFiles.Pdf(), (string?)"1")).ToArray();

        var body = await AssertCode(await client.PostAsync(
            $"/api/meetings/{s.MeetingId}/agenda/{item}/documents/upload", DocumentFixture.Form(files)), 400, "MSG288");
        Assert.Equal("Mỗi lần tải tối đa 10 tệp, tổng dung lượng không quá 100 MB.",
            body.GetProperty("message").GetString());
    }

    [Fact]
    public async Task Item_upload_answers_MSG166_when_the_store_is_not_configured()
    {
        var s = await fixture.Seed();
        var item = await AddItem(s.MeetingId);
        var objects = fixture.Storage.Objects.Count;
        fixture.Storage.Configured = false;
        try
        {
            using var client = fixture.Client(s.Chair);
            await AssertCode(await client.PostAsync(
                $"/api/meetings/{s.MeetingId}/agenda/{item}/documents/upload",
                DocumentFixture.Form(("a.pdf", SampleFiles.Pdf(), "1"))), 503, "MSG166");
        }
        finally
        {
            fixture.Storage.Reset();
        }
        await using var db = fixture.Db();
        Assert.Equal(0, await db.MeetingDocuments.CountAsync(d => d.MeetingId == s.MeetingId));
        Assert.Equal(objects, fixture.Storage.Objects.Count);
    }

    [Fact]
    public async Task Item_upload_without_a_token_answers_401()
    {
        using var anonymous = Anonymous();
        Assert.Equal(HttpStatusCode.Unauthorized,
            (await anonymous.PostAsync(
                $"/api/meetings/{Guid.CreateVersion7()}/agenda/{Guid.CreateVersion7()}/documents/upload",
                new MultipartFormDataContent())).StatusCode);
    }

    [Fact]
    public async Task Unlink_without_a_token_answers_401()
    {
        using var anonymous = Anonymous();
        Assert.Equal(HttpStatusCode.Unauthorized,
            (await anonymous.DeleteAsync(
                $"/api/meetings/{Guid.CreateVersion7()}/agenda/{Guid.CreateVersion7()}" +
                $"/documents/{Guid.CreateVersion7()}")).StatusCode);
    }

    // ---- comments ----

    [Fact]
    public async Task List_comments_without_a_token_answers_401()
    {
        using var anonymous = Anonymous();
        Assert.Equal(HttpStatusCode.Unauthorized,
            (await anonymous.GetAsync(
                $"/api/meetings/{Guid.CreateVersion7()}/documents/{Guid.CreateVersion7()}/comments")).StatusCode);
    }

    [Fact]
    public async Task Add_comment_without_a_token_answers_401()
    {
        using var anonymous = Anonymous();
        Assert.Equal(HttpStatusCode.Unauthorized,
            (await anonymous.PostAsJsonAsync(
                $"/api/meetings/{Guid.CreateVersion7()}/documents/{Guid.CreateVersion7()}/comments",
                new { body = "Đồng ý" })).StatusCode);
    }

    // ---- read confirmation ----

    [Fact]
    public async Task Confirm_read_without_a_token_answers_401()
    {
        using var anonymous = Anonymous();
        Assert.Equal(HttpStatusCode.Unauthorized,
            (await anonymous.PostAsJsonAsync(
                $"/api/meetings/{Guid.CreateVersion7()}/documents/{Guid.CreateVersion7()}/read-confirmation",
                new { })).StatusCode);
    }
}
