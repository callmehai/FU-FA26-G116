using System.Net;
using System.Net.Http.Json;
using System.Text;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using TrustCabinet.Domain.Entities;
using TrustCabinet.Domain.Enums;
using TrustCabinet.Web.Tests.Documents;
using TrustCabinet.Web.Tests.Participants;
using static TrustCabinet.Web.Tests.Documents.PersonalLibraryEndpointsTests;
using static TrustCabinet.Web.Tests.Participants.ParticipantEndpointTests;

namespace TrustCabinet.Web.Tests.Votes;

/// <summary>Round 1 gap tests for FE-11 (SCRUM-28, UC-52–UC-57) on the list, detail,
/// open, ballot, close and conclusion endpoints.</summary>
public sealed class VoteRound1Tests(ParticipantFixture f) : IClassFixture<ParticipantFixture>
{
    [Fact]
    public async Task VoteList_OutsideMeetingIs404()
    {
        var s = await f.Seed(MeetingStatus.InProgress);
        using var outsider = f.Client(s.Outsider);

        var response = await outsider.GetAsync($"{s.Url}/votes");

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
        Assert.Equal("MSG205", JsonDocument.Parse(await response.Content.ReadAsStringAsync()).RootElement.GetProperty("code").GetString());
    }

    [Fact]
    public async Task VoteList_RequiresToken()
    {
        var s = await f.Seed(MeetingStatus.InProgress);

        Assert.Equal(HttpStatusCode.Unauthorized, (await f.Client().GetAsync($"{s.Url}/votes")).StatusCode);
    }

    [Fact]
    public async Task VoteDetail_UnknownOrForeignVoteIs404()
    {
        var s = await f.Seed(MeetingStatus.InProgress);
        var other = await f.Seed(MeetingStatus.InProgress);
        using var chair = f.Client(s.Chair);
        using var otherChair = f.Client(other.Chair);
        var id = await VoteEndpointTests.Draft(chair, s);
        var foreign = await VoteEndpointTests.Draft(otherChair, other);

        await Code(await chair.GetAsync($"{s.Url}/votes/{Guid.CreateVersion7()}"), 404, "MSG205");
        await Code(await chair.GetAsync($"{s.Url}/votes/{foreign}"), 404, "MSG205");
        await Code(await chair.GetAsync($"{other.Url}/votes/{id}"), 404, "MSG205");
    }

    [Fact]
    public async Task VoteOpen_ByNonChairIs409()
    {
        var s = await f.Seed(MeetingStatus.InProgress);
        using var secretary = f.Client(s.Secretary);
        var id = await VoteEndpointTests.Draft(secretary, s);

        await Code(await secretary.PostAsync($"{s.Url}/votes/{id}/open", null), 409, "MSG218");

        await using var db = f.Db();
        Assert.Equal(VotingStatus.Draft, await db.VotingSessions.Where(v => v.Id == id).Select(v => v.Status).SingleAsync());
    }

    [Fact]
    public async Task Ballot_OnClosedVoteIs409()
    {
        var s = await f.Seed(MeetingStatus.InProgress);
        using var chair = f.Client(s.Chair);
        using var member = f.Client(s.Member);
        var id = await VoteEndpointTests.Opened(chair, s);
        var option = (await VoteEndpointTests.Options(chair, s, id))[0];
        await Code(await VoteEndpointTests.Ballot(member, s, id, option), 200, "MSG227");
        await Code(await chair.PostAsync($"{s.Url}/votes/{id}/close", null), 200, "MSG229");

        await Code(await VoteEndpointTests.Ballot(member, s, id, option), 409, "MSG225");

        await using var db = f.Db();
        Assert.Equal(1, await db.VoteReceipts.CountAsync(r => r.VotingSessionId == id));
    }

    [Fact]
    public async Task VoteClose_ByNonChairIs409()
    {
        var s = await f.Seed(MeetingStatus.InProgress);
        using var chair = f.Client(s.Chair);
        using var secretary = f.Client(s.Secretary);
        var id = await VoteEndpointTests.Opened(chair, s);

        await Code(await secretary.PostAsync($"{s.Url}/votes/{id}/close", null), 409, "MSG230");

        await using var db = f.Db();
        Assert.Equal(VotingStatus.Open, await db.VotingSessions.Where(v => v.Id == id).Select(v => v.Status).SingleAsync());
    }

