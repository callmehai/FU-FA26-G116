using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using TrustCabinet.Application.Abstractions;
using TrustCabinet.Application.Calls;
using TrustCabinet.Application.Notes;
using TrustCabinet.Application.Participants;
using TrustCabinet.Application.Speaking;
using TrustCabinet.Application.Tests.Fakes;
using TrustCabinet.Domain.Audit;
using TrustCabinet.Domain.Calls;
using TrustCabinet.Domain.Entities;
using TrustCabinet.Domain.Enums;
using TrustCabinet.Domain.Meetings;
using TrustCabinet.Domain.Participants;

namespace TrustCabinet.Application.Tests.Unit.Round1;

// Round 1 unit tests for FE-08 (Online Room/Calls): RoomUnitRound1.
// Every test carries [Trait("UTC", ...)] + [Trait("Trait", "UTC")] and the
// "<Class.Method>/<UTCnnn>" string in its DisplayName, so the trx (which has
// no traits) is matched back to the UTC rows by name.
public sealed class RoomUnitRound1Tests
{
    private static readonly DateTimeOffset Now = new(2026, 11, 18, 3, 0, 0, TimeSpan.Zero);

    private readonly FakeCallRepository _calls = new();
    private readonly FakeUnitOfWork _unitOfWork = new();
    private readonly FakeLiveKitGateway _liveKit = new();
    private readonly FakeAuditService _audit = new();
    private readonly RoomCapturingLogger<CallRoomService> _roomLog = new();
    private readonly FakeSpeakingChangePublisher _speaking;
    private readonly Guid _meeting = Guid.CreateVersion7();
    private readonly Guid _chair = Guid.CreateVersion7();
    private readonly Guid _member = Guid.CreateVersion7();

    public RoomUnitRound1Tests()
    {
        _speaking = new(_unitOfWork);
        _calls.Access[_chair] = new(_meeting, MeetingStatus.InProgress, MeetingMode.Online, MeetingRole.Chair, "Chu tri");
        _calls.Access[_member] = new(_meeting, MeetingStatus.InProgress, MeetingMode.Online, MeetingRole.Member, "Thanh vien");
    }

    private CallRoomService Room(bool enforceRoster = true) => new(_calls, _unitOfWork, _liveKit,
        Options.Create(new LiveKitOptions { TokenTtlMinutes = 10, EnforceRoster = enforceRoster }), _audit, _roomLog,
        timeProvider: new FixedClock(Now), speaking: _speaking);

    private string RoomName => CallRoomNames.ForMeeting(_meeting);

    // ---- CallRoomRules.StatusOf ----

    [Fact(DisplayName = "CallRoomRules.StatusOf/UTC001: InProgress Online is Open")]
    [Trait("UTC", "CallRoomRules.StatusOf/UTC001")]
    [Trait("Trait", "UTC")]
    public void StatusOf_InProgressOnline_IsOpen()
    {
        Assert.Equal(CallRoomStatus.Open, CallRoomRules.StatusOf(MeetingStatus.InProgress, MeetingMode.Online));
        Assert.Equal(CallRoomStatus.Open, CallRoomRules.StatusOf(MeetingStatus.InProgress, MeetingMode.Hybrid));
        Assert.NotEqual(CallRoomStatus.Open, CallRoomRules.StatusOf(MeetingStatus.Published, MeetingMode.Online));
    }

    [Fact(DisplayName = "CallRoomRules.StatusOf/UTC002: InProgress Hybrid is Open")]
    [Trait("UTC", "CallRoomRules.StatusOf/UTC002")]
    [Trait("Trait", "UTC")]
    public void StatusOf_InProgressHybrid_IsOpen()
    {
        Assert.Equal(CallRoomStatus.Open, CallRoomRules.StatusOf(MeetingStatus.InProgress, MeetingMode.Hybrid));
        Assert.Equal(CallRoomRules.StatusOf(MeetingStatus.InProgress, MeetingMode.Online),
            CallRoomRules.StatusOf(MeetingStatus.InProgress, MeetingMode.Hybrid));
        Assert.NotEqual(CallRoomStatus.Open, CallRoomRules.StatusOf(MeetingStatus.Ended, MeetingMode.Online));
    }

    [Fact(DisplayName = "CallRoomRules.StatusOf/UTC003: Published Online is NotStarted")]
    [Trait("UTC", "CallRoomRules.StatusOf/UTC003")]
    [Trait("Trait", "UTC")]
    public void StatusOf_PublishedOnline_IsNotStarted()
    {
        Assert.Equal(CallRoomStatus.NotStarted, CallRoomRules.StatusOf(MeetingStatus.Published, MeetingMode.Online));
        Assert.Equal(CallRoomStatus.NotStarted, CallRoomRules.StatusOf(MeetingStatus.WaitingApproval, MeetingMode.Online));
        Assert.Equal(CallRoomStatus.NotStarted, CallRoomRules.StatusOf(MeetingStatus.Draft, MeetingMode.Online));
    }

