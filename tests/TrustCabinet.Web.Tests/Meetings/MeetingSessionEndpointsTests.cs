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
using TrustCabinet.Web.Tests.Users;

namespace TrustCabinet.Web.Tests.Meetings;

/// <summary>UC-53 and UC-26 to UC-30 through the real HTTP pipeline, over in-memory repositories.</summary>
public sealed class MeetingSessionEndpointsTests : IDisposable
{
    private static readonly DateTimeOffset NextWeek = new(DateTimeOffset.UtcNow.UtcDateTime.Date.AddDays(7).AddHours(2), TimeSpan.Zero);
    private readonly Factory _factory = new();
    private readonly User _organizer;
    private readonly User _secretary;
    private readonly User _member;
    private readonly User _chair;

    public MeetingSessionEndpointsTests()
    {
        _organizer = GivenUser("organizer", BuiltInRoles.Secretary);
        _secretary = GivenUser("secretary", BuiltInRoles.Secretary);
        _member = GivenUser("member", BuiltInRoles.Member);
        _chair = GivenUser("chair", BuiltInRoles.Member);
    }

    public void Dispose() => _factory.Dispose();

    private sealed class Factory : TrustCabinetApiFactory
    {
        internal FakeMeetingRepository Meetings { get; } = new();
        internal FakeMeetingSessionRepository Sessions { get; }
        internal FakeUserAccountRepository Accounts { get; } = new();

        public Factory() => Sessions = new FakeMeetingSessionRepository(Meetings);

        protected override void ConfigureWebHost(IWebHostBuilder builder)
        {
            base.ConfigureWebHost(builder);
            builder.ConfigureServices(services =>
            {
                services.RemoveAll<IUserAccountRepository>();
                services.AddSingleton<IUserAccountRepository>(Accounts);
                services.RemoveAll<IMeetingRepository>();
                services.AddSingleton<IMeetingRepository>(Meetings);
                services.RemoveAll<IMeetingSessionRepository>();
                services.AddSingleton<IMeetingSessionRepository>(Sessions);
            });
        }

        internal HttpClient ClientFor(User user)
        {
            var client = CreateClient();
            client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue(
                "Bearer", Services.GetRequiredService<ITokenIssuer>().CreateAccessToken(user, []));
            return client;
        }
    }

    private User GivenUser(string name, Guid roleId)
    {
        var user = new User
        {
            FullName = $"Người dùng {name}",
            Email = $"{name}@trustcabinet.local",
            PasswordHash = "test-hash",
            RoleId = roleId,
        };
        _factory.Accounts.WithUser(user);
        return user;
    }

    private Meeting GivenMeeting(MeetingStatus status)
    {
        var meeting = new Meeting
        {
            Title = "Giao ban tuần",
            Status = status,
            Sensitivity = Sensitivity.Normal,
            Mode = MeetingMode.InPerson,
            CreatedById = _organizer.Id,
        };
        meeting.Participants.Add(Participant(meeting, _secretary, MeetingRole.Secretary));
        meeting.Participants.Add(Participant(meeting, _member, MeetingRole.Member));
        _factory.Meetings.Meetings.Add(meeting);
        return meeting;
    }

    private static MeetingParticipant Participant(Meeting meeting, User user, MeetingRole role) => new()
    {
        MeetingId = meeting.Id,
        UserId = user.Id,
        User = user,
        Role = role,
    };

    private static MeetingSession GivenSession(
        Meeting meeting,
        MeetingSessionStatus status = MeetingSessionStatus.Planned,
        DateTimeOffset? startsAt = null,
        string title = "Phiên sáng")
    {
        var start = startsAt ?? NextWeek;
        var session = new MeetingSession
        {
            MeetingId = meeting.Id,
            Title = title,
            PlannedStartsAt = start,
            PlannedEndsAt = start.AddHours(2),
            Status = status,
        };
        meeting.Sessions.Add(session);
        return session;
    }

    private static object Body(string? title = "Phiên chiều", DateTimeOffset? startsAt = null, DateTimeOffset? endsAt = null)
    {
        var start = startsAt ?? NextWeek.AddHours(5);
        return new
        {
            title,
            plannedStartsAt = start.ToString("O"),
            plannedEndsAt = (endsAt ?? start.AddHours(1)).ToString("O"),
        };
    }

    private Meeting StoredMeeting(Guid id) => _factory.Meetings.Meetings.Single(meeting => meeting.Id == id);

    private static async Task<JsonDocument> JsonOf(HttpResponseMessage response)
        => JsonDocument.Parse(await response.Content.ReadAsStringAsync());

    private static string Code(JsonDocument body) => body.RootElement.GetProperty("code").GetString()!;

    // ---- UC-53 View Meeting Sessions -------------------------------------------------------