    [Fact]
    public async Task Conclusion_OutsideAllowedSetIs400()
    {
        var s = await f.Seed(MeetingStatus.InProgress);
        using var chair = f.Client(s.Chair);
        var id = await VoteEndpointTests.Opened(chair, s);
        await Code(await chair.PostAsync($"{s.Url}/votes/{id}/close", null), 200, "MSG229");

        await Code(await chair.PostAsJsonAsync($"{s.Url}/votes/{id}/conclusion", new { conclusion = "Maybe" }), 400, "MSG232");

        await using var db = f.Db();
        Assert.Null(await db.VotingSessions.Where(v => v.Id == id).Select(v => v.Conclusion).SingleAsync());
    }

    [Fact]
    public async Task Conclusion_OnOpenVoteIs409()
    {
        var s = await f.Seed(MeetingStatus.InProgress);
        using var chair = f.Client(s.Chair);
        var id = await VoteEndpointTests.Opened(chair, s);

        await Code(await chair.PostAsJsonAsync($"{s.Url}/votes/{id}/conclusion", new { conclusion = "Passed" }), 409, "MSG234");

        await using var db = f.Db();
        Assert.Null(await db.VotingSessions.Where(v => v.Id == id).Select(v => v.Conclusion).SingleAsync());
    }
}

/// <summary>Round 1 gap tests for FE-11 vote documents (SCRUM-96, UC-76a–UC-76b): the three
/// attach sources and detaching.</summary>
[Collection("Documents")]
public sealed class VoteDocumentRound1Tests(DocumentFixture fixture) : IAsyncLifetime
{
    public Task InitializeAsync()
    {
        Reset();
        return Task.CompletedTask;
    }

    public Task DisposeAsync()
    {
        Reset();
        return Task.CompletedTask;
    }

    private void Reset()
    {
        fixture.Storage.Reset();
        fixture.SaveFailure.Enabled = false;
        fixture.SaveFailure.When = null;
        fixture.ReadFailure.Table = null;
    }

    private static string Votes(DocumentFixture.Scenario s) => $"/api/meetings/{s.MeetingId}/votes";
    private static string Documents(DocumentFixture.Scenario s, Guid vote) => $"{Votes(s)}/{vote}/documents";

    private static async Task<Guid> Draft(DocumentFixture fixture, DocumentFixture.Scenario s, Guid actor)
    {
        using var client = fixture.Client(actor);
        var (status, code, body) = await Read(await client.PostAsJsonAsync(Votes(s), VoteEndpointTests.Body()));
        Assert.Equal((201, "MSG210"), (status, code));
        return body.GetProperty("voteId").GetGuid();
    }

    /// <summary>A document added to the meeting through the API, as its Secretary would.</summary>
    private static async Task<Guid> InMeeting(DocumentFixture fixture, DocumentFixture.Scenario s, string name = "Tờ trình.pdf")
    {
        var document = await AddDocument(fixture, s.Secretary, name);
        using var client = fixture.Client(s.Secretary);
        var (status, _, body) = await Read(await client.PostAsJsonAsync($"/api/meetings/{s.MeetingId}/documents",
            new { documentIds = new[] { document } }));
        Assert.Equal(201, status);
        return body.GetProperty("documents")[0].GetProperty("id").GetGuid();
    }

    [Fact]
    public async Task VoteDocuments_AttachForeignDocumentIs404()
    {
        var s = await fixture.Seed();
        var foreign = await fixture.Seed();
        var vote = await Draft(fixture, s, s.Chair);
        var other = await InMeeting(fixture, foreign);
        using var chair = fixture.Client(s.Chair);

        var (status, code, _) = await Read(await chair.PostAsJsonAsync(Documents(s, vote),
            new { meetingDocumentIds = new[] { other } }));

        Assert.Equal((404, "MSG170"), (status, code));
        await using var db = fixture.Db();
        Assert.False(await db.VoteDocuments.AnyAsync(l => l.VotingSessionId == vote));
    }

