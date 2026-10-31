using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using TrustCabinet.Domain.Audit;
using TrustCabinet.Domain.Documents;
using TrustCabinet.Domain.Entities;
using TrustCabinet.Domain.Enums;
using TrustCabinet.Web.Tests.Documents;
using static TrustCabinet.Web.Tests.GuestAccess.GuestAccessFixture;

namespace TrustCabinet.Web.Tests.GuestAccess;

/// <summary>
/// SCRUM-123: a guest with a current UC-04 session reads the meeting's documents (UC-32a, UC-32c) — every
/// document, view and download — and nothing else of them; sessions come from the real UC-04 flow.
/// </summary>
public sealed class GuestDocumentAccessTests(GuestAccessFixture f) : IClassFixture<GuestAccessFixture>
{
    private static string Documents(Guid meetingId) => $"/api/meetings/{meetingId}/documents";
    private static string Content(Guid meetingId, Guid documentId) => $"{Documents(meetingId)}/{documentId}/content";

    private async Task<Guid> AddDocument(Scenario s, string name, Guid? item = null, bool removed = false)
    {
        await using var db = f.Db();
        var fileId = Guid.CreateVersion7();
        var bytes = SampleFiles.Pdf(name);
        var document = new MeetingDocument
        {
            MeetingId = s.MeetingId, FileName = name, AddedById = s.SecretaryId, AddedAt = f.Clock.Now,
            RemovedAt = removed ? f.Clock.Now : null, RemovedById = removed ? s.SecretaryId : null,
            StoredFile = new StoredFile { Id = fileId, StorageKey = DocumentFileRules.StorageKey(fileId), OriginalFileName = name,
                ContentType = DocumentFileRules.PdfContentType, ByteSize = bytes.Length, ContentSha256 = new string('a', 64),
                UploadedById = s.SecretaryId, UploadedAt = f.Clock.Now },
        };
        if (item is { } id) document.AgendaItemLinks.Add(new() { AgendaItemId = id });
        db.MeetingDocuments.Add(document);
        await db.SaveChangesAsync();
        f.Storage.Objects[document.StoredFile.StorageKey] = bytes;
        return document.Id;
    }

    private async Task SetMeeting(Scenario s, MeetingStatus? status = null, Sensitivity? sensitivity = null)
    {
        await using var db = f.Db();
        var meeting = await db.Meetings.SingleAsync(m => m.Id == s.MeetingId);
        meeting.Status = status ?? meeting.Status;
        meeting.Sensitivity = sensitivity ?? meeting.Sensitivity;
        await db.SaveChangesAsync();
    }

    [Fact]
    public async Task Guest_lists_views_and_downloads_every_document_of_the_invited_meeting()
    {
        var s = await f.Seed(MeetingStatus.InProgress);
        var linked = await AddDocument(s, "Tờ trình.pdf", s.ItemId);
        var general = await AddDocument(s, "Phụ lục.pdf");
        var removed = await AddDocument(s, "Đã gỡ.pdf", removed: true);
        await SetMeeting(s, sensitivity: Sensitivity.Confidential);
        var (guest, _) = await f.SignIn(s);

        var list = await Expect(await guest.GetAsync(Documents(s.MeetingId)), 200);
        var item = Assert.Single(list.GetProperty("agendaItems").EnumerateArray());
        Assert.Equal(linked, Assert.Single(item.GetProperty("documents").EnumerateArray()).GetProperty("id").GetGuid());
        Assert.Equal(general, Assert.Single(list.GetProperty("general").EnumerateArray()).GetProperty("id").GetGuid());

        using (var view = await guest.GetAsync(Content(s.MeetingId, linked)))
        {
            Assert.Equal(HttpStatusCode.OK, view.StatusCode);
            Assert.Equal(SampleFiles.Pdf("Tờ trình.pdf"), await view.Content.ReadAsByteArrayAsync());
            Assert.True(view.Headers.CacheControl!.NoStore);
        }
        Assert.Equal(HttpStatusCode.OK, (await guest.GetAsync(Content(s.MeetingId, general) + "?download=true")).StatusCode);
        await Expect(await guest.GetAsync(Content(s.MeetingId, removed)), 404, "MSG156");

        // BR-318: the guest has no user row, so the audit row names the invitation instead.
        await using (var db = f.Db())
        {
            var rows = await db.AuditLog.Where(a => a.ObjectId == linked || a.ObjectId == general)
                .OrderBy(a => a.Action).ToListAsync();
            Assert.Equal([AuditActions.DocumentDownloaded, AuditActions.DocumentViewed], rows.Select(r => r.Action));
            Assert.All(rows, r =>
            {
                Assert.Null(r.ActorId);
                Assert.Equal(s.InvitationId, JsonDocument.Parse(r.Details).RootElement.GetProperty("guestInvitationId").GetGuid());
            });
        }

        // BR-188 holds for a guest too.
        await SetMeeting(s, sensitivity: Sensitivity.Secret);
        await Expect(await guest.GetAsync(Content(s.MeetingId, general) + "?download=true"), 403, "MSG159");
        Assert.Equal(HttpStatusCode.OK, (await guest.GetAsync(Content(s.MeetingId, general))).StatusCode);
    }

