using System.Net;
using System.Net.Http.Json;
using System.Text;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using TrustCabinet.Domain.Audit;
using TrustCabinet.Domain.Entities;
using TrustCabinet.Domain.Enums;
using static TrustCabinet.Web.Tests.Agenda.AgendaEndpointTests;

namespace TrustCabinet.Web.Tests.Summaries;

/// <summary>
/// ADR-0056 — the administrator's "Máy AI" screen (GET/PUT /api/admin/ai, POST /api/admin/ai/test) and where a draft is
/// then written: the API for an ordinary meeting, the in-house model for every other one.
/// </summary>
public sealed class AiSettingsEndpointTests(SummaryFixture f) : IClassFixture<SummaryFixture>
{
    private const string Url = "/api/admin/ai";
    private const string Key = "sk-zen-test-key-0123456789abcd";

    private static object Api(string? key = Key, string url = "https://api.example.test/zen/v1", string model = "deepseek-v4.1-flash")
        => new { mode = "Api", apiBaseUrl = url, apiModel = model, apiKey = key };

    [Fact]
    public async Task Only_an_administrator_reads_or_changes_where_drafts_are_written()
    {
        var s = await f.Seed();
        using var anonymous = f.Client();
        using var secretary = f.Client(s.Secretary, nameof(Capability.MeetingCreator));
        Assert.Equal(HttpStatusCode.Unauthorized, (await anonymous.GetAsync(Url)).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, (await secretary.GetAsync(Url)).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, (await secretary.PutAsJsonAsync(Url, Api())).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, (await secretary.PostAsJsonAsync(Url + "/test", Api())).StatusCode);
        // A token that claims the capability while the database says otherwise is refused too.
        using var claims = f.Client(s.Secretary, nameof(Capability.AdminUsers));
        Assert.Equal(HttpStatusCode.Forbidden, (await claims.GetAsync(Url)).StatusCode);
    }

    [Fact]
    public async Task Without_a_choice_the_in_house_model_writes_every_draft()
    {
        using var admin = await AdminWithRole();
        var view = await admin.GetFromJsonAsync<JsonElement>(Url);
        Assert.Equal("Local", view.GetProperty("mode").GetString());
        Assert.False(view.GetProperty("hasApiKey").GetBoolean());
        Assert.Equal("", view.GetProperty("apiKeyHint").GetString());
    }

    [Fact]
    public async Task The_key_is_stored_encrypted_and_only_its_last_four_characters_come_back()
    {
        using var admin = await AdminWithRole();
        var raw = await (await admin.PutAsJsonAsync(Url, Api())).Content.ReadAsStringAsync();
        Assert.DoesNotContain(Key, raw);
        var saved = JsonDocument.Parse(raw).RootElement;
        Assert.Equal("MSG408", saved.GetProperty("code").GetString());
        var settings = saved.GetProperty("settings");
        Assert.Equal("Api", settings.GetProperty("mode").GetString());
        Assert.True(settings.GetProperty("hasApiKey").GetBoolean());
        Assert.Equal("abcd", settings.GetProperty("apiKeyHint").GetString());
        Assert.DoesNotContain(Key, await admin.GetStringAsync(Url));

        await using var db = f.Db();
        var row = await db.AiProviderSettings.SingleAsync();
        Assert.NotNull(row.ApiKeyCiphertext);
        Assert.DoesNotContain(Key, Encoding.UTF8.GetString(row.ApiKeyCiphertext!));
        var audit = await db.AuditLog.Where(a => a.Action == AuditActions.AiSettingsUpdated).OrderBy(a => a.OccurredAt).LastAsync();
        Assert.DoesNotContain(Key, audit.Details);
        Assert.Contains("\"replaced\"", audit.Details);

        // Saved again without a key: the stored one stays. Back to local: the API's address and model stay for later.
        await AssertCode(await admin.PutAsJsonAsync(Url, Api(key: null)), 200, "MSG408");
        var local = await AssertCode(await admin.PutAsJsonAsync(Url,
            new { mode = "Local", apiBaseUrl = "https://api.example.test/zen/v1", apiModel = "deepseek-v4.1-flash", apiKey = (string?)null }), 200, "MSG408");
        Assert.Equal("Local", local.GetProperty("settings").GetProperty("mode").GetString());
        Assert.True(local.GetProperty("settings").GetProperty("hasApiKey").GetBoolean());
        Assert.Equal("deepseek-v4.1-flash", local.GetProperty("settings").GetProperty("apiModel").GetString());
        // Removed while local.
        var removed = await AssertCode(await admin.PutAsJsonAsync(Url,
            new { mode = "Local", apiBaseUrl = "", apiModel = "", apiKey = "" }), 200, "MSG408");
        Assert.False(removed.GetProperty("settings").GetProperty("hasApiKey").GetBoolean());
    }

