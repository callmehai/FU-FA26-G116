using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Npgsql;
using TrustCabinet.Domain.Access;
using TrustCabinet.Domain.Audit;
using TrustCabinet.Domain.Authentication;
using TrustCabinet.Domain.Entities;
using TrustCabinet.Domain.Enums;
using TrustCabinet.Domain.Users;
using TrustCabinet.Web.Tests.Users;

namespace TrustCabinet.Web.Tests.Access;

/// <summary>
/// /api/roles over real HTTP (SCRUM-63) — status codes, message codes, the JSON shape and the
/// capability gate. This is the contract the frontend builds the role screen against.
///
/// Same arrangement as <c>RolePermissionEndpointsTests</c>: the real <c>RoleService</c> over an
/// in-memory repository, so routing, binding, the JWT middleware and the gate all run for real
/// with no PostgreSQL.
/// </summary>
public class RoleEndpointsTests : IDisposable
{
    private readonly Factory _factory = new();

    public void Dispose() => _factory.Dispose();

    private sealed class Factory : TrustCabinetApiFactory
    {
        internal FakeRoleRepository Roles { get; } = new();

        /// <summary>Backs the gate's re-read of the caller.</summary>
        internal FakeUserAccountRepository Accounts { get; } = new();

        internal FakeAuditService Audit { get; } = new();

        internal FakeAdministratorInvariantGuard Invariant { get; } = new();