    [Fact]
    public async Task List_returns_session_level_data_ordered_by_planned_start()
    {
        var meeting = GivenMeeting(MeetingStatus.Published);
        var later = GivenSession(meeting, startsAt: NextWeek.AddDays(1), title: "Phiên ngày hai");
        var earlier = GivenSession(meeting, MeetingSessionStatus.Cancelled, NextWeek, "Phiên ngày một");
        using var client = _factory.ClientFor(_member);

        var response = await client.GetAsync($"/api/meetings/{meeting.Id}/sessions");
        using var body = await JsonOf(response);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var items = body.RootElement.EnumerateArray().ToArray();
        Assert.Equal(new[] { earlier.Id, later.Id }, items.Select(item => item.GetProperty("id").GetGuid()));
        Assert.Equal("Cancelled", items[0].GetProperty("status").GetString());
        Assert.Equal(
            new[] { "id", "meetingId", "title", "plannedStartsAt", "plannedEndsAt", "status", "actualStartedAt", "actualEndedAt" },
            items[0].EnumerateObject().Select(property => property.Name));
    }

    [Fact]
    public async Task List_returns_an_empty_array_when_the_meeting_has_no_sessions()
    {
        var meeting = GivenMeeting(MeetingStatus.Draft);
        using var client = _factory.ClientFor(_organizer);

        var response = await client.GetAsync($"/api/meetings/{meeting.Id}/sessions");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal("[]", await response.Content.ReadAsStringAsync());
    }

    [Fact]
    public async Task List_returns_404_without_body_for_a_meeting_the_caller_cannot_read()
    {
        // A Member sees a meeting only once it is published (BR-29).
        var meeting = GivenMeeting(MeetingStatus.Approved);
        GivenSession(meeting);
        var outsider = GivenUser("outsider", BuiltInRoles.Member);

        using var memberClient = _factory.ClientFor(_member);
        using var outsiderClient = _factory.ClientFor(outsider);
        var member = await memberClient.GetAsync($"/api/meetings/{meeting.Id}/sessions");
        var unknown = await outsiderClient.GetAsync($"/api/meetings/{Guid.CreateVersion7()}/sessions");

        Assert.Equal(HttpStatusCode.NotFound, member.StatusCode);
        Assert.Empty(await member.Content.ReadAsByteArrayAsync());
        Assert.Equal(HttpStatusCode.NotFound, unknown.StatusCode);
    }

    [Fact]
    public async Task List_returns_MSG127_when_sessions_cannot_be_loaded()
    {
        var meeting = GivenMeeting(MeetingStatus.Published);
        _factory.Sessions.NextListThrows = new Npgsql.NpgsqlException("database unavailable");
        using var client = _factory.ClientFor(_member);

        var response = await client.GetAsync($"/api/meetings/{meeting.Id}/sessions");
        using var body = await JsonOf(response);

        Assert.Equal(HttpStatusCode.InternalServerError, response.StatusCode);
        Assert.Equal("MSG127", Code(body));
    }