    [Fact(DisplayName = "CallRoomRules.StatusOf/UTC004: InProgress InPerson is InPersonOnly")]
    [Trait("UTC", "CallRoomRules.StatusOf/UTC004")]
    [Trait("Trait", "UTC")]
    public void StatusOf_InProgressInPerson_IsInPersonOnly()
    {
        Assert.Equal(CallRoomStatus.InPersonOnly, CallRoomRules.StatusOf(MeetingStatus.InProgress, MeetingMode.InPerson));
        Assert.Equal(CallRoomStatus.InPersonOnly, CallRoomRules.StatusOf(MeetingStatus.Ended, MeetingMode.InPerson));
        Assert.NotEqual(CallRoomStatus.Open, CallRoomRules.StatusOf(MeetingStatus.InProgress, MeetingMode.InPerson));
    }

    // ---- CallRoomService.JoinAsync ----

    [Fact(DisplayName = "CallRoomService.JoinAsync/UTC001: member joins an InProgress online meeting")]
    [Trait("UTC", "CallRoomService.JoinAsync/UTC001")]
    [Trait("Trait", "UTC")]
    public async Task Join_MemberInProgressOnline_GetsTicket()
    {
        var result = await Room().JoinAsync(_meeting, _member);

        Assert.Equal(CallOutcome.Success, result.Outcome);
        Assert.Equal($"meeting-{_meeting:D}", result.Value!.RoomName);
        Assert.Equal(DateTimeOffset.UnixEpoch.AddMinutes(10), result.Value.ExpiresAt);
        Assert.Empty(_roomLog.Messages);
    }

    [Fact(DisplayName = "CallRoomService.JoinAsync/UTC002: in-person meeting refuses with MSG280")]
    [Trait("UTC", "CallRoomService.JoinAsync/UTC002")]
    [Trait("Trait", "UTC")]
    public async Task Join_InPersonMeeting_IsInPersonMeeting()
    {
        _calls.Access[_member] = new(_meeting, MeetingStatus.InProgress, MeetingMode.InPerson, MeetingRole.Member, "Thanh vien");

        var result = await Room().JoinAsync(_meeting, _member);

        Assert.Equal(CallOutcome.InPersonMeeting, result.Outcome);
        Assert.Null(result.Value);
        Assert.Empty(_liveKit.Calls);
    }

    [Fact(DisplayName = "CallRoomService.JoinAsync/UTC003: published meeting refuses with MSG117")]
    [Trait("UTC", "CallRoomService.JoinAsync/UTC003")]
    [Trait("Trait", "UTC")]
    public async Task Join_PublishedMeeting_IsRoomNotOpen()
    {
        _calls.Access[_member] = new(_meeting, MeetingStatus.Published, MeetingMode.Online, MeetingRole.Member, "Thanh vien");

        var result = await Room().JoinAsync(_meeting, _member);

        Assert.Equal(CallOutcome.RoomNotOpen, result.Outcome);
        Assert.Null(result.Value);
        Assert.Empty(_liveKit.Calls);
    }

    [Fact(DisplayName = "CallRoomService.JoinAsync/UTC004: unconfigured provider refuses with MSG281")]
    [Trait("UTC", "CallRoomService.JoinAsync/UTC004")]
    [Trait("Trait", "UTC")]
    public async Task Join_LiveKitNotConfigured_IsNotConfigured()
    {
        _liveKit.IsConfigured = false;

        var result = await Room().JoinAsync(_meeting, _member);

        Assert.Equal(CallOutcome.NotConfigured, result.Outcome);
        Assert.Null(result.Value);
        Assert.Empty(_liveKit.Calls);
    }

    // ---- CallRoomService.MuteAsync ----

    private void SeedMemberWithMicrophone(bool muted)
    {
        _liveKit.Rooms[RoomName] =
        [
            new RoomParticipant(_member.ToString("D"), "Thanh vien", Now,
                [new RoomTrack("mic-1", CallTrackSource.Microphone, muted)]),
        ];
    }

    [Fact(DisplayName = "CallRoomService.MuteAsync/UTC001: chair mutes a member microphone")]
    [Trait("UTC", "CallRoomService.MuteAsync/UTC001")]
    [Trait("Trait", "UTC")]
    public async Task Mute_ChairMutesUnmutedMicrophone_Succeeds()
    {
        SeedMemberWithMicrophone(muted: false);

        var outcome = await Room().MuteAsync(_meeting, _chair, CallMember.User(_member));

        Assert.Equal(CallOutcome.Success, outcome);
        Assert.Contains(_liveKit.Calls, c => c == $"mute:{_member:D}:mic-1");
        Assert.Empty(_roomLog.Messages);
    }

    [Fact(DisplayName = "CallRoomService.MuteAsync/UTC002: member muting another is refused MSG123")]
    [Trait("UTC", "CallRoomService.MuteAsync/UTC002")]
    [Trait("Trait", "UTC")]
    public async Task Mute_MemberMutesAnother_IsNotChair()
    {
        SeedMemberWithMicrophone(muted: false);

        var outcome = await Room().MuteAsync(_meeting, _member, CallMember.User(_chair));

        Assert.Equal(CallOutcome.NotChair, outcome);
        Assert.DoesNotContain(_liveKit.Calls, c => c.StartsWith("mute:"));
        Assert.Empty(_roomLog.Messages);
    }

