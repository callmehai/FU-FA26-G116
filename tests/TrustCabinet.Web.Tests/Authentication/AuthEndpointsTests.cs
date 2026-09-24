using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using TrustCabinet.Domain.Access;
using TrustCabinet.Domain.Authentication;
using TrustCabinet.Domain.Entities;
using TrustCabinet.Domain.Enums;
using TrustCabinet.Domain.Users;
using TrustCabinet.Web.Tests.Users;

namespace TrustCabinet.Web.Tests.Authentication;

/// <summary>
/// The endpoints over real HTTP — status codes, message codes and the shape of the JSON, which is
/// the contract the frontend will build against.
///
/// The repository and the token store are replaced with in-memory fakes, so these run with no
/// PostgreSQL and no Redis. What is exercised for real is everything in between: routing, model
/// binding, the JWT middleware and the controller's mapping of outcomes to responses.
///
/// One factory per test rather than a shared IClassFixture: each test seeds the same email with a
/// different account, so a shared repository would let one test's active user answer another
/// test's lookup for a deactivated one.
/// </summary>
public class AuthEndpointsTests : IDisposable
{
    private const string Password = "MatKhau@2026";
    private const string Email = "thuky@trustcabinet.local";

    private readonly Factory _factory = new();

    public void Dispose() => _factory.Dispose();

    private sealed class Factory : TrustCabinetApiFactory
    {
        internal FakeUserRepository Users { get; } = new();

        /// <summary>
        /// GET /api/auth/me reads the caller back from here rather than from the token's claims
        /// (SCRUM-12), so it needs the account repository too. Seeded with the same
        /// <see cref="User"/> instance as <see cref="Users"/>, so the two cannot disagree about
        /// who is signed in.
        /// </summary>
        internal FakeUserAccountRepository Accounts { get; } = new();

        internal FakeRefreshTokenStore RefreshTokens { get; } = new();

        protected override void ConfigureWebHost(IWebHostBuilder builder)
        {
            base.ConfigureWebHost(builder);

            builder.ConfigureServices(services =>
            {
                // The real UserRepository needs a database and the real store needs Redis; the
                // rest of the pipeline is what these tests are about.
                services.RemoveAll<IUserRepository>();
                services.AddSingleton<IUserRepository>(Users);

                services.RemoveAll<IUserAccountRepository>();
                services.AddSingleton<IUserAccountRepository>(Accounts);

                services.RemoveAll<IRefreshTokenStore>();
                services.AddSingleton<IRefreshTokenStore>(RefreshTokens);

                // The real hasher is deliberately slow; each of these tests signs in at least once.
                services.RemoveAll<IPasswordHasher>();
                services.AddSingleton<IPasswordHasher, FakePasswordHasher>();
            });
        }
    }

    private HttpClient NewClient(
        Guid? role = null,
        bool isActive = true,
        Capability[]? capabilities = null)
    {
        var user = new User
        {
            FullName = "Nguyễn Thư Ký",
            Email = Email,
            PasswordHash = new FakePasswordHasher().Hash(Password),
            RoleId = role ?? BuiltInRoles.Secretary,
            IsActive = isActive,
        };

        _factory.Users
            .WithUser(user)
            .WithCapabilities(user.RoleId, capabilities ?? [Capability.MeetingCreator]);

        // The same instance in both fakes: sign-in resolves the caller through one, /api/auth/me
        // reads them back through the other.
        _factory.Accounts
            .WithUser(user)
            .WithCapabilities(user.RoleId, capabilities ?? [Capability.MeetingCreator]);

        return _factory.CreateClient();
    }

    private static Task<HttpResponseMessage> PostLogin(HttpClient client, string email, string password)
        => client.PostAsJsonAsync("/api/auth/login", new { email, password });

    [Fact]
    public async Task Login_returns_a_token_pair_and_the_user()
    {
        var client = NewClient();

        var response = await PostLogin(client, Email, Password);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var body = await response.Content.ReadFromJsonAsync<SignInBody>();
        Assert.NotNull(body);
        Assert.False(string.IsNullOrWhiteSpace(body.AccessToken));
        Assert.False(string.IsNullOrWhiteSpace(body.RefreshToken));
        Assert.Equal(Email, body.User.Email);
        // The role by id and by its current name (ADR-0008), never the old enum string.
        Assert.Equal(BuiltInRoles.Secretary.ToString(), body.User.RoleId);
        Assert.Equal("Thư ký", body.User.RoleName);
        Assert.Equal(["MeetingCreator"], body.User.Capabilities);
    }

