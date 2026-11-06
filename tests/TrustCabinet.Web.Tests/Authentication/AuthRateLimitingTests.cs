using System.Net;
using System.Net.Http.Json;
using Microsoft.AspNetCore.Hosting;
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
/// SCRUM-132 over real HTTP: the per-email cap on /login, the per-IP cap shared by /login, /google
/// and /password-reset, and the window that lets a caller back in. In-memory TestServer requests
/// have no remote address, so every request here is the same "IP".
/// </summary>
public class AuthRateLimitingTests
{
    private const string Password = "MatKhau@2026";
    private const string Email = "thuky@trustcabinet.local";

    private sealed class Factory(int perEmail, int perIp, int windowSeconds = 60) : TrustCabinetApiFactory
    {
        protected override void ConfigureWebHost(IWebHostBuilder builder)
        {
            base.ConfigureWebHost(builder);
            builder.UseSetting("RateLimiting:SignInPerEmail", perEmail.ToString());
            builder.UseSetting("RateLimiting:AuthPerIp", perIp.ToString());
            builder.UseSetting("RateLimiting:WindowSeconds", windowSeconds.ToString());

            var user = new User
            {
                FullName = "Nguyễn Thư Ký",
                Email = Email,
                PasswordHash = new FakePasswordHasher().Hash(Password),
                RoleId = BuiltInRoles.Secretary,
                IsActive = true,
            };
            builder.ConfigureServices(services =>
            {
                services.RemoveAll<IUserRepository>();
                services.AddSingleton<IUserRepository>(new FakeUserRepository().WithUser(user)
                    .WithCapabilities(user.RoleId, Capability.MeetingCreator));
                services.RemoveAll<IUserAccountRepository>();
                services.AddSingleton<IUserAccountRepository>(new FakeUserAccountRepository().WithUser(user));
                services.RemoveAll<IRefreshTokenStore>();
                services.AddSingleton<IRefreshTokenStore>(new FakeRefreshTokenStore());
                services.RemoveAll<IPasswordHasher>();
                services.AddSingleton<IPasswordHasher, FakePasswordHasher>();
            });
        }
    }

    private static Task<HttpResponseMessage> PostLogin(HttpClient client, string email, string password)
        => client.PostAsJsonAsync("/api/auth/login", new { email, password });

    private static async Task AssertTooManyAttempts(HttpResponseMessage response)
    {
        Assert.Equal(HttpStatusCode.TooManyRequests, response.StatusCode);
        var body = await response.Content.ReadFromJsonAsync<AuthErrorBody>();
        Assert.Equal("MSG406", body!.Code);
        Assert.Equal("Bạn đã thử quá nhiều lần. Vui lòng thử lại sau ít phút.", body.Message);
        Assert.True(response.Headers.RetryAfter?.Delta > TimeSpan.Zero);
    }

    [Fact]
    public async Task Login_past_the_email_limit_answers_429_MSG406_even_with_the_right_password()
    {
        using var factory = new Factory(perEmail: 3, perIp: 100);
        var client = factory.CreateClient();

        for (var i = 0; i < 3; i++)
        {
            Assert.Equal(HttpStatusCode.Unauthorized, (await PostLogin(client, Email, "SaiMatKhau@2026")).StatusCode);
        }

        // Past the limit the password is not even checked, so a correct guess is not revealed.
        await AssertTooManyAttempts(await PostLogin(client, Email, Password));
        // Case and spaces do not open a fresh count for the same account.
        await AssertTooManyAttempts(await PostLogin(client, "  THUKY@trustcabinet.local ", Password));
        // Another account has a count of its own.
        Assert.Equal(
            HttpStatusCode.Unauthorized,
            (await PostLogin(client, "khac@trustcabinet.local", Password)).StatusCode);
    }

    [Fact]
    public async Task The_ip_limit_is_shared_by_login_google_and_password_reset()
    {
        using var factory = new Factory(perEmail: 100, perIp: 3);
        var client = factory.CreateClient();

        // Different emails each time: only the IP limit can stop these.
        for (var i = 0; i < 3; i++)
        {
            Assert.Equal(
                HttpStatusCode.Unauthorized,
                (await PostLogin(client, $"khach{i}@trustcabinet.local", Password)).StatusCode);
        }

        await AssertTooManyAttempts(await PostLogin(client, "khach9@trustcabinet.local", Password));
        await AssertTooManyAttempts(await client.PostAsJsonAsync("/api/auth/google", new { idToken = "x" }));
        await AssertTooManyAttempts(await client.PostAsJsonAsync(
            "/api/auth/password-reset", new { token = "x", newPassword = "MatKhauMoi@2026" }));
    }

    [Fact]
    public async Task Login_works_again_once_the_window_has_passed()
    {
        using var factory = new Factory(perEmail: 1, perIp: 100, windowSeconds: 1);
        var client = factory.CreateClient();

        Assert.Equal(HttpStatusCode.OK, (await PostLogin(client, Email, Password)).StatusCode);
        await AssertTooManyAttempts(await PostLogin(client, Email, Password));

        await Task.Delay(TimeSpan.FromSeconds(2.2));

        Assert.Equal(HttpStatusCode.OK, (await PostLogin(client, Email, Password)).StatusCode);
    }

    [Fact]
    public async Task Refresh_and_logout_are_not_limited()
    {
        // Every page load refreshes: capping it would sign people out of an open app.
        using var factory = new Factory(perEmail: 100, perIp: 1);
        var client = factory.CreateClient();

        for (var i = 0; i < 3; i++)
        {
            Assert.NotEqual(HttpStatusCode.TooManyRequests, (await client.PostAsync("/api/auth/refresh", null)).StatusCode);
            Assert.NotEqual(HttpStatusCode.TooManyRequests, (await client.PostAsync("/api/auth/logout", null)).StatusCode);
        }
    }

    private sealed record AuthErrorBody(string Code, string Message);
}