    [Fact(DisplayName = "CallRoomService.MuteAsync/UTC003: published meeting mute is refused MSG117")]
    [Trait("UTC", "CallRoomService.MuteAsync/UTC003")]
    [Trait("Trait", "UTC")]
    public async Task Mute_PublishedMeeting_IsRoomNotOpen()
    {
        _calls.Access[_chair] = new(_meeting, MeetingStatus.Published, MeetingMode.Online, MeetingRole.Chair, "Chu tri");
        SeedMemberWithMicrophone(muted: false);

        var outcome = await Room().MuteAsync(_meeting, _chair, CallMember.User(_member));

        Assert.Equal(CallOutcome.RoomNotOpen, outcome);
        Assert.DoesNotContain(_liveKit.Calls, c => c.StartsWith("mute:"));
        Assert.Empty(_roomLog.Messages);
    }

    [Fact(DisplayName = "CallRoomService.MuteAsync/UTC004: unconfigured provider refuses MSG281")]
    [Trait("UTC", "CallRoomService.MuteAsync/UTC004")]
    [Trait("Trait", "UTC")]
    public async Task Mute_LiveKitNotConfigured_IsNotConfigured()
    {
        _liveKit.IsConfigured = false;

        var outcome = await Room().MuteAsync(_meeting, _chair, CallMember.User(_member));

        Assert.Equal(CallOutcome.NotConfigured, outcome);
        Assert.Empty(_liveKit.Calls);
        Assert.Empty(_roomLog.Messages);
    }

    // ---- CallRoomService.GetRoomAsync ----

    [Fact(DisplayName = "CallRoomService.GetRoomAsync/UTC001: member sees the open room view")]
    [Trait("UTC", "CallRoomService.GetRoomAsync/UTC001")]
    [Trait("Trait", "UTC")]
    public async Task GetRoom_MemberInProgressOnline_ReturnsOpenView()
    {
        _liveKit.Rooms[RoomName] =
        [
            new RoomParticipant(_member.ToString("D"), "Thanh vien", Now, []),
            new RoomParticipant(_chair.ToString("D"), "Chu tri", Now, []),
        ];

        var result = await Room().GetRoomAsync(_meeting, _member);

        Assert.Equal(CallOutcome.Success, result.Outcome);
        Assert.Equal(CallRoomStatus.Open, result.Value!.Status);
        Assert.Equal(2, result.Value.ParticipantCount);
        Assert.Empty(_roomLog.Messages);
    }

    [Fact(DisplayName = "CallRoomService.GetRoomAsync/UTC002: published meeting shows NotStarted")]
    [Trait("UTC", "CallRoomService.GetRoomAsync/UTC002")]
    [Trait("Trait", "UTC")]
    public async Task GetRoom_PublishedMeeting_ReturnsNotStartedWithoutCounting()
    {
        _calls.Access[_member] = new(_meeting, MeetingStatus.Published, MeetingMode.Online, MeetingRole.Member, "Thanh vien");

        var result = await Room().GetRoomAsync(_meeting, _member);

        Assert.Equal(CallOutcome.Success, result.Outcome);
        Assert.Equal(CallRoomStatus.NotStarted, result.Value!.Status);
        Assert.Null(result.Value.ParticipantCount);
        Assert.DoesNotContain(_liveKit.Calls, c => c is "list" or "rooms");
    }

    [Fact(DisplayName = "CallRoomService.GetRoomAsync/UTC003: uncountable room still opens")]
    [Trait("UTC", "CallRoomService.GetRoomAsync/UTC003")]
    [Trait("Trait", "UTC")]
    public async Task GetRoom_CountFails_ReturnsOpenWithNullCount()
    {
        _liveKit.Unavailable = new LiveKitUnavailableException("down");

        var result = await Room().GetRoomAsync(_meeting, _member);

        Assert.Equal(CallOutcome.Success, result.Outcome);
        Assert.Null(result.Value!.ParticipantCount);
        Assert.Contains(_roomLog.Messages, m => m.Contains("Could not count participants"));
    }

    [Fact(DisplayName = "CallRoomService.GetRoomAsync/UTC004: storage failure is ReadFailed MSG283")]
    [Trait("UTC", "CallRoomService.GetRoomAsync/UTC004")]
    [Trait("Trait", "UTC")]
    public async Task GetRoom_StorageFailure_IsReadFailed()
    {
        _calls.Fail = new PersistenceUnavailableException();

        var result = await Room().GetRoomAsync(_meeting, _member);

        Assert.Equal(CallOutcome.ReadFailed, result.Outcome);
        Assert.Null(result.Value);
        Assert.Contains(_roomLog.Messages, m => m.Contains("Could not read call room access"));
    }

