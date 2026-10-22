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
using TrustCabinet.Web.Tests.Users;

namespace TrustCabinet.Web.Tests.Access;

/// <summary>
/// The /api/roles/capabilities endpoints over real HTTP — status codes, message codes, the shape
/// of the JSON, and the capability gate. This is the contract the frontend builds against.
///
/// Same arrangement as <c>UserEndpointsTests</c>: in-memory fakes for the repositories and the
/// token store, so these run with no PostgreSQL and no Redis, while routing, model binding, the
/// JWT middleware and the authorization policy all run for real.
/// </summary>
public class RolePermissionEndpointsTests : IDisposable
{
    private readonly Factory _factory = new();

    public void Dispose() => _factory.Dispose();

    private sealed class Factory : TrustCabinetApiFactory
    {
        internal FakeRolePermissionRepository Roles { get; } = new();

        /// <summary>
        /// Backs the revalidation filter, which re-reads the caller from the database on every
        /// request — so the gate cannot be satisfied by a token alone.
        /// </summary>
        internal FakeUserAccountRepository Accounts { get; } = new();

        internal FakeAuditService Audit { get; } = new();

        internal FakeRefreshTokenStore RefreshTokens { get; } = new();

        internal FakeAdministratorInvariantGuard Invariant { get; } = new();

        protected override void ConfigureWebHost(IWebHostBuilder builder)
        {
            base.ConfigureWebHost(builder);

            builder.ConfigureServices(services =>
            {
                services.RemoveAll<IRolePermissionRepository>();
                services.AddSingleton<IRolePermissionRepository>(Roles);

                // The real one opens a transaction and takes an advisory lock, which needs the
                // PostgreSQL these tests exist to avoid. The lock itself is tested against a real
                // database in AdministratorInvariantGuardTests.
                services.RemoveAll<IAdministratorInvariantGuard>();
                services.AddSingleton<IAdministratorInvariantGuard>(Invariant);

                services.RemoveAll<IUserAccountRepository>();
                services.AddSingleton<IUserAccountRepository>(Accounts);

                services.RemoveAll<IAuditService>();
                services.AddSingleton<IAuditService>(Audit);

                services.RemoveAll<IRefreshTokenStore>();
                services.AddSingleton<IRefreshTokenStore>(RefreshTokens);
            });
        }

        internal HttpClient ClientFor(User user, params Capability[] capabilities)
        {
            var client = CreateClient();
            var issuer = Services.GetRequiredService<ITokenIssuer>();

            client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue(
                "Bearer", issuer.CreateAccessToken(user, capabilities));

            return client;
        }
    }

    private User GivenUser(Guid? role = null, bool isActive = true)
    {
        var user = new User
        {
            FullName = "Phạm Quản Trị",
            Email = $"{Guid.CreateVersion7():N}@trustcabinet.local",
            PasswordHash = "hash",
            RoleId = role ?? BuiltInRoles.Member,
            IsActive = isActive,
        };

        _factory.Accounts.WithUser(user);
        return user;
    }

    /// <summary>The signed-in administrator these tests act as.</summary>
    private HttpClient AdminClient()
        => _factory.ClientFor(GivenUser(BuiltInRoles.Admin), Capability.AdminUsers);

    private static async Task<JsonElement> JsonOf(HttpResponseMessage response)
        => JsonDocument.Parse(await response.Content.ReadAsStringAsync()).RootElement;

    // ------------------------------------------------------------------- The capability gate

