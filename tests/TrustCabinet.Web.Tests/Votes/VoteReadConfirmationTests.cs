using System.Net.Http.Json;
using System.Text;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using TrustCabinet.Domain.Documents;
using TrustCabinet.Domain.Entities;
using TrustCabinet.Domain.Enums;
using TrustCabinet.Web.Tests.Participants;
using static TrustCabinet.Web.Tests.Attendance.AttendanceEndpointTests;
using static TrustCabinet.Web.Tests.Participants.ParticipantEndpointTests;
using static TrustCabinet.Web.Tests.Votes.VoteEndpointTests;

namespace TrustCabinet.Web.Tests.Votes;

/// <summary>SCRUM-96: UC-74 and the reading requirement of UC-55 (BR-289–BR-293).</summary>
public sealed class VoteReadConfirmationTests(ParticipantFixture f) : IClassFixture<ParticipantFixture>
{
    /// <summary>A meeting document written straight to the database; nothing here reads its bytes.</summary>
    private async Task<Guid> Document(ParticipantFixture.Scenario s, string name = "Tờ trình.pdf")
    {
        await using var db = f.Db();
        var fileId = Guid.CreateVersion7();
        var document = new MeetingDocument
        {
            MeetingId = s.MeetingId, FileName = name, AddedById = s.Secretary, AddedAt = f.Clock.Now,
            StoredFile = new StoredFile
            {
                Id = fileId, StorageKey = DocumentFileRules.StorageKey(fileId), OriginalFileName = name,
                ContentType = DocumentFileRules.PdfContentType, ByteSize = 10, ContentSha256 = new string('c', 64), PageCount = 3,
                UploadedById = s.Secretary, UploadedAt = f.Clock.Now
            }
        };
        db.MeetingDocuments.Add(document);
        await db.SaveChangesAsync();
        return document.Id;
    }

    private static async Task Attach(HttpClient manager, ParticipantFixture.Scenario s, Guid vote, params Guid[] documents)
        => await Code(await manager.PostAsJsonAsync($"{s.Url}/votes/{vote}/documents", new { meetingDocumentIds = documents }),
            200, "MSG265");

    private static Task<HttpResponseMessage> Confirm(HttpClient client, ParticipantFixture.Scenario s, Guid document,
        bool? lastPageReached = true)
        => client.PostAsJsonAsync($"{s.Url}/documents/{document}/read-confirmation", new { lastPageReached });

    [Fact]
    public async Task A_voter_confirms_each_vote_document_before_the_ballot_is_accepted()
    {
        var s = await f.Seed(MeetingStatus.InProgress);
        using var chair = f.Client(s.Chair); using var secretary = f.Client(s.Secretary); using var member = f.Client(s.Member);
        var first = await Document(s, "Tờ trình.pdf"); var second = await Document(s, "Phụ lục.pdf");
        var vote = await Draft(secretary, s);
        await Attach(secretary, s, vote, first, second);
        await Code(await chair.PostAsync($"{s.Url}/votes/{vote}/open", null), 200, "MSG217");
        var option = (await Options(member, s, vote))[0];

        var refused = await Code(await Ballot(member, s, vote, option), 409, "MSG252");
        Assert.Equal([first, second], refused.GetProperty("documents").EnumerateArray().Select(d => d.GetProperty("id").GetGuid()));

        // The viewer says the last page was reached; nothing is recorded until it does (BR-290).
        await Code(await Confirm(member, s, first, false), 400, "MSG254");
        await Code(await Confirm(member, s, first, null), 400, "MSG254");
        await Code(await member.PostAsync($"{s.Url}/documents/{first}/read-confirmation",
            new StringContent("{}", Encoding.UTF8, "text/plain")), 415, "MSG34");
        await Code(await member.PostAsync($"{s.Url}/documents/{first}/read-confirmation",
            new StringContent(new string(' ', 65 * 1024) + "{}", Encoding.UTF8, "application/json")), 413, "MSG87");
        var confirmed = await Code(await Confirm(member, s, first), 200, "MSG253");
        Assert.False(confirmed.GetProperty("alreadyConfirmed").GetBoolean());
        Assert.Equal(f.Clock.Now, confirmed.GetProperty("confirmedAt").GetDateTimeOffset());

        // Confirmed once and never withdrawn: a second tick reports the first (UC-74 AF-1, BR-291).
        f.Clock.Now = f.Clock.Now.AddMinutes(5);
        var again = await Code(await Confirm(member, s, first, false), 200, "MSG253");
        Assert.True(again.GetProperty("alreadyConfirmed").GetBoolean());
        Assert.Equal(confirmed.GetProperty("confirmedAt").GetDateTimeOffset(), again.GetProperty("confirmedAt").GetDateTimeOffset());
        var documents = (await Detail(member, s, vote)).GetProperty("documents").EnumerateArray().ToArray();
        Assert.Equal(JsonValueKind.String, documents[0].GetProperty("readConfirmedAt").ValueKind);
        Assert.Equal(JsonValueKind.Null, documents[1].GetProperty("readConfirmedAt").ValueKind);

        refused = await Code(await Ballot(member, s, vote, option), 409, "MSG252");
        Assert.Equal(second, Assert.Single(refused.GetProperty("documents").EnumerateArray()).GetProperty("id").GetGuid());

        // A vote document removed from the meeting while the vote is open no longer counts (BR-292).
        await using (var db = f.Db())
            await db.MeetingDocuments.Where(d => d.Id == second).ExecuteUpdateAsync(u => u.SetProperty(d => d.RemovedAt, f.Clock.Now));
        Assert.Equal(1, (await Detail(member, s, vote)).GetProperty("documentCount").GetInt32());
        await Code(await Ballot(member, s, vote, option), 200, "MSG227");
    }