    [Fact]
    public async Task Guest_cannot_reach_another_meetings_documents_or_change_any()
    {
        var s = await f.Seed(MeetingStatus.InProgress);
        var other = await f.Seed(MeetingStatus.InProgress);
        var mine = await AddDocument(s, "Của tôi.pdf");
        var foreign = await AddDocument(other, "Cuộc họp khác.pdf");
        var (guest, _) = await f.SignIn(s);

        await Expect(await guest.GetAsync(Documents(other.MeetingId)), 404, "MSG156");
        await Expect(await guest.GetAsync(Content(other.MeetingId, foreign)), 404, "MSG156");
        await Expect(await guest.GetAsync(Content(s.MeetingId, foreign)), 404, "MSG156");

        var agendaLinks = $"/api/meetings/{s.MeetingId}/agenda/{s.ItemId}/documents";
        foreach (var write in new Func<Task<HttpResponseMessage>>[]
                 {
                     () => guest.PostAsJsonAsync(Documents(s.MeetingId), new { documentIds = new[] { mine } }),
                     () => guest.PostAsync(Documents(s.MeetingId) + "/upload", new MultipartFormDataContent()),
                     () => guest.DeleteAsync($"{Documents(s.MeetingId)}/{mine}"),
                     () => guest.PostAsJsonAsync(agendaLinks, new { documentIds = new[] { mine } }),
                     () => guest.PostAsJsonAsync(agendaLinks + "/from-library", new { documentIds = new[] { mine } }),
                     () => guest.DeleteAsync($"{agendaLinks}/{mine}"),
                 })
            Assert.Equal(HttpStatusCode.Forbidden, (await write()).StatusCode);

        await using var db = f.Db();
        var row = await db.MeetingDocuments.Include(d => d.AgendaItemLinks).SingleAsync(d => d.Id == mine);
        Assert.Null(row.RemovedAt);
        Assert.Empty(row.AgendaItemLinks);
    }

    [Fact]
    public async Task Logout_revocation_expiry_meeting_end_and_a_missing_profile_each_close_the_documents()
    {
        async Task Closed(HttpClient guest, Scenario s, Guid document)
        {
            await Expect(await guest.GetAsync(Documents(s.MeetingId)), 404, "MSG156");
            await Expect(await guest.GetAsync(Content(s.MeetingId, document)), 404, "MSG156");
            await Expect(await guest.GetAsync(Content(s.MeetingId, document) + "?download=true"), 404, "MSG156");
        }

        var s = await f.Seed(MeetingStatus.InProgress);
        var document = await AddDocument(s, "Tờ trình.pdf");

        var (noProfile, _) = await f.SignIn(s, profile: false);
        await Closed(noProfile, s, document);

        // Past the 60 s resend interval of the same invitation.
        f.Clock.Now = f.Clock.Now.AddMinutes(1);
        var (loggedOut, _) = await f.SignIn(s);
        Assert.Equal(HttpStatusCode.OK, (await loggedOut.GetAsync(Documents(s.MeetingId))).StatusCode);
        await loggedOut.DeleteAsync("/api/guest-access/session");
        await Closed(loggedOut, s, document);

        f.Clock.Now = f.Clock.Now.AddMinutes(1);
        var (ended, _) = await f.SignIn(s);
        await SetMeeting(s, MeetingStatus.Ended);
        await Closed(ended, s, document);
        // Members keep reading an Ended meeting's documents.
        using (var secretary = f.UserClient(s.SecretaryId))
            Assert.Equal(HttpStatusCode.OK, (await secretary.GetAsync(Documents(s.MeetingId))).StatusCode);

        var revokedMeeting = await f.Seed();
        var revokedDocument = await AddDocument(revokedMeeting, "Tờ trình.pdf");
        var (revoked, _) = await f.SignIn(revokedMeeting);
        await using (var db = f.Db())
            await db.MeetingGuestInvitations.Where(g => g.Id == revokedMeeting.InvitationId).ExecuteUpdateAsync(set => set
                .SetProperty(g => g.RevokedAt, f.Clock.Now).SetProperty(g => g.RevokedById, revokedMeeting.SecretaryId));
        await Closed(revoked, revokedMeeting, revokedDocument);

        var expiringMeeting = await f.Seed();
        var expiringDocument = await AddDocument(expiringMeeting, "Tờ trình.pdf");
        var (expiring, _) = await f.SignIn(expiringMeeting);
        f.Clock.Now = f.Clock.Now.AddHours(12);
        await Closed(expiring, expiringMeeting, expiringDocument);
    }

