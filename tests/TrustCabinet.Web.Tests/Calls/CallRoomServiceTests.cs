using System.Text.Json;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using TrustCabinet.Domain.Calls;
using TrustCabinet.Domain.Enums;
using TrustCabinet.Infrastructure.Calls;

namespace TrustCabinet.Web.Tests.Calls;

public sealed class CallRoomServiceTests
{
    private readonly FakeCallRepository _calls = new();
    private readonly FakeLiveKitGateway _liveKit = new();
    private readonly LiveKitOptions _options = new() { TokenTtlMinutes = 10, EnforceRoster = true };
    private readonly Guid _meetingId = Guid.CreateVersion7();
    private readonly Guid _chair = Guid.CreateVersion7();
    private readonly Guid _member = Guid.CreateVersion7();

    private CallRoomService Service => new(_calls, _liveKit, Options.Create(_options), NullLogger<CallRoomService>.Instance);

    private string Room => CallRoomNames.ForMeeting(_meetingId);

    [Fact]
    public void Room_names_round_trip_and_reject_anything_else()
    {
        Assert.True(CallRoomNames.TryParseMeeting(CallRoomNames.ForMeeting(_meetingId), out var parsed));
        Assert.Equal(_meetingId, parsed);
        Assert.False(CallRoomNames.TryParseMeeting("direct-" + _meetingId, out _));
        Assert.False(CallRoomNames.TryParseMeeting("meeting-not-a-guid", out _));
        Assert.False(CallRoomNames.TryParseMeeting(null, out _));
    }

    [Theory]
    [InlineData(MeetingStatus.InProgress, MeetingMode.Online, CallRoomStatus.Open)]
    [InlineData(MeetingStatus.InProgress, MeetingMode.Hybrid, CallRoomStatus.Open)]
    [InlineData(MeetingStatus.InProgress, MeetingMode.InPerson, CallRoomStatus.InPersonOnly)]
    [InlineData(MeetingStatus.Published, MeetingMode.Online, CallRoomStatus.NotStarted)]
    [InlineData(MeetingStatus.Ended, MeetingMode.Online, CallRoomStatus.Ended)]
    public void Room_status_follows_meeting_status_and_mode(MeetingStatus status, MeetingMode mode, CallRoomStatus expected)
        => Assert.Equal(expected, CallRoomRules.StatusOf(status, mode));

    [Fact]
    public async Task Join_issues_a_token_for_the_callers_own_identity_and_the_meetings_room()
    {
        _calls.Invite(_meetingId, _chair, MeetingRole.Chair);

        var result = await Service.JoinAsync(_meetingId, _chair);

        Assert.Equal(CallOutcome.Success, result.Outcome);
        var ticket = _liveKit.LastTicket!.Value;
        Assert.Equal(Room, ticket.Room);
        Assert.Equal(_chair, ticket.UserId);
        Assert.Equal("Chair", JsonDocument.Parse(ticket.Metadata).RootElement.GetProperty("meetingRole").GetString());
        Assert.Equal(TimeSpan.FromMinutes(10), ticket.Ttl);
    }

    [Fact]
    public async Task Join_is_refused_in_order_not_invited_in_person_not_open_not_configured()
    {
        Assert.Equal(CallOutcome.NotFound, (await Service.JoinAsync(_meetingId, _member)).Outcome);

        _calls.Invite(_meetingId, _member, mode: MeetingMode.InPerson);
        Assert.Equal(CallOutcome.InPersonMeeting, (await Service.JoinAsync(_meetingId, _member)).Outcome);

        _calls.Invite(_meetingId, _member, status: MeetingStatus.Published);
        Assert.Equal(CallOutcome.RoomNotOpen, (await Service.JoinAsync(_meetingId, _member)).Outcome);

        _calls.Invite(_meetingId, _member);
        _liveKit.IsConfigured = false;
        Assert.Equal(CallOutcome.NotConfigured, (await Service.JoinAsync(_meetingId, _member)).Outcome);
        Assert.Null(_liveKit.LastTicket);
    }