        protected override void ConfigureWebHost(IWebHostBuilder builder)
        {
            base.ConfigureWebHost(builder);

            builder.ConfigureServices(services =>
            {
                services.RemoveAll<IRoleRepository>();
                services.AddSingleton<IRoleRepository>(Roles);

                services.RemoveAll<IAdministratorInvariantGuard>();
                services.AddSingleton<IAdministratorInvariantGuard>(Invariant);

                services.RemoveAll<IUserAccountRepository>();
                services.AddSingleton<IUserAccountRepository>(Accounts);

                services.RemoveAll<IAuditService>();
                services.AddSingleton<IAuditService>(Audit);
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

    private User GivenUser(Guid roleId, bool isActive = true)
    {
        var user = new User
        {
            FullName = "Phạm Quản Trị",
            Email = $"{Guid.CreateVersion7():N}@trustcabinet.local",
            PasswordHash = "hash",
            RoleId = roleId,
            IsActive = isActive,
        };

        _factory.Accounts.WithUser(user);
        return user;
    }

    private HttpClient AdminClient()
        => _factory.ClientFor(GivenUser(BuiltInRoles.Admin), Capability.AdminUsers);

    private Role GivenCustomRole(string name = "Thư ký phó")
    {
        var role = new Role { Name = name };
        _factory.Roles.WithRole(role);
        return role;
    }

    private static async Task<JsonElement> JsonOf(HttpResponseMessage response)
        => JsonDocument.Parse(await response.Content.ReadAsStringAsync()).RootElement;

    // ------------------------------------------------------------------- The capability gate

    public static TheoryData<string, string> EveryEndpoint => new()
    {
        { "GET", "/api/roles" },
        { "POST", "/api/roles" },
        { "PUT", $"/api/roles/{Guid.CreateVersion7()}" },
        { "DELETE", $"/api/roles/{Guid.CreateVersion7()}" },
    };

    private static HttpRequestMessage Request(string method, string path) => new(new HttpMethod(method), path)
    {
        Content = method is "POST" or "PUT" ? JsonContent.Create(new { name = "Thư ký phó" }) : null,
    };

    [Theory]
    [MemberData(nameof(EveryEndpoint))]
    public async Task Anonymous_callers_get_401(string method, string path)
    {
        var response = await _factory.CreateClient().SendAsync(Request(method, path));

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Theory]
    [MemberData(nameof(EveryEndpoint))]
    public async Task Callers_without_admin_users_get_403(string method, string path)
    {
        var client = _factory.ClientFor(GivenUser(BuiltInRoles.Secretary), Capability.MinutesEditor);

        var response = await client.SendAsync(Request(method, path));

        // 403 rather than 404: the endpoint is public API; hiding it gains nothing (AGENTS.md).
        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
        Assert.Empty(_factory.Audit.Records);
    }

    [Fact]
    public async Task A_token_claiming_admin_users_is_refused_once_the_account_is_deactivated()
    {
        var client = _factory.ClientFor(
            GivenUser(BuiltInRoles.Admin, isActive: false), Capability.AdminUsers);

        var response = await client.PostAsJsonAsync("/api/roles", new { name = "Thư ký phó" });

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
        Assert.Equal(BuiltInRoles.All.Count, _factory.Roles.Roles.Count);
    }

    // ---------------------------------------------------------------- GET /api/roles

    [Fact]
    public async Task List_returns_every_role_with_its_user_count_in_display_order()
    {
        var deputy = GivenCustomRole();
        _factory.Roles.WithUsers(BuiltInRoles.Chair, 3);

        var response = await AdminClient().GetAsync("/api/roles");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var roles = (await JsonOf(response)).EnumerateArray().ToArray();

        Assert.Equal(
            [.. BuiltInRoles.All.Select(id => id.ToString()), deputy.Id.ToString()],
            roles.Select(r => r.GetProperty("id").GetString()));

        var chair = roles[0];
        Assert.Equal("Chủ toạ", chair.GetProperty("name").GetString());
        Assert.True(chair.GetProperty("isBuiltIn").GetBoolean());
        Assert.Equal(3, chair.GetProperty("userCount").GetInt32());
        Assert.False(string.IsNullOrWhiteSpace(chair.GetProperty("description").GetString()));

        var custom = roles[^1];
        Assert.Equal("Thư ký phó", custom.GetProperty("name").GetString());
        Assert.False(custom.GetProperty("isBuiltIn").GetBoolean());
        Assert.Equal(0, custom.GetProperty("userCount").GetInt32());

        // Capabilities live in GET /api/roles/capabilities only.
        Assert.False(custom.TryGetProperty("capabilities", out _));

        // Exactly one role is preselected when creating a user: Member.
        Assert.Equal(
            [BuiltInRoles.Member.ToString()],
            roles.Where(r => r.GetProperty("isDefault").GetBoolean()).Select(r => r.GetProperty("id").GetString()));
    }

    // --------------------------------------------------------------- POST /api/roles

    [Fact]
    public async Task Create_answers_MSG19_with_201_and_a_location()
    {
        var response = await AdminClient().PostAsJsonAsync("/api/roles", new
        {
            name = "Thư ký phó",
            description = "Hỗ trợ thư ký soạn biên bản.",
            capabilities = new[] { "MinutesEditor" },
        });

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);

        var body = await JsonOf(response);
        Assert.Equal("MSG19", body.GetProperty("code").GetString());
        Assert.Equal("Tạo vai trò thành công.", body.GetProperty("message").GetString());

        var role = body.GetProperty("role");
        var id = role.GetProperty("id").GetString();
        Assert.Equal("Thư ký phó", role.GetProperty("name").GetString());
        Assert.Equal("Hỗ trợ thư ký soạn biên bản.", role.GetProperty("description").GetString());
        Assert.False(role.GetProperty("isBuiltIn").GetBoolean());
        Assert.Equal(0, role.GetProperty("userCount").GetInt32());
        Assert.Equal($"/api/roles/{id}", response.Headers.Location?.ToString());

        Assert.Equal([Capability.MinutesEditor], _factory.Roles.CapabilitiesOf(Guid.Parse(id!)));
    }

    [Fact]
    public async Task Create_records_the_signed_in_administrator_as_the_actor()
    {
        var admin = GivenUser(BuiltInRoles.Admin);
        var client = _factory.ClientFor(admin, Capability.AdminUsers);

        await client.PostAsJsonAsync("/api/roles", new { name = "Thư ký phó" });

        var record = Assert.Single(_factory.Audit.Records);
        Assert.Equal(AuditActions.RoleCreated, record.Action);
        Assert.Equal(admin.Id, record.ActorId);
    }

    [Fact]
    public async Task Create_without_capabilities_is_accepted()
    {
        var response = await AdminClient().PostAsJsonAsync("/api/roles", new { name = "Khách mời" });

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
    }

    [Fact]
    public async Task Create_answers_MSG20_with_400_and_the_offending_fields()
    {
        var response = await AdminClient().PostAsJsonAsync("/api/roles", new
        {
            name = "   ",
            description = new string('m', 257),
            capabilities = new[] { "DeleteEverything" },
        });

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);

        var body = await JsonOf(response);
        Assert.Equal("MSG20", body.GetProperty("code").GetString());
        Assert.Equal("Thông tin vai trò không hợp lệ.", body.GetProperty("message").GetString());

        var errors = body.GetProperty("errors");
        Assert.Equal(
            ["capabilities", "description", "name"],
            errors.EnumerateObject().Select(p => p.Name).Order());
        Assert.Contains(
            "DeleteEverything", errors.GetProperty("capabilities").GetString(), StringComparison.Ordinal);
    }

    [Fact]
    public async Task Create_answers_MSG20_for_an_empty_body_rather_than_a_binding_error()
    {
        var response = await AdminClient().PostAsJsonAsync("/api/roles", new { });

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Equal("MSG20", (await JsonOf(response)).GetProperty("code").GetString());
    }

    [Theory]
    [InlineData("")]
    [InlineData("null")]
    public async Task Create_and_update_answer_MSG20_when_there_is_no_body_at_all(string body)
    {
        var role = GivenCustomRole();
        var client = AdminClient();

        HttpContent Json() => new StringContent(body, System.Text.Encoding.UTF8, "application/json");

        foreach (var response in new[]
        {
            await client.PostAsync("/api/roles", Json()),
            await client.PutAsync($"/api/roles/{role.Id}", Json()),
        })
        {
            Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
            Assert.Equal("MSG20", (await JsonOf(response)).GetProperty("code").GetString());
        }
    }

    [Fact]
    public async Task Create_answers_MSG21_with_409_for_a_name_already_used()
    {
        var response = await AdminClient().PostAsJsonAsync("/api/roles", new { name = "THƯ KÝ" });

        Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);

        var body = await JsonOf(response);
        Assert.Equal("MSG21", body.GetProperty("code").GetString());
        Assert.Equal(
            "Đã có vai trò dùng tên này. Hãy đặt một tên khác.",
            body.GetProperty("message").GetString());
    }

