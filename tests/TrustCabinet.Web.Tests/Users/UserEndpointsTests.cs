using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
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

namespace TrustCabinet.Web.Tests.Users;

/// <summary>
/// The /api/users endpoints over real HTTP — status codes, message codes, the shape of the JSON,
/// and the capability gate. This is the contract the frontend builds against in SCRUM-46.
///
/// The repository, the audit writer and the token store are in-memory fakes, so these run with no
/// PostgreSQL and no Redis. What runs for real is routing, model binding, the JWT middleware, the
/// authorization policy and the controller's mapping of outcomes onto responses.
///
/// Tokens are minted by the application's own <c>JwtTokenIssuer</c> rather than stubbed, so the
/// capability gate is exercised end to end: a token that the real issuer would not produce cannot
/// pass a test here either.
/// </summary>
public class UserEndpointsTests : IDisposable
{
    private const string ValidPassword = "MatKhauDaiVaAnToan@2026";

    private readonly Factory _factory = new();

    public void Dispose() => _factory.Dispose();

    private sealed class Factory : TrustCabinetApiFactory
    {
        internal FakeUserAccountRepository Accounts { get; } = new();

        internal FakeAuditService Audit { get; } = new();

        internal FakeRefreshTokenStore RefreshTokens { get; } = new();

        internal FakeAdministratorInvariantGuard Invariant { get; } = new();

        internal FakePasswordResetTokenStore ResetTokens { get; } = new();

        internal FakePasswordResetEmailSender ResetEmail { get; } = new();

        protected override void ConfigureWebHost(IWebHostBuilder builder)
        {
            base.ConfigureWebHost(builder);

            builder.ConfigureServices(services =>
            {
                services.RemoveAll<IUserAccountRepository>();
                services.AddSingleton<IUserAccountRepository>(Accounts);

                // The real one opens a transaction and takes an advisory lock, which needs the
                // PostgreSQL these tests exist to avoid. What it guards is tested against a real
                // database in AdministratorInvariantGuardTests; here it only has to run the write.
                services.RemoveAll<IAdministratorInvariantGuard>();
                services.AddSingleton<IAdministratorInvariantGuard>(Invariant);

                services.RemoveAll<IAuditService>();
                services.AddSingleton<IAuditService>(Audit);

                services.RemoveAll<IRefreshTokenStore>();
                services.AddSingleton<IRefreshTokenStore>(RefreshTokens);

                // UC-07: no Redis for the links and no SMTP server for the email.
                services.RemoveAll<IPasswordResetTokenStore>();
                services.AddSingleton<IPasswordResetTokenStore>(ResetTokens);
                services.RemoveAll<IPasswordResetEmailSender>();
                services.AddSingleton<IPasswordResetEmailSender>(ResetEmail);

                // The real hasher is deliberately slow and every create test pays for it.
                services.RemoveAll<IPasswordHasher>();
                services.AddSingleton<IPasswordHasher, FakePasswordHasher>();
            });
        }

        /// <summary>
        /// A client carrying a genuine access token for an account with the given capabilities.
        /// </summary>
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
        Guid? role = null,
        bool isActive = true)
    {
        var user = new User
        {
            FullName = fullName,
            Email = email,
            PasswordHash = new FakePasswordHasher().Hash(ValidPassword),
            RoleId = role ?? BuiltInRoles.Member,
            IsActive = isActive,
        };

        _factory.Accounts.WithUser(user);
        return user;
    }

    /// <summary>The signed-in administrator these tests act as.</summary>
    private HttpClient AdminClient()
        => _factory.ClientFor(
            GivenUser("Phạm Quản Trị", "quantri@trustcabinet.local", BuiltInRoles.Admin),
            Capability.AdminUsers);

    private static async Task<JsonElement> JsonOf(HttpResponseMessage response)
        => JsonDocument.Parse(await response.Content.ReadAsStringAsync()).RootElement;

    // ------------------------------------------------------------------ The capability gate