    [Fact]
    public async Task A_failed_guest_session_read_is_an_error_never_a_grant()
    {
        var s = await f.Seed(MeetingStatus.InProgress);
        var document = await AddDocument(s, "Tờ trình.pdf");
        var (guest, _) = await f.SignIn(s);
        f.ReadFailure.Enabled = true;
        try
        {
            await Expect(await guest.GetAsync(Documents(s.MeetingId)), 500, "MSG157");
            await Expect(await guest.GetAsync(Content(s.MeetingId, document)), 503, "MSG158");
            await Expect(await guest.GetAsync(Content(s.MeetingId, document) + "?download=true"), 503, "MSG160");
        }
        finally
        {
            f.ReadFailure.Enabled = false;
        }
    }

    [Fact]
    public async Task Guest_opens_the_vote_document_confirms_reading_it_and_then_votes()
    {
        var s = await f.Seed(MeetingStatus.InProgress);
        Guid chairId;
        await using (var db = f.Db())
        {
            var chair = new User { FullName = "Chủ toạ", Email = $"{Guid.CreateVersion7():N}@internal.test", PasswordHash = "test-only" };
            db.Users.Add(chair);
            db.MeetingParticipants.Add(new MeetingParticipant { MeetingId = s.MeetingId, UserId = chair.Id, Role = MeetingRole.Chair });
            await db.SaveChangesAsync();
            chairId = chair.Id;
        }
        var document = await AddDocument(s, "Tờ trình.pdf");
        var votes = $"/api/meetings/{s.MeetingId}/votes";
        using var secretary = f.UserClient(s.SecretaryId);
        using var chairClient = f.UserClient(chairId);
        var vote = (await Expect(await secretary.PostAsJsonAsync(votes, new
        {
            title = "Thông qua tờ trình", description = "", options = new[] { "Đồng ý", "Không đồng ý" },
            isAnonymous = false, eligibleRoles = new[] { "Guest" },
        }), 201, "MSG210")).GetProperty("voteId").GetGuid();
        await Expect(await secretary.PostAsJsonAsync($"{votes}/{vote}/documents", new { meetingDocumentIds = new[] { document } }), 200, "MSG265");
        await Expect(await chairClient.PostAsync($"{votes}/{vote}/open", null), 200, "MSG217");

        var (guest, _) = await f.SignIn(s);
        var option = (await Expect(await guest.GetAsync($"{votes}/{vote}"), 200))
            .GetProperty("options").EnumerateArray().First().GetProperty("id").GetGuid();
        await Expect(await guest.PostAsJsonAsync($"{votes}/{vote}/ballots", new { optionId = option }), 409, "MSG252");

        var list = await Expect(await guest.GetAsync(Documents(s.MeetingId)), 200);
        Assert.Equal(document, Assert.Single(list.GetProperty("general").EnumerateArray()).GetProperty("id").GetGuid());
        Assert.Equal(HttpStatusCode.OK, (await guest.GetAsync(Content(s.MeetingId, document))).StatusCode);
        await Expect(await guest.PostAsJsonAsync($"{Documents(s.MeetingId)}/{document}/read-confirmation",
            new { lastPageReached = true }), 200, "MSG253");
        await Expect(await guest.PostAsJsonAsync($"{votes}/{vote}/ballots", new { optionId = option }), 200, "MSG227");
    }
}
