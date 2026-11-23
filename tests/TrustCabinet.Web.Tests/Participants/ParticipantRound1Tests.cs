using System.IdentityModel.Tokens.Jwt;
using System.Net;
using System.Net.Http.Json;
using System.Security.Claims;
using System.Text;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;
using TrustCabinet.Domain.Entities;
using TrustCabinet.Domain.Enums;
using static TrustCabinet.Web.Tests.Attendance.AttendanceEndpointTests;
using static TrustCabinet.Web.Tests.Participants.ParticipantEndpointTests;

namespace TrustCabinet.Web.Tests.Participants;

/// <summary>Round 1 API tests for FE-06 Participants.</summary>
public sealed class ParticipantRound1Tests(ParticipantFixture f) : IClassFixture<ParticipantFixture>
{
    private async Task<HttpClient> CreatorClientAsync(Guid userId)
    {
        await using var db = f.Db();
        var role = new Role { Name = "Creator-" + Guid.NewGuid() };
        db.Roles.Add(role);
        db.RoleCapabilities.Add(new RoleCapability { RoleId = role.Id, Capability = Capability.MeetingCreator });
        await db.SaveChangesAsync();
        await db.Users.Where(u => u.Id == userId).ExecuteUpdateAsync(set => set.SetProperty(u => u.RoleId, role.Id));
        var jwt = new JwtSecurityToken("TrustCabinet", "TrustCabinet",
            [new Claim("sub", userId.ToString()), new Claim("cap", "MeetingCreator")],
            expires: DateTime.UtcNow.AddMinutes(5),
            signingCredentials: new SigningCredentials(
                new SymmetricSecurityKey(Encoding.UTF8.GetBytes("khoa-ky-chi-dung-trong-test-du-32-ky-tu")),
                SecurityAlgorithms.HmacSha256));
        var client = f.Host.CreateClient();
        client.DefaultRequestHeaders.Authorization = new("Bearer", new JwtSecurityTokenHandler().WriteToken(jwt));
        return client;
    }

    private static async Task<MeetingGuestInvitation> NewGuest(ParticipantFixture f, ParticipantFixture.Scenario s, string name, string email)
    {
        await using var db = f.Db();
        var guest = new MeetingGuestInvitation
        {
            MeetingId = s.MeetingId, InvitedById = s.Secretary,
            InvitedName = name, Email = email, SensitivityAtInvitation = Sensitivity.Internal,
        };
        db.MeetingGuestInvitations.Add(guest);
        await db.SaveChangesAsync();
        return guest;
    }

    [Fact]
    public async Task ParticipantOptions_NeverListsDeactivatedAccounts()
    {
        var s = await f.Seed();
        const string marker = "NguyenVanDeactivated";
        await using (var db = f.Db())
        {
            await db.Users.Where(u => u.Id == s.Outsider).ExecuteUpdateAsync(set => set.SetProperty(u => u.FullName, marker));
            (await db.Users.FindAsync(s.Outsider))!.IsActive = false;
            await db.SaveChangesAsync();
        }
        using var client = await CreatorClientAsync(s.Organizer);
        var page = await client.GetFromJsonAsync<JsonElement>($"/api/meetings/participant-options?q={marker}");
        Assert.Equal(0, page.GetProperty("total").GetInt32());
        Assert.Empty(page.GetProperty("items").EnumerateArray());
    }

    [Fact]
    public async Task ParticipantOptions_SearchIgnoresCaseAndTreatsWildcardsLiterally()
    {
        var s = await f.Seed();
        await using (var db = f.Db())
        {
            await db.Users.Where(u => u.Id == s.Member).ExecuteUpdateAsync(set =>
                set.SetProperty(u => u.FullName, "Nguyễn Văn Tìm").SetProperty(u => u.Email, "nguyen.tim@example.test"));
            await db.SaveChangesAsync();
        }
        using var client = await CreatorClientAsync(s.Organizer);
        var byName = await client.GetFromJsonAsync<JsonElement>("/api/meetings/participant-options?q=NGUYEN");
        Assert.Contains(byName.GetProperty("items").EnumerateArray(), i => i.GetProperty("id").GetGuid() == s.Member);
        var byEmail = await client.GetFromJsonAsync<JsonElement>("/api/meetings/participant-options?q=TIM%40EXAMPLE");
        Assert.Contains(byEmail.GetProperty("items").EnumerateArray(), i => i.GetProperty("id").GetGuid() == s.Member);
        var percent = await client.GetFromJsonAsync<JsonElement>("/api/meetings/participant-options?q=%25");
        Assert.Equal(0, percent.GetProperty("total").GetInt32());
        Assert.Empty(percent.GetProperty("items").EnumerateArray());
        var underscore = await client.GetFromJsonAsync<JsonElement>("/api/meetings/participant-options?q=_");
        Assert.Equal(0, underscore.GetProperty("total").GetInt32());
        Assert.Empty(underscore.GetProperty("items").EnumerateArray());
    }