    [Fact]
    public async Task Login_never_returns_the_password_hash()
    {
        var client = NewClient();

        var raw = await (await PostLogin(client, Email, Password)).Content.ReadAsStringAsync();

        Assert.DoesNotContain("password", raw, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain(Password, raw);
    }

    [Theory]
    [InlineData("khong-ton-tai@trustcabinet.local", Password)]
    [InlineData(Email, "SaiMatKhau@2026")]
    [InlineData("", "")]
    public async Task Login_answers_MSG01_for_bad_credentials(string email, string password)
    {
        var client = NewClient();

        var response = await PostLogin(client, email, password);

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);

        var body = await response.Content.ReadFromJsonAsync<AuthErrorBody>();
        // One code for every kind of bad credential — the response must not say which part failed.
        Assert.Equal("MSG01", body!.Code);
    }

    [Fact]
    public async Task Login_answers_MSG03_for_a_deactivated_account()
    {
        var client = NewClient(isActive: false);

        var response = await PostLogin(client, Email, Password);

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
        Assert.Equal("MSG03", (await response.Content.ReadFromJsonAsync<AuthErrorBody>())!.Code);
    }

    [Fact]
    public async Task Login_succeeds_when_the_role_grants_nothing()
    {
        // MSG04 is gone (SCRUM-63): a role with no capabilities works like Member — attending,
        // reading and voting need none — so its holders sign in rather than being locked out.
        var client = NewClient(role: BuiltInRoles.Chair, capabilities: []);

        var response = await PostLogin(client, Email, Password);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Empty((await response.Content.ReadFromJsonAsync<SignInBody>())!.User.Capabilities);
    }

    [Fact]
    public async Task Every_rejection_carries_a_vietnamese_message()
    {
        var client = NewClient();

        var body = await (await PostLogin(client, Email, "SaiMatKhau@2026"))
            .Content.ReadFromJsonAsync<AuthErrorBody>();

        // UI strings are Vietnamese — the endpoint is what produces them for now.
        Assert.Equal("Email hoặc mật khẩu không đúng.", body!.Message);
    }

    [Fact]
    public async Task Me_returns_the_signed_in_user()
    {
        var client = NewClient();
        var signIn = await (await PostLogin(client, Email, Password)).Content.ReadFromJsonAsync<SignInBody>();

        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", signIn!.AccessToken);
        var response = await client.GetAsync("/api/auth/me");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var me = await response.Content.ReadFromJsonAsync<CurrentUserBody>();
        Assert.Equal(Email, me!.Email);
        Assert.Equal(BuiltInRoles.Secretary.ToString(), me.RoleId);
        Assert.Equal("Thư ký", me.RoleName);
        Assert.Equal(["MeetingCreator"], me.Capabilities);
    }

    [Fact]
    public async Task Me_rejects_a_request_with_no_token()
    {
        var response = await _factory.CreateClient().GetAsync("/api/auth/me");

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Theory]
    [InlineData("khong-phai-token")]
    [InlineData("eyJhbGciOiJIUzI1NiJ9.eyJzdWIiOiJrZSB0cm9tIn0.giaMaoChuKy")]
    public async Task Me_rejects_a_forged_token(string token)
    {
        var client = _factory.CreateClient();
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);

        var response = await client.GetAsync("/api/auth/me");

        // The signature is checked, so a token nobody signed with our key is worthless.
        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task Refresh_exchanges_the_token_for_a_new_pair()
    {
        var client = NewClient();
        var signIn = await (await PostLogin(client, Email, Password)).Content.ReadFromJsonAsync<SignInBody>();

        var response = await client.PostAsJsonAsync("/api/auth/refresh",
            new { refreshToken = signIn!.RefreshToken });

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var refreshed = await response.Content.ReadFromJsonAsync<SignInBody>();
        Assert.NotEqual(signIn.RefreshToken, refreshed!.RefreshToken);
    }

    [Fact]
    public async Task Refresh_rejects_a_token_that_was_already_used()
    {
        var client = NewClient();
        var signIn = await (await PostLogin(client, Email, Password)).Content.ReadFromJsonAsync<SignInBody>();

        await client.PostAsJsonAsync("/api/auth/refresh", new { refreshToken = signIn!.RefreshToken });
        var replay = await client.PostAsJsonAsync("/api/auth/refresh", new { refreshToken = signIn.RefreshToken });

        Assert.Equal(HttpStatusCode.Unauthorized, replay.StatusCode);
    }

    [Fact]
    public async Task Logout_invalidates_the_refresh_token_server_side()
    {
        var client = NewClient();
        var signIn = await (await PostLogin(client, Email, Password)).Content.ReadFromJsonAsync<SignInBody>();

        var logout = await client.PostAsJsonAsync("/api/auth/logout",
            new { refreshToken = signIn!.RefreshToken });

        Assert.Equal(HttpStatusCode.NoContent, logout.StatusCode);

        // The acceptance criterion: a client that kept the token cannot resume the session.
        var afterLogout = await client.PostAsJsonAsync("/api/auth/refresh",
            new { refreshToken = signIn.RefreshToken });

        Assert.Equal(HttpStatusCode.Unauthorized, afterLogout.StatusCode);
    }

