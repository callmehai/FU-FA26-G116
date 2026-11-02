using System.IdentityModel.Tokens.Jwt;
using System.Net;
using System.Net.Http.Json;
using System.Security.Claims;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using TrustCabinet.Domain.Meetings;
using static TrustCabinet.Web.Tests.GuestAccess.GuestAccessFixture;

namespace TrustCabinet.Web.Tests.GuestAccess;

/// <summary>UC-04 steps 5–6 and ALT-1 (SCRUM-119): guest token, identity resolver, self-declared profile, logout.</summary>
public sealed class GuestSessionTests(GuestAccessFixture f) : IClassFixture<GuestAccessFixture>
{
    [Fact]
    public async Task Verification_issues_a_guest_token_bound_to_the_session_without_internal_claims()
    {
        var s = await f.Seed();
        var (_, token) = await f.SignIn(s, profile: false);

        var jwt = new JwtSecurityTokenHandler().ReadJwtToken(token);
        await using var db = f.Db();
        var session = await db.GuestSessions.SingleAsync(x => x.InvitationId == s.InvitationId);
        Assert.Equal("guest:" + session.Id, jwt.Subject);
        Assert.False(Guid.TryParse(jwt.Subject, out _));
        Assert.Equal(s.MeetingId.ToString(), jwt.Claims.Single(c => c.Type == "guest_meeting").Value);
        Assert.Equal(s.InvitationId.ToString(), jwt.Claims.Single(c => c.Type == "guest_invitation").Value);
        Assert.Equal(session.Id.ToString(), jwt.Claims.Single(c => c.Type == "guest_session").Value);
        Assert.DoesNotContain(jwt.Claims, c => c.Type is "cap" or "email" or ClaimTypes.Name or ClaimTypes.Role);
        Assert.Equal(session.ExpiresAt.ToUnixTimeSeconds(), new DateTimeOffset(jwt.ValidTo).ToUnixTimeSeconds());
    }

    [Fact]
    public async Task Profile_is_self_declared_and_never_overwrites_the_invitation()
    {
        var s = await f.Seed();
        var (client, _) = await f.SignIn(s, profile: false);

        var before = await Expect(await client.GetAsync("/api/guest-access/session"), 200);
        Assert.False(before.GetProperty("profileCompleted").GetBoolean());
        Assert.Equal(s.Email, before.GetProperty("email").GetString());

        var invalid = await Expect(await client.PutAsJsonAsync("/api/guest-access/session/profile",
            new { displayName = "  ", jobTitle = new string('x', 201) }), 400, "MSG377");
        Assert.True(invalid.GetProperty("errors").TryGetProperty("displayName", out _));
        Assert.True(invalid.GetProperty("errors").TryGetProperty("jobTitle", out _));

        // Shown to every member: NUL would fail in PostgreSQL, U+202E would reverse the name on the roster.
        var invisible = await Expect(await client.PutAsJsonAsync("/api/guest-access/session/profile",
            new { displayName = "Khách\u202EAdmin", jobTitle = "Chuyên gia\0" }), 400, "MSG377");
        Assert.True(invisible.GetProperty("errors").TryGetProperty("displayName", out _));
        Assert.True(invisible.GetProperty("errors").TryGetProperty("jobTitle", out _));

        var saved = await Expect(await client.PutAsJsonAsync("/api/guest-access/session/profile",
            new { displayName = "  Nguyễn Văn Khách ", jobTitle = " " }), 200, "MSG376");
        Assert.Equal("Nguyễn Văn Khách", saved.GetProperty("session").GetProperty("displayName").GetString());
        Assert.Equal(System.Text.Json.JsonValueKind.Null, saved.GetProperty("session").GetProperty("jobTitle").ValueKind);
        Assert.True(saved.GetProperty("session").GetProperty("profileCompleted").GetBoolean());

        await using var db = f.Db();
        var invitation = await db.MeetingGuestInvitations.SingleAsync(g => g.Id == s.InvitationId);
        Assert.Equal("Khách mời", invitation.InvitedName);
        Assert.Equal(s.Email, invitation.Email);
        Assert.Equal("Nguyễn Văn Khách", invitation.SelfDeclaredDisplayName);
    }