    [Fact]
    public async Task List_returns_401_without_a_token()
    {
        var meeting = GivenMeeting(MeetingStatus.Published);
        using var client = _factory.CreateClient();

        var response = await client.GetAsync($"/api/meetings/{meeting.Id}/sessions");

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    // ---- UC-26 Create Meeting Session ------------------------------------------------------

    [Theory]
    [InlineData(MeetingStatus.Draft)]
    [InlineData(MeetingStatus.Rejected)]
    [InlineData(MeetingStatus.Approved)]
    public async Task Create_adds_a_planned_session_without_notifying_before_publication(MeetingStatus status)
    {
        var meeting = GivenMeeting(status);
        using var client = _factory.ClientFor(_secretary);

        var response = await client.PostAsJsonAsync(
            $"/api/meetings/{meeting.Id}/sessions",
            new { title = "  Phiên chiều  ", plannedStartsAt = "2026-01-05T14:00+07:00", plannedEndsAt = "2026-01-05T16:30:00+07:00" });
        using var body = await JsonOf(response);

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        Assert.Equal("MSG48", Code(body));
        var session = Assert.Single(StoredMeeting(meeting.Id).Sessions);
        Assert.Equal("Phiên chiều", session.Title);
        Assert.Equal(MeetingSessionStatus.Planned, session.Status);
        Assert.Equal(new DateTimeOffset(2026, 1, 5, 7, 0, 0, TimeSpan.Zero), session.PlannedStartsAt);
        Assert.Equal(TimeSpan.Zero, session.PlannedStartsAt.Offset);
        Assert.Equal(session.Id, body.RootElement.GetProperty("session").GetProperty("id").GetGuid());
        Assert.Equal("Planned", body.RootElement.GetProperty("session").GetProperty("status").GetString());
        Assert.Equal(status, StoredMeeting(meeting.Id).Status);
        Assert.Empty(_factory.Meetings.Notifications);
    }

    [Theory]
    [InlineData(MeetingStatus.Published)]
    [InlineData(MeetingStatus.InProgress)]
    public async Task Create_after_publication_notifies_other_participants_and_keeps_the_meeting_status(MeetingStatus status)
    {
        var meeting = GivenMeeting(status);
        using var client = _factory.ClientFor(_secretary);

        var response = await client.PostAsJsonAsync($"/api/meetings/{meeting.Id}/sessions", Body());

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        Assert.Equal(status, StoredMeeting(meeting.Id).Status);
        var notification = Assert.Single(_factory.Meetings.Notifications);
        Assert.Equal(_member.Id, notification.UserId);
        Assert.Equal(NotificationType.MeetingSessionChanged, notification.Type);
        Assert.Equal(meeting.Id, notification.TargetId);
        Assert.Equal("Meeting", notification.TargetType);
        Assert.Contains("Phiên chiều", notification.Body, StringComparison.Ordinal);
    }

    [Fact]
    public async Task Create_returns_MSG53_with_field_errors_for_invalid_information()
    {
        var meeting = GivenMeeting(MeetingStatus.Draft);
        using var client = _factory.ClientFor(_secretary);

        var missing = await client.PostAsJsonAsync(
            $"/api/meetings/{meeting.Id}/sessions",
            new { title = " ", plannedStartsAt = "2026-01-05T14:00:00", plannedEndsAt = (string?)null });
        using var missingBody = await JsonOf(missing);
        var reversed = await client.PostAsJsonAsync(
            $"/api/meetings/{meeting.Id}/sessions", Body(new string('a', 301), NextWeek, NextWeek));
        using var reversedBody = await JsonOf(reversed);

        Assert.Equal(HttpStatusCode.BadRequest, missing.StatusCode);
        Assert.Equal("MSG53", Code(missingBody));
        var errors = missingBody.RootElement.GetProperty("errors");
        Assert.Equal("Tên phiên họp không được để trống.", errors.GetProperty("title").GetString());
        // A timestamp without Z or an offset is ambiguous, so it is rejected rather than guessed.
        Assert.Equal("Thời gian bắt đầu dự kiến không hợp lệ.", errors.GetProperty("plannedStartsAt").GetString());
        Assert.Equal("Thời gian kết thúc dự kiến không hợp lệ.", errors.GetProperty("plannedEndsAt").GetString());
        Assert.Equal(HttpStatusCode.BadRequest, reversed.StatusCode);
        var reversedErrors = reversedBody.RootElement.GetProperty("errors");
        Assert.Equal("Tên phiên họp không được vượt quá 300 ký tự.", reversedErrors.GetProperty("title").GetString());
        Assert.Equal(
            "Thời gian kết thúc dự kiến phải sau thời gian bắt đầu dự kiến.",
            reversedErrors.GetProperty("plannedEndsAt").GetString());
        Assert.Empty(StoredMeeting(meeting.Id).Sessions);
    }

    [Fact]
    public async Task Create_returns_MSG53_for_a_past_start_only_after_publication()
    {
        var draft = GivenMeeting(MeetingStatus.Draft);
        var published = GivenMeeting(MeetingStatus.Published);
        var yesterday = DateTimeOffset.UtcNow.AddDays(-1);
        using var client = _factory.ClientFor(_secretary);

        var draftResponse = await client.PostAsJsonAsync($"/api/meetings/{draft.Id}/sessions", Body(startsAt: yesterday));
        var publishedResponse = await client.PostAsJsonAsync(
            $"/api/meetings/{published.Id}/sessions", Body(startsAt: yesterday));
        using var body = await JsonOf(publishedResponse);

        Assert.Equal(HttpStatusCode.Created, draftResponse.StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest, publishedResponse.StatusCode);
        Assert.Equal("MSG53", Code(body));
        Assert.Equal(
            "Thời gian bắt đầu dự kiến phải sau thời điểm hiện tại.",
            body.RootElement.GetProperty("errors").GetProperty("plannedStartsAt").GetString());
        Assert.Empty(StoredMeeting(published.Id).Sessions);
    }

    [Fact]
    public async Task Create_returns_MSG53_without_errors_for_a_body_that_is_not_json()
    {
        var meeting = GivenMeeting(MeetingStatus.Draft);
        using var client = _factory.ClientFor(_secretary);

        var malformed = await client.PostAsync(
            $"/api/meetings/{meeting.Id}/sessions", new StringContent("{", Encoding.UTF8, "application/json"));
        using var malformedBody = await JsonOf(malformed);
        var text = await client.PostAsync(
            $"/api/meetings/{meeting.Id}/sessions", new StringContent("title=x", Encoding.UTF8, "text/plain"));
        using var textBody = await JsonOf(text);

        Assert.Equal(HttpStatusCode.BadRequest, malformed.StatusCode);
        Assert.Equal("MSG53", Code(malformedBody));
        Assert.False(malformedBody.RootElement.TryGetProperty("errors", out var errors)
            && errors.ValueKind != JsonValueKind.Null);
        Assert.Equal(HttpStatusCode.BadRequest, text.StatusCode);
        Assert.Equal("MSG53", Code(textBody));
    }

    [Theory]
    [InlineData(MeetingStatus.WaitingApproval)]
    [InlineData(MeetingStatus.Ended)]
    public async Task Create_returns_MSG54_409_when_the_meeting_is_closed_for_planning(MeetingStatus status)
    {
        var meeting = GivenMeeting(status);
        using var client = _factory.ClientFor(_secretary);

        var response = await client.PostAsJsonAsync($"/api/meetings/{meeting.Id}/sessions", Body());
        using var body = await JsonOf(response);

        Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);
        Assert.Equal("MSG54", Code(body));
        Assert.Empty(StoredMeeting(meeting.Id).Sessions);
    }

