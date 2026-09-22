using System.Net;
using System.Net.Http.Headers;
using System.Text.Json;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using TrustCabinet.Domain.Access;
using TrustCabinet.Domain.Audit;
using TrustCabinet.Domain.Authentication;
using TrustCabinet.Domain.Entities;
using TrustCabinet.Domain.Enums;
using TrustCabinet.Domain.Users;
using TrustCabinet.Web.Tests.Authentication;
using TrustCabinet.Web.Tests.Users;

namespace TrustCabinet.Web.Tests.Access;

/// <summary>
/// SCRUM-12's acceptance criteria, over real HTTP: the server refuses what the caller may not do,
/// whatever the interface rendered.
///
/// These sit alongside the per-feature endpoint tests rather than inside them because the criteria
/// are about the gate itself — that it is applied at all, that it reads the database rather than
/// only the token, and that refusing does not leak. The per-feature suites then test what each
/// endpoint does once the gate has let the caller through.
///
/// Tokens are minted by the application's own <c>JwtTokenIssuer</c>, so a token the real issuer
/// would not produce cannot pass a test here either.
/// </summary>
public class CapabilityGateTests : IDisposable
{
    private readonly Factory _factory = new();

    public void Dispose() => _factory.Dispose();

    private sealed class Factory : TrustCabinetApiFactory
    {
        internal FakeUserAccountRepository Accounts { get; } = new();

        internal FakeAuditService Audit { get; } = new();

        protected override void ConfigureWebHost(IWebHostBuilder builder)
        {
            base.ConfigureWebHost(builder);

            builder.ConfigureServices(services =>
            {
                services.RemoveAll<IUserAccountRepository>();
                services.AddSingleton<IUserAccountRepository>(Accounts);

                services.RemoveAll<IAdministratorInvariantGuard>();
                services.AddSingleton<IAdministratorInvariantGuard>(
                    new FakeAdministratorInvariantGuard());

                services.RemoveAll<IAuditService>();
                services.AddSingleton<IAuditService>(Audit);

                services.RemoveAll<IRefreshTokenStore>();
                services.AddSingleton<IRefreshTokenStore>(new FakeRefreshTokenStore());

                services.RemoveAll<IPasswordHasher>();
                services.AddSingleton<IPasswordHasher, FakePasswordHasher>();
            });
        }

        /// <summary>A client carrying a genuine access token with the given capabilities.</summary>
        internal HttpClient ClientFor(User user, params Capability[] capabilities)
        {
            var client = CreateClient();
            var issuer = Services.GetRequiredService<ITokenIssuer>();

            client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue(
                "Bearer", issuer.CreateAccessToken(user, capabilities));

            return client;
        }
    }

    private User GivenUser(
        string fullName = "Lê Thành Viên",
        string email = "thanhvien@trustcabinet.local",
        SystemRole role = SystemRole.Member,
        bool isActive = true)
    {
        var user = new User
        {
            FullName = fullName,
            Email = email,
            PasswordHash = new FakePasswordHasher().Hash("MatKhauDaiVaAnToan@2026"),
            Role = role,
            IsActive = isActive,
        };

        _factory.Accounts.WithUser(user);
        return user;
    }

    private User GivenAdmin()
        => GivenUser("Phạm Quản Trị", "quantri@trustcabinet.local", SystemRole.Admin);

    /// <summary>
    /// Every route behind the AdminUsers gate. Kept as one list so a criterion is checked against
    /// all of them at once — a gate applied to three groups out of four is the bug this catches.
    /// </summary>
    public static TheoryData<string, string> GatedRoutes() => new()
    {
        { "GET", "/api/users" },
        { "POST", "/api/users" },
        { "GET", "/api/roles/capabilities" },
        { "PUT", "/api/roles/capabilities" },
    };

    // ------------------------------------------------- "Chưa đăng nhập" → 401