    [Fact]
    public async Task Create_answers_500_with_a_retry_message_when_the_database_refuses()
    {
        _factory.Roles.NextSaveThrows = new NpgsqlException("connection lost");

        var response = await AdminClient().PostAsJsonAsync("/api/roles", new { name = "Thư ký phó" });

        Assert.Equal(HttpStatusCode.InternalServerError, response.StatusCode);

        var body = await JsonOf(response);
        Assert.Equal("MSG26", body.GetProperty("code").GetString());
        Assert.Equal("Không thể tạo vai trò. Vui lòng thử lại.", body.GetProperty("message").GetString());
    }

    [Fact]
    public async Task Update_answers_MSG27_when_the_database_refuses()
    {
        var role = GivenCustomRole();
        _factory.Roles.NextSaveThrows = new NpgsqlException("connection lost");

        var response = await AdminClient().PutAsJsonAsync(
            $"/api/roles/{role.Id}", new { name = "Phó thư ký" });

        Assert.Equal(HttpStatusCode.InternalServerError, response.StatusCode);

        var body = await JsonOf(response);
        Assert.Equal("MSG27", body.GetProperty("code").GetString());
        Assert.Equal("Không thể cập nhật vai trò. Vui lòng thử lại.", body.GetProperty("message").GetString());
    }

    [Fact]
    public async Task Delete_answers_MSG28_when_the_database_refuses()
    {
        var role = GivenCustomRole();
        _factory.Roles.NextSaveThrows = new NpgsqlException("connection lost");

        var response = await AdminClient().DeleteAsync($"/api/roles/{role.Id}");

        Assert.Equal(HttpStatusCode.InternalServerError, response.StatusCode);

        var body = await JsonOf(response);
        Assert.Equal("MSG28", body.GetProperty("code").GetString());
        Assert.Equal("Không thể xoá vai trò. Vui lòng thử lại.", body.GetProperty("message").GetString());
    }

    // ---------------------------------------------------------- PUT /api/roles/{id}

    [Fact]
    public async Task Update_answers_MSG22_with_the_updated_role()
    {
        var role = GivenCustomRole();
        _factory.Roles.WithUsers(role.Id, 2);

        var response = await AdminClient().PutAsJsonAsync(
            $"/api/roles/{role.Id}", new { name = "Phó thư ký", description = "Mới" });

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var body = await JsonOf(response);
        Assert.Equal("MSG22", body.GetProperty("code").GetString());
        Assert.Equal("Cập nhật vai trò thành công.", body.GetProperty("message").GetString());
        Assert.Equal("Phó thư ký", body.GetProperty("role").GetProperty("name").GetString());
        Assert.Equal(2, body.GetProperty("role").GetProperty("userCount").GetInt32());
        Assert.Equal(AuditActions.RoleUpdated, Assert.Single(_factory.Audit.Records).Action);
    }