    [Fact]
    public async Task Database_failure_on_join_is_read_failed()
    {
        _calls.ReadThrows = new TimeoutException();

        Assert.Equal(CallOutcome.ReadFailed, (await Service.JoinAsync(_meetingId, _member)).Outcome);
    }

    [Fact]
    public async Task Room_view_counts_people_in_the_room_and_survives_livekit_being_down()
    {
        _calls.Invite(_meetingId, _member);
        _liveKit.Join(Room, _chair);

        var view = (await Service.GetRoomAsync(_meetingId, _member)).Value!;
        Assert.Equal(CallRoomStatus.Open, view.Status);
        Assert.Equal(1, view.ParticipantCount);

        _liveKit.Unavailable = new LiveKitUnavailableException("down");
        var degraded = await Service.GetRoomAsync(_meetingId, _member);
        Assert.Equal(CallOutcome.Success, degraded.Outcome);
        Assert.Null(degraded.Value!.ParticipantCount);
    }

    [Fact]
    public async Task Chair_mutes_a_live_microphone_and_nobody_else_may()
    {
        _calls.Invite(_meetingId, _chair, MeetingRole.Chair).Invite(_meetingId, _member);
        _liveKit.Join(Room, _member, new RoomTrack("TR_mic", CallTrackSource.Microphone, Muted: false));

        Assert.Equal(CallOutcome.NotChair, await Service.MuteAsync(_meetingId, _member, _chair));
        Assert.Empty(_liveKit.Muted);

        Assert.Equal(CallOutcome.Success, await Service.MuteAsync(_meetingId, _chair, _member));
        Assert.Equal((Room, _member.ToString("D"), "TR_mic"), Assert.Single(_liveKit.Muted));
    }

    [Fact]
    public async Task Muting_someone_absent_or_already_muted()
    {
        _calls.Invite(_meetingId, _chair, MeetingRole.Chair);
        Assert.Equal(CallOutcome.TargetNotInRoom, await Service.MuteAsync(_meetingId, _chair, _member));

        _liveKit.Join(Room, _member, new RoomTrack("TR_mic", CallTrackSource.Microphone, Muted: true));
        Assert.Equal(CallOutcome.Success, await Service.MuteAsync(_meetingId, _chair, _member));
        Assert.Empty(_liveKit.Muted);

        _liveKit.Unavailable = new LiveKitUnavailableException("down");
        Assert.Equal(CallOutcome.ProviderFailed, await Service.MuteAsync(_meetingId, _chair, _member));
    }

    [Fact]
    public async Task Repeated_join_webhooks_open_one_stint_and_leave_closes_it()
    {
        var joinedAt = DateTimeOffset.UtcNow.AddMinutes(-3);
        var joined = Webhook("participant_joined", _member, joinedAt);

        await Service.HandleWebhookAsync(joined);
        await Service.HandleWebhookAsync(joined);

        var session = Assert.Single(_calls.Sessions);
        Assert.Equal(_member, session.StartedById);
        var stint = Assert.Single(_calls.Stints);
        Assert.Equal(joinedAt, stint.JoinedAt);

        await Service.HandleWebhookAsync(Webhook("participant_left", _member));
        Assert.NotNull(stint.LeftAt);

        await Service.HandleWebhookAsync(Webhook("room_finished", null));
        Assert.Equal(CallStatus.Ended, session.Status);
    }

    [Fact]
    public async Task Webhooks_for_other_rooms_or_synthetic_identities_are_ignored()
    {
        await Service.HandleWebhookAsync(Webhook("participant_joined", _member) with { RoomName = "direct-x" });
        await Service.HandleWebhookAsync(Webhook("participant_joined", null) with { ParticipantIdentity = "lk-load-test-1" });
        await Service.HandleWebhookAsync(Webhook("participant_left", _member));

        Assert.Empty(_calls.Sessions);
    }

