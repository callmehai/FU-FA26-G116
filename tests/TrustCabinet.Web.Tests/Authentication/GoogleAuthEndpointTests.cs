using System.Net;
using System.Net.Http.Json;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using TrustCabinet.Domain.Audit;
using TrustCabinet.Domain.Authentication;
using TrustCabinet.Domain.Entities;
using TrustCabinet.Domain.Enums;
using TrustCabinet.Web.Tests.Users;

namespace TrustCabinet.Web.Tests.Authentication;

/// <summary>
/// POST /api/auth/google over real HTTP: the status codes and the message codes UC-02 specifies,
/// which are what the tester checks and what the login page will branch on.
/// </summary>
public class GoogleAuthEndpointTests : IDisposable
{
    private const string IdToken = "id-token-hop-le";
    private const string GoogleSubject = "115742-google-sub";
    private const string Email = "thuky@trustcabinet.local";

    private readonly Factory _factory = new();

    public void Dispose() => _factory.Dispose();

    private sealed class Factory : TrustCabinetApiFactory
    {
        /// <summary>Whether the built host offers Google sign-in. Set before the first request.</summary>
        internal bool GoogleEnabled { get; set; } = true;

        internal FakeUserRepository Users { get; } = new();

        internal FakeGoogleTokenVerifier Google { get; } = new();

        internal FakeAuditService Audit { get; } = new();

        protected override void ConfigureWebHost(IWebHostBuilder builder)
        {
            base.ConfigureWebHost(builder);

            builder.UseSetting("Authentication:Google:Enabled", GoogleEnabled ? "true" : "false");
            // Required whenever the feature is on; any non-empty value will do, since the real
            // verifier — the only thing that would check it against Google — is replaced below.
            builder.UseSetting("Authentication:Google:ClientId", "test-client-id.apps.googleusercontent.com");

            builder.ConfigureServices(services =>
            {
                services.RemoveAll<IUserRepository>();
                services.AddSingleton<IUserRepository>(Users);

                services.RemoveAll<IRefreshTokenStore>();
                services.AddSingleton<IRefreshTokenStore, FakeRefreshTokenStore>();

                // Verifying a real ID token would mean calling Google and holding a genuinely
                // signed token; what these tests cover is what happens after verification.
                services.RemoveAll<IGoogleTokenVerifier>();
                services.AddSingleton<IGoogleTokenVerifier>(Google);

                // The real one writes to PostgreSQL, which these tests do not have.
                services.RemoveAll<IAuditService>();
                services.AddSingleton<IAuditService>(Audit);
            });
        }
    }

    private static User NewGoogleUser(
        string? googleSubjectId = GoogleSubject,
        SystemRole role = SystemRole.Secretary,
        bool isActive = true) => new()
        {
            FullName = "Nguyễn Thư Ký",
            Email = Email,
            PasswordHash = null,
            GoogleSubjectId = googleSubjectId,
            Role = role,
            IsActive = isActive,
        };

    private Task<HttpResponseMessage> PostGoogle(string idToken = IdToken)
        => _factory.CreateClient().PostAsJsonAsync("/api/auth/google", new { idToken });

    [Fact]
    public async Task A_linked_active_account_gets_a_token_pair()
    {
        _factory.Google.WithValidToken(IdToken, GoogleSubject, Email);
        _factory.Users.WithUser(NewGoogleUser()).WithCapabilities(SystemRole.Secretary, Capability.MinutesEditor);

        var response = await PostGoogle();

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var body = await response.Content.ReadFromJsonAsync<SignInBody>();
        Assert.False(string.IsNullOrWhiteSpace(body!.AccessToken));
        Assert.False(string.IsNullOrWhiteSpace(body.RefreshToken));
        Assert.Equal(Email, body.User.Email);
        Assert.Equal(["MinutesEditor"], body.User.Capabilities);
    }

    [Fact]
    public async Task The_response_shape_matches_password_sign_in()
    {
        _factory.Google.WithValidToken(IdToken, GoogleSubject, Email);
        _factory.Users.WithUser(NewGoogleUser()).WithCapabilities(SystemRole.Secretary, Capability.MinutesEditor);

        var raw = await (await PostGoogle()).Content.ReadAsStringAsync();

        // The client should not have to care which way the session was established.
        foreach (var field in new[] { "accessToken", "refreshToken", "accessTokenExpiresAt", "user" })
        {
            Assert.Contains($"\"{field}\"", raw);
        }
    }

