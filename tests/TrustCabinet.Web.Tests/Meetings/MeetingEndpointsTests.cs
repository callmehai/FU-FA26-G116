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

    private User GivenMember(string email)
    {
        var user = new User
        {
            FullName = "Lê Thành Viên",
            Email = email,
            PasswordHash = "test-hash",
            RoleId = BuiltInRoles.Member,
        };
        _factory.Accounts.WithUser(user);
        return user;
    }

    private static Meeting OwnedMeetingBy(User creator, Guid? approverId = null, Guid? participantId = null)
    {
        var meeting = new Meeting
        {
            Title = "Giao ban tuần",
            Description = "Kế hoạch tuần.",
            Status = MeetingStatus.Draft,
            Sensitivity = Sensitivity.Internal,
            Mode = MeetingMode.InPerson,
            Location = "Phòng họp A",
            StartsAt = new DateTimeOffset(2026, 10, 1, 2, 0, 0, TimeSpan.Zero),
            EndsAt = new DateTimeOffset(2026, 10, 1, 3, 0, 0, TimeSpan.Zero),
            CreatedById = creator.Id,
            AssignedApproverId = approverId,
        };

        if (participantId is { } id)
        {
            meeting.Participants.Add(new MeetingParticipant
            {
                MeetingId = meeting.Id,
                UserId = id,
            });
        }

        return meeting;
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
    public async Task Post_uses_defaults_and_accepts_an_omitted_optional_end_time()
    {
        var creator = GivenCreator();
        using var client = _factory.ClientFor(creator, Capability.MeetingCreator);
        var request = new
        {
            title = "Giao ban tuần",
            startsAt = "2026-10-01T09:00:00+07:00",
        };

        var response = await client.PostAsJsonAsync("/api/meetings", request);
        using var body = await JsonOf(response);
        var meeting = body.RootElement.GetProperty("meeting");

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        Assert.Equal("InPerson", meeting.GetProperty("mode").GetString());
        Assert.Equal("Normal", meeting.GetProperty("sensitivity").GetString());
        Assert.Equal(JsonValueKind.Null, meeting.GetProperty("endsAt").ValueKind);
        Assert.Equal(JsonValueKind.Null, meeting.GetProperty("assignedApproverId").ValueKind);
    }

    [Fact]
    public async Task Post_rejects_an_approver_without_the_required_capability()
    {
        var invalidApproverId = Guid.CreateVersion7();
        _factory.Meetings.IneligibleApproverIds.Add(invalidApproverId);
        using var client = _factory.ClientFor(GivenCreator(), Capability.MeetingCreator);

        var response = await client.PostAsJsonAsync(
            "/api/meetings", ValidRequest() with { AssignedApproverId = invalidApproverId });
        using var body = await JsonOf(response);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Equal("MEETING_INVALID_INFORMATION", body.RootElement.GetProperty("code").GetString());
        Assert.True(body.RootElement.GetProperty("errors").TryGetProperty("assignedApproverId", out _));
        Assert.Empty(_factory.Meetings.Meetings);
    }

    [Fact]
    public async Task Get_approvers_returns_the_repository_options()
    {
        var first = new MeetingApproverOption(Guid.CreateVersion7(), "An Nguyễn");
        var second = new MeetingApproverOption(Guid.CreateVersion7(), "Bình Trần");
        _factory.Meetings.EligibleApprovers.AddRange([first, second]);
        using var client = _factory.ClientFor(GivenCreator(), Capability.MeetingCreator);

        var response = await client.GetAsync("/api/meetings/approvers");
        using var body = await JsonOf(response);
        var options = body.RootElement.EnumerateArray().ToArray();

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal(2, options.Length);
        Assert.Equal(first.Id, options[0].GetProperty("id").GetGuid());
        Assert.Equal(first.FullName, options[0].GetProperty("fullName").GetString());
        Assert.Equal(second.Id, options[1].GetProperty("id").GetGuid());
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
    public async Task Get_allows_an_active_assigned_approver_without_MeetingCreator()
    {
        var creator = GivenCreator();
        var approver = GivenMember("approver@trustcabinet.local");
        var meeting = OwnedMeetingBy(creator, approver.Id);
        meeting.Status = MeetingStatus.WaitingApproval;
        _factory.Meetings.Meetings.Add(meeting);
        using var client = _factory.ClientFor(approver);

        var response = await client.GetAsync($"/api/meetings/{meeting.Id}");
        using var body = await JsonOf(response);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal(meeting.Id, body.RootElement.GetProperty("id").GetGuid());
    }

    [Fact]
    public async Task Get_allows_an_active_meeting_participant_without_MeetingCreator()
    {
        var creator = GivenCreator();
        var participant = GivenMember("participant@trustcabinet.local");
        var meeting = OwnedMeetingBy(creator, participantId: participant.Id);
        meeting.Status = MeetingStatus.Published;
        _factory.Meetings.Meetings.Add(meeting);
        using var client = _factory.ClientFor(participant);

        var response = await client.GetAsync($"/api/meetings/{meeting.Id}");
        using var body = await JsonOf(response);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal(meeting.Id, body.RootElement.GetProperty("id").GetGuid());
    }

    [Fact]
    public async Task Get_enforces_lifecycle_visibility_for_related_readers()
    {
        var creator = GivenCreator();
        var approver = GivenMember("lifecycle-approver@trustcabinet.local");
        var participant = GivenMember("lifecycle-participant@trustcabinet.local");
        using var approverClient = _factory.ClientFor(approver);
        using var participantClient = _factory.ClientFor(participant);

        foreach (var status in Enum.GetValues<MeetingStatus>())
        {
            var meeting = OwnedMeetingBy(creator, approver.Id, participant.Id);
            meeting.Status = status;
            _factory.Meetings.Meetings.Add(meeting);

            var approverResponse = await approverClient.GetAsync($"/api/meetings/{meeting.Id}");
            var participantResponse = await participantClient.GetAsync($"/api/meetings/{meeting.Id}");

            Assert.Equal(
                status == MeetingStatus.Draft ? HttpStatusCode.NotFound : HttpStatusCode.OK,
                approverResponse.StatusCode);
            var participantCanRead = status is MeetingStatus.Published
                or MeetingStatus.InProgress
                or MeetingStatus.Ended
                or MeetingStatus.MinutesIssued
                or MeetingStatus.Archived;
            Assert.Equal(
                participantCanRead ? HttpStatusCode.OK : HttpStatusCode.NotFound,
                participantResponse.StatusCode);
        }
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
            assignedApproverId = createBody.RootElement.GetProperty("meeting")
                .GetProperty("assignedApproverId").GetGuid(),
            status = "Published",
            createdById = Guid.CreateVersion7(),
        });
        using var updateBody = await JsonOf(update);

        Assert.Equal(HttpStatusCode.OK, update.StatusCode);
        Assert.Equal("MEETING_UPDATED", updateBody.RootElement.GetProperty("code").GetString());
        Assert.Equal("Đã cập nhật", updateBody.RootElement.GetProperty("meeting").GetProperty("title").GetString());
        Assert.Equal("Hybrid", updateBody.RootElement.GetProperty("meeting").GetProperty("mode").GetString());
        Assert.Equal("Draft", updateBody.RootElement.GetProperty("meeting").GetProperty("status").GetString());
        Assert.Equal(creator.Id, updateBody.RootElement.GetProperty("meeting").GetProperty("createdById").GetGuid());
    }

    [Fact]
    public async Task Put_with_null_assigned_approver_clears_the_existing_assignment()
    {
        var creator = GivenCreator();
        var previousApproverId = Guid.CreateVersion7();
        var meeting = OwnedMeetingBy(creator, previousApproverId);
        _factory.Meetings.Meetings.Add(meeting);
        using var client = _factory.ClientFor(creator, Capability.MeetingCreator);

        var response = await client.PutAsJsonAsync(
            $"/api/meetings/{meeting.Id}",
            ValidRequest() with { Title = "Cập nhật không có người duyệt", AssignedApproverId = null });
        using var body = await JsonOf(response);
        var responseMeeting = body.RootElement.GetProperty("meeting");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal(JsonValueKind.Null, responseMeeting.GetProperty("assignedApproverId").ValueKind);
        Assert.Null(meeting.AssignedApproverId);
        Assert.Equal(1, _factory.Meetings.SaveCount);
    }

    [Fact]
    public async Task Put_allows_resubmitting_an_existing_approver_who_is_no_longer_eligible()
    {
        var creator = GivenCreator();
        var existingApproverId = Guid.CreateVersion7();
        var meeting = OwnedMeetingBy(creator, existingApproverId);
        _factory.Meetings.Meetings.Add(meeting);
        _factory.Meetings.IneligibleApproverIds.Add(existingApproverId);
        using var client = _factory.ClientFor(creator, Capability.MeetingCreator);

        var response = await client.PutAsJsonAsync(
            $"/api/meetings/{meeting.Id}",
            ValidRequest() with { Title = "Cập nhật thông tin", AssignedApproverId = existingApproverId });
        using var body = await JsonOf(response);
        var responseMeeting = body.RootElement.GetProperty("meeting");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal(existingApproverId, responseMeeting.GetProperty("assignedApproverId").GetGuid());
        Assert.Equal("Cập nhật thông tin", responseMeeting.GetProperty("title").GetString());
        Assert.Equal(existingApproverId, meeting.AssignedApproverId);
        Assert.Equal(1, _factory.Meetings.SaveCount);
    }

    [Fact]
    public async Task Put_returns_MEETING_NOT_EDITABLE_if_the_meeting_is_not_editable()
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
        Assert.Equal("MEETING_NOT_EDITABLE", body.RootElement.GetProperty("code").GetString());
        Assert.Equal("Đã gửi duyệt", meeting.Title);
    }

    [Theory]
    [InlineData("{")]
    [InlineData("{\"title\":42}")]
    public async Task Put_checks_status_before_parsing_invalid_json(string json)
    {
        var creator = GivenCreator();
        using var client = _factory.ClientFor(creator, Capability.MeetingCreator);
        var meeting = OwnedMeetingBy(creator);
        meeting.Status = MeetingStatus.WaitingApproval;
        _factory.Meetings.Meetings.Add(meeting);

        using var request = new HttpRequestMessage(HttpMethod.Put, $"/api/meetings/{meeting.Id}")
        {
            Content = new StringContent(json, System.Text.Encoding.UTF8, "application/json"),
        };
        var response = await client.SendAsync(request);
        using var body = await JsonOf(response);

        Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);
        Assert.Equal("MEETING_NOT_EDITABLE", body.RootElement.GetProperty("code").GetString());
        Assert.Equal("Giao ban tuần", meeting.Title);
    }

    [Fact]
    public async Task Put_returns_400_for_invalid_json_when_meeting_is_editable()
    {
        var creator = GivenCreator();
        using var client = _factory.ClientFor(creator, Capability.MeetingCreator);
        var meeting = OwnedMeetingBy(creator);
        _factory.Meetings.Meetings.Add(meeting);

        using var request = new HttpRequestMessage(HttpMethod.Put, $"/api/meetings/{meeting.Id}")
        {
            Content = new StringContent("{", System.Text.Encoding.UTF8, "application/json"),
        };
        var response = await client.SendAsync(request);
        using var body = await JsonOf(response);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Equal("MEETING_INVALID_INFORMATION", body.RootElement.GetProperty("code").GetString());
    }

    [Fact]
    public async Task Put_checks_state_before_rejecting_a_missing_or_unsupported_content_type()
    {
        var creator = GivenCreator();
        using var client = _factory.ClientFor(creator, Capability.MeetingCreator);
        var editable = OwnedMeetingBy(creator);
        var waiting = OwnedMeetingBy(creator);
        waiting.Status = MeetingStatus.WaitingApproval;
        _factory.Meetings.Meetings.AddRange([editable, waiting]);

        using var unsupportedRequest = new HttpRequestMessage(HttpMethod.Put, $"/api/meetings/{editable.Id}")
        {
            Content = new StringContent("{}", System.Text.Encoding.UTF8, "text/plain"),
        };
        using var missingTypeRequest = new HttpRequestMessage(HttpMethod.Put, $"/api/meetings/{editable.Id}")
        {
            Content = new StringContent("{}", System.Text.Encoding.UTF8, "application/json"),
        };
        missingTypeRequest.Content.Headers.ContentType = null;
        using var waitingRequest = new HttpRequestMessage(HttpMethod.Put, $"/api/meetings/{waiting.Id}")
        {
            Content = new StringContent("{}", System.Text.Encoding.UTF8, "text/plain"),
        };

        var unsupportedResponse = await client.SendAsync(unsupportedRequest);
        var missingTypeResponse = await client.SendAsync(missingTypeRequest);
        var waitingResponse = await client.SendAsync(waitingRequest);

        Assert.Equal(HttpStatusCode.UnsupportedMediaType, unsupportedResponse.StatusCode);
        Assert.Equal(HttpStatusCode.UnsupportedMediaType, missingTypeResponse.StatusCode);
        Assert.Equal(HttpStatusCode.Conflict, waitingResponse.StatusCode);
    }

    [Fact]
    public async Task Put_returns_validation_errors_without_mutating_the_meeting()
    {
        var creator = GivenCreator();
        using var client = _factory.ClientFor(creator, Capability.MeetingCreator);
        var meeting = OwnedMeetingBy(creator, Guid.CreateVersion7());
        _factory.Meetings.Meetings.Add(meeting);
        var request = ValidRequest("Dữ liệu không hợp lệ") with
        {
            StartsAt = "2026-10-01T09:00:00+07:00",
            EndsAt = "2026-10-01T09:00:00+07:00",
            AssignedApproverId = meeting.AssignedApproverId,
        };

        var response = await client.PutAsJsonAsync($"/api/meetings/{meeting.Id}", request);
        using var body = await JsonOf(response);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Equal("MEETING_INVALID_INFORMATION", body.RootElement.GetProperty("code").GetString());
        Assert.True(body.RootElement.GetProperty("errors").TryGetProperty("endsAt", out _));
        Assert.Equal("Giao ban tuần", Assert.Single(_factory.Meetings.Meetings).Title);
        Assert.Equal(0, _factory.Meetings.SaveCount);
    }

    [Fact]
    public async Task Put_save_failure_returns_MEETING_UPDATE_FAILED_and_500()
    {
        var creator = GivenCreator();
        using var client = _factory.ClientFor(creator, Capability.MeetingCreator);
        var meeting = OwnedMeetingBy(creator, Guid.CreateVersion7());
        _factory.Meetings.Meetings.Add(meeting);
        _factory.Meetings.NextSaveThrows = new Npgsql.NpgsqlException("database unavailable");

        var response = await client.PutAsJsonAsync(
            $"/api/meetings/{meeting.Id}", ValidRequest("Không được lưu") with
            {
                AssignedApproverId = meeting.AssignedApproverId,
            });
        using var body = await JsonOf(response);

        Assert.Equal(HttpStatusCode.InternalServerError, response.StatusCode);
        Assert.Equal("MEETING_UPDATE_FAILED", body.RootElement.GetProperty("code").GetString());
    }

    [Fact]
    public async Task Get_returns_MEETING_LOAD_FAILED_when_the_repository_fails()
    {
        var creator = GivenCreator();
        using var client = _factory.ClientFor(creator);
        _factory.Meetings.NextReadThrows = new Npgsql.NpgsqlException("database unavailable");

        var response = await client.GetAsync($"/api/meetings/{Guid.CreateVersion7()}");
        using var body = await JsonOf(response);

        Assert.Equal(HttpStatusCode.InternalServerError, response.StatusCode);
        Assert.Equal("MEETING_LOAD_FAILED", body.RootElement.GetProperty("code").GetString());
    }

    [Fact]
    public async Task Anonymous_and_callers_without_MeetingCreator_are_rejected_by_the_gate()
    {
        using var anonymous = _factory.CreateClient();
        var creator = GivenCreator("wrong-capability@trustcabinet.local");
        using var wrongCapability = _factory.ClientFor(creator, Capability.AdminUsers);

        var anonymousResponse = await anonymous.GetAsync($"/api/meetings/{Guid.CreateVersion7()}");
        var forbiddenResponse = await wrongCapability.PostAsJsonAsync("/api/meetings", ValidRequest());
        var forbiddenApprovers = await wrongCapability.GetAsync("/api/meetings/approvers");

        Assert.Equal(HttpStatusCode.Unauthorized, anonymousResponse.StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, forbiddenResponse.StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, forbiddenApprovers.StatusCode);
    }

    [Fact]
    public async Task Create_save_failure_returns_MEETING_CREATE_FAILED_and_500()
    {
        using var client = _factory.ClientFor(GivenCreator(), Capability.MeetingCreator);
        _factory.Meetings.NextSaveThrows = new Npgsql.NpgsqlException("database unavailable");

        var response = await client.PostAsJsonAsync("/api/meetings", ValidRequest());
        using var body = await JsonOf(response);

        Assert.Equal(HttpStatusCode.InternalServerError, response.StatusCode);
        Assert.Equal("MEETING_CREATE_FAILED", body.RootElement.GetProperty("code").GetString());
        Assert.Empty(_factory.Meetings.Meetings);
    }
}