    [Fact]
    public async Task Create_returns_MSG54_403_to_a_reader_who_is_not_the_assigned_secretary()
    {
        var meeting = GivenMeeting(MeetingStatus.Published);
        using var organizerClient = _factory.ClientFor(_organizer);
        using var memberClient = _factory.ClientFor(_member);

        var organizer = await organizerClient.PostAsJsonAsync($"/api/meetings/{meeting.Id}/sessions", Body());
        using var organizerBody = await JsonOf(organizer);
        var member = await memberClient.PostAsJsonAsync($"/api/meetings/{meeting.Id}/sessions", Body());

        Assert.Equal(HttpStatusCode.Forbidden, organizer.StatusCode);
        Assert.Equal("MSG54", Code(organizerBody));
        Assert.Equal(HttpStatusCode.Forbidden, member.StatusCode);
        Assert.Empty(StoredMeeting(meeting.Id).Sessions);
    }

    [Fact]
    public async Task Create_returns_404_when_the_caller_cannot_read_the_meeting()
    {
        var meeting = GivenMeeting(MeetingStatus.Draft);
        using var client = _factory.ClientFor(_member);

        var response = await client.PostAsJsonAsync($"/api/meetings/{meeting.Id}/sessions", Body());

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
        Assert.Empty(await response.Content.ReadAsByteArrayAsync());
    }

    [Fact]
    public async Task Create_rejects_a_secretary_who_delegated_the_invitation()
    {
        var meeting = GivenMeeting(MeetingStatus.Published);
        meeting.Participants.Single(participant => participant.UserId == _secretary.Id).Response = InvitationResponse.Delegated;
        using var client = _factory.ClientFor(_secretary);

        var response = await client.PostAsJsonAsync($"/api/meetings/{meeting.Id}/sessions", Body());

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    public async Task Create_returns_MSG138_and_keeps_nothing_when_saving_fails()
    {
        var meeting = GivenMeeting(MeetingStatus.Published);
        _factory.Meetings.NextSaveThrows = new Microsoft.EntityFrameworkCore.DbUpdateException("write failed");
        using var client = _factory.ClientFor(_secretary);

        var response = await client.PostAsJsonAsync($"/api/meetings/{meeting.Id}/sessions", Body());
        using var body = await JsonOf(response);

        Assert.Equal(HttpStatusCode.InternalServerError, response.StatusCode);
        Assert.Equal("MSG138", Code(body));
        Assert.Empty(StoredMeeting(meeting.Id).Sessions);
        Assert.Empty(_factory.Meetings.Notifications);
    }

    [Fact]
    public async Task Planning_returns_MSG131_when_the_active_account_check_fails()
    {
        var meeting = GivenMeeting(MeetingStatus.Draft);
        _factory.Accounts.NextReadThrows = new Npgsql.NpgsqlException("database unavailable");
        using var client = _factory.ClientFor(_secretary);

        var response = await client.PostAsJsonAsync($"/api/meetings/{meeting.Id}/sessions", Body());
        using var body = await JsonOf(response);

        Assert.Equal(HttpStatusCode.ServiceUnavailable, response.StatusCode);
        Assert.Equal("MSG131", Code(body));
    }

    // ---- UC-27 Update Meeting Session ------------------------------------------------------

    [Fact]
    public async Task Update_reschedules_a_planned_session_and_notifies_after_publication()
    {
        var meeting = GivenMeeting(MeetingStatus.InProgress);
        var session = GivenSession(meeting);
        var other = GivenSession(meeting, startsAt: NextWeek.AddDays(2), title: "Phiên khác");
        using var client = _factory.ClientFor(_secretary);

        var response = await client.PutAsJsonAsync(
            $"/api/meetings/{meeting.Id}/sessions/{session.Id}", Body("Phiên dời lịch", NextWeek.AddDays(1)));
        using var body = await JsonOf(response);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal("MSG49", Code(body));
        var stored = StoredMeeting(meeting.Id).Sessions.Single(item => item.Id == session.Id);
        Assert.Equal("Phiên dời lịch", stored.Title);
        Assert.Equal(NextWeek.AddDays(1), stored.PlannedStartsAt);
        Assert.Equal(MeetingSessionStatus.Planned, stored.Status);
        Assert.Equal("Phiên khác", StoredMeeting(meeting.Id).Sessions.Single(item => item.Id == other.Id).Title);
        Assert.Equal(MeetingStatus.InProgress, StoredMeeting(meeting.Id).Status);
        Assert.Equal(_member.Id, Assert.Single(_factory.Meetings.Notifications).UserId);
    }

    [Fact]
    public async Task Update_without_changes_saves_and_notifies_nothing()
    {
        var meeting = GivenMeeting(MeetingStatus.Published);
        var session = GivenSession(meeting);
        using var client = _factory.ClientFor(_secretary);

        var response = await client.PutAsJsonAsync(
            $"/api/meetings/{meeting.Id}/sessions/{session.Id}",
            Body(session.Title, session.PlannedStartsAt, session.PlannedEndsAt));

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal(0, _factory.Meetings.SaveCount);
        Assert.Empty(_factory.Meetings.Notifications);
    }

    [Fact]
    public async Task Update_may_rename_a_published_session_whose_start_has_passed_but_not_reschedule_it_into_the_past()
    {
        var meeting = GivenMeeting(MeetingStatus.Published);
        var pastStart = new DateTimeOffset(DateTimeOffset.UtcNow.UtcDateTime.Date.AddHours(-1), TimeSpan.Zero);
        var session = GivenSession(meeting, startsAt: pastStart);
        using var client = _factory.ClientFor(_secretary);

        var rename = await client.PutAsJsonAsync(
            $"/api/meetings/{meeting.Id}/sessions/{session.Id}", Body("Đổi tên", pastStart, pastStart.AddHours(2)));
        var moved = await client.PutAsJsonAsync(
            $"/api/meetings/{meeting.Id}/sessions/{session.Id}", Body("Đổi tên", pastStart.AddMinutes(-30)));
        using var movedBody = await JsonOf(moved);

        Assert.Equal(HttpStatusCode.OK, rename.StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest, moved.StatusCode);
        Assert.Equal("MSG53", Code(movedBody));
    }

    [Theory]
    [InlineData(MeetingSessionStatus.InProgress)]
    [InlineData(MeetingSessionStatus.Completed)]
    [InlineData(MeetingSessionStatus.Cancelled)]
    public async Task Update_returns_MSG55_409_for_a_session_that_is_no_longer_planned(MeetingSessionStatus status)
    {
        var meeting = GivenMeeting(MeetingStatus.InProgress);
        var session = GivenSession(meeting, status);
        using var client = _factory.ClientFor(_secretary);

        var response = await client.PutAsJsonAsync($"/api/meetings/{meeting.Id}/sessions/{session.Id}", Body());
        using var body = await JsonOf(response);

        Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);
        Assert.Equal("MSG55", Code(body));
        Assert.Equal("Phiên sáng", session.Title);
    }