    [Theory]
    [InlineData("")]
    [InlineData("khong-phai-id-token")]
    public async Task An_unverifiable_token_answers_MSG01(string idToken)
    {
        _factory.Google.WithValidToken(IdToken, GoogleSubject, Email);
        _factory.Users.WithUser(NewGoogleUser()).WithCapabilities(SystemRole.Secretary, Capability.MinutesEditor);

        var response = await PostGoogle(idToken);

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
        Assert.Equal("MSG01", (await response.Content.ReadFromJsonAsync<AuthErrorBody>())!.Code);
    }

    [Fact]
    public async Task An_unlinked_google_account_answers_MSG03()
    {
        _factory.Google.WithValidToken(IdToken, "sub-chua-lien-ket", "nguoi.la@gmail.com");

        var response = await PostGoogle();

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
        Assert.Equal("MSG03", (await response.Content.ReadFromJsonAsync<AuthErrorBody>())!.Code);
    }

    [Fact]
    public async Task An_account_an_administrator_created_with_that_gmail_signs_in_without_manual_linking()
    {
        // The SCRUM-11 bug: an account made through POST /api/users has no google_subject_id, and
        // nothing but SQL could set one, so this answered MSG03 for everybody.
        _factory.Google.WithValidToken(IdToken, GoogleSubject, Email);
        _factory.Users.WithUser(NewGoogleUser(googleSubjectId: null))
            .WithCapabilities(SystemRole.Secretary, Capability.MinutesEditor);

        var response = await PostGoogle();

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal(AuditActions.UserGoogleLinked, Assert.Single(_factory.Audit.Records).Action);
    }

    [Fact]
    public async Task A_deactivated_account_answers_MSG04()
    {
        _factory.Google.WithValidToken(IdToken, GoogleSubject, Email);
        _factory.Users.WithUser(NewGoogleUser(isActive: false))
            .WithCapabilities(SystemRole.Secretary, Capability.MinutesEditor);

        var response = await PostGoogle();

        // MSG04, not MSG03: UC-02 numbers this differently from UC-01. Changing it to match the
        // password flow would break the acceptance criteria for this use case.
        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
        Assert.Equal("MSG04", (await response.Content.ReadFromJsonAsync<AuthErrorBody>())!.Code);
    }

    [Fact]
    public async Task An_account_deactivated_before_its_first_google_sign_in_answers_MSG04()
    {
        _factory.Google.WithValidToken(IdToken, GoogleSubject, Email);
        _factory.Users.WithUser(NewGoogleUser(googleSubjectId: null, isActive: false))
            .WithCapabilities(SystemRole.Secretary, Capability.MinutesEditor);

        var response = await PostGoogle();

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
        var body = await response.Content.ReadFromJsonAsync<AuthErrorBody>();
        Assert.Equal("MSG04", body!.Code);
        Assert.Equal("Tài khoản không khả dụng. Vui lòng liên hệ quản trị viên.", body.Message);
    }

    [Fact]
    public async Task A_role_granting_nothing_answers_MSG05()
    {
        _factory.Google.WithValidToken(IdToken, GoogleSubject, Email);
        _factory.Users.WithUser(NewGoogleUser());

        var response = await PostGoogle();

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
        Assert.Equal("MSG05", (await response.Content.ReadFromJsonAsync<AuthErrorBody>())!.Code);
    }

    [Fact]
    public async Task Every_rejection_carries_a_vietnamese_message()
    {
        _factory.Google.WithValidToken(IdToken, "sub-chua-lien-ket", "nguoi.la@gmail.com");

        var body = await (await PostGoogle()).Content.ReadFromJsonAsync<AuthErrorBody>();

        Assert.Equal("Tài khoản Google này chưa được cấp quyền truy cập hệ thống.", body!.Message);
    }

    [Fact]
    public async Task The_endpoint_is_absent_when_google_sign_in_is_switched_off()
    {
        _factory.GoogleEnabled = false;
        _factory.Google.WithValidToken(IdToken, GoogleSubject, Email);
        _factory.Users.WithUser(NewGoogleUser()).WithCapabilities(SystemRole.Secretary, Capability.MinutesEditor);

        var response = await PostGoogle();

        // 404, not 403 or 501: a feature that is not offered should look like a URL that does not
        // exist, the same reasoning as RequireCapability rendering a 404. A valid token must not
        // succeed here either.
        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    private sealed record SignInBody(
        string AccessToken, string RefreshToken, DateTimeOffset AccessTokenExpiresAt, CurrentUserBody User);

    private sealed record CurrentUserBody(
        string Id, string Email, string FullName, string Role, string[] Capabilities);

    private sealed record AuthErrorBody(string Code, string Message);
}
