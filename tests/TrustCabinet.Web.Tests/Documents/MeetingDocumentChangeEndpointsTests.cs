using System.Net.Http.Json;
using System.Text.Json;
using System.Threading.Channels;
using Microsoft.AspNetCore.SignalR.Client;
using Microsoft.EntityFrameworkCore;
using TrustCabinet.Domain.Documents;
using TrustCabinet.Domain.Entities;
using TrustCabinet.Domain.Enums;
using TrustCabinet.Domain.Participants;
using static TrustCabinet.Web.Tests.Documents.PersonalLibraryEndpointsTests;

namespace TrustCabinet.Web.Tests.Documents;

/// <summary>SCRUM-112 (SCRUM-58): UC-33a from the library, UC-33b, UC-33c and UC-33d against PostgreSQL.</summary>
[Collection("Documents")]
public sealed class MeetingDocumentChangeEndpointsTests(DocumentFixture fixture) : IAsyncLifetime
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

    private async Task<Guid> AddItem(Guid meetingId, int position = 1)
    {
        await using var db = fixture.Db();
        var item = new AgendaItem { MeetingId = meetingId, Title = $"Mục {position}", Position = position };
        db.AgendaItems.Add(item);
        await db.SaveChangesAsync();
        return item.Id;
    }

    /// <summary>Adds a library document of <paramref name="owner"/> to the meeting through the API, as its Secretary would.</summary>
    private async Task<Guid> Attached(DocumentFixture.Scenario s, string name = "Tờ trình.pdf")
    {
        var document = await AddDocument(fixture, s.Secretary, name);
        using var client = fixture.Client(s.Secretary);
        var (status, _, body) = await Read(await client.PostAsJsonAsync(Url(s), new { documentIds = new[] { document } }));
        Assert.Equal(201, status);
        return body.GetProperty("documents")[0].GetProperty("id").GetGuid();
    }

    private static string Url(DocumentFixture.Scenario s) => $"/api/meetings/{s.MeetingId}/documents";
    private static string Links(DocumentFixture.Scenario s, Guid item) => $"/api/meetings/{s.MeetingId}/agenda/{item}/documents";

    private static (int, string?) Pick((int Status, string? Code, JsonElement Body) result) => (result.Status, result.Code);

    private async Task SetStatus(Guid meetingId, MeetingStatus status)
    {
        await using var db = fixture.Db();
        await db.Meetings.Where(m => m.Id == meetingId).ExecuteUpdateAsync(m => m.SetProperty(x => x.Status, status));
    }

    // ---- UC-33a: from the library ----

    [Fact]
    public async Task Secretary_adds_own_library_documents_as_general_documents_under_their_current_name()
    {
        var s = await fixture.Seed();
        var first = await AddDocument(fixture, s.Secretary, "Tờ trình.pdf");
        var second = await AddDocument(fixture, s.Secretary, "Phụ lục.docx", docx: true);
        using var client = fixture.Client(s.Secretary);

        var (status, code, body) = await Read(await client.PostAsJsonAsync(Url(s), new { documentIds = new[] { first, second, first } }));

        Assert.Equal((201, "MSG161"), (status, code));
        Assert.Equal(2, body.GetProperty("addedCount").GetInt32());
        var (_, _, list) = await Read(await client.GetAsync(Url(s)));
        Assert.Equal(["Tờ trình.pdf", "Phụ lục.docx"], list.GetProperty("general").EnumerateArray()
            .Select(d => d.GetProperty("fileName").GetString()).Order().Reverse());
        await using var db = fixture.Db();
        // No byte copied: the meeting copy points at the library row's stored file (ADR-0018).
        var rows = await db.MeetingDocuments.Where(d => d.MeetingId == s.MeetingId).ToListAsync();
        var files = await db.Documents.Where(d => d.Id == first || d.Id == second).Select(d => d.StoredFileId).ToListAsync();
        Assert.Equal(files.Order(), rows.Select(r => r.StoredFileId).Order());
        Assert.Equal(2, fixture.Storage.Objects.Keys.Count(k => files.Any(f => k.EndsWith(f.ToString()))));
    }

    [Fact]
    public async Task Library_selection_must_be_the_actors_own_available_and_not_yet_in_the_meeting()
    {
        var s = await fixture.Seed();
        var mine = await AddDocument(fixture, s.Chair, "a.pdf");
        var theirs = await AddDocument(fixture, s.Member, "b.pdf");
        using var client = fixture.Client(s.Chair);

        Assert.Equal((400, "MSG249"), Pick(await Read(await client.PostAsJsonAsync(Url(s), new { documentIds = Array.Empty<Guid>() }))));
        Assert.Equal((400, "MSG249"), Pick(await Read(await client.PostAsJsonAsync(Url(s), new { documentIds = new[] { mine, theirs } }))));
        Assert.Equal((201, "MSG161"), Pick(await Read(await client.PostAsJsonAsync(Url(s), new { documentIds = new[] { mine } }))));
        Assert.Equal((409, "MSG165"), Pick(await Read(await client.PostAsJsonAsync(Url(s), new { documentIds = new[] { mine } }))));
        await using var db = fixture.Db();
        Assert.Equal(1, await db.MeetingDocuments.CountAsync(d => d.MeetingId == s.MeetingId));
    }

    [Fact]
    public async Task Adding_from_the_library_is_for_the_current_chair_or_secretary_until_the_meeting_ends()
    {
        var s = await fixture.Seed();
        var memberDocument = await AddDocument(fixture, s.Member, "a.pdf");
        var outsiderDocument = await AddDocument(fixture, s.Outsider, "b.pdf");
        var secretaryDocument = await AddDocument(fixture, s.Secretary, "c.pdf");

        using (var member = fixture.Client(s.Member))
            Assert.Equal((403, "MSG162"), Pick(await Read(await member.PostAsJsonAsync(Url(s), new { documentIds = new[] { memberDocument } }))));
        using (var outsider = fixture.Client(s.Outsider))
            Assert.Equal((404, "MSG174"), Pick(await Read(await outsider.PostAsJsonAsync(Url(s), new { documentIds = new[] { outsiderDocument } }))));
        fixture.SaveFailure.When = db => db.ChangeTracker.Entries<MeetingDocument>().Any(e => e.State == EntityState.Added);
        using var secretary = fixture.Client(s.Secretary);
        Assert.Equal((503, "MSG166"), Pick(await Read(await secretary.PostAsJsonAsync(Url(s), new { documentIds = new[] { secretaryDocument } }))));
        fixture.SaveFailure.When = null;
        await SetStatus(s.MeetingId, MeetingStatus.Ended);
        Assert.Equal((409, "MSG172"), Pick(await Read(await secretary.PostAsJsonAsync(Url(s), new { documentIds = new[] { secretaryDocument } }))));
    }

    // ---- UC-33c ----

    [Fact]
    public async Task Chair_removes_a_document_keeping_its_comments_and_links_out_of_every_list()
    {
        var s = await fixture.Seed();
        var item = await AddItem(s.MeetingId);
        var document = await Attached(s);
        await using (var db = fixture.Db())
        {
            db.MeetingDocumentAgendaItems.Add(new() { MeetingDocumentId = document, AgendaItemId = item });
            db.DocumentComments.Add(new() { MeetingDocumentId = document, AuthorId = s.Member, Body = "Góp ý" });
            await db.SaveChangesAsync();
        }
        await using var hub = fixture.Hub(s.Member);
        var events = Channel.CreateUnbounded<JsonElement>();
        hub.On<JsonElement>("documentsChanged", e => events.Writer.TryWrite(e));
        await hub.StartAsync();
        await hub.InvokeAsync("JoinMeeting", s.MeetingId);
        using var client = fixture.Client(s.Chair);

        var (status, code, _) = await Read(await client.DeleteAsync($"{Url(s)}/{document}"));

        Assert.Equal((200, "MSG167"), (status, code));
        using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(5));
        Assert.Equal(s.MeetingId, (await events.Reader.ReadAsync(timeout.Token)).GetProperty("meetingId").GetGuid());
        var (_, _, list) = await Read(await client.GetAsync(Url(s)));
        Assert.Equal(0, list.GetProperty("general").GetArrayLength());
        Assert.Equal(0, list.GetProperty("agendaItems")[0].GetProperty("documents").GetArrayLength());
        await using var check = fixture.Db();
        var row = await check.MeetingDocuments.SingleAsync(d => d.Id == document);
        Assert.Equal(s.Chair, row.RemovedById);
        Assert.NotNull(row.RemovedAt);
        Assert.False(await check.MeetingDocumentAgendaItems.AnyAsync(l => l.MeetingDocumentId == document));
        Assert.Equal(1, await check.DocumentComments.CountAsync(c => c.MeetingDocumentId == document));
        Assert.Equal((404, "MSG170"), Pick(await Read(await client.DeleteAsync($"{Url(s)}/{document}"))));
    }

    [Fact]
    public async Task Adding_a_removed_file_again_is_a_new_document_without_the_old_comments()
    {
        var s = await fixture.Seed();
        var library = await AddDocument(fixture, s.Secretary, "Lại.pdf");
        using var client = fixture.Client(s.Secretary);
        var (_, _, first) = await Read(await client.PostAsJsonAsync(Url(s), new { documentIds = new[] { library } }));
        var old = first.GetProperty("documents")[0].GetProperty("id").GetGuid();
        await using (var db = fixture.Db())
        {
            db.DocumentComments.Add(new() { MeetingDocumentId = old, AuthorId = s.Member, Body = "Cũ" });
            await db.SaveChangesAsync();
        }
        await client.DeleteAsync($"{Url(s)}/{old}");

        var (status, _, again) = await Read(await client.PostAsJsonAsync(Url(s), new { documentIds = new[] { library } }));

        Assert.Equal(201, status);
        var (_, _, list) = await Read(await client.GetAsync(Url(s)));
        var row = list.GetProperty("general")[0];
        Assert.NotEqual(old, row.GetProperty("id").GetGuid());
        Assert.Equal(again.GetProperty("documents")[0].GetProperty("id").GetGuid(), row.GetProperty("id").GetGuid());
        Assert.Equal(0, row.GetProperty("commentCount").GetInt32());
    }

    [Fact]
    public async Task Removing_is_refused_to_others_after_the_end_for_another_meetings_document_and_on_failure()
    {
        var s = await fixture.Seed();
        var document = await Attached(s);
        var other = await fixture.Seed();
        var elsewhere = await Attached(other);

        using (var member = fixture.Client(s.Member))
            Assert.Equal((403, "MSG169"), Pick(await Read(await member.DeleteAsync($"{Url(s)}/{document}"))));
        using var secretary = fixture.Client(s.Secretary);
        // BR-211: an ID of another meeting answers like a removed one.
        Assert.Equal((404, "MSG170"), Pick(await Read(await secretary.DeleteAsync($"{Url(s)}/{elsewhere}"))));
        fixture.SaveFailure.When = db => db.ChangeTracker.Entries<MeetingDocument>().Any(e => e.State == EntityState.Modified);
        Assert.Equal((503, "MSG171"), Pick(await Read(await secretary.DeleteAsync($"{Url(s)}/{document}"))));
        fixture.SaveFailure.When = null;
        await SetStatus(s.MeetingId, MeetingStatus.Ended);
        Assert.Equal((409, "MSG248"), Pick(await Read(await secretary.DeleteAsync($"{Url(s)}/{document}"))));
        using var outsider = fixture.Client(s.Outsider);
        Assert.Equal((404, "MSG174"), Pick(await Read(await outsider.DeleteAsync($"{Url(s)}/{document}"))));
    }

    // ---- UC-33b ----

    [Fact]
    public async Task Chair_links_meeting_documents_to_an_item_keeping_their_other_links()
    {
        var s = await fixture.Seed();
        var first = await AddItem(s.MeetingId, 1);
        var second = await AddItem(s.MeetingId, 2);
        var a = await Attached(s, "a.pdf");
        var b = await Attached(s, "b.pdf");
        using var client = fixture.Client(s.Chair);
        Assert.Equal((200, "MSG238"), Pick(await Read(await client.PostAsJsonAsync(Links(s, first), new { documentIds = new[] { a } }))));

        var (status, code, _) = await Read(await client.PostAsJsonAsync(Links(s, second), new { documentIds = new[] { a, b } }));

        Assert.Equal((200, "MSG238"), (status, code));
        var (_, _, list) = await Read(await client.GetAsync(Url(s)));
        var groups = list.GetProperty("agendaItems").EnumerateArray()
            .Select(g => g.GetProperty("documents").EnumerateArray().Select(d => d.GetProperty("id").GetGuid()).Order().ToArray()).ToArray();
        Assert.Equal([a], groups[0]);
        Assert.Equal(new[] { a, b }.Order(), groups[1]);
        Assert.Equal((409, "MSG243"), Pick(await Read(await client.PostAsJsonAsync(Links(s, second), new { documentIds = new[] { b } }))));
    }

    [Fact]
    public async Task Linking_checks_the_item_and_that_every_document_is_still_in_the_meeting_all_or_none()
    {
        var s = await fixture.Seed();
        var item = await AddItem(s.MeetingId);
        var other = await fixture.Seed();
        var otherItem = await AddItem(other.MeetingId);
        var kept = await Attached(s, "a.pdf");
        var removed = await Attached(s, "b.pdf");
        using var client = fixture.Client(s.Secretary);
        await client.DeleteAsync($"{Url(s)}/{removed}");

        Assert.Equal((404, "MSG242"), Pick(await Read(await client.PostAsJsonAsync(Links(s, otherItem), new { documentIds = new[] { kept } }))));
        Assert.Equal((404, "MSG170"), Pick(await Read(await client.PostAsJsonAsync(Links(s, item), new { documentIds = new[] { kept, removed } }))));
        Assert.Equal((404, "MSG170"), Pick(await Read(await client.PostAsJsonAsync(Links(s, item), new { documentIds = Array.Empty<Guid>() }))));
        await using var db = fixture.Db();
        Assert.False(await db.MeetingDocumentAgendaItems.AnyAsync(l => l.AgendaItemId == item));
    }

    [Fact]
    public async Task Linking_is_refused_to_others_after_the_end_and_on_failure()
    {
        var s = await fixture.Seed();
        var item = await AddItem(s.MeetingId);
        var document = await Attached(s);

        using (var member = fixture.Client(s.Member))
            Assert.Equal((403, "MSG240"), Pick(await Read(await member.PostAsJsonAsync(Links(s, item), new { documentIds = new[] { document } }))));
        using var secretary = fixture.Client(s.Secretary);
        fixture.SaveFailure.When = db => db.ChangeTracker.Entries<MeetingDocumentAgendaItem>().Any(e => e.State == EntityState.Added);
        Assert.Equal((503, "MSG244"), Pick(await Read(await secretary.PostAsJsonAsync(Links(s, item), new { documentIds = new[] { document } }))));
        fixture.SaveFailure.When = null;
        await SetStatus(s.MeetingId, MeetingStatus.Ended);
        Assert.Equal((409, "MSG247"), Pick(await Read(await secretary.PostAsJsonAsync(Links(s, item), new { documentIds = new[] { document } }))));
    }

    [Fact]
    public async Task From_the_library_adds_to_the_meeting_and_links_in_one_go()
    {
        var s = await fixture.Seed();
        var item = await AddItem(s.MeetingId);
        var library = await AddDocument(fixture, s.Chair, "Từ kho.pdf");
        using var client = fixture.Client(s.Chair);

        var (status, code, body) = await Read(await client.PostAsJsonAsync($"{Links(s, item)}/from-library", new { documentIds = new[] { library } }));

        Assert.Equal((200, "MSG238"), (status, code));
        var added = body.GetProperty("documents")[0].GetProperty("id").GetGuid();
        var (_, _, list) = await Read(await client.GetAsync(Url(s)));
        Assert.Equal(added, list.GetProperty("agendaItems")[0].GetProperty("documents")[0].GetProperty("id").GetGuid());
        Assert.Equal((409, "MSG165"), Pick(await Read(await client.PostAsJsonAsync($"{Links(s, item)}/from-library",
            new { documentIds = new[] { library } }))));
        Assert.Equal((400, "MSG249"), Pick(await Read(await client.PostAsJsonAsync($"{Links(s, item)}/from-library",
            new { documentIds = new[] { Guid.NewGuid() } }))));
        var another = await AddDocument(fixture, s.Chair, "Khác.pdf");
        fixture.SaveFailure.When = db => db.ChangeTracker.Entries<MeetingDocument>().Any(e => e.State == EntityState.Added);
        Assert.Equal((503, "MSG166"), Pick(await Read(await client.PostAsJsonAsync($"{Links(s, item)}/from-library",
            new { documentIds = new[] { another } }))));
    }

    [Fact]
    public async Task Upload_into_an_item_past_the_storage_limit_adds_nothing()
    {
        var s = await fixture.Seed();
        var item = await AddItem(s.MeetingId);
        await using var _ = await FillStorage(fixture, s.Secretary, room: 10);
        using var client = fixture.Client(s.Secretary);

        Assert.Equal((409, "MSG346"), Pick(await Read(await client.PostAsync($"{Links(s, item)}/upload",
            DocumentFixture.Form(("x.pdf", SampleFiles.Pdf(), null))))));

        await using var db = fixture.Db();
        Assert.Equal(0, await db.MeetingDocuments.CountAsync(d => d.MeetingId == s.MeetingId));
    }

    [Fact]
    public async Task Upload_into_an_item_adds_to_the_library_folder_the_meeting_and_the_item()
    {
        var s = await fixture.Seed();
        var item = await AddItem(s.MeetingId);
        using var client = fixture.Client(s.Secretary);

        var (status, code, body) = await Read(await client.PostAsync($"{Links(s, item)}/upload",
            DocumentFixture.Form(("Tải thẳng.pdf", SampleFiles.Pdf("thẳng"), "2"))));

        Assert.Equal((200, "MSG238"), (status, code));
        var added = body.GetProperty("documents")[0].GetProperty("id").GetGuid();
        await using var db = fixture.Db();
        var row = await db.MeetingDocuments.Include(d => d.Document).ThenInclude(d => d!.Folder).SingleAsync(d => d.Id == added);
        Assert.Equal(s.MeetingId, row.Document!.Folder!.MeetingId);
        Assert.True(await db.MeetingDocumentAgendaItems.AnyAsync(l => l.MeetingDocumentId == added && l.AgendaItemId == item));
        // Refused before the body is read.
        Assert.Equal((404, "MSG242"), Pick(await Read(await client.PostAsync($"{Links(s, Guid.NewGuid())}/upload",
            DocumentFixture.Form(("x.pdf", SampleFiles.Pdf(), null))))));
        Assert.Equal((400, "MSG164"), Pick(await Read(await client.PostAsync($"{Links(s, item)}/upload",
            DocumentFixture.Form(("x.pdf", SampleFiles.Png(), null))))));
        using var member = fixture.Client(s.Member);
        Assert.Equal((403, "MSG240"), Pick(await Read(await member.PostAsync($"{Links(s, item)}/upload",
            DocumentFixture.Form(("x.pdf", SampleFiles.Pdf(), null))))));
    }

    // ---- UC-33d ----

    [Fact]
    public async Task Unlinking_drops_one_link_and_the_document_stays_in_the_meeting()
    {
        var s = await fixture.Seed();
        var first = await AddItem(s.MeetingId, 1);
        var second = await AddItem(s.MeetingId, 2);
        var document = await Attached(s);
        using var client = fixture.Client(s.Chair);
        await client.PostAsJsonAsync(Links(s, first), new { documentIds = new[] { document } });
        await client.PostAsJsonAsync(Links(s, second), new { documentIds = new[] { document } });

        Assert.Equal((200, "MSG239"), Pick(await Read(await client.DeleteAsync($"{Links(s, first)}/{document}"))));
        var (_, _, list) = await Read(await client.GetAsync(Url(s)));
        Assert.Equal(0, list.GetProperty("agendaItems")[0].GetProperty("documents").GetArrayLength());
        Assert.Equal(1, list.GetProperty("agendaItems")[1].GetProperty("documents").GetArrayLength());

        Assert.Equal((200, "MSG239"), Pick(await Read(await client.DeleteAsync($"{Links(s, second)}/{document}"))));
        (_, _, list) = await Read(await client.GetAsync(Url(s)));
        // No link left: a general document (BR-280).
        Assert.Equal(document, list.GetProperty("general")[0].GetProperty("id").GetGuid());
        Assert.Equal((404, "MSG246"), Pick(await Read(await client.DeleteAsync($"{Links(s, second)}/{document}"))));
    }

    [Fact]
    public async Task Unlinking_is_refused_to_others_for_a_removed_document_an_unknown_item_after_the_end_and_on_failure()
    {
        var s = await fixture.Seed();
        var item = await AddItem(s.MeetingId);
        var document = await Attached(s, "a.pdf");
        var removed = await Attached(s, "b.pdf");
        using var secretary = fixture.Client(s.Secretary);
        await secretary.PostAsJsonAsync(Links(s, item), new { documentIds = new[] { document } });
        await secretary.DeleteAsync($"{Url(s)}/{removed}");

        using (var member = fixture.Client(s.Member))
            Assert.Equal((403, "MSG241"), Pick(await Read(await member.DeleteAsync($"{Links(s, item)}/{document}"))));
        Assert.Equal((404, "MSG170"), Pick(await Read(await secretary.DeleteAsync($"{Links(s, item)}/{removed}"))));
        Assert.Equal((404, "MSG242"), Pick(await Read(await secretary.DeleteAsync($"{Links(s, Guid.NewGuid())}/{document}"))));
        fixture.SaveFailure.When = db => db.ChangeTracker.Entries<MeetingDocumentAgendaItem>().Any(e => e.State == EntityState.Deleted);
        Assert.Equal((503, "MSG245"), Pick(await Read(await secretary.DeleteAsync($"{Links(s, item)}/{document}"))));
        fixture.SaveFailure.When = null;
        await SetStatus(s.MeetingId, MeetingStatus.Ended);
        Assert.Equal((409, "MSG247"), Pick(await Read(await secretary.DeleteAsync($"{Links(s, item)}/{document}"))));
    }

    // ---- ADR-0043: out of the general group ----

    private static string General(DocumentFixture.Scenario s, Guid document) => $"{Url(s)}/{document}/general";

    private static Guid[] GeneralIds(JsonElement list)
        => list.GetProperty("general").EnumerateArray().Select(d => d.GetProperty("id").GetGuid()).ToArray();

    [Fact]
    public async Task A_document_added_straight_to_an_item_or_linked_later_stays_general_until_taken_out()
    {
        var s = await fixture.Seed();
        var first = await AddItem(s.MeetingId, 1);
        var second = await AddItem(s.MeetingId, 2);
        var general = await Attached(s, "a.pdf");
        using var client = fixture.Client(s.Chair);
        // Linking a general document only adds a reference under the item.
        await client.PostAsJsonAsync(Links(s, first), new { documentIds = new[] { general } });
        // Uploaded straight into an item: under the item and in the general group.
        var (_, _, uploaded) = await Read(await client.PostAsync($"{Links(s, second)}/upload",
            DocumentFixture.Form(("b.pdf", SampleFiles.Pdf("b"), null))));
        var direct = uploaded.GetProperty("documents")[0].GetProperty("id").GetGuid();
        var (_, _, list) = await Read(await client.GetAsync(Url(s)));
        Assert.Equal([general, direct], GeneralIds(list));

        Assert.Equal((200, "MSG368"), Pick(await Read(await client.DeleteAsync(General(s, general)))));
        (_, _, list) = await Read(await client.GetAsync(Url(s)));
        Assert.Equal([direct], GeneralIds(list));
        Assert.Equal(general, list.GetProperty("agendaItems")[0].GetProperty("documents")[0].GetProperty("id").GetGuid());
        Assert.Equal((404, "MSG370"), Pick(await Read(await client.DeleteAsync(General(s, general)))));

        // Its last link gone, it is general again (BR-280), and stays so when linked again.
        Assert.Equal((200, "MSG239"), Pick(await Read(await client.DeleteAsync($"{Links(s, first)}/{general}"))));
        await client.PostAsJsonAsync(Links(s, second), new { documentIds = new[] { general } });
        (_, _, list) = await Read(await client.GetAsync(Url(s)));
        Assert.Equal([general, direct], GeneralIds(list));
    }

    [Fact]
    public async Task Taking_out_of_the_general_group_needs_a_link_the_role_an_open_meeting_and_a_save()
    {
        var s = await fixture.Seed();
        var item = await AddItem(s.MeetingId);
        var linked = await Attached(s, "a.pdf");
        var alone = await Attached(s, "b.pdf");
        var removed = await Attached(s, "c.pdf");
        using var secretary = fixture.Client(s.Secretary);
        await secretary.PostAsJsonAsync(Links(s, item), new { documentIds = new[] { linked, removed } });
        await secretary.DeleteAsync($"{Url(s)}/{removed}");

        // Nowhere else to show it: the general group is its only place.
        Assert.Equal((409, "MSG369"), Pick(await Read(await secretary.DeleteAsync(General(s, alone)))));
        Assert.Equal((404, "MSG170"), Pick(await Read(await secretary.DeleteAsync(General(s, removed)))));
        using (var member = fixture.Client(s.Member))
            Assert.Equal((403, "MSG241"), Pick(await Read(await member.DeleteAsync(General(s, linked)))));
        fixture.SaveFailure.When = db => db.ChangeTracker.Entries<MeetingDocument>().Any(e => e.State == EntityState.Modified);
        Assert.Equal((503, "MSG245"), Pick(await Read(await secretary.DeleteAsync(General(s, linked)))));
        fixture.SaveFailure.When = null;
        await using (var db = fixture.Db())
            Assert.False(await db.MeetingDocuments.Where(d => d.Id == linked).Select(d => d.ExcludedFromGeneral).SingleAsync());
        await SetStatus(s.MeetingId, MeetingStatus.Ended);
        Assert.Equal((409, "MSG247"), Pick(await Read(await secretary.DeleteAsync(General(s, linked)))));
        await SetStatus(s.MeetingId, MeetingStatus.Cancelled);
        Assert.Equal((409, "MSG349"), Pick(await Read(await secretary.DeleteAsync(General(s, linked)))));
    }

    // ---- Across the changes ----

    [Fact]
    public async Task Every_change_is_refused_once_the_meeting_is_cancelled()
    {
        var s = await fixture.Seed();
        var item = await AddItem(s.MeetingId);
        var document = await Attached(s, "a.pdf");
        var library = await AddDocument(fixture, s.Secretary, "b.pdf");
        using var client = fixture.Client(s.Secretary);
        await client.PostAsJsonAsync(Links(s, item), new { documentIds = new[] { document } });
        await SetStatus(s.MeetingId, MeetingStatus.Cancelled);

        foreach (var response in new[]
        {
            await client.PostAsync(s.Url, DocumentFixture.Form(("x.pdf", SampleFiles.Pdf(), null))),
            await client.PostAsJsonAsync(Url(s), new { documentIds = new[] { library } }),
            await client.DeleteAsync($"{Url(s)}/{document}"),
            await client.PostAsJsonAsync(Links(s, item), new { documentIds = new[] { document } }),
            await client.PostAsJsonAsync($"{Links(s, item)}/from-library", new { documentIds = new[] { library } }),
            await client.PostAsync($"{Links(s, item)}/upload", DocumentFixture.Form(("x.pdf", SampleFiles.Pdf(), null))),
            await client.DeleteAsync($"{Links(s, item)}/{document}"),
        })
            Assert.Equal((409, "MSG349"), Pick(await Read(response)));
        await using var db = fixture.Db();
        Assert.Equal(1, await db.MeetingDocuments.CountAsync(d => d.MeetingId == s.MeetingId && d.RemovedAt == null));
        Assert.True(await db.MeetingDocumentAgendaItems.AnyAsync(l => l.MeetingDocumentId == document));
    }

    [Fact]
    public async Task Every_change_tells_the_meeting_room_to_reload_its_documents()
    {
        var s = await fixture.Seed();
        var item = await AddItem(s.MeetingId);
        var library = await AddDocument(fixture, s.Secretary, "a.pdf");
        var another = await AddDocument(fixture, s.Secretary, "b.pdf");
        await using var hub = fixture.Hub(s.Member);
        var events = Channel.CreateUnbounded<JsonElement>();
        hub.On<JsonElement>("documentsChanged", e => events.Writer.TryWrite(e));
        await hub.StartAsync();
        await hub.InvokeAsync("JoinMeeting", s.MeetingId);
        using var client = fixture.Client(s.Secretary);
        Guid document = default;

        foreach (var change in new Func<Task<HttpResponseMessage>>[]
        {
            () => client.PostAsJsonAsync(Url(s), new { documentIds = new[] { library } }),
            () => client.PostAsync(s.Url, DocumentFixture.Form(("x.pdf", SampleFiles.Pdf(), null))),
            () => client.PostAsJsonAsync(Links(s, item), new { documentIds = new[] { document } }),
            () => client.PostAsJsonAsync($"{Links(s, item)}/from-library", new { documentIds = new[] { another } }),
            () => client.PostAsync($"{Links(s, item)}/upload", DocumentFixture.Form(("y.pdf", SampleFiles.Pdf("y"), null))),
            () => client.DeleteAsync($"{Links(s, item)}/{document}"),
            () => client.PostAsJsonAsync(Links(s, item), new { documentIds = new[] { document } }),
            () => client.DeleteAsync(General(s, document)),
        })
        {
            var (status, _, body) = await Read(await change());
            Assert.InRange(status, 200, 201);
            if (document == default) document = body.GetProperty("documents")[0].GetProperty("id").GetGuid();
            using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(5));
            Assert.Equal(s.MeetingId, (await events.Reader.ReadAsync(timeout.Token)).GetProperty("meetingId").GetGuid());
        }
    }

    [Fact]
    public async Task Changes_are_refused_to_a_removed_manager_another_meetings_chair_and_a_deactivated_account()
    {
        var s = await fixture.Seed();
        var item = await AddItem(s.MeetingId);
        var document = await Attached(s);
        var other = await fixture.Seed();
        var library = await AddDocument(fixture, s.Chair, "a.pdf");
        await using (var db = fixture.Db())
            await db.MeetingParticipants.Where(p => p.MeetingId == s.MeetingId && p.UserId == s.Chair)
                .ExecuteUpdateAsync(p => p.SetProperty(x => x.AssignmentStatus, AssignmentStatus.Removed)
                    .SetProperty(x => x.RemovedAt, DateTimeOffset.UtcNow).SetProperty(x => x.RemovedById, s.Organizer));

        // Removed, and Chair elsewhere: neither can see this meeting any more (SCRUM-22).
        foreach (var actor in new[] { s.Chair, other.Chair })
        {
            using var client = fixture.Client(actor);
            foreach (var response in new[]
            {
                await client.PostAsJsonAsync(Url(s), new { documentIds = new[] { library } }),
                await client.DeleteAsync($"{Url(s)}/{document}"),
                await client.PostAsJsonAsync(Links(s, item), new { documentIds = new[] { document } }),
                await client.DeleteAsync($"{Links(s, item)}/{document}"),
            })
                Assert.Equal((404, "MSG174"), Pick(await Read(response)));
        }
        await using (var db = fixture.Db())
            await db.Users.Where(u => u.Id == s.Secretary).ExecuteUpdateAsync(u => u.SetProperty(x => x.IsActive, false));
        using var secretary = fixture.Client(s.Secretary);
        Assert.Equal(403, (int)(await secretary.DeleteAsync($"{Url(s)}/{document}")).StatusCode);
        Assert.Equal(403, (int)(await secretary.PostAsJsonAsync(Links(s, item), new { documentIds = new[] { document } })).StatusCode);
        await using var check = fixture.Db();
        Assert.Null((await check.MeetingDocuments.SingleAsync(d => d.Id == document)).RemovedAt);
        Assert.False(await check.MeetingDocumentAgendaItems.AnyAsync(l => l.MeetingDocumentId == document));
    }
}
