using System.Net.Http.Json;
using System.Text.Json;
using System.Threading.Channels;
using Microsoft.AspNetCore.SignalR.Client;
using Microsoft.EntityFrameworkCore;
using TrustCabinet.Domain.Audit;
using TrustCabinet.Domain.Documents;
using TrustCabinet.Domain.Entities;
using TrustCabinet.Domain.Enums;
using static TrustCabinet.Web.Tests.Agenda.AgendaEndpointTests;

namespace TrustCabinet.Web.Tests.Documents;

/// <summary>SCRUM-21 — GET documents (UC-32a) and GET content (UC-32c) against PostgreSQL.</summary>
[Collection("Documents")]
public sealed class MeetingDocumentReadEndpointsTests(DocumentFixture fixture) : IAsyncLifetime
{
    public Task InitializeAsync() { Reset(); return Task.CompletedTask; }
    public Task DisposeAsync() { Reset(); return Task.CompletedTask; }

    private void Reset()
    {
        fixture.Storage.Reset();
        fixture.SaveFailure.Enabled = false;
        fixture.SaveFailure.FailAudit = false;
        fixture.ReadFailure.Table = null;
    }

    /// <summary>A meeting document with its stored file; the object is put in the store only when bytes are given.</summary>
    private async Task<Guid> AddDocument(Guid meetingId, Guid uploader, string name, byte[]? bytes = null,
        Guid[]? items = null, bool removed = false, int comments = 0, int? pages = null)
    {
        await using var db = fixture.Db();
        var fileId = Guid.CreateVersion7();
        var content = bytes ?? SampleFiles.Pdf(name);
        var document = new MeetingDocument
        {
            MeetingId = meetingId, FileName = name, AddedById = uploader, AddedAt = DateTimeOffset.UtcNow,
            RemovedAt = removed ? DateTimeOffset.UtcNow : null, RemovedById = removed ? uploader : null,
            StoredFile = new StoredFile { Id = fileId, StorageKey = DocumentFileRules.StorageKey(fileId),
                OriginalFileName = name, ContentType = DocumentFileRules.PdfContentType, ByteSize = content.Length,
                ContentSha256 = new string('a', 64), PageCount = pages, UploadedById = uploader, UploadedAt = DateTimeOffset.UtcNow },
        };
        foreach (var item in items ?? []) document.AgendaItemLinks.Add(new() { AgendaItemId = item });
        for (var i = 0; i < comments; i++) document.Comments.Add(new() { AuthorId = uploader, Body = $"Góp ý {i}" });
        db.MeetingDocuments.Add(document);
        await db.SaveChangesAsync();
        if (bytes is not null) fixture.Storage.Objects[document.StoredFile.StorageKey] = bytes;
        return document.Id;
    }

    private async Task<Guid[]> AddItems(Guid meetingId, params AgendaItemStatus[] statuses)
    {
        await using var db = fixture.Db();
        // Inserted in reverse so the answer must come from Position, not insertion order.
        var items = statuses.Select((status, i) => new AgendaItem { MeetingId = meetingId, Title = $"Mục {i + 1}",
            Position = i + 1, Status = status }).ToArray();
        db.AgendaItems.AddRange(items.Reverse());
        await db.SaveChangesAsync();
        return items.Select(i => i.Id).ToArray();
    }

    private async Task<List<AuditLogEntry>> Audit(Guid documentId)
    {
        await using var db = fixture.Db();
        return await db.AuditLog.Where(a => a.ObjectId == documentId).ToListAsync();
    }

    private static Guid[] Ids(JsonElement documents)
        => documents.EnumerateArray().Select(d => d.GetProperty("id").GetGuid()).ToArray();

