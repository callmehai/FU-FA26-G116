using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using Microsoft.AspNetCore.Hosting;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using TrustCabinet.Application.Audit;
using TrustCabinet.Application.Authentication;
using TrustCabinet.Application.Users;
using TrustCabinet.Domain.Access;
using TrustCabinet.Domain.Entities;
using TrustCabinet.Domain.Enums;
using TrustCabinet.Web.Tests.GuestAccess;
using TrustCabinet.Web.Tests.Users;

namespace TrustCabinet.Web.Tests.Authentication;

/// <summary>
/// Round 1 gap tests for FE-01 (Authentication, UC-01 to UC-04): the cases the report marked
/// "Manual only", plus the Expected lines the cited tests did not pin (message codes on replayed
/// or body-carried refresh tokens, the step after a reactivation, password sign-in with Google
/// switched off, and /api/auth/me for a deactivated account or a Guest token).
///
/// Same style as <see cref="AuthEndpointsTests"/>: fakes instead of PostgreSQL and Redis, the real
/// pipeline in between. The guest cases run against the shared <see cref="GuestAccessFixture"/>
/// (real PostgreSQL), in a second class in this file.
/// </summary>
public sealed class AuthRound1Tests : IDisposable
{
    private const string Password = "MatKhau@2026";
    private const string Email = "thuky@trustcabinet.local";

    private readonly Factory _factory = new();

    public void Dispose() => _factory.Dispose();

    private sealed class Factory : TrustCabinetApiFactory
    {
        internal bool GoogleEnabled { get; set; } = true;

        internal int SignInPerEmail { get; set; } = 100;

        internal int AuthPerIp { get; set; } = 100;

        internal FakeUserRepository Users { get; } = new();

        internal FakeUserAccountRepository Accounts { get; } = new();

        internal FakeGoogleTokenVerifier Google { get; } = new();

        internal FakeAuditService Audit { get; } = new();

        protected override void ConfigureWebHost(IWebHostBuilder builder)
        {
            base.ConfigureWebHost(builder);

            builder.UseSetting("Authentication:Google:Enabled", GoogleEnabled ? "true" : "false");
            builder.UseSetting("Authentication:Google:ClientId", "test-client-id.apps.googleusercontent.com");
            builder.UseSetting("RateLimiting:SignInPerEmail", SignInPerEmail.ToString());
            builder.UseSetting("RateLimiting:AuthPerIp", AuthPerIp.ToString());

            builder.ConfigureServices(services =>
            {
                services.RemoveAll<IUserRepository>();
                services.AddSingleton<IUserRepository>(Users);

                services.RemoveAll<IUserAccountRepository>();
                services.AddSingleton<IUserAccountRepository>(Accounts);

                services.RemoveAll<IRefreshTokenStore>();
                services.AddSingleton<IRefreshTokenStore>(new FakeRefreshTokenStore());

                services.RemoveAll<IGoogleTokenVerifier>();
                services.AddSingleton<IGoogleTokenVerifier>(Google);

                services.RemoveAll<IPasswordHasher>();
                services.AddSingleton<IPasswordHasher, FakePasswordHasher>();

                services.RemoveAll<IAuditService>();
                services.AddSingleton<IAuditService>(Audit);
            });
        }
    }

    private HttpClient NewClient(
        Guid? role = null,
        bool isActive = true,
        Capability[]? capabilities = null,
        string email = Email,
        string password = Password)
    {
        var user = new User
        {
            FullName = "Nguyễn Thư Ký",
            Email = email,
            PasswordHash = new FakePasswordHasher().Hash(password),
            RoleId = role ?? BuiltInRoles.Approver,
            IsActive = isActive,
        };

        _factory.Users
            .WithUser(user)
            .WithCapabilities(user.RoleId, capabilities ?? [Capability.MeetingCreator]);
        _factory.Accounts
            .WithUser(user)
            .WithCapabilities(user.RoleId, capabilities ?? [Capability.MeetingCreator]);

        return _factory.CreateClient();
    }

    private static Task<HttpResponseMessage> PostLogin(HttpClient client, string email, string password)
        => client.PostAsJsonAsync("/api/auth/login", new { email, password });

    private static async Task<string> SignInForRefreshToken(HttpClient client)
        => RefreshCookieValue(await PostLogin(client, Email, Password));

