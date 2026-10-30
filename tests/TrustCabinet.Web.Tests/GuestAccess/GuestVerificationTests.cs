using System.Net.Http.Json;
using Microsoft.EntityFrameworkCore;
using TrustCabinet.Domain.Enums;
using static TrustCabinet.Web.Tests.GuestAccess.GuestAccessFixture;

namespace TrustCabinet.Web.Tests.GuestAccess;

/// <summary>UC-04 steps 1–4 (SCRUM-118): invitation link, one-time code, verification.</summary>
public sealed class GuestVerificationTests(GuestAccessFixture f) : IClassFixture<GuestAccessFixture>
{
    private Task<HttpResponseMessage> Verify(HttpClient client, Scenario s, string code, string? token = null)
        => client.PostAsJsonAsync(s.Url + "/verify", new { token = token ?? s.LinkToken, code });

    private static string Wrong(string code) => ((int.Parse(code) + 1) % 1_000_000).ToString("D6");

    [Fact]
    public async Task Open_needs_the_link_token_and_shows_the_meeting_with_a_masked_email()
    {
        var s = await f.Seed();
        var other = await f.Seed();
        using var client = f.Host.CreateClient();

        foreach (var token in new[] { "", "not-a-token", other.LinkToken })
            await Expect(await client.PostAsJsonAsync(s.Url, new { token }), 404, "MSG371");
        await Expect(await client.PostAsync(s.Url, null), 404, "MSG371");
        await Expect(await client.PostAsJsonAsync($"/api/guest-access/invitations/{Guid.NewGuid()}", new { token = s.LinkToken }), 404, "MSG371");

        var response = await client.PostAsJsonAsync(s.Url, new { token = s.LinkToken });
        var body = await Expect(response, 200);
        Assert.Equal("no-store", response.Headers.CacheControl?.ToString());
        Assert.Equal("Họp có Khách", body.GetProperty("meeting").GetProperty("title").GetString());
        Assert.Equal(s.Email[..2] + "***@guests.test", body.GetProperty("maskedEmail").GetString());
    }

    [Theory]
    [InlineData(MeetingStatus.Draft)]
    [InlineData(MeetingStatus.Approved)]
    [InlineData(MeetingStatus.Ended)]
    [InlineData(MeetingStatus.Cancelled)]
    public async Task Invitation_of_a_meeting_that_is_not_open_is_no_longer_valid(MeetingStatus status)
    {
        var s = await f.Seed(status);
        using var client = f.Host.CreateClient();
        await Expect(await client.PostAsJsonAsync(s.Url, new { token = s.LinkToken }), 404, "MSG371");
        await Expect(await client.PostAsJsonAsync(s.Url + "/code", new { token = s.LinkToken }), 404, "MSG371");
        Assert.DoesNotContain(f.Mail.Sent, m => m.ToAddress == s.Email);
    }

    [Fact]
    public async Task Code_goes_only_to_the_invited_email_and_is_stored_only_as_a_hash()
    {
        var s = await f.Seed();
        using var client = f.Host.CreateClient();
        var body = await Expect(await client.PostAsJsonAsync(s.Url + "/code", new { token = s.LinkToken }), 200, "MSG375");
        Assert.Equal(900, body.GetProperty("expiresInSeconds").GetInt32());
        Assert.Equal(60, body.GetProperty("resendAfterSeconds").GetInt32());

        var code = f.Mail.LastCode(s.Email);
        Assert.Matches("^[0-9]{6}$", code);
        await using var db = f.Db();
        var challenge = await db.GuestVerificationChallenges.SingleAsync(c => c.InvitationId == s.InvitationId);
        Assert.Equal(64, challenge.CodeHash.Length);
        Assert.DoesNotContain(code, challenge.CodeHash);
        Assert.Equal(f.Clock.Now.AddMinutes(15), challenge.ExpiresAt);
        // Straight to the transport: the code never lands in the stored email queue.
        Assert.False(await db.EmailMessages.AnyAsync(e => e.ToAddress == s.Email));
    }

    [Fact]
    public async Task Resend_waits_one_minute_then_retires_the_previous_code()
    {
        var s = await f.Seed();
        using var client = f.Host.CreateClient();
        var first = await f.SendCode(client, s);

        f.Clock.Now = f.Clock.Now.AddSeconds(20);
        var tooSoon = await client.PostAsJsonAsync(s.Url + "/code", new { token = s.LinkToken });
        var wait = await Expect(tooSoon, 429, "MSG373");
        Assert.Equal(40, wait.GetProperty("retryAfterSeconds").GetInt32());
        Assert.Equal("40", tooSoon.Headers.GetValues("Retry-After").Single());

        f.Clock.Now = f.Clock.Now.AddSeconds(40);
        var second = await f.SendCode(client, s);
        if (second != first) await Expect(await Verify(client, s, first), 400, "MSG126");
        await Expect(await Verify(client, s, second), 200);
    }