    [Theory]
    [MemberData(nameof(GatedRoutes))]
    public async Task An_anonymous_caller_is_refused(string method, string path)
    {
        var response = await _factory.CreateClient()
            .SendAsync(new HttpRequestMessage(new HttpMethod(method), path));

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    // ------------------- "Thiếu capability, endpoint cố định" → 403, not 404

    [Theory]
    [MemberData(nameof(GatedRoutes))]
    public async Task A_signed_in_caller_without_the_capability_is_refused(
        string method, string path)
    {
        // Signed in, with a genuine token — it simply does not carry AdminUsers.
        var client = _factory.ClientFor(
            GivenUser("Nguyễn Thư Ký", "thuky@trustcabinet.local", SystemRole.Secretary),
            Capability.MeetingCreator);

        var response = await client.SendAsync(new HttpRequestMessage(new HttpMethod(method), path));

        // 403 rather than 404: these paths are in the published API documentation, so hiding them
        // gains nothing, and a 404 would leave the frontend unable to tell "wrong permission" from
        // "wrong URL" — AGENTS.md, "403 or 404?".
        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
        Assert.Empty(_factory.Audit.Records);
    }

    // -------------------------- The token is not the authority; the database is

    [Fact]
    public async Task A_token_that_claims_a_capability_the_role_no_longer_has_is_refused()
    {
        // The heart of the story. The token is genuine, unexpired and carries AdminUsers — it was
        // issued while the role still had it. An administrator has since taken it away (SCRUM-60),
        // and the caller must stop being an administrator at their next request rather than
        // whenever their access token happens to expire.
        var admin = GivenAdmin();
        var client = _factory.ClientFor(admin, Capability.AdminUsers);

        _factory.Accounts.WithCapabilities(SystemRole.Admin, Capability.MeetingCreator);

        var response = await client.GetAsync("/api/users");

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [Fact]
    public async Task A_token_belonging_to_a_deactivated_account_is_refused()
    {
        // Same window, the other way in: deactivating someone revokes their refresh tokens but
        // cannot reach into the access token already in their hands.
        var admin = GivenAdmin();
        var client = _factory.ClientFor(admin, Capability.AdminUsers);

        admin.IsActive = false;

        var response = await client.GetAsync("/api/users");

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [Fact]
    public async Task A_token_whose_account_no_longer_exists_is_refused()
    {
        // Nothing deletes a user today — rows are deactivated instead — but the gate must not
        // depend on that staying true: a caller it cannot resolve is a caller it cannot authorize.
        var client = _factory.ClientFor(
            new User
            {
                FullName = "Người Đã Xoá",
                Email = "daxoa@trustcabinet.local",
                PasswordHash = "hashed:x",
                Role = SystemRole.Admin,
                IsActive = true,
            },
            Capability.AdminUsers);

        var response = await client.GetAsync("/api/users");

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    // ------------------ "Không được đọc một tài nguyên cụ thể" → 404, not 403

    [Fact]
    public async Task An_unreadable_resource_answers_404_without_confirming_it_exists()
    {
        // A record the caller may not see must not be distinguishable from one that is not there:
        // a 403 on a specific id is itself the information, because it confirms that id exists
        // (AGENTS.md — "giấu sự tồn tại của một dòng dữ liệu").
        var target = GivenUser();
        var client = _factory.ClientFor(GivenAdmin(), Capability.AdminUsers);

        var existing = await client.GetAsync($"/api/users/{target.Id}");
        var unknown = await client.GetAsync($"/api/users/{Guid.CreateVersion7()}");

        Assert.Equal(HttpStatusCode.OK, existing.StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, unknown.StatusCode);

        // Byte-for-byte identical, so the response cannot be used as an existence oracle.
        Assert.Equal(
            await unknown.Content.ReadAsStringAsync(),
            await (await client.GetAsync($"/api/users/{Guid.CreateVersion7()}"))
                .Content.ReadAsStringAsync());
    }

    // ------------------------ "GET /api/auth/me trả về tập quyền" for the UI

    [Fact]
    public async Task Me_returns_the_capability_set_the_interface_renders_from()
    {
        var secretary = GivenUser(
            "Nguyễn Thư Ký", "thuky@trustcabinet.local", SystemRole.Secretary);
        var client = _factory.ClientFor(
            secretary, Capability.MeetingCreator, Capability.MinutesEditor);

        var response = await client.GetAsync("/api/auth/me");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var body = JsonDocument.Parse(await response.Content.ReadAsStringAsync()).RootElement;
        var capabilities = body.GetProperty("capabilities")
            .EnumerateArray().Select(c => c.GetString() ?? "").ToArray();

        Assert.Equal(secretary.Email, body.GetProperty("email").GetString());
        Assert.Equal("Secretary", body.GetProperty("role").GetString());
        Assert.Equal(["MeetingCreator", "MinutesEditor"], capabilities);
        // The frontend's stale table gave the Secretary no admin rights either; this is now the
        // only place that answer comes from.
        Assert.DoesNotContain("AdminUsers", capabilities);
    }

    [Fact]
    public async Task Me_is_refused_without_a_token()
    {
        var response = await _factory.CreateClient().GetAsync("/api/auth/me");

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    // ---------------------------------------- Every capability is gateable

    [Fact]
    public async Task Every_capability_has_a_registered_policy()
    {
        // RequireCapability names a policy per capability. One added to the enum without a policy
        // would throw at request time on whichever endpoint asked for it — a capability that
        // cannot be gated is an endpoint that ships ungated, so it is checked here instead.
        using var scope = _factory.Services.CreateScope();
        var policies = scope.ServiceProvider
            .GetRequiredService<Microsoft.AspNetCore.Authorization.IAuthorizationPolicyProvider>();

        foreach (var capability in Enum.GetValues<Capability>())
        {
            Assert.NotNull(
                await policies.GetPolicyAsync(Web.Endpoints.CapabilityGate.PolicyFor(capability)));
        }
    }
}
