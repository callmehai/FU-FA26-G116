using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using TrustCabinet.Domain.Authentication;
using TrustCabinet.Domain.Entities;
using TrustCabinet.Domain.Enums;

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

                services.RemoveAll<IRefreshTokenStore>();
                services.AddSingleton<IRefreshTokenStore>(RefreshTokens);

                // The real hasher is deliberately slow; each of these tests signs in at least once.
                services.RemoveAll<IPasswordHasher>();
                services.AddSingleton<IPasswordHasher, FakePasswordHasher>();
            });
        }
    }

    private HttpClient NewClient(
        SystemRole role = SystemRole.Secretary,
        bool isActive = true,
        Capability[]? capabilities = null)
    {
        _factory.Users
            .WithUser(new User
            {
                FullName = "Nguyễn Thư Ký",
                Email = Email,
                PasswordHash = new FakePasswordHasher().Hash(Password),
                Role = role,
                IsActive = isActive,
            })
            .WithCapabilities(role, capabilities ?? [Capability.MeetingCreator]);

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
        Assert.Equal("Secretary", body.User.Role);
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
    public async Task Login_answers_MSG04_when_the_role_grants_nothing()
    {
        var client = NewClient(role: SystemRole.Chair, capabilities: []);

        var response = await PostLogin(client, Email, Password);

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
        Assert.Equal("MSG04", (await response.Content.ReadFromJsonAsync<AuthErrorBody>())!.Code);
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

    private sealed record SignInBody(
        string AccessToken, string RefreshToken, DateTimeOffset AccessTokenExpiresAt, CurrentUserBody User);

    private sealed record CurrentUserBody(
        string Id, string Email, string FullName, string Role, string[] Capabilities);

    private sealed record AuthErrorBody(string Code, string Message);
}