    private static Task<HttpResponseMessage> PostWithCookie(HttpClient client, string path, string refreshToken)
    {
        var request = new HttpRequestMessage(HttpMethod.Post, path);
        request.Headers.Add("Cookie", $"tc_refresh_token={Uri.EscapeDataString(refreshToken)}");
        return client.SendAsync(request);
    }

    private static string RefreshCookieValue(HttpResponseMessage response)
    {
        var header = Assert.Single(
            response.Headers.GetValues("Set-Cookie"),
            h => h.StartsWith("tc_refresh_token=", StringComparison.Ordinal));
        return Uri.UnescapeDataString(header["tc_refresh_token=".Length..header.IndexOf(';')]);
    }

    private static bool HasRefreshCookie(HttpResponseMessage response)
        => response.Headers.TryGetValues("Set-Cookie", out var values)
            && values.Any(h => h.StartsWith("tc_refresh_token=", StringComparison.Ordinal));

    private sealed record AuthErrorBody(string Code, string Message);

    private sealed record SignInBody(string AccessToken, DateTimeOffset AccessTokenExpiresAt, CurrentUserBody User);

    private sealed record CurrentUserBody(
        string Id, string Email, string FullName, string RoleId, string RoleName, string[] Capabilities);

    // AUTH_API_IT04: empty credentials take the MSG01 branch and set no cookie.
    [Fact]
    public async Task Login_rejects_empty_credentials_with_MSG01_and_sets_no_cookie()
    {
        var client = NewClient();

        var response = await PostLogin(client, "", "");

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
        var body = await response.Content.ReadFromJsonAsync<AuthErrorBody>();
        Assert.Equal("MSG01", body!.Code);
        Assert.Equal("Email hoặc mật khẩu không đúng.", body.Message);
        Assert.False(HasRefreshCookie(response));
    }

    // AUTH_API_IT03: an unknown email answers exactly like a wrong password, without a cookie.
    [Fact]
    public async Task Unknown_email_answers_exactly_like_a_wrong_password()
    {
        var client = NewClient();

        var unknown = await PostLogin(client, "khongtontai@trustcabinet.local", Password);
        var wrong = await PostLogin(client, Email, "SaiMatKhau@2026");

        Assert.Equal(HttpStatusCode.Unauthorized, unknown.StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized, wrong.StatusCode);
        var unknownBody = await unknown.Content.ReadFromJsonAsync<AuthErrorBody>();
        var wrongBody = await wrong.Content.ReadFromJsonAsync<AuthErrorBody>();
        Assert.Equal("MSG01", unknownBody!.Code);
        Assert.Equal(wrongBody!.Code, unknownBody.Code);
        Assert.Equal(wrongBody.Message, unknownBody.Message);
        Assert.False(HasRefreshCookie(unknown));
    }

    // AUTH_API_IT06: the password is checked before the account status, so a wrong password
    // answers MSG01 even for a deactivated account — only someone holding the password learns
    // the account is deactivated (MSG02).
    [Fact]
    public async Task Login_checks_the_password_before_the_account_status()
    {
        var client = NewClient(isActive: false);

        var wrongPassword = await PostLogin(client, Email, "SaiMatKhau@2026");
        Assert.Equal(HttpStatusCode.Unauthorized, wrongPassword.StatusCode);
        Assert.Equal("MSG01", (await wrongPassword.Content.ReadFromJsonAsync<AuthErrorBody>())!.Code);

        var rightPassword = await PostLogin(client, Email, Password);
        Assert.Equal(HttpStatusCode.Unauthorized, rightPassword.StatusCode);
        Assert.Equal("MSG02", (await rightPassword.Content.ReadFromJsonAsync<AuthErrorBody>())!.Code);
    }

    // AUTH_API_IT23: a capability change reaches the next refresh without signing in again.
    [Fact]
    public async Task Capability_change_reaches_the_next_refresh_without_signing_in_again()
    {
        var client = NewClient();
        var refreshToken = await SignInForRefreshToken(client);

        _factory.Users.WithCapabilities(BuiltInRoles.Approver);
        _factory.Accounts.WithCapabilities(BuiltInRoles.Approver);

        var response = await PostWithCookie(client, "/api/auth/refresh", refreshToken);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var body = await response.Content.ReadFromJsonAsync<SignInBody>();
        Assert.NotNull(body);
        Assert.False(string.IsNullOrWhiteSpace(body.AccessToken));
        Assert.Empty(body.User.Capabilities);
    }

