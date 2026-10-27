using System.IdentityModel.Tokens.Jwt;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Security.Claims;
using System.Text;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;
using TrustCabinet.Domain.Access;
using TrustCabinet.Domain.Audit;
using TrustCabinet.Domain.Documents;
using TrustCabinet.Domain.Entities;
using TrustCabinet.Domain.Enums;
using TrustCabinet.Web.Tests.Agenda;

namespace TrustCabinet.Web.Tests.Documents;

/// <summary>SCRUM-110 folders (UC-72a–d) and SCRUM-111 documents (UC-34, UC-36, UC-71a–c) of the personal library.</summary>
[Collection("Documents")]
public sealed class PersonalLibraryEndpointsTests(DocumentFixture fixture) : IAsyncLifetime
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

    internal static async Task<Guid> NewUser(DocumentFixture fixture, bool active = true, Guid? role = null)
    {
        await using var db = fixture.Db();
        var user = new User { FullName = "Library test", Email = $"{Guid.NewGuid():N}@library.test",
            PasswordHash = "test-only-non-login-hash", IsActive = active, RoleId = role ?? BuiltInRoles.Member };
        db.Users.Add(user);
        await db.SaveChangesAsync();
        return user.Id;
    }

    internal static async Task<Guid> AddFolder(DocumentFixture fixture, Guid owner, string name, Guid? parent = null)
    {
        await using var db = fixture.Db();
        var folder = new Folder { OwnerId = owner, Name = name, ParentId = parent };
        db.Folders.Add(folder);
        await db.SaveChangesAsync();
        return folder.Id;
    }

    /// <summary>A library document with its stored file; the bytes go in the store unless <paramref name="size"/> fakes a size.</summary>
    internal static async Task<Guid> AddDocument(DocumentFixture fixture, Guid owner, string name, Guid? folder = null,
        long? size = null, bool docx = false)
    {
        await using var db = fixture.Db();
        var bytes = docx ? SampleFiles.Docx() : SampleFiles.Pdf(name);
        var fileId = Guid.CreateVersion7();
        var document = new Document { OwnerId = owner, FolderId = folder, FileName = name,
            StoredFile = new StoredFile { Id = fileId, StorageKey = DocumentFileRules.StorageKey(fileId), OriginalFileName = name,
                ContentType = docx ? DocumentFileRules.DocxContentType : DocumentFileRules.PdfContentType,
                ByteSize = size ?? bytes.Length, ContentSha256 = new string('b', 64), PageCount = docx ? null : 5,
                UploadedById = owner, UploadedAt = DateTimeOffset.UtcNow } };
        db.Documents.Add(document);
        await db.SaveChangesAsync();
        if (size is null) fixture.Storage.Objects[document.StoredFile.StorageKey] = bytes;
        return document.Id;
    }

    private static string AdminToken(Guid actor) => new JwtSecurityTokenHandler().WriteToken(new JwtSecurityToken(
        "TrustCabinet", "TrustCabinet", [new Claim("sub", actor.ToString()), new Claim("cap", nameof(Capability.AdminUsers))],
        expires: DateTime.UtcNow.AddMinutes(10),
        signingCredentials: new SigningCredentials(new SymmetricSecurityKey(Encoding.UTF8.GetBytes(AgendaFixture.SigningKey)),
            SecurityAlgorithms.HmacSha256)));

    /// <summary>An administrator, optionally a Member of <paramref name="meetingId"/> so they may view it themselves.</summary>
    private async Task<HttpClient> AdminClient(Guid? meetingId = null)
    {
        var admin = await NewUser(fixture, role: BuiltInRoles.Admin);
        await using var db = fixture.Db();
        if (meetingId is { } meeting)
            db.MeetingParticipants.Add(new MeetingParticipant { MeetingId = meeting, UserId = admin, Role = MeetingRole.Member });
        if (!await db.RoleCapabilities.AnyAsync(c => c.RoleId == BuiltInRoles.Admin && c.Capability == Capability.AdminUsers))
            db.RoleCapabilities.Add(new RoleCapability { RoleId = BuiltInRoles.Admin, Capability = Capability.AdminUsers });
        await db.SaveChangesAsync();
        var client = fixture.Host.CreateClient();
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", AdminToken(admin));
        return client;
    }

    internal static async Task<(int Status, string? Code, JsonElement Body)> Read(HttpResponseMessage response)
    {
        var text = await response.Content.ReadAsStringAsync();
        var body = text.Length == 0 ? default : JsonDocument.Parse(text).RootElement.Clone();
        var code = body.ValueKind == JsonValueKind.Object && body.TryGetProperty("code", out var c) ? c.GetString() : null;
        return ((int)response.StatusCode, code, body);
    }

    private static Task<HttpResponseMessage> Put(HttpClient client, string url, object body) => client.PutAsJsonAsync(url, body);

    // ---- UC-72a–d: folders ----

    [Fact]
    public async Task Owner_creates_nested_folders_with_duplicate_names_and_sees_them_in_the_library()
    {
        var owner = await NewUser(fixture);
        using var client = fixture.Client(owner);

        var (status, code, body) = await Read(await client.PostAsJsonAsync("/api/folders", new { name = "  Báo cáo  " }));
        Assert.Equal((201, "MSG187"), (status, code));
        var parent = body.GetProperty("folder").GetProperty("id").GetGuid();
        Assert.Equal("Báo cáo", body.GetProperty("folder").GetProperty("name").GetString());
        // Not unique (BR-221).
        var (childStatus, _, child) = await Read(await client.PostAsJsonAsync("/api/folders", new { name = "Báo cáo", parentId = parent }));
        Assert.Equal(201, childStatus);
        Assert.Equal(parent, child.GetProperty("folder").GetProperty("parentId").GetGuid());

        var (_, _, view) = await Read(await client.GetAsync("/api/documents"));
        Assert.Equal(2, view.GetProperty("folders").GetArrayLength());
        Assert.Equal(DocumentFileRules.MaxLibraryBytes, view.GetProperty("quotaBytes").GetInt64());
    }

    [Fact]
    public async Task Folder_name_must_not_be_blank_or_too_long()
    {
        using var client = fixture.Client(await NewUser(fixture));

        Assert.Equal((400, "MSG188"), Pick(await Read(await client.PostAsJsonAsync("/api/folders", new { name = "   " }))));
        Assert.Equal((400, "MSG348"), Pick(await Read(await client.PostAsJsonAsync("/api/folders", new { name = new string('x', 401) }))));
    }

    [Fact]
    public async Task Someone_elses_folder_is_neither_a_parent_nor_renamable_nor_deletable()
    {
        var owner = await NewUser(fixture);
        var theirs = await AddFolder(fixture, await NewUser(fixture), "Riêng");
        using var client = fixture.Client(owner);

        Assert.Equal((404, "MSG179"), Pick(await Read(await client.PostAsJsonAsync("/api/folders", new { name = "A", parentId = theirs }))));
        Assert.Equal((404, "MSG175"), Pick(await Read(await Put(client, $"/api/folders/{theirs}/name", new { name = "B" }))));
        Assert.Equal((404, "MSG175"), Pick(await Read(await Put(client, $"/api/folders/{theirs}/parent", new { parentId = (Guid?)null }))));
        Assert.Equal((404, "MSG175"), Pick(await Read(await client.DeleteAsync($"/api/folders/{theirs}"))));
        Assert.Equal((404, "MSG175"), Pick(await Read(await client.GetAsync($"/api/documents?folderId={theirs}"))));
    }

    [Fact]
    public async Task A_sixth_level_is_refused_on_create_and_on_move()
    {
        var owner = await NewUser(fixture);
        Guid? parent = null;
        var chain = new List<Guid>();
        for (var level = 1; level <= 5; level++) chain.Add((parent = await AddFolder(fixture, owner, $"Cấp {level}", parent)).Value);
        var twoLevels = await AddFolder(fixture, owner, "Có con");
        await AddFolder(fixture, owner, "Con", twoLevels);
        using var client = fixture.Client(owner);

        Assert.Equal((409, "MSG189"), Pick(await Read(await client.PostAsJsonAsync("/api/folders", new { name = "Cấp 6", parentId = chain[4] }))));
        // Into level 4 the folder would be level 5, but its child level 6 (BR-191).
        Assert.Equal((409, "MSG189"), Pick(await Read(await Put(client, $"/api/folders/{twoLevels}/parent", new { parentId = chain[3] }))));
        var (status, code, body) = await Read(await Put(client, $"/api/folders/{twoLevels}/parent", new { parentId = chain[2] }));
        Assert.Equal((200, "MSG194"), (status, code));
        Assert.Equal(chain[2], body.GetProperty("folder").GetProperty("parentId").GetGuid());
    }

    [Fact]
    public async Task A_folder_cannot_move_into_itself_or_a_subfolder_but_can_move_to_the_root()
    {
        var owner = await NewUser(fixture);
        var top = await AddFolder(fixture, owner, "Gốc");
        var child = await AddFolder(fixture, owner, "Con", top);
        var grandchild = await AddFolder(fixture, owner, "Cháu", child);
        using var client = fixture.Client(owner);

        Assert.Equal((409, "MSG193"), Pick(await Read(await Put(client, $"/api/folders/{top}/parent", new { parentId = top }))));
        Assert.Equal((409, "MSG193"), Pick(await Read(await Put(client, $"/api/folders/{top}/parent", new { parentId = grandchild }))));
        Assert.Equal((404, "MSG179"), Pick(await Read(await Put(client, $"/api/folders/{top}/parent", new { parentId = Guid.NewGuid() }))));
        Assert.Equal((200, "MSG194"), Pick(await Read(await Put(client, $"/api/folders/{child}/parent", new { parentId = (Guid?)null }))));

        await using var db = fixture.Db();
        Assert.Null((await db.Folders.SingleAsync(f => f.Id == child)).ParentId);
        Assert.Equal(child, (await db.Folders.SingleAsync(f => f.Id == grandchild)).ParentId);
    }

    [Fact]
    public async Task Two_crossing_moves_at_once_never_make_a_cycle()
    {
        var owner = await NewUser(fixture);
        var a = await AddFolder(fixture, owner, "A");
        var b = await AddFolder(fixture, owner, "B");
        using var client = fixture.Client(owner);

        var results = await Task.WhenAll(
            Put(client, $"/api/folders/{a}/parent", new { parentId = b }),
            Put(client, $"/api/folders/{b}/parent", new { parentId = a }));

        Assert.Equal(1, results.Count(r => r.IsSuccessStatusCode));
        await using var db = fixture.Db();
        Assert.Equal(1, await db.Folders.CountAsync(f => f.OwnerId == owner && f.ParentId == null));
    }

    [Fact]
    public async Task Rename_keeps_place_and_contents()
    {
        var owner = await NewUser(fixture);
        var folder = await AddFolder(fixture, owner, "Cũ");
        await AddDocument(fixture, owner, "a.pdf", folder);
        using var client = fixture.Client(owner);

        var (status, code, body) = await Read(await Put(client, $"/api/folders/{folder}/name", new { name = "Mới" }));

        Assert.Equal((200, "MSG191"), (status, code));
        Assert.Equal("Mới", body.GetProperty("folder").GetProperty("name").GetString());
        Assert.Equal((400, "MSG188"), Pick(await Read(await Put(client, $"/api/folders/{folder}/name", new { name = "" }))));
        var (_, _, view) = await Read(await client.GetAsync($"/api/documents?folderId={folder}"));
        Assert.Equal(1, view.GetProperty("documents").GetArrayLength());
    }

    [Fact]
    public async Task Only_an_empty_folder_is_deleted()
    {
        var owner = await NewUser(fixture);
        var withDocument = await AddFolder(fixture, owner, "Có tài liệu");
        await AddDocument(fixture, owner, "a.pdf", withDocument);
        var withChild = await AddFolder(fixture, owner, "Có con");
        await AddFolder(fixture, owner, "Con", withChild);
        var empty = await AddFolder(fixture, owner, "Rỗng");
        using var client = fixture.Client(owner);

        Assert.Equal((409, "MSG196"), Pick(await Read(await client.DeleteAsync($"/api/folders/{withDocument}"))));
        Assert.Equal((409, "MSG196"), Pick(await Read(await client.DeleteAsync($"/api/folders/{withChild}"))));
        Assert.Equal((200, "MSG197"), Pick(await Read(await client.DeleteAsync($"/api/folders/{empty}"))));
        Assert.Equal((404, "MSG175"), Pick(await Read(await client.DeleteAsync($"/api/folders/{empty}"))));
    }

    [Fact]
    public async Task Folder_changes_that_cannot_be_saved_answer_their_own_codes()
    {
        var owner = await NewUser(fixture);
        var folder = await AddFolder(fixture, owner, "Thư mục");
        using var client = fixture.Client(owner);
        fixture.ReadFailure.Table = "folders";

        Assert.Equal((503, "MSG190"), Pick(await Read(await client.PostAsJsonAsync("/api/folders", new { name = "A" }))));
        Assert.Equal((503, "MSG192"), Pick(await Read(await Put(client, $"/api/folders/{folder}/name", new { name = "B" }))));
        Assert.Equal((503, "MSG195"), Pick(await Read(await Put(client, $"/api/folders/{folder}/parent", new { parentId = (Guid?)null }))));
        Assert.Equal((503, "MSG198"), Pick(await Read(await client.DeleteAsync($"/api/folders/{folder}"))));
        Assert.Equal((500, "MSG176"), Pick(await Read(await client.GetAsync("/api/documents"))));
    }

    // ---- UC-34, UC-36, UC-71a–c: documents ----

    [Fact]
    public async Task Owner_uploads_into_a_folder_and_sees_the_documents_and_the_space_used()
    {
        var owner = await NewUser(fixture);
        var folder = await AddFolder(fixture, owner, "Hồ sơ");
        using var client = fixture.Client(owner);
        var pdf = SampleFiles.Pdf("kho");
        var docx = SampleFiles.Docx();

        var (status, code, body) = await Read(await client.PostAsync($"/api/documents/upload?folderId={folder}",
            DocumentFixture.Form(("Tờ trình.pdf", pdf, "3"), ("Dự thảo.docx", docx, ""))));

        Assert.Equal((201, "MSG177"), (status, code));
        Assert.Equal(2, body.GetProperty("addedCount").GetInt32());
        var (_, _, view) = await Read(await client.GetAsync($"/api/documents?folderId={folder}"));
        var documents = view.GetProperty("documents").EnumerateArray().ToArray();
        Assert.Equal(["Dự thảo.docx", "Tờ trình.pdf"], documents.Select(d => d.GetProperty("fileName").GetString()));
        Assert.Equal(3, documents[1].GetProperty("pageCount").GetInt32());
        Assert.All(documents, d => Assert.Equal(0, d.GetProperty("meetingCount").GetInt32()));
        Assert.Equal(pdf.Length + docx.Length, view.GetProperty("usedBytes").GetInt64());
        await using var db = fixture.Db();
        // No meeting involved: a library row only, pointing at an immutable stored file (BR-183).
        var stored = await db.Documents.Where(d => d.OwnerId == owner).Select(d => d.StoredFile.StorageKey).ToListAsync();
        Assert.All(stored, key => Assert.True(fixture.Storage.Objects.ContainsKey(key)));
        Assert.False(await db.MeetingDocuments.AnyAsync(d => d.AddedById == owner));
    }

    [Fact]
    public async Task Upload_checks_destination_files_and_limits()
    {
        var owner = await NewUser(fixture);
        var theirs = await AddFolder(fixture, await NewUser(fixture), "Riêng");
        using var client = fixture.Client(owner);

        Assert.Equal((404, "MSG179"), Pick(await Read(await client.PostAsync($"/api/documents/upload?folderId={theirs}",
            DocumentFixture.Form(("a.pdf", SampleFiles.Pdf(), null))))));
        var (status, code, body) = await Read(await client.PostAsync("/api/documents/upload",
            DocumentFixture.Form(("anh.pdf", SampleFiles.Png(), null))));
        Assert.Equal((400, "MSG164"), (status, code));
        Assert.True(body.GetProperty("errors").TryGetProperty("files[0]", out _));
        var eleven = Enumerable.Range(0, 11).Select(i => ($"{i}.pdf", SampleFiles.Pdf(), (string?)null)).ToArray();
        Assert.Equal((400, "MSG288"), Pick(await Read(await client.PostAsync("/api/documents/upload", DocumentFixture.Form(eleven)))));
    }

    [Fact]
    public async Task Upload_past_the_quota_adds_nothing()
    {
        var owner = await NewUser(fixture);
        await AddDocument(fixture, owner, "lớn.pdf", size: DocumentFileRules.MaxLibraryBytes - 10);
        using var client = fixture.Client(owner);
        var before = fixture.Storage.Objects.Count;

        Assert.Equal((409, "MSG346"), Pick(await Read(await client.PostAsync("/api/documents/upload",
            DocumentFixture.Form(("a.pdf", SampleFiles.Pdf(), null))))));

        Assert.Equal(before, fixture.Storage.Objects.Count);
        await using var db = fixture.Db();
        Assert.Equal(1, await db.Documents.CountAsync(d => d.OwnerId == owner));
    }

    [Fact]
    public async Task Two_uploads_at_once_near_the_quota_let_only_one_in()
    {
        var owner = await NewUser(fixture);
        var pdf = SampleFiles.Pdf("x");
        await AddDocument(fixture, owner, "Đầy.pdf", size: DocumentFileRules.MaxLibraryBytes - pdf.Length * 3 / 2);
        using var client = fixture.Client(owner);

        var results = await Task.WhenAll(Enumerable.Range(0, 2).Select(async _ =>
            Pick(await Read(await client.PostAsync("/api/documents/upload", DocumentFixture.Form(("x.pdf", pdf, null)))))));

        Assert.Equal([(201, (string?)"MSG177"), (409, "MSG346")], results.Order());
        await using var db = fixture.Db();
        Assert.True(await db.Documents.Where(d => d.OwnerId == owner).SumAsync(d => d.StoredFile.ByteSize) <= DocumentFileRules.MaxLibraryBytes);
    }

    [Fact]
    public async Task Upload_into_a_meeting_counts_towards_the_used_space_without_a_quota_check()
    {
        var s = await fixture.Seed();
        await AddDocument(fixture, s.Secretary, "Đầy.pdf", size: DocumentFileRules.MaxLibraryBytes);
        using var client = fixture.Client(s.Secretary);

        Assert.Equal(201, (int)(await client.PostAsync(s.Url, DocumentFixture.Form(("x.pdf", SampleFiles.Pdf(), null)))).StatusCode);

        var (_, _, view) = await Read(await client.GetAsync("/api/documents"));
        Assert.True(view.GetProperty("usedBytes").GetInt64() > view.GetProperty("quotaBytes").GetInt64());
    }

    [Fact]
    public async Task A_failed_store_adds_nothing_and_leaves_no_object()
    {
        var owner = await NewUser(fixture);
        using var client = fixture.Client(owner);
        fixture.Storage.BeforeWrite = n => n == 2 ? throw new IOException("R2 down") : Task.CompletedTask;
        var before = fixture.Storage.Objects.Count;

        Assert.Equal((503, "MSG178"), Pick(await Read(await client.PostAsync("/api/documents/upload",
            DocumentFixture.Form(("a.pdf", SampleFiles.Pdf("1"), null), ("b.pdf", SampleFiles.Pdf("2"), null))))));

        Assert.Equal(before, fixture.Storage.Objects.Count);
        fixture.Storage.Reset();
        fixture.Storage.Configured = false;
        Assert.Equal((503, "MSG178"), Pick(await Read(await client.PostAsync("/api/documents/upload",
            DocumentFixture.Form(("a.pdf", SampleFiles.Pdf(), null))))));
        await using var db = fixture.Db();
        Assert.False(await db.Documents.AnyAsync(d => d.OwnerId == owner));
    }

    [Fact]
    public async Task Search_and_type_filter_stay_within_the_location()
    {
        var owner = await NewUser(fixture);
        var folder = await AddFolder(fixture, owner, "Thư mục");
        await AddDocument(fixture, owner, "Báo cáo quý 3.pdf");
        await AddDocument(fixture, owner, "Báo cáo quý 4.docx", docx: true);
        await AddDocument(fixture, owner, "100%_xong.pdf");
        await AddDocument(fixture, owner, "Báo cáo trong thư mục.pdf", folder);
        using var client = fixture.Client(owner);

        async Task<string[]> Names(string query) => (await Read(await client.GetAsync("/api/documents" + query))).Body
            .GetProperty("documents").EnumerateArray().Select(d => d.GetProperty("fileName").GetString()!).ToArray();

        Assert.Equal(["Báo cáo quý 3.pdf", "Báo cáo quý 4.docx"], await Names("?q=báo CÁO"));
        Assert.Equal(["Báo cáo quý 4.docx"], await Names("?q=báo&type=docx"));
        // % and _ are literal characters, not wildcards.
        Assert.Equal(["100%_xong.pdf"], await Names("?q=%25_"));
        Assert.Equal(["Báo cáo trong thư mục.pdf"], await Names($"?folderId={folder}&q=báo"));
    }

    [Fact]
    public async Task Meeting_count_only_includes_meetings_the_owner_can_view_and_still_using_the_file()
    {
        var s = await fixture.Seed();
        var document = await AddDocument(fixture, s.Secretary, "Tờ trình.pdf");
        await using (var db = fixture.Db())
        {
            var file = await db.Documents.Where(d => d.Id == document).Select(d => d.StoredFileId).SingleAsync();
            var hidden = new Meeting { Title = "Không xem được", CreatedById = s.Outsider, Status = MeetingStatus.Draft,
                PlannedStartTime = DocumentFixture.PlannedStart, PlannedEndTime = DocumentFixture.PlannedStart.AddHours(1) };
            var removedFrom = (await fixture.Seed()).MeetingId;
            db.Meetings.Add(hidden);
            db.MeetingParticipants.Add(new MeetingParticipant { MeetingId = removedFrom, UserId = s.Secretary, Role = MeetingRole.Member });
            db.MeetingDocuments.AddRange(
                new MeetingDocument { MeetingId = s.MeetingId, StoredFileId = file, DocumentId = document, FileName = "x", AddedById = s.Secretary },
                new MeetingDocument { MeetingId = hidden.Id, StoredFileId = file, DocumentId = document, FileName = "x", AddedById = s.Secretary },
                new MeetingDocument { MeetingId = removedFrom, StoredFileId = file, DocumentId = document, FileName = "x",
                    AddedById = s.Secretary, RemovedAt = DateTimeOffset.UtcNow, RemovedById = s.Secretary });
            await db.SaveChangesAsync();
        }
        using var client = fixture.Client(s.Secretary);

        var (_, _, view) = await Read(await client.GetAsync("/api/documents"));

        var row = view.GetProperty("documents").EnumerateArray().Single(d => d.GetProperty("id").GetGuid() == document);
        Assert.Equal(1, row.GetProperty("meetingCount").GetInt32());
    }

    [Fact]
    public async Task Rename_keeps_the_extension_and_leaves_meeting_copies_alone()
    {
        var s = await fixture.Seed();
        var document = await AddDocument(fixture, s.Secretary, "Cũ.pdf");
        using var client = fixture.Client(s.Secretary);
        Assert.Equal(201, (await Read(await client.PostAsJsonAsync($"/api/meetings/{s.MeetingId}/documents",
            new { documentIds = new[] { document } }))).Status);

        var (status, code, body) = await Read(await Put(client, $"/api/documents/{document}/name", new { fileName = " Mới.PDF " }));

        Assert.Equal((200, "MSG183"), (status, code));
        Assert.Equal("Mới.PDF", body.GetProperty("document").GetProperty("fileName").GetString());
        Assert.Equal(1, body.GetProperty("document").GetProperty("meetingCount").GetInt32());
        Assert.Equal((400, "MSG347"), Pick(await Read(await Put(client, $"/api/documents/{document}/name", new { fileName = "Mới.html" }))));
        Assert.Equal((400, "MSG347"), Pick(await Read(await Put(client, $"/api/documents/{document}/name", new { fileName = "  " }))));
        Assert.Equal((400, "MSG347"), Pick(await Read(await Put(client, $"/api/documents/{document}/name", new { fileName = ".pdf" }))));
        await using var db = fixture.Db();
        // BR-276: the meeting keeps the name it was added with.
        Assert.Equal("Cũ.pdf", await db.MeetingDocuments.Where(d => d.DocumentId == document).Select(d => d.FileName).SingleAsync());
    }

    [Fact]
    public async Task Move_goes_to_an_owned_folder_or_the_root()
    {
        var owner = await NewUser(fixture);
        var folder = await AddFolder(fixture, owner, "Đích");
        var theirs = await AddFolder(fixture, await NewUser(fixture), "Riêng");
        var document = await AddDocument(fixture, owner, "a.pdf");
        using var client = fixture.Client(owner);

        var (status, code, body) = await Read(await Put(client, $"/api/documents/{document}/folder", new { folderId = folder }));
        Assert.Equal((200, "MSG185"), (status, code));
        Assert.Equal(folder, body.GetProperty("document").GetProperty("folderId").GetGuid());
        Assert.Equal((404, "MSG179"), Pick(await Read(await Put(client, $"/api/documents/{document}/folder", new { folderId = theirs }))));
        Assert.Equal((200, "MSG185"), Pick(await Read(await Put(client, $"/api/documents/{document}/folder", new { folderId = (Guid?)null }))));
    }

    [Fact]
    public async Task Someone_elses_document_is_not_there()
    {
        var owner = await NewUser(fixture);
        var theirs = await AddDocument(fixture, await NewUser(fixture), "riêng.pdf");
        using var client = fixture.Client(owner);

        Assert.Equal((404, "MSG175"), Pick(await Read(await Put(client, $"/api/documents/{theirs}/name", new { fileName = "x.pdf" }))));
        Assert.Equal((404, "MSG175"), Pick(await Read(await Put(client, $"/api/documents/{theirs}/folder", new { folderId = (Guid?)null }))));
        Assert.Equal((404, "MSG175"), Pick(await Read(await client.DeleteAsync($"/api/documents/{theirs}"))));
        Assert.Equal((404, "MSG175"), Pick(await Read(await client.GetAsync($"/api/documents/{theirs}/content"))));
    }

    [Fact]
    public async Task Deleting_from_the_library_leaves_the_meeting_copy_readable()
    {
        var s = await fixture.Seed();
        var document = await AddDocument(fixture, s.Secretary, "Tờ trình.pdf");
        using var client = fixture.Client(s.Secretary);
        var (_, _, added) = await Read(await client.PostAsJsonAsync($"/api/meetings/{s.MeetingId}/documents",
            new { documentIds = new[] { document } }));
        var meetingDocument = added.GetProperty("documents")[0].GetProperty("id").GetGuid();

        Assert.Equal((200, "MSG180"), Pick(await Read(await client.DeleteAsync($"/api/documents/{document}"))));

        // BR-186: still there for the meeting, even once the owner is deactivated.
        await using (var db = fixture.Db())
        {
            Assert.Null(await db.MeetingDocuments.Where(d => d.Id == meetingDocument).Select(d => d.DocumentId).SingleAsync());
            await db.Users.Where(u => u.Id == s.Secretary).ExecuteUpdateAsync(u => u.SetProperty(x => x.IsActive, false));
        }
        using var member = fixture.Client(s.Member);
        var content = await member.GetAsync(s.ContentUrl(meetingDocument));
        Assert.Equal(200, (int)content.StatusCode);
        Assert.Equal(SampleFiles.Pdf("Tờ trình.pdf"), await content.Content.ReadAsByteArrayAsync());
    }

    [Fact]
    public async Task Document_changes_that_cannot_be_saved_answer_their_own_codes()
    {
        var owner = await NewUser(fixture);
        var document = await AddDocument(fixture, owner, "a.pdf");
        using var client = fixture.Client(owner);
        fixture.ReadFailure.Table = "documents";

        Assert.Equal((503, "MSG184"), Pick(await Read(await Put(client, $"/api/documents/{document}/name", new { fileName = "b.pdf" }))));
        Assert.Equal((503, "MSG186"), Pick(await Read(await Put(client, $"/api/documents/{document}/folder", new { folderId = (Guid?)null }))));
        Assert.Equal((503, "MSG181"), Pick(await Read(await client.DeleteAsync($"/api/documents/{document}"))));
    }

    [Fact]
    public async Task Owner_opens_their_document_and_a_missing_object_reads_as_try_again()
    {
        var owner = await NewUser(fixture);
        var document = await AddDocument(fixture, owner, "Đọc.pdf");
        using var client = fixture.Client(owner);

        var response = await client.GetAsync($"/api/documents/{document}/content");
        Assert.Equal(200, (int)response.StatusCode);
        Assert.Equal(SampleFiles.Pdf("Đọc.pdf"), await response.Content.ReadAsByteArrayAsync());
        Assert.Equal("attachment", response.Content.Headers.ContentDisposition!.DispositionType);

        fixture.Storage.Objects.Clear();
        Assert.Equal((503, "MSG158"), Pick(await Read(await client.GetAsync($"/api/documents/{document}/content"))));
        Assert.Equal((503, "MSG160"), Pick(await Read(await client.GetAsync($"/api/documents/{document}/content?download=true"))));
    }

    // ---- PO 27/10/2026: an administrator reads a deactivated person's library ----

    [Fact]
    public async Task Administrator_reads_a_deactivated_library_and_every_read_is_audited()
    {
        var owner = await NewUser(fixture, active: false);
        var folder = await AddFolder(fixture, owner, "Hồ sơ");
        var document = await AddDocument(fixture, owner, "Bàn giao.pdf", folder);
        using var admin = await AdminClient();

        var (status, _, view) = await Read(await admin.GetAsync($"/api/users/{owner}/library?folderId={folder}"));
        Assert.Equal(200, status);
        Assert.Equal(document, view.GetProperty("documents")[0].GetProperty("id").GetGuid());
        var content = await admin.GetAsync($"/api/users/{owner}/library/documents/{document}/content");
        Assert.Equal(200, (int)content.StatusCode);

        await using var db = fixture.Db();
        var audit = await db.AuditLog.Where(a => a.ObjectId == owner).ToListAsync();
        Assert.Equal(2, audit.Count);
        Assert.All(audit, a => Assert.Equal((AuditActions.PersonalLibraryRead, AuditActions.UserObjectType), (a.Action, a.ObjectType)));
    }

    [Fact]
    public async Task Nobody_else_reads_an_active_library_and_a_non_administrator_is_refused()
    {
        var active = await NewUser(fixture);
        var document = await AddDocument(fixture, active, "a.pdf");
        using var admin = await AdminClient();

        Assert.Equal((404, "MSG175"), Pick(await Read(await admin.GetAsync($"/api/users/{active}/library"))));
        Assert.Equal((404, "MSG175"), Pick(await Read(await admin.GetAsync($"/api/users/{active}/library/documents/{document}/content"))));
        using var member = fixture.Client(await NewUser(fixture));
        Assert.Equal(403, (int)(await member.GetAsync($"/api/users/{active}/library")).StatusCode);
        await using var db = fixture.Db();
        Assert.False(await db.AuditLog.AnyAsync(a => a.ObjectId == active));
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task Administrator_does_not_see_files_or_folders_of_a_secret_meeting_even_one_they_can_view(bool member)
    {
        // The Secretary uploads into a Secret meeting: the file lands in their library, in the meeting's folder.
        var s = await fixture.Seed(sensitivity: Sensitivity.Secret);
        using (var secretary = fixture.Client(s.Secretary))
            Assert.Equal(201, (int)(await secretary.PostAsync(s.Url, DocumentFixture.Form(("Mật.pdf", SampleFiles.Pdf("mật"), null)))).StatusCode);
        Guid meetingFolder, secretDocument;
        await using (var db = fixture.Db())
        {
            meetingFolder = await db.Folders.Where(f => f.OwnerId == s.Secretary && f.MeetingId == s.MeetingId).Select(f => f.Id).SingleAsync();
            secretDocument = await db.Documents.Where(d => d.FolderId == meetingFolder).Select(d => d.Id).SingleAsync();
        }
        var sub = await AddFolder(fixture, s.Secretary, "Con của thư mục cuộc họp", meetingFolder);
        // Moved to the root, the file is still the meeting's.
        var moved = await AddDocument(fixture, s.Secretary, "Khác.pdf", meetingFolder);
        await using (var db = fixture.Db())
        {
            await db.Documents.Where(d => d.Id == secretDocument).ExecuteUpdateAsync(d => d.SetProperty(x => x.FolderId, (Guid?)null));
            await db.Users.Where(u => u.Id == s.Secretary).ExecuteUpdateAsync(u => u.SetProperty(x => x.IsActive, false));
        }
        var loose = await AddDocument(fixture, s.Secretary, "Thường.pdf");
        using var admin = await AdminClient(member ? s.MeetingId : null);

        var (status, _, view) = await Read(await admin.GetAsync($"/api/users/{s.Secretary}/library"));

        Assert.Equal(200, status);
        Assert.Equal([loose], view.GetProperty("documents").EnumerateArray().Select(d => d.GetProperty("id").GetGuid()));
        var folders = view.GetProperty("folders").EnumerateArray().Select(f => f.GetProperty("id").GetGuid()).ToList();
        Assert.DoesNotContain(meetingFolder, folders);
        Assert.DoesNotContain(sub, folders);
        Assert.Equal((404, "MSG175"), Pick(await Read(await admin.GetAsync($"/api/users/{s.Secretary}/library/documents/{secretDocument}/content?download=true"))));
        Assert.Equal((404, "MSG175"), Pick(await Read(await admin.GetAsync($"/api/users/{s.Secretary}/library/documents/{moved}/content"))));
        Assert.Equal((404, "MSG175"), Pick(await Read(await admin.GetAsync($"/api/users/{s.Secretary}/library?folderId={sub}"))));
        Assert.Equal(200, (int)(await admin.GetAsync($"/api/users/{s.Secretary}/library/documents/{loose}/content")).StatusCode);
        if (!member) return;
        // A Member reads it through the meeting, under its rules: Secret documents are not downloaded (BR-188).
        Guid meetingDocument;
        await using (var db = fixture.Db())
            meetingDocument = await db.MeetingDocuments.Where(d => d.MeetingId == s.MeetingId).Select(d => d.Id).SingleAsync();
        Assert.Equal((403, "MSG159"), Pick(await Read(await admin.GetAsync($"/api/meetings/{s.MeetingId}/documents/{meetingDocument}/content?download=true"))));
    }

    private static (int, string?) Pick((int Status, string? Code, JsonElement Body) result) => (result.Status, result.Code);
}