    // ---- CallRoomService.CloseMeetingRoomAsync ----

    [Fact(DisplayName = "CallRoomService.CloseMeetingRoomAsync/UTC001: closing ends session and hands")]
    [Trait("UTC", "CallRoomService.CloseMeetingRoomAsync/UTC001")]
    [Trait("Trait", "UTC")]
    public async Task CloseMeetingRoom_OpenSession_DeletesRoomEndsSessionAndClosesHands()
    {
        _calls.ActiveSession = new() { MeetingId = _meeting, Kind = CallKind.MeetingRoom, Status = CallStatus.Active, StartedAt = Now };
        _calls.ClosedHands = 1;

        await Room().CloseMeetingRoomAsync(_meeting);

        Assert.Contains(_liveKit.Calls, c => c == $"delete:{RoomName}");
        Assert.Contains(_calls.Calls, c => c == "end-session");
        Assert.Equal("Left", Assert.Single(_speaking.Published).Change.ChangeType);
    }

    [Fact(DisplayName = "CallRoomService.CloseMeetingRoomAsync/UTC002: unconfigured provider does nothing")]
    [Trait("UTC", "CallRoomService.CloseMeetingRoomAsync/UTC002")]
    [Trait("Trait", "UTC")]
    public async Task CloseMeetingRoom_NotConfigured_DoesNothing()
    {
        _liveKit.IsConfigured = false;

        await Room().CloseMeetingRoomAsync(_meeting);

        Assert.Empty(_liveKit.Calls);
        Assert.Empty(_calls.Calls);
        Assert.Empty(_speaking.Published);
    }

    // ---- CallRoomService.EnforceRoomAccessAsync ----

    [Fact(DisplayName = "CallRoomService.EnforceRoomAccessAsync/UTC001: removed participant is taken out at once")]
    [Trait("UTC", "CallRoomService.EnforceRoomAccessAsync/UTC001")]
    [Trait("Trait", "UTC")]
    public async Task EnforceRoomAccess_ActiveSession_RemovesWhoLostAccess()
    {
        var outsider = Guid.CreateVersion7();
        _calls.Sessions.Add(new(Guid.CreateVersion7(), _meeting, MeetingStatus.InProgress, MeetingMode.Online,
            new HashSet<CallMember> { CallMember.User(_chair), CallMember.User(outsider) }));
        _liveKit.Rooms[RoomName] =
        [
            new RoomParticipant(_chair.ToString("D"), "Chu tri", Now, []),
            new RoomParticipant(outsider.ToString("D"), "Bi go", Now, []),
        ];

        await Room().EnforceRoomAccessAsync(_meeting);

        Assert.Contains(_liveKit.Calls, c => c == $"remove:{outsider:D}");
        Assert.DoesNotContain(_liveKit.Calls, c => c == $"remove:{_chair:D}");
        Assert.Contains(_roomLog.Messages, m => m.Contains(outsider.ToString("D")) && m.Contains("no longer allowed in"));
    }

    [Fact(DisplayName = "CallRoomService.EnforceRoomAccessAsync/UTC002: no session calls nothing")]
    [Trait("UTC", "CallRoomService.EnforceRoomAccessAsync/UTC002")]
    [Trait("Trait", "UTC")]
    public async Task EnforceRoomAccess_NoSession_ProviderNotCalled()
    {
        await Room().EnforceRoomAccessAsync(_meeting);

        Assert.Empty(_liveKit.Calls);
        Assert.Contains(_calls.Calls, c => c == "list-sessions");
        Assert.Empty(_roomLog.Messages);
    }

    // ---- CallRoomService.HandleWebhookAsync ----

    [Fact(DisplayName = "CallRoomService.HandleWebhookAsync/UTC001: joined member visit is recorded")]
    [Trait("UTC", "CallRoomService.HandleWebhookAsync/UTC001")]
    [Trait("Trait", "UTC")]
    public async Task Webhook_JoinedMember_EnsuresSessionAndRecordsJoin()
    {
        await Room().HandleWebhookAsync(new CallWebhookEvent("e1", "participant_joined", RoomName,
            _member.ToString("D"), Now, null, CallTrackSource.Unknown, Now));

        Assert.Contains(_calls.Calls, c => c == "ensure-session");
        Assert.Contains(_calls.Calls, c => c == "join");
        Assert.NotNull(_calls.ActiveSession);
        Assert.DoesNotContain(_liveKit.Calls, c => c.StartsWith("delete:"));
    }

    [Fact(DisplayName = "CallRoomService.HandleWebhookAsync/UTC002: leaver with a hand publishes Left")]
    [Trait("UTC", "CallRoomService.HandleWebhookAsync/UTC002")]
    [Trait("Trait", "UTC")]
    public async Task Webhook_LeaverWithHand_ClosesLeaveAndPublishesLeft()
    {
        _calls.ActiveSession = new() { MeetingId = _meeting, Kind = CallKind.MeetingRoom, Status = CallStatus.Active, StartedAt = Now };
        _calls.ClosedHands = 1;

        await Room().HandleWebhookAsync(new CallWebhookEvent("e1", "participant_left", RoomName,
            _member.ToString("D"), null, null, CallTrackSource.Unknown, Now));

        Assert.Contains(_calls.Calls, c => c == "leave");
        Assert.Equal("Left", Assert.Single(_speaking.Published).Change.ChangeType);
        Assert.Empty(_roomLog.Messages);
    }