    [Fact]
    public async Task Update_returns_MSG55_for_a_closed_meeting_or_a_caller_who_is_not_the_secretary()
    {
        var waiting = GivenMeeting(MeetingStatus.WaitingApproval);
        var waitingSession = GivenSession(waiting);
        var published = GivenMeeting(MeetingStatus.Published);
        var publishedSession = GivenSession(published);
        using var secretaryClient = _factory.ClientFor(_secretary);
        using var memberClient = _factory.ClientFor(_member);

        var closed = await secretaryClient.PutAsJsonAsync(
            $"/api/meetings/{waiting.Id}/sessions/{waitingSession.Id}", Body());
        using var closedBody = await JsonOf(closed);
        var forbidden = await memberClient.PutAsJsonAsync(
            $"/api/meetings/{published.Id}/sessions/{publishedSession.Id}", Body());
        using var forbiddenBody = await JsonOf(forbidden);

        Assert.Equal(HttpStatusCode.Conflict, closed.StatusCode);
        Assert.Equal("MSG55", Code(closedBody));
        Assert.Equal(HttpStatusCode.Forbidden, forbidden.StatusCode);
        Assert.Equal("MSG55", Code(forbiddenBody));
    }

    [Fact]
    public async Task Update_returns_404_for_a_session_of_another_meeting()
    {
        var meeting = GivenMeeting(MeetingStatus.Draft);
        var otherMeeting = GivenMeeting(MeetingStatus.Draft);
        var foreign = GivenSession(otherMeeting);
        using var client = _factory.ClientFor(_secretary);

        var response = await client.PutAsJsonAsync($"/api/meetings/{meeting.Id}/sessions/{foreign.Id}", Body());

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
        Assert.Equal("Phiên sáng", foreign.Title);
    }

    [Fact]
    public async Task Update_returns_MSG139_when_saving_fails()
    {
        var meeting = GivenMeeting(MeetingStatus.Draft);
        var session = GivenSession(meeting);
        _factory.Meetings.NextSaveThrows = new Microsoft.EntityFrameworkCore.DbUpdateException("write failed");
        using var client = _factory.ClientFor(_secretary);

        var response = await client.PutAsJsonAsync($"/api/meetings/{meeting.Id}/sessions/{session.Id}", Body());
        using var body = await JsonOf(response);

        Assert.Equal(HttpStatusCode.InternalServerError, response.StatusCode);
        Assert.Equal("MSG139", Code(body));
        Assert.Equal("Phiên sáng", StoredMeeting(meeting.Id).Sessions.Single().Title);
    }