    // AUTH_API_IT27: signing out on one device leaves the other devices signed in.
    [Fact]
    public async Task Logout_on_one_device_leaves_the_other_signed_in()
    {
        var client = NewClient();
        var cookieA = await SignInForRefreshToken(client);
        var cookieB = await SignInForRefreshToken(client);

        Assert.Equal(HttpStatusCode.NoContent, (await PostWithCookie(client, "/api/auth/logout", cookieA)).StatusCode);

        var stillAlive = await PostWithCookie(client, "/api/auth/refresh", cookieB);
        Assert.Equal(HttpStatusCode.OK, stillAlive.StatusCode);
        Assert.False(string.IsNullOrWhiteSpace((await stillAlive.Content.ReadFromJsonAsync<SignInBody>())!.AccessToken));

        Assert.Equal(HttpStatusCode.Unauthorized, (await PostWithCookie(client, "/api/auth/refresh", cookieA)).StatusCode);
    }

    // AUTH_API_IT30: /api/auth/me reads the account from the database on every request, so a
    // still-valid access token stops working once the account is deactivated — 401, not 403.
    [Fact]
    public async Task Me_refuses_a_deactivated_account_with_401()
    {
        var client = NewClient();
        var accessToken = (await (await PostLogin(client, Email, Password))
            .Content.ReadFromJsonAsync<SignInBody>())!.AccessToken;
        (await _factory.Users.FindByEmailAsync(Email))!.IsActive = false;

        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", accessToken);
        var response = await client.GetAsync("/api/auth/me");

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    // AUTH_API_IT16, step 2: after the reactivation the same Gmail signs in and links.
    [Fact]
    public async Task Google_sign_in_links_after_reactivation()
    {
        const string gmail = "moi@gmail.test";
        var user = new User
        {
            FullName = "Người Mới",
            Email = gmail,
            PasswordHash = null,
            GoogleSubjectId = null,
            RoleId = BuiltInRoles.Approver,
            IsActive = false,
        };
        _factory.Users.WithUser(user).WithCapabilities(user.RoleId, Capability.MeetingApprover);
        _factory.Google.WithValidToken("id-token-mot", "google-sub-1", gmail);

        var refused = await _factory.CreateClient().PostAsJsonAsync(
            "/api/auth/google", new { idToken = "id-token-mot" });
        Assert.Equal(HttpStatusCode.Unauthorized, refused.StatusCode);
        Assert.Equal("MSG02", (await refused.Content.ReadFromJsonAsync<AuthErrorBody>())!.Code);
        Assert.Null(user.GoogleSubjectId);

        user.IsActive = true;
        _factory.Google.WithValidToken("id-token-hai", "google-sub-1", gmail);

        var response = await _factory.CreateClient().PostAsJsonAsync(
            "/api/auth/google", new { idToken = "id-token-hai" });
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal("google-sub-1", user.GoogleSubjectId);
    }

    // AUTH_API_IT17, step 2: switching Google off removes only /google; password sign-in works.
    [Fact]
    public async Task Password_sign_in_still_works_when_google_is_switched_off()
    {
        using var factory = new Factory { GoogleEnabled = false };
        var user = new User
        {
            FullName = "Nguyễn Thư Ký",
            Email = Email,
            PasswordHash = new FakePasswordHasher().Hash(Password),
            RoleId = BuiltInRoles.Approver,
            IsActive = true,
        };
        factory.Users.WithUser(user).WithCapabilities(user.RoleId, Capability.MeetingCreator);
        factory.Accounts.WithUser(user).WithCapabilities(user.RoleId, Capability.MeetingCreator);

        var google = await factory.CreateClient().PostAsJsonAsync(
            "/api/auth/google", new { idToken = "bat-ky-gi" });
        Assert.Equal(HttpStatusCode.NotFound, google.StatusCode);

        var login = await factory.CreateClient().PostAsJsonAsync(
            "/api/auth/login", new { email = Email, password = Password });
        Assert.Equal(HttpStatusCode.OK, login.StatusCode);
    }

    // AUTH_API_IT18, step 3: replaying a spent refresh token answers MSG01.
    [Fact]
    public async Task Refresh_replay_answers_MSG01()
    {
        var client = NewClient();
        var refreshToken = await SignInForRefreshToken(client);

        var first = await PostWithCookie(client, "/api/auth/refresh", refreshToken);
        Assert.Equal(HttpStatusCode.OK, first.StatusCode);

        var replay = await PostWithCookie(client, "/api/auth/refresh", refreshToken);
        Assert.Equal(HttpStatusCode.Unauthorized, replay.StatusCode);
        Assert.Equal("MSG01", (await replay.Content.ReadFromJsonAsync<AuthErrorBody>())!.Code);
    }

    // AUTH_API_IT20: a refresh token sent in the body is ignored — 401 MSG01, cookie only.
    [Fact]
    public async Task Refresh_ignores_a_token_in_the_body_with_MSG01()
    {
        var client = NewClient();
        var refreshToken = await SignInForRefreshToken(client);

        var response = await client.PostAsJsonAsync("/api/auth/refresh", new { refreshToken });

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
        Assert.Equal("MSG01", (await response.Content.ReadFromJsonAsync<AuthErrorBody>())!.Code);
    }

    // AUTH_API_IT25, step 3: after the logout the same cookie refreshes to 401 MSG01.
    [Fact]
    public async Task Refresh_after_logout_answers_MSG01()
    {
        var client = NewClient();
        var refreshToken = await SignInForRefreshToken(client);

        var logout = await PostWithCookie(client, "/api/auth/logout", refreshToken);
        Assert.Equal(HttpStatusCode.NoContent, logout.StatusCode);

        var refresh = await PostWithCookie(client, "/api/auth/refresh", refreshToken);
        Assert.Equal(HttpStatusCode.Unauthorized, refresh.StatusCode);
        Assert.Equal("MSG01", (await refresh.Content.ReadFromJsonAsync<AuthErrorBody>())!.Code);
    }

    // AUTH_API_IT24: refresh and logout are not rate limited — ten chained refreshes stay 200
    // even with the per-IP budget down to one.
    [Fact]
    public async Task Refresh_stays_200_when_the_sign_in_limit_is_tight()
    {
        using var factory = new Factory { SignInPerEmail = 100, AuthPerIp = 1 };
        var user = new User
        {
            FullName = "Nguyễn Thư Ký",
            Email = Email,
            PasswordHash = new FakePasswordHasher().Hash(Password),
            RoleId = BuiltInRoles.Approver,
            IsActive = true,
        };
        factory.Users.WithUser(user).WithCapabilities(user.RoleId, Capability.MeetingCreator);
        factory.Accounts.WithUser(user).WithCapabilities(user.RoleId, Capability.MeetingCreator);
        var client = factory.CreateClient();

        var login = await PostLogin(client, Email, Password);
        Assert.Equal(HttpStatusCode.OK, login.StatusCode);
        var cookie = RefreshCookieValue(login);

        for (var i = 0; i < 10; i++)
        {
            var refresh = await PostWithCookie(client, "/api/auth/refresh", cookie);
            Assert.Equal(HttpStatusCode.OK, refresh.StatusCode);
            cookie = RefreshCookieValue(refresh);
        }
    }
}

/// <summary>
/// AUTH_API_IT21 over the real Redis store with a controllable clock: replaying a spent refresh
/// token past the 60-second grace period ends every session of the account (RT2 dies with RT1).
/// The in-memory fake cannot prove this — the reuse marker lives in the Redis store.
/// </summary>
public sealed class AuthReuseRound1Tests : IDisposable
{
    private sealed class Factory(TestClock Clock) : TrustCabinetApiFactory
    {
        internal FakeUserRepository Users { get; } = new();

