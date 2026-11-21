using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using TrustCabinet.Domain.Entities;
using TrustCabinet.Domain.Enums;
using static TrustCabinet.Web.Tests.Agenda.AgendaEndpointTests;

namespace TrustCabinet.Web.Tests.Summaries;

/// <summary>Round 1 gap coverage for FE-10: the administrator's AI settings boundaries and the
/// capability re-read behind GET /api/admin/ai.</summary>
public sealed class AiCoverageTests(SummaryFixture f) : IClassFixture<SummaryFixture>
{
    private const string Url = "/api/admin/ai";

    /// <summary>An administrator whose role really holds AdminUsers, as the gate re-reads it from the database.</summary>
    private async Task<HttpClient> AdminWithRole()
    {
        await using var db = f.Db();
        await db.AiProviderSettings.ExecuteDeleteAsync();
        f.Model.Reset();
        f.Api.Reset();
        var role = new Role { Name = "AI-admin-" + Guid.NewGuid() };
        db.Roles.Add(role);
        db.RoleCapabilities.Add(new RoleCapability { RoleId = role.Id, Capability = Capability.AdminUsers });
        var user = new User { FullName = "Phạm Quản Trị", Email = $"{Guid.NewGuid():N}@summary.test",
            PasswordHash = "test-only-non-login-hash", RoleId = role.Id };
        db.Users.Add(user);
        await db.SaveChangesAsync();
        return f.Client(user.Id, nameof(Capability.AdminUsers));
    }

    [Fact]
    public async Task A_role_that_lost_admin_users_is_refused_with_its_old_token()
    {
        // TRANS_API_IT165: the capability is re-read from the database, not taken from the token.
        using var admin = await AdminWithRole();
        Assert.Equal(HttpStatusCode.OK, (await admin.GetAsync(Url)).StatusCode);
        await using (var db = f.Db())
            await db.RoleCapabilities.Where(rc => rc.Capability == Capability.AdminUsers).ExecuteDeleteAsync();
        var refused = await admin.GetAsync(Url);
        Assert.Equal(HttpStatusCode.Forbidden, refused.StatusCode);
    }

    [Fact]
    public async Task Api_mode_needs_an_address_and_a_known_mode()
    {
        // TRANS_API_IT170.
        using var admin = await AdminWithRole();
        var missing = await AssertCode(await admin.PutAsJsonAsync(Url, new
        {
            mode = "Api", apiBaseUrl = "", apiModel = "model-x", apiKey = "khoa-kiem-tra-hop-le-123",
        }), 400, "MSG409");
        Assert.Equal("Nhập địa chỉ API.",
            missing.GetProperty("errors").GetProperty("apiBaseUrl")[0].GetString());
        var mode = await AssertCode(await admin.PutAsJsonAsync(Url, new
        {
            mode = "Cloud", apiBaseUrl = "https://api.example.test/v1", apiModel = "model-x",
            apiKey = "khoa-kiem-tra-hop-le-123",
        }), 400, "MSG409");
        Assert.Equal("Chọn máy local hoặc API.", mode.GetProperty("errors").GetProperty("mode")[0].GetString());
        await using var db = f.Db();
        Assert.False(await db.AiProviderSettings.AnyAsync());
    }

    [Fact]
    public async Task Key_and_model_limits_are_checked_character_by_character()
    {
        // TRANS_API_IT171: keys of 8 to 500 characters without spaces, models of at most 100 without spaces.
        using var admin = await AdminWithRole();
        foreach (var key in new[] { "short", "khoa co khoang trang", new string('k', 501) })
        {
            var rejected = await AssertCode(await admin.PutAsJsonAsync(Url, new
            {
                mode = "Api", apiBaseUrl = "https://api.example.test/v1", apiModel = "model-x", apiKey = key,
            }), 400, "MSG409");
            Assert.Equal("Key dài 8–500 ký tự, không có khoảng trắng.",
                rejected.GetProperty("errors").GetProperty("apiKey")[0].GetString());
        }
        foreach (var model in new[] { "model x", new string('m', 101) })
        {
            var rejected = await AssertCode(await admin.PutAsJsonAsync(Url, new
            {
                mode = "Api", apiBaseUrl = "https://api.example.test/v1", apiModel = model,
                apiKey = "khoa-kiem-tra-hop-le-123",
            }), 400, "MSG409");
            Assert.Equal("Tên model không có khoảng trắng và không quá 100 ký tự.",
                rejected.GetProperty("errors").GetProperty("apiModel")[0].GetString());
        }
        var saved = await AssertCode(await admin.PutAsJsonAsync(Url, new
        {
            mode = "Api", apiBaseUrl = "https://api.example.test/v1", apiModel = new string('m', 100),
            apiKey = "12345678",
        }), 200, "MSG408");
        Assert.Equal("Api", saved.GetProperty("settings").GetProperty("mode").GetString());
    }

    [Fact]
    public async Task The_api_key_cannot_be_removed_while_the_api_is_in_use()
    {
        // TRANS_API_IT172.
        using var admin = await AdminWithRole();
        await AssertCode(await admin.PutAsJsonAsync(Url, new
        {
            mode = "Api", apiBaseUrl = "https://api.example.test/v1", apiModel = "model-x",
            apiKey = "khoa-kiem-tra-hop-le-123",
        }), 200, "MSG408");
        var refused = await AssertCode(await admin.PutAsJsonAsync(Url, new
        {
            mode = "Api", apiBaseUrl = "https://api.example.test/v1", apiModel = "model-x", apiKey = "",
        }), 400, "MSG409");
        Assert.Equal("Đang dùng API thì không xoá key được.",
            refused.GetProperty("errors").GetProperty("apiKey")[0].GetString());
        Assert.True((await admin.GetFromJsonAsync<JsonElement>(Url)).GetProperty("hasApiKey").GetBoolean());
    }

    [Fact]
    public async Task Testing_the_local_machine_reports_when_it_is_not_configured()
    {
        // TRANS_API_IT178: 502 MSG413 while the in-house model is not configured; nothing is saved.
        using var admin = await AdminWithRole();
        f.Model.Configured = false;
        try
        {
            var failed = await AssertCode(await admin.PostAsJsonAsync(Url + "/test", new
            {
                mode = "Local", apiBaseUrl = "", apiModel = "", apiKey = (string?)null,
            }), 502, "MSG413");
            Assert.Contains("chưa cấu hình máy local", failed.GetProperty("message").GetString());
        }
        finally
        {
            f.Model.Configured = true;
        }
        await using var db = f.Db();
        Assert.False(await db.AiProviderSettings.AnyAsync());
    }
}