    [Fact]
    public async Task The_declared_name_cannot_be_changed_afterwards_even_from_another_session()
    {
        var s = await f.Seed();
        var (first, _) = await f.SignIn(s, profile: false);
        await Expect(await first.PutAsJsonAsync("/api/guest-access/session/profile",
            new { displayName = "Nguyễn Văn Khách", jobTitle = "Chuyên gia" }), 200, "MSG376");

        // A retry of the same answer is harmless; any other answer is refused, from this session or a new one.
        await Expect(await first.PutAsJsonAsync("/api/guest-access/session/profile",
            new { displayName = " Nguyễn Văn Khách ", jobTitle = "Chuyên gia" }), 200, "MSG376");
        await Expect(await first.PutAsJsonAsync("/api/guest-access/session/profile",
            new { displayName = "Giám đốc Sở", jobTitle = "Chuyên gia" }), 409, "MSG381");
        await Expect(await first.PutAsJsonAsync("/api/guest-access/session/profile",
            new { displayName = "Nguyễn Văn Khách" }), 409, "MSG381");
        f.Clock.Now = f.Clock.Now.AddMinutes(1);
        var (second, _) = await f.SignIn(s, profile: false);
        await Expect(await second.PutAsJsonAsync("/api/guest-access/session/profile",
            new { displayName = "Tên khác" }), 409, "MSG381");

        await using var db = f.Db();
        var invitation = await db.MeetingGuestInvitations.SingleAsync(g => g.Id == s.InvitationId);
        Assert.Equal(("Nguyễn Văn Khách", "Chuyên gia"), (invitation.SelfDeclaredDisplayName, invitation.SelfDeclaredJobTitle));
    }

    [Fact]
    public async Task Session_says_whether_documents_may_be_downloaded_without_telling_the_sensitivity()
    {
        var s = await f.Seed();
        var (client, _) = await f.SignIn(s);
        var session = await Expect(await client.GetAsync("/api/guest-access/session"), 200);
        Assert.True(session.GetProperty("canDownload").GetBoolean());
        Assert.False(session.TryGetProperty("sensitivity", out _));
        Assert.False(session.GetProperty("meeting").TryGetProperty("sensitivity", out _));

        await using (var db = f.Db())
            await db.Meetings.Where(m => m.Id == s.MeetingId)
                .ExecuteUpdateAsync(set => set.SetProperty(m => m.Sensitivity, Domain.Enums.Sensitivity.Secret));
        Assert.False((await Expect(await client.GetAsync("/api/guest-access/session"), 200)).GetProperty("canDownload").GetBoolean());
    }

    [Fact]
    public async Task Reopening_with_a_live_session_needs_no_new_code_and_a_second_device_reuses_the_profile()
    {
        var s = await f.Seed();
        var (first, _) = await f.SignIn(s);
        var sent = f.Mail.Sent.Count(m => m.ToAddress == s.Email);
        // ALT-1: the stored token is enough; no code is requested.
        Assert.True((await Expect(await first.GetAsync("/api/guest-access/session"), 200)).GetProperty("profileCompleted").GetBoolean());
        Assert.Equal(sent, f.Mail.Sent.Count(m => m.ToAddress == s.Email));

        f.Clock.Now = f.Clock.Now.AddMinutes(1);
        var (second, _) = await f.SignIn(s, profile: false);
        Assert.True((await Expect(await second.GetAsync("/api/guest-access/session"), 200)).GetProperty("profileCompleted").GetBoolean());
    }

