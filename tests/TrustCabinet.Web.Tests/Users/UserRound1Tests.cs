using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using TrustCabinet.Application.Access;
using TrustCabinet.Application.Audit;
using TrustCabinet.Application.Authentication;
using TrustCabinet.Application.CalendarSharing;
using TrustCabinet.Application.Users;
using TrustCabinet.Application.VoiceId;
using TrustCabinet.Domain.Access;
using TrustCabinet.Domain.Entities;
using TrustCabinet.Domain.Enums;
using TrustCabinet.Web.Tests.Authentication;

namespace TrustCabinet.Web.Tests.Users;

/// <summary>
/// Round 1 gap tests for FE-02 (User Management, UC-06 to UC-08): the cases the report marked
/// "Manual only" (401s, the length limits, deactivation killing a reset link), plus the Expected
/// lines the cited tests did not pin (the MSG09 validation bodies, the 401 halves of the gate
/// cases, the sessions ending on an email change, and the refresh-then-/me view after a role
/// change). Same fakes as <see cref="UserEndpointsTests"/>, no PostgreSQL and no Redis.
/// </summary>
public class UserRound1Tests : IDisposable
{
    private const string ValidPassword = "MatKhauDaiVaAnToan@2026";
    private const string NewPassword = "MotMatKhauMoiRatDai@2026";

    private readonly Factory _factory = new();

    public void Dispose() => _factory.Dispose();

    private sealed class Factory : TrustCabinetApiFactory
    {
        internal FakeUserAccountRepository Accounts { get; } = new();

        internal FakeUserRepository Users { get; } = new();

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
                services.RemoveAll<IUserRepository>();
                services.AddSingleton<IUserRepository>(Users);

                services.RemoveAll<IUserAccountRepository>();
                services.AddSingleton<IUserAccountRepository>(Accounts);

                services.RemoveAll<IAdministratorInvariantGuard>();
                services.AddSingleton<IAdministratorInvariantGuard>(Invariant);

                services.RemoveAll<ICalendarShareStopper>();
                services.AddSingleton<ICalendarShareStopper>(new FakeCalendarShareStopper());

                services.RemoveAll<IAuditService>();
                services.AddSingleton<IAuditService>(Audit);

                services.RemoveAll<IRefreshTokenStore>();
                services.AddSingleton<IRefreshTokenStore>(RefreshTokens);

                services.RemoveAll<IPasswordResetTokenStore>();
                services.AddSingleton<IPasswordResetTokenStore>(ResetTokens);
                services.RemoveAll<IPasswordResetEmailSender>();
                services.AddSingleton<IPasswordResetEmailSender>(ResetEmail);

                services.RemoveAll<IVoiceIdErasure>();
                services.AddSingleton<IVoiceIdErasure>(new FakeVoiceIdErasure());

                services.RemoveAll<IPasswordHasher>();
                services.AddSingleton<IPasswordHasher, FakePasswordHasher>();
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

        _factory.Users.WithUser(user).WithCapabilities(user.RoleId, DefaultCaps(user.RoleId));
        _factory.Accounts.WithUser(user);
        return user;
    }

    private static Capability[] DefaultCaps(Guid role)
    {
        if (role == BuiltInRoles.Admin) return [Capability.AdminUsers];
        if (role == BuiltInRoles.Approver) return [Capability.MeetingCreator, Capability.MeetingApprover];
        return [];
    }

    private HttpClient AdminClient()
        => _factory.ClientFor(
            GivenUser("Phạm Quản Trị", "quantri@trustcabinet.local", BuiltInRoles.Admin),
            Capability.AdminUsers);

    private static async Task<JsonElement> JsonOf(HttpResponseMessage response)
        => JsonDocument.Parse(await response.Content.ReadAsStringAsync()).RootElement;

    // Invalid profile fields are refused with MSG09 naming each field,
    // and the stored account keeps its old values.
    [Fact]
    public async Task Update_rejects_invalid_information_with_messages()
    {
        var target = GivenUser();
        var client = AdminClient();

        var response = await client.PutAsJsonAsync($"/api/users/{target.Id}", new
        {
            fullName = "",
            email = "khong-phai-email",
            roleId = BuiltInRoles.Member,
        });

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        var body = await JsonOf(response);
        Assert.Equal("MSG09", body.GetProperty("code").GetString());
        var errors = body.GetProperty("errors");
        Assert.Equal("Họ tên không được để trống.", errors.GetProperty("fullName").GetString());
        Assert.Equal("Email không hợp lệ.", errors.GetProperty("email").GetString());
        Assert.Equal("Lê Thành Viên", target.FullName);
        Assert.Equal("thanhvien@trustcabinet.local", target.Email);
    }