    [Fact]
    public async Task Anonymous_callers_are_refused()
    {
        var response = await _factory.CreateClient().GetAsync("/api/roles/capabilities");

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task A_caller_without_admin_users_is_refused()
    {
        var client = _factory.ClientFor(GivenUser(BuiltInRoles.Secretary), Capability.MinutesEditor);

        var response = await client.GetAsync("/api/roles/capabilities");

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [Fact]
    public async Task A_token_claiming_admin_users_is_refused_once_the_role_has_lost_it()
    {
        // The token says AdminUsers; the database no longer agrees. This is the window SCRUM-60
        // opens — an administrator can now take the capability away from a role, and the people
        // holding it must stop being administrators at their next request rather than when their
        // token expires.
        var caller = GivenUser(BuiltInRoles.Secretary);
        _factory.Accounts.WithCapabilities(BuiltInRoles.Secretary);

        var client = _factory.ClientFor(caller, Capability.AdminUsers);

        var response = await client.GetAsync("/api/roles/capabilities");

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [Fact]
    public async Task A_deactivated_administrator_is_refused_even_with_a_valid_token()
    {
        var caller = GivenUser(BuiltInRoles.Admin, isActive: false);
        var client = _factory.ClientFor(caller, Capability.AdminUsers);

        var response = await client.GetAsync("/api/roles/capabilities");

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    // ------------------------------------------------------ UC-10 · View the matrix

    [Fact]
    public async Task Get_returns_the_matrix_and_the_catalogue()
    {
        // Deliberately not the seeded defaults: asserting the defaults back would pass even if
        // the endpoint returned a constant instead of reading the repository.
        _factory.Roles.WithCapabilities(BuiltInRoles.Member, Capability.MinutesEditor);

        var response = await AdminClient().GetAsync("/api/roles/capabilities");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var json = await JsonOf(response);

        // Four roles by four capabilities — the grid the screen draws.
        Assert.Equal(
            BuiltInRoles.All.Count,
            json.GetProperty("roles").GetArrayLength());
        Assert.Equal(
            Enum.GetValues<Capability>().Length,
            json.GetProperty("capabilities").GetArrayLength());

        var member = json.GetProperty("roles")
            .EnumerateArray()
            .Single(r => r.GetProperty("id").GetString() == BuiltInRoles.Member.ToString());

        Assert.Equal(
            "MinutesEditor",
            member.GetProperty("capabilities").EnumerateArray().Single().GetString());

        // Roles are rows (ADR-0008): each carries its id, name and whether it is built in, so the
        // screen needs no table of its own.
        Assert.Equal("Thành viên", member.GetProperty("name").GetString());
        Assert.True(member.GetProperty("isBuiltIn").GetBoolean());
        Assert.True(member.TryGetProperty("description", out _));
    }

    [Fact]
    public async Task Catalogue_entries_carry_vietnamese_labels()
    {
        var json = await JsonOf(await AdminClient().GetAsync("/api/roles/capabilities"));

        // The screen's column headers come from the backend, so a capability added to the enum
        // cannot reach the administrator as a blank column.
        Assert.All(json.GetProperty("capabilities").EnumerateArray(), capability =>
        {
            Assert.False(string.IsNullOrWhiteSpace(capability.GetProperty("code").GetString()));
            Assert.False(string.IsNullOrWhiteSpace(capability.GetProperty("name").GetString()));
        });
    }

    // --------------------------------------------------- UC-11 · Configure role permissions

    [Fact]
    public async Task Put_answers_MSG16_and_returns_the_updated_matrix()
    {
        var response = await AdminClient().PutAsJsonAsync(
            "/api/roles/capabilities",
            new { roleId = BuiltInRoles.Member, capabilities = new[] { "MeetingCreator" } });

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var json = await JsonOf(response);
        Assert.Equal("MSG16", json.GetProperty("code").GetString());

        var member = json.GetProperty("matrix").GetProperty("roles")
            .EnumerateArray()
            .Single(r => r.GetProperty("id").GetString() == BuiltInRoles.Member.ToString());

        Assert.Equal(
            "MeetingCreator",
            member.GetProperty("capabilities").EnumerateArray().Single().GetString());
    }

    [Fact]
    public async Task Put_does_not_sign_out_the_accounts_holding_the_role()
    {
        // SCRUM-105: every holder used to lose their refresh token here, and with it the session.
        // Granting and then taking away again must leave every session standing — the gate test
        // above is what keeps a capability taken away from working.
        _factory.Roles.WithActiveUser(BuiltInRoles.Member, Guid.CreateVersion7());
        var admin = AdminClient();

        foreach (var capabilities in new[] { new[] { "MeetingCreator" }, [] })
        {
            var response = await admin.PutAsJsonAsync(
                "/api/roles/capabilities", new { roleId = BuiltInRoles.Member, capabilities });
            Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        }

        Assert.Empty(_factory.RefreshTokens.RevokedUsers);
    }

    [Fact]
    public async Task Put_answers_MSG18_when_the_database_refuses()
    {
        _factory.Roles.NextSaveThrows = new Npgsql.NpgsqlException("connection lost");

        var response = await AdminClient().PutAsJsonAsync(
            "/api/roles/capabilities",
            new { roleId = BuiltInRoles.Member, capabilities = new[] { "MeetingCreator" } });

        Assert.Equal(HttpStatusCode.InternalServerError, response.StatusCode);

        var json = await JsonOf(response);
        Assert.Equal("MSG18", json.GetProperty("code").GetString());
        Assert.Equal(
            "Không thể cập nhật phân quyền. Vui lòng thử lại.", json.GetProperty("message").GetString());
    }

    [Fact]
    public async Task Put_accepts_a_capability_name_in_any_casing()
    {
        // JSON casing conventions differ between the two ends; a code is not a secret handshake.
        var response = await AdminClient().PutAsJsonAsync(
            "/api/roles/capabilities",
            new { roleId = BuiltInRoles.Member, capabilities = new[] { "meetingCreator" } });

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    [Fact]
    public async Task Put_answers_MSG17_when_removing_the_last_admin_users_grant()
    {
        var response = await AdminClient().PutAsJsonAsync(
            "/api/roles/capabilities",
            new { roleId = BuiltInRoles.Admin, capabilities = Array.Empty<string>() });

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);

        var json = await JsonOf(response);
        Assert.Equal("MSG17", json.GetProperty("code").GetString());

        // Under its own key, so the screen can tell "this would lock everyone out" apart from
        // "you sent an unknown capability" and show the right message for each.
        Assert.True(json.GetProperty("errors").TryGetProperty("adminUsers", out _));
    }

    [Theory]
    [InlineData("01990000-0000-7000-8000-000000000000")] // a well-formed id no role has
    [InlineData("Admin")]                                 // the old contract's enum name
    [InlineData("")]
    public async Task Put_answers_MSG17_with_the_roleId_key_for_a_role_that_does_not_exist(string roleId)
    {
        var response = await AdminClient().PutAsJsonAsync(
            "/api/roles/capabilities",
            new { roleId, capabilities = new[] { "AdminUsers" } });

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);

        var json = await JsonOf(response);
        Assert.Equal("MSG17", json.GetProperty("code").GetString());
        Assert.True(json.GetProperty("errors").TryGetProperty("roleId", out _));
    }

    [Fact]
    public async Task Put_answers_MSG17_for_a_capability_the_enum_does_not_know()
    {
        var response = await AdminClient().PutAsJsonAsync(
            "/api/roles/capabilities",
            new { roleId = BuiltInRoles.Member, capabilities = new[] { "DeleteEverything" } });

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);

        var json = await JsonOf(response);
        Assert.Equal("MSG17", json.GetProperty("code").GetString());

        // Names the offending code. "Danh sách quyền không hợp lệ" leaves an administrator with
        // four checkboxes and no idea which one the backend refused.
        var message = json.GetProperty("errors").GetProperty("capabilities").GetString();
        Assert.Contains("DeleteEverything", message!, StringComparison.Ordinal);
    }

    [Fact]
    public async Task Put_names_every_unknown_capability_not_just_the_first()
    {
        var response = await AdminClient().PutAsJsonAsync(
            "/api/roles/capabilities",
            new { roleId = BuiltInRoles.Member, capabilities = new[] { "Nonsense", "AlsoWrong" } });

        var message = (await JsonOf(response))
            .GetProperty("errors").GetProperty("capabilities").GetString();

        // Both, because they all parse to the same out-of-range value once cast — which is why
        // the endpoint reports them before parsing rather than the service afterwards.
        Assert.Contains("Nonsense", message!, StringComparison.Ordinal);
        Assert.Contains("AlsoWrong", message!, StringComparison.Ordinal);
    }

    [Fact]
    public async Task A_missing_capability_list_is_refused_rather_than_read_as_empty()
    {
        // "{}" must not strip the role of everything and answer "thành công" — the destructive
        // reading of an absent field, the same trap ChangeUserStatusRequest.IsActive documents.
        var response = await AdminClient().PutAsJsonAsync(
            "/api/roles/capabilities", new { roleId = BuiltInRoles.Secretary });

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Equal("MSG17", (await JsonOf(response)).GetProperty("code").GetString());

        // And nothing was taken away.
        Assert.NotEmpty(_factory.Roles.CapabilitiesOf(BuiltInRoles.Secretary));
    }

    [Fact]
    public async Task An_empty_capability_list_is_accepted()
    {
        // Distinct from the missing list above: stated explicitly, it is a legitimate request.
        var response = await AdminClient().PutAsJsonAsync(
            "/api/roles/capabilities",
            new { roleId = BuiltInRoles.Secretary, capabilities = Array.Empty<string>() });

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Empty(_factory.Roles.CapabilitiesOf(BuiltInRoles.Secretary));
    }

    [Fact]
    public async Task Every_rejection_carries_a_vietnamese_message()
    {
        var response = await AdminClient().PutAsJsonAsync(
            "/api/roles/capabilities",
            new { roleId = BuiltInRoles.Admin, capabilities = Array.Empty<string>() });

        var message = (await JsonOf(response)).GetProperty("message").GetString();

        Assert.False(string.IsNullOrWhiteSpace(message));
        // A Vietnamese diacritic, so an English fallback string fails here.
        Assert.Contains("ề", message!, StringComparison.Ordinal);
    }
}