    [Fact]
    public async Task ParticipantOptions_EmptyPagePastTheEnd()
    {
        var s = await f.Seed();
        using var client = await CreatorClientAsync(s.Organizer);
        var page = await client.GetFromJsonAsync<JsonElement>("/api/meetings/participant-options?page=999&pageSize=20");
        Assert.Equal(HttpStatusCode.OK, HttpStatusCode.OK);
        Assert.Empty(page.GetProperty("items").EnumerateArray());
        Assert.True(page.GetProperty("total").GetInt32() > 0);
        Assert.Equal(999, page.GetProperty("page").GetInt32());
    }

    [Fact]
    public async Task ParticipantOptions_AcceptsPageSize100And200CharQuery()
    {
        var s = await f.Seed();
        using var client = await CreatorClientAsync(s.Organizer);
        var wide = await client.GetFromJsonAsync<JsonElement>("/api/meetings/participant-options?pageSize=100");
        Assert.Equal(100, wide.GetProperty("pageSize").GetInt32());
        var longQuery = await client.GetAsync("/api/meetings/participant-options?q=" + new string('a', 200));
        Assert.Equal(HttpStatusCode.OK, longQuery.StatusCode);
        var body = JsonDocument.Parse(await longQuery.Content.ReadAsStringAsync()).RootElement;
        Assert.Empty(body.GetProperty("items").EnumerateArray());
    }

    [Fact]
    public async Task ParticipantOptions_RejectsOversizedPaging()
    {
        var s = await f.Seed();
        using var client = await CreatorClientAsync(s.Organizer);
        await Code(await client.GetAsync("/api/meetings/participant-options?pageSize=101"), 400, "MSG276");
        await Code(await client.GetAsync("/api/meetings/participant-options?page=0"), 400, "MSG276");
        await Code(await client.GetAsync("/api/meetings/participant-options?q=" + new string('a', 201)), 400, "MSG276");
    }

    [Fact]
    public async Task ParticipantOptions_RejectsUnknownOrRepeatedKeys()
    {
        var s = await f.Seed();
        using var client = await CreatorClientAsync(s.Organizer);
        await Code(await client.GetAsync("/api/meetings/participant-options?sort=name"), 400, "MSG276");
        await Code(await client.GetAsync("/api/meetings/participant-options?page=1&page=2"), 400, "MSG276");
    }

    [Fact]
    public async Task ParticipantOptions_AnonymousIs401()
    {
        await f.Seed();
        using var anonymous = f.Host.CreateClient();
        Assert.Equal(HttpStatusCode.Unauthorized, (await anonymous.GetAsync("/api/meetings/participant-options")).StatusCode);
    }

    [Fact]
    public async Task ParticipantOptions_MemberWithoutCreatorIs403()
    {
        var s = await f.Seed();
        using var member = f.Client(s.Member);
        Assert.Equal(HttpStatusCode.Forbidden, (await member.GetAsync("/api/meetings/participant-options")).StatusCode);
    }

    [Fact]
    public async Task ParticipantContext_UnknownMeetingIs404()
    {
        var s = await f.Seed();
        using var client = f.Client(s.Secretary);
        var response = await client.GetAsync("/api/meetings/00000000-0000-7000-8000-000000000001/participant-context");
        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
        Assert.Equal(string.Empty, await response.Content.ReadAsStringAsync());
    }

    [Fact]
    public async Task ParticipantContext_AnonymousIs401()
    {
        var s = await f.Seed();
        using var anonymous = f.Host.CreateClient();
        Assert.Equal(HttpStatusCode.Unauthorized, (await anonymous.GetAsync(s.Url + "/participant-context")).StatusCode);
    }

    [Fact]
    public async Task ParticipantContext_RejectsQueryString()
    {
        var s = await f.Seed();
        using var client = f.Client(s.Secretary);
        await Code(await client.GetAsync(s.Url + "/participant-context?page=1"), 400, "MSG276");
    }