    [Fact]
    public async Task VoteDocuments_AttachDuplicateIs409()
    {
        var s = await fixture.Seed();
        var document = await InMeeting(fixture, s);
        var vote = await Draft(fixture, s, s.Chair);
        using var chair = fixture.Client(s.Chair);
        Assert.Equal((200, "MSG265"), Pick(await Read(await chair.PostAsJsonAsync(Documents(s, vote),
            new { meetingDocumentIds = new[] { document } }))));

        Assert.Equal((409, "MSG266"), Pick(await Read(await chair.PostAsJsonAsync(Documents(s, vote),
            new { meetingDocumentIds = new[] { document } }))));
    }

    private static (int Status, string? Code) Pick((int Status, string? Code, JsonElement Body) result) => (result.Status, result.Code);

    [Fact]
    public async Task VoteDocuments_AttachFromEmptyLibraryIs400()
    {
        var s = await fixture.Seed();
        var vote = await Draft(fixture, s, s.Chair);
        using var chair = fixture.Client(s.Chair);

        var (status, code, _) = await Read(await chair.PostAsJsonAsync($"{Documents(s, vote)}/from-library",
            new { documentIds = Array.Empty<Guid>() }));

        Assert.Equal((400, "MSG249"), (status, code));
        await using var db = fixture.Db();
        Assert.False(await db.VoteDocuments.AnyAsync(l => l.VotingSessionId == vote));
    }

    [Fact]
    public async Task VoteDocuments_UploadNonPdfIs400()
    {
        var s = await fixture.Seed();
        var vote = await Draft(fixture, s, s.Chair);
        using var chair = fixture.Client(s.Chair);

        var (status, code, _) = await Read(await chair.PostAsync($"{Documents(s, vote)}/upload",
            DocumentFixture.Form(("ghi-chu.txt", Encoding.UTF8.GetBytes("ghi chú bỏ phiếu"), null))));

        Assert.Equal((400, "MSG164"), (status, code));
        await using var db = fixture.Db();
        Assert.False(await db.VoteDocuments.AnyAsync(l => l.VotingSessionId == vote));
        Assert.Equal(0, await db.MeetingDocuments.CountAsync(d => d.MeetingId == s.MeetingId));
    }

    [Fact]
    public async Task VoteDocuments_UploadElevenFilesIs400()
    {
        var s = await fixture.Seed();
        var vote = await Draft(fixture, s, s.Chair);
        using var chair = fixture.Client(s.Chair);

        var (status, code, _) = await Read(await chair.PostAsync($"{Documents(s, vote)}/upload",
            DocumentFixture.Form(Enumerable.Range(0, 11).Select(i => ($"{i}.pdf", SampleFiles.Pdf($"{i}"), (string?)null)).ToArray())));

        Assert.Equal((400, "MSG288"), (status, code));
        await using var db = fixture.Db();
        Assert.False(await db.VoteDocuments.AnyAsync(l => l.VotingSessionId == vote));
        Assert.Equal(0, await db.MeetingDocuments.CountAsync(d => d.MeetingId == s.MeetingId));
    }

    [Fact]
    public async Task VoteDocuments_UploadWhenStorageFullIs409()
    {
        var s = await fixture.Seed();
        await using var _ = await FillStorage(fixture, s.Chair, room: 10);
        var vote = await Draft(fixture, s, s.Chair);
        using var chair = fixture.Client(s.Chair);

        var (status, code, body) = await Read(await chair.PostAsync($"{Documents(s, vote)}/upload",
            DocumentFixture.Form(("Tải lên.pdf", SampleFiles.Pdf("upload"), null))));

        Assert.Equal((409, "MSG346"), (status, code));
        Assert.Equal("Bộ nhớ tài liệu của hệ thống đã đầy. Chưa có tài liệu nào được tải lên. Hãy báo quản trị viên.",
            body.GetProperty("message").GetString());
        await using var db = fixture.Db();
        Assert.False(await db.VoteDocuments.AnyAsync(l => l.VotingSessionId == vote));
        Assert.Equal(0, await db.MeetingDocuments.CountAsync(d => d.MeetingId == s.MeetingId));
    }

    [Fact]
    public async Task VoteDocuments_DetachNeverLinkedIs404()
    {
        var s = await fixture.Seed();
        var document = await InMeeting(fixture, s);
        var vote = await Draft(fixture, s, s.Chair);
        using var chair = fixture.Client(s.Chair);

        var (status, code, _) = await Read(await chair.DeleteAsync($"{Documents(s, vote)}/{document}"));

        Assert.Equal((404, "MSG269"), (status, code));
    }
}