    [Fact]
    public async Task Member_sees_documents_grouped_by_agenda_item_in_order_with_general_and_without_removed()
    {
        var s = await fixture.Seed();
        var items = await AddItems(s.MeetingId, AgendaItemStatus.Done, AgendaItemStatus.InProgress, AgendaItemStatus.Pending);
        var both = await AddDocument(s.MeetingId, s.Secretary, "Tờ trình.pdf", items: [items[0], items[2]], comments: 2, pages: 12);
        var second = await AddDocument(s.MeetingId, s.Chair, "Báo cáo.pdf", items: [items[1]]);
        var general = await AddDocument(s.MeetingId, s.Secretary, "Phụ lục.pdf");
        await AddDocument(s.MeetingId, s.Secretary, "Đã gỡ.pdf", items: [items[0]], removed: true);
        using var client = fixture.Client(s.Member);

        var response = await client.GetAsync(s.ListUrl);
        var body = await response.Content.ReadFromJsonAsync<JsonElement>();

        Assert.Equal(200, (int)response.StatusCode);
        Assert.Equal(s.MeetingId, body.GetProperty("meetingId").GetGuid());
        // The room panel puts the item under discussion first (BR-316).
        Assert.Equal(items[1], body.GetProperty("currentAgendaItemId").GetGuid());
        var groups = body.GetProperty("agendaItems").EnumerateArray().ToArray();
        Assert.Equal(items, groups.Select(g => g.GetProperty("id").GetGuid()));
        Assert.Equal([1, 2, 3], groups.Select(g => g.GetProperty("position").GetInt32()));
        Assert.Equal("InProgress", groups[1].GetProperty("status").GetString());
        // Linked to items 1 and 3: shows under both (BR-277); the removed one nowhere (BR-211).
        Assert.Equal([both], Ids(groups[0].GetProperty("documents")));
        Assert.Equal([second], Ids(groups[1].GetProperty("documents")));
        Assert.Equal([both], Ids(groups[2].GetProperty("documents")));
        // Every document is general as well, linked or not, until taken out of the group (ADR-0043).
        Assert.Equal([both, second, general], Ids(body.GetProperty("general")));

        var row = groups[0].GetProperty("documents")[0];
        Assert.Equal("Tờ trình.pdf", row.GetProperty("fileName").GetString());
        Assert.Equal(DocumentFileRules.PdfContentType, row.GetProperty("contentType").GetString());
        Assert.Equal(12, row.GetProperty("pageCount").GetInt32());
        Assert.Equal(2, row.GetProperty("commentCount").GetInt32());
        Assert.Equal(s.Secretary, row.GetProperty("addedBy").GetProperty("userId").GetGuid());
        Assert.Equal("Document test 0", row.GetProperty("addedBy").GetProperty("fullName").GetString());
        Assert.True(row.GetProperty("byteSize").GetInt64() > 0);
        Assert.Equal(JsonValueKind.String, row.GetProperty("addedAt").ValueKind);
        Assert.Equal(JsonValueKind.Null, body.GetProperty("general").EnumerateArray()
            .Single(d => d.GetProperty("id").GetGuid() == general).GetProperty("pageCount").ValueKind);
    }

    [Fact]
    public async Task Empty_meeting_has_empty_groups_and_no_current_item()
    {
        var s = await fixture.Seed(MeetingStatus.Published);
        using var client = fixture.Client(s.Member);

        var body = await client.GetFromJsonAsync<JsonElement>(s.ListUrl);

        Assert.Equal(JsonValueKind.Null, body.GetProperty("currentAgendaItemId").ValueKind);
        Assert.Empty(body.GetProperty("agendaItems").EnumerateArray());
        Assert.Empty(body.GetProperty("general").EnumerateArray());
    }

    [Fact]
    public async Task Ended_meeting_documents_stay_readable()
    {
        var s = await fixture.Seed(MeetingStatus.Ended);
        var bytes = SampleFiles.Pdf("ended");
        var id = await AddDocument(s.MeetingId, s.Secretary, "Biên bản.pdf", bytes);
        using var client = fixture.Client(s.Member);

        Assert.Equal([id], Ids((await client.GetFromJsonAsync<JsonElement>(s.ListUrl)).GetProperty("general")));
        Assert.Equal(bytes, await client.GetByteArrayAsync(s.ContentUrl(id)));
    }

