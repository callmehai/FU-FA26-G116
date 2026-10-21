using System.Net;
using System.Net.Http.Json;
using System.Text;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using TrustCabinet.Domain.Entities;
using TrustCabinet.Domain.Enums;
using TrustCabinet.Domain.Participants;

namespace TrustCabinet.Web.Tests.Participants;

public sealed class ParticipantEndpointTests(ParticipantFixture f) : IClassFixture<ParticipantFixture>
{
    internal static async Task<JsonElement> Code(HttpResponseMessage response, int status, string code)
    {
        var text = await response.Content.ReadAsStringAsync();
        Assert.True((int)response.StatusCode == status, $"Expected {status}/{code}, got {(int)response.StatusCode}: {text}");
        var json = JsonDocument.Parse(text).RootElement.Clone(); Assert.Equal(code, json.GetProperty("code").GetString()); return json;
    }
    internal static async Task<HttpResponseMessage> Write(HttpClient client, string url, HttpMethod method, object? body = null, string? etag = "\"participants-0\"")
    {
        using var request = new HttpRequestMessage(method, url);
        if (etag is not null) request.Headers.TryAddWithoutValidation("If-Match", etag);
        if (body is not null) request.Content = JsonContent.Create(body);
        return await client.SendAsync(request);
    }
    internal static async Task<string> Tag(HttpClient client, string url)
    {
        var response = await client.GetAsync(url + "/participant-context"); response.EnsureSuccessStatusCode(); return response.Headers.ETag!.ToString();
    }

    [Fact]
    public async Task Internal_invitee_matching_current_guest_returns_userIds_error()
    {
        var s = await f.Seed();
        using var client = f.Client(s.Secretary);
        await Code(await Write(client, s.Url + "/guest-invitations", HttpMethod.Post,
            new { invitedName = "Khách", email = "duplicate@guests.test" }), 201, "MSG96");
        await using var db = f.Db();
        await db.Users.Where(u => u.Id == s.Outsider).ExecuteUpdateAsync(set => set.SetProperty(u => u.Email, "DUPLICATE@guests.test"));
        var result = await Code(await Write(client, s.Url + "/participants", HttpMethod.Post,
            new { userIds = new[] { s.Outsider }, role = "Member" }, await Tag(client, s.Url)), 400, "MSG97");
        Assert.NotEmpty(result.GetProperty("errors").GetProperty("userIds").EnumerateArray());
        Assert.False(await db.MeetingParticipants.AnyAsync(p => p.MeetingId == s.MeetingId && p.UserId == s.Outsider));
    }

    [Fact]
    public async Task Response_is_owned_without_attendance_changes()
    {
        var s = await f.Seed(); using var secretary = f.Client(s.Secretary); using var member = f.Client(s.Member);
        var summary = await member.GetFromJsonAsync<JsonElement>(s.Url + "/participants");
        Assert.Equal(3, summary.GetProperty("totals").GetProperty("pending").GetInt32());
        await Code(await Write(member, s.Url + "/invitation-response", HttpMethod.Put, new { response = "Accepted" }), 200, "MSG105");
        await Code(await Write(member, s.Url + "/invitation-response", HttpMethod.Put, new { response = "Accepted" }), 200, "MSG154");
        await Code(await Write(member, s.Url + "/invitation-response", HttpMethod.Put, new { response = "Delegated" }), 400, "MSG106");
        await using var db = f.Db(); var p = await db.MeetingParticipants.FindAsync(s.MemberAssignment);
        Assert.Equal(InvitationResponse.Accepted, p!.Response); Assert.Equal(f.Clock.Now, p.RespondedAt); Assert.Equal(AttendanceStatus.Unknown, p.Attendance);
        Assert.Equal(MeetingRole.Member, p.Role); Assert.Equal(0, (await db.Meetings.FindAsync(s.MeetingId))!.ParticipantsRevision);
    }



