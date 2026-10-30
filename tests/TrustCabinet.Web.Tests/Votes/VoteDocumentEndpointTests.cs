using System.Net.Http.Json;
using System.Text;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using TrustCabinet.Domain.Entities;
using TrustCabinet.Web.Tests.Documents;
using static TrustCabinet.Web.Tests.Documents.PersonalLibraryEndpointsTests;

namespace TrustCabinet.Web.Tests.Votes;

/// <summary>SCRUM-96: UC-76a (three sources) and UC-76b against PostgreSQL, with the object store in memory.</summary>
[Collection("Documents")]
public sealed class VoteDocumentEndpointTests(DocumentFixture fixture) : IAsyncLifetime
{
    public Task InitializeAsync() { Reset(); return Task.CompletedTask; }
    public Task DisposeAsync() { Reset(); return Task.CompletedTask; }

    private void Reset()
    {
        fixture.Storage.Reset();
        fixture.SaveFailure.Enabled = false;
        fixture.SaveFailure.When = null;
        fixture.ReadFailure.Table = null;
    }

    private static string Votes(DocumentFixture.Scenario s) => $"/api/meetings/{s.MeetingId}/votes";
    private static string Documents(DocumentFixture.Scenario s, Guid vote) => $"{Votes(s)}/{vote}/documents";

    private static (int, string?) Pick((int Status, string? Code, JsonElement Body) result) => (result.Status, result.Code);

    private async Task<Guid> Draft(DocumentFixture.Scenario s, Guid actor)
    {
        using var client = fixture.Client(actor);
        var (status, code, body) = await Read(await client.PostAsJsonAsync(Votes(s), VoteEndpointTests.Body()));
        Assert.Equal((201, "MSG210"), (status, code));
        return body.GetProperty("voteId").GetGuid();
    }

    /// <summary>A document added to the meeting through the API, as its Secretary would.</summary>
    private async Task<Guid> InMeeting(DocumentFixture.Scenario s, string name = "Tờ trình.pdf")
    {
        var document = await AddDocument(fixture, s.Secretary, name);
        using var client = fixture.Client(s.Secretary);
        var (status, _, body) = await Read(await client.PostAsJsonAsync($"/api/meetings/{s.MeetingId}/documents",
            new { documentIds = new[] { document } }));
        Assert.Equal(201, status);
        return body.GetProperty("documents")[0].GetProperty("id").GetGuid();
    }

    private async Task<JsonElement> Detail(DocumentFixture.Scenario s, Guid actor, Guid vote)
    {
        using var client = fixture.Client(actor);
        var (status, _, body) = await Read(await client.GetAsync($"{Votes(s)}/{vote}"));
        Assert.Equal(200, status);
        return body;
    }

    private static Guid[] Ids(JsonElement detail)
        => detail.GetProperty("documents").EnumerateArray().Select(d => d.GetProperty("id").GetGuid()).ToArray();

    [Fact]
    public async Task Secretary_attaches_meeting_documents_to_a_draft_and_detaches_one_that_stays_in_the_meeting()
    {
        var s = await fixture.Seed();
        var first = await InMeeting(s, "Tờ trình.pdf");
        var second = await InMeeting(s, "Phụ lục.pdf");
        var vote = await Draft(s, s.Secretary);
        using var secretary = fixture.Client(s.Secretary);

        var (status, code, body) = await Read(await secretary.PostAsJsonAsync(Documents(s, vote),
            new { meetingDocumentIds = new[] { first, second, first } }));
        Assert.Equal((200, "MSG265"), (status, code));
        Assert.Equal(vote, body.GetProperty("voteId").GetGuid());
        Assert.Empty(body.GetProperty("documents").EnumerateArray());
        var detail = await Detail(s, s.Chair, vote);
        Assert.Equal([first, second], Ids(detail));
        Assert.Equal(2, detail.GetProperty("documentCount").GetInt32());
        Assert.All(detail.GetProperty("documents").EnumerateArray(),
            d => Assert.Equal(JsonValueKind.Null, d.GetProperty("readConfirmedAt").ValueKind));

        // Nothing is attached twice, and a selection with one bad document attaches none of it (BR-304).
        Assert.Equal((409, "MSG266"), Pick(await Read(await secretary.PostAsJsonAsync(Documents(s, vote),
            new { meetingDocumentIds = new[] { first } }))));
        Assert.Equal((404, "MSG170"), Pick(await Read(await secretary.PostAsJsonAsync(Documents(s, vote),
            new { meetingDocumentIds = new[] { Guid.CreateVersion7() } }))));
        Assert.Equal((404, "MSG170"), Pick(await Read(await secretary.PostAsJsonAsync(Documents(s, vote),
            new { meetingDocumentIds = Array.Empty<Guid>() }))));
        Assert.Equal((404, "MSG170"), Pick(await Read(await secretary.PostAsync(Documents(s, vote),
            new StringContent("""{"meetingDocumentIds":["x"]}""", Encoding.UTF8, "application/json")))));

        // A draft and its documents are the Chair's and the Secretary's alone (BR-253).
        using (var member = fixture.Client(s.Member))
            Assert.Equal((404, "MSG205"), Pick(await Read(await member.DeleteAsync($"{Documents(s, vote)}/{first}"))));

        Assert.Equal((200, "MSG268"), Pick(await Read(await secretary.DeleteAsync($"{Documents(s, vote)}/{first}"))));
        Assert.Equal((404, "MSG269"), Pick(await Read(await secretary.DeleteAsync($"{Documents(s, vote)}/{first}"))));
        Assert.Equal([second], Ids(await Detail(s, s.Secretary, vote)));
        await using var db = fixture.Db();
        // Detaching keeps the document in the meeting (BR-305).
        Assert.Null((await db.MeetingDocuments.SingleAsync(d => d.Id == first)).RemovedAt);
    }