    [Fact]
    public async Task Fifth_wrong_code_locks_it_even_against_the_right_code_until_a_new_one_is_sent()
    {
        var s = await f.Seed();
        using var client = f.Host.CreateClient();
        var code = await f.SendCode(client, s);

        await Expect(await Verify(client, s, "12a456"), 400, "MSG126");
        for (var attempt = 1; attempt <= 4; attempt++) await Expect(await Verify(client, s, Wrong(code)), 400, "MSG126");
        await Expect(await Verify(client, s, Wrong(code)), 400, "MSG372");
        await Expect(await Verify(client, s, code), 400, "MSG372");
        await using (var db = f.Db())
            Assert.Equal(5, (await db.GuestVerificationChallenges.SingleAsync(c => c.InvitationId == s.InvitationId)).FailedAttempts);

        f.Clock.Now = f.Clock.Now.AddMinutes(1);
        await Expect(await Verify(client, s, await f.SendCode(client, s)), 200);
    }

    [Fact]
    public async Task Code_expires_after_fifteen_minutes()
    {
        var s = await f.Seed();
        using var client = f.Host.CreateClient();
        var code = await f.SendCode(client, s);
        f.Clock.Now = f.Clock.Now.AddMinutes(15);
        await Expect(await Verify(client, s, code), 400, "MSG126");
    }

    [Fact]
    public async Task Code_is_single_use_and_concurrent_verifications_open_one_session_without_users()
    {
        var s = await f.Seed();
        using var client = f.Host.CreateClient();
        await Expect(await Verify(client, s, "123456"), 400, "MSG126");
        var code = await f.SendCode(client, s);
        int users;
        await using (var db = f.Db()) users = await db.Users.CountAsync();

        var responses = await Task.WhenAll(Enumerable.Range(0, 4).Select(_ => Verify(client, s, code)));
        Assert.Single(responses, r => r.IsSuccessStatusCode);
        foreach (var rejected in responses.Where(r => !r.IsSuccessStatusCode)) await Expect(rejected, 400, "MSG126");
        await Expect(await Verify(client, s, code), 400, "MSG126");

        await using var check = f.Db();
        var session = await check.GuestSessions.SingleAsync(x => x.InvitationId == s.InvitationId);
        Assert.Equal(f.Clock.Now.AddHours(12), session.ExpiresAt);
        Assert.Equal(users, await check.Users.CountAsync());
        Assert.False(await check.MeetingParticipants.AnyAsync(p => p.MeetingId == s.MeetingId && p.UserId != s.SecretaryId));
    }

    [Fact]
    public async Task Revoking_between_send_and_verify_denies_the_code()
    {
        var s = await f.Seed();
        using var client = f.Host.CreateClient();
        var code = await f.SendCode(client, s);
        await using (var db = f.Db())
            await db.MeetingGuestInvitations.Where(g => g.Id == s.InvitationId).ExecuteUpdateAsync(set => set
                .SetProperty(g => g.RevokedAt, f.Clock.Now).SetProperty(g => g.RevokedById, s.SecretaryId));
        await Expect(await Verify(client, s, code), 404, "MSG371");
        await using var check = f.Db();
        Assert.False(await check.GuestSessions.AnyAsync(x => x.InvitationId == s.InvitationId));
    }

    [Fact]
    public async Task Mail_failure_leaves_no_code_and_allows_an_immediate_retry()
    {
        var s = await f.Seed();
        using var client = f.Host.CreateClient();
        f.Mail.Fail = true;
        try { await Expect(await client.PostAsJsonAsync(s.Url + "/code", new { token = s.LinkToken }), 503, "MSG374"); }
        finally { f.Mail.Fail = false; }
        await using (var db = f.Db())
            Assert.False(await db.GuestVerificationChallenges.AnyAsync(c => c.InvitationId == s.InvitationId));
        await Expect(await Verify(client, s, await f.SendCode(client, s)), 200);
    }

    [Fact]
    public async Task Persistence_failure_while_verifying_opens_no_session()
    {
        var s = await f.Seed();
        using var client = f.Host.CreateClient();
        var code = await f.SendCode(client, s);
        f.SaveFailure.Enabled = true;
        try { await Expect(await Verify(client, s, code), 503, "MSG374"); }
        finally { f.SaveFailure.Enabled = false; }
        await using (var db = f.Db())
            Assert.False(await db.GuestSessions.AnyAsync(x => x.InvitationId == s.InvitationId));
        await Expect(await Verify(client, s, code), 200);
    }
}