    [Fact]
    public async Task A_new_screen_share_mutes_the_previous_presenter()
    {
        _liveKit.Join(Room, _chair, new RoomTrack("TR_old", CallTrackSource.ScreenShare, Muted: false));
        _liveKit.Join(Room, _member, new RoomTrack("TR_new", CallTrackSource.ScreenShare, Muted: false));

        await Service.HandleWebhookAsync(Webhook("track_published", _member) with { TrackSource = CallTrackSource.ScreenShare });

        Assert.Equal((Room, _chair.ToString("D"), "TR_old"), Assert.Single(_liveKit.Muted));
    }

    [Fact]
    public async Task Reconcile_closes_the_room_of_a_meeting_that_ended()
    {
        _calls.Invite(_meetingId, _member);
        await Service.HandleWebhookAsync(Webhook("participant_joined", _member));
        _liveKit.Join(Room, _member);
        _calls.MeetingStatusOverride[_meetingId] = MeetingStatus.Ended;

        await Service.ReconcileAsync();

        Assert.Contains(Room, _liveKit.Deleted);
        Assert.Equal(CallStatus.Ended, Assert.Single(_calls.Sessions).Status);
        Assert.NotNull(Assert.Single(_calls.Stints).LeftAt);
    }

    [Fact]
    public async Task Reconcile_removes_people_no_longer_allowed_and_repairs_lost_webhooks()
    {
        var lostJoin = Guid.CreateVersion7();
        var intruder = Guid.CreateVersion7();
        _calls.Invite(_meetingId, _member).Invite(_meetingId, lostJoin).Invite(_meetingId, _chair, MeetingRole.Chair);
        await Service.HandleWebhookAsync(Webhook("participant_joined", _chair)); // chair's leave webhook gets lost
        _liveKit.Join(Room, _member);
        _liveKit.Join(Room, lostJoin);
        _liveKit.Join(Room, intruder);

        await Service.HandleWebhookAsync(Webhook("participant_joined", _member));
        await Service.ReconcileAsync();

        Assert.Equal((Room, intruder.ToString("D")), Assert.Single(_liveKit.Removed));
        Assert.Contains(_calls.Stints, s => s.UserId == lostJoin && s.LeftAt == null);
        Assert.NotNull(_calls.Stints.Single(s => s.UserId == _chair).LeftAt);
    }

    [Fact]
    public async Task Reconcile_leaves_synthetic_participants_alone_when_roster_is_not_enforced()
    {
        _options.EnforceRoster = false;
        _calls.Invite(_meetingId, _member);
        await Service.HandleWebhookAsync(Webhook("participant_joined", _member));
        _liveKit.Join(Room, _member);
        _liveKit.Join(Room, new RoomParticipant("lk-load-test-1", "bot", DateTimeOffset.UtcNow, []));

        await Service.ReconcileAsync();

        Assert.Empty(_liveKit.Removed);
    }

    [Fact]
    public async Task A_room_nobody_invited_is_in_is_closed_unless_the_roster_is_not_enforced()
    {
        _liveKit.Join(Room, new RoomParticipant("lk-load-test-1", "bot", DateTimeOffset.UtcNow, []));

        _options.EnforceRoster = false;
        await Service.ReconcileAsync();
        Assert.Empty(_liveKit.Deleted);

        _options.EnforceRoster = true;
        await Service.ReconcileAsync();
        Assert.Contains(Room, _liveKit.Deleted);
    }

    private CallWebhookEvent Webhook(string name, Guid? identity, DateTimeOffset? joinedAt = null)
        => new(
            "EV_" + Guid.NewGuid().ToString("N"),
            name,
            Room,
            identity?.ToString("D"),
            joinedAt,
            null,
            CallTrackSource.Unknown,
            DateTimeOffset.UtcNow);
}