    // Reading one account without a token is refused before anything is read.
    [Fact]
    public async Task Get_answers_401_without_a_token()
    {
        var target = GivenUser();

        var response = await _factory.CreateClient().GetAsync($"/api/users/{target.Id}");

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    // Without AdminUsers the account is refused and none of its data is shown.
    [Fact]
    public async Task Get_answers_403_without_admin_users_and_shows_nothing()
    {
        var target = GivenUser();
        var client = _factory.ClientFor(
            GivenUser("Nguyễn Thư Ký", "thuky@trustcabinet.local", BuiltInRoles.Approver),
            Capability.MeetingCreator);

        var response = await client.GetAsync($"/api/users/{target.Id}");
        var body = await response.Content.ReadAsStringAsync();

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
        Assert.DoesNotContain(target.Email, body, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain(target.FullName, body);
    }

    // An over-long name and an over-long email are refused with MSG09, naming
    // the field, and no account is created.
    [Fact]
    public async Task Create_rejects_overlong_name_and_email_with_MSG09()
    {
        var client = AdminClient();
        var before = _factory.Accounts.Users.Count;

        var tooLongName = await client.PostAsJsonAsync("/api/users", new
        {
            fullName = new string('H', 201),
            email = "hople@trustcabinet.local",
            password = ValidPassword,
            roleId = BuiltInRoles.Member,
        });
        Assert.Equal(HttpStatusCode.BadRequest, tooLongName.StatusCode);
        var nameErrors = (await JsonOf(tooLongName)).GetProperty("errors");
        Assert.Equal("MSG09", (await JsonOf(tooLongName)).GetProperty("code").GetString());
        Assert.Equal("Họ tên không được vượt quá 200 ký tự.", nameErrors.GetProperty("fullName").GetString());

        var tooLongEmail = await client.PostAsJsonAsync("/api/users", new
        {
            fullName = "Tên hợp lệ",
            email = new string('e', 246) + "@test.local",
            password = ValidPassword,
            roleId = BuiltInRoles.Member,
        });
        Assert.Equal(HttpStatusCode.BadRequest, tooLongEmail.StatusCode);
        var emailBody = await JsonOf(tooLongEmail);
        Assert.Equal("MSG09", emailBody.GetProperty("code").GetString());
        Assert.Equal("Email không hợp lệ.", emailBody.GetProperty("errors").GetProperty("email").GetString());

        Assert.Equal(before, _factory.Accounts.Users.Count);
    }

    // Every invalid field is reported at once, with its message.
    [Fact]
    public async Task Create_reports_every_invalid_field_with_its_message()
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
        var body = await JsonOf(response);
        Assert.Equal("MSG09", body.GetProperty("code").GetString());
        var errors = body.GetProperty("errors");
        Assert.Equal("Họ tên không được để trống.", errors.GetProperty("fullName").GetString());
        Assert.Equal("Email không hợp lệ.", errors.GetProperty("email").GetString());
        Assert.Equal("Mật khẩu phải có ít nhất 12 ký tự.", errors.GetProperty("password").GetString());
    }

    // Creating an account without a token is refused before anything is stored.
    [Fact]
    public async Task Create_answers_401_without_a_token_and_stores_nothing()
    {
        var before = _factory.Accounts.Users.Count;

        var response = await _factory.CreateClient().PostAsJsonAsync("/api/users", new
        {
            fullName = "Trần Chủ Toạ",
            email = "chutoa@trustcabinet.local",
            password = ValidPassword,
            roleId = BuiltInRoles.Approver,
        });

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
        Assert.Equal(before, _factory.Accounts.Users.Count);
    }

    // Deactivating the account after the link was sent kills the link, even
    // after the reactivation — 400 MSG93, without saying what became of the account.
    [Fact]
    public async Task Reset_link_dies_when_the_account_was_deactivated_after_the_send()
    {
        var target = GivenUser();
        var admin = AdminClient();

        var sent = await admin.PostAsync($"/api/users/{target.Id}/password-reset", null);
        Assert.Equal(HttpStatusCode.OK, sent.StatusCode);
        var token = Assert.Single(_factory.ResetEmail.Sent).Token;

        var deactivated = await admin.PutAsJsonAsync(
            $"/api/users/{target.Id}/status", new { isActive = false });
        Assert.Equal(HttpStatusCode.OK, deactivated.StatusCode);
        var reactivated = await admin.PutAsJsonAsync(
            $"/api/users/{target.Id}/status", new { isActive = true });
        Assert.Equal(HttpStatusCode.OK, reactivated.StatusCode);

        var response = await _factory.CreateClient().PostAsJsonAsync(
            "/api/auth/password-reset", new { token, newPassword = NewPassword });

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Equal("MSG93", (await JsonOf(response)).GetProperty("code").GetString());
        Assert.True(new FakePasswordHasher().Verify(target.PasswordHash!, ValidPassword));
    }

    // Editing an account without a token is refused (401, not 403).
    [Fact]
    public async Task Update_answers_401_without_a_token()
    {
        var target = GivenUser();

        var response = await _factory.CreateClient().PutAsJsonAsync($"/api/users/{target.Id}", new
        {
            fullName = "Tên khác",
            email = target.Email,
            roleId = BuiltInRoles.Member,
        });

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    // Changing the status without a token is refused (401, not 403).
    [Fact]
    public async Task Status_change_answers_401_without_a_token()
    {
        var target = GivenUser();

        var response = await _factory.CreateClient().PutAsJsonAsync(
            $"/api/users/{target.Id}/status", new { isActive = false });

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
        Assert.True(target.IsActive);
    }

    // A status body that does not state the status is refused with MSG09,
    // naming isActive, and nothing happens.
    [Fact]
    public async Task Status_change_refuses_a_body_without_isActive_with_MSG09()
    {
        var target = GivenUser();
        var client = AdminClient();

        var response = await client.PutAsJsonAsync(
            $"/api/users/{target.Id}/status", new { active = true });

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        var body = await JsonOf(response);
        Assert.Equal("MSG09", body.GetProperty("code").GetString());
        Assert.Equal(
            "Thiếu trạng thái cần đặt cho tài khoản.",
            body.GetProperty("errors").GetProperty("isActive").GetString());
        Assert.True(target.IsActive);
        Assert.Empty(_factory.Audit.Records);
    }

    // Requesting a reset link without a token is refused (401, not 403).
    [Fact]
    public async Task Reset_request_answers_401_without_a_token()
    {
        var target = GivenUser();

        var response = await _factory.CreateClient()
            .PostAsync($"/api/users/{target.Id}/password-reset", null);

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
        Assert.Empty(_factory.ResetEmail.Sent);
    }

    // Changing the email ends the sessions — the old cookie no longer
    // refreshes (401 MSG01), while the account itself keeps working.
    [Fact]
    public async Task Changing_the_email_ends_the_sessions()
    {
        var target = GivenUser();
        await _factory.RefreshTokens.StoreAsync("rt-cu-cua-ho", target.Id, TimeSpan.FromDays(7));
        var client = AdminClient();

        var renamed = await client.PutAsJsonAsync($"/api/users/{target.Id}", new
        {
            fullName = target.FullName,
            email = "diachimoi@trustcabinet.local",
            roleId = BuiltInRoles.Member,
        });
        Assert.Equal(HttpStatusCode.OK, renamed.StatusCode);

        var request = new HttpRequestMessage(HttpMethod.Post, "/api/auth/refresh");
        request.Headers.Add("Cookie", "tc_refresh_token=rt-cu-cua-ho");
        var refresh = await _factory.CreateClient().SendAsync(request);

        Assert.Equal(HttpStatusCode.Unauthorized, refresh.StatusCode);
        Assert.Equal("MSG01", (await JsonOf(refresh)).GetProperty("code").GetString());
    }

    // After a role change the next refresh succeeds and /api/auth/me
    // lists the capabilities of the new role.
    [Fact]
    public async Task Changed_role_reaches_refresh_and_me()
    {
        var target = GivenUser();
        await _factory.RefreshTokens.StoreAsync("rt-cua-thanh-vien", target.Id, TimeSpan.FromDays(7));
        var client = AdminClient();

        var changed = await client.PutAsJsonAsync($"/api/users/{target.Id}", new
        {
            fullName = target.FullName,
            email = target.Email,
            roleId = BuiltInRoles.Approver,
        });
        Assert.Equal(HttpStatusCode.OK, changed.StatusCode);

        var refreshRequest = new HttpRequestMessage(HttpMethod.Post, "/api/auth/refresh");
        refreshRequest.Headers.Add("Cookie", "tc_refresh_token=rt-cua-thanh-vien");
        var refresh = await _factory.CreateClient().SendAsync(refreshRequest);
        Assert.Equal(HttpStatusCode.OK, refresh.StatusCode);
        var accessToken = (await JsonOf(refresh)).GetProperty("accessToken").GetString();

        using var me = _factory.CreateClient();
        me.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", accessToken);
        var meResponse = await me.GetAsync("/api/auth/me");
        Assert.Equal(HttpStatusCode.OK, meResponse.StatusCode);
        var capabilities = (await JsonOf(meResponse)).GetProperty("capabilities")
            .EnumerateArray().Select(c => c.GetString()).ToArray();
        Assert.Contains("MeetingApprover", capabilities);
    }

    // After the reset the new password signs in and the
    // old one answers MSG01.
    [Fact]
    public async Task After_reset_the_new_password_signs_in_and_the_old_one_does_not()
    {
        var target = GivenUser();
        await AdminClient().PostAsync($"/api/users/{target.Id}/password-reset", null);
        var token = Assert.Single(_factory.ResetEmail.Sent).Token;

        var reset = await _factory.CreateClient().PostAsJsonAsync(
            "/api/auth/password-reset", new { token, newPassword = NewPassword });
        Assert.Equal(HttpStatusCode.OK, reset.StatusCode);

        var loginNew = await _factory.CreateClient().PostAsJsonAsync(
            "/api/auth/login", new { email = target.Email, password = NewPassword });
        Assert.Equal(HttpStatusCode.OK, loginNew.StatusCode);

        var loginOld = await _factory.CreateClient().PostAsJsonAsync(
            "/api/auth/login", new { email = target.Email, password = ValidPassword });
        Assert.Equal(HttpStatusCode.Unauthorized, loginOld.StatusCode);
        Assert.Equal("MSG01", (await JsonOf(loginOld)).GetProperty("code").GetString());
    }
}