    [Fact]
    public async Task Logout_is_idempotent()
    {
        var client = NewClient();
        var signIn = await (await PostLogin(client, Email, Password)).Content.ReadFromJsonAsync<SignInBody>();

        await client.PostAsJsonAsync("/api/auth/logout", new { refreshToken = signIn!.RefreshToken });
        var again = await client.PostAsJsonAsync("/api/auth/logout", new { refreshToken = signIn.RefreshToken });

        // Signing out twice is not an error, and the second call must not reveal that the token
        // had already gone.
        Assert.Equal(HttpStatusCode.NoContent, again.StatusCode);
    }

    /// <summary>
    /// The one Set-Cookie header for the refresh token cookie, lower-cased for attribute checks.
    /// </summary>
    private static string RefreshCookieHeader(HttpResponseMessage response)
        => Assert.Single(
                response.Headers.GetValues("Set-Cookie"),
                h => h.StartsWith("tc_refresh_token=", StringComparison.Ordinal))
            .ToLowerInvariant();

    /// <summary>
    /// A POST carrying the cookie by hand. The test client talks plain http, over which a real
    /// cookie jar would never send a Secure cookie, so it cannot be relied on to do it here.
    /// </summary>
    private static Task<HttpResponseMessage> PostWithCookie(
        HttpClient client, string path, string refreshToken, object? body = null)
    {
        var request = new HttpRequestMessage(HttpMethod.Post, path)
        {
            Content = body is null ? null : JsonContent.Create(body),
        };
        request.Headers.Add("Cookie", $"tc_refresh_token={refreshToken}");
        return client.SendAsync(request);
    }

    [Fact]
    public async Task Login_sets_the_refresh_token_as_an_HttpOnly_cookie()
    {
        var client = NewClient();

        var response = await PostLogin(client, Email, Password);
        var body = await response.Content.ReadFromJsonAsync<SignInBody>();
        var cookie = RefreshCookieHeader(response);

        // Same token in both places until the frontend stops reading the body (step 3).
        Assert.StartsWith($"tc_refresh_token={body!.RefreshToken}".ToLowerInvariant(), cookie);
        Assert.Contains("httponly", cookie);
        Assert.Contains("secure", cookie);
        Assert.Contains("samesite=strict", cookie);
        Assert.Contains("path=/api/auth", cookie);
        Assert.Contains("max-age=604800", cookie); // seven days, the refresh token's lifetime
    }

    [Fact]
    public async Task Refresh_reads_the_token_from_the_cookie_with_no_body_at_all()
    {
        var client = NewClient();
        var signIn = await (await PostLogin(client, Email, Password)).Content.ReadFromJsonAsync<SignInBody>();

        var response = await PostWithCookie(client, "/api/auth/refresh", signIn!.RefreshToken);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var refreshed = await response.Content.ReadFromJsonAsync<SignInBody>();
        // The rotated token replaces the cookie, not just the body.
        Assert.StartsWith(
            $"tc_refresh_token={refreshed!.RefreshToken}".ToLowerInvariant(), RefreshCookieHeader(response));
    }

    [Fact]
    public async Task Refresh_prefers_the_cookie_over_the_body()
    {
        var client = NewClient();
        var signIn = await (await PostLogin(client, Email, Password)).Content.ReadFromJsonAsync<SignInBody>();

        var response = await PostWithCookie(
            client, "/api/auth/refresh", signIn!.RefreshToken, new { refreshToken = "khong-phai-token" });

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    [Fact]
    public async Task Logout_through_the_cookie_invalidates_the_token_and_clears_the_cookie()
    {
        var client = NewClient();
        var signIn = await (await PostLogin(client, Email, Password)).Content.ReadFromJsonAsync<SignInBody>();

        var logout = await PostWithCookie(client, "/api/auth/logout", signIn!.RefreshToken);

        Assert.Equal(HttpStatusCode.NoContent, logout.StatusCode);
        Assert.Contains("max-age=0", RefreshCookieHeader(logout));

        var afterLogout = await PostWithCookie(client, "/api/auth/refresh", signIn.RefreshToken);
        Assert.Equal(HttpStatusCode.Unauthorized, afterLogout.StatusCode);
    }

    [Fact]
    public async Task Logout_clears_the_cookie_even_with_no_valid_token()
    {
        var logout = await _factory.CreateClient().PostAsync("/api/auth/logout", content: null);

        Assert.Equal(HttpStatusCode.NoContent, logout.StatusCode);
        Assert.Contains("max-age=0", RefreshCookieHeader(logout));
    }

    private sealed record SignInBody(
        string AccessToken, string RefreshToken, DateTimeOffset AccessTokenExpiresAt, CurrentUserBody User);

    private sealed record CurrentUserBody(
        string Id, string Email, string FullName, string RoleId, string RoleName, string[] Capabilities);

    private sealed record AuthErrorBody(string Code, string Message);
}