    [Fact]
    public async Task Anonymous_callers_are_refused()
    {
        var response = await _factory.CreateClient().GetAsync("/api/users");

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Theory]
    [InlineData("GET", "/api/users")]
    [InlineData("POST", "/api/users")]
    public async Task A_signed_in_user_without_admin_users_is_refused(string method, string path)
    {
        // The acceptance criterion: only admin_users reaches these endpoints. A Secretary is
        // signed in and carries a real token — it just does not carry this capability.
        var client = _factory.ClientFor(
            GivenUser("Nguyễn Thư Ký", "thuky@trustcabinet.local", BuiltInRoles.Secretary),
            Capability.MeetingCreator);

        var response = await client.SendAsync(new HttpRequestMessage(new HttpMethod(method), path));

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [Theory]
    [InlineData("PUT", "/api/users/{0}")]
    [InlineData("PUT", "/api/users/{0}/status")]
    public async Task Writes_are_refused_without_admin_users(string method, string template)
    {
        var target = GivenUser();
        var client = _factory.ClientFor(
            GivenUser("Nguyễn Thư Ký", "thuky@trustcabinet.local", BuiltInRoles.Secretary),
            Capability.MeetingCreator);

        var response = await client.SendAsync(new HttpRequestMessage(
            new HttpMethod(method), string.Format(template, target.Id)));

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
        // Refused before anything happened, not merely refused in the response.
        Assert.True(target.IsActive);
        Assert.Empty(_factory.Audit.Records);
    }

    // ---------------------------------------------------------- UC-05 · GET /api/users

    [Fact]
    public async Task List_returns_the_accounts_sorted_and_without_secrets()
    {
        GivenUser("Nguyễn Thư Ký", "thuky@trustcabinet.local", BuiltInRoles.Secretary);
        var client = AdminClient();

        var response = await client.GetAsync("/api/users");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var body = await response.Content.ReadAsStringAsync();
        var accounts = JsonDocument.Parse(body).RootElement;

        Assert.Equal(2, accounts.GetArrayLength());
        Assert.Equal("Nguyễn Thư Ký", accounts[0].GetProperty("fullName").GetString());
        // The role reads back as its id and its current name (ADR-0008) — no enum string for the
        // frontend to translate.
        Assert.Equal(BuiltInRoles.Secretary.ToString(), accounts[0].GetProperty("roleId").GetString());
        Assert.Equal("Thư ký", accounts[0].GetProperty("roleName").GetString());
        Assert.False(accounts[0].TryGetProperty("role", out _));
        Assert.True(accounts[0].GetProperty("isActive").GetBoolean());

        // Nothing credential-shaped on the wire, whatever a later refactor adds to the response.
        Assert.DoesNotContain("passwordHash", body, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("googleSubjectId", body, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("hashed:", body);
    }

    [Fact]
    public async Task Get_returns_one_account_and_404_for_an_unknown_id()
    {
        var target = GivenUser();
        var client = AdminClient();

        var found = await client.GetAsync($"/api/users/{target.Id}");
        var missing = await client.GetAsync($"/api/users/{Guid.CreateVersion7()}");

        Assert.Equal(HttpStatusCode.OK, found.StatusCode);
        Assert.Equal(target.Email, (await JsonOf(found)).GetProperty("email").GetString());
        Assert.Equal(HttpStatusCode.NotFound, missing.StatusCode);
    }

    // -------------------------------------------------------- UC-06a · POST /api/users

    [Fact]
    public async Task Create_answers_MSG07_with_201_and_a_location()
    {
        var client = AdminClient();

        var response = await client.PostAsJsonAsync("/api/users", new
        {
            fullName = "Trần Chủ Toạ",
            email = "chutoa@trustcabinet.local",
            password = ValidPassword,
            roleId = BuiltInRoles.Chair,
        });

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);

        var body = await JsonOf(response);
        Assert.Equal("MSG07", body.GetProperty("code").GetString());

        var created = body.GetProperty("user");
        Assert.Equal("chutoa@trustcabinet.local", created.GetProperty("email").GetString());
        Assert.True(created.GetProperty("isActive").GetBoolean());
        Assert.Equal(
            $"/api/users/{created.GetProperty("id").GetString()}",
            response.Headers.Location?.ToString());
    }

    [Fact]
    public async Task Create_records_the_signed_in_administrator_as_the_actor()
    {
        // The actor comes from the token, never from the body — an actor id a caller can choose is
        // not evidence of anything.
        var admin = GivenUser("Phạm Quản Trị", "quantri@trustcabinet.local", BuiltInRoles.Admin);
        var client = _factory.ClientFor(admin, Capability.AdminUsers);

        await client.PostAsJsonAsync("/api/users", new
        {
            fullName = "Trần Chủ Toạ",
            email = "chutoa@trustcabinet.local",
            password = ValidPassword,
            roleId = BuiltInRoles.Chair,
        });

        Assert.Equal(admin.Id, Assert.Single(_factory.Audit.Records).ActorId);
    }

    [Fact]
    public async Task Create_answers_MSG08_with_409_for_a_duplicate_email()
    {
        GivenUser(email: "trungnhau@trustcabinet.local");
        var client = AdminClient();

        var response = await client.PostAsJsonAsync("/api/users", new
        {
            fullName = "Tên khác",
            email = "trungnhau@trustcabinet.local",
            password = ValidPassword,
            roleId = BuiltInRoles.Member,
        });

        Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);
        Assert.Equal("MSG08", (await JsonOf(response)).GetProperty("code").GetString());
    }

    [Fact]
    public async Task Create_answers_400_with_the_offending_fields()
    {
        var client = AdminClient();

        var response = await client.PostAsJsonAsync("/api/users", new
        {
            fullName = "",
            email = "khong-phai-email",
            password = "ngan",
            roleId = BuiltInRoles.Member,
        });

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);

        // "Display invalid fields" needs to know which ones.
        var errors = (await JsonOf(response)).GetProperty("errors");
        Assert.Equal(
            ["email", "fullName", "password"],
            errors.EnumerateObject().Select(p => p.Name).OrderBy(n => n));
    }