    [Fact]
    public async Task An_api_needs_an_https_address_a_model_and_a_key()
    {
        using var admin = await AdminWithRole();
        var errors = (await AssertCode(await admin.PutAsJsonAsync(Url, Api(key: null, url: "http://api.example.test/v1", model: "")), 400, "MSG409"))
            .GetProperty("errors");
        Assert.True(errors.TryGetProperty("apiBaseUrl", out _));
        Assert.True(errors.TryGetProperty("apiModel", out _));
        Assert.True(errors.TryGetProperty("apiKey", out _));
        var spaced = (await AssertCode(await admin.PutAsJsonAsync(Url, Api(key: "sk with spaces")), 400, "MSG409")).GetProperty("errors");
        Assert.True(spaced.TryGetProperty("apiKey", out _));
        var mode = (await AssertCode(await admin.PutAsJsonAsync(Url, new { mode = "api" }), 400, "MSG409")).GetProperty("errors");
        Assert.True(mode.TryGetProperty("mode", out _));
        await AssertCode(await admin.PutAsJsonAsync(Url, Api()), 200, "MSG408");
        // Using the API, its key cannot be removed.
        await AssertCode(await admin.PutAsJsonAsync(Url, Api(key: "")), 400, "MSG409");
        await using var db = f.Db();
        Assert.Equal(AiProviderMode.Api, (await db.AiProviderSettings.SingleAsync()).Mode);
    }

    [Fact]
    public async Task With_the_api_chosen_an_ordinary_meeting_is_written_by_it_and_a_confidential_one_never_leaves()
    {
        using var admin = await AdminWithRole();
        await AssertCode(await admin.PutAsJsonAsync(Url, Api()), 200, "MSG408");

        var normal = await f.Seed();
        using var secretary = f.Client(normal.Secretary);
        await AssertCode(await secretary.PostAsync(normal.Summary, null), 202, "MSG384");
        await f.ProcessAll();
        Assert.NotEmpty(f.Api.Requests);
        Assert.Empty(f.Model.Prompts);
        Assert.All(f.Api.Requests, r =>
        {
            Assert.Equal("https://api.example.test/zen/v1/chat/completions", r.Url);
            Assert.Equal($"Bearer {Key}", r.Authorization);
            Assert.Contains("\"model\":\"deepseek-v4.1-flash\"", r.Body);
        });
        var draft = (await secretary.GetFromJsonAsync<JsonElement>(normal.Summary)).GetProperty("summary");
        Assert.Equal("deepseek-v4.1-flash", draft.GetProperty("model").GetString());
        Assert.Equal("Mục tiêu do API viết.", draft.GetProperty("draft").GetProperty("objectives").GetString());

        foreach (var sensitivity in new[] { Sensitivity.Internal, Sensitivity.Confidential, Sensitivity.Secret })
        {
            f.Api.Reset();
            f.Model.Reset();
            var kept = await f.Seed(sensitivity: sensitivity);
            using var keeper = f.Client(kept.Secretary);
            await AssertCode(await keeper.PostAsync(kept.Summary, null), 202, "MSG384");
            await f.ProcessAll();
            Assert.Empty(f.Api.Requests);
            Assert.NotEmpty(f.Model.Prompts);
            Assert.Equal("fake-llm@test", (await keeper.GetFromJsonAsync<JsonElement>(kept.Summary)).GetProperty("summary").GetProperty("model").GetString());
        }
    }

    [Fact]
    public async Task A_confidential_meeting_reads_as_not_ready_when_only_the_api_could_write()
    {
        using var admin = await AdminWithRole();
        await AssertCode(await admin.PutAsJsonAsync(Url, Api()), 200, "MSG408");
        f.Model.Configured = false;
        var normal = await f.Seed();
        var secret = await f.Seed(sensitivity: Sensitivity.Confidential);
        using var a = f.Client(normal.Secretary);
        using var b = f.Client(secret.Secretary);
        Assert.True((await a.GetFromJsonAsync<JsonElement>(normal.Summary)).GetProperty("aiReady").GetBoolean());
        Assert.False((await b.GetFromJsonAsync<JsonElement>(secret.Summary)).GetProperty("aiReady").GetBoolean());
        await AssertCode(await b.PostAsync(secret.Summary, null), 503, "MSG394");
        f.Model.Reset();
    }