    [Fact]
    public async Task Chair_attaches_from_the_library_and_from_the_computer_adding_them_to_the_meeting_in_the_same_save()
    {
        var s = await fixture.Seed();
        var vote = await Draft(s, s.Chair);
        var mine = await AddDocument(fixture, s.Chair, "Báo cáo.pdf");
        var theirs = await AddDocument(fixture, s.Member, "Của người khác.pdf");
        using var chair = fixture.Client(s.Chair);

        var (status, code, body) = await Read(await chair.PostAsJsonAsync($"{Documents(s, vote)}/from-library",
            new { documentIds = new[] { mine } }));
        Assert.Equal((200, "MSG265"), (status, code));
        var fromLibrary = body.GetProperty("documents")[0].GetProperty("id").GetGuid();
        Assert.Equal((409, "MSG165"), Pick(await Read(await chair.PostAsJsonAsync($"{Documents(s, vote)}/from-library",
            new { documentIds = new[] { mine } }))));
        Assert.Equal((400, "MSG249"), Pick(await Read(await chair.PostAsJsonAsync($"{Documents(s, vote)}/from-library",
            new { documentIds = new[] { theirs } }))));

        (status, code, body) = await Read(await chair.PostAsync($"{Documents(s, vote)}/upload",
            DocumentFixture.Form(("Tờ trình.pdf", SampleFiles.Pdf(), "3"))));
        Assert.Equal((200, "MSG265"), (status, code));
        var uploaded = body.GetProperty("documents")[0].GetProperty("id").GetGuid();
        Assert.Equal((400, "MSG164"), Pick(await Read(await chair.PostAsync($"{Documents(s, vote)}/upload",
            DocumentFixture.Form(("ảnh.png", SampleFiles.Png(), null))))));
        Assert.Equal((400, "MSG288"), Pick(await Read(await chair.PostAsync($"{Documents(s, vote)}/upload",
            DocumentFixture.Form(Enumerable.Range(0, 11).Select(i => ($"{i}.pdf", SampleFiles.Pdf($"{i}"), (string?)"1")).ToArray())))));

        Assert.Equal([fromLibrary, uploaded], Ids(await Detail(s, s.Chair, vote)));
        await using var db = fixture.Db();
        var rows = await db.MeetingDocuments.Where(d => d.MeetingId == s.MeetingId).ToListAsync();
        Assert.Equal([fromLibrary, uploaded], rows.Select(r => r.Id).Order());
        // The uploaded file also lands in the uploader's library (BR-210).
        var upload = rows.Single(r => r.Id == uploaded);
        Assert.Equal(s.Chair, (await db.Documents.SingleAsync(d => d.Id == upload.DocumentId)).OwnerId);
    }