    [Fact(DisplayName = "CallRoomService.HandleWebhookAsync/UTC003: second screen share mutes the first")]
    [Trait("UTC", "CallRoomService.HandleWebhookAsync/UTC003")]
    [Trait("Trait", "UTC")]
    public async Task Webhook_SecondScreenShare_MutesFirstSharerOnly()
    {
        var first = Guid.CreateVersion7();
        _liveKit.Rooms[RoomName] =
        [
            new RoomParticipant(first.ToString("D"), "Nguoi 1", Now,
                [new RoomTrack("scr-1", CallTrackSource.ScreenShare, false)]),
            new RoomParticipant(_member.ToString("D"), "Thanh vien", Now,
                [new RoomTrack("scr-2", CallTrackSource.ScreenShare, false)]),
        ];

        await Room().HandleWebhookAsync(new CallWebhookEvent("e1", "track_published", RoomName,
            _member.ToString("D"), null, "scr-2", CallTrackSource.ScreenShare, Now));

        Assert.Contains(_liveKit.Calls, c => c == $"mute:{first:D}:scr-1");
        Assert.DoesNotContain(_liveKit.Calls, c => c.Contains($"mute:{_member:D}"));
        Assert.Empty(_roomLog.Messages);
    }

    [Fact(DisplayName = "CallRoomService.HandleWebhookAsync/UTC004: unknown device is ignored")]
    [Trait("UTC", "CallRoomService.HandleWebhookAsync/UTC004")]
    [Trait("Trait", "UTC")]
    public async Task Webhook_UnknownDevice_Ignored()
    {
        await Room().HandleWebhookAsync(new CallWebhookEvent("e1", "participant_joined", RoomName,
            "unknown-device", Now, null, CallTrackSource.Unknown, Now));

        Assert.DoesNotContain(_calls.Calls, c => c is "ensure-session" or "join");
        Assert.Empty(_liveKit.Calls);
        Assert.Empty(_roomLog.Messages);
    }

    // ---- CallRoomService.ReconcileAsync ----

    [Fact(DisplayName = "CallRoomService.ReconcileAsync/UTC001: ended sitting closes the room and open hands")]
    [Trait("UTC", "CallRoomService.ReconcileAsync/UTC001")]
    [Trait("Trait", "UTC")]
    public async Task Reconcile_SittingEnded_DeletesRoomEndsSessionClosesHands()
    {
        var session = Guid.CreateVersion7();
        _calls.Sessions.Add(new(session, _meeting, MeetingStatus.Ended, MeetingMode.Online, new HashSet<CallMember>()));
        _calls.ClosedHands = 2;
        _liveKit.Rooms[RoomName] = [];

        await Room().ReconcileAsync();

        Assert.Contains(_liveKit.Calls, c => c == $"delete:{RoomName}");
        Assert.Contains(_calls.Calls, c => c == "end-session");
        var left = Assert.Single(_speaking.Published);
        Assert.Equal(_meeting, left.Change.MeetingId);
        Assert.Equal("Left", left.Change.ChangeType);
    }

    [Fact(DisplayName = "CallRoomService.ReconcileAsync/UTC002: person without access is removed, allowed keep their visits")]
    [Trait("UTC", "CallRoomService.ReconcileAsync/UTC002")]
    [Trait("Trait", "UTC")]
    public async Task Reconcile_PersonNoLongerAllowed_RemovedOthersUntouched()
    {
        var outsider = Guid.CreateVersion7();
        _calls.Sessions.Add(new(Guid.CreateVersion7(), _meeting, MeetingStatus.InProgress, MeetingMode.Online,
            new HashSet<CallMember> { CallMember.User(_chair) }));
        _liveKit.Rooms[RoomName] =
        [
            new RoomParticipant(_chair.ToString("D"), "Chu tri", Now, []),
            new RoomParticipant(outsider.ToString("D"), "Nguoi la", Now, []),
        ];

        await Room().ReconcileAsync();

        Assert.Contains(_liveKit.Calls, c => c == $"remove:{outsider:D}");
        Assert.DoesNotContain(_liveKit.Calls, c => c == $"remove:{_chair:D}");
        Assert.DoesNotContain(_calls.Calls, c => c is "leave" or "join" or "end-session");
        Assert.Contains(_roomLog.Messages, m => m.Contains("Removing") && m.Contains(RoomName) && m.Contains("no longer allowed in"));
    }