    [Fact]
    public async Task Only_a_document_of_a_vote_the_reader_may_see_can_be_confirmed()
    {
        var s = await f.Seed(MeetingStatus.InProgress);
        using var secretary = f.Client(s.Secretary); using var member = f.Client(s.Member); using var outsider = f.Client(s.Outsider);
        var loose = await Document(s, "Không gắn biểu quyết.pdf");
        var draftOnly = await Document(s, "Gắn nháp.pdf");
        var vote = await Draft(secretary, s);
        await Attach(secretary, s, vote, draftOnly);

        await Code(await Confirm(member, s, loose), 404, "MSG156");
        // A draft is the Chair's and the Secretary's alone, so its document is not a vote document to anyone else (BR-253).
        await Code(await Confirm(member, s, draftOnly), 404, "MSG156");
        await Code(await Confirm(outsider, s, draftOnly), 404, "MSG156");
        await Code(await Confirm(secretary, s, Guid.CreateVersion7()), 404, "MSG156");
        // The Secretary sees the draft, but nobody votes in it yet: no confirmation waits for a ballot.
        await Code(await Confirm(secretary, s, draftOnly), 409, "MSG224");
        await using var db = f.Db();
        Assert.False(await db.DocumentReadConfirmations.AnyAsync(c => c.MeetingDocumentId == draftOnly));
    }

    [Fact]
    public async Task Only_a_voter_of_an_open_vote_confirms_and_the_confirmation_outlives_a_detach()
    {
        var s = await f.Seed(MeetingStatus.InProgress);
        using var chair = f.Client(s.Chair); using var secretary = f.Client(s.Secretary); using var member = f.Client(s.Member);
        var shared = await Document(s, "Dùng chung.pdf");
        var chairOnly = await Draft(secretary, s, Body(roles: ["Chair"]));
        await Attach(secretary, s, chairOnly, shared);
        var draft = await Draft(secretary, s);
        await Attach(secretary, s, draft, shared);
        await Code(await chair.PostAsync($"{s.Url}/votes/{chairOnly}/open", null), 200, "MSG217");

        // The Member sees the Open vote but is not on its fixed list (UC-55's MSG224).
        await Code(await Confirm(member, s, shared), 409, "MSG224");
        var confirmed = await Confirm(chair, s, shared);
        await Code(confirmed, 200, "MSG253");
        // A confirmation must never be served from a cache.
        Assert.True(confirmed.Headers.CacheControl?.NoStore);

        // Detaching from the draft keeps the confirmation (BR-305).
        await Code(await secretary.DeleteAsync($"{s.Url}/votes/{draft}/documents/{shared}"), 200, "MSG268");
        await using (var db = f.Db())
            Assert.True(await db.DocumentReadConfirmations.AnyAsync(c => c.MeetingDocumentId == shared && c.UserId == s.Chair));

        // Once the vote stops taking ballots there is nothing left to confirm for (UC-55's MSG225).
        var later = await Document(s, "Sau khi đóng.pdf");
        var closing = await Draft(secretary, s, Body(roles: ["Chair"]));
        await Attach(secretary, s, closing, later);
        await Code(await chair.PostAsync($"{s.Url}/votes/{chairOnly}/close", null), 200, "MSG229");
        await Code(await chair.PostAsync($"{s.Url}/votes/{closing}/open", null), 200, "MSG217");
        await Code(await chair.PostAsync($"{s.Url}/votes/{closing}/close", null), 200, "MSG229");
        await Code(await Confirm(chair, s, later), 409, "MSG225");
    }

    [Fact]
    public async Task A_guest_confirms_and_casts_a_secret_ballot_and_the_confirmation_says_nothing_about_the_choice()
    {
        var s = await f.Seed(MeetingStatus.InProgress); var invitation = await Guest(f, s);
        using var chair = f.Client(s.Chair); using var secretary = f.Client(s.Secretary);
        using var guest = f.Guest(s.MeetingId, invitation.Id);
        var document = await Document(s);
        var vote = await Draft(secretary, s, Body(anonymous: true, roles: ["Guest"]));
        await Attach(secretary, s, vote, document);
        await Code(await chair.PostAsync($"{s.Url}/votes/{vote}/open", null), 200, "MSG217");
        var option = (await Options(guest, s, vote))[0];

        await Code(await Ballot(guest, s, vote, option), 409, "MSG252");
        await Code(await Confirm(guest, s, document), 200, "MSG253");
        await Code(await Ballot(guest, s, vote, option), 200, "MSG227");

        await using var db = f.Db();
        var confirmation = await db.DocumentReadConfirmations.SingleAsync(c => c.MeetingDocumentId == document);
        Assert.Equal(invitation.Id, confirmation.GuestInvitationId);
        Assert.Null(confirmation.UserId);
        // Stored apart from ballots: nothing on the row points at a vote, an option or a choice (BR-293).
        var columns = db.Model.FindEntityType(typeof(DocumentReadConfirmation))!.GetProperties().Select(p => p.Name).Order();
        Assert.Equal(["ConfirmedAt", "CreatedAt", "GuestInvitationId", "Id", "MeetingDocumentId", "UpdatedAt", "UserId"], columns);
        Assert.False(await db.VoteChoices.AnyAsync(c => c.VotingSessionId == vote && c.GuestInvitationId != null));
    }
}