    [Fact]
    public async Task Logout_ends_only_that_session_and_cannot_be_undone_by_signing_in_again()
    {
        var s = await f.Seed();
        var (first, _) = await f.SignIn(s);
        f.Clock.Now = f.Clock.Now.AddMinutes(1);
        var (second, _) = await f.SignIn(s);

        Assert.Equal(HttpStatusCode.NoContent, (await first.DeleteAsync("/api/guest-access/session")).StatusCode);
        Assert.Equal(HttpStatusCode.NoContent, (await first.DeleteAsync("/api/guest-access/session")).StatusCode);
        await Expect(await first.GetAsync("/api/guest-access/session"), 404, "MSG378");
        await Expect(await first.PutAsJsonAsync("/api/guest-access/session/profile", new { displayName = "Khác" }), 404, "MSG378");
        await Expect(await second.GetAsync("/api/guest-access/session"), 200);

        f.Clock.Now = f.Clock.Now.AddMinutes(1);
        await f.SignIn(s);
        await Expect(await first.GetAsync("/api/guest-access/session"), 404, "MSG378");
    }

    [Fact]
    public async Task Session_ends_after_twelve_hours_and_with_its_invitation()
    {
        var s = await f.Seed();
        var (client, _) = await f.SignIn(s);
        f.Clock.Now = f.Clock.Now.AddHours(12);
        await Expect(await client.GetAsync("/api/guest-access/session"), 404, "MSG378");

        var other = await f.Seed();
        var (revoked, _) = await f.SignIn(other);
        await using (var db = f.Db())
            await db.MeetingGuestInvitations.Where(g => g.Id == other.InvitationId).ExecuteUpdateAsync(set => set
                .SetProperty(g => g.RevokedAt, f.Clock.Now).SetProperty(g => g.RevokedById, other.SecretaryId));
        await Expect(await revoked.GetAsync("/api/guest-access/session"), 404, "MSG378");
    }

    [Fact]
    public async Task Session_endpoints_refuse_anonymous_and_internal_callers()
    {
        using var anonymous = f.Host.CreateClient();
        Assert.Equal(HttpStatusCode.Unauthorized, (await anonymous.GetAsync("/api/guest-access/session")).StatusCode);
        var s = await f.Seed();
        using var secretary = f.UserClient(s.SecretaryId);
        Assert.Equal(HttpStatusCode.Forbidden, (await secretary.GetAsync("/api/guest-access/session")).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, (await secretary.DeleteAsync("/api/guest-access/session")).StatusCode);
    }

    [Fact]
    public async Task Resolver_reads_only_well_formed_guest_claims()
    {
        var resolver = f.Host.Services.GetRequiredService<IMeetingGuestIdentityResolver>();
        Assert.IsType<GuestTokenIdentityResolver>(resolver);
        ClaimsPrincipal Principal(params (string Type, string Value)[] claims)
            => new(new ClaimsIdentity(claims.Select(c => new Claim(c.Type, c.Value)), "Bearer"));
        var (meeting, invitation, session) = (Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid());
        var guest = new[] { ("guest_meeting", meeting.ToString()), ("guest_invitation", invitation.ToString()), ("guest_session", session.ToString()) };
        var empty = new MeetingGuestSession(Guid.Empty, Guid.Empty, Guid.Empty);

        Assert.Equal(new MeetingGuestSession(meeting, invitation, session), resolver.Resolve(Principal([("sub", $"guest:{session}"), .. guest])));
        Assert.Null(resolver.Resolve(Principal(("sub", Guid.NewGuid().ToString()))));
        Assert.Null(resolver.Resolve(new ClaimsPrincipal(new ClaimsIdentity(guest.Select(c => new Claim(c.Item1, c.Item2))))));
        // Mixed, mismatched or malformed: still a guest, so the caller denies instead of falling back to a user.
        Assert.Equal(empty, resolver.Resolve(Principal([("sub", Guid.NewGuid().ToString()), .. guest])));
        Assert.Equal(empty, resolver.Resolve(Principal([("sub", $"guest:{Guid.NewGuid()}"), .. guest])));
        Assert.Equal(empty, resolver.Resolve(Principal(("sub", $"guest:{session}"), ("guest_meeting", "x"))));
        Assert.Equal(empty, resolver.Resolve(Principal([("sub", $"guest:{session}"), .. guest, ("guest_meeting", Guid.NewGuid().ToString())])));
    }
}
