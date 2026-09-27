using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text;
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

    private sealed class UnknownLengthContent(byte[] body) : HttpContent
    {
        protected override bool TryComputeLength(out long length)
        {
            length = 0;
            return false;
        }

        protected override Task SerializeToStreamAsync(Stream stream, TransportContext? context)
            => stream.WriteAsync(body.AsMemory()).AsTask();
    }

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
    public async Task Post_transition_accepts_the_documented_submit_contract_and_returns_history()
    {
        var creator = GivenCreator();
        var approverId = Guid.CreateVersion7();
        var meeting = OwnedMeetingBy(creator, approverId);
        meeting.AgendaItems.Add(new AgendaItem
        {
            MeetingId = meeting.Id,
            Position = 1,
            Title = "Báo cáo",
            PlannedDurationMinutes = 30,
            PresenterId = Guid.CreateVersion7(),
        });
        _factory.Meetings.Meetings.Add(meeting);
        _factory.Meetings.UserCapabilities[creator.Id] = [Capability.MeetingCreator];
        using var client = _factory.ClientFor(creator, Capability.MeetingCreator);

        var response = await client.PostAsJsonAsync($"/api/meetings/{meeting.Id}/transitions", new
        {
            action = "SubmitForApproval",
            assignedApproverId = approverId,
        });
        using var body = await JsonOf(response);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal("WaitingApproval", body.RootElement.GetProperty("meeting").GetProperty("status").GetString());
        Assert.Equal("SubmitForApproval", body.RootElement.GetProperty("history").GetProperty("action").GetString());
        Assert.Equal(approverId, body.RootElement.GetProperty("history").GetProperty("assignedApproverId").GetGuid());
        Assert.Single(_factory.Meetings.Notifications);
        Assert.Empty(_factory.Meetings.EmailMessages);
    }

    [Fact]
    public async Task Post_transition_rejects_unknown_fields_and_numeric_actions()
    {
        var creator = GivenCreator();
        var meeting = OwnedMeetingBy(creator, Guid.CreateVersion7());
        _factory.Meetings.Meetings.Add(meeting);
        using var client = _factory.ClientFor(creator, Capability.MeetingCreator);

        var unknownField = await client.PostAsJsonAsync(
            $"/api/meetings/{meeting.Id}/transitions",
            new { action = "ReturnToDraft", status = "Draft" });
        using var unknownBody = await JsonOf(unknownField);
        var numericAction = await client.PostAsync(
            $"/api/meetings/{meeting.Id}/transitions",
            new StringContent("{\"action\":0}", Encoding.UTF8, "application/json"));

        Assert.Equal(HttpStatusCode.BadRequest, unknownField.StatusCode);
        Assert.Equal("MEETING_INVALID_TRANSITION_REQUEST", unknownBody.RootElement.GetProperty("code").GetString());
        Assert.Equal(HttpStatusCode.BadRequest, numericAction.StatusCode);
        Assert.Equal(MeetingStatus.Draft, meeting.Status);
        Assert.Empty(meeting.StatusHistory);
    }

    [Fact]
    public async Task Get_submission_readiness_uses_persisted_meeting_and_agenda_data_without_writing()
    {
        var creator = GivenCreator();
        var meeting = OwnedMeetingBy(creator, Guid.CreateVersion7());
        _factory.Meetings.Meetings.Add(meeting);
        _factory.Meetings.UserCapabilities[creator.Id] = [Capability.MeetingCreator];
        using var client = _factory.ClientFor(creator, Capability.MeetingCreator);

        var response = await client.GetAsync($"/api/meetings/{meeting.Id}/submission-readiness");
        using var body = await JsonOf(response);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.False(body.RootElement.GetProperty("ready").GetBoolean());
        Assert.True(body.RootElement.GetProperty("errors").TryGetProperty("agendaItems", out _));
        Assert.Equal(MeetingStatus.Draft, meeting.Status);
        Assert.Equal(0, _factory.Meetings.SaveCount);
    }

    [Fact]
    public async Task Get_history_returns_full_history_to_the_current_assigned_approver()
    {
        var creator = GivenCreator();
        var approver = GivenMember("assigned-history@trustcabinet.local");
        var meeting = OwnedMeetingBy(creator, approver.Id);
        meeting.StatusHistory.Add(new MeetingStatusHistory
        {
            MeetingId = meeting.Id,
            FromStatus = MeetingStatus.WaitingApproval,
            ToStatus = MeetingStatus.Rejected,
            Action = MeetingHistoryAction.Reject,
            ActorId = approver.Id,
            Reason = "Cần bổ sung số liệu",
            OccurredAt = DateTimeOffset.UtcNow,
        });
        _factory.Meetings.Meetings.Add(meeting);
        using var client = _factory.ClientFor(approver);

        var response = await client.GetAsync($"/api/meetings/{meeting.Id}/history");
        using var body = await JsonOf(response);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var entry = Assert.Single(body.RootElement.GetProperty("items").EnumerateArray());
        Assert.Equal("Reject", entry.GetProperty("action").GetString());
        Assert.Equal("Cần bổ sung số liệu", entry.GetProperty("rejectionReason").GetString());
    }

    [Fact]
    public async Task Get_history_returns_redacted_timeline_to_a_published_participant()
    {
        var creator = GivenCreator();
        var participant = GivenMember("history-participant@trustcabinet.local");
        var meeting = OwnedMeetingBy(creator, participantId: participant.Id);
        meeting.Status = MeetingStatus.Published;
        meeting.StatusHistory.Add(new MeetingStatusHistory
        {
            MeetingId = meeting.Id,
            FromStatus = MeetingStatus.WaitingApproval,
            ToStatus = MeetingStatus.Rejected,
            Action = MeetingHistoryAction.Reject,
            ActorId = Guid.CreateVersion7(),
            Reason = "Lý do nội bộ",
            OccurredAt = DateTimeOffset.UtcNow,
        });
        _factory.Meetings.Meetings.Add(meeting);
        using var client = _factory.ClientFor(participant);

        var response = await client.GetAsync($"/api/meetings/{meeting.Id}/history");

        using var body = await JsonOf(response);
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var entry = Assert.Single(body.RootElement.GetProperty("items").EnumerateArray());
        Assert.Equal("Reject", entry.GetProperty("action").GetString());
        Assert.Equal(JsonValueKind.Null, entry.GetProperty("rejectionReason").ValueKind);
    }

    [Fact]
    public async Task List_requires_authentication_and_an_active_account()
    {
        using var anonymousClient = _factory.CreateClient();
        var anonymous = await anonymousClient.GetAsync("/api/meetings");
        Assert.Equal(HttpStatusCode.Unauthorized, anonymous.StatusCode);

        var inactive = GivenMember("inactive-list@trustcabinet.local");
        inactive.IsActive = false;
        using var inactiveClient = _factory.ClientFor(inactive);
        var refused = await inactiveClient.GetAsync("/api/meetings");
        Assert.Equal(HttpStatusCode.Forbidden, refused.StatusCode);
    }

    [Fact]
    public async Task List_returns_only_authorized_items_and_pagination_metadata()
    {
        var creator = GivenCreator();
        var unrelated = GivenMember("unrelated-list@trustcabinet.local");
        var owned = OwnedMeetingBy(creator);
        owned.Title = "Giao ban % tuần";
        var hidden = OwnedMeetingBy(unrelated);
        hidden.Title = owned.Title;
        _factory.Meetings.Meetings.AddRange([owned, hidden]);
        using var client = _factory.ClientFor(creator);

        var response = await client.GetAsync(
            "/api/meetings?status=Draft&q=Giao&page=1&pageSize=1&sortBy=createdAt&sortDir=asc");
        using var body = await JsonOf(response);
        var root = body.RootElement;

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal(1, root.GetProperty("page").GetInt32());
        Assert.Equal(1, root.GetProperty("pageSize").GetInt32());
        Assert.Equal(1, root.GetProperty("totalCount").GetInt64());
        Assert.Equal(1, root.GetProperty("totalPages").GetInt64());
        Assert.Equal(MeetingSortBy.CreatedAt, _factory.Meetings.LastListQuery!.SortBy);
        Assert.Equal(MeetingSortDirection.Asc, _factory.Meetings.LastListQuery.SortDirection);
        var item = Assert.Single(root.GetProperty("items").EnumerateArray());
        Assert.Equal(owned.Id, item.GetProperty("id").GetGuid());
        Assert.Equal("Draft", item.GetProperty("status").GetString());
        Assert.Equal("Internal", item.GetProperty("sensitivity").GetString());
        Assert.Equal(
            ["id", "title", "startsAt", "createdAt", "status", "sensitivity"],
            item.EnumerateObject().Select(property => property.Name));
    }

    [Fact]
    public async Task List_returns_empty_page_for_no_match_or_page_beyond_end()
    {
        var creator = GivenCreator();
        _factory.Meetings.Meetings.Add(OwnedMeetingBy(creator));
        using var client = _factory.ClientFor(creator);

        var noMatch = await client.GetAsync("/api/meetings?status=Archived");
        using var noMatchBody = await JsonOf(noMatch);
        Assert.Equal(HttpStatusCode.OK, noMatch.StatusCode);
        Assert.Empty(noMatchBody.RootElement.GetProperty("items").EnumerateArray());
        Assert.Equal(0, noMatchBody.RootElement.GetProperty("totalCount").GetInt64());
        Assert.Equal(0, noMatchBody.RootElement.GetProperty("totalPages").GetInt64());

        var beyond = await client.GetAsync("/api/meetings?page=2&pageSize=1");
        using var beyondBody = await JsonOf(beyond);
        Assert.Equal(HttpStatusCode.OK, beyond.StatusCode);
        Assert.Empty(beyondBody.RootElement.GetProperty("items").EnumerateArray());
        Assert.Equal(1, beyondBody.RootElement.GetProperty("totalCount").GetInt64());
        Assert.Equal(1, beyondBody.RootElement.GetProperty("totalPages").GetInt64());

        var veryLargePage = await client.GetAsync("/api/meetings?page=2147483647&pageSize=20");
        using var veryLargeBody = await JsonOf(veryLargePage);
        Assert.Equal(HttpStatusCode.OK, veryLargePage.StatusCode);
        Assert.Empty(veryLargeBody.RootElement.GetProperty("items").EnumerateArray());
        Assert.Equal(int.MaxValue, veryLargeBody.RootElement.GetProperty("page").GetInt32());
        Assert.Equal(1, veryLargeBody.RootElement.GetProperty("totalCount").GetInt64());
    }

    [Fact]
    public async Task List_returns_field_errors_for_invalid_query()
    {
        using var client = _factory.ClientFor(GivenCreator());

        var response = await client.GetAsync("/api/meetings?status=Wrong&page=0");
        using var body = await JsonOf(response);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Equal("MEETING_INVALID_QUERY", body.RootElement.GetProperty("code").GetString());
        Assert.True(body.RootElement.GetProperty("errors").TryGetProperty("status", out _));
        Assert.True(body.RootElement.GetProperty("errors").TryGetProperty("page", out _));
        Assert.Null(_factory.Meetings.LastListQuery);
    }

    [Fact]
    public async Task List_returns_MSG08_when_the_database_read_fails()
    {
        _factory.Meetings.NextReadThrows = new Npgsql.NpgsqlException("database unavailable");
        using var client = _factory.ClientFor(GivenCreator());

        var response = await client.GetAsync("/api/meetings");
        using var body = await JsonOf(response);

        Assert.Equal(HttpStatusCode.ServiceUnavailable, response.StatusCode);
        Assert.Equal("MSG08", body.RootElement.GetProperty("code").GetString());
        Assert.Equal("Không thể tải danh sách cuộc họp. Vui lòng thử lại.",
            body.RootElement.GetProperty("message").GetString());
    }

    [Fact]
    public async Task List_returns_MSG08_when_active_account_lookup_fails()
    {
        using var client = _factory.ClientFor(GivenCreator());
        _factory.Accounts.NextReadThrows = new Npgsql.NpgsqlException("database unavailable");

        var response = await client.GetAsync("/api/meetings");
        using var body = await JsonOf(response);

        Assert.Equal(HttpStatusCode.ServiceUnavailable, response.StatusCode);
        Assert.Equal("MSG08", body.RootElement.GetProperty("code").GetString());
        Assert.Equal("Không thể tải danh sách cuộc họp. Vui lòng thử lại.",
            body.RootElement.GetProperty("message").GetString());
        Assert.Null(_factory.Meetings.LastListQuery);
    }

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
            assignedApproverId = Guid.CreateVersion7(),
        };

        var response = await client.PostAsJsonAsync("/api/meetings", request);
        using var body = await JsonOf(response);
        var meeting = body.RootElement.GetProperty("meeting");

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        Assert.Equal("InPerson", meeting.GetProperty("mode").GetString());
        Assert.Equal("Normal", meeting.GetProperty("sensitivity").GetString());
        Assert.Equal(JsonValueKind.Null, meeting.GetProperty("endsAt").ValueKind);
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
        Assert.Equal("MSG03", body.RootElement.GetProperty("code").GetString());
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

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task Put_rejects_json_larger_than_64_kib(bool unknownLength)
    {
        var creator = GivenCreator();
        using var client = _factory.ClientFor(creator, Capability.MeetingCreator);
        var meeting = OwnedMeetingBy(creator);
        _factory.Meetings.Meetings.Add(meeting);
        var oversizedBody = new byte[64 * 1024 + 1];

        using HttpContent content = unknownLength
            ? new UnknownLengthContent(oversizedBody)
            : new ByteArrayContent(oversizedBody);
        content.Headers.ContentType = new MediaTypeHeaderValue("application/json");
        using var request = new HttpRequestMessage(HttpMethod.Put, $"/api/meetings/{meeting.Id}")
        {
            Content = content,
        };

        var response = await client.SendAsync(request);
        using var body = await JsonOf(response);

        Assert.Equal(HttpStatusCode.RequestEntityTooLarge, response.StatusCode);
        Assert.Equal("MEETING_BODY_TOO_LARGE", body.RootElement.GetProperty("code").GetString());
        Assert.Equal("Giao ban tuần", meeting.Title);
        Assert.Equal(0, _factory.Meetings.SaveCount);
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
    public async Task Put_save_failure_returns_MSG04_and_503()
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

        Assert.Equal(HttpStatusCode.ServiceUnavailable, response.StatusCode);
        Assert.Equal("MSG04", body.RootElement.GetProperty("code").GetString());
    }

    [Fact]
    public async Task Get_returns_MEETING_LOAD_FAILED_when_the_repository_fails()
    {
        var creator = GivenCreator();
        using var client = _factory.ClientFor(creator);
        _factory.Meetings.NextReadThrows = new Npgsql.NpgsqlException("database unavailable");

        var response = await client.GetAsync($"/api/meetings/{Guid.CreateVersion7()}");
        using var body = await JsonOf(response);

        Assert.Equal(HttpStatusCode.ServiceUnavailable, response.StatusCode);
        Assert.Equal("MEETING_LOAD_FAILED", body.RootElement.GetProperty("code").GetString());
    }

    [Fact]
    public async Task Get_preserves_its_error_code_when_active_account_lookup_fails()
    {
        using var client = _factory.ClientFor(GivenCreator());
        _factory.Accounts.NextReadThrows = new Npgsql.NpgsqlException("database unavailable");

        var response = await client.GetAsync($"/api/meetings/{Guid.CreateVersion7()}");
        using var body = await JsonOf(response);

        Assert.Equal(HttpStatusCode.ServiceUnavailable, response.StatusCode);
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