    [Fact]
    public async Task Documents_change_only_while_the_vote_is_a_draft_and_only_by_the_chair_or_secretary()
    {
        var s = await fixture.Seed();
        var document = await InMeeting(s);
        var vote = await Draft(s, s.Secretary);
        using var secretary = fixture.Client(s.Secretary);
        Assert.Equal((200, "MSG265"), Pick(await Read(await secretary.PostAsJsonAsync(Documents(s, vote),
            new { meetingDocumentIds = new[] { document } }))));
        var other = await InMeeting(s, "Khác.pdf");
        using (var chair = fixture.Client(s.Chair))
            Assert.Equal((200, "MSG217"), Pick(await Read(await chair.PostAsync($"{Votes(s)}/{vote}/open", null))));

        // The list is fixed once the vote opens (BR-288).
        Assert.Equal((409, "MSG213"), Pick(await Read(await secretary.PostAsJsonAsync(Documents(s, vote),
            new { meetingDocumentIds = new[] { other } }))));
        Assert.Equal((409, "MSG213"), Pick(await Read(await secretary.DeleteAsync($"{Documents(s, vote)}/{document}"))));
        var objects = fixture.Storage.Objects.Count;
        // Refused before the body is read: no byte reaches the store.
        Assert.Equal((409, "MSG213"), Pick(await Read(await secretary.PostAsync($"{Documents(s, vote)}/upload",
            DocumentFixture.Form(("a.pdf", SampleFiles.Pdf(), "1"))))));
        Assert.Equal(objects, fixture.Storage.Objects.Count);
        using (var member = fixture.Client(s.Member))
            Assert.Equal((409, "MSG213"), Pick(await Read(await member.PostAsJsonAsync(Documents(s, vote),
                new { meetingDocumentIds = new[] { other } }))));
        using (var outsider = fixture.Client(s.Outsider))
            Assert.Equal((404, "MSG205"), Pick(await Read(await outsider.PostAsJsonAsync(Documents(s, vote),
                new { meetingDocumentIds = new[] { other } }))));
        await using var db = fixture.Db();
        Assert.Equal([document], await db.VoteDocuments.Where(l => l.VotingSessionId == vote).Select(l => l.MeetingDocumentId).ToListAsync());
    }

    [Fact]
    public async Task A_failed_save_attaches_detaches_and_adds_nothing()
    {
        var s = await fixture.Seed();
        var document = await InMeeting(s);
        var vote = await Draft(s, s.Secretary);
        var library = await AddDocument(fixture, s.Secretary, "Kho.pdf");
        using var secretary = fixture.Client(s.Secretary);

        fixture.SaveFailure.When = db => db.ChangeTracker.Entries<VoteDocument>().Any(e => e.State == EntityState.Added);
        Assert.Equal((503, "MSG267"), Pick(await Read(await secretary.PostAsJsonAsync(Documents(s, vote),
            new { meetingDocumentIds = new[] { document } }))));
        Assert.Equal((503, "MSG166"), Pick(await Read(await secretary.PostAsJsonAsync($"{Documents(s, vote)}/from-library",
            new { documentIds = new[] { library } }))));
        var objects = fixture.Storage.Objects.Count;
        Assert.Equal((503, "MSG166"), Pick(await Read(await secretary.PostAsync($"{Documents(s, vote)}/upload",
            DocumentFixture.Form(("Tải lên.pdf", SampleFiles.Pdf("upload"), "1"))))));
        // The bytes of a failed upload are deleted again (BR-286).
        Assert.Equal(objects, fixture.Storage.Objects.Count);
        await using (var db = fixture.Db())
        {
            Assert.False(await db.VoteDocuments.AnyAsync(l => l.VotingSessionId == vote));
            Assert.Equal(1, await db.MeetingDocuments.CountAsync(d => d.MeetingId == s.MeetingId));
        }

        fixture.SaveFailure.When = null;
        Assert.Equal((200, "MSG265"), Pick(await Read(await secretary.PostAsJsonAsync(Documents(s, vote),
            new { meetingDocumentIds = new[] { document } }))));
        fixture.SaveFailure.When = db => db.ChangeTracker.Entries<VoteDocument>().Any(e => e.State == EntityState.Deleted);
        Assert.Equal((503, "MSG270"), Pick(await Read(await secretary.DeleteAsync($"{Documents(s, vote)}/{document}"))));
        fixture.SaveFailure.When = db => db.ChangeTracker.Entries<DocumentReadConfirmation>().Any(e => e.State == EntityState.Added);
        Assert.Equal((503, "MSG255"), Pick(await Read(await secretary.PostAsJsonAsync(
            $"/api/meetings/{s.MeetingId}/documents/{document}/read-confirmation", new { lastPageReached = true }))));
        await using var after = fixture.Db();
        Assert.True(await after.VoteDocuments.AnyAsync(l => l.VotingSessionId == vote));
        Assert.False(await after.DocumentReadConfirmations.AnyAsync(c => c.MeetingDocumentId == document));
    }
}