    [Fact]
    public async Task Update_ignores_capabilities_in_the_body()
    {
        // One path changes an existing role's capabilities — PUT /api/roles/capabilities — so the
        // lock-out rule is guarded in one place.
        var role = GivenCustomRole();

        await AdminClient().PutAsJsonAsync(
            $"/api/roles/{role.Id}", new { name = "Phó thư ký", capabilities = new[] { "AdminUsers" } });

        Assert.Empty(_factory.Roles.CapabilitiesOf(role.Id));
    }

    [Fact]
    public async Task Update_answers_MSG24_with_409_for_a_built_in_role()
    {
        var response = await AdminClient().PutAsJsonAsync(
            $"/api/roles/{BuiltInRoles.Admin}", new { name = "Siêu quản trị" });

        Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);

        var body = await JsonOf(response);
        Assert.Equal("MSG24", body.GetProperty("code").GetString());
        Assert.Contains("vai trò mặc định", body.GetProperty("message").GetString(), StringComparison.Ordinal);
    }

    [Fact]
    public async Task Update_answers_MSG21_for_the_name_of_another_role()
    {
        var role = GivenCustomRole();

        var response = await AdminClient().PutAsJsonAsync(
            $"/api/roles/{role.Id}", new { name = "quản trị viên" });

        Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);
        Assert.Equal("MSG21", (await JsonOf(response)).GetProperty("code").GetString());
    }

    [Fact]
    public async Task Update_answers_MSG20_for_an_invalid_name()
    {
        var role = GivenCustomRole();

        var response = await AdminClient().PutAsJsonAsync(
            $"/api/roles/{role.Id}", new { name = new string('a', 65) });

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);

        var body = await JsonOf(response);
        Assert.Equal("MSG20", body.GetProperty("code").GetString());
        Assert.True(body.GetProperty("errors").TryGetProperty("name", out _));
    }

    [Fact]
    public async Task Update_answers_404_without_a_body_for_an_unknown_role()
    {
        var response = await AdminClient().PutAsJsonAsync(
            $"/api/roles/{Guid.CreateVersion7()}", new { name = "Tên" });

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
        Assert.Equal("", await response.Content.ReadAsStringAsync());
    }

    // ------------------------------------------------------- DELETE /api/roles/{id}

    [Fact]
    public async Task Delete_answers_MSG23()
    {
        var role = GivenCustomRole();

        var response = await AdminClient().DeleteAsync($"/api/roles/{role.Id}");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var body = await JsonOf(response);
        Assert.Equal("MSG23", body.GetProperty("code").GetString());
        Assert.Equal("Xoá vai trò thành công.", body.GetProperty("message").GetString());
        Assert.DoesNotContain(_factory.Roles.Roles, r => r.Id == role.Id);
        Assert.Equal(AuditActions.RoleDeleted, Assert.Single(_factory.Audit.Records).Action);
    }

    [Fact]
    public async Task Delete_answers_MSG25_with_409_and_the_user_count()
    {
        var role = GivenCustomRole();
        _factory.Roles.WithUsers(role.Id, 3);

        var response = await AdminClient().DeleteAsync($"/api/roles/{role.Id}");

        Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);

        var body = await JsonOf(response);
        Assert.Equal("MSG25", body.GetProperty("code").GetString());
        Assert.Equal(3, body.GetProperty("userCount").GetInt32());
        Assert.Equal(
            "Vai trò này vẫn còn 3 tài khoản mang. Hãy chuyển họ sang vai trò khác trước khi xoá.",
            body.GetProperty("message").GetString());
    }

    [Fact]
    public async Task Delete_answers_MSG24_with_409_for_a_built_in_role()
    {
        var response = await AdminClient().DeleteAsync($"/api/roles/{BuiltInRoles.Member}");

        Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);
        Assert.Equal("MSG24", (await JsonOf(response)).GetProperty("code").GetString());
    }

    [Fact]
    public async Task Delete_answers_404_without_a_body_for_an_unknown_role()
    {
        var response = await AdminClient().DeleteAsync($"/api/roles/{Guid.CreateVersion7()}");

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
        Assert.Equal("", await response.Content.ReadAsStringAsync());
    }

    [Fact]
    public async Task A_role_id_that_is_not_a_guid_is_an_unmatched_route()
    {
        // The {id:guid} constraint; SCRUM-62 makes an unmatched /api path a plain 404.
        var response = await AdminClient().DeleteAsync("/api/roles/Chair");

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }
}
