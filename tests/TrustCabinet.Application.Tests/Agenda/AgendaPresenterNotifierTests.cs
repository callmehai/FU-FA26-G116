using TrustCabinet.Application.Abstractions;
using TrustCabinet.Application.Agenda;
using TrustCabinet.Application.Participants;
using TrustCabinet.Application.Tests.Fakes;
using TrustCabinet.Domain.Entities;
using TrustCabinet.Domain.Enums;
using TrustCabinet.Domain.Participants;

namespace TrustCabinet.Application.Tests.Agenda;

public sealed class AgendaPresenterNotifierTests
{
    private static readonly DateTimeOffset Now = new(2026, 11, 16, 8, 0, 0, TimeSpan.Zero);

    private readonly FakeAgendaRepository _agenda = new();
    private readonly FakeUnitOfWork _unitOfWork = new();
    private readonly FakeNotificationRepository _notifications = new();
    private readonly FakeEmailMessageRepository _emails = new();
    private readonly FakeServiceScopeFactory _scopes;
    private readonly Meeting _meeting = new()
    {
        Title = "Họp giao ban", Status = MeetingStatus.Published,
        PlannedStartTime = new(2026, 11, 20, 2, 0, 0, TimeSpan.Zero), PlannedEndTime = new(2026, 11, 20, 3, 0, 0, TimeSpan.Zero),
    };
    private readonly Guid _presenter = Guid.CreateVersion7();
    private readonly AgendaItem _item;

    public AgendaPresenterNotifierTests()
    {
        _scopes = new(_unitOfWork, _agenda, _notifications, _emails);
        _agenda.Meetings.Add(_meeting);
        _item = new AgendaItem { MeetingId = _meeting.Id, Title = "Báo cáo quý", Position = 1, PresenterId = _presenter };
        _agenda.Items.Add(_item);
        _agenda.Participations.Add(new MeetingParticipant
        {
            MeetingId = _meeting.Id, UserId = _presenter, Role = MeetingRole.Member, AssignmentStatus = AssignmentStatus.Current,
            User = new User { Id = _presenter, FullName = "Người trình bày", Email = "presenter@trustcabinet.local", IsActive = true },
        });
    }

    private AgendaPresenterAssignment Assignment(Guid? presenterId, Guid? guestId)
        => new(Guid.CreateVersion7(), _meeting.Id, _item.Id, presenterId, guestId, Now);

    private Task Notify(AgendaPresenterAssignment assignment)
        => new AgendaPresenterNotifier(_scopes).NotifyAsync(assignment, default);

    [Fact]
    public async Task An_internal_presenter_is_notified_in_a_transaction_of_its_own_scope()
    {
        var assignment = Assignment(_presenter, null);

        await Notify(assignment);

        var notification = Assert.Single(_notifications.Added);
        Assert.Equal((_presenter, NotificationType.AgendaPresenterAssigned, $"agenda-presenter:{assignment.EventId:D}:{_presenter:D}"),
            (notification.UserId, notification.Type, notification.DeduplicationKey));
        // 20/11/2026 02:00 UTC is 09:00 in Hanoi.
        Assert.Contains("\"Họp giao ban\" lúc 09:00 20/11/2026", notification.Body);
        Assert.Equal([_meeting.Id], _agenda.Locked);
        Assert.Equal([TransactionMode.ReadCommitted], _unitOfWork.Modes);
        Assert.Equal((1, 1), (_unitOfWork.Commits, _unitOfWork.Saves));
        Assert.Equal((1, 1), (_scopes.Created, _scopes.Disposed));
    }

    [Fact]
    public async Task A_notice_already_queued_is_not_queued_again()
    {
        var assignment = Assignment(_presenter, null);
        _notifications.Rows.Add(new Notification { Title = "Đã gửi", DeduplicationKey = $"agenda-presenter:{assignment.EventId:D}:{_presenter:D}" });

        await Notify(assignment);

        Assert.Empty(_notifications.Added);
        Assert.Equal(0, _unitOfWork.Saves);
    }

    [Fact]
    public async Task A_guest_presenter_is_emailed_once()
    {
        var guest = new MeetingGuestInvitation { MeetingId = _meeting.Id, InvitedName = "Khách", Email = "guest@example.com" };
        _agenda.Guests.Add(guest);
        _item.PresenterId = null;
        _item.PresenterGuestInvitationId = guest.Id;
        var assignment = Assignment(null, guest.Id);

        await Notify(assignment);
        _emails.Rows.AddRange(_emails.Added);
        await Notify(assignment);

        var email = Assert.Single(_emails.Added);
        Assert.Equal(("guest@example.com", $"agenda-presenter:{assignment.EventId:D}:{guest.Id:D}"), (email.ToAddress, email.DeduplicationKey));
        Assert.Empty(_notifications.Added);
        Assert.Equal(1, _unitOfWork.Saves);
    }

    [Fact]
    public async Task A_guest_presenter_gets_the_way_in_and_the_organizer_s_name()
    {
        var guest = new MeetingGuestInvitation { MeetingId = _meeting.Id, InvitedName = "Khách", Email = "guest@example.com" };
        _agenda.Guests.Add(guest);
        _item.PresenterId = null;
        _item.PresenterGuestInvitationId = guest.Id;
        var scopes = new FakeServiceScopeFactory(_unitOfWork, _agenda, _notifications, _emails, new ReadyLinks());

        await new AgendaPresenterNotifier(scopes).NotifyAsync(Assignment(null, guest.Id), default);

        var email = Assert.Single(_emails.Added);
        Assert.Contains($"Xác minh email để vào cuộc họp: https://hop.example.vn/guest/{guest.Id:D}#token=t", email.Body);
        Assert.Contains("Người tổ chức: Trần Tổ Chức", email.Body);
        Assert.Contains($"https://hop.example.vn/guest/{guest.Id:D}#token=t", email.HtmlBody);
    }

    private sealed class ReadyLinks : IGuestInvitationLinkProvider
    {
        public Task<bool> IsAvailableAsync(CancellationToken ct) => Task.FromResult(true);

        public Task<GuestInvitationLink> PrepareAsync(Guid invitationId, CancellationToken ct)
            => Task.FromResult(new GuestInvitationLink(GuestLinkStatus.Ready, $"https://hop.example.vn/guest/{invitationId:D}#token=t"));
    }

    [Fact]
    public async Task A_presenter_replaced_since_the_assignment_is_not_notified()
    {
        _item.PresenterId = Guid.CreateVersion7();

        await Notify(Assignment(_presenter, null));

        Assert.Empty(_notifications.Added);
        Assert.Equal(0, _unitOfWork.Saves);
    }

    [Fact]
    public async Task A_store_failure_reaches_the_caller_and_writes_nothing()
    {
        _unitOfWork.FailNextSave = new PersistenceUnavailableException();

        await Assert.ThrowsAsync<PersistenceUnavailableException>(() => Notify(Assignment(_presenter, null)));

        Assert.Equal((0, 1), (_unitOfWork.Commits, _unitOfWork.Rollbacks));
        Assert.Equal(1, _scopes.Disposed);
    }
}