    [Theory]
    [InlineData(MeetingStatus.Draft, true, false)]
    [InlineData(MeetingStatus.WaitingApproval, false, false)]
    [InlineData(MeetingStatus.Rejected, true, false)]
    [InlineData(MeetingStatus.Approved, true, false)]
    [InlineData(MeetingStatus.Published, true, true)]
    [InlineData(MeetingStatus.InProgress, true, true)]
    [InlineData(MeetingStatus.Ended, false, true)]
    public async Task State_matrix_controls_read_management_and_response(MeetingStatus state, bool editable, bool published)
    {
        var s = await f.Seed(state); using var secretary = f.Client(s.Secretary); using var chair = f.Client(s.Chair); using var member = f.Client(s.Member);
        Assert.Equal(HttpStatusCode.OK, (await secretary.GetAsync(s.Url + "/participants")).StatusCode);
        Assert.Equal(HttpStatusCode.OK, (await chair.GetAsync(s.Url + "/participants")).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await secretary.GetAsync(s.Url + "/invitation-responses")).StatusCode);
        var add = await Write(secretary, s.Url + "/participants", HttpMethod.Post, new { userIds = new[] { s.Outsider }, role = "Member" });
        await Code(add, editable ? 201 : 409, editable ? "MSG96" : "MSG98");
        var response = await Write(member, s.Url + "/invitation-response", HttpMethod.Put, new { response = "Accepted" });
        if (ParticipantRules.Respondable(state)) await Code(response, 200, "MSG105");
        else if (published) await Code(response, 409, "MSG107");
        else Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    public async Task Removing_and_reinviting_keeps_history_and_revokes_meeting_and_agenda_access()
    {
        var s = await f.Seed(); using var secretary = f.Client(s.Secretary); using var member = f.Client(s.Member);
        await Code(await Write(member, s.Url + "/invitation-response", HttpMethod.Put, new { response = "Declined" }), 200, "MSG105");
        await Code(await Write(secretary, s.Url + $"/participants/{s.MemberAssignment}", HttpMethod.Delete), 200, "MSG103");
        Assert.Equal(HttpStatusCode.NotFound, (await member.GetAsync(s.Url)).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await member.GetAsync(s.Url + "/agenda")).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await Write(member, s.Url + "/invitation-response", HttpMethod.Put, new { response = "Accepted" })).StatusCode);
        var tag = await Tag(secretary, s.Url);
        var json = await Code(await Write(secretary, s.Url + "/participants", HttpMethod.Post, new { userIds = new[] { s.Member }, role = "Member" }, tag), 201, "MSG96");
        var newId = json.GetProperty("createdIds")[0].GetGuid(); Assert.NotEqual(s.MemberAssignment, newId);
        await using var db = f.Db(); var history = await db.MeetingParticipants.FindAsync(s.MemberAssignment);
        Assert.Equal(AssignmentStatus.Removed, history!.AssignmentStatus); Assert.Equal(InvitationResponse.Declined, history.Response);
        Assert.Equal(InvitationResponse.Pending, (await db.MeetingParticipants.FindAsync(newId))!.Response);
        var removal = await db.Notifications.SingleAsync(n => n.MeetingId == s.MeetingId && n.Type == NotificationType.MeetingParticipantRemoved);
        Assert.Null(removal.TargetId); Assert.Null(removal.TargetType);
    }

    [Fact]
    public async Task Chair_replacement_requires_confirmation_and_rolls_back_both_roles_on_second_save_failure()
    {
        var s = await f.Seed(); using var secretary = f.Client(s.Chair);
        var changes = new[] { new { participantId = s.ChairAssignment, role = "Member" }, new { participantId = s.MemberAssignment, role = "Chair" } };
        await Code(await Write(secretary, s.Url + "/participants/assignments", HttpMethod.Put, new { changes }), 400, "MSG101");
        var body = new { changes, chairReplacement = new { outgoingParticipantId = s.ChairAssignment, incomingParticipantId = s.MemberAssignment, confirmed = true } };
        f.Failure.MeetingId = s.MeetingId; f.Failure.FailOnSave = 2;
        try { await Code(await Write(secretary, s.Url + "/participants/assignments", HttpMethod.Put, body), 503, "MSG102"); }
        finally { f.Failure.Reset(); }
        await using (var db = f.Db())
        {
            Assert.Equal(MeetingRole.Chair, (await db.MeetingParticipants.FindAsync(s.ChairAssignment))!.Role);
            Assert.Equal(MeetingRole.Member, (await db.MeetingParticipants.FindAsync(s.MemberAssignment))!.Role);
            Assert.Equal(0, await db.MeetingInvitationDispatches.CountAsync(d => d.MeetingId == s.MeetingId));
        }
        await Code(await Write(secretary, s.Url + "/participants/assignments", HttpMethod.Put, body), 200, "MSG257");
        await using var check = f.Db(); Assert.Equal(2, await check.MeetingInvitationDispatches.CountAsync(d => d.MeetingId == s.MeetingId));
    }

    [Theory]
    [InlineData(MeetingStatus.Published)]
    [InlineData(MeetingStatus.InProgress)]
    public async Task Inactive_chair_can_only_be_demoted_during_confirmed_replacement_by_active_member(MeetingStatus status)
    {
        var s = await f.Seed(status); using var secretary = f.Client(s.Secretary);
        await using (var db = f.Db())
        {
            (await db.Users.FindAsync(s.Chair))!.IsActive = false;
            await db.SaveChangesAsync();
        }
        var changes = new[] { new { participantId = s.ChairAssignment, role = "Member" }, new { participantId = s.MemberAssignment, role = "Chair" } };
        var body = new { changes, chairReplacement = new { outgoingParticipantId = s.ChairAssignment, incomingParticipantId = s.MemberAssignment, confirmed = true } };
        await Code(await Write(secretary, s.Url + "/participants/assignments", HttpMethod.Put, new { changes }), 400, "MSG101");
        await using (var db = f.Db())
        {
            (await db.Users.FindAsync(s.Member))!.IsActive = false;
            await db.SaveChangesAsync();
        }
        await Code(await Write(secretary, s.Url + "/participants/assignments", HttpMethod.Put, body), 400, "MSG259");
        await using (var db = f.Db())
        {
            (await db.Users.FindAsync(s.Member))!.IsActive = true;
            await db.SaveChangesAsync();
        }
        f.Failure.MeetingId = s.MeetingId; f.Failure.FailOnSave = 2;
        try { await Code(await Write(secretary, s.Url + "/participants/assignments", HttpMethod.Put, body), 503, "MSG102"); }
        finally { f.Failure.Reset(); }
        await using (var db = f.Db())
        {
            Assert.Equal(MeetingRole.Chair, (await db.MeetingParticipants.FindAsync(s.ChairAssignment))!.Role);
            Assert.Equal(MeetingRole.Member, (await db.MeetingParticipants.FindAsync(s.MemberAssignment))!.Role);
        }
        await Code(await Write(secretary, s.Url + "/participants/assignments", HttpMethod.Put, body), 200, "MSG100");
        await using var check = f.Db();
        Assert.Equal(MeetingRole.Member, (await check.MeetingParticipants.FindAsync(s.ChairAssignment))!.Role);
        Assert.Equal(MeetingRole.Chair, (await check.MeetingParticipants.FindAsync(s.MemberAssignment))!.Role);
        Assert.Equal(MeetingRole.Secretary, (await check.MeetingParticipants.FindAsync(s.SecretaryAssignment))!.Role);
    }

    [Fact]
    public async Task Last_secretary_is_retained_but_may_be_replaced_atomically()
    {
        var s = await f.Seed(); using var secretary = f.Client(s.Secretary);
        await Code(await Write(secretary, s.Url + $"/participants/{s.SecretaryAssignment}", HttpMethod.Delete), 409, "MSG109");
        await Code(await Write(secretary, s.Url + "/participants/assignments", HttpMethod.Put, new
        {
            changes = new[] {
            new { participantId = s.SecretaryAssignment, role = "Member" }, new { participantId = s.MemberAssignment, role = "Secretary" } }
        }), 200, "MSG100");
        Assert.Equal(HttpStatusCode.OK, (await secretary.GetAsync(s.Url + "/participants")).StatusCode);
    }

    [Fact]
    public async Task Concurrent_adds_use_revision_and_store_one_assignment_and_delivery_intent()
    {
        var s = await f.Seed(); using var a = f.Client(s.Secretary); using var b = f.Client(s.Secretary);
        var body = new { userIds = new[] { s.Outsider }, role = "Member" };
        var results = await Task.WhenAll(Write(a, s.Url + "/participants", HttpMethod.Post, body), Write(b, s.Url + "/participants", HttpMethod.Post, body));
        await Code(results.Single(r => r.StatusCode == HttpStatusCode.Created), 201, "MSG96");
        await Code(results.Single(r => r.StatusCode == HttpStatusCode.Conflict), 409, "MSG275");
        await using var db = f.Db(); Assert.Equal(1, await db.MeetingParticipants.CountAsync(p => p.MeetingId == s.MeetingId && p.UserId == s.Outsider));
        Assert.Equal(1, await db.MeetingInvitationDispatches.CountAsync(d => d.MeetingId == s.MeetingId));
    }

    [Fact]
    public async Task Guest_contract_requires_verified_current_access_and_never_internal_roles()
    {
        var s = await f.Seed(); using var secretary = f.Client(s.Secretary);
        var json = await Code(await Write(secretary, s.Url + "/guest-invitations", HttpMethod.Post, new
        {
            invitedName = "Khách",
            email = "guest@example.test"
        }), 201, "MSG96");
        var id = json.GetProperty("createdIds")[0].GetGuid();
        using var guest = f.Guest(s.MeetingId, id); using var unverified = f.Guest(s.MeetingId, id, verified: false);
        using var expired = f.Guest(s.MeetingId, id, expired: true); using var mixed = f.Guest(s.MeetingId, id, user: s.Secretary);
        Assert.Equal(HttpStatusCode.OK, (await guest.GetAsync(s.Url + "/participants")).StatusCode);
        foreach (var denied in new[] { unverified, expired, mixed }) Assert.Equal(HttpStatusCode.NotFound, (await denied.GetAsync(s.Url + "/participants")).StatusCode);
        await Code(await Write(guest, s.Url + "/invitation-response", HttpMethod.Put, new { response = "Accepted" }), 200, "MSG105");
        Assert.Equal(HttpStatusCode.Forbidden, (await Write(guest, s.Url + "/participants", HttpMethod.Post, new { userIds = new[] { s.Outsider }, role = "Secretary" })).StatusCode);
        await Code(await Write(secretary, s.Url + $"/guest-invitations/{id}", HttpMethod.Delete, etag: await Tag(secretary, s.Url)), 200, "MSG103");
        Assert.Equal(HttpStatusCode.NotFound, (await guest.GetAsync(s.Url + "/invitation-response")).StatusCode);
        await using var db = f.Db(); Assert.Equal(InvitationResponse.Accepted, (await db.MeetingGuestInvitations.FindAsync(id))!.Response);
        Assert.False(await db.Users.AnyAsync(u => u.Email == "guest@example.test"));
    }

    [Fact]
    public async Task Batch_validation_is_atomic_and_guest_email_uses_citext()
    {
        var s = await f.Seed(); using var client = f.Client(s.Secretary);
        await Code(await Write(client, s.Url + "/participants", HttpMethod.Post, new { userIds = new[] { s.Outsider, Guid.NewGuid() }, role = "Member" }), 400, "MSG97");
        await Code(await Write(client, s.Url + "/participants", HttpMethod.Post, new { userIds = new[] { s.Outsider }, role = "Member", additionalPermissions = new[] { "AdminUsers" } }), 400, "MSG97");
        await Code(await Write(client, s.Url + "/guest-invitations", HttpMethod.Post, new { invitedName = "Khách", email = "Case@example.test" }), 201, "MSG96");
        await Code(await Write(client, s.Url + "/guest-invitations", HttpMethod.Post, new { invitedName = "Khách", email = "case@EXAMPLE.test" }, await Tag(client, s.Url)), 400, "MSG97");
        await using var db = f.Db(); Assert.False(await db.MeetingParticipants.AnyAsync(p => p.MeetingId == s.MeetingId && p.UserId == s.Outsider));
    }

    [Theory]
    [InlineData("text/plain", "{}", 415, "MSG34")]
    [InlineData("application/json", "{\"response\":\"Accepted\",\"response\":\"Declined\"}", 400, "MSG106")]
    [InlineData("application/json", "{\"response\":\"Accepted\",\"userId\":\"spoof\"}", 400, "MSG106")]
    [InlineData("application/json", "{\"Response\":\"Accepted\"}", 400, "MSG106")]
    public async Task Strict_json_rejects_ambiguous_or_untrusted_fields(string type, string body, int status, string code)
    {
        var s = await f.Seed(); using var client = f.Client(s.Member);
        await Code(await client.PutAsync(s.Url + "/invitation-response", new StringContent(body, Encoding.UTF8, type)), status, code);
    }

    [Fact]
    public async Task Payload_auth_and_write_failure_contracts_are_explicit()
    {
        var s = await f.Seed(); using var client = f.Client(s.Secretary); using var outsider = f.Client(s.Outsider); using var anonymous = f.Client();
        Assert.Equal(HttpStatusCode.Unauthorized, (await anonymous.GetAsync(s.Url + "/participants")).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await outsider.GetAsync(s.Url + "/participants")).StatusCode);
        f.Guests.FailProfiles = true;
        try { await Code(await client.GetAsync(s.Url + "/participants"), 500, "MSG95"); }
        finally { f.Guests.FailProfiles = false; }
        await Code(await Write(client, s.Url + "/participants", HttpMethod.Post, new { userIds = new[] { s.Outsider }, role = "Member" }, etag: null), 400, "MSG276");
        await Code(await client.PutAsync(s.Url + "/invitation-response", new StringContent(new string('x', 65537), Encoding.UTF8, "application/json")), 413, "MSG87");
        foreach (var (op, body, method, code) in new[] {
            ("/participants", (object)new { userIds = new[] { s.Outsider }, role = "Member" }, HttpMethod.Post, "MSG99"),
            ($"/participants/{s.MemberAssignment}", (object?)null, HttpMethod.Delete, "MSG104"),
            ("/invitation-response", (object)new { response = "Accepted" }, HttpMethod.Put, "MSG108") })
        {
            f.Failure.MeetingId = s.MeetingId;
            try { await Code(await Write(client, s.Url + op, method, body), 503, code); }
            finally { f.Failure.Reset(); }
        }
    }

    [Fact]
    public async Task Guest_sessions_are_independent_and_profile_does_not_replace_invitation_evidence()
    {
        var s = await f.Seed(); using var secretary = f.Client(s.Secretary);
        var created = await Code(await Write(secretary, s.Url + "/guest-invitations", HttpMethod.Post,
            new { invitedName = "Tên trên lời mời", email = "identity@example.test" }), 201, "MSG96");
        var id = created.GetProperty("createdIds")[0].GetGuid();
        f.Guests.Profiles[id] = new("Tên tự khai", "Chức danh tự khai");
        using var first = f.Guest(s.MeetingId, id);
        var firstSession = f.Guests.Sessions.Values.Single(v => v.Identity.InvitationId == id);
        using var second = f.Guest(s.MeetingId, id);
        var roster = await second.GetFromJsonAsync<JsonElement>(s.Url + "/participants");
        var row = roster.GetProperty("items").EnumerateArray().Single(p => p.GetProperty("id").GetGuid() == id);
        Assert.Equal("Tên trên lời mời", row.GetProperty("invitedName").GetString());
        Assert.Equal("Tên tự khai", row.GetProperty("displayName").GetString());
        Assert.Equal("identity@example.test", row.GetProperty("email").GetString());
        Assert.False(row.TryGetProperty("additionalPermissions", out _));
        Assert.Equal(HttpStatusCode.NotFound, (await second.GetAsync(s.Url + "/invitation-responses")).StatusCode);
        f.Guests.Sessions[firstSession.Identity.SessionId] = firstSession with { Revoked = true };
        Assert.Equal(HttpStatusCode.NotFound, (await first.GetAsync(s.Url + "/invitation-response")).StatusCode);
        Assert.Equal(HttpStatusCode.OK, (await second.GetAsync(s.Url + "/invitation-response")).StatusCode);
        var other = await f.Seed();
        Assert.Equal(HttpStatusCode.NotFound, (await second.GetAsync(other.Url + "/participants")).StatusCode);
        Assert.Equal(HttpStatusCode.OK, (await second.GetAsync(s.Url + "/participants")).StatusCode);

    }

    [Fact]
    public async Task Inactive_secretary_is_forbidden_and_chunked_payload_is_bounded()
    {
        var s = await f.Seed(MeetingStatus.Approved); using var organizer = f.Client(s.Organizer); using var secretary = f.Client(s.Secretary);
        await using var db = f.Db(); var user = (await db.Users.FindAsync(s.Secretary))!; user.IsActive = false; await db.SaveChangesAsync();
        var context = await organizer.GetFromJsonAsync<JsonElement>(s.Url + "/participant-context");
        Assert.False(context.GetProperty("permissions").GetProperty("canManageParticipants").GetBoolean());
        Assert.Equal(HttpStatusCode.Forbidden, (await secretary.GetAsync(s.Url + "/participants")).StatusCode);
        Assert.Equal(HttpStatusCode.MethodNotAllowed, (await Write(organizer, s.Url + "/participants/secretary-recovery", HttpMethod.Post, new { userId = s.Outsider })).StatusCode);
        user.IsActive = true; await db.SaveChangesAsync();
        using var request = new HttpRequestMessage(HttpMethod.Post, s.Url + "/participants") { Content = new UnknownLengthContent(new string('x', 65537)) };
        request.Headers.IfMatch.ParseAdd("\"participants-0\"");
        await Code(await secretary.SendAsync(request), 413, "MSG87");
        await Code(await Write(secretary, s.Url + $"/participants/{s.MemberAssignment}", HttpMethod.Delete, new { role = "Chair" }), 400, "MSG97");
    }

    private sealed class UnknownLengthContent : HttpContent
    {
        private readonly byte[] _bytes;
        public UnknownLengthContent(string value)
        {
            _bytes = Encoding.UTF8.GetBytes(value);
            Headers.ContentType = new("application/json");
        }
        protected override Task SerializeToStreamAsync(Stream stream, TransportContext? context)
            => stream.WriteAsync(_bytes).AsTask();
        protected override bool TryComputeLength(out long length) { length = 0; return false; }
    }
}