    [Fact]
    public async Task Participants_SearchesNameEmailAndInvitedName()
    {
        var s = await f.Seed();
        await NewGuest(f, s, "Người Khách Tìm", "khach-tim@example.test");
        await using (var db = f.Db())
        {
            await db.Users.Where(u => u.Id == s.Member).ExecuteUpdateAsync(set => set.SetProperty(u => u.FullName, "Trần Thành Viên"));
        }
        using var client = f.Client(s.Secretary);
        var byEmail = await client.GetFromJsonAsync<JsonElement>(s.Url + "/participants?q=KHACH-TIM");
        var guestRow = Assert.Single(byEmail.GetProperty("items").EnumerateArray());
        Assert.Equal("Người Khách Tìm", guestRow.GetProperty("invitedName").GetString());
        var byName = await client.GetFromJsonAsync<JsonElement>(s.Url + "/participants?q=" + Uri.EscapeDataString("thành"));
        Assert.Contains(byName.GetProperty("items").EnumerateArray(), i => i.GetProperty("displayName").GetString() == "Trần Thành Viên");
    }

    [Fact]
    public async Task Participants_AcceptsPageSize100ButNot101()
    {
        var s = await f.Seed();
        using var client = f.Client(s.Secretary);
        var wide = await client.GetFromJsonAsync<JsonElement>(s.Url + "/participants?pageSize=100");
        Assert.Equal(100, wide.GetProperty("pageSize").GetInt32());
        await Code(await client.GetAsync(s.Url + "/participants?pageSize=101"), 400, "MSG276");
    }

    [Fact]
    public async Task Participants_RejectsBadPagingKeys()
    {
        var s = await f.Seed();
        using var client = f.Client(s.Secretary);
        await Code(await client.GetAsync(s.Url + "/participants?page=0"), 400, "MSG276");
        await Code(await client.GetAsync(s.Url + "/participants?q=" + new string('a', 201)), 400, "MSG276");
        await Code(await client.GetAsync(s.Url + "/participants?role=Member"), 400, "MSG276");
    }

    [Fact]
    public async Task Participants_AnonymousIs401()
    {
        var s = await f.Seed();
        using var anonymous = f.Host.CreateClient();
        Assert.Equal(HttpStatusCode.Unauthorized, (await anonymous.GetAsync(s.Url + "/participants")).StatusCode);
    }

    [Fact]
    public async Task EditorOptions_SearchesNameAndEmailWithPaging()
    {
        var s = await f.Seed();
        const string marker = "Biên Tập Tìm";
        await using (var db = f.Db())
        {
            await db.Users.Where(u => u.Id == s.Organizer || u.Id == s.Outsider)
                .ExecuteUpdateAsync(set => set.SetProperty(u => u.FullName, marker));
        }
        using var client = f.Client(s.Chair);
        var first = await client.GetFromJsonAsync<JsonElement>(
            s.Url + "/participants/editor-options?q=" + Uri.EscapeDataString("biên tập") + "&pageSize=1");
        Assert.Equal(2, first.GetProperty("total").GetInt32());
        var firstId = Assert.Single(first.GetProperty("items").EnumerateArray()).GetProperty("id").GetGuid();
        var second = await client.GetFromJsonAsync<JsonElement>(
            s.Url + "/participants/editor-options?q=" + Uri.EscapeDataString("biên tập") + "&pageSize=1&page=2");
        Assert.NotEqual(firstId, Assert.Single(second.GetProperty("items").EnumerateArray()).GetProperty("id").GetGuid());
    }

    [Fact]
    public async Task EditorOptions_RejectsOversizedPageAndUnknownKeys()
    {
        var s = await f.Seed(MeetingStatus.Draft);
        using var client = f.Client(s.Secretary);
        await Code(await client.GetAsync(s.Url + "/participants/editor-options?pageSize=101"), 400, "MSG276");
        await Code(await client.GetAsync(s.Url + "/participants/editor-options?foo=1"), 400, "MSG276");
    }

    [Fact]
    public async Task EditorOptions_StrangerIs404()
    {
        var s = await f.Seed(MeetingStatus.Draft);
        using var client = f.Client(s.Outsider);
        var response = await client.GetAsync(s.Url + "/participants/editor-options");
        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
        Assert.Equal(string.Empty, await response.Content.ReadAsStringAsync());
    }