    // ---- UC-28 Cancel Meeting Session ------------------------------------------------------

    [Fact]
    public async Task Cancel_allows_the_last_planned_session_and_never_ends_the_meeting()
    {
        var meeting = GivenMeeting(MeetingStatus.InProgress);
        var session = GivenSession(meeting);
        GivenSession(meeting, MeetingSessionStatus.Completed, NextWeek.AddDays(-14), "Phiên đã họp");
        using var client = _factory.ClientFor(_secretary);

        var response = await client.PostAsync($"/api/meetings/{meeting.Id}/sessions/{session.Id}/cancel", null);
        using var body = await JsonOf(response);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal("MSG50", Code(body));
        Assert.Equal("Cancelled", body.RootElement.GetProperty("session").GetProperty("status").GetString());
        var stored = StoredMeeting(meeting.Id);
        Assert.Equal(MeetingSessionStatus.Cancelled, stored.Sessions.Single(item => item.Id == session.Id).Status);
        Assert.Equal(2, stored.Sessions.Count);
        Assert.Equal(MeetingStatus.InProgress, stored.Status);
        var notification = Assert.Single(_factory.Meetings.Notifications);
        Assert.Contains("đã bị hủy", notification.Body, StringComparison.Ordinal);
    }

    [Fact]
    public async Task Cancel_before_publication_does_not_notify()
    {
        var meeting = GivenMeeting(MeetingStatus.Approved);
        var session = GivenSession(meeting);
        using var client = _factory.ClientFor(_secretary);

        var response = await client.PostAsync($"/api/meetings/{meeting.Id}/sessions/{session.Id}/cancel", null);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Empty(_factory.Meetings.Notifications);
    }

    [Fact]
    public async Task Cancel_returns_MSG56_for_a_session_that_is_not_planned_or_a_caller_who_is_not_the_secretary()
    {
        var meeting = GivenMeeting(MeetingStatus.Published);
        var cancelled = GivenSession(meeting, MeetingSessionStatus.Cancelled);
        var planned = GivenSession(meeting);
        using var secretaryClient = _factory.ClientFor(_secretary);
        using var memberClient = _factory.ClientFor(_member);

        var again = await secretaryClient.PostAsync($"/api/meetings/{meeting.Id}/sessions/{cancelled.Id}/cancel", null);
        using var againBody = await JsonOf(again);
        var forbidden = await memberClient.PostAsync($"/api/meetings/{meeting.Id}/sessions/{planned.Id}/cancel", null);
        using var forbiddenBody = await JsonOf(forbidden);

        Assert.Equal(HttpStatusCode.Conflict, again.StatusCode);
        Assert.Equal("MSG56", Code(againBody));
        Assert.Equal(HttpStatusCode.Forbidden, forbidden.StatusCode);
        Assert.Equal("MSG56", Code(forbiddenBody));
        Assert.Equal(MeetingSessionStatus.Planned, planned.Status);
    }

