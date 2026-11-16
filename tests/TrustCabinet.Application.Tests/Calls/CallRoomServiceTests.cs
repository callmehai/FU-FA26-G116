using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using TrustCabinet.Application.Abstractions;
using TrustCabinet.Application.Calls;
using TrustCabinet.Application.Tests.Fakes;
using TrustCabinet.Domain.Audit;
using TrustCabinet.Domain.Calls;
using TrustCabinet.Domain.Enums;
using TrustCabinet.Domain.Meetings;

namespace TrustCabinet.Application.Tests.Calls;

/// <summary>
/// SCRUM-178 (CA-13): the call room over the unit of work. Each repository step runs in <c>ExecuteAsync(None)</c>,
/// so a storage failure is caught as <see cref="PersistenceException"/>; a LiveKit failure never is.
/// </summary>
public sealed class CallRoomServiceTests
{
    private static readonly DateTimeOffset Now = new(2026, 11, 16, 8, 0, 0, TimeSpan.Zero);

    private readonly FakeCallRepository _calls = new();
    private readonly FakeUnitOfWork _unitOfWork = new();
    private readonly FakeLiveKitGateway _liveKit = new();
    private readonly FakeAuditService _audit = new();
    private readonly FakeSpeakingChangePublisher _speaking;
    private readonly Guid _meeting = Guid.CreateVersion7();
    private readonly Guid _chair = Guid.CreateVersion7();
    private readonly Guid _member = Guid.CreateVersion7();

    public CallRoomServiceTests()
    {
        _speaking = new(_unitOfWork);
        _calls.Access[_chair] = new(_meeting, MeetingStatus.InProgress, MeetingMode.Online, MeetingRole.Chair, "Chủ trì");
        _calls.Access[_member] = new(_meeting, MeetingStatus.InProgress, MeetingMode.Online, MeetingRole.Member, "Thành viên");
    }

    private CallRoomService Service() => new(_calls, _unitOfWork, _liveKit,
        Options.Create(new LiveKitOptions { TokenTtlMinutes = 10 }), _audit, NullLogger<CallRoomService>.Instance,
        timeProvider: new FixedClock(Now), speaking: _speaking);

    private string Room => CallRoomNames.ForMeeting(_meeting);

    [Fact]
    public async Task Joining_reads_access_without_a_transaction_then_issues_a_ticket()
    {
        var result = await Service().JoinAsync(_meeting, _member);

        Assert.Equal(CallOutcome.Success, result.Outcome);
        Assert.Equal([TransactionMode.None], _unitOfWork.Modes);
        Assert.Equal(["find-access"], _calls.Calls);
        Assert.Equal([$"ticket:{_member:D}"], _liveKit.Calls);
    }

    [Fact]
    public async Task Only_the_chair_mutes()
    {
        Assert.Equal(CallOutcome.NotChair, await Service().MuteAsync(_meeting, _member, CallMember.User(_chair)));
        Assert.Empty(_liveKit.Calls);
    }

    [Fact]
    public async Task A_storage_failure_reading_access_is_read_failed_and_livekit_is_not_asked()
    {
        _calls.Fail = new PersistenceUnavailableException();

        Assert.Equal(CallOutcome.ReadFailed, (await Service().GetRoomAsync(_meeting, _member)).Outcome);
        Assert.Equal(CallOutcome.ReadFailed, (await Service().JoinAsync(_meeting, _member)).Outcome);
        Assert.Equal(CallOutcome.ReadFailed, await Service().MuteAsync(_meeting, _chair, CallMember.User(_member)));
        Assert.Empty(_liveKit.Calls);
    }

    [Fact]
    public async Task A_guest_whose_admission_cannot_be_recorded_gets_no_ticket()
    {
        var guest = new MeetingGuestSession(_meeting, Guid.CreateVersion7(), Guid.CreateVersion7());
        _calls.Access[guest.SessionId] = new(_meeting, MeetingStatus.InProgress, MeetingMode.Online, MeetingRole.Guest, "Khách mời");
        _audit.Fail = new PersistenceUnavailableException();

        var result = await Service().JoinAsync(_meeting, new CallCaller(Guest: guest), recordingNoticeAcknowledged: true);

        Assert.Equal(CallOutcome.ReadFailed, result.Outcome);
        Assert.Empty(_liveKit.Calls);
    }

    [Fact]
    public async Task Admitting_a_guest_is_audited_before_the_ticket()
    {
        var guest = new MeetingGuestSession(_meeting, Guid.CreateVersion7(), Guid.CreateVersion7());
        _calls.Access[guest.SessionId] = new(_meeting, MeetingStatus.InProgress, MeetingMode.Online, MeetingRole.Guest, "Khách mời");

        var result = await Service().JoinAsync(_meeting, new CallCaller(Guest: guest), recordingNoticeAcknowledged: true);

        Assert.Equal(CallOutcome.Success, result.Outcome);
        Assert.Equal([AuditActions.CallGuestAdmitted], _audit.Actions);
    }

    [Fact]
    public async Task A_livekit_failure_while_muting_is_provider_failed()
    {
        _liveKit.Unavailable = new LiveKitUnavailableException("down");

        Assert.Equal(CallOutcome.ProviderFailed, await Service().MuteAsync(_meeting, _chair, CallMember.User(_member)));
    }

    [Fact]
    public async Task Leaving_with_a_raised_hand_tells_the_speaking_panels()
    {
        _calls.ActiveSession = new() { MeetingId = _meeting, Kind = CallKind.MeetingRoom, Status = CallStatus.Active, StartedAt = Now };
        _calls.ClosedHands = 1;

        await Service().HandleWebhookAsync(new CallWebhookEvent("e1", "participant_left", Room, _member.ToString("D"), null,
            null, CallTrackSource.Unknown, Now));

        Assert.Equal(["find-session", "leave"], _calls.Calls);
        Assert.Equal([TransactionMode.None, TransactionMode.None], _unitOfWork.Modes);
        Assert.Equal(("Left", _meeting), (Assert.Single(_speaking.Published).Change.ChangeType, _speaking.Published[0].Change.MeetingId));
    }

    [Fact]
    public async Task Leaving_with_no_hand_up_sends_nothing()
    {
        _calls.ActiveSession = new() { MeetingId = _meeting, Kind = CallKind.MeetingRoom, Status = CallStatus.Active, StartedAt = Now };

        await Service().HandleWebhookAsync(new CallWebhookEvent("e1", "participant_left", Room, _member.ToString("D"), null,
            null, CallTrackSource.Unknown, Now));

        Assert.Empty(_speaking.Published);
    }

    [Fact]
    public async Task A_storage_failure_in_a_webhook_reaches_the_caller()
    {
        _calls.Fail = new PersistenceUnavailableException();

        // The webhook endpoint answers 500 so LiveKit retries, and the janitor catches up.
        await Assert.ThrowsAsync<PersistenceUnavailableException>(() => Service().HandleWebhookAsync(
            new CallWebhookEvent("e1", "room_finished", Room, null, null, null, CallTrackSource.Unknown, Now)));
    }
}