    [Fact]
    public async Task EditorOptions_AnonymousIs401()
    {
        var s = await f.Seed(MeetingStatus.Draft);
        using var anonymous = f.Host.CreateClient();
        Assert.Equal(HttpStatusCode.Unauthorized, (await anonymous.GetAsync(s.Url + "/participants/editor-options")).StatusCode);
    }

    [Fact]
    public async Task Participants_RefusesSecondChair()
    {
        var s = await f.Seed();
        using var client = f.Client(s.Secretary);
        await Code(await Write(client, s.Url + "/participants", HttpMethod.Post,
            new { userIds = new[] { s.Outsider }, role = "Chair" }, await Tag(client, s.Url)), 409, "MSG109");
        await using var db = f.Db();
        Assert.Equal(1, await db.MeetingParticipants.CountAsync(p => p.MeetingId == s.MeetingId && p.Role == MeetingRole.Chair));
    }

    [Fact]
    public async Task Participants_RejectsQueryString()
    {
        var s = await f.Seed();
        using var client = f.Client(s.Secretary);
        await Code(await Write(client, s.Url + "/participants?dryRun=true", HttpMethod.Post,
            new { userIds = new[] { s.Outsider }, role = "Member" }, await Tag(client, s.Url)), 400, "MSG276");
        await using var db = f.Db();
        Assert.False(await db.MeetingParticipants.AnyAsync(p => p.MeetingId == s.MeetingId && p.UserId == s.Outsider));
    }

    [Fact]
    public async Task Participants_StrangerIs404()
    {
        var s = await f.Seed(MeetingStatus.Draft);
        using var client = f.Client(s.Outsider);
        var response = await Write(client, s.Url + "/participants", HttpMethod.Post,
            new { userIds = new[] { s.Member }, role = "Member" }, "\"participants-0\"");
        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
        Assert.Equal(string.Empty, await response.Content.ReadAsStringAsync());
    }

