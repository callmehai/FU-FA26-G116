using System.Text.Json;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using TrustCabinet.Domain.Audit;
using TrustCabinet.Domain.Calls;
using TrustCabinet.Domain.Enums;
using TrustCabinet.Domain.Meetings;
using TrustCabinet.Domain.Participants;
using TrustCabinet.Infrastructure.Calls;

namespace TrustCabinet.Web.Tests.Calls;

/// <summary>SCRUM-59: verified guests in the meeting room, against the fakes.</summary>
public sealed class GuestCallRoomServiceTests
{
    private readonly FakeCallRepository _calls = new();
    private readonly FakeLiveKitGateway _liveKit = new();
    private readonly Users.FakeAuditService _audit = new();
    private readonly LiveKitOptions _options = new() { TokenTtlMinutes = 10, EnforceRoster = true };
    private readonly Guid _meetingId = Guid.CreateVersion7();
    private readonly MeetingGuestSession _guest;

    public GuestCallRoomServiceTests() => _guest = new(_meetingId, Guid.CreateVersion7(), Guid.CreateVersion7());

    private CallRoomService Service => new(_calls, _liveKit, Options.Create(_options), _audit, NullLogger<CallRoomService>.Instance);

    private string Room => CallRoomNames.ForMeeting(_meetingId);

    private CallMember Member => CallMember.Guest(_guest.SessionId);

    private CallCaller Caller => new(Guest: _guest);

    [Fact]
    public void Identities_round_trip_and_a_guest_can_never_read_as_a_user()
    {
        var user = Guid.CreateVersion7();
        Assert.Equal(user.ToString("D"), CallMember.User(user).Identity);
        Assert.True(CallMember.TryParse(Member.Identity, out var guest));
        Assert.Equal(Member, guest);
        Assert.True(guest.IsGuest);
        Assert.False(Guid.TryParse(Member.Identity, out _));
        Assert.False(CallMember.TryParse("guest-not-a-guid", out _));
        Assert.False(CallMember.TryParse("lk-load-test-1", out _));
        Assert.False(CallMember.TryParse(null, out _));
    }

    [Fact]
    public async Task Guest_gets_a_member_ticket_only_after_confirming_the_recording_notice_and_it_is_audited()
    {
        _calls.AdmitGuest(_meetingId, _guest.SessionId);

        Assert.Equal(CallOutcome.RecordingNoticeRequired, (await Service.JoinAsync(_meetingId, Caller)).Outcome);
        Assert.Null(_liveKit.LastTicket);
        Assert.Empty(_audit.Records);

        var result = await Service.JoinAsync(_meetingId, Caller, recordingNoticeAcknowledged: true);

        Assert.Equal(CallOutcome.Success, result.Outcome);
        var ticket = _liveKit.LastTicket!.Value;
        Assert.Equal((Room, Member.Identity, "Khách tự khai"), (ticket.Room, ticket.Identity, ticket.Name));
        Assert.Equal("Guest", JsonDocument.Parse(ticket.Metadata).RootElement.GetProperty("meetingRole").GetString());
        var record = Assert.Single(_audit.Records);
        Assert.Equal((AuditActions.CallGuestAdmitted, AuditActions.GuestSessionObjectType, _guest.SessionId, (Guid?)null),
            (record.Action, record.ObjectType, record.ObjectId, record.ActorId));
        Assert.Contains("\"recordingNoticeAcknowledged\":true", JsonSerializer.Serialize(record.Details));
    }

    [Fact]
    public async Task Guest_of_another_meeting_or_without_access_is_not_found_and_the_room_rules_still_apply()
    {
        var foreign = new CallCaller(Guest: _guest with { MeetingId = Guid.CreateVersion7() });
        _calls.AdmitGuest(_meetingId, _guest.SessionId);
        Assert.Equal(CallOutcome.NotFound, (await Service.GetRoomAsync(_meetingId, foreign)).Outcome);
        Assert.Equal(CallOutcome.NotFound, (await Service.JoinAsync(_meetingId, foreign, true)).Outcome);

        _calls.GuestAccess.Clear();
        Assert.Equal(CallOutcome.NotFound, (await Service.JoinAsync(_meetingId, Caller, true)).Outcome);

        _calls.AdmitGuest(_meetingId, _guest.SessionId, MeetingStatus.Published);
        Assert.Equal(CallRoomStatus.NotStarted, (await Service.GetRoomAsync(_meetingId, Caller)).Value!.Status);
        Assert.Equal(MeetingRole.Guest, (await Service.GetRoomAsync(_meetingId, Caller)).Value!.MyRole);
        Assert.Equal(CallOutcome.RoomNotOpen, (await Service.JoinAsync(_meetingId, Caller, true)).Outcome);

        _calls.AdmitGuest(_meetingId, _guest.SessionId, mode: MeetingMode.InPerson);
        Assert.Equal(CallOutcome.InPersonMeeting, (await Service.JoinAsync(_meetingId, Caller, true)).Outcome);
        Assert.Null(_liveKit.LastTicket);
        Assert.Empty(_audit.Records);
    }

    [Fact]
    public async Task Guest_visit_is_a_guest_stint_and_a_guest_may_open_the_room()
    {
        _calls.AdmitGuest(_meetingId, _guest.SessionId);

        await Service.HandleWebhookAsync(Webhook("participant_joined"));
        await Service.HandleWebhookAsync(Webhook("participant_joined"));

        Assert.Null(Assert.Single(_calls.Sessions).StartedById);
        var stint = Assert.Single(_calls.Stints);
        Assert.Equal((null, _guest.SessionId), (stint.UserId, stint.GuestSessionId));
        Assert.Empty(_liveKit.Removed);

        await Service.HandleWebhookAsync(Webhook("participant_left"));
        Assert.NotNull(stint.LeftAt);
    }

