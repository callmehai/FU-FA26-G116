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
using TrustCabinet.Domain.Notifications;
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
            PlannedStartTime = FutureStart,
            PlannedEndTime = FutureStart.AddHours(1),
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

    /// <summary>A week ahead, so BR-162 (start in the future) holds whenever the suite runs.</summary>
    private static readonly DateTimeOffset FutureStart =
        new(DateTimeOffset.UtcNow.Date.AddDays(7).AddHours(2), TimeSpan.Zero);

    private static string Iso(DateTimeOffset value)
        => value.ToString("yyyy-MM-dd'T'HH:mm:ss'Z'", System.Globalization.CultureInfo.InvariantCulture);

    private static CreateMeetingRequest ValidCreateRequest(string? title = "Giao ban tuần")
    {
        var r = ValidRequest(title);
        return new(r.Title, r.PlannedStartTime, r.PlannedEndTime, r.Mode, r.Location, r.OnlineLink,
            r.Sensitivity, r.Description, [new(Guid.Parse("aaaaaaaa-aaaa-4aaa-8aaa-aaaaaaaaaaaa"), "Chair")]);
    }

    [Fact]
    public async Task Create_requires_participants_with_MSG277_and_update_rejects_roster()
    {
        var creator = GivenCreator();
        using var client = _factory.ClientFor(creator, Capability.MeetingCreator);
        var invalid = await client.PostAsJsonAsync("/api/meetings", ValidCreateRequest() with { Participants = [] });
        using var error = await JsonOf(invalid);
        Assert.Equal(HttpStatusCode.BadRequest, invalid.StatusCode);
        Assert.Equal("MSG277", error.RootElement.GetProperty("code").GetString());
        Assert.True(error.RootElement.GetProperty("errors").TryGetProperty("participants", out _));
        Assert.Empty(_factory.Meetings.Meetings);

        var created = await client.PostAsJsonAsync("/api/meetings", ValidCreateRequest());
        using var body = await JsonOf(created);
        var meeting = body.RootElement.GetProperty("meeting");
        Assert.Equal(HttpStatusCode.Created, created.StatusCode);
        Assert.Equal("Selected user", meeting.GetProperty("chair").GetProperty("fullName").GetString());
        Assert.Empty(meeting.GetProperty("secretaries").EnumerateArray());
        Assert.Equal(JsonValueKind.Null, meeting.GetProperty("myMeetingRole").ValueKind);
        var update = await client.PutAsJsonAsync($"/api/meetings/{meeting.GetProperty("id").GetGuid()}", ValidCreateRequest());
        using var rejected = await JsonOf(update);
        Assert.Equal(HttpStatusCode.BadRequest, update.StatusCode);
        Assert.Equal("MSG31", rejected.RootElement.GetProperty("code").GetString());
    }

    private static MeetingRequest ValidRequest(string? title = "Giao ban tuần")
        => new(
            title,
            Iso(FutureStart),
            Iso(FutureStart.AddHours(1)),
            "InPerson",
            "Phòng họp A",
            null,
            "Internal",
            "Kế hoạch tuần.");

    private static void AddPublishRoster(Meeting meeting)
    {
        foreach (var role in new[] { MeetingRole.Chair, MeetingRole.Secretary })
        {
            var user = new User
            {
                FullName = role.ToString(),
                Email = $"{role.ToString().ToLowerInvariant()}-{Guid.CreateVersion7():N}@trustcabinet.local",
                PasswordHash = "test-hash",
                IsActive = true,
            };
            meeting.Participants.Add(new MeetingParticipant
            {
                Meeting = meeting,
                User = user,
                UserId = user.Id,
                Role = role,
                Response = InvitationResponse.Pending,
            });
        }
    }

    private static async Task<JsonDocument> JsonOf(HttpResponseMessage response)
        => JsonDocument.Parse(await response.Content.ReadAsStringAsync());

    private (User Administrator, User Replacement, Meeting Meeting) GivenOrganizerRecovery()
    {
        var organizer = GivenCreator("disabled-organizer@trustcabinet.local");
        organizer.IsActive = false;
        var administrator = new User
        {
            FullName = "Quản trị",
            Email = "recovery-admin@trustcabinet.local",
            PasswordHash = "test-hash",
            RoleId = BuiltInRoles.Admin,
        };
        _factory.Accounts.WithUser(administrator);
        var replacement = GivenCreator("replacement-organizer@trustcabinet.local");
        var meeting = OwnedMeetingBy(organizer, Guid.CreateVersion7());
        meeting.Status = MeetingStatus.Approved;
        _factory.Meetings.Meetings.Add(meeting);
        _factory.Meetings.UserCapabilities[administrator.Id] = [Capability.AdminUsers];
        _factory.Meetings.UserCapabilities[organizer.Id] = [Capability.MeetingCreator];
        _factory.Meetings.UserCapabilities[replacement.Id] = [Capability.MeetingCreator];
        _factory.Meetings.InactiveUserIds.Add(organizer.Id);
        return (administrator, replacement, meeting);
    }

    [Fact]
    public async Task Administrator_transfers_disabled_organizer_meeting_with_MSG89_and_typed_history()
    {
        var (administrator, replacement, meeting) = GivenOrganizerRecovery();
        var previousOrganizerId = meeting.CreatedById;
        var approverId = meeting.AssignedApproverId;
        using var client = _factory.ClientFor(administrator, Capability.AdminUsers);

        var response = await client.PostAsJsonAsync($"/api/meetings/{meeting.Id}/transitions",
            new { action = "TransferOrganizer", organizerId = replacement.Id });
        using var body = await JsonOf(response);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal("MSG89", body.RootElement.GetProperty("code").GetString());
        Assert.Equal(meeting.Id, body.RootElement.GetProperty("meetingId").GetGuid());
        Assert.Equal("Approved", body.RootElement.GetProperty("status").GetString());
        Assert.Equal(replacement.Id, body.RootElement.GetProperty("organizerId").GetGuid());
        Assert.False(body.RootElement.TryGetProperty("meeting", out _));
        var history = body.RootElement.GetProperty("history");
        Assert.Equal("TransferOrganizer", history.GetProperty("action").GetString());
        Assert.Equal(previousOrganizerId, history.GetProperty("previousOrganizerId").GetGuid());
        Assert.Equal(replacement.Id, history.GetProperty("newOrganizerId").GetGuid());
        Assert.Equal(administrator.Id, history.GetProperty("actorId").GetGuid());
        Assert.Equal(approverId, meeting.AssignedApproverId);
        Assert.Single(meeting.StatusHistory);
        Assert.Empty(_factory.Meetings.Notifications);
        Assert.Empty(_factory.Meetings.StagedPublicationIds);
        Assert.Equal(HttpStatusCode.NotFound, (await client.GetAsync($"/api/meetings/{meeting.Id}")).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await client.GetAsync($"/api/meetings/{meeting.Id}/history")).StatusCode);

        var repeat = await client.PostAsJsonAsync($"/api/meetings/{meeting.Id}/transitions",
            new { action = "TransferOrganizer", organizerId = replacement.Id });
        using var repeatBody = await JsonOf(repeat);
        Assert.Equal(HttpStatusCode.Conflict, repeat.StatusCode);
        Assert.Equal("MSG135", repeatBody.RootElement.GetProperty("code").GetString());
        Assert.Single(meeting.StatusHistory);
    }

    [Fact]
    public async Task Transfer_does_not_return_sensitive_content_to_a_recovery_administrator()
    {
        var (administrator, replacement, meeting) = GivenOrganizerRecovery();
        meeting.Title = "Restricted title for recovery";
        meeting.Description = "Restricted description for recovery";
        meeting.Location = "Restricted location for recovery";
        meeting.Sensitivity = Sensitivity.Secret;
        using var client = _factory.ClientFor(administrator, Capability.AdminUsers);

        Assert.Equal(HttpStatusCode.NotFound, (await client.GetAsync($"/api/meetings/{meeting.Id}")).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await client.GetAsync($"/api/meetings/{meeting.Id}/history")).StatusCode);

        var response = await client.PostAsJsonAsync($"/api/meetings/{meeting.Id}/transitions",
            new { action = "TransferOrganizer", organizerId = replacement.Id });
        using var body = await JsonOf(response);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal(
            ["code", "history", "meetingId", "message", "organizerId", "status"],
            body.RootElement.EnumerateObject().Select(property => property.Name).Order(StringComparer.Ordinal));
        var responseJson = body.RootElement.GetRawText();
        Assert.DoesNotContain(meeting.Title, responseJson, StringComparison.Ordinal);
        Assert.DoesNotContain(meeting.Description, responseJson, StringComparison.Ordinal);
        Assert.DoesNotContain(meeting.Location, responseJson, StringComparison.Ordinal);
        Assert.DoesNotContain("\"sensitivity\"", responseJson, StringComparison.Ordinal);
        Assert.DoesNotContain("\"plannedStartTime\"", responseJson, StringComparison.Ordinal);
        Assert.DoesNotContain("\"plannedEndTime\"", responseJson, StringComparison.Ordinal);
        Assert.Equal(HttpStatusCode.NotFound, (await client.GetAsync($"/api/meetings/{meeting.Id}")).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await client.GetAsync($"/api/meetings/{meeting.Id}/history")).StatusCode);
    }

    [Fact]
    public async Task Transfer_rechecks_administrator_capability_despite_the_old_access_token()
    {
        var (administrator, replacement, meeting) = GivenOrganizerRecovery();
        using var client = _factory.ClientFor(administrator, Capability.AdminUsers);
        _factory.Meetings.UserCapabilities[administrator.Id].Clear();

        var response = await client.PostAsJsonAsync($"/api/meetings/{meeting.Id}/transitions",
            new { action = "TransferOrganizer", organizerId = replacement.Id });

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
        Assert.NotEqual(replacement.Id, meeting.CreatedById);
        Assert.Empty(meeting.StatusHistory);
        Assert.Empty(_factory.Meetings.Notifications);
    }

    [Theory]
    [InlineData("{\"action\":\"TransferOrganizer\"}")]
    [InlineData("{\"action\":\"TransferOrganizer\",\"organizerId\":null}")]
    [InlineData("{\"action\":\"TransferOrganizer\",\"organizerId\":23}")]
    [InlineData("{\"action\":\"TransferOrganizer\",\"organizerId\":\"00000000-0000-0000-0000-000000000000\"}")]
    [InlineData("{\"action\":\"TransferOrganizer\",\"organizerId\":\"0199a000-0000-7000-8000-000000000001\",\"reason\":null}")]
    [InlineData("{\"action\":\"TransferOrganizer\",\"organizerId\":\"0199a000-0000-7000-8000-000000000001\",\"assignedApproverId\":\"0199a000-0000-7000-8000-000000000002\"}")]
    [InlineData("{\"action\":\"TransferOrganizer\",\"organizerId\":\"0199a000-0000-7000-8000-000000000001\",\"organizerId\":\"0199a000-0000-7000-8000-000000000002\"}")]
    [InlineData("{\"action\":\"Publish\",\"organizerId\":\"0199a000-0000-7000-8000-000000000001\"}")]
    public async Task Transfer_body_shape_is_strict_and_other_commands_do_not_accept_organizerId(string json)
    {
        var (administrator, _, meeting) = GivenOrganizerRecovery();
        using var client = _factory.ClientFor(administrator, Capability.AdminUsers);

        var response = await client.PostAsync($"/api/meetings/{meeting.Id}/transitions",
            new StringContent(json, Encoding.UTF8, "application/json"));
        using var body = await JsonOf(response);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Equal("MSG80", body.RootElement.GetProperty("code").GetString());
        Assert.Empty(meeting.StatusHistory);
        Assert.Empty(_factory.Meetings.Notifications);
    }

    [Fact]
    public async Task Invalid_replacement_returns_MSG134_and_transfer_persistence_failure_returns_MSG88()
    {
        var (administrator, replacement, meeting) = GivenOrganizerRecovery();
        var previousOrganizerId = meeting.CreatedById;
        using var client = _factory.ClientFor(administrator, Capability.AdminUsers);
        _factory.Meetings.InactiveUserIds.Add(replacement.Id);

        var invalid = await client.PostAsJsonAsync($"/api/meetings/{meeting.Id}/transitions",
            new { action = "TransferOrganizer", organizerId = replacement.Id });
        using var invalidBody = await JsonOf(invalid);
        Assert.Equal(HttpStatusCode.BadRequest, invalid.StatusCode);
        Assert.Equal("MSG134", invalidBody.RootElement.GetProperty("code").GetString());

        _factory.Meetings.InactiveUserIds.Remove(replacement.Id);
        _factory.Meetings.NextSaveThrows = new TimeoutException("Database unavailable");
        var failed = await client.PostAsJsonAsync($"/api/meetings/{meeting.Id}/transitions",
            new { action = "TransferOrganizer", organizerId = replacement.Id });
        using var failedBody = await JsonOf(failed);
        Assert.Equal(HttpStatusCode.ServiceUnavailable, failed.StatusCode);
        Assert.Equal("MSG88", failedBody.RootElement.GetProperty("code").GetString());
        var persisted = Assert.Single(_factory.Meetings.Meetings);
        Assert.Equal(previousOrganizerId, persisted.CreatedById);
        Assert.Empty(persisted.StatusHistory);
        Assert.Empty(_factory.Meetings.Notifications);
        Assert.Empty(_factory.Meetings.StagedPublicationIds);
    }

    [Theory]
    [InlineData(Sensitivity.Normal)]
    [InlineData(Sensitivity.Internal)]
    [InlineData(Sensitivity.Confidential)]
    [InlineData(Sensitivity.Secret)]
    public async Task Submit_to_the_organizer_returns_MSG82_without_writing_any_workflow_rows(Sensitivity sensitivity)
    {
        var creator = GivenCreator();
        var meeting = OwnedMeetingBy(creator);
        meeting.Sensitivity = sensitivity;
        meeting.Participants.Add(new MeetingParticipant { MeetingId = meeting.Id, UserId = creator.Id, Role = MeetingRole.Chair });
        meeting.AgendaItems.Add(new AgendaItem { MeetingId = meeting.Id, Title = "Báo cáo" });
        _factory.Meetings.Meetings.Add(meeting);
        _factory.Meetings.UserCapabilities[creator.Id] = [Capability.MeetingCreator, Capability.MeetingApprover];
        using var client = _factory.ClientFor(creator, Capability.MeetingCreator, Capability.MeetingApprover);

        var response = await client.PostAsJsonAsync($"/api/meetings/{meeting.Id}/transitions",
            new { action = "SubmitForApproval", assignedApproverId = creator.Id });
        using var body = await JsonOf(response);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Equal("MSG82", body.RootElement.GetProperty("code").GetString());
        Assert.Equal(MeetingStatus.Draft, meeting.Status);
        Assert.Null(meeting.AssignedApproverId);
        Assert.Empty(meeting.StatusHistory);
        Assert.Empty(_factory.Meetings.Notifications);
        Assert.Empty(_factory.Meetings.EmailMessages);
    }

    [Theory]
    [InlineData("Approve")]
    [InlineData("Reject")]
    public async Task Legacy_self_assignment_returns_MSG85_without_approving_or_rejecting(string action)
    {
        var creator = GivenCreator();
        var meeting = OwnedMeetingBy(creator, creator.Id);
        meeting.Status = MeetingStatus.WaitingApproval;
        _factory.Meetings.Meetings.Add(meeting);
        _factory.Meetings.UserCapabilities[creator.Id] = [Capability.MeetingCreator, Capability.MeetingApprover];
        using var client = _factory.ClientFor(creator, Capability.MeetingCreator, Capability.MeetingApprover);
        var json = action == "Reject" ? "{\"action\":\"Reject\",\"reason\":\"Lý do\"}" : "{\"action\":\"Approve\"}";

        var response = await client.PostAsync($"/api/meetings/{meeting.Id}/transitions",
            new StringContent(json, Encoding.UTF8, "application/json"));
        using var body = await JsonOf(response);

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
        Assert.Equal("MSG85", body.RootElement.GetProperty("code").GetString());
        Assert.Equal(MeetingStatus.WaitingApproval, meeting.Status);
        Assert.Empty(meeting.StatusHistory);
        Assert.Empty(_factory.Meetings.Notifications);
        Assert.Empty(_factory.Meetings.EmailMessages);
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public async Task Legacy_missing_or_self_assignment_returns_MSG79_when_the_organizer_withdraws(bool selfAssignment)
    {
        var creator = GivenCreator();
        var meeting = OwnedMeetingBy(creator, selfAssignment ? creator.Id : null);
        meeting.Status = MeetingStatus.WaitingApproval;
        _factory.Meetings.Meetings.Add(meeting);
        _factory.Meetings.UserCapabilities[creator.Id] = [Capability.MeetingCreator, Capability.MeetingApprover];
        using var client = _factory.ClientFor(creator, Capability.MeetingCreator, Capability.MeetingApprover);

        var response = await client.PostAsJsonAsync($"/api/meetings/{meeting.Id}/transitions",
            new { action = "ReturnToDraft" });
        using var body = await JsonOf(response);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal("MSG79", body.RootElement.GetProperty("code").GetString());
        Assert.Equal("WaitingApproval", body.RootElement.GetProperty("history").GetProperty("fromStatus").GetString());
        Assert.Equal("Draft", body.RootElement.GetProperty("history").GetProperty("resultingStatus").GetString());
        Assert.Equal(MeetingStatus.Draft, meeting.Status);
        Assert.Null(meeting.AssignedApproverId);
        Assert.Equal(MeetingHistoryAction.ReturnToDraft, Assert.Single(meeting.StatusHistory).Action);
        Assert.Empty(_factory.Meetings.Notifications);
        Assert.Empty(_factory.Meetings.EmailMessages);
        Assert.Empty(_factory.Meetings.StagedPublicationIds);
    }

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
        Assert.Equal("MSG78", body.RootElement.GetProperty("code").GetString());
        Assert.Equal("WaitingApproval", body.RootElement.GetProperty("meeting").GetProperty("status").GetString());
        Assert.Equal("SubmitForApproval", body.RootElement.GetProperty("history").GetProperty("action").GetString());
        Assert.Equal(approverId, body.RootElement.GetProperty("history").GetProperty("assignedApproverId").GetGuid());
        var notification = Assert.Single(_factory.Meetings.Notifications);
        Assert.Equal("Meeting", notification.TargetType);
        Assert.Equal(meeting.Id, notification.TargetId);
        Assert.Empty(_factory.Meetings.EmailMessages);
    }

    [Theory]
    [InlineData(true, true)]
    [InlineData(false, true)]
    [InlineData(false, false)]
    public async Task Waiting_withdrawal_checks_current_approver_and_organizer_permissions(
        bool approverEligible, bool organizerHasCapability)
    {
        var creator = GivenCreator();
        var approverId = Guid.CreateVersion7();
        var meeting = OwnedMeetingBy(creator, approverId);
        meeting.Status = MeetingStatus.WaitingApproval;
        _factory.Meetings.Meetings.Add(meeting);
        if (organizerHasCapability)
            _factory.Meetings.UserCapabilities[creator.Id] = [Capability.MeetingCreator];
        if (approverEligible)
            _factory.Meetings.UserCapabilities[approverId] = [Capability.MeetingApprover];
        using var client = _factory.ClientFor(creator);

        var response = await client.PostAsJsonAsync($"/api/meetings/{meeting.Id}/transitions",
            new { action = "ReturnToDraft" });
        using var body = await JsonOf(response);

        var success = organizerHasCapability && !approverEligible;
        Assert.Equal(!organizerHasCapability ? HttpStatusCode.Forbidden
            : approverEligible ? HttpStatusCode.Conflict : HttpStatusCode.OK, response.StatusCode);
        Assert.Equal(!organizerHasCapability ? "MSG85" : approverEligible ? "MSG133" : "MSG79",
            body.RootElement.GetProperty("code").GetString());
        Assert.Equal(success ? MeetingStatus.Draft : MeetingStatus.WaitingApproval, meeting.Status);
        Assert.Equal(success ? null : (Guid?)approverId, meeting.AssignedApproverId);
        Assert.Equal(success ? 1 : 0, meeting.StatusHistory.Count);
        if (success)
        {
            Assert.Equal("WaitingApproval", body.RootElement.GetProperty("history").GetProperty("fromStatus").GetString());
            Assert.Equal("Draft", body.RootElement.GetProperty("history").GetProperty("resultingStatus").GetString());
        }
        Assert.Empty(_factory.Meetings.Notifications);
        Assert.Empty(_factory.Meetings.EmailMessages);
        Assert.Empty(_factory.Meetings.StagedPublicationIds);
    }

    [Fact]
    public async Task Current_approver_cannot_withdraw_a_blocked_meeting_for_the_organizer()
    {
        var creator = GivenCreator();
        var approver = GivenMember("blocked-approver@trustcabinet.local");
        var meeting = OwnedMeetingBy(creator, approver.Id);
        meeting.Status = MeetingStatus.WaitingApproval;
        _factory.Meetings.Meetings.Add(meeting);
        _factory.Meetings.UserCapabilities[approver.Id] = [Capability.MeetingCreator];
        using var client = _factory.ClientFor(approver, Capability.MeetingCreator);

        var response = await client.PostAsJsonAsync($"/api/meetings/{meeting.Id}/transitions",
            new { action = "ReturnToDraft" });

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
        Assert.Equal(MeetingStatus.WaitingApproval, meeting.Status);
        Assert.Empty(meeting.StatusHistory);
        Assert.Empty(_factory.Meetings.Notifications);
        Assert.Empty(_factory.Meetings.StagedPublicationIds);
    }

    [Theory]
    [InlineData("Approve", "MSG43")]
    [InlineData("Reject", "MSG44")]
    [InlineData("ReturnToDraft", "MSG79")]
    [InlineData("Publish", "MSG45")]
    public async Task Post_transition_returns_action_specific_success_codes(string action, string expectedCode)
    {
        var creator = GivenCreator();
        var actor = creator;
        Guid? approverId = null;
        var meeting = OwnedMeetingBy(creator);
        if (action is "Approve" or "Reject")
        {
            var approver = GivenMember($"{action.ToLowerInvariant()}@trustcabinet.local");
            actor = approver;
            approverId = approver.Id;
            meeting.AssignedApproverId = approver.Id;
            meeting.Status = MeetingStatus.WaitingApproval;
            _factory.Meetings.UserCapabilities[approver.Id] = [Capability.MeetingApprover];
        }
        else
        {
            meeting.Status = action == "ReturnToDraft" ? MeetingStatus.Rejected : MeetingStatus.Approved;
            if (action == "Publish")
            {
                AddPublishRoster(meeting);
            }
            _factory.Meetings.UserCapabilities[creator.Id] = [Capability.MeetingCreator];
        }

        _factory.Meetings.Meetings.Add(meeting);
        using var client = _factory.ClientFor(actor);
        var json = action == "Reject"
            ? JsonSerializer.Serialize(new { action, reason = "Cần bổ sung thông tin" })
            : JsonSerializer.Serialize(new { action });
        var response = await client.PostAsync(
            $"/api/meetings/{meeting.Id}/transitions",
            new StringContent(json, Encoding.UTF8, "application/json"));
        using var body = await JsonOf(response);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal(expectedCode, body.RootElement.GetProperty("code").GetString());
    }

    [Theory]
    [InlineData("Approve", MeetingStatus.Rejected, "MSG132")]
    [InlineData("ReturnToDraft", MeetingStatus.Draft, "MSG133")]
    [InlineData("Publish", MeetingStatus.Draft, "MSG46")]
    public async Task Post_transition_returns_action_specific_conflict_codes(
        string action, MeetingStatus status, string expectedCode)
    {
        var creator = GivenCreator();
        var actor = creator;
        var meeting = OwnedMeetingBy(creator);
        meeting.Status = status;
        if (action == "Approve")
        {
            var approver = GivenMember("conflict-approver@trustcabinet.local");
            actor = approver;
            meeting.AssignedApproverId = approver.Id;
            _factory.Meetings.UserCapabilities[approver.Id] = [Capability.MeetingApprover];
        }
        else
        {
            _factory.Meetings.UserCapabilities[creator.Id] = [Capability.MeetingCreator];
        }

        _factory.Meetings.Meetings.Add(meeting);
        using var client = _factory.ClientFor(actor);
        var response = await client.PostAsJsonAsync($"/api/meetings/{meeting.Id}/transitions", new { action });
        using var body = await JsonOf(response);

        Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);
        Assert.Equal(expectedCode, body.RootElement.GetProperty("code").GetString());
    }

    [Theory]
    [InlineData("SubmitForApproval", "MSG42")]
    [InlineData("Approve", "MSG88")]
    [InlineData("Publish", "MSG47")]
    public async Task Post_transition_maps_persistence_failures_to_action_specific_codes(
        string action, string expectedCode)
    {
        var creator = GivenCreator();
        var actor = creator;
        var meeting = OwnedMeetingBy(creator);
        Guid? approverId = null;
        switch (action)
        {
            case "SubmitForApproval":
                approverId = Guid.CreateVersion7();
                meeting.AgendaItems.Add(new AgendaItem
                {
                    MeetingId = meeting.Id,
                    Position = 1,
                    Title = "Báo cáo",
                    PresenterId = Guid.CreateVersion7(),
                });
                _factory.Meetings.UserCapabilities[creator.Id] = [Capability.MeetingCreator];
                break;
            case "Approve":
                actor = GivenMember("approve-save-failure@trustcabinet.local");
                approverId = actor.Id;
                meeting.AssignedApproverId = actor.Id;
                meeting.Status = MeetingStatus.WaitingApproval;
                _factory.Meetings.UserCapabilities[actor.Id] = [Capability.MeetingApprover];
                break;
            case "Publish":
                meeting.Status = MeetingStatus.Approved;
                AddPublishRoster(meeting);
                _factory.Meetings.UserCapabilities[creator.Id] = [Capability.MeetingCreator];
                break;
        }

        _factory.Meetings.Meetings.Add(meeting);
        _factory.Meetings.NextSaveThrows = new Npgsql.NpgsqlException("database unavailable");
        using var client = _factory.ClientFor(actor);
        var json = action == "SubmitForApproval"
            ? JsonSerializer.Serialize(new { action, assignedApproverId = approverId })
            : JsonSerializer.Serialize(new { action });
        var response = await client.PostAsync(
            $"/api/meetings/{meeting.Id}/transitions",
            new StringContent(json, Encoding.UTF8, "application/json"));
        using var body = await JsonOf(response);

        Assert.Equal(HttpStatusCode.ServiceUnavailable, response.StatusCode);
        Assert.Equal(expectedCode, body.RootElement.GetProperty("code").GetString());
    }

    [Fact]
    public async Task Post_submit_returns_MSG81_when_not_ready_and_MSG82_for_an_ineligible_approver()
    {
        var creator = GivenCreator();
        var approverId = Guid.CreateVersion7();
        var meeting = OwnedMeetingBy(creator);
        meeting.Title = " ";
        _factory.Meetings.Meetings.Add(meeting);
        _factory.Meetings.UserCapabilities[creator.Id] = [Capability.MeetingCreator];
        using var client = _factory.ClientFor(creator);

        var notReady = await client.PostAsJsonAsync($"/api/meetings/{meeting.Id}/transitions", new
        {
            action = "SubmitForApproval",
            assignedApproverId = approverId,
        });
        using var notReadyBody = await JsonOf(notReady);
        meeting.Title = "Giao ban tuần";
        _factory.Meetings.IneligibleApproverIds.Add(approverId);
        var ineligible = await client.PostAsJsonAsync($"/api/meetings/{meeting.Id}/transitions", new
        {
            action = "SubmitForApproval",
            assignedApproverId = approverId,
        });
        using var ineligibleBody = await JsonOf(ineligible);

        Assert.Equal(HttpStatusCode.BadRequest, notReady.StatusCode);
        Assert.Equal("MSG81", notReadyBody.RootElement.GetProperty("code").GetString());
        Assert.Equal(HttpStatusCode.BadRequest, ineligible.StatusCode);
        Assert.Equal("MSG82", ineligibleBody.RootElement.GetProperty("code").GetString());
    }

    [Fact]
    public async Task Post_reject_returns_MSG84_when_reason_exceeds_1000_characters()
    {
        var creator = GivenCreator();
        var approver = GivenMember("long-reason-approver@trustcabinet.local");
        var meeting = OwnedMeetingBy(creator, approver.Id);
        meeting.Status = MeetingStatus.WaitingApproval;
        _factory.Meetings.Meetings.Add(meeting);
        _factory.Meetings.UserCapabilities[approver.Id] = [Capability.MeetingApprover];
        using var client = _factory.ClientFor(approver);

        var response = await client.PostAsJsonAsync($"/api/meetings/{meeting.Id}/transitions", new
        {
            action = "Reject",
            reason = new string('x', 1001),
        });
        using var body = await JsonOf(response);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Equal("MSG84", body.RootElement.GetProperty("code").GetString());
    }

    [Fact]
    public async Task Publish_returns_MSG46_when_the_owner_lacks_the_required_capability()
    {
        var creator = GivenCreator();
        var meeting = OwnedMeetingBy(creator);
        meeting.Status = MeetingStatus.Approved;
        AddPublishRoster(meeting);
        _factory.Meetings.Meetings.Add(meeting);
        using var client = _factory.ClientFor(creator);

        var response = await client.PostAsJsonAsync($"/api/meetings/{meeting.Id}/transitions", new
        {
            action = "Publish",
        });
        using var body = await JsonOf(response);

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
        Assert.Equal("MSG46", body.RootElement.GetProperty("code").GetString());
        Assert.Equal(
            "Cuộc họp này không còn có thể công bố.",
            body.RootElement.GetProperty("message").GetString());
        Assert.Equal(MeetingStatus.Approved, meeting.Status);
        Assert.Empty(meeting.StatusHistory);
        Assert.Empty(_factory.Meetings.Notifications);
        Assert.Empty(_factory.Meetings.StagedPublicationIds);
    }



    [Fact]
    public async Task Publish_returns_MSG137_when_the_required_roster_is_not_ready()
    {
        var creator = GivenCreator();
        var meeting = OwnedMeetingBy(creator);
        meeting.Status = MeetingStatus.Approved;
        _factory.Meetings.Meetings.Add(meeting);
        _factory.Meetings.UserCapabilities[creator.Id] = [Capability.MeetingCreator];
        using var client = _factory.ClientFor(creator, Capability.MeetingCreator);

        var response = await client.PostAsJsonAsync(
            $"/api/meetings/{meeting.Id}/transitions", new { action = "Publish" });
        using var body = await JsonOf(response);

        Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);
        Assert.Equal("MSG137", body.RootElement.GetProperty("code").GetString());
        Assert.Equal(
            "Cuộc họp cần có đúng một Chủ tọa đang hoạt động và ít nhất một Thư ký đang hoạt động trước khi công bố.",
            body.RootElement.GetProperty("message").GetString());
        Assert.Equal(MeetingStatus.Approved, meeting.Status);
        Assert.Empty(meeting.StatusHistory);
        Assert.Empty(_factory.Meetings.Notifications);
        Assert.Empty(_factory.Meetings.EmailMessages);
        Assert.Empty(_factory.Meetings.StagedPublicationIds);
        Assert.Equal(0, _factory.Meetings.SaveCount);
    }

    [Fact]
    public async Task Get_workflow_endpoints_return_MSG136_and_MSG131_for_transient_read_failures()
    {
        var creator = GivenCreator();
        var meeting = OwnedMeetingBy(creator);
        _factory.Meetings.Meetings.Add(meeting);
        using var client = _factory.ClientFor(creator);
        _factory.Meetings.NextWorkflowReadThrows = new Npgsql.NpgsqlException("database unavailable");

        var readiness = await client.GetAsync($"/api/meetings/{meeting.Id}/submission-readiness");
        using var readinessBody = await JsonOf(readiness);
        _factory.Accounts.NextReadThrows = new Npgsql.NpgsqlException("database unavailable");
        var history = await client.GetAsync($"/api/meetings/{meeting.Id}/history");
        using var historyBody = await JsonOf(history);

        Assert.Equal(HttpStatusCode.ServiceUnavailable, readiness.StatusCode);
        Assert.Equal("MSG136", readinessBody.RootElement.GetProperty("code").GetString());
        Assert.Equal(HttpStatusCode.ServiceUnavailable, history.StatusCode);
        Assert.Equal("MSG131", historyBody.RootElement.GetProperty("code").GetString());
    }

    [Fact]
    public async Task Post_transition_requires_json_and_returns_MSG34()
    {
        var creator = GivenCreator();
        var meeting = OwnedMeetingBy(creator);
        _factory.Meetings.Meetings.Add(meeting);
        using var client = _factory.ClientFor(creator);
        using var request = new HttpRequestMessage(HttpMethod.Post, $"/api/meetings/{meeting.Id}/transitions")
        {
            Content = new StringContent("{}", Encoding.UTF8, "text/plain"),
        };

        var response = await client.SendAsync(request);
        using var body = await JsonOf(response);

        Assert.Equal(HttpStatusCode.UnsupportedMediaType, response.StatusCode);
        Assert.Equal("MSG34", body.RootElement.GetProperty("code").GetString());
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
        var nullReasonOnReturn = await client.PostAsync(
            $"/api/meetings/{meeting.Id}/transitions",
            new StringContent("{\"action\":\"ReturnToDraft\",\"reason\":null}", Encoding.UTF8, "application/json"));
        using var nullReasonBody = await JsonOf(nullReasonOnReturn);
        var numericAction = await client.PostAsync(
            $"/api/meetings/{meeting.Id}/transitions",
            new StringContent("{\"action\":0}", Encoding.UTF8, "application/json"));

        Assert.Equal(HttpStatusCode.BadRequest, unknownField.StatusCode);
        Assert.Equal("MSG80", unknownBody.RootElement.GetProperty("code").GetString());
        Assert.Equal(HttpStatusCode.BadRequest, nullReasonOnReturn.StatusCode);
        Assert.Equal("MSG80", nullReasonBody.RootElement.GetProperty("code").GetString());
        Assert.Equal(HttpStatusCode.BadRequest, numericAction.StatusCode);
        Assert.Equal(MeetingStatus.Draft, meeting.Status);
        Assert.Empty(meeting.StatusHistory);
    }

    [Theory]
    [InlineData("4")]
    [InlineData("04")]
    [InlineData("+4")]
    [InlineData("SubmitForApproval,Publish")]
    [InlineData("Publish,Publish")]
    [InlineData(" Publish ")]
    [InlineData("publish")]
    public async Task Post_transition_requires_one_exact_action_name_without_publishing(string action)
    {
        var creator = GivenCreator();
        var meeting = OwnedMeetingBy(creator);
        meeting.Status = MeetingStatus.Approved;
        AddPublishRoster(meeting);
        _factory.Meetings.Meetings.Add(meeting);
        _factory.Meetings.UserCapabilities[creator.Id] = [Capability.MeetingCreator];
        using var client = _factory.ClientFor(creator, Capability.MeetingCreator);

        var response = await client.PostAsJsonAsync(
            $"/api/meetings/{meeting.Id}/transitions", new { action });
        using var body = await JsonOf(response);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Equal("MSG80", body.RootElement.GetProperty("code").GetString());
        Assert.Equal(MeetingStatus.Approved, meeting.Status);
        Assert.Empty(meeting.StatusHistory);
        Assert.Empty(_factory.Meetings.Notifications);
        Assert.Empty(_factory.Meetings.EmailMessages);
    }

    [Fact]
    public async Task Post_transition_maps_null_rejection_reason_to_required_reason_error()
    {
        var creator = GivenCreator();
        var approver = GivenMember("null-reason-approver@trustcabinet.local");
        var meeting = OwnedMeetingBy(creator, approver.Id);
        meeting.Status = MeetingStatus.WaitingApproval;
        _factory.Meetings.Meetings.Add(meeting);
        _factory.Meetings.UserCapabilities[approver.Id] = [Capability.MeetingApprover];
        using var client = _factory.ClientFor(approver, Capability.MeetingApprover);

        var response = await client.PostAsync(
            $"/api/meetings/{meeting.Id}/transitions",
            new StringContent("{\"action\":\"Reject\",\"reason\":null}", Encoding.UTF8, "application/json"));
        using var body = await JsonOf(response);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Equal("MSG83", body.RootElement.GetProperty("code").GetString());
        Assert.Equal(MeetingStatus.WaitingApproval, meeting.Status);
        Assert.Empty(meeting.StatusHistory);
    }

    [Fact]
    public async Task Post_transition_resubmits_rejected_directly_and_preserves_the_previous_rejection()
    {
        var creator = GivenCreator();
        var formerApproverId = Guid.CreateVersion7();
        var newApproverId = Guid.CreateVersion7();
        var meeting = OwnedMeetingBy(creator, formerApproverId);
        meeting.Status = MeetingStatus.Rejected;
        var rejection = new MeetingStatusHistory
        {
            MeetingId = meeting.Id,
            FromStatus = MeetingStatus.WaitingApproval,
            ToStatus = MeetingStatus.Rejected,
            Action = MeetingHistoryAction.Reject,
            ActorId = formerApproverId,
            AssignedApproverId = formerApproverId,
            Reason = "Cần bổ sung số liệu",
            OccurredAt = DateTimeOffset.UtcNow,
        };
        meeting.StatusHistory.Add(rejection);
        _factory.Meetings.Meetings.Add(meeting);
        _factory.Meetings.UserCapabilities[creator.Id] = [Capability.MeetingCreator];
        using var client = _factory.ClientFor(creator, Capability.MeetingCreator);

        var readinessResponse = await client.GetAsync($"/api/meetings/{meeting.Id}/submission-readiness");
        using var readinessBody = await JsonOf(readinessResponse);
        Assert.Equal(HttpStatusCode.OK, readinessResponse.StatusCode);
        Assert.True(readinessBody.RootElement.GetProperty("ready").GetBoolean());

        var response = await client.PostAsJsonAsync($"/api/meetings/{meeting.Id}/transitions", new
        {
            action = "SubmitForApproval",
            assignedApproverId = newApproverId,
        });
        using var body = await JsonOf(response);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal("MSG78", body.RootElement.GetProperty("code").GetString());
        Assert.Equal(MeetingStatus.WaitingApproval, meeting.Status);
        Assert.Equal(newApproverId, meeting.AssignedApproverId);
        Assert.Equal(2, meeting.StatusHistory.Count);
        Assert.Same(rejection, meeting.StatusHistory.First());
        Assert.Equal("Cần bổ sung số liệu", rejection.Reason);
        Assert.Equal(formerApproverId, rejection.AssignedApproverId);
        var submission = meeting.StatusHistory.Last();
        Assert.Equal(MeetingStatus.Rejected, submission.FromStatus);
        Assert.Equal(MeetingHistoryAction.SubmitForApproval, submission.Action);
        Assert.Equal(newApproverId, Assert.Single(_factory.Meetings.Notifications).UserId);
        Assert.Empty(_factory.Meetings.EmailMessages);
        Assert.Empty(_factory.Meetings.StagedPublicationIds);
    }

    [Fact]
    public async Task Submit_and_readiness_from_approved_return_MSG41_without_side_effects()
    {
        var creator = GivenCreator();
        var meeting = OwnedMeetingBy(creator, Guid.CreateVersion7());
        meeting.Status = MeetingStatus.Approved;
        _factory.Meetings.Meetings.Add(meeting);
        _factory.Meetings.UserCapabilities[creator.Id] = [Capability.MeetingCreator];
        using var client = _factory.ClientFor(creator, Capability.MeetingCreator);

        var readiness = await client.GetAsync($"/api/meetings/{meeting.Id}/submission-readiness");
        using var readinessBody = await JsonOf(readiness);
        Assert.Equal(HttpStatusCode.Conflict, readiness.StatusCode);
        Assert.Equal("MSG41", readinessBody.RootElement.GetProperty("code").GetString());

        var submission = await client.PostAsJsonAsync($"/api/meetings/{meeting.Id}/transitions", new
        {
            action = "SubmitForApproval",
            assignedApproverId = Guid.CreateVersion7(),
        });
        using var submissionBody = await JsonOf(submission);
        Assert.Equal(HttpStatusCode.Conflict, submission.StatusCode);
        Assert.Equal("MSG41", submissionBody.RootElement.GetProperty("code").GetString());
        Assert.Equal(MeetingStatus.Approved, meeting.Status);
        Assert.Empty(meeting.StatusHistory);
        Assert.Empty(_factory.Meetings.Notifications);
        Assert.Empty(_factory.Meetings.EmailMessages);
        Assert.Empty(_factory.Meetings.StagedPublicationIds);
        Assert.Equal(0, _factory.Meetings.SaveCount);
    }

    [Fact]
    public async Task Get_submission_readiness_does_not_require_agenda_or_sessions_and_does_not_write()
    {
        var creator = GivenCreator();
        var meeting = OwnedMeetingBy(creator, Guid.CreateVersion7());
        _factory.Meetings.Meetings.Add(meeting);
        _factory.Meetings.UserCapabilities[creator.Id] = [Capability.MeetingCreator];
        using var client = _factory.ClientFor(creator, Capability.MeetingCreator);

        var response = await client.GetAsync($"/api/meetings/{meeting.Id}/submission-readiness");
        using var body = await JsonOf(response);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.True(body.RootElement.GetProperty("ready").GetBoolean());
        Assert.Empty(body.RootElement.GetProperty("errors").EnumerateObject());
        Assert.Equal(MeetingStatus.Draft, meeting.Status);
        Assert.Equal(0, _factory.Meetings.SaveCount);
    }

    [Fact]
    public async Task Publish_without_a_meeting_session_returns_MSG45_and_commits()
    {
        var creator = GivenCreator();
        var meeting = OwnedMeetingBy(creator);
        meeting.Status = MeetingStatus.Approved;
        AddPublishRoster(meeting);
        _factory.Meetings.Meetings.Add(meeting);
        _factory.Meetings.UserCapabilities[creator.Id] = [Capability.MeetingCreator];
        using var client = _factory.ClientFor(creator, Capability.MeetingCreator);

        var response = await client.PostAsJsonAsync($"/api/meetings/{meeting.Id}/transitions",
            new { action = "Publish" });
        using var body = await JsonOf(response);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal("MSG45", body.RootElement.GetProperty("code").GetString());
        Assert.Equal(MeetingStatus.Published, meeting.Status);
        Assert.Equal(MeetingHistoryAction.Publish, Assert.Single(meeting.StatusHistory).Action);
        Assert.Equal(2, _factory.Meetings.Notifications.Count);
        Assert.Empty(_factory.Meetings.EmailMessages);
        Assert.Equal(meeting.Id, Assert.Single(_factory.Meetings.StagedPublicationIds));
        Assert.Equal(1, _factory.Meetings.SaveCount);
    }

    [Theory]
    [InlineData(MeetingStatus.Draft)]
    [InlineData(MeetingStatus.Rejected)]
    [InlineData(MeetingStatus.Approved)]
    public async Task Assigned_secretary_can_read_detail_list_and_rejection_before_publication(MeetingStatus status)
    {
        var creator = GivenCreator();
        var secretary = GivenMember("assigned-secretary@trustcabinet.local");
        var meeting = OwnedMeetingBy(creator);
        meeting.Status = status;
        meeting.Participants.Add(new MeetingParticipant
        {
            MeetingId = meeting.Id,
            UserId = secretary.Id,
            User = secretary,
            Role = MeetingRole.Secretary,
            Response = InvitationResponse.Accepted,
        });
        meeting.StatusHistory.Add(new MeetingStatusHistory
        {
            MeetingId = meeting.Id,
            Action = MeetingHistoryAction.Reject,
            FromStatus = MeetingStatus.WaitingApproval,
            ToStatus = MeetingStatus.Rejected,
            ActorId = Guid.CreateVersion7(),
            Reason = "Lý do dành cho thư ký",
        });
        meeting.StatusHistory.Add(new MeetingStatusHistory
        {
            MeetingId = meeting.Id,
            Action = MeetingHistoryAction.TransferOrganizer,
            FromStatus = MeetingStatus.Approved,
            ToStatus = MeetingStatus.Approved,
            ActorId = Guid.CreateVersion7(),
            PreviousOrganizerId = Guid.CreateVersion7(),
            NewOrganizerId = creator.Id,
        });
        _factory.Meetings.Meetings.Add(meeting);
        using var client = _factory.ClientFor(secretary);

        var response = await client.GetAsync($"/api/meetings/{meeting.Id}/history");
        using var body = await JsonOf(response);
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var entries = body.RootElement.GetProperty("items").EnumerateArray().ToArray();
        Assert.Equal("Lý do dành cho thư ký", Assert.Single(entries,
            item => item.GetProperty("action").GetString() == "Reject")
            .GetProperty("rejectionReason").GetString());
        Assert.All(entries, item =>
        {
            Assert.Equal(JsonValueKind.Null, item.GetProperty("previousOrganizerId").ValueKind);
            Assert.Equal(JsonValueKind.Null, item.GetProperty("newOrganizerId").ValueKind);
        });
        Assert.Equal(HttpStatusCode.OK, (await client.GetAsync($"/api/meetings/{meeting.Id}")).StatusCode);
        using var listBody = await JsonOf(await client.GetAsync("/api/meetings"));
        Assert.Equal(meeting.Id, Assert.Single(listBody.RootElement.GetProperty("items").EnumerateArray())
            .GetProperty("id").GetGuid());
    }

    [Theory]
    [InlineData("globalRoleOnly")]
    [InlineData("otherMeeting")]
    [InlineData("delegated")]
    [InlineData("delegationTarget")]
    public async Task Invalid_secretary_assignment_does_not_grant_history_access(string invalid)
    {
        var creator = GivenCreator();
        var secretary = GivenCreator("unassigned-secretary@trustcabinet.local");
        var meeting = OwnedMeetingBy(creator);
        meeting.Status = MeetingStatus.Rejected;
        if (invalid != "globalRoleOnly")
        {
            meeting.Participants.Add(new MeetingParticipant
            {
                MeetingId = invalid == "otherMeeting" ? Guid.CreateVersion7() : meeting.Id,
                UserId = secretary.Id,
                User = secretary,
                Role = MeetingRole.Secretary,
                Response = invalid == "delegated" ? InvitationResponse.Delegated : InvitationResponse.Accepted,
                DelegatedToUserId = invalid == "delegationTarget" ? Guid.CreateVersion7() : null,
            });
        }
        _factory.Meetings.Meetings.Add(meeting);
        using var client = _factory.ClientFor(secretary, Capability.MeetingCreator);

        var response = await client.GetAsync($"/api/meetings/{meeting.Id}/history");

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
        Assert.Equal(string.Empty, await response.Content.ReadAsStringAsync());
    }

    [Fact]
    public async Task Get_history_returns_full_history_to_the_current_assigned_approver()
    {
        var creator = GivenCreator();
        var approver = GivenMember("assigned-history@trustcabinet.local");
        var meeting = OwnedMeetingBy(creator, approver.Id);
        meeting.Status = MeetingStatus.Rejected;
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
    public async Task Assigned_chair_can_read_before_publication()
    {
        var creator = GivenCreator();
        var chair = GivenMember("history-chair@trustcabinet.local");
        var meeting = OwnedMeetingBy(creator);
        meeting.Status = MeetingStatus.Rejected;
        meeting.Participants.Add(new MeetingParticipant
        {
            MeetingId = meeting.Id,
            UserId = chair.Id,
            User = chair,
            Role = MeetingRole.Chair,
            Response = InvitationResponse.Accepted,
        });
        meeting.StatusHistory.Add(new MeetingStatusHistory
        {
            MeetingId = meeting.Id,
            FromStatus = MeetingStatus.WaitingApproval,
            ToStatus = MeetingStatus.Rejected,
            Action = MeetingHistoryAction.Reject,
            ActorId = Guid.CreateVersion7(),
            Reason = "Lý do chỉ dành cho người tổ chức và thư ký",
            PreviousOrganizerId = Guid.CreateVersion7(),
            NewOrganizerId = creator.Id,
            OccurredAt = DateTimeOffset.UtcNow,
        });
        _factory.Meetings.Meetings.Add(meeting);
        using var client = _factory.ClientFor(chair);

        Assert.Equal(HttpStatusCode.OK, (await client.GetAsync($"/api/meetings/{meeting.Id}")).StatusCode);
        var historyResponse = await client.GetAsync($"/api/meetings/{meeting.Id}/history");
        Assert.Equal(HttpStatusCode.OK, historyResponse.StatusCode);
    }

    [Fact]
    public async Task Get_history_does_not_treat_a_legacy_draft_approver_as_current()
    {
        var creator = GivenCreator();
        var formerApprover = GivenMember("legacy-draft-approver@trustcabinet.local");
        var meeting = OwnedMeetingBy(creator, formerApprover.Id);
        meeting.StatusHistory.Add(new MeetingStatusHistory
        {
            MeetingId = meeting.Id,
            FromStatus = null,
            ToStatus = MeetingStatus.Draft,
            Action = MeetingHistoryAction.Create,
            ActorId = creator.Id,
            OccurredAt = DateTimeOffset.UtcNow,
        });
        _factory.Meetings.Meetings.Add(meeting);
        using var client = _factory.ClientFor(formerApprover);

        var detail = await client.GetAsync($"/api/meetings/{meeting.Id}");
        var history = await client.GetAsync($"/api/meetings/{meeting.Id}/history");
        var list = await client.GetAsync("/api/meetings");

        Assert.Equal(HttpStatusCode.NotFound, detail.StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, history.StatusCode);
        var action = await client.PostAsJsonAsync($"/api/meetings/{meeting.Id}/transitions", new
        {
            action = "Approve",
        });
        Assert.Equal(HttpStatusCode.NotFound, action.StatusCode);
        using var listBody = await JsonOf(list);
        Assert.Empty(listBody.RootElement.GetProperty("items").EnumerateArray());
    }

    [Fact]
    public async Task Get_history_returns_empty_for_an_out_of_range_page_before_offset_cast()
    {
        var creator = GivenCreator();
        var meeting = OwnedMeetingBy(creator);
        meeting.StatusHistory.Add(new MeetingStatusHistory
        {
            MeetingId = meeting.Id,
            ToStatus = MeetingStatus.Draft,
            Action = MeetingHistoryAction.Create,
            ActorId = creator.Id,
            OccurredAt = DateTimeOffset.UtcNow,
        });
        _factory.Meetings.Meetings.Add(meeting);
        using var client = _factory.ClientFor(creator);

        var response = await client.GetAsync(
            $"/api/meetings/{meeting.Id}/history?page=2147483647&pageSize=100");
        using var body = await JsonOf(response);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Empty(body.RootElement.GetProperty("items").EnumerateArray());
        Assert.Equal(1, body.RootElement.GetProperty("totalCount").GetInt64());
    }

    [Fact]
    public async Task Get_history_returns_invalid_query_when_offset_is_unrepresentable_within_data()
    {
        var creator = GivenCreator();
        var meeting = OwnedMeetingBy(creator);
        _factory.Meetings.Meetings.Add(meeting);
        _factory.Meetings.HistoryTotalCountOverride = long.MaxValue;
        using var client = _factory.ClientFor(creator);

        var response = await client.GetAsync(
            $"/api/meetings/{meeting.Id}/history?page=2147483647&pageSize=100");
        using var body = await JsonOf(response);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Equal("MSG86", body.RootElement.GetProperty("code").GetString());
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
            ["id", "title", "plannedStartTime", "plannedEndTime", "mode", "location", "createdAt", "status", "sensitivity",
                "myResponse", "canRespond"],
            item.EnumerateObject().Select(property => property.Name));
    }

    [Fact]
    public async Task List_returns_empty_page_for_no_match_or_page_beyond_end()
    {
        var creator = GivenCreator();
        _factory.Meetings.Meetings.Add(OwnedMeetingBy(creator));
        using var client = _factory.ClientFor(creator);

        var noMatch = await client.GetAsync("/api/meetings?status=Ended");
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
        Assert.Equal("MSG32", body.RootElement.GetProperty("code").GetString());
        Assert.Equal(
            "Tiêu chí danh sách cuộc họp không hợp lệ.",
            body.RootElement.GetProperty("message").GetString());
        Assert.True(body.RootElement.GetProperty("errors").TryGetProperty("status", out _));
        Assert.True(body.RootElement.GetProperty("errors").TryGetProperty("page", out _));
        Assert.Null(_factory.Meetings.LastListQuery);
    }

    [Fact]
    public async Task List_returns_MSG38_and_500_when_the_database_read_fails()
    {
        _factory.Meetings.NextReadThrows = new Npgsql.NpgsqlException("database unavailable");
        using var client = _factory.ClientFor(GivenCreator());

        var response = await client.GetAsync("/api/meetings");
        using var body = await JsonOf(response);

        Assert.Equal(HttpStatusCode.InternalServerError, response.StatusCode);
        Assert.Equal("MSG38", body.RootElement.GetProperty("code").GetString());
        Assert.Equal("Không thể tải danh sách cuộc họp. Vui lòng thử lại.",
            body.RootElement.GetProperty("message").GetString());
    }

    [Fact]
    public async Task List_returns_MSG38_and_500_when_active_account_lookup_fails()
    {
        using var client = _factory.ClientFor(GivenCreator());
        _factory.Accounts.NextReadThrows = new Npgsql.NpgsqlException("database unavailable");

        var response = await client.GetAsync("/api/meetings");
        using var body = await JsonOf(response);

        Assert.Equal(HttpStatusCode.InternalServerError, response.StatusCode);
        Assert.Equal("MSG38", body.RootElement.GetProperty("code").GetString());
        Assert.Equal("Không thể tải danh sách cuộc họp. Vui lòng thử lại.",
            body.RootElement.GetProperty("message").GetString());
        Assert.Null(_factory.Meetings.LastListQuery);
    }

    [Fact]
    public async Task Post_creates_a_draft_and_returns_a_location_that_get_can_read()
    {
        var creator = GivenCreator();
        using var client = _factory.ClientFor(creator, Capability.MeetingCreator);

        var created = await client.PostAsJsonAsync("/api/meetings", ValidCreateRequest());
        using var body = await JsonOf(created);
        var meetingId = body.RootElement.GetProperty("meeting").GetProperty("id").GetGuid();

        Assert.Equal(HttpStatusCode.Created, created.StatusCode);
        Assert.Equal($"/api/meetings/{meetingId}", created.Headers.Location!.OriginalString);
        Assert.Equal("MSG29", body.RootElement.GetProperty("code").GetString());
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
            "/api/meetings", ValidCreateRequest(title: "   ") with { Mode = "Wrong" });
        using var body = await JsonOf(response);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Equal("MSG31", body.RootElement.GetProperty("code").GetString());
        Assert.True(body.RootElement.GetProperty("errors").TryGetProperty("title", out _));
        Assert.True(body.RootElement.GetProperty("errors").TryGetProperty("mode", out _));
        Assert.Empty(_factory.Meetings.Meetings);
    }

    [Fact]
    public async Task Post_requires_the_planned_schedule()
    {
        using var client = _factory.ClientFor(GivenCreator(), Capability.MeetingCreator);

        var response = await client.PostAsJsonAsync("/api/meetings", new { title = "Giao ban tuần" });
        using var body = await JsonOf(response);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Equal("MSG31", body.RootElement.GetProperty("code").GetString());
        var errors = body.RootElement.GetProperty("errors");
        Assert.True(errors.TryGetProperty("plannedStartTime", out _));
        Assert.True(errors.TryGetProperty("plannedEndTime", out _));
        Assert.Empty(_factory.Meetings.Meetings);
    }

    [Fact]
    public async Task Post_rejects_a_start_in_the_past_and_an_end_not_after_the_start()
    {
        using var client = _factory.ClientFor(GivenCreator(), Capability.MeetingCreator);

        var past = await client.PostAsJsonAsync("/api/meetings", ValidCreateRequest() with
        {
            PlannedStartTime = Iso(DateTimeOffset.UtcNow.AddHours(-2)),
            PlannedEndTime = Iso(DateTimeOffset.UtcNow.AddHours(-1)),
        });
        using var pastBody = await JsonOf(past);
        var backwards = await client.PostAsJsonAsync("/api/meetings", ValidCreateRequest() with
        {
            PlannedEndTime = Iso(FutureStart),
        });
        using var backwardsBody = await JsonOf(backwards);

        Assert.Equal(HttpStatusCode.BadRequest, past.StatusCode);
        Assert.Equal(
            "Thời gian bắt đầu phải sau thời điểm hiện tại.",
            pastBody.RootElement.GetProperty("errors").GetProperty("plannedStartTime").GetString());
        Assert.Equal(HttpStatusCode.BadRequest, backwards.StatusCode);
        Assert.Equal(
            "Thời gian kết thúc phải sau thời gian bắt đầu.",
            backwardsBody.RootElement.GetProperty("errors").GetProperty("plannedEndTime").GetString());
        Assert.Empty(_factory.Meetings.Meetings);
    }

    [Fact]
    public async Task Post_returns_the_schedule_and_meeting_level_defaults()
    {
        var creator = GivenCreator();
        using var client = _factory.ClientFor(creator, Capability.MeetingCreator);
        var request = new
        {
            title = "Giao ban tuần",
            participants = new[] { new ParticipantSeed(creator.Id, "Chair") },
            plannedStartTime = Iso(FutureStart),
            plannedEndTime = Iso(FutureStart.AddMinutes(90)),
            onlineLink = "  ",
        };

        var response = await client.PostAsJsonAsync("/api/meetings", request);
        using var body = await JsonOf(response);
        var meeting = body.RootElement.GetProperty("meeting");

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        Assert.Equal("InPerson", meeting.GetProperty("mode").GetString());
        Assert.Equal("Normal", meeting.GetProperty("sensitivity").GetString());
        Assert.Equal(FutureStart, meeting.GetProperty("plannedStartTime").GetDateTimeOffset());
        Assert.Equal(FutureStart.AddMinutes(90), meeting.GetProperty("plannedEndTime").GetDateTimeOffset());
        Assert.Equal(JsonValueKind.Null, meeting.GetProperty("actualStartedAt").ValueKind);
        Assert.Equal(JsonValueKind.Null, meeting.GetProperty("actualEndedAt").ValueKind);
        Assert.Equal(JsonValueKind.Null, meeting.GetProperty("onlineLink").ValueKind);
        Assert.Equal(JsonValueKind.Null, meeting.GetProperty("assignedApproverId").ValueKind);
    }

    [Fact]
    public async Task Post_rejects_an_online_link_over_1000_characters()
    {
        using var client = _factory.ClientFor(GivenCreator(), Capability.MeetingCreator);

        var response = await client.PostAsJsonAsync("/api/meetings", ValidCreateRequest() with
        {
            OnlineLink = "https://meet.example.vn/" + new string('a', 1000),
        });
        using var body = await JsonOf(response);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.True(body.RootElement.GetProperty("errors").TryGetProperty("onlineLink", out _));
    }

    [Fact]
    public async Task Post_does_not_create_a_current_assignment_from_a_client_supplied_approver()
    {
        var creator = GivenCreator();
        using var client = _factory.ClientFor(creator, Capability.MeetingCreator);

        var response = await client.PostAsJsonAsync("/api/meetings", new
        {
            title = "Giao ban tuần",
            participants = new[] { new ParticipantSeed(creator.Id, "Chair") },
            plannedStartTime = Iso(FutureStart),
            plannedEndTime = Iso(FutureStart.AddHours(1)),
            assignedApproverId = Guid.CreateVersion7(),
        });
        using var body = await JsonOf(response);

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        Assert.Equal(JsonValueKind.Null,
            body.RootElement.GetProperty("meeting").GetProperty("assignedApproverId").ValueKind);
        Assert.Null(Assert.Single(_factory.Meetings.Meetings).AssignedApproverId);
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public async Task Get_approvers_excludes_the_caller_and_preserves_other_options(bool includeOtherApprover)
    {
        var creator = GivenCreator();
        var other = new MeetingApproverOption(Guid.CreateVersion7(), "An Nguyễn");
        _factory.Meetings.EligibleApprovers.Add(new MeetingApproverOption(creator.Id, creator.FullName));
        if (includeOtherApprover)
            _factory.Meetings.EligibleApprovers.Add(other);
        using var client = _factory.ClientFor(creator, Capability.MeetingCreator, Capability.MeetingApprover);

        var response = await client.GetAsync("/api/meetings/approvers");
        using var body = await JsonOf(response);
        var options = body.RootElement.EnumerateArray().ToArray();

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.DoesNotContain(options, option => option.GetProperty("id").GetGuid() == creator.Id);
        if (includeOtherApprover)
            Assert.Equal(other.Id, Assert.Single(options).GetProperty("id").GetGuid());
        else
            Assert.Empty(options);
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
    public async Task Get_approvers_returns_a_structured_503_when_the_repository_is_unavailable()
    {
        _factory.Meetings.NextApproverReadThrows = new Npgsql.NpgsqlException("database unavailable");
        using var client = _factory.ClientFor(GivenCreator(), Capability.MeetingCreator);

        var response = await client.GetAsync("/api/meetings/approvers");
        using var body = await JsonOf(response);

        Assert.Equal(HttpStatusCode.ServiceUnavailable, response.StatusCode);
        Assert.Equal("MSG130", body.RootElement.GetProperty("code").GetString());
        Assert.Equal(
            "Không thể tải danh sách người duyệt. Vui lòng thử lại.",
            body.RootElement.GetProperty("message").GetString());
    }

    [Fact]
    public async Task Get_approvers_returns_the_contract_503_when_capability_account_lookup_fails()
    {
        _factory.Accounts.NextReadThrows = new Npgsql.NpgsqlException("database unavailable");
        using var client = _factory.ClientFor(GivenCreator(), Capability.MeetingCreator);

        var response = await client.GetAsync("/api/meetings/approvers");
        using var body = await JsonOf(response);

        Assert.Equal(HttpStatusCode.ServiceUnavailable, response.StatusCode);
        Assert.Equal("MSG130", body.RootElement.GetProperty("code").GetString());
        Assert.Empty(_factory.Meetings.EligibleApprovers);
    }

    [Fact]
    public async Task Get_approvers_returns_the_contract_503_when_capability_read_times_out()
    {
        _factory.Accounts.NextCapabilitiesReadThrows = new TimeoutException("database timed out");
        using var client = _factory.ClientFor(GivenCreator(), Capability.MeetingCreator);

        var response = await client.GetAsync("/api/meetings/approvers");
        using var body = await JsonOf(response);

        Assert.Equal(HttpStatusCode.ServiceUnavailable, response.StatusCode);
        Assert.Equal("MSG130", body.RootElement.GetProperty("code").GetString());
        Assert.Empty(_factory.Meetings.EligibleApprovers);
    }

    [Fact]
    public async Task Get_approvers_propagates_request_cancellation_from_capability_lookup()
    {
        _factory.Accounts.WaitForReadCancellation = true;
        using var client = _factory.ClientFor(GivenCreator(), Capability.MeetingCreator);
        using var cancellation = new CancellationTokenSource();

        var request = client.GetAsync("/api/meetings/approvers", cancellation.Token);
        await _factory.Accounts.ReadCancellationWaitStarted.Task.WaitAsync(TimeSpan.FromSeconds(5));
        await cancellation.CancelAsync();

        await Assert.ThrowsAnyAsync<OperationCanceledException>(async () => await request);
    }

    [Fact]
    public async Task Missing_and_other_owner_resources_return_the_same_empty_404()
    {
        var creator = GivenCreator();
        var other = GivenCreator("other-meeting-creator@trustcabinet.local");
        using var creatorClient = _factory.ClientFor(creator, Capability.MeetingCreator);
        using var otherClient = _factory.ClientFor(other, Capability.MeetingCreator);
        var create = await creatorClient.PostAsJsonAsync("/api/meetings", ValidCreateRequest());
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
                or MeetingStatus.Ended;
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
        var create = await client.PostAsJsonAsync("/api/meetings", ValidCreateRequest());
        using var createBody = await JsonOf(create);
        var id = createBody.RootElement.GetProperty("meeting").GetProperty("id").GetGuid();

        var update = await client.PutAsJsonAsync($"/api/meetings/{id}", new
        {
            title = "Đã cập nhật",
            plannedStartTime = "2026-10-01T09:30:00+07:00",
            plannedEndTime = "2026-10-01T10:30:00+07:00",
            mode = "Hybrid",
            location = "Phòng họp B",
            sensitivity = "Secret",
            description = "Nội dung mới",
            assignedApproverId = Guid.CreateVersion7(),
            status = "Published",
            createdById = Guid.CreateVersion7(),
        });
        using var updateBody = await JsonOf(update);

        Assert.Equal(HttpStatusCode.OK, update.StatusCode);
        Assert.Equal("MSG30", updateBody.RootElement.GetProperty("code").GetString());
        Assert.Equal("Đã cập nhật", updateBody.RootElement.GetProperty("meeting").GetProperty("title").GetString());
        Assert.Equal("Hybrid", updateBody.RootElement.GetProperty("meeting").GetProperty("mode").GetString());
        Assert.Equal("Draft", updateBody.RootElement.GetProperty("meeting").GetProperty("status").GetString());
        Assert.Equal(creator.Id, updateBody.RootElement.GetProperty("meeting").GetProperty("createdById").GetGuid());
        Assert.Equal(JsonValueKind.Null,
            updateBody.RootElement.GetProperty("meeting").GetProperty("assignedApproverId").ValueKind);
    }

    [Fact]
    public async Task Put_updates_an_owned_rejected_meeting_without_restarting_approval()
    {
        var creator = GivenCreator();
        var approverId = Guid.CreateVersion7();
        var meeting = OwnedMeetingBy(creator, approverId);
        meeting.Status = MeetingStatus.Rejected;
        var rejection = new MeetingStatusHistory
        {
            MeetingId = meeting.Id,
            FromStatus = MeetingStatus.WaitingApproval,
            ToStatus = MeetingStatus.Rejected,
            Action = MeetingHistoryAction.Reject,
            ActorId = approverId,
            Reason = "Cần bổ sung số liệu",
            OccurredAt = DateTimeOffset.UtcNow,
        };
        meeting.StatusHistory.Add(rejection);
        _factory.Meetings.Meetings.Add(meeting);
        using var client = _factory.ClientFor(creator, Capability.MeetingCreator);

        var response = await client.PutAsJsonAsync($"/api/meetings/{meeting.Id}", new
        {
            title = "Đã bổ sung số liệu",
            plannedStartTime = "2026-10-01T09:30:00+07:00",
            plannedEndTime = "2026-10-01T10:30:00+07:00",
            mode = "Hybrid",
            location = "Phòng họp B",
            sensitivity = "Secret",
            description = "Nội dung mới",
            assignedApproverId = Guid.CreateVersion7(),
            status = "Draft",
        });
        using var body = await JsonOf(response);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal("MSG30", body.RootElement.GetProperty("code").GetString());
        var updated = body.RootElement.GetProperty("meeting");
        Assert.Equal("Đã bổ sung số liệu", updated.GetProperty("title").GetString());
        Assert.Equal("Rejected", updated.GetProperty("status").GetString());
        Assert.Equal(approverId, updated.GetProperty("assignedApproverId").GetGuid());
        Assert.Equal("Đã bổ sung số liệu", meeting.Title);
        Assert.Same(rejection, Assert.Single(meeting.StatusHistory));
        Assert.Equal("Cần bổ sung số liệu", rejection.Reason);
        Assert.Empty(_factory.Meetings.Notifications);
        Assert.Empty(_factory.Meetings.EmailMessages);
    }

    [Theory]
    [InlineData(MeetingStatus.WaitingApproval)]
    [InlineData(MeetingStatus.Approved)]
    [InlineData(MeetingStatus.InProgress)]
    [InlineData(MeetingStatus.Ended)]
    public async Task Put_returns_MSG33_if_the_meeting_is_not_editable(MeetingStatus status)
    {
        var creator = GivenCreator();
        using var client = _factory.ClientFor(creator, Capability.MeetingCreator);
        var meeting = new Meeting
        {
            Title = status.ToString(),
            Description = string.Empty,
            Status = status,
            Sensitivity = Sensitivity.Normal,
            Mode = MeetingMode.InPerson,
            CreatedById = creator.Id,
        };
        _factory.Meetings.Meetings.Add(meeting);

        var response = await client.PutAsJsonAsync($"/api/meetings/{meeting.Id}", ValidRequest(title: ""));
        using var body = await JsonOf(response);

        Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);
        Assert.Equal("MSG33", body.RootElement.GetProperty("code").GetString());
        Assert.Equal(status.ToString(), meeting.Title);
    }

    private Meeting PublishedMeetingWithInvitees(User creator, out User[] invitees)
    {
        var meeting = OwnedMeetingBy(creator);
        meeting.Status = MeetingStatus.Published;
        invitees = [GivenMember("invitee-a@trustcabinet.local"), GivenMember("invitee-b@trustcabinet.local")];
        foreach (var invitee in invitees)
        {
            meeting.Participants.Add(new MeetingParticipant
            {
                MeetingId = meeting.Id,
                UserId = invitee.Id,
                User = invitee,
                Role = MeetingRole.Member,
            });
        }

        _factory.Meetings.Meetings.Add(meeting);
        return meeting;
    }

    [Fact]
    public async Task Put_reschedules_a_published_meeting_and_notifies_every_invitee()
    {
        var creator = GivenCreator();
        var meeting = PublishedMeetingWithInvitees(creator, out var invitees);
        using var client = _factory.ClientFor(creator, Capability.MeetingCreator);
        var newStart = FutureStart.AddDays(1);

        var response = await client.PutAsJsonAsync($"/api/meetings/{meeting.Id}", ValidRequest() with
        {
            PlannedStartTime = Iso(newStart),
            PlannedEndTime = Iso(newStart.AddHours(2)),
            Location = "Phòng họp B",
            OnlineLink = "https://meet.example.vn/abc",
        });
        using var body = await JsonOf(response);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal("MSG138", body.RootElement.GetProperty("code").GetString());
        var updated = body.RootElement.GetProperty("meeting");
        Assert.Equal("Published", updated.GetProperty("status").GetString());
        Assert.Equal(newStart, updated.GetProperty("plannedStartTime").GetDateTimeOffset());
        Assert.Equal("https://meet.example.vn/abc", updated.GetProperty("onlineLink").GetString());
        var history = Assert.Single(meeting.StatusHistory);
        Assert.Equal(MeetingHistoryAction.Reschedule, history.Action);
        Assert.Equal(MeetingStatus.Published, history.FromStatus);
        Assert.Equal(MeetingStatus.Published, history.ToStatus);
        Assert.Equal(
            invitees.Select(invitee => invitee.Id).Order(),
            _factory.Meetings.Notifications.Select(notification => notification.UserId).Order());
        Assert.All(_factory.Meetings.Notifications, notification =>
        {
            Assert.Equal(NotificationType.MeetingRescheduled, notification.Type);
            Assert.Equal(history.Id, notification.OriginHistoryId);
        });
    }

    [Fact]
    public async Task Put_reschedule_does_not_notify_the_organizer_of_their_own_change()
    {
        var creator = GivenCreator();
        var meeting = PublishedMeetingWithInvitees(creator, out var invitees);
        meeting.Participants.Add(new MeetingParticipant
        {
            MeetingId = meeting.Id,
            UserId = creator.Id,
            User = creator,
            Role = MeetingRole.Chair,
        });
        using var client = _factory.ClientFor(creator, Capability.MeetingCreator);
        var newStart = FutureStart.AddDays(1);

        var response = await client.PutAsJsonAsync($"/api/meetings/{meeting.Id}", ValidRequest() with
        {
            PlannedStartTime = Iso(newStart),
            PlannedEndTime = Iso(newStart.AddHours(2)),
        });

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal(
            invitees.Select(invitee => invitee.Id).Order(),
            _factory.Meetings.Notifications.Select(notification => notification.UserId).Order());
    }

    [Fact]
    public async Task Put_on_a_published_meeting_rejects_fields_outside_the_schedule()
    {
        var creator = GivenCreator();
        var meeting = PublishedMeetingWithInvitees(creator, out _);
        using var client = _factory.ClientFor(creator, Capability.MeetingCreator);

        var response = await client.PutAsJsonAsync($"/api/meetings/{meeting.Id}", ValidRequest("Tên mới") with
        {
            Sensitivity = "Secret",
            PlannedStartTime = Iso(FutureStart.AddDays(1)),
            PlannedEndTime = Iso(FutureStart.AddDays(1).AddHours(1)),
        });
        using var body = await JsonOf(response);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Equal("MSG31", body.RootElement.GetProperty("code").GetString());
        var errors = body.RootElement.GetProperty("errors");
        Assert.True(errors.TryGetProperty("title", out _));
        Assert.True(errors.TryGetProperty("sensitivity", out _));
        Assert.Equal("Giao ban tuần", meeting.Title);
        Assert.Equal(FutureStart, meeting.PlannedStartTime);
        Assert.Empty(meeting.StatusHistory);
        Assert.Empty(_factory.Meetings.Notifications);
    }

    [Fact]
    public async Task Put_on_a_published_meeting_needs_a_start_in_the_future()
    {
        var creator = GivenCreator();
        var meeting = PublishedMeetingWithInvitees(creator, out _);
        using var client = _factory.ClientFor(creator, Capability.MeetingCreator);

        var response = await client.PutAsJsonAsync($"/api/meetings/{meeting.Id}", ValidRequest() with
        {
            PlannedStartTime = Iso(DateTimeOffset.UtcNow.AddMinutes(-30)),
            PlannedEndTime = Iso(DateTimeOffset.UtcNow.AddMinutes(30)),
        });
        using var body = await JsonOf(response);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.True(body.RootElement.GetProperty("errors").TryGetProperty("plannedStartTime", out _));
        Assert.Equal(FutureStart, meeting.PlannedStartTime);
    }

    [Fact]
    public async Task Put_on_a_published_meeting_without_changes_notifies_nobody()
    {
        var creator = GivenCreator();
        var meeting = PublishedMeetingWithInvitees(creator, out _);
        using var client = _factory.ClientFor(creator, Capability.MeetingCreator);

        var response = await client.PutAsJsonAsync($"/api/meetings/{meeting.Id}", ValidRequest());
        using var body = await JsonOf(response);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal("MSG30", body.RootElement.GetProperty("code").GetString());
        Assert.Empty(meeting.StatusHistory);
        Assert.Empty(_factory.Meetings.Notifications);
    }

    [Fact]
    public async Task Put_on_a_published_online_meeting_ignores_a_stored_location_the_form_cannot_show()
    {
        var creator = GivenCreator();
        var meeting = PublishedMeetingWithInvitees(creator, out _);
        meeting.Mode = MeetingMode.Online;
        meeting.Location = "Trực tuyến";
        using var client = _factory.ClientFor(creator, Capability.MeetingCreator);

        var response = await client.PutAsJsonAsync($"/api/meetings/{meeting.Id}", ValidRequest() with
        {
            Mode = "Online",
            Location = "",
        });
        using var body = await JsonOf(response);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal("MSG30", body.RootElement.GetProperty("code").GetString());
        Assert.Empty(meeting.StatusHistory);
        Assert.Empty(_factory.Meetings.Notifications);
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
        Assert.Equal("MSG33", body.RootElement.GetProperty("code").GetString());
        Assert.Equal("Giao ban tuần", meeting.Title);
    }

    [Theory]
    [InlineData(MeetingStatus.Draft)]
    [InlineData(MeetingStatus.Rejected)]
    public async Task Put_returns_400_for_invalid_json_when_meeting_is_editable(MeetingStatus status)
    {
        var creator = GivenCreator();
        using var client = _factory.ClientFor(creator, Capability.MeetingCreator);
        var meeting = OwnedMeetingBy(creator);
        meeting.Status = status;
        _factory.Meetings.Meetings.Add(meeting);

        using var request = new HttpRequestMessage(HttpMethod.Put, $"/api/meetings/{meeting.Id}")
        {
            Content = new StringContent("{", System.Text.Encoding.UTF8, "application/json"),
        };
        var response = await client.SendAsync(request);
        using var body = await JsonOf(response);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Equal("MSG31", body.RootElement.GetProperty("code").GetString());
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
        using var unsupportedBody = await JsonOf(unsupportedResponse);

        Assert.Equal(HttpStatusCode.UnsupportedMediaType, unsupportedResponse.StatusCode);
        Assert.Equal("MSG34", unsupportedBody.RootElement.GetProperty("code").GetString());
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
        Assert.Equal("MSG87", body.RootElement.GetProperty("code").GetString());
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
        var request = ValidRequest("");

        var response = await client.PutAsJsonAsync($"/api/meetings/{meeting.Id}", request);
        using var body = await JsonOf(response);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Equal("MSG31", body.RootElement.GetProperty("code").GetString());
        Assert.True(body.RootElement.GetProperty("errors").TryGetProperty("title", out _));
        Assert.Equal("Giao ban tuần", Assert.Single(_factory.Meetings.Meetings).Title);
        Assert.Equal(0, _factory.Meetings.SaveCount);
    }

    [Fact]
    public async Task Put_save_failure_returns_MSG36_and_500()
    {
        var creator = GivenCreator();
        using var client = _factory.ClientFor(creator, Capability.MeetingCreator);
        var meeting = OwnedMeetingBy(creator, Guid.CreateVersion7());
        _factory.Meetings.Meetings.Add(meeting);
        _factory.Meetings.NextSaveThrows = new Npgsql.NpgsqlException("database unavailable");

        var response = await client.PutAsJsonAsync(
            $"/api/meetings/{meeting.Id}", ValidRequest("Không được lưu"));
        using var body = await JsonOf(response);

        Assert.Equal(HttpStatusCode.InternalServerError, response.StatusCode);
        Assert.Equal("MSG36", body.RootElement.GetProperty("code").GetString());
    }

    [Fact]
    public async Task Get_returns_MSG37_when_the_repository_fails()
    {
        var creator = GivenCreator();
        using var client = _factory.ClientFor(creator);
        _factory.Meetings.NextReadThrows = new Npgsql.NpgsqlException("database unavailable");

        var response = await client.GetAsync($"/api/meetings/{Guid.CreateVersion7()}");
        using var body = await JsonOf(response);

        Assert.Equal(HttpStatusCode.InternalServerError, response.StatusCode);
        Assert.Equal("MSG37", body.RootElement.GetProperty("code").GetString());
    }

    [Fact]
    public async Task Get_preserves_its_error_code_when_active_account_lookup_fails()
    {
        using var client = _factory.ClientFor(GivenCreator());
        _factory.Accounts.NextReadThrows = new Npgsql.NpgsqlException("database unavailable");

        var response = await client.GetAsync($"/api/meetings/{Guid.CreateVersion7()}");
        using var body = await JsonOf(response);

        Assert.Equal(HttpStatusCode.InternalServerError, response.StatusCode);
        Assert.Equal("MSG37", body.RootElement.GetProperty("code").GetString());
    }

    [Fact]
    public async Task Anonymous_and_callers_without_MeetingCreator_are_rejected_by_the_gate()
    {
        using var anonymous = _factory.CreateClient();
        var creator = GivenCreator("wrong-capability@trustcabinet.local");
        using var wrongCapability = _factory.ClientFor(creator, Capability.AdminUsers);

        var anonymousResponse = await anonymous.GetAsync($"/api/meetings/{Guid.CreateVersion7()}");
        var forbiddenResponse = await wrongCapability.PostAsJsonAsync("/api/meetings", ValidCreateRequest());
        var forbiddenApprovers = await wrongCapability.GetAsync("/api/meetings/approvers");

        Assert.Equal(HttpStatusCode.Unauthorized, anonymousResponse.StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, forbiddenResponse.StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, forbiddenApprovers.StatusCode);
    }

    [Fact]
    public async Task Create_save_failure_returns_MSG35_and_500()
    {
        using var client = _factory.ClientFor(GivenCreator(), Capability.MeetingCreator);
        _factory.Meetings.NextSaveThrows = new Npgsql.NpgsqlException("database unavailable");

        var response = await client.PostAsJsonAsync("/api/meetings", ValidCreateRequest());
        using var body = await JsonOf(response);

        Assert.Equal(HttpStatusCode.InternalServerError, response.StatusCode);
        Assert.Equal("MSG35", body.RootElement.GetProperty("code").GetString());
        Assert.Empty(_factory.Meetings.Meetings);
    }
}