        internal FakeUserAccountRepository Accounts { get; } = new();

        protected override void ConfigureWebHost(IWebHostBuilder builder)
        {
            base.ConfigureWebHost(builder);

            builder.ConfigureServices(services =>
            {
                services.RemoveAll<IUserRepository>();
                services.AddSingleton<IUserRepository>(Users);

                services.RemoveAll<IUserAccountRepository>();
                services.AddSingleton<IUserAccountRepository>(Accounts);

                services.RemoveAll<TimeProvider>();
                services.AddSingleton<TimeProvider>(Clock);

                services.RemoveAll<IPasswordHasher>();
                services.AddSingleton<IPasswordHasher, FakePasswordHasher>();

                services.RemoveAll<IAuditService>();
                services.AddSingleton<IAuditService, FakeAuditService>();
            });
        }
    }

    internal sealed class TestClock : TimeProvider
    {
        internal DateTimeOffset Now = DateTimeOffset.UtcNow;

        public override DateTimeOffset GetUtcNow() => Now;
    }

    private const string Password = "MatKhau@2026";
    private const string Email = "nguoidunglai@trustcabinet.local";

    private readonly TestClock _clock = new();
    private readonly Factory _factory;

    public AuthReuseRound1Tests() => _factory = new Factory(_clock);