    [Theory]
    [InlineData("01990000-0000-7000-8000-000000000000")] // a well-formed id no role has
    [InlineData("Chair")]                                 // the old contract's enum name
    [InlineData(null)]
    public async Task Create_answers_MSG09_on_roleId_for_a_role_that_does_not_exist(string? roleId)
    {
        var client = AdminClient();

        var response = await client.PostAsJsonAsync("/api/users", new
        {
            fullName = "Tên hợp lệ",
            email = "hople@trustcabinet.local",
            password = ValidPassword,
            roleId,
        });

        // A validation message on the field, not a model-binding 400 with no field named.
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);

        var json = await JsonOf(response);
        Assert.Equal("MSG09", json.GetProperty("code").GetString());
        Assert.Equal(
            "Vai trò không tồn tại.",
            json.GetProperty("errors").GetProperty("roleId").GetString());
    }

    [Fact]
    public async Task Create_answers_with_the_role_id_and_name()
    {
        var client = AdminClient();

        var response = await client.PostAsJsonAsync("/api/users", new
        {
            fullName = "Trần Chủ Toạ",
            email = "chutoa@trustcabinet.local",
            password = ValidPassword,
            roleId = BuiltInRoles.Chair,
        });

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);

        var user = (await JsonOf(response)).GetProperty("user");
        Assert.Equal(BuiltInRoles.Chair.ToString(), user.GetProperty("roleId").GetString());
        Assert.Equal("Chủ toạ", user.GetProperty("roleName").GetString());
    }

    // --------------------------------------------------------- UC-06b · PUT /api/users/{id}

    [Fact]
    public async Task Update_answers_MSG10_with_the_updated_account()
    {
        var target = GivenUser("Tên Cũ", "cu@trustcabinet.local");
        var client = AdminClient();

        var response = await client.PutAsJsonAsync($"/api/users/{target.Id}", new
        {
            fullName = "Tên Mới",
            email = "moi@trustcabinet.local",
            roleId = BuiltInRoles.Secretary,
        });

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var body = await JsonOf(response);
        Assert.Equal("MSG10", body.GetProperty("code").GetString());
        Assert.Equal("Tên Mới", body.GetProperty("user").GetProperty("fullName").GetString());
        Assert.Equal(AuditActions.UserUpdated, Assert.Single(_factory.Audit.Records).Action);
    }

    [Fact]
    public async Task Update_answers_404_for_an_unknown_account()
    {
        var client = AdminClient();

        var response = await client.PutAsJsonAsync($"/api/users/{Guid.CreateVersion7()}", new
        {
            fullName = "Tên hợp lệ",
            email = "hople@trustcabinet.local",
            roleId = BuiltInRoles.Member,
        });

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    public async Task Update_cannot_change_the_status()
    {
        // The edit form has no status field, and posting one must not smuggle a deactivation
        // through UC-06b's endpoint, where UC-06c's session handling does not run.
        var target = GivenUser(isActive: true);
        var client = AdminClient();

        await client.PutAsJsonAsync($"/api/users/{target.Id}", new
        {
            fullName = target.FullName,
            email = target.Email,
            roleId = BuiltInRoles.Member,
            isActive = false,
        });

        Assert.True(target.IsActive);
    }

    // -------------------------------------------------- UC-06c · PUT /api/users/{id}/status

    [Fact]
    public async Task Deactivate_answers_MSG11_and_ends_the_sessions()
    {
        var target = GivenUser(isActive: true);
        await _factory.RefreshTokens.StoreAsync("rt-cua-ho", target.Id, TimeSpan.FromDays(7));
        var client = AdminClient();

        var response = await client.PutAsJsonAsync(
            $"/api/users/{target.Id}/status", new { isActive = false });

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var body = await JsonOf(response);
        Assert.Equal("MSG11", body.GetProperty("code").GetString());
        Assert.False(body.GetProperty("user").GetProperty("isActive").GetBoolean());
        Assert.Empty(_factory.RefreshTokens.ActiveTokens);
        Assert.Equal(AuditActions.UserDeactivated, Assert.Single(_factory.Audit.Records).Action);
    }

    [Fact]
    public async Task Reactivate_answers_MSG11()
    {
        var target = GivenUser(isActive: false);
        var client = AdminClient();

        var response = await client.PutAsJsonAsync(
            $"/api/users/{target.Id}/status", new { isActive = true });

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal("MSG11", (await JsonOf(response)).GetProperty("code").GetString());
        Assert.Equal(AuditActions.UserReactivated, Assert.Single(_factory.Audit.Records).Action);
    }

    [Fact]
    public async Task Status_change_answers_409_when_it_is_not_allowed()
    {
        var target = GivenUser(isActive: false);
        var client = AdminClient();

        // Already inactive — the diagram's "Status change is not allowed" branch.
        var response = await client.PutAsJsonAsync(
            $"/api/users/{target.Id}/status", new { isActive = false });

        Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);
        Assert.Equal("MSG12", (await JsonOf(response)).GetProperty("code").GetString());
        Assert.Empty(_factory.Audit.Records);
    }

    [Fact]
    public async Task An_administrator_cannot_deactivate_themselves_over_http()
    {
        var admin = GivenUser("Phạm Quản Trị", "quantri@trustcabinet.local", BuiltInRoles.Admin);
        var client = _factory.ClientFor(admin, Capability.AdminUsers);

        var response = await client.PutAsJsonAsync(
            $"/api/users/{admin.Id}/status", new { isActive = false });

        Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);
        Assert.True(admin.IsActive);
    }

    [Fact]
    public async Task A_deactivated_administrator_is_refused_even_with_a_valid_token()
    {
        // The window the endpoint filter closes. The token is genuine, unexpired and carries
        // AdminUsers — revoking refresh tokens does not touch it. Without a server-side re-read
        // this caller keeps full power over every account until the token expires, long enough
        // to deactivate whoever deactivated them or to mint a fresh admin account.
        var admin = GivenUser("Phạm Quản Trị", "quantri@trustcabinet.local", BuiltInRoles.Admin);
        var client = _factory.ClientFor(admin, Capability.AdminUsers);
        var target = GivenUser();

        admin.IsActive = false;

        var response = await client.PutAsJsonAsync(
            $"/api/users/{target.Id}/status", new { isActive = false });

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
        Assert.True(target.IsActive);
        Assert.Empty(_factory.Audit.Records);
    }

    [Fact]
    public async Task An_administrator_whose_role_lost_the_capability_is_refused()
    {
        // Same idea for a demotion rather than a deactivation: the token still says AdminUsers,
        // the database no longer agrees, and the database wins.
        var admin = GivenUser("Phạm Quản Trị", "quantri@trustcabinet.local", BuiltInRoles.Admin);
        var client = _factory.ClientFor(admin, Capability.AdminUsers);

        admin.RoleId = BuiltInRoles.Member;

        var response = await client.GetAsync("/api/users");

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [Theory]
    // An empty body, and a client sending snake_case or a misspelled field.
    [InlineData("{}")]
    [InlineData("{\"is_active\":true}")]
    [InlineData("{\"active\":true}")]
    public async Task Status_change_refuses_a_body_that_does_not_state_the_status(string body)
    {
        // A non-nullable bool would bind every one of these to false — deactivating the account,
        // revoking their sessions, writing user.deactivated to the permanent audit log, and
        // answering 200 MSG11 "thành công".
        var target = GivenUser(isActive: true);
        var client = AdminClient();

        var response = await client.PutAsync(
            $"/api/users/{target.Id}/status",
            new StringContent(body, System.Text.Encoding.UTF8, "application/json"));

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.True(target.IsActive);
        Assert.Empty(_factory.Audit.Records);
    }

    [Fact]
    public async Task Status_change_answers_404_for_an_unknown_account()
    {
        var client = AdminClient();

        var response = await client.PutAsJsonAsync(
            $"/api/users/{Guid.CreateVersion7()}/status", new { isActive = false });

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    // ------------------------------------------------------------------ UC-07 · Reset password

    private const string NewPassword = "MotMatKhauMoiRatDai@2026";

    [Fact]
    public async Task Reset_request_answers_MSG13_and_emails_the_account()
    {
        var target = GivenUser();
        var client = AdminClient();

        var response = await client.PostAsync($"/api/users/{target.Id}/password-reset", null);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var body = await JsonOf(response);
        Assert.Equal("MSG13", body.GetProperty("code").GetString());
        Assert.Equal(target.Id.ToString(), body.GetProperty("user").GetProperty("id").GetString());
        Assert.Equal(target.Email, Assert.Single(_factory.ResetEmail.Sent).ToAddress);
    }

    [Fact]
    public async Task Reset_request_answers_MSG14_for_a_deactivated_account()
    {
        var target = GivenUser(isActive: false);
        var client = AdminClient();

        var response = await client.PostAsync($"/api/users/{target.Id}/password-reset", null);

        Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);
        Assert.Equal("MSG14", (await JsonOf(response)).GetProperty("code").GetString());
    }

    [Fact]
    public async Task Reset_request_answers_MSG14_for_an_account_without_a_password()
    {
        var target = GivenUser();
        target.PasswordHash = null;
        target.GoogleSubjectId = "google-sub-123";
        var client = AdminClient();

        var response = await client.PostAsync($"/api/users/{target.Id}/password-reset", null);

        Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);
        Assert.Equal("MSG14", (await JsonOf(response)).GetProperty("code").GetString());
        Assert.Empty(_factory.ResetEmail.Sent);
    }

    [Fact]
    public async Task Reset_request_answers_MSG15_when_the_email_cannot_be_sent()
    {
        var target = GivenUser();
        var client = AdminClient();
        _factory.ResetEmail.Fails = true;

        var response = await client.PostAsync($"/api/users/{target.Id}/password-reset", null);

        Assert.Equal(HttpStatusCode.ServiceUnavailable, response.StatusCode);
        Assert.Equal("MSG15", (await JsonOf(response)).GetProperty("code").GetString());
    }

    [Fact]
    public async Task Reset_request_answers_404_for_an_unknown_account()
    {
        var client = AdminClient();

        var response = await client.PostAsync($"/api/users/{Guid.CreateVersion7()}/password-reset", null);

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    public async Task Reset_request_needs_admin_users()
    {
        var target = GivenUser();
        var client = _factory.ClientFor(
            GivenUser("Nguyễn Thư Ký", "thuky@trustcabinet.local", BuiltInRoles.Secretary),
            Capability.MeetingCreator);

        var response = await client.PostAsync($"/api/users/{target.Id}/password-reset", null);

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
        Assert.Empty(_factory.ResetEmail.Sent);
    }

    [Fact]
    public async Task Reset_with_the_emailed_link_answers_MSG92_without_signing_in()
    {
        var target = GivenUser();
        await AdminClient().PostAsync($"/api/users/{target.Id}/password-reset", null);
        var token = Assert.Single(_factory.ResetEmail.Sent).Token;

        // Anonymous: someone resetting a forgotten password has no session to send.
        var response = await _factory.CreateClient().PostAsJsonAsync(
            "/api/auth/password-reset", new { token, newPassword = NewPassword });

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var body = await JsonOf(response);
        Assert.Equal("MSG92", body.GetProperty("code").GetString());
        Assert.False(body.TryGetProperty("accessToken", out _));
        Assert.True(new FakePasswordHasher().Verify(target.PasswordHash!, NewPassword));
    }

    [Theory]
    [InlineData("{\"token\":\"khong-ton-tai\",\"newPassword\":\"MotMatKhauMoiRatDai@2026\"}")]
    [InlineData("{\"newPassword\":\"MotMatKhauMoiRatDai@2026\"}")]
    [InlineData("{}")]
    public async Task Reset_with_a_dead_link_answers_MSG93(string body)
    {
        var response = await _factory.CreateClient().PostAsync(
            "/api/auth/password-reset",
            new StringContent(body, System.Text.Encoding.UTF8, "application/json"));

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Equal("MSG93", (await JsonOf(response)).GetProperty("code").GetString());
    }

    [Fact]
    public async Task Reset_with_a_too_short_password_answers_MSG94_on_the_field()
    {
        var target = GivenUser();
        await AdminClient().PostAsync($"/api/users/{target.Id}/password-reset", null);
        var token = Assert.Single(_factory.ResetEmail.Sent).Token;

        var response = await _factory.CreateClient().PostAsJsonAsync(
            "/api/auth/password-reset", new { token, newPassword = "ngan" });

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        var body = await JsonOf(response);
        Assert.Equal("MSG94", body.GetProperty("code").GetString());
        Assert.True(body.GetProperty("errors").TryGetProperty("newPassword", out _));
    }
}