    [Fact]
    public async Task Cancel_returns_MSG56_409_while_the_meeting_waits_for_approval()
    {
        var meeting = GivenMeeting(MeetingStatus.WaitingApproval);
        var session = GivenSession(meeting);
        using var client = _factory.ClientFor(_secretary);

        var response = await client.PostAsync($"/api/meetings/{meeting.Id}/sessions/{session.Id}/cancel", null);
        using var body = await JsonOf(response);

        Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);
        Assert.Equal("MSG56", Code(body));
        Assert.Equal(MeetingSessionStatus.Planned, session.Status);
    }

    [Fact]
    public async Task Cancel_returns_MSG140_and_keeps_the_session_planned_when_saving_fails()
    {
        var meeting = GivenMeeting(MeetingStatus.Published);
        var session = GivenSession(meeting);
        _factory.Meetings.NextSaveThrows = new Microsoft.EntityFrameworkCore.DbUpdateException("write failed");
        using var client = _factory.ClientFor(_secretary);

        var response = await client.PostAsync($"/api/meetings/{meeting.Id}/sessions/{session.Id}/cancel", null);
        using var body = await JsonOf(response);

        Assert.Equal(HttpStatusCode.InternalServerError, response.StatusCode);
        Assert.Equal("MSG140", Code(body));
        Assert.Equal(MeetingSessionStatus.Planned, StoredMeeting(meeting.Id).Sessions.Single().Status);
        Assert.Empty(_factory.Meetings.Notifications);
    }

    // ---- UC-29 Start Meeting Session -------------------------------------------------------

    private Meeting GivenRunningMeeting(MeetingStatus status = MeetingStatus.InProgress)
    {
        var meeting = GivenMeeting(status);
        meeting.Participants.Add(Participant(meeting, _chair, MeetingRole.Chair));
        return meeting;
    }

    private async Task<HttpResponseMessage> PostAsync(User user, Meeting meeting, MeetingSession session, string action)
    {
        using var client = _factory.ClientFor(user);
        return await client.PostAsync($"/api/meetings/{meeting.Id}/sessions/{session.Id}/{action}", null);
    }

    [Fact]
    public async Task Start_moves_a_planned_session_to_InProgress_and_leaves_the_meeting_InProgress()
    {
        var meeting = GivenRunningMeeting();
        GivenSession(meeting);
        var before = MeetingSessionRules.ToStoredPrecision(DateTimeOffset.UtcNow);

        var response = await PostAsync(_chair, meeting, meeting.Sessions.Single(), "start");
        using var body = await JsonOf(response);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal("MSG51", Code(body));
        var json = body.RootElement.GetProperty("session");
        Assert.Equal("InProgress", json.GetProperty("status").GetString());
        Assert.Equal(JsonValueKind.Null, json.GetProperty("actualEndedAt").ValueKind);
        var stored = StoredMeeting(meeting.Id);
        var started = stored.Sessions.Single();
        Assert.Equal(MeetingSessionStatus.InProgress, started.Status);
        Assert.InRange(started.ActualStartedAt!.Value, before, DateTimeOffset.UtcNow);
        Assert.Equal(started.ActualStartedAt, json.GetProperty("actualStartedAt").GetDateTimeOffset());
        Assert.Equal(MeetingStatus.InProgress, stored.Status);
        Assert.Empty(stored.StatusHistory);
        Assert.Empty(_factory.Meetings.Notifications);
    }

    [Theory]
    [InlineData(MeetingStatus.Published)]
    [InlineData(MeetingStatus.Ended)]
    public async Task Start_returns_MSG58_409_unless_the_meeting_is_InProgress(MeetingStatus status)
    {
        // A published meeting is started through UC-43 first.
        var meeting = GivenRunningMeeting(status);
        var session = GivenSession(meeting);

        var response = await PostAsync(_chair, meeting, session, "start");
        using var body = await JsonOf(response);

        Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);
        Assert.Equal("MSG58", Code(body));
        Assert.Equal(MeetingSessionStatus.Planned, StoredMeeting(meeting.Id).Sessions.Single().Status);
    }

    [Fact]
    public async Task Start_returns_MSG57_while_another_session_is_InProgress()
    {
        var meeting = GivenRunningMeeting();
        GivenSession(meeting, MeetingSessionStatus.InProgress, NextWeek.AddHours(-3), "Phiên đang họp");
        var next = GivenSession(meeting);

        var response = await PostAsync(_chair, meeting, next, "start");
        using var body = await JsonOf(response);

        Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);
        Assert.Equal("MSG57", Code(body));
        Assert.Equal(
            MeetingSessionStatus.Planned,
            StoredMeeting(meeting.Id).Sessions.Single(item => item.Id == next.Id).Status);
    }

    [Theory]
    [InlineData(MeetingSessionStatus.InProgress)]
    [InlineData(MeetingSessionStatus.Completed)]
    [InlineData(MeetingSessionStatus.Cancelled)]
    public async Task Start_returns_MSG58_409_for_a_session_that_is_not_planned(MeetingSessionStatus status)
    {
        var meeting = GivenRunningMeeting();
        var session = GivenSession(meeting, status);

        var response = await PostAsync(_chair, meeting, session, "start");
        using var body = await JsonOf(response);

        Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);
        Assert.Equal("MSG58", Code(body));
        Assert.Null(StoredMeeting(meeting.Id).Sessions.Single().ActualStartedAt);
    }

    [Fact]
    public async Task Start_returns_MSG58_409_when_no_active_secretary_remains()
    {
        // BR-89: starting a session needs an active Secretary as well as the Chair.
        var meeting = GivenRunningMeeting();
        var session = GivenSession(meeting);
        _secretary.IsActive = false;

        var response = await PostAsync(_chair, meeting, session, "start");
        using var body = await JsonOf(response);

        Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);
        Assert.Equal("MSG58", Code(body));
    }

    [Fact]
    public async Task Start_returns_MSG58_403_to_readers_who_are_not_the_chair_and_404_to_others()
    {
        var meeting = GivenRunningMeeting();
        var session = GivenSession(meeting);
        var foreign = GivenSession(GivenRunningMeeting());
        var outsider = GivenUser("outsider", BuiltInRoles.Member);

        var secretary = await PostAsync(_secretary, meeting, session, "start");
        using var secretaryBody = await JsonOf(secretary);
        var member = await PostAsync(_member, meeting, session, "start");
        var stranger = await PostAsync(outsider, meeting, session, "start");
        var otherMeeting = await PostAsync(_chair, meeting, foreign, "start");

        Assert.Equal(HttpStatusCode.Forbidden, secretary.StatusCode);
        Assert.Equal("MSG58", Code(secretaryBody));
        Assert.Equal(HttpStatusCode.Forbidden, member.StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, stranger.StatusCode);
        Assert.Empty(await stranger.Content.ReadAsByteArrayAsync());
        Assert.Equal(HttpStatusCode.NotFound, otherMeeting.StatusCode);
        Assert.Equal(MeetingSessionStatus.Planned, StoredMeeting(meeting.Id).Sessions.Single().Status);
    }

    [Fact]
    public async Task Start_returns_MSG141_and_keeps_the_session_planned_when_saving_fails()
    {
        var meeting = GivenRunningMeeting();
        var session = GivenSession(meeting);
        _factory.Meetings.NextSaveThrows = new Microsoft.EntityFrameworkCore.DbUpdateException("write failed");

        var response = await PostAsync(_chair, meeting, session, "start");
        using var body = await JsonOf(response);

        Assert.Equal(HttpStatusCode.InternalServerError, response.StatusCode);
        Assert.Equal("MSG141", Code(body));
        var stored = StoredMeeting(meeting.Id).Sessions.Single();
        Assert.Equal(MeetingSessionStatus.Planned, stored.Status);
        Assert.Null(stored.ActualStartedAt);
    }

    [Fact]
    public async Task Start_returns_MSG131_when_the_active_account_check_fails()
    {
        var meeting = GivenRunningMeeting();
        var session = GivenSession(meeting);
        _factory.Accounts.NextReadThrows = new Npgsql.NpgsqlException("database unavailable");

        var response = await PostAsync(_chair, meeting, session, "start");
        using var body = await JsonOf(response);

        Assert.Equal(HttpStatusCode.ServiceUnavailable, response.StatusCode);
        Assert.Equal("MSG131", Code(body));
    }

    // ---- UC-30 End Meeting Session ---------------------------------------------------------

    [Fact]
    public async Task End_completes_the_last_session_without_ending_the_meeting()
    {
        var meeting = GivenRunningMeeting();
        var session = GivenSession(meeting, MeetingSessionStatus.InProgress);
        var startedAt = DateTimeOffset.UtcNow.AddHours(-1);
        session.ActualStartedAt = startedAt;

        var response = await PostAsync(_chair, meeting, session, "end");
        using var body = await JsonOf(response);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal("MSG52", Code(body));
        Assert.Equal("Completed", body.RootElement.GetProperty("session").GetProperty("status").GetString());
        var stored = StoredMeeting(meeting.Id);
        var ended = stored.Sessions.Single();
        Assert.Equal(MeetingSessionStatus.Completed, ended.Status);
        Assert.Equal(startedAt, ended.ActualStartedAt);
        Assert.NotNull(ended.ActualEndedAt);
        Assert.Equal(MeetingStatus.InProgress, stored.Status);
        Assert.Empty(stored.StatusHistory);
        Assert.Empty(_factory.Meetings.Notifications);
    }

    [Theory]
    [InlineData(MeetingSessionStatus.Planned)]
    [InlineData(MeetingSessionStatus.Completed)]
    [InlineData(MeetingSessionStatus.Cancelled)]
    public async Task End_returns_MSG59_409_for_a_session_that_is_not_InProgress(MeetingSessionStatus status)
    {
        var meeting = GivenRunningMeeting();
        var session = GivenSession(meeting, status);

        var response = await PostAsync(_chair, meeting, session, "end");
        using var body = await JsonOf(response);

        Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);
        Assert.Equal("MSG59", Code(body));
        Assert.Equal(status, StoredMeeting(meeting.Id).Sessions.Single().Status);
    }

    [Fact]
    public async Task End_returns_MSG59_409_when_the_meeting_is_not_InProgress_and_403_to_the_secretary()
    {
        var ended = GivenRunningMeeting(MeetingStatus.Ended);
        var endedSession = GivenSession(ended, MeetingSessionStatus.InProgress);
        var running = GivenRunningMeeting();
        var runningSession = GivenSession(running, MeetingSessionStatus.InProgress);

        var closed = await PostAsync(_chair, ended, endedSession, "end");
        using var closedBody = await JsonOf(closed);
        var forbidden = await PostAsync(_secretary, running, runningSession, "end");
        using var forbiddenBody = await JsonOf(forbidden);

        Assert.Equal(HttpStatusCode.Conflict, closed.StatusCode);
        Assert.Equal("MSG59", Code(closedBody));
        Assert.Equal(HttpStatusCode.Forbidden, forbidden.StatusCode);
        Assert.Equal("MSG59", Code(forbiddenBody));
        Assert.Equal(MeetingSessionStatus.InProgress, StoredMeeting(running.Id).Sessions.Single().Status);
    }

    [Fact]
    public async Task End_returns_MSG142_and_keeps_the_session_running_when_saving_fails()
    {
        var meeting = GivenRunningMeeting();
        var session = GivenSession(meeting, MeetingSessionStatus.InProgress);
        _factory.Meetings.NextSaveThrows = new Microsoft.EntityFrameworkCore.DbUpdateException("write failed");

        var response = await PostAsync(_chair, meeting, session, "end");
        using var body = await JsonOf(response);

        Assert.Equal(HttpStatusCode.InternalServerError, response.StatusCode);
        Assert.Equal("MSG142", Code(body));
        var stored = StoredMeeting(meeting.Id).Sessions.Single();
        Assert.Equal(MeetingSessionStatus.InProgress, stored.Status);
        Assert.Null(stored.ActualEndedAt);
    }
}