    [Fact]
    public async Task Participants_CreateAnonymousIs401()
    {
        var s = await f.Seed(MeetingStatus.Draft);
        using var anonymous = f.Host.CreateClient();
        var response = await Write(anonymous, s.Url + "/participants", HttpMethod.Post,
            new { userIds = new[] { Guid.NewGuid() }, role = "Member" }, "\"participants-0\"");
        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task Assignments_SameRoleIsNoOp()
    {
        var s = await f.Seed();
        using var client = f.Client(s.Secretary);
        var before = await Tag(client, s.Url);
        await using (var db = f.Db())
        {
            var audits = await db.AuditLog.CountAsync(a => a.ObjectId == s.MeetingId);
            await Code(await Write(client, s.Url + "/participants/assignments", HttpMethod.Put,
                new { changes = new[] { new { participantId = s.MemberAssignment, role = "Member" } } }, before), 200, "MSG154");
            Assert.Equal(before, await Tag(client, s.Url));
            Assert.Equal(audits, await db.AuditLog.CountAsync(a => a.ObjectId == s.MeetingId));
        }
    }

    [Fact]
    public async Task Assignments_DraftMeetingMayHaveNoSecretary()
    {
        var s = await f.Seed(MeetingStatus.Draft);
        using var client = f.Client(s.Chair);
        await Code(await Write(client, s.Url + "/participants/assignments", HttpMethod.Put,
            new { changes = new[] { new { participantId = s.SecretaryAssignment, role = "Member" } } },
            await Tag(client, s.Url)), 200, "MSG100");
        var roster = await client.GetFromJsonAsync<JsonElement>(s.Url + "/participants");
        Assert.DoesNotContain(roster.GetProperty("items").EnumerateArray(), i => i.GetProperty("role").GetString() == "Secretary");
    }

    [Fact]
    public async Task Assignments_RejectsEmptyDuplicateUnknownAndOversizedChanges()
    {
        var s = await f.Seed();
        using var client = f.Client(s.Secretary);
        var tag = await Tag(client, s.Url);
        await Code(await Write(client, s.Url + "/participants/assignments", HttpMethod.Put,
            new { changes = Array.Empty<object>() }, tag), 400, "MSG101");
        await Code(await Write(client, s.Url + "/participants/assignments", HttpMethod.Put,
            new
            {
                changes = new[]
                {
                    new { participantId = s.MemberAssignment, role = "Secretary" },
                    new { participantId = s.MemberAssignment, role = "Secretary" },
                },
            }, tag), 400, "MSG101");
        await Code(await Write(client, s.Url + "/participants/assignments", HttpMethod.Put,
            new { changes = new[] { new { participantId = s.MemberAssignment, role = "Guest" } } }, tag), 400, "MSG101");
        await Code(await Write(client, s.Url + "/participants/assignments", HttpMethod.Put,
            new { changes = Enumerable.Range(0, 101).Select(_ => new { participantId = s.MemberAssignment, role = "Member" }).ToArray() },
            tag), 400, "MSG101");
        await using var db = f.Db();
        Assert.Equal(MeetingRole.Member, (await db.MeetingParticipants.FindAsync(s.MemberAssignment))!.Role);
    }

    [Fact]
    public async Task Assignments_UnknownParticipantIs404()
    {
        var s = await f.Seed();
        using var client = f.Client(s.Secretary);
        var response = await Write(client, s.Url + "/participants/assignments", HttpMethod.Put,
            new { changes = new[] { new { participantId = Guid.Parse("00000000-0000-7000-8000-000000000001"), role = "Secretary" } } },
            await Tag(client, s.Url));
        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
        Assert.Equal(string.Empty, await response.Content.ReadAsStringAsync());
    }

    [Fact]
    public async Task Assignments_StrangerIs404AndAnonymousIs401()
    {
        var s = await f.Seed(MeetingStatus.Draft);
        using var stranger = f.Client(s.Outsider);
        var denied = await Write(stranger, s.Url + "/participants/assignments", HttpMethod.Put,
            new { changes = new[] { new { participantId = s.MemberAssignment, role = "Secretary" } } }, "\"participants-0\"");
        Assert.Equal(HttpStatusCode.NotFound, denied.StatusCode);
        Assert.Equal(string.Empty, await denied.Content.ReadAsStringAsync());
        using var anonymous = f.Host.CreateClient();
        var noToken = await Write(anonymous, s.Url + "/participants/assignments", HttpMethod.Put,
            new { changes = new[] { new { participantId = s.MemberAssignment, role = "Secretary" } } }, "\"participants-1\"");
        Assert.Equal(HttpStatusCode.Unauthorized, noToken.StatusCode);
    }

    [Fact]
    public async Task Assignments_PublishedChangeNotifiesNewRoleHolderAndAudits()
    {
        var s = await f.Seed();
        using var client = f.Client(s.Secretary);
        int audits;
        await using (var db = f.Db()) { audits = await db.AuditLog.CountAsync(a => a.ObjectId == s.MeetingId); }
        await Code(await Write(client, s.Url + "/participants/assignments", HttpMethod.Put,
            new { changes = new[] { new { participantId = s.MemberAssignment, role = "Secretary" } } },
            await Tag(client, s.Url)), 200, "MSG100");
        await using (var db = f.Db())
        {
            var rows = await db.AuditLog.Where(a => a.ObjectId == s.MeetingId).ToArrayAsync();
            Assert.Equal(audits + 1, rows.Length);
            Assert.Contains(rows, r => r.ActorId == s.Secretary);
        }
        using var promoted = f.Client(s.Member);
        var inbox = await promoted.GetFromJsonAsync<JsonElement>("/api/notifications");
        var note = Assert.Single(inbox.GetProperty("items").EnumerateArray());
        Assert.Equal("Vai trò cuộc họp đã thay đổi", note.GetProperty("title").GetString());
        Assert.Contains("bạn đã được giao làm Thư ký", note.GetProperty("body").GetString());
    }

    [Fact]
    public async Task Remove_ClearsAgendaPresenterAndSpeakingQueue()
    {
        var s = await f.Seed(MeetingStatus.InProgress);
        Guid itemId;
        await using (var db = f.Db())
        {
            var item = new AgendaItem { MeetingId = s.MeetingId, Position = 1, Title = "Nội dung 1", PresenterId = s.Member };
            db.AgendaItems.Add(item);
            db.SpeakingRequests.Add(new SpeakingRequest { MeetingId = s.MeetingId, UserId = s.Member, QueuePosition = 0 });
            await db.SaveChangesAsync();
            itemId = item.Id;
        }
        using var client = f.Client(s.Secretary);
        await Code(await Write(client, $"{s.Url}/participants/{s.MemberAssignment}", HttpMethod.Delete, null, await Tag(client, s.Url)), 200, "MSG103");
        await using (var db = f.Db())
        {
            Assert.Null((await db.AgendaItems.FindAsync(itemId))!.PresenterId);
            var speech = await db.SpeakingRequests.SingleAsync(r => r.MeetingId == s.MeetingId && r.UserId == s.Member);
            Assert.NotEqual(SpeakingRequestStatus.Queued, speech.Status);
        }
    }

    [Fact]
    public async Task Remove_UnknownAssignmentIs404()
    {
        var s = await f.Seed();
        using var client = f.Client(s.Secretary);
        var response = await Write(client, $"{s.Url}/participants/00000000-0000-7000-8000-000000000001",
            HttpMethod.Delete, null, await Tag(client, s.Url));
        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
        Assert.Equal(string.Empty, await response.Content.ReadAsStringAsync());
    }

    [Fact]
    public async Task Remove_RejectsRequestBody()
    {
        var s = await f.Seed();
        using var client = f.Client(s.Secretary);
        await Code(await Write(client, $"{s.Url}/participants/{s.MemberAssignment}", HttpMethod.Delete,
            new { reason = "x" }, await Tag(client, s.Url)), 400, "MSG97");
        await using var db = f.Db();
        Assert.NotNull(await db.MeetingParticipants.FindAsync(s.MemberAssignment));
    }

    [Fact]
    public async Task Remove_StrangerIs404AndAnonymousIs401()
    {
        var s = await f.Seed(MeetingStatus.Draft);
        using var stranger = f.Client(s.Outsider);
        var denied = await Write(stranger, $"{s.Url}/participants/{s.MemberAssignment}", HttpMethod.Delete, null, "\"participants-0\"");
        Assert.Equal(HttpStatusCode.NotFound, denied.StatusCode);
        Assert.Equal(string.Empty, await denied.Content.ReadAsStringAsync());
        using var anonymous = f.Host.CreateClient();
        var noToken = await Write(anonymous, $"{s.Url}/participants/{s.MemberAssignment}", HttpMethod.Delete, null, "\"participants-0\"");
        Assert.Equal(HttpStatusCode.Unauthorized, noToken.StatusCode);
    }

    [Fact]
    public async Task Remove_PublishedRemovalNotifiesPersonAndAudits()
    {
        var s = await f.Seed();
        using var client = f.Client(s.Secretary);
        int audits;
        await using (var db = f.Db()) { audits = await db.AuditLog.CountAsync(a => a.ObjectId == s.MeetingId); }
        await Code(await Write(client, $"{s.Url}/participants/{s.MemberAssignment}", HttpMethod.Delete, null, await Tag(client, s.Url)), 200, "MSG103");
        await using (var db = f.Db())
        {
            var rows = await db.AuditLog.Where(a => a.ObjectId == s.MeetingId).ToArrayAsync();
            Assert.Equal(audits + 1, rows.Length);
            Assert.Contains(rows, r => r.ActorId == s.Secretary);
        }
        using var removed = f.Client(s.Member);
        var inbox = await removed.GetFromJsonAsync<JsonElement>("/api/notifications");
        Assert.NotEmpty(inbox.GetProperty("items").EnumerateArray());
    }

    [Fact]
    public async Task GuestInvite_TrimsNameAndAcceptsBoundaryLengths()
    {
        var s = await f.Seed();
        using var client = f.Client(s.Secretary);
        var name200 = new string('B', 200);
        var email256 = new string('c', 243) + "@example.test";
        Assert.Equal(256, email256.Length);
        var saved = await Code(await Write(client, s.Url + "/guest-invitations", HttpMethod.Post,
            new { invitedName = "  " + name200 + "  ", email = email256 }, await Tag(client, s.Url)), 201, "MSG96");
        var invitationId = Assert.Single(saved.GetProperty("createdIds").EnumerateArray()).GetGuid();
        await using var db = f.Db();
        Assert.Equal(name200, (await db.MeetingGuestInvitations.FindAsync(invitationId))!.InvitedName);
    }

    [Fact]
    public async Task GuestInvite_RejectsEmailOfCurrentInternalParticipant()
    {
        var s = await f.Seed();
        await using (var db = f.Db())
        {
            var email = await db.Users.Where(u => u.Id == s.Member).Select(u => u.Email).SingleAsync();
            using var client = f.Client(s.Secretary);
            var result = await Code(await Write(client, s.Url + "/guest-invitations", HttpMethod.Post,
                new { invitedName = "Thành viên", email }, await Tag(client, s.Url)), 400, "MSG97");
            Assert.NotEmpty(result.GetProperty("errors").GetProperty("email").EnumerateArray());
        }
    }

    [Fact]
    public async Task GuestInvite_StrangerIs404()
    {
        var s = await f.Seed(MeetingStatus.Draft);
        using var client = f.Client(s.Outsider);
        var response = await Write(client, s.Url + "/guest-invitations", HttpMethod.Post,
            new { invitedName = "Khách", email = "khach@example.test" }, "\"participants-0\"");
        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
        Assert.Equal(string.Empty, await response.Content.ReadAsStringAsync());
    }

    [Fact]
    public async Task GuestInvite_AnonymousIs401()
    {
        var s = await f.Seed(MeetingStatus.Draft);
        using var anonymous = f.Host.CreateClient();
        var response = await Write(anonymous, s.Url + "/guest-invitations", HttpMethod.Post,
            new { invitedName = "Khách", email = "khach@example.test" }, "\"participants-0\"");
        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task Revoke_ClearsGuestAsAgendaPresenter()
    {
        var s = await f.Seed(MeetingStatus.Draft);
        var guest = await NewGuest(f, s, "Khách Trình Bày", "khach-trinh-bay@example.test");
        Guid itemId;
        await using (var db = f.Db())
        {
            var item = new AgendaItem { MeetingId = s.MeetingId, Position = 1, Title = "Nội dung 1", PresenterGuestInvitationId = guest.Id };
            db.AgendaItems.Add(item);
            await db.SaveChangesAsync();
            itemId = item.Id;
        }
        using var client = f.Client(s.Secretary);
        await Code(await Write(client, $"{s.Url}/guest-invitations/{guest.Id}", HttpMethod.Delete, null, await Tag(client, s.Url)), 200, "MSG103");
        await using (var db = f.Db())
        {
            Assert.Null((await db.AgendaItems.FindAsync(itemId))!.PresenterGuestInvitationId);
        }
    }

    [Fact]
    public async Task Revoke_UnknownOrRevokedInvitationIs404()
    {
        var s = await f.Seed();
        var guest = await NewGuest(f, s, "Khách", "khach@example.test");
        using var client = f.Client(s.Secretary);
        var unknown = await Write(client, $"{s.Url}/guest-invitations/00000000-0000-7000-8000-000000000001",
            HttpMethod.Delete, null, await Tag(client, s.Url));
        Assert.Equal(HttpStatusCode.NotFound, unknown.StatusCode);
        await Code(await Write(client, $"{s.Url}/guest-invitations/{guest.Id}", HttpMethod.Delete, null, await Tag(client, s.Url)), 200, "MSG103");
        var revoked = await Write(client, $"{s.Url}/guest-invitations/{guest.Id}", HttpMethod.Delete, null, await Tag(client, s.Url));
        Assert.Equal(HttpStatusCode.NotFound, revoked.StatusCode);
        Assert.Equal(string.Empty, await revoked.Content.ReadAsStringAsync());
    }

    [Fact]
    public async Task Revoke_MemberIs403StrangerIs404AnonymousIs401()
    {
        var s = await f.Seed();
        var guest = await NewGuest(f, s, "Khách", "khach@example.test");
        using var member = f.Client(s.Member);
        await Code(await Write(member, $"{s.Url}/guest-invitations/{guest.Id}", HttpMethod.Delete, null, await Tag(member, s.Url)), 403, "MSG85");
        using var stranger = f.Client(s.Outsider);
        var denied = await Write(stranger, $"{s.Url}/guest-invitations/{guest.Id}", HttpMethod.Delete, null, "\"participants-0\"");
        Assert.Equal(HttpStatusCode.NotFound, denied.StatusCode);
        Assert.Equal(string.Empty, await denied.Content.ReadAsStringAsync());
        using var anonymous = f.Host.CreateClient();
        var noToken = await Write(anonymous, $"{s.Url}/guest-invitations/{guest.Id}", HttpMethod.Delete, null, "\"participants-0\"");
        Assert.Equal(HttpStatusCode.Unauthorized, noToken.StatusCode);
        await using var db = f.Db();
        Assert.Null((await db.MeetingGuestInvitations.FindAsync(guest.Id))!.RevokedAt);
    }

    [Fact]
    public async Task OwnResponse_DraftMeetingIs404()
    {
        var s = await f.Seed(MeetingStatus.Draft);
        using var client = f.Client(s.Chair);
        var response = await client.GetAsync(s.Url + "/invitation-response");
        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
        Assert.Equal(string.Empty, await response.Content.ReadAsStringAsync());
    }

    [Fact]
    public async Task OwnResponse_OrganizerWithoutSeatIs404()
    {
        var s = await f.Seed();
        using var client = f.Client(s.Organizer);
        var response = await client.GetAsync(s.Url + "/invitation-response");
        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
        Assert.Equal(string.Empty, await response.Content.ReadAsStringAsync());
    }

    [Fact]
    public async Task OwnResponse_StrangerIs404AndAnonymousIs401()
    {
        var s = await f.Seed();
        using var stranger = f.Client(s.Outsider);
        var denied = await stranger.GetAsync(s.Url + "/invitation-response");
        Assert.Equal(HttpStatusCode.NotFound, denied.StatusCode);
        Assert.Equal(string.Empty, await denied.Content.ReadAsStringAsync());
        using var anonymous = f.Host.CreateClient();
        Assert.Equal(HttpStatusCode.Unauthorized, (await anonymous.GetAsync(s.Url + "/invitation-response")).StatusCode);
    }

    [Fact]
    public async Task OwnResponse_RejectsQueryString()
    {
        var s = await f.Seed();
        using var client = f.Client(s.Member);
        await Code(await client.GetAsync(s.Url + "/invitation-response?x=1"), 400, "MSG276");
    }

    [Fact]
    public async Task Respond_OrganizerWithoutSeatIs404()
    {
        var s = await f.Seed();
        using var client = f.Client(s.Organizer);
        var response = await Write(client, s.Url + "/invitation-response", HttpMethod.Put, new { response = "Accepted" });
        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
        Assert.Equal(string.Empty, await response.Content.ReadAsStringAsync());
    }

    [Fact]
    public async Task Respond_StrangerIs404AndAnonymousIs401()
    {
        var s = await f.Seed();
        using var stranger = f.Client(s.Outsider);
        var denied = await Write(stranger, s.Url + "/invitation-response", HttpMethod.Put, new { response = "Accepted" });
        Assert.Equal(HttpStatusCode.NotFound, denied.StatusCode);
        Assert.Equal(string.Empty, await denied.Content.ReadAsStringAsync());
        using var anonymous = f.Host.CreateClient();
        var noToken = await Write(anonymous, s.Url + "/invitation-response", HttpMethod.Put, new { response = "Accepted" });
        Assert.Equal(HttpStatusCode.Unauthorized, noToken.StatusCode);
    }

    [Fact]
    public async Task Attendance_PagesListAndEmptyPagePastEnd()
    {
        var s = await f.Seed(MeetingStatus.InProgress);
        await NewGuest(f, s, "Khách Một", "khach-mot@example.test");
        await NewGuest(f, s, "Khách Hai", "khach-hai@example.test");
        using var client = f.Client(s.Secretary);
        var second = await client.GetFromJsonAsync<JsonElement>(s.Url + "/attendance?page=2&pageSize=2");
        Assert.Equal(2, second.GetProperty("items").EnumerateArray().Count());
        Assert.Equal(2, second.GetProperty("page").GetInt32());
        Assert.Equal(5, second.GetProperty("total").GetInt32());
        var past = await client.GetFromJsonAsync<JsonElement>(s.Url + "/attendance?page=99&pageSize=2");
        Assert.Empty(past.GetProperty("items").EnumerateArray());
        Assert.Equal(5, past.GetProperty("total").GetInt32());
    }

    [Fact]
    public async Task Attendance_CanMarkGuest()
    {
        var s = await f.Seed(MeetingStatus.InProgress);
        var guest = await NewGuest(f, s, "Khách", "khach-diem-danh@example.test");
        using var client = f.Client(s.Secretary);
        await Code(await Save(client, s, Entry(s.MeetingId, guest.Id, "Guest")), 200, "MSG147");
        var list = await client.GetFromJsonAsync<JsonElement>(s.Url + "/attendance");
        var row = list.GetProperty("items").EnumerateArray().Single(i => i.GetProperty("id").GetGuid() == guest.Id);
        Assert.Equal("Present", row.GetProperty("attendance").GetString());
    }

    [Fact]
    public async Task ChairCandidates_DeactivatedChairIs403()
    {
        var s = await f.Seed();
        using var client = f.Client(s.Chair);
        await using (var db = f.Db())
        {
            (await db.Users.FindAsync(s.Chair))!.IsActive = false;
            await db.SaveChangesAsync();
        }
        var response = await client.GetAsync(s.Url + "/participants/chair-handover-candidates");
        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
        Assert.Equal(string.Empty, await response.Content.ReadAsStringAsync());
    }
}