    public void Dispose() => _factory.Dispose();

    private static string RefreshCookieValue(HttpResponseMessage response)
    {
        var header = Assert.Single(
            response.Headers.GetValues("Set-Cookie"),
            h => h.StartsWith("tc_refresh_token=", StringComparison.Ordinal));
        return Uri.UnescapeDataString(header["tc_refresh_token=".Length..header.IndexOf(';')]);
    }

    private static Task<HttpResponseMessage> PostWithCookie(HttpClient client, string path, string refreshToken)
    {
        var request = new HttpRequestMessage(HttpMethod.Post, path);
        request.Headers.Add("Cookie", $"tc_refresh_token={Uri.EscapeDataString(refreshToken)}");
        return client.SendAsync(request);
    }

    private sealed record AuthErrorBody(string Code, string Message);

    [Fact]
    public async Task Replaying_a_used_token_past_the_grace_period_ends_every_session()
    {
        var user = new User
        {
            FullName = "Người Dùng Lại",
            Email = Email,
            PasswordHash = new FakePasswordHasher().Hash(Password),
            RoleId = BuiltInRoles.Approver,
            IsActive = true,
        };
        _factory.Users.WithUser(user).WithCapabilities(user.RoleId, Capability.MeetingCreator);
        _factory.Accounts.WithUser(user).WithCapabilities(user.RoleId, Capability.MeetingCreator);
        var client = _factory.CreateClient();

        var login = await client.PostAsJsonAsync("/api/auth/login",
            new { email = Email, password = Password });
        Assert.Equal(HttpStatusCode.OK, login.StatusCode);
        var first = RefreshCookieValue(login);

        var rotated = await PostWithCookie(client, "/api/auth/refresh", first);
        Assert.Equal(HttpStatusCode.OK, rotated.StatusCode);
        var second = RefreshCookieValue(rotated);

        // Past the reuse grace period: presenting the spent token again reads as theft.
        _clock.Now = _clock.Now.AddMinutes(5);
        var replay = await PostWithCookie(client, "/api/auth/refresh", first);
        Assert.Equal(HttpStatusCode.Unauthorized, replay.StatusCode);
        Assert.Equal("MSG01", (await replay.Content.ReadFromJsonAsync<AuthErrorBody>())!.Code);

        // The session continued with the second token has ended too.
        var afterTheft = await PostWithCookie(client, "/api/auth/refresh", second);
        Assert.Equal(HttpStatusCode.Unauthorized, afterTheft.StatusCode);
        Assert.Equal("MSG01", (await afterTheft.Content.ReadFromJsonAsync<AuthErrorBody>())!.Code);
    }
}

/// <summary>
/// The FE-01 guest cases (UC-04) against the shared guest fixture: a Guest token at /api/auth/me,
/// a revoked invitation at open, malformed verification codes, and the self-declared profile.
/// </summary>
public sealed class AuthGuestRound1Tests(GuestAccessFixture f) : IClassFixture<GuestAccessFixture>
{
    // AUTH_API_IT31: a Guest token never opens an internal endpoint — 401, not 403 or 200.
    [Fact]
    public async Task Me_refuses_a_guest_token()
    {
        var s = await f.Seed();
        var (_, token) = await f.SignIn(s);

        using var caller = f.Host.CreateClient();
        caller.DefaultRequestHeaders.Authorization = new("Bearer", token);

        Assert.Equal(HttpStatusCode.Unauthorized, (await caller.GetAsync("/api/auth/me")).StatusCode);
    }