    [Fact(DisplayName = "CallRoomService.ReconcileAsync/UTC003: lost joins create one session from the earliest join")]
    [Trait("UTC", "CallRoomService.ReconcileAsync/UTC003")]
    [Trait("Trait", "UTC")]
    public async Task Reconcile_NoSessionButAllowedMembers_CreatesSessionAndRecordsVisits()
    {
        _liveKit.Rooms[RoomName] =
        [
            new RoomParticipant(_member.ToString("D"), "Thanh vien", Now.AddMinutes(-1), []),
            new RoomParticipant(_chair.ToString("D"), "Chu tri", Now.AddMinutes(-5), []),
        ];

        await Room().ReconcileAsync();

        Assert.Equal(1, _calls.Calls.Count(c => c == "ensure-session"));
        Assert.Equal(Now.AddMinutes(-5), _calls.ActiveSession!.StartedAt);
        Assert.Equal(2, _calls.Calls.Count(c => c == "join"));
        Assert.DoesNotContain(_liveKit.Calls, c => c.StartsWith("delete:") || c.StartsWith("remove:"));
    }

    [Fact(DisplayName = "CallRoomService.ReconcileAsync/UTC004: orphan room is deleted")]
    [Trait("UTC", "CallRoomService.ReconcileAsync/UTC004")]
    [Trait("Trait", "UTC")]
    public async Task Reconcile_OrphanRoom_DeletesIt()
    {
        _liveKit.Rooms[RoomName] = [];

        await Room().ReconcileAsync();

        Assert.Contains(_liveKit.Calls, c => c == $"delete:{RoomName}");
        Assert.DoesNotContain(_calls.Calls, c => c is "end-session" or "join" or "leave");
        Assert.Empty(_roomLog.Messages);
    }
}

public sealed class MeetingNoteRound1Tests
{
    private static readonly DateTimeOffset Now = new(2026, 11, 18, 3, 0, 0, TimeSpan.Zero);

    private readonly FakeMeetingNoteRepository _notes = new();
    private readonly FakeMeetingRepository _meetings = new();
    private readonly FakeUnitOfWork _unitOfWork = new();
    private readonly FakeNotesChangePublisher _publisher;
    private readonly RoomCapturingLogger<MeetingNoteService> _log = new();
    private readonly Guid _secretary = Guid.CreateVersion7();
    private readonly Guid _chair = Guid.CreateVersion7();
    private readonly Guid _meeting = Guid.CreateVersion7();

    public MeetingNoteRound1Tests() => _publisher = new(_unitOfWork);

    private MeetingNoteService Service() => new(_notes, _meetings, _publisher, _unitOfWork,
        new FixedClock(Now), _log);

    private void SeedMeeting(MeetingStatus status = MeetingStatus.InProgress)
    {
        MeetingParticipant Participant(Guid userId, MeetingRole role) => new()
        {
            MeetingId = _meeting,
            UserId = userId,
            Role = role,
            AssignmentStatus = AssignmentStatus.Current,
            Response = InvitationResponse.Accepted,
            User = new User { Id = userId, FullName = role.ToString(), Email = "u@trustcabinet.local", IsActive = true },
        };
        _meetings.Meetings.Add(new Meeting
        {
            Id = _meeting,
            Title = "Hop giao ban",
            Status = status,
            Sensitivity = Sensitivity.Normal,
            PlannedStartTime = Now.AddHours(-1),
            PlannedEndTime = Now.AddHours(1),
            CreatedAt = Now,
            UpdatedAt = Now,
            Participants = [Participant(_secretary, MeetingRole.Secretary), Participant(_chair, MeetingRole.Chair)],
        });
        _meetings.ActiveUsers.Add(_secretary);
        _meetings.ActiveUsers.Add(_chair);
    }

    [Fact(DisplayName = "MeetingNoteService.SaveAsync/UTC001: secretary creates a meeting-wide note")]
    [Trait("UTC", "MeetingNoteService.SaveAsync/UTC001")]
    [Trait("Trait", "UTC")]
    public async Task Save_NewMeetingWideNote_SavesAndPublishes()
    {
        SeedMeeting();

        var outcome = await Service().SaveAsync(_meeting, _secretary, null, "Ghi chu buoi hop", null);

        Assert.Equal(NoteFailure.None, outcome.Failure);
        Assert.Equal("Ghi chu buoi hop", outcome.Note!.Body);
        Assert.Equal(["publish:commits=1"], _publisher.Calls);
    }

    [Fact(DisplayName = "MeetingNoteService.SaveAsync/UTC002: exactly 20000 characters is kept")]
    [Trait("UTC", "MeetingNoteService.SaveAsync/UTC002")]
    [Trait("Trait", "UTC")]
    public async Task Save_ExactlyMaxLength_Succeeds()
    {
        SeedMeeting();

        var outcome = await Service().SaveAsync(_meeting, _secretary, null, new string('x', 20_000), null);

        Assert.Equal(NoteFailure.None, outcome.Failure);
        Assert.Equal(20_000, outcome.Note!.Body.Length);
        Assert.Equal(1, _unitOfWork.Saves);
    }