    [Fact]
    public async Task Caller_who_cannot_view_the_meeting_gets_the_same_404_as_a_missing_meeting()
    {
        var s = await fixture.Seed();
        var draft = await fixture.Seed(MeetingStatus.Draft);
        var id = await AddDocument(s.MeetingId, s.Secretary, "Mật.pdf", SampleFiles.Pdf());
        using var outsider = fixture.Client(s.Outsider);
        using var draftMember = fixture.Client(draft.Member);

        var expected = await AssertCode(await outsider.GetAsync(s.ListUrl), 404, "MSG156");
        var missing = await AssertCode(await outsider.GetAsync($"/api/meetings/{Guid.NewGuid()}/documents"), 404, "MSG156");
        Assert.Equal(expected.ToString(), missing.ToString());
        // A Member does not see a Draft meeting (MeetingReadPolicy), so not its documents either.
        await AssertCode(await draftMember.GetAsync(draft.ListUrl), 404, "MSG156");
        await AssertCode(await outsider.GetAsync(s.ContentUrl(id)), 404, "MSG156");
        await AssertCode(await outsider.GetAsync(s.ContentUrl(id, download: true)), 404, "MSG156");
        Assert.Empty(await Audit(id));
    }

    [Fact]
    public async Task List_read_failure_is_MSG157_not_an_empty_list()
    {
        var s = await fixture.Seed();
        using var client = fixture.Client(s.Member);
        fixture.ReadFailure.Table = "meeting_documents";

        await AssertCode(await client.GetAsync(s.ListUrl), 500, "MSG157");
    }

    [Fact]
    public async Task View_streams_the_stored_bytes_unchanged_as_an_attachment()
    {
        var s = await fixture.Seed();
        var bytes = SampleFiles.Pdf("nội dung");
        var id = await AddDocument(s.MeetingId, s.Secretary, "Báo cáo quý 3.pdf", bytes);
        using var client = fixture.Client(s.Member);

        var response = await client.GetAsync(s.ContentUrl(id));

        Assert.Equal(200, (int)response.StatusCode);
        Assert.Equal(bytes, await response.Content.ReadAsByteArrayAsync());
        Assert.Equal(DocumentFileRules.PdfContentType, response.Content.Headers.ContentType!.MediaType);
        Assert.Equal(bytes.Length, response.Content.Headers.ContentLength);
        var disposition = response.Content.Headers.ContentDisposition!;
        Assert.Equal("attachment", disposition.DispositionType);
        // The Vietnamese name travels encoded, never raw in the header.
        Assert.Equal("Báo cáo quý 3.pdf", disposition.FileNameStar);
        Assert.Contains("no-store", response.Headers.CacheControl!.ToString());
        Assert.Equal("nosniff", response.Headers.GetValues("X-Content-Type-Options").Single());
        // Normal meeting: nothing to audit (BR-318 covers Confidential and Secret only).
        Assert.Empty(await Audit(id));
        Assert.Equal(200, (int)(await client.GetAsync(s.ContentUrl(id, download: true))).StatusCode);
        Assert.Empty(await Audit(id));
    }