    // AUTH_API_IT35: opening an invitation the Secretary has since revoked is refused (MSG371),
    // and no session is created.
    [Fact]
    public async Task Revoked_invitation_is_refused_at_open()
    {
        var s = await f.Seed();
        await using (var db = f.Db())
            await db.MeetingGuestInvitations.Where(g => g.Id == s.InvitationId).ExecuteUpdateAsync(set => set
                .SetProperty(g => g.RevokedAt, f.Clock.Now).SetProperty(g => g.RevokedById, s.SecretaryId));

        using var client = f.Host.CreateClient();
        await GuestAccessFixture.Expect(
            await client.PostAsJsonAsync(s.Url, new { token = s.LinkToken }), 404, "MSG371");

        await using var check = f.Db();
        Assert.False(await check.GuestSessions.AnyAsync(x => x.InvitationId == s.InvitationId));
    }

    // AUTH_API_IT43: a code that is not 6 digits is refused with MSG126, before any guess counting.
    [Fact]
    public async Task Verify_refuses_malformed_codes()
    {
        var s = await f.Seed();
        using var client = f.Host.CreateClient();
        await f.SendCode(client, s);

        await GuestAccessFixture.Expect(
            await client.PostAsJsonAsync(s.Url + "/verify", new { token = s.LinkToken, code = "12ab" }),
            400, "MSG126");
        await GuestAccessFixture.Expect(
            await client.PostAsJsonAsync(s.Url + "/verify", new { token = s.LinkToken, code = "1234567" }),
            400, "MSG126");
    }

    // AUTH_API_IT53: a blank display name is rejected and the profile stays incomplete.
    [Fact]
    public async Task Guest_profile_rejects_a_blank_display_name()
    {
        var s = await f.Seed();
        var (client, _) = await f.SignIn(s, profile: false);

        var refused = await GuestAccessFixture.Expect(
            await client.PutAsJsonAsync("/api/guest-access/session/profile",
                new { displayName = "   ", jobTitle = "" }),
            400, "MSG377");
        Assert.True(refused.GetProperty("errors").TryGetProperty("displayName", out _));

        var session = await GuestAccessFixture.Expect(await client.GetAsync("/api/guest-access/session"), 200);
        Assert.False(session.GetProperty("profileCompleted").GetBoolean());
    }

    // AUTH_API_IT54: over-long and control-character names are rejected, on either field.
    [Fact]
    public async Task Guest_profile_rejects_overlong_and_control_character_values()
    {
        var s = await f.Seed();
        var (client, _) = await f.SignIn(s, profile: false);

        var tooLong = await GuestAccessFixture.Expect(
            await client.PutAsJsonAsync("/api/guest-access/session/profile",
                new { displayName = new string('K', 201) }),
            400, "MSG377");
        Assert.True(tooLong.GetProperty("errors").TryGetProperty("displayName", out _));

        var control = await GuestAccessFixture.Expect(
            await client.PutAsJsonAsync("/api/guest-access/session/profile",
                new { displayName = "Khách\nmới" }),
            400, "MSG377");
        Assert.True(control.GetProperty("errors").TryGetProperty("displayName", out _));

        var jobTooLong = await GuestAccessFixture.Expect(
            await client.PutAsJsonAsync("/api/guest-access/session/profile",
                new { displayName = "Khách hợp lệ", jobTitle = new string('C', 201) }),
            400, "MSG377");
        Assert.True(jobTooLong.GetProperty("errors").TryGetProperty("jobTitle", out _));

        var session = await GuestAccessFixture.Expect(await client.GetAsync("/api/guest-access/session"), 200);
        Assert.False(session.GetProperty("profileCompleted").GetBoolean());
    }

    // AUTH_API_IT41: the verify answer says the profile is still missing, with the expiry and the
    // meeting, and no internal user account is created for the Guest.
    [Fact]
    public async Task Verify_response_says_the_profile_is_missing()
    {
        var s = await f.Seed();
        await using (var before = f.Db())
        {
            var users = await before.Users.CountAsync();
            using var client = f.Host.CreateClient();
            var code = await f.SendCode(client, s);
            var verified = await GuestAccessFixture.Expect(
                await client.PostAsJsonAsync(s.Url + "/verify", new { token = s.LinkToken, code }), 200);
            Assert.False(verified.GetProperty("profileCompleted").GetBoolean());
            Assert.Equal(s.MeetingId, verified.GetProperty("meetingId").GetGuid());
            Assert.Equal(
                f.Clock.Now.AddHours(12).ToUnixTimeSeconds(),
                verified.GetProperty("expiresAt").GetDateTimeOffset().ToUnixTimeSeconds());
            Assert.Equal(users, await before.Users.CountAsync());
        }
    }
}