    [Fact(DisplayName = "MeetingNoteService.SaveAsync/UTC003: chair cannot write MSG416")]
    [Trait("UTC", "MeetingNoteService.SaveAsync/UTC003")]
    [Trait("Trait", "UTC")]
    public async Task Save_ChairWrites_IsForbidden()
    {
        SeedMeeting();

        var outcome = await Service().SaveAsync(_meeting, _chair, null, "Ghi chu", null);

        Assert.Equal(NoteFailure.Forbidden, outcome.Failure);
        Assert.Equal(0, _unitOfWork.Saves);
        Assert.Empty(_publisher.Published);
    }

    [Fact(DisplayName = "MeetingNoteService.SaveAsync/UTC004: published meeting refuses MSG417")]
    [Trait("UTC", "MeetingNoteService.SaveAsync/UTC004")]
    [Trait("Trait", "UTC")]
    public async Task Save_PublishedMeeting_IsClosed()
    {
        SeedMeeting(MeetingStatus.Published);

        var outcome = await Service().SaveAsync(_meeting, _secretary, null, "Ghi chu", null);

        Assert.Equal(NoteFailure.Closed, outcome.Failure);
        Assert.Equal(0, _unitOfWork.Saves);
        Assert.Empty(_publisher.Published);
    }

    [Fact(DisplayName = "MeetingNoteService.SaveAsync/UTC005: 20001 characters is TooLong MSG419")]
    [Trait("UTC", "MeetingNoteService.SaveAsync/UTC005")]
    [Trait("Trait", "UTC")]
    public async Task Save_OverMaxLength_IsTooLong()
    {
        SeedMeeting();

        var outcome = await Service().SaveAsync(_meeting, _secretary, null, new string('x', 20_001), null);

        Assert.Equal(NoteFailure.TooLong, outcome.Failure);
        Assert.Equal(0, _unitOfWork.Saves);
        Assert.Empty(_publisher.Published);
    }
}

public sealed class SpeakingRound1Tests
{
    private static readonly DateTimeOffset Now = new(2026, 11, 18, 3, 0, 0, TimeSpan.Zero);

    private readonly FakeSpeakingRequestRepository _requests = new();
    private readonly FakeParticipantRepository _participants = new();
    private readonly FakeUnitOfWork _unitOfWork = new();
    private readonly FakeParticipantEffects _guests;
    private readonly FakeSpeakingChangePublisher _publisher;
    private readonly RoomCapturingLogger<SpeakingService> _log = new();
    private readonly Meeting _meeting;
    private readonly Guid _chair = Guid.CreateVersion7();
    private readonly Guid _member = Guid.CreateVersion7();
    private readonly Guid _other = Guid.CreateVersion7();

    public SpeakingRound1Tests()
    {
        _guests = new(_unitOfWork);
        _publisher = new(_unitOfWork);
        _meeting = new Meeting
        {
            Title = "Hop giao ban", Status = MeetingStatus.InProgress, CreatedById = Guid.CreateVersion7(),
            PlannedStartTime = Now.AddHours(-1), PlannedEndTime = Now.AddHours(1),
        };
        foreach (var (userId, role) in new[] { (_chair, MeetingRole.Chair), (_member, MeetingRole.Member), (_other, MeetingRole.Member) })
        {
            _meeting.Participants.Add(new MeetingParticipant
            {
                MeetingId = _meeting.Id, UserId = userId, Role = role, AssignmentStatus = AssignmentStatus.Current,
                Response = InvitationResponse.Accepted,
                User = new User { Id = userId, FullName = $"{role}", Email = $"{userId}@trustcabinet.local", IsActive = true },
            });
            _requests.ActiveUsers.Add(userId);
        }
        _participants.Snapshots[_meeting.Id] = new(_meeting, []);
        _requests.InRoom.Add(_chair);
        _requests.InRoom.Add(_member);
    }

    private SpeakingService Service() => new(_requests, _participants, _guests, _publisher, _unitOfWork,
        new FixedClock(Now), _log);

    private static ParticipantActor User(Guid userId) => new(UserId: userId);

    private SpeakingRequest Request(Guid userId, SpeakingRequestStatus status, int position)
    {
        var request = new SpeakingRequest
        {
            Id = Guid.CreateVersion7(), MeetingId = _meeting.Id, UserId = userId, Status = status,
            QueuePosition = position, RequestedAt = Now.AddMinutes(-10), CreatedAt = Now.AddMinutes(-10),
            UpdatedAt = Now.AddMinutes(-10),
        };
        _requests.Requests.Add(request);
        return request;
    }

    [Fact(DisplayName = "SpeakingService.MutateAsync/UTC001: member raises a hand")]
    [Trait("UTC", "SpeakingService.MutateAsync/UTC001")]
    [Trait("Trait", "UTC")]
    public async Task Mutate_RaiseFirstHand_CreatesQueuedRequest()
    {
        var outcome = await Service().MutateAsync(_meeting.Id, null, User(_member), SpeakingOperation.Raise);

        Assert.Equal(SpeakingFailure.None, outcome.Failure);
        Assert.True(outcome.Created);
        Assert.Equal("Raise", Assert.Single(_publisher.Published).Change.ChangeType);
    }

