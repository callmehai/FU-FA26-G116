using System.Text.Json;
using TrustCabinet.Application.Email;
using TrustCabinet.Application.Tests.Fakes;
using TrustCabinet.Domain.Entities;
using TrustCabinet.Domain.Enums;

namespace TrustCabinet.Application.Tests.Email;

public sealed class MeetingInvitationEventsTests
{
    private static readonly DateTimeOffset Now = new(2026, 11, 16, 9, 0, 0, TimeSpan.Zero);

    private readonly FakeMeetingInvitationDispatchRepository _dispatches = new();
    private readonly FakeNotificationRepository _notifications = new();
    private readonly Meeting _meeting = new()
    {
        Title = "Họp giao ban",
        Mode = MeetingMode.InPerson,
        Location = "Phòng 1",
        PlannedStartTime = Now.AddDays(1),
        PlannedEndTime = Now.AddDays(1).AddHours(1),
    };

    private MeetingInvitationEvents Events() => new(_dispatches, _notifications);

    private MeetingParticipant Participant(MeetingRole role = MeetingRole.Member)
    {
        var user = new User { FullName = "Đại biểu", Email = $"{Guid.NewGuid():N}@example.vn", IsActive = true };
        var participant = new MeetingParticipant { MeetingId = _meeting.Id, UserId = user.Id, User = user, Role = role };
        _meeting.Participants.Add(participant);
        return participant;
    }

    private MeetingGuestInvitation Guest(bool revoked = false)
    {
        var guest = new MeetingGuestInvitation
        {
            MeetingId = _meeting.Id,
            InvitedName = "Khách",
            Email = $"{Guid.NewGuid():N}@guests.test",
            RevokedAt = revoked ? Now : null,
        };
        _dispatches.Guests.Add(guest);
        return guest;
    }

    [Fact]
    public async Task Publication_invites_every_participant_and_every_guest_whose_invitation_stands()
    {
        var member = Participant();
        var guest = Guest();
        Guest(revoked: true);

        await Events().StagePublicationInvitationsAsync(_meeting, Now);

        Assert.Equal([member.Id, null], _dispatches.Added.Select(d => d.ParticipantId));
        Assert.Equal([null, guest.Id], _dispatches.Added.Select(d => d.GuestInvitationId));
        Assert.All(_dispatches.Added, d => Assert.Equal(InvitationEventKind.Invitation, d.EventKind));
        Assert.Equal(MeetingInvitationEvents.InitialKey(member), _dispatches.Added[0].DeduplicationKey);
        // The service sends the bell for a publication itself.
        Assert.Empty(_notifications.Added);
    }

    [Fact]
    public async Task Cancellation_emails_everyone_with_the_reason_and_rings_everyone_but_the_organizer()
    {
        var organizer = Participant(MeetingRole.Chair);
        var member = Participant();
        Guest();
        var historyId = Guid.NewGuid();

        await Events().StageCancellationAsync(_meeting, historyId, organizer.UserId, "Lãnh đạo đi công tác", Now);

        Assert.Equal(3, _dispatches.Added.Count);
        Assert.All(_dispatches.Added, d =>
        {
            Assert.Equal(InvitationEventKind.Cancelled, d.EventKind);
            Assert.Equal(historyId, d.EventId);
            Assert.Equal("Lãnh đạo đi công tác", JsonSerializer.Deserialize<InvitationPayload>(d.PayloadJson)!.Reason);
        });
        var notice = Assert.Single(_notifications.Added);
        Assert.Equal(member.UserId, notice.UserId);
        Assert.Equal(NotificationType.MeetingCancelled, notice.Type);
        Assert.Contains("Lãnh đạo đi công tác", notice.Body, StringComparison.Ordinal);
    }

    [Fact]
    public async Task A_new_time_is_emailed_to_every_invitee_and_guest_with_the_old_time()
    {
        // PO 15/11/2026: internal invitees get the email too, not only the bell (which the caller adds).
        var member = Participant();
        var guest = Guest();
        var historyId = Guid.NewGuid();
        var previous = (Now.AddDays(-1), Now.AddDays(-1).AddHours(1));

        await Events().StageRescheduleInvitationsAsync(_meeting, historyId, previous, Now);

        Assert.Equal(2, _dispatches.Added.Count);
        Assert.Contains(_dispatches.Added, d => d.ParticipantId == member.Id);
        Assert.Contains(_dispatches.Added, d => d.GuestInvitationId == guest.Id);
        Assert.All(_dispatches.Added, d =>
        {
            Assert.Equal(InvitationEventKind.Rescheduled, d.EventKind);
            Assert.Equal(historyId, d.EventId);
            var payload = JsonSerializer.Deserialize<InvitationPayload>(d.PayloadJson)!;
            Assert.Equal(previous.Item1, payload.PreviousStartsAt);
            Assert.Equal(previous.Item2, payload.PreviousEndsAt);
        });
        Assert.Empty(_notifications.Added);
    }

    [Fact]
    public void A_role_change_tells_the_participant_their_new_role()
    {
        var secretary = Participant(MeetingRole.Secretary);

        Events().Stage(_meeting, secretary, null, InvitationEventKind.RoleChanged, Now, notify: true);

        var dispatch = Assert.Single(_dispatches.Added);
        var notice = Assert.Single(_notifications.Added);
        Assert.Equal("inapp:" + dispatch.DeduplicationKey, notice.DeduplicationKey);
        Assert.Equal(NotificationType.MeetingRoleChanged, notice.Type);
        Assert.Contains("Thư ký", notice.Body, StringComparison.Ordinal);
    }
}
