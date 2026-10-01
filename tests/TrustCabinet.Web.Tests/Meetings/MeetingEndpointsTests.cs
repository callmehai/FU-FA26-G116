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
        internal FakeMeetingInvitationQueue Invitations { get; } = new();

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
                services.RemoveAll<IMeetingInvitationQueue>();
                services.AddSingleton<IMeetingInvitationQueue>(Invitations);
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
            "Kế hoạch tuần.");

    private static async Task<JsonDocument> JsonOf(HttpResponseMessage response)
        => JsonDocument.Parse(await response.Content.ReadAsStringAsync());

    [Theory]
    [InlineData(Sensitivity.Normal)]
    [InlineData(Sensitivity.Internal)]
    [InlineData(Sensitivity.Confidential)]
    [InlineData(Sensitivity.Secret)]
    public async Task Submit_to_the_organizer_returns_MSG52_without_writing_any_workflow_rows(Sensitivity sensitivity)
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
        Assert.Equal("MSG52", body.RootElement.GetProperty("code").GetString());
        Assert.Equal(MeetingStatus.Draft, meeting.Status);
        Assert.Null(meeting.AssignedApproverId);
        Assert.Empty(meeting.StatusHistory);
        Assert.Empty(_factory.Meetings.Notifications);
        Assert.Empty(_factory.Meetings.EmailMessages);
    }

    [Theory]
    [InlineData("Approve")]
    [InlineData("Reject")]
    public async Task Legacy_self_assignment_returns_MSG55_without_approving_or_rejecting(string action)
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
        Assert.Equal("MSG55", body.RootElement.GetProperty("code").GetString());
        Assert.Equal(MeetingStatus.WaitingApproval, meeting.Status);
        Assert.Empty(meeting.StatusHistory);
        Assert.Empty(_factory.Meetings.Notifications);
        Assert.Empty(_factory.Meetings.EmailMessages);
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public async Task Legacy_missing_or_self_assignment_returns_MSG49_when_the_organizer_withdraws(bool selfAssignment)
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
        Assert.Equal("MSG49", body.RootElement.GetProperty("code").GetString());
        Assert.Equal("WaitingApproval", body.RootElement.GetProperty("history").GetProperty("fromStatus").GetString());
        Assert.Equal("Draft", body.RootElement.GetProperty("history").GetProperty("resultingStatus").GetString());
        Assert.Equal(MeetingStatus.Draft, meeting.Status);
        Assert.Null(meeting.AssignedApproverId);
        Assert.Equal(MeetingHistoryAction.ReturnToDraft, Assert.Single(meeting.StatusHistory).Action);
        Assert.Empty(_factory.Meetings.Notifications);
        Assert.Empty(_factory.Meetings.EmailMessages);
        Assert.Empty(_factory.Invitations.MeetingIds);
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
        Assert.Equal("MSG48", body.RootElement.GetProperty("code").GetString());
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
        Assert.Equal(!organizerHasCapability ? "MSG55" : approverEligible ? "MSG64" : "MSG49",
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
        Assert.Empty(_factory.Invitations.MeetingIds);
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
        Assert.Empty(_factory.Invitations.MeetingIds);
    }

    [Theory]
    [InlineData("Approve", "MSG43")]
    [InlineData("Reject", "MSG44")]
    [InlineData("ReturnToDraft", "MSG49")]
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
    [InlineData("Approve", MeetingStatus.Rejected, "MSG63")]
    [InlineData("ReturnToDraft", MeetingStatus.Draft, "MSG64")]
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
    [InlineData("Approve", "MSG59")]
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
                    PlannedDurationMinutes = 30,
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
    public async Task Post_submit_returns_MSG51_when_not_ready_and_MSG52_for_an_ineligible_approver()
    {
        var creator = GivenCreator();
        var approverId = Guid.CreateVersion7();
        var meeting = OwnedMeetingBy(creator);
        _factory.Meetings.Meetings.Add(meeting);
        _factory.Meetings.UserCapabilities[creator.Id] = [Capability.MeetingCreator];
        using var client = _factory.ClientFor(creator);

        var notReady = await client.PostAsJsonAsync($"/api/meetings/{meeting.Id}/transitions", new
        {
            action = "SubmitForApproval",
            assignedApproverId = approverId,
        });
        using var notReadyBody = await JsonOf(notReady);
        meeting.AgendaItems.Add(new AgendaItem
        {
            MeetingId = meeting.Id,
            Position = 1,
            Title = "Báo cáo",
            PlannedDurationMinutes = 30,
            PresenterId = Guid.CreateVersion7(),
        });
        _factory.Meetings.IneligibleApproverIds.Add(approverId);
        var ineligible = await client.PostAsJsonAsync($"/api/meetings/{meeting.Id}/transitions", new
        {
            action = "SubmitForApproval",
            assignedApproverId = approverId,
        });
        using var ineligibleBody = await JsonOf(ineligible);

        Assert.Equal(HttpStatusCode.BadRequest, notReady.StatusCode);
        Assert.Equal("MSG51", notReadyBody.RootElement.GetProperty("code").GetString());
        Assert.Equal(HttpStatusCode.BadRequest, ineligible.StatusCode);
        Assert.Equal("MSG52", ineligibleBody.RootElement.GetProperty("code").GetString());
    }

    [Fact]
    public async Task Post_reject_returns_MSG54_when_reason_exceeds_1000_characters()
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
        Assert.Equal("MSG54", body.RootElement.GetProperty("code").GetString());
    }

    [Fact]
    public async Task Post_transition_returns_MSG55_when_the_owner_lacks_the_required_capability()
    {
        var creator = GivenCreator();
        var meeting = OwnedMeetingBy(creator);
        _factory.Meetings.Meetings.Add(meeting);
        using var client = _factory.ClientFor(creator);

        var response = await client.PostAsJsonAsync($"/api/meetings/{meeting.Id}/transitions", new
        {
            action = "Publish",
        });
        using var body = await JsonOf(response);

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
        Assert.Equal("MSG55", body.RootElement.GetProperty("code").GetString());
    }

    [Fact]
    public async Task Publish_producer_configuration_failure_returns_MSG47_and_rolls_back_the_workflow()
    {
        var creator = GivenCreator();
        var meeting = OwnedMeetingBy(creator);
        meeting.Status = MeetingStatus.Approved;
        _factory.Meetings.Meetings.Add(meeting);
        _factory.Meetings.UserCapabilities[creator.Id] = [Capability.MeetingCreator];
        _factory.Invitations.NextQueueThrows = new InvalidOperationException("Sender missing");
        using var client = _factory.ClientFor(creator, Capability.MeetingCreator);

        var response = await client.PostAsJsonAsync(
            $"/api/meetings/{meeting.Id}/transitions", new { action = "Publish" });
        using var body = await JsonOf(response);

        Assert.Equal(HttpStatusCode.ServiceUnavailable, response.StatusCode);
        Assert.Equal("MSG47", body.RootElement.GetProperty("code").GetString());
        Assert.Equal(MeetingStatus.Approved, Assert.Single(_factory.Meetings.Meetings).Status);
        Assert.Empty(_factory.Meetings.Meetings.Single().StatusHistory);
        Assert.Empty(_factory.Meetings.Notifications);
        Assert.Empty(_factory.Invitations.MeetingIds);
    }

    [Fact]
    public async Task Get_workflow_endpoints_return_MSG60_and_MSG62_for_transient_read_failures()
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
        Assert.Equal("MSG60", readinessBody.RootElement.GetProperty("code").GetString());
        Assert.Equal(HttpStatusCode.ServiceUnavailable, history.StatusCode);
        Assert.Equal("MSG62", historyBody.RootElement.GetProperty("code").GetString());
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
        Assert.Equal("MSG50", unknownBody.RootElement.GetProperty("code").GetString());
        Assert.Equal(HttpStatusCode.BadRequest, nullReasonOnReturn.StatusCode);
        Assert.Equal("MSG50", nullReasonBody.RootElement.GetProperty("code").GetString());
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
        _factory.Meetings.Meetings.Add(meeting);
        _factory.Meetings.UserCapabilities[creator.Id] = [Capability.MeetingCreator];
        using var client = _factory.ClientFor(creator, Capability.MeetingCreator);

        var response = await client.PostAsJsonAsync(
            $"/api/meetings/{meeting.Id}/transitions", new { action });
        using var body = await JsonOf(response);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Equal("MSG50", body.RootElement.GetProperty("code").GetString());
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
        Assert.Equal("MSG53", body.RootElement.GetProperty("code").GetString());
        Assert.Equal(MeetingStatus.WaitingApproval, meeting.Status);
        Assert.Empty(meeting.StatusHistory);
    }

    [Fact]
    public async Task Post_transition_rejects_submit_from_rejected_without_side_effects()
    {
        var creator = GivenCreator();
        var formerApproverId = Guid.CreateVersion7();
        var meeting = OwnedMeetingBy(creator, formerApproverId);
        meeting.Status = MeetingStatus.Rejected;
        meeting.StatusHistory.Add(new MeetingStatusHistory
        {
            MeetingId = meeting.Id,
            FromStatus = MeetingStatus.WaitingApproval,
            ToStatus = MeetingStatus.Rejected,
            Action = MeetingHistoryAction.Reject,
            ActorId = formerApproverId,
            AssignedApproverId = formerApproverId,
            Reason = "Cần bổ sung số liệu",
            OccurredAt = DateTimeOffset.UtcNow,
        });
        _factory.Meetings.Meetings.Add(meeting);
        _factory.Meetings.UserCapabilities[creator.Id] = [Capability.MeetingCreator];
        using var client = _factory.ClientFor(creator, Capability.MeetingCreator);

        var response = await client.PostAsJsonAsync($"/api/meetings/{meeting.Id}/transitions", new
        {
            action = "SubmitForApproval",
            assignedApproverId = Guid.CreateVersion7(),
        });
        using var body = await JsonOf(response);

        Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);
        Assert.Equal("MSG41", body.RootElement.GetProperty("code").GetString());
        Assert.Equal("Cuộc họp không còn ở trạng thái Draft.", body.RootElement.GetProperty("message").GetString());
        var readinessResponse = await client.GetAsync($"/api/meetings/{meeting.Id}/submission-readiness");
        using var readinessBody = await JsonOf(readinessResponse);
        Assert.Equal(HttpStatusCode.Conflict, readinessResponse.StatusCode);
        Assert.Equal("MSG41", readinessBody.RootElement.GetProperty("code").GetString());
        Assert.Equal(body.RootElement.GetProperty("message").GetString(),
            readinessBody.RootElement.GetProperty("message").GetString());
        Assert.Equal(MeetingStatus.Rejected, meeting.Status);
        Assert.Equal(formerApproverId, meeting.AssignedApproverId);
        Assert.Single(meeting.StatusHistory);
        Assert.Empty(_factory.Meetings.Notifications);
        Assert.Empty(_factory.Meetings.EmailMessages);
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
        Assert.Equal("MSG56", body.RootElement.GetProperty("code").GetString());
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
        Assert.Equal("MSG32", body.RootElement.GetProperty("code").GetString());
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

        var created = await client.PostAsJsonAsync("/api/meetings", ValidRequest());
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
            "/api/meetings", ValidRequest(title: "   ") with { Mode = "Wrong" });
        using var body = await JsonOf(response);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Equal("MSG31", body.RootElement.GetProperty("code").GetString());
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
    public async Task Post_does_not_create_a_current_assignment_from_a_client_supplied_approver()
    {
        var creator = GivenCreator();
        using var client = _factory.ClientFor(creator, Capability.MeetingCreator);

        var response = await client.PostAsJsonAsync("/api/meetings", new
        {
            title = "Giao ban tuần",
            startsAt = "2026-10-01T09:00:00+07:00",
            assignedApproverId = Guid.CreateVersion7(),
        });
        using var body = await JsonOf(response);

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        Assert.Equal(JsonValueKind.Null,
            body.RootElement.GetProperty("meeting").GetProperty("assignedApproverId").ValueKind);
        Assert.Null(Assert.Single(_factory.Meetings.Meetings).AssignedApproverId);
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
        Assert.Equal("MSG61", body.RootElement.GetProperty("code").GetString());
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
        Assert.Equal("MSG61", body.RootElement.GetProperty("code").GetString());
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
        Assert.Equal("MSG61", body.RootElement.GetProperty("code").GetString());
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
            startsAt = "2026-10-01T09:30:00+07:00",
            endsAt = "2026-10-01T10:30:00+07:00",
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
    [InlineData(MeetingStatus.Published)]
    [InlineData(MeetingStatus.InProgress)]
    [InlineData(MeetingStatus.Ended)]
    [InlineData(MeetingStatus.MinutesIssued)]
    [InlineData(MeetingStatus.Archived)]
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
            StartsAt = new DateTimeOffset(2026, 10, 1, 2, 0, 0, TimeSpan.Zero),
            EndsAt = new DateTimeOffset(2026, 10, 1, 3, 0, 0, TimeSpan.Zero),
        };
        _factory.Meetings.Meetings.Add(meeting);

        var response = await client.PutAsJsonAsync($"/api/meetings/{meeting.Id}", ValidRequest(title: ""));
        using var body = await JsonOf(response);

        Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);
        Assert.Equal("MSG33", body.RootElement.GetProperty("code").GetString());
        Assert.Equal(status.ToString(), meeting.Title);
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
        Assert.Equal("MSG58", body.RootElement.GetProperty("code").GetString());
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
        };

        var response = await client.PutAsJsonAsync($"/api/meetings/{meeting.Id}", request);
        using var body = await JsonOf(response);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Equal("MSG31", body.RootElement.GetProperty("code").GetString());
        Assert.True(body.RootElement.GetProperty("errors").TryGetProperty("endsAt", out _));
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
        var forbiddenResponse = await wrongCapability.PostAsJsonAsync("/api/meetings", ValidRequest());
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

        var response = await client.PostAsJsonAsync("/api/meetings", ValidRequest());
        using var body = await JsonOf(response);

        Assert.Equal(HttpStatusCode.InternalServerError, response.StatusCode);
        Assert.Equal("MSG35", body.RootElement.GetProperty("code").GetString());
        Assert.Empty(_factory.Meetings.Meetings);
    }
}