    [Fact(DisplayName = "SpeakingService.MutateAsync/UTC002: reader out of room cannot raise MSG458")]
    [Trait("UTC", "SpeakingService.MutateAsync/UTC002")]
    [Trait("Trait", "UTC")]
    public async Task Mutate_RaiseOutsideRoom_IsNotInRoom()
    {
        var outcome = await Service().MutateAsync(_meeting.Id, null, User(_other), SpeakingOperation.Raise);

        Assert.Equal(SpeakingFailure.NotInRoom, outcome.Failure);
        Assert.Equal(0, _unitOfWork.Saves);
        Assert.Empty(_publisher.Published);
    }

    [Fact(DisplayName = "SpeakingService.MutateAsync/UTC003: member inviting is refused MSG457")]
    [Trait("UTC", "SpeakingService.MutateAsync/UTC003")]
    [Trait("Trait", "UTC")]
    public async Task Mutate_MemberInvites_IsNotChair()
    {
        var request = Request(_other, SpeakingRequestStatus.Queued, 1);

        var outcome = await Service().MutateAsync(_meeting.Id, request.Id, User(_member), SpeakingOperation.Invite);

        Assert.Equal(SpeakingFailure.NotChair, outcome.Failure);
        Assert.Equal(0, _unitOfWork.Saves);
        Assert.Empty(_publisher.Published);
    }

    [Fact(DisplayName = "SpeakingService.MutateAsync/UTC004: invite while floor held is MSG459")]
    [Trait("UTC", "SpeakingService.MutateAsync/UTC004")]
    [Trait("Trait", "UTC")]
    public async Task Mutate_InviteWhileFloorHeld_IsFloorTaken()
    {
        Request(_other, SpeakingRequestStatus.Invited, 1);
        var queued = Request(_member, SpeakingRequestStatus.Queued, 2);

        var outcome = await Service().MutateAsync(_meeting.Id, queued.Id, User(_chair), SpeakingOperation.Invite);

        Assert.Equal(SpeakingFailure.FloorTaken, outcome.Failure);
        Assert.Equal(0, _unitOfWork.Saves);
        Assert.Empty(_publisher.Published);
    }

    // ---- SpeakingMeetingEnd.PrepareAsync ----

    private SpeakingMeetingEnd Ender() => new(_requests, _unitOfWork);

    [Fact(DisplayName = "SpeakingMeetingEnd.PrepareAsync/UTC001: ending closes every open request")]
    [Trait("UTC", "SpeakingMeetingEnd.PrepareAsync/UTC001")]
    [Trait("Trait", "UTC")]
    public async Task Prepare_OpenRequests_FinishesSpeakingAndCancelsRest()
    {
        var speaking = Request(_member, SpeakingRequestStatus.Speaking, 1);
        var invited = Request(_other, SpeakingRequestStatus.Invited, 2);
        var queued = Request(_chair, SpeakingRequestStatus.Queued, 3);
        var endedAt = new DateTimeOffset(2026, 11, 10, 9, 0, 0, TimeSpan.Zero);

        await Ender().PrepareAsync(_meeting.Id, endedAt);

        Assert.Equal(SpeakingRequestStatus.Finished, speaking.Status);
        Assert.Equal(SpeakingRequestStatus.Cancelled, invited.Status);
        Assert.Equal(SpeakingRequestStatus.Cancelled, queued.Status);
    }

    [Fact(DisplayName = "SpeakingMeetingEnd.PrepareAsync/UTC002: no open request changes nothing")]
    [Trait("UTC", "SpeakingMeetingEnd.PrepareAsync/UTC002")]
    [Trait("Trait", "UTC")]
    public async Task Prepare_NoOpenRequests_ChangesNothing()
    {
        var finished = Request(_member, SpeakingRequestStatus.Finished, 1);
        var cancelled = Request(_other, SpeakingRequestStatus.Cancelled, 2);

        await Ender().PrepareAsync(_meeting.Id, new DateTimeOffset(2026, 11, 10, 9, 0, 0, TimeSpan.Zero));

        Assert.Equal(SpeakingRequestStatus.Finished, finished.Status);
        Assert.Equal(SpeakingRequestStatus.Cancelled, cancelled.Status);
        Assert.Equal([TransactionMode.None], _unitOfWork.Modes);
    }
}

internal sealed class RoomCapturingLogger<T> : ILogger<T>
{
    public List<string> Messages { get; } = [];

    public IDisposable BeginScope<TState>(TState state) where TState : notnull => new RoomNullScope();

    public bool IsEnabled(LogLevel logLevel) => true;

    public void Log<TState>(LogLevel logLevel, EventId eventId, TState state, Exception? exception,
        Func<TState, Exception?, string> formatter) => Messages.Add(formatter(state, exception));

    private sealed class RoomNullScope : IDisposable
    {
        public void Dispose() { }
    }
}
