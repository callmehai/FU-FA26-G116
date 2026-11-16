using System.Text.Json;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using TrustCabinet.Application.Abstractions;
using TrustCabinet.Application.Email;
using TrustCabinet.Application.Participants;
using TrustCabinet.Application.Tests.Fakes;
using TrustCabinet.Domain.Entities;
using TrustCabinet.Domain.Enums;

namespace TrustCabinet.Application.Tests.Email;

public sealed class MeetingInvitationDispatchProcessorTests
{
    private static readonly DateTimeOffset Now = new(2026, 11, 16, 9, 0, 0, TimeSpan.Zero);

    private readonly FakeMeetingInvitationDispatchRepository _dispatches = new();
    private readonly FakeEmailMessageRepository _emails = new();
    private readonly FakeUnitOfWork _unitOfWork = new();
    private readonly Links _links = new();

    private MeetingInvitationDispatchProcessor Processor() => new(
        _dispatches, _emails, _unitOfWork,
        Options.Create(new EmailDeliveryOptions
        {
            ApplicationBaseUrl = "https://hop.example.vn",
            MaxAttempts = 5,
            Smtp = new() { Enabled = true, FromAddress = "no-reply@example.vn" },
        }),
        new FixedClock(Now), _links, NullLogger<MeetingInvitationDispatchProcessor>.Instance);

    private MeetingInvitationDispatch Dispatch(InvitationEventKind kind = InvitationEventKind.Invitation, bool guest = false)
    {
        var dispatch = new MeetingInvitationDispatch
        {
            MeetingId = Guid.NewGuid(),
            ParticipantId = guest ? null : Guid.NewGuid(),
            GuestInvitationId = guest ? Guid.NewGuid() : null,
            EventKind = kind,
            DeduplicationKey = $"roster:{Guid.NewGuid()}",
            PayloadJson = JsonSerializer.Serialize(new InvitationPayload("Nguyễn Văn A", "a@example.vn", "Họp giao ban",
                "Mô tả", Now.AddDays(1), Now.AddDays(1).AddHours(1), "Phòng 1", "InPerson", "Member")),
        };
        _dispatches.Rows.Add(dispatch);
        _dispatches.Locked = dispatch;
        return dispatch;
    }

    [Fact]
    public async Task An_invitation_becomes_an_email_with_a_calendar_entry_under_the_locks()
    {
        var dispatch = Dispatch();

        Assert.Equal(1, await Processor().ProcessAsync(dispatch.Id, default));

        var email = Assert.Single(_emails.Added);
        Assert.Equal("a@example.vn", email.ToAddress);
        Assert.Equal(dispatch.DeduplicationKey, email.DeduplicationKey);
        Assert.Contains($"https://hop.example.vn/mgmt/meetings/{dispatch.MeetingId}", email.Body, StringComparison.Ordinal);
        Assert.NotNull(email.CalendarAttachment);
        Assert.Equal(InvitationDispatchState.Materialized, dispatch.State);
        Assert.Equal(email.Id, dispatch.EmailMessageId);
        Assert.Equal([TransactionMode.None, TransactionMode.ReadCommitted], _unitOfWork.Modes);
        Assert.Equal((1, 1, 0), (_unitOfWork.Saves, _unitOfWork.Commits, _unitOfWork.Rollbacks));
    }

    [Fact]
    public async Task An_invitation_to_a_meeting_that_has_ended_is_suppressed()
    {
        var dispatch = Dispatch();
        _dispatches.MeetingStatus = MeetingStatus.Ended;

        Assert.Equal(0, await Processor().ProcessAsync(dispatch.Id, default));

        Assert.Empty(_emails.Added);
        Assert.Equal(InvitationDispatchState.Suppressed, dispatch.State);
        Assert.Equal((1, 1), (_unitOfWork.Saves, _unitOfWork.Commits));
    }

    [Fact]
    public async Task A_withdrawal_of_an_invitation_that_never_went_out_is_suppressed()
    {
        var dispatch = Dispatch(InvitationEventKind.Removed);

        Assert.Equal(0, await Processor().ProcessAsync(dispatch.Id, default));

        Assert.Empty(_emails.Added);
        Assert.Equal(InvitationDispatchState.Suppressed, dispatch.State);
    }

    [Fact]
    public async Task A_dispatch_another_worker_holds_is_left_alone_and_its_transaction_not_committed()
    {
        var dispatch = Dispatch();
        _dispatches.Locked = null;

        Assert.Equal(0, await Processor().ProcessAsync(dispatch.Id, default));

        Assert.Empty(_emails.Added);
        Assert.Equal((0, 0, 1), (_unitOfWork.Saves, _unitOfWork.Commits, _unitOfWork.Rollbacks));
        Assert.Empty(_dispatches.Writes);
    }

    [Fact]
    public async Task A_guest_invitation_waits_while_the_link_provider_is_unavailable()
    {
        var dispatch = Dispatch(guest: true);
        _links.Available = false;

        Assert.Equal(0, await Processor().ProcessAsync(dispatch.Id, default));

        Assert.Equal(["BlockOnDependencyAsync"], _dispatches.Writes);
        Assert.Equal([TransactionMode.None, TransactionMode.None], _unitOfWork.Modes);
        Assert.Empty(_emails.Added);
    }

    [Fact]
    public async Task A_guest_invitation_revoked_while_the_provider_is_unavailable_is_suppressed()
    {
        var dispatch = Dispatch(guest: true);
        _links.Available = false;
        _dispatches.GuestActive = false;

        Assert.Equal(0, await Processor().ProcessAsync(dispatch.Id, default));

        Assert.Equal(["SuppressAsync"], _dispatches.Writes);
    }

    [Fact]
    public async Task A_storage_failure_rolls_back_and_counts_the_attempt_under_its_own_exception_type()
    {
        var dispatch = Dispatch();
        _dispatches.FailStatus = new PersistenceUnavailableException(new TimeoutException("db"));

        Assert.Equal(0, await Processor().ProcessAsync(dispatch.Id, default));

        Assert.Empty(_emails.Added);
        Assert.Equal((0, 1, 1), (_unitOfWork.Commits, _unitOfWork.Rollbacks, _unitOfWork.Discards));
        Assert.Equal((dispatch.Id, 5, "TimeoutException"), _dispatches.FailedAttempt);
        Assert.Equal(TransactionMode.None, _unitOfWork.Modes[^1]);
    }

    [Fact]
    public async Task A_payload_it_cannot_read_counts_a_failed_attempt()
    {
        var dispatch = Dispatch();
        dispatch.PayloadVersion = 2;

        Assert.Equal(0, await Processor().ProcessAsync(dispatch.Id, default));

        Assert.Equal("JsonException", _dispatches.FailedAttempt?.Error);
        Assert.Empty(_emails.Added);
    }

    private sealed class Links : IGuestInvitationLinkProvider
    {
        public bool Available { get; set; } = true;

        public Task<bool> IsAvailableAsync(CancellationToken ct) => Task.FromResult(Available);

        public Task<GuestInvitationLink> PrepareAsync(Guid invitationId, CancellationToken ct)
            => Task.FromResult(new GuestInvitationLink(GuestLinkStatus.Ready, "https://hop.example.vn/guest/verify"));
    }
}