    [Fact]
    public async Task Secret_meeting_documents_are_viewable_and_audited_but_not_downloadable()
    {
        var s = await fixture.Seed(sensitivity: Sensitivity.Secret);
        var bytes = SampleFiles.Pdf("secret");
        var id = await AddDocument(s.MeetingId, s.Secretary, "Tuyệt mật.pdf", bytes);
        using var client = fixture.Client(s.Member);

        Assert.Equal(bytes, await client.GetByteArrayAsync(s.ContentUrl(id)));
        var viewed = Assert.Single(await Audit(id));
        Assert.Equal(AuditActions.DocumentViewed, viewed.Action);
        Assert.Equal(AuditActions.MeetingDocumentObjectType, viewed.ObjectType);
        Assert.Equal(s.Member, viewed.ActorId);
        Assert.Contains(s.MeetingId.ToString(), viewed.Details);

        // Also the Chair: no role may download from a Secret meeting (BR-188).
        using var chair = fixture.Client(s.Chair);
        await AssertCode(await chair.GetAsync(s.ContentUrl(id, download: true)), 403, "MSG159");
        Assert.Single(await Audit(id));
    }

    [Fact]
    public async Task Confidential_meeting_download_is_audited()
    {
        var s = await fixture.Seed(sensitivity: Sensitivity.Confidential);
        var bytes = SampleFiles.Pdf("confidential");
        var id = await AddDocument(s.MeetingId, s.Secretary, "Hạn chế.pdf", bytes);
        using var client = fixture.Client(s.Organizer);

        Assert.Equal(bytes, await client.GetByteArrayAsync(s.ContentUrl(id, download: true)));

        var downloaded = Assert.Single(await Audit(id));
        Assert.Equal(AuditActions.DocumentDownloaded, downloaded.Action);
        Assert.Equal(s.Organizer, downloaded.ActorId);
    }

    [Fact]
    public async Task Removed_document_and_a_document_of_another_meeting_are_MSG156()
    {
        var s = await fixture.Seed();
        var other = await fixture.Seed();
        var removed = await AddDocument(s.MeetingId, s.Secretary, "Đã gỡ.pdf", SampleFiles.Pdf(), removed: true);
        var foreign = await AddDocument(other.MeetingId, other.Secretary, "Họp khác.pdf", SampleFiles.Pdf());
        using var client = fixture.Client(s.Member);

        var gone = await AssertCode(await client.GetAsync(s.ContentUrl(removed)), 404, "MSG156");
        // The caller can read this meeting, but not reach another meeting's file through it.
        var elsewhere = await AssertCode(await client.GetAsync(s.ContentUrl(foreign)), 404, "MSG156");
        var unknown = await AssertCode(await client.GetAsync(s.ContentUrl(Guid.NewGuid(), download: true)), 404, "MSG156");
        Assert.Equal(gone.ToString(), elsewhere.ToString());
        Assert.Equal(gone.ToString(), unknown.ToString());
    }

    [Fact]
    public async Task Secret_download_checks_attachment_before_refusing_and_refuses_before_touching_the_store()
    {
        var s = await fixture.Seed(sensitivity: Sensitivity.Secret);
        var other = await fixture.Seed(sensitivity: Sensitivity.Secret);
        var removed = await AddDocument(s.MeetingId, s.Secretary, "Đã gỡ.pdf", SampleFiles.Pdf(), removed: true);
        var foreign = await AddDocument(other.MeetingId, other.Secretary, "Họp khác.pdf", SampleFiles.Pdf());
        var noObject = await AddDocument(s.MeetingId, s.Secretary, "Mất.pdf");
        using var client = fixture.Client(s.Member);

        // A 403 here would confirm the ID exists in this meeting.
        foreach (var id in new[] { removed, foreign, Guid.NewGuid() })
            await AssertCode(await client.GetAsync(s.ContentUrl(id, download: true)), 404, "MSG156");
        // The object is missing, yet the answer is the refusal: the store is never asked.
        await AssertCode(await client.GetAsync(s.ContentUrl(noObject, download: true)), 403, "MSG159");
    }

    [Fact]
    public async Task Content_lookup_failure_is_MSG158_when_viewing_and_MSG160_when_downloading()
    {
        var s = await fixture.Seed();
        var id = await AddDocument(s.MeetingId, s.Secretary, "Báo cáo.pdf", SampleFiles.Pdf());
        using var client = fixture.Client(s.Member);
        fixture.ReadFailure.Table = "meeting_documents";

        await AssertCode(await client.GetAsync(s.ContentUrl(id)), 503, "MSG158");
        await AssertCode(await client.GetAsync(s.ContentUrl(id, download: true)), 503, "MSG160");
    }