    [Fact]
    public async Task The_test_call_uses_the_form_or_the_stored_key_and_reports_the_api_s_refusal()
    {
        using var admin = await AdminWithRole();
        var passed = await AssertCode(await admin.PostAsJsonAsync(Url + "/test", Api()), 200, "MSG412");
        Assert.Equal("deepseek-v4.1-flash", passed.GetProperty("model").GetString());
        Assert.Equal($"Bearer {Key}", f.Api.Requests.Single().Authorization);
        // OpenCode Go refuses a call without a session id (400 "missing x-opencode-session").
        Assert.True(Guid.TryParse(f.Api.Requests.Single().Session, out _));
        // Nothing was saved by the test.
        await using (var db = f.Db()) Assert.False(await db.AiProviderSettings.AnyAsync());

        // No key in the form and none stored.
        await AssertCode(await admin.PostAsJsonAsync(Url + "/test", Api(key: null)), 400, "MSG409");
        await AssertCode(await admin.PutAsJsonAsync(Url, Api()), 200, "MSG408");
        f.Api.Reset();
        await AssertCode(await admin.PostAsJsonAsync(Url + "/test", Api(key: null)), 200, "MSG412");
        Assert.Equal($"Bearer {Key}", f.Api.Requests.Single().Authorization);

        f.Api.Status = HttpStatusCode.Unauthorized;
        var refused = await AssertCode(await admin.PostAsJsonAsync(Url + "/test", Api(key: null)), 502, "MSG413");
        Assert.Contains("từ chối key", refused.GetProperty("message").GetString());

        // A paid model on an account without credit says so instead of a bare status code.
        // The provider's own reason follows, read from the OpenAI error shape.
        f.Api.Status = HttpStatusCode.PaymentRequired;
        f.Api.ErrorBody = """{"error":{"message":"Insufficient account funds","type":"billing"}}""";
        var unpaid = await AssertCode(await admin.PostAsJsonAsync(Url + "/test", Api(key: null)), 502, "MSG413");
        Assert.Contains("đòi thanh toán (402)", unpaid.GetProperty("message").GetString());
        Assert.EndsWith("Nhà cung cấp: Insufficient account funds", unpaid.GetProperty("message").GetString());
        // A body that is not JSON adds nothing.
        f.Api.Status = HttpStatusCode.InternalServerError;
        f.Api.ErrorBody = "<html>Bad gateway</html>";
        var broken = await AssertCode(await admin.PostAsJsonAsync(Url + "/test", Api(key: null)), 502, "MSG413");
        Assert.Equal("API AI trả lỗi 500.", broken.GetProperty("message").GetString());
        f.Api.Reset();
    }

    [Fact]
    public async Task A_key_that_can_no_longer_be_read_sends_nothing_out()
    {
        using var admin = await AdminWithRole();
        await AssertCode(await admin.PutAsJsonAsync(Url, Api()), 200, "MSG408");
        await using (var db = f.Db())
            await db.AiProviderSettings.ExecuteUpdateAsync(u => u.SetProperty(s => s.ApiKeyCiphertext, new byte[40]));
        var s = await f.Seed();
        using var secretary = f.Client(s.Secretary);
        await AssertCode(await secretary.PostAsync(s.Summary, null), 202, "MSG384");
        await f.ProcessAll();
        Assert.Empty(f.Api.Requests);
        Assert.NotEmpty(f.Model.Prompts);
    }

    [Fact]
    public async Task Database_outages_have_their_codes()
    {
        using var admin = await AdminWithRole();
        f.Failures.FailAiSettingsRead = true;
        try { await AssertCode(await admin.GetAsync(Url), 500, "MSG410"); }
        finally { f.Failures.FailAiSettingsRead = false; }
        f.Failures.FailSave = true;
        try { await AssertCode(await admin.PutAsJsonAsync(Url, Api()), 503, "MSG411"); }
        finally { f.Failures.FailSave = false; }
        await using var db = f.Db();
        Assert.False(await db.AiProviderSettings.AnyAsync());
    }

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
}