    [Fact]
    public async Task Guest_joining_with_an_old_ticket_after_losing_access_is_taken_out_at_once()
    {
        await Service.HandleWebhookAsync(Webhook("participant_joined"));

        Assert.Equal((Room, Member.Identity), Assert.Single(_liveKit.Removed));
        Assert.Single(_calls.Stints); // the visit is still evidence
        Assert.Empty(_liveKit.Deleted);
    }

    [Fact]
    public async Task Reconcile_keeps_an_admitted_guest_and_takes_out_one_whose_access_ended()
    {
        var revoked = CallMember.Guest(Guid.CreateVersion7());
        _calls.AdmitGuest(_meetingId, _guest.SessionId);
        _liveKit.Join(Room, Member);
        _liveKit.Join(Room, revoked);

        await Service.ReconcileAsync();

        Assert.Equal((Room, revoked.Identity), Assert.Single(_liveKit.Removed));
        Assert.Null(Assert.Single(_calls.Sessions).StartedById);
        Assert.Equal(_guest.SessionId, Assert.Single(_calls.Stints).GuestSessionId);

        // Revoked mid-meeting: the next reconciliation closes the guest's stint as well.
        _calls.GuestAccess.Clear();
        await Service.ReconcileAsync();
        Assert.Contains((Room, Member.Identity), _liveKit.Removed);
        Assert.NotNull(Assert.Single(_calls.Stints).LeftAt);
    }

    [Fact]
    public async Task Enforcing_one_room_takes_out_whoever_lost_access_without_touching_other_rooms()
    {
        var otherRoom = CallRoomNames.ForMeeting(Guid.CreateVersion7());
        _calls.AdmitGuest(_meetingId, _guest.SessionId);
        await Service.HandleWebhookAsync(Webhook("participant_joined"));
        _liveKit.Join(Room, Member);
        _liveKit.Join(otherRoom, CallMember.Guest(Guid.CreateVersion7()));
        _calls.GuestAccess.Clear(); // revoked

        await Service.EnforceRoomAccessAsync(_meetingId);

        Assert.Equal((Room, Member.Identity), Assert.Single(_liveKit.Removed));
        Assert.NotNull(Assert.Single(_calls.Stints).LeftAt);
    }

    [Fact]
    public async Task Enforcing_a_room_with_no_active_session_or_no_livekit_calls_nothing()
    {
        _liveKit.Join(Room, Member);
        _liveKit.Unavailable = new LiveKitUnavailableException("any LiveKit call would throw");

        await Service.EnforceRoomAccessAsync(_meetingId); // e.g. a Published meeting: nobody known to be in

        _liveKit.IsConfigured = false;
        await Service.EnforceRoomAccessAsync(_meetingId);
        Assert.Empty(_liveKit.Removed);
        Assert.Empty(_liveKit.Deleted);
    }

    [Fact]
    public async Task The_access_publisher_gives_livekit_two_seconds_then_leaves_it_to_the_janitor()
    {
        var hanging = new HangingRooms();
        var publisher = new CallRoomAccessPublisher(hanging);
        var started = DateTimeOffset.UtcNow;

        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => publisher.PublishAsync(
            new MeetingAccessChange(_meetingId, _guest.InvitationId, IsGuest: true, Guid.CreateVersion7()), CancellationToken.None));

        Assert.Equal(_meetingId, hanging.Enforced);
        Assert.InRange(DateTimeOffset.UtcNow - started, TimeSpan.FromSeconds(1.5), TimeSpan.FromSeconds(10));
    }

    [Fact]
    public async Task Chair_mutes_a_guest_like_any_member()
    {
        var chair = Guid.CreateVersion7();
        _calls.Invite(_meetingId, chair, MeetingRole.Chair);
        _liveKit.Join(Room, Member, new RoomTrack("TR_guest_mic", CallTrackSource.Microphone, Muted: false));

        Assert.Equal(CallOutcome.Success, await Service.MuteAsync(_meetingId, chair, Member));
        Assert.Equal((Room, Member.Identity, "TR_guest_mic"), Assert.Single(_liveKit.Muted));
    }

    /// <summary>A room service whose LiveKit never answers, honouring cancellation as the real gateway does.</summary>
    private sealed class HangingRooms : ICallRoomService
    {
        public Guid? Enforced { get; private set; }

        public async Task EnforceRoomAccessAsync(Guid meetingId, CancellationToken ct = default)
        {
            Enforced = meetingId;
            await Task.Delay(Timeout.Infinite, ct);
        }

        public Task CloseMeetingRoomAsync(Guid meetingId, CancellationToken ct = default) => throw new NotSupportedException();
        public Task<CallResult<CallRoomView>> GetRoomAsync(Guid meetingId, CallCaller caller, CancellationToken ct = default) => throw new NotSupportedException();
        public Task<CallResult<CallJoinTicket>> JoinAsync(Guid meetingId, CallCaller caller, bool recordingNoticeAcknowledged = false, CancellationToken ct = default) => throw new NotSupportedException();
        public Task<CallOutcome> MuteAsync(Guid meetingId, Guid actorId, CallMember target, CancellationToken ct = default) => throw new NotSupportedException();
        public Task HandleWebhookAsync(CallWebhookEvent webhook, CancellationToken ct = default) => throw new NotSupportedException();
        public Task ReconcileAsync(CancellationToken ct = default) => throw new NotSupportedException();
    }

    private CallWebhookEvent Webhook(string name)
        => new("EV_" + Guid.NewGuid().ToString("N"), name, Room, Member.Identity, null, null, CallTrackSource.Unknown,
            DateTimeOffset.UtcNow);
}