    [Fact]
    public async Task Missing_object_is_MSG158_when_viewing_and_MSG160_when_downloading_and_is_not_audited()
    {
        var s = await fixture.Seed(sensitivity: Sensitivity.Confidential);
        var id = await AddDocument(s.MeetingId, s.Secretary, "Mất.pdf");
        using var client = fixture.Client(s.Member);

        await AssertCode(await client.GetAsync(s.ContentUrl(id)), 503, "MSG158");
        await AssertCode(await client.GetAsync(s.ContentUrl(id, download: true)), 503, "MSG160");
        fixture.Storage.Configured = false;
        await AssertCode(await client.GetAsync(s.ContentUrl(id)), 503, "MSG158");
        Assert.Empty(await Audit(id));
    }

    [Fact]
    public async Task Audit_failure_withholds_the_content()
    {
        var s = await fixture.Seed(sensitivity: Sensitivity.Secret);
        var id = await AddDocument(s.MeetingId, s.Secretary, "Mật.pdf", SampleFiles.Pdf());
        using var client = fixture.Client(s.Member);
        fixture.SaveFailure.FailAudit = true;

        await AssertCode(await client.GetAsync(s.ContentUrl(id)), 503, "MSG158");
        Assert.Empty(await Audit(id));
    }

    [Fact]
    public async Task Upload_tells_joined_readers_only()
    {
        var s = await fixture.Seed();
        var other = await fixture.Seed();
        await using var member = fixture.Hub(s.Member);
        await using var outsider = fixture.Hub(other.Member);
        // A reader of the same meeting who never joined it.
        await using var notJoined = fixture.Hub(s.Chair);
        var received = Channel.CreateUnbounded<MeetingDocumentsChange>();
        var excluded = Channel.CreateUnbounded<MeetingDocumentsChange>();
        member.On<MeetingDocumentsChange>("documentsChanged", e => received.Writer.TryWrite(e));
        outsider.On<MeetingDocumentsChange>("documentsChanged", e => excluded.Writer.TryWrite(e));
        notJoined.On<MeetingDocumentsChange>("documentsChanged", e => excluded.Writer.TryWrite(e));
        await Task.WhenAll(member.StartAsync(), outsider.StartAsync(), notJoined.StartAsync());
        await member.InvokeAsync("JoinMeeting", s.MeetingId);
        await outsider.InvokeAsync("JoinMeeting", other.MeetingId);
        using var secretary = fixture.Client(s.Secretary);

        await AssertCode(await secretary.PostAsync(s.Url, DocumentFixture.Form(("Mới.pdf", SampleFiles.Pdf(), "1"))), 201, "MSG161");

        using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(10));
        var first = await received.Reader.ReadAsync(timeout.Token);
        Assert.Equal(s.MeetingId, first.MeetingId);
        Assert.NotEqual(default, first.OccurredAt);
        // A failed upload changes nothing, so it says nothing.
        await AssertCode(await secretary.PostAsync(s.Url, DocumentFixture.Form(("anh.pdf", SampleFiles.Png(), null))), 400, "MSG164");
        await Task.Delay(500);
        Assert.False(received.Reader.TryRead(out _));
        // Every event has its own id: the FE drops an id it has already seen.
        await AssertCode(await secretary.PostAsync(s.Url, DocumentFixture.Form(("Mới 2.pdf", SampleFiles.Pdf(), "1"))), 201, "MSG161");
        var second = await received.Reader.ReadAsync(timeout.Token);
        Assert.NotEqual(Guid.Empty, first.EventId);
        Assert.NotEqual(first.EventId, second.EventId);
        Assert.False(excluded.Reader.TryRead(out _));
    }
}
