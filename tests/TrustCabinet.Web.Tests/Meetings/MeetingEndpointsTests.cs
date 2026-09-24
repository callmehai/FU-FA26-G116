using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using TrustCabinet.Domain.Access;
using TrustCabinet.Domain.Authentication;
using TrustCabinet.Domain.Entities;
using TrustCabinet.Domain.Enums;
using TrustCabinet.Domain.Meetings;
using TrustCabinet.Domain.Users;
using TrustCabinet.Infrastructure.Meetings;
using TrustCabinet.Infrastructure.Seed;
using TrustCabinet.Web.Endpoints;
using TrustCabinet.Web.Tests.Authentication;
using TrustCabinet.Web.Tests.Users;

namespace TrustCabinet.Web.Tests.Meetings;

public sealed class MeetingEndpointsTests : IDisposable
{
    private readonly Factory _factory = new();

    public void Dispose() => _factory.Dispose();

    private sealed class Factory : TrustCabinetApiFactory
    {
        internal FakeMeetingRepository Meetings { get; } = new();

        internal FakeUserAccountRepository Accounts { get; } = new();

        protected override void ConfigureWebHost(IWebHostBuilder builder)
        {
            base.ConfigureWebHost(builder);
            builder.ConfigureServices(services =>
            {
                services.RemoveAll<IUserAccountRepository>();
                services.AddSingleton<IUserAccountRepository>(Accounts);
                services.RemoveAll<IMeetingRepository>();
                services.AddSingleton<IMeetingRepository>(Meetings);
                services.RemoveAll<IMeetingService>();
                services.AddScoped<IMeetingService, MeetingService>();
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

    private User GivenCreator(string email = "meeting-creator@trustcabinet.local")
    {
        var user = new User
        {
            FullName = "Nguyễn Thư Ký",
            Email = email,
            PasswordHash = "test-hash",
            RoleId = BuiltInRoles.Secretary,
        };
        _factory.Accounts.WithUser(user);
        return user;
    }

    private static MeetingRequest ValidRequest(string? title = "Giao ban tuần")
        => new(
            title,
            "2026-10-01T09:00:00+07:00",
            "2026-10-01T10:00:00+07:00",
            "InPerson",
            "Phòng họp A",
            "Internal",
            "Kế hoạch tuần.",
            Guid.CreateVersion7());

    private static async Task<JsonDocument> JsonOf(HttpResponseMessage response)
        => JsonDocument.Parse(await response.Content.ReadAsStringAsync());

    [Fact]
    public async Task Post_creates_a_draft_and_returns_a_location_that_get_can_read()
    {
        var creator = GivenCreator();
        using var client = _factory.ClientFor(creator, Capability.MeetingCreator);

        var created = await client.PostAsJsonAsync("/api/meetings", ValidRequest());
        using var body = await JsonOf(created);
        var meetingId = body.RootElement.GetProperty("meeting").GetProperty("id").GetGuid();

        Assert.Equal(HttpStatusCode.Created, created.StatusCode);
        Assert.Equal($"/api/meetings/{meetingId}", created.Headers.Location!.OriginalString);
        Assert.Equal("MEETING_CREATED", body.RootElement.GetProperty("code").GetString());
        Assert.Equal("Draft", body.RootElement.GetProperty("meeting").GetProperty("status").GetString());
        Assert.Equal(creator.Id, body.RootElement.GetProperty("meeting").GetProperty("createdById").GetGuid());

        var read = await client.GetAsync($"/api/meetings/{meetingId}");
        using var readBody = await JsonOf(read);
        Assert.Equal(HttpStatusCode.OK, read.StatusCode);
        Assert.Equal(meetingId, readBody.RootElement.GetProperty("id").GetGuid());
    }

    [Fact]
    public async Task Post_returns_all_validation_errors_without_writing()
    {
        using var client = _factory.ClientFor(GivenCreator(), Capability.MeetingCreator);

        var response = await client.PostAsJsonAsync(
            "/api/meetings", ValidRequest(title: "   ") with { Mode = "Wrong" });
        using var body = await JsonOf(response);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Equal("MEETING_INVALID_INFORMATION", body.RootElement.GetProperty("code").GetString());
        Assert.True(body.RootElement.GetProperty("errors").TryGetProperty("title", out _));
        Assert.True(body.RootElement.GetProperty("errors").TryGetProperty("mode", out _));
        Assert.Empty(_factory.Meetings.Meetings);
    }

    [Fact]
    public async Task Missing_and_other_owner_resources_return_the_same_empty_404()
    {
        var creator = GivenCreator();
        var other = GivenCreator("other-meeting-creator@trustcabinet.local");
        using var creatorClient = _factory.ClientFor(creator, Capability.MeetingCreator);
        using var otherClient = _factory.ClientFor(other, Capability.MeetingCreator);
        var create = await creatorClient.PostAsJsonAsync("/api/meetings", ValidRequest());
        using var createBody = await JsonOf(create);
        var id = createBody.RootElement.GetProperty("meeting").GetProperty("id").GetGuid();

        var ownedByOther = await otherClient.GetAsync($"/api/meetings/{id}");
        var nonexistent = await otherClient.GetAsync($"/api/meetings/{Guid.CreateVersion7()}");
        var updateOther = await otherClient.PutAsJsonAsync($"/api/meetings/{id}", ValidRequest());

        Assert.Equal(HttpStatusCode.NotFound, ownedByOther.StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, nonexistent.StatusCode);
        Assert.Empty(await ownedByOther.Content.ReadAsStringAsync());
        Assert.Empty(await nonexistent.Content.ReadAsStringAsync());
        Assert.Equal(HttpStatusCode.NotFound, updateOther.StatusCode);
    }

    [Fact]
    public async Task Put_updates_an_owned_draft_and_ignores_client_owned_fields()
    {
        var creator = GivenCreator();
        using var client = _factory.ClientFor(creator, Capability.MeetingCreator);
        var create = await client.PostAsJsonAsync("/api/meetings", ValidRequest());
        using var createBody = await JsonOf(create);
        var id = createBody.RootElement.GetProperty("meeting").GetProperty("id").GetGuid();

        var update = await client.PutAsJsonAsync($"/api/meetings/{id}", new
        {
            title = "Đã cập nhật",
            startsAt = "2026-10-01T09:30:00+07:00",
            endsAt = "2026-10-01T10:30:00+07:00",
            mode = "Hybrid",
            location = "Phòng họp B",
            sensitivity = "Secret",
            description = "Nội dung mới",
            status = "Published",
            createdById = Guid.CreateVersion7(),
        });
        using var updateBody = await JsonOf(update);

        Assert.Equal(HttpStatusCode.OK, update.StatusCode);
        Assert.Equal("MSG02", updateBody.RootElement.GetProperty("code").GetString());
        Assert.Equal("Đã cập nhật", updateBody.RootElement.GetProperty("meeting").GetProperty("title").GetString());
        Assert.Equal("Hybrid", updateBody.RootElement.GetProperty("meeting").GetProperty("mode").GetString());
        Assert.Equal("Draft", updateBody.RootElement.GetProperty("meeting").GetProperty("status").GetString());
        Assert.Equal(creator.Id, updateBody.RootElement.GetProperty("meeting").GetProperty("createdById").GetGuid());
    }

    [Fact]
    public async Task Put_returns_MSG03_if_the_meeting_is_not_editable()
    {
        var creator = GivenCreator();
        using var client = _factory.ClientFor(creator, Capability.MeetingCreator);
        var meeting = new Meeting
        {
            Title = "Đã gửi duyệt",
            Description = string.Empty,
            Status = MeetingStatus.WaitingApproval,
            Sensitivity = Sensitivity.Normal,
            Mode = MeetingMode.InPerson,
            CreatedById = creator.Id,
            StartsAt = new DateTimeOffset(2026, 10, 1, 2, 0, 0, TimeSpan.Zero),
            EndsAt = new DateTimeOffset(2026, 10, 1, 3, 0, 0, TimeSpan.Zero),
        };
        _factory.Meetings.Meetings.Add(meeting);

        var response = await client.PutAsJsonAsync($"/api/meetings/{meeting.Id}", ValidRequest(title: ""));
        using var body = await JsonOf(response);

        Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);
        Assert.Equal("MSG03", body.RootElement.GetProperty("code").GetString());
        Assert.Equal("Đã gửi duyệt", meeting.Title);
    }

    [Fact]
    public async Task Anonymous_and_callers_without_MeetingCreator_are_rejected_by_the_gate()
    {
        using var anonymous = _factory.CreateClient();
        var creator = GivenCreator("wrong-capability@trustcabinet.local");
        using var wrongCapability = _factory.ClientFor(creator, Capability.AdminUsers);

        var anonymousResponse = await anonymous.GetAsync($"/api/meetings/{Guid.CreateVersion7()}");
        var forbiddenResponse = await wrongCapability.PostAsJsonAsync("/api/meetings", ValidRequest());

        Assert.Equal(HttpStatusCode.Unauthorized, anonymousResponse.StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, forbiddenResponse.StatusCode);
    }

    [Fact]
    public async Task Create_save_failure_returns_MSG01_and_503()
    {
        using var client = _factory.ClientFor(GivenCreator(), Capability.MeetingCreator);
        _factory.Meetings.NextSaveThrows = new Npgsql.NpgsqlException("database unavailable");

        var response = await client.PostAsJsonAsync("/api/meetings", ValidRequest());
        using var body = await JsonOf(response);

        Assert.Equal(HttpStatusCode.ServiceUnavailable, response.StatusCode);
        Assert.Equal("MSG01", body.RootElement.GetProperty("code").GetString());
        Assert.Empty(_factory.Meetings.Meetings);
    }
}
