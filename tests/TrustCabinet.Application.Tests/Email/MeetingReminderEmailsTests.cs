using Microsoft.Extensions.Options;
using TrustCabinet.Application.Abstractions;
using TrustCabinet.Application.Email;
using TrustCabinet.Application.Participants;
using TrustCabinet.Application.Tests.Fakes;
using TrustCabinet.Domain.Entities;
using TrustCabinet.Domain.Enums;

namespace TrustCabinet.Application.Tests.Email;

/// <summary>The reminder email 15 minutes before a meeting (PO 15/11/2026), over its query port.</summary>
public sealed class MeetingReminderEmailsTests
{
    private static readonly DateTimeOffset Now = new(2026, 11, 16, 1, 50, 0, TimeSpan.Zero);

    private readonly FakeUnitOfWork _unitOfWork = new();
    private readonly FakeReminderQuery _query = new();
    private readonly FakeEmailMessageRepository _emails = new();
    private readonly FakeLinks _links = new();

    private MeetingReminderEmails Service() => new(_unitOfWork, _query, _emails, _links,
        Options.Create(new EmailDeliveryOptions { ApplicationBaseUrl = "https://hop.example.vn/" }), new FixedClock(Now));

    [Fact]
    public async Task Every_active_invitee_and_guest_with_a_link_is_reminded_once_and_saved_together()
    {
        var meeting = GivenMeeting();
        var chair = Participant(meeting, MeetingRole.Chair, "Nguyễn Chủ Toạ");
        var guest = Guest(meeting);

        var queued = await Service().QueueDueAsync();

        Assert.Equal(2, queued);
        Assert.Equal(1, _unitOfWork.Saves);
        Assert.Equal([TransactionMode.None], _unitOfWork.Modes);
        var key = $"{MeetingReminderEmails.KeyPrefix}{meeting.Id:D}:{meeting.PlannedStartTime.ToUnixTimeSeconds()}";
        var staff = Assert.Single(_emails.Added, e => e.ToAddress == chair.Email);
        Assert.Equal($"{key}:{chair.Id:D}", staff.DeduplicationKey);
        Assert.Contains($"https://hop.example.vn/mgmt/meetings/{meeting.Id:D}/room", staff.Body);
        Assert.Contains("organizer@example.vn", staff.Body);
        var forGuest = Assert.Single(_emails.Added, e => e.ToAddress == guest.Email);
        Assert.Contains($"/guest/{guest.Id:D}", forGuest.Body);
        // A Guest is told whom to ask, not given a staff member's address.
        Assert.DoesNotContain("organizer@example.vn", forGuest.Body);
        Assert.All(_emails.Added, e => Assert.StartsWith("Cuộc họp sắp bắt đầu: ", e.Subject));
    }

    [Fact]
    public async Task Someone_invited_inside_the_window_or_already_reminded_is_not_emailed_again()
    {
        var meeting = GivenMeeting();
        var late = Participant(meeting, MeetingRole.Member, "Mời muộn");
        var early = Participant(meeting, MeetingRole.Member, "Mời sớm");
        var reminded = Participant(meeting, MeetingRole.Member, "Đã nhắc");
        _query.Invitations.Add(new ReminderInvitation(meeting.Id, late.Id, meeting.PlannedStartTime.AddMinutes(-10)));
        _query.Invitations.Add(new ReminderInvitation(meeting.Id, early.Id, meeting.PlannedStartTime.AddDays(-1)));
        _query.Keys.Add($"{MeetingReminderEmails.KeyPrefix}{meeting.Id:D}:{meeting.PlannedStartTime.ToUnixTimeSeconds()}:{reminded.Id:D}");

        var queued = await Service().QueueDueAsync();

        Assert.Equal(1, queued);
        Assert.Equal(early.Email, Assert.Single(_emails.Added).ToAddress);
    }

    [Fact]
    public async Task A_guest_without_a_ready_link_gets_no_email_and_nothing_queued_saves_nothing()
    {
        var meeting = GivenMeeting();
        Guest(meeting);
        _links.Ready = false;

        Assert.Equal(0, await Service().QueueDueAsync());
        Assert.Empty(_emails.Added);
        Assert.Equal(0, _unitOfWork.Saves);
    }

    [Fact]
    public async Task No_meeting_about_to_start_reads_nothing_more()
    {
        Assert.Equal(0, await Service().QueueDueAsync());
        Assert.Equal(["starting"], _query.Calls);
    }

    [Fact]
    public async Task A_storage_failure_reaches_the_worker()
    {
        GivenMeeting();
        var failure = new PersistenceUnavailableException();
        _query.Fail = failure;

        Assert.Same(failure, await Assert.ThrowsAsync<PersistenceUnavailableException>(() => Service().QueueDueAsync()));
        Assert.Empty(_emails.Added);
    }

    private Meeting GivenMeeting()
    {
        var meeting = new Meeting
        {
            Title = "Họp giao ban", Status = MeetingStatus.Published, Mode = MeetingMode.Online,
            PlannedStartTime = Now.AddMinutes(10), PlannedEndTime = Now.AddMinutes(70), CreatedById = Guid.CreateVersion7(),
        };
        _query.Meetings.Add(new ReminderMeeting(meeting, "Trần Tổ Chức", "organizer@example.vn"));
        return meeting;
    }

    private ReminderParticipant Participant(Meeting meeting, MeetingRole role, string name)
    {
        var participant = new ReminderParticipant(Guid.CreateVersion7(), meeting.Id, role, name, $"{Guid.NewGuid():N}@example.vn");
        _query.Participants.Add(participant);
        return participant;
    }

    private ReminderGuest Guest(Meeting meeting)
    {
        var guest = new ReminderGuest(Guid.CreateVersion7(), meeting.Id, "Khách mời", "guest@example.vn");
        _query.Guests.Add(guest);
        return guest;
    }

    private sealed class FakeReminderQuery : IMeetingReminderQuery
    {
        public List<ReminderMeeting> Meetings { get; } = [];
        public List<ReminderParticipant> Participants { get; } = [];
        public List<ReminderGuest> Guests { get; } = [];
        public List<ReminderInvitation> Invitations { get; } = [];
        public List<string> Keys { get; } = [];
        public List<string> Calls { get; } = [];
        public Exception? Fail { get; set; }

        public Task<IReadOnlyList<ReminderMeeting>> ListStartingAsync(DateTimeOffset now, DateTimeOffset cutoff,
            CancellationToken ct = default)
            => Answer("starting", Meetings.Where(m => m.Meeting.PlannedStartTime > now && m.Meeting.PlannedStartTime <= cutoff));

        public Task<IReadOnlyList<ReminderParticipant>> ListParticipantsAsync(Guid[] meetingIds, CancellationToken ct = default)
            => Answer("participants", Participants.Where(p => meetingIds.Contains(p.MeetingId)));

        public Task<IReadOnlyList<ReminderGuest>> ListGuestsAsync(Guid[] meetingIds, CancellationToken ct = default)
            => Answer("guests", Guests.Where(g => meetingIds.Contains(g.MeetingId)));

        public Task<IReadOnlyList<ReminderInvitation>> ListInvitationsAsync(Guid[] meetingIds, CancellationToken ct = default)
            => Answer("invitations", Invitations.Where(i => meetingIds.Contains(i.MeetingId)));

        public Task<IReadOnlyList<string>> ListReminderKeysAsync(Guid[] meetingIds, CancellationToken ct = default)
            => Answer("keys", Keys);

        private Task<IReadOnlyList<T>> Answer<T>(string call, IEnumerable<T> rows)
        {
            if (Fail is { } failure) return Task.FromException<IReadOnlyList<T>>(failure);
            Calls.Add(call);
            return Task.FromResult<IReadOnlyList<T>>(rows.ToList());
        }
    }

    private sealed class FakeLinks : IGuestInvitationLinkProvider
    {
        public bool Ready { get; set; } = true;

        public Task<bool> IsAvailableAsync(CancellationToken ct) => Task.FromResult(Ready);

        public Task<GuestInvitationLink> PrepareAsync(Guid invitationId, CancellationToken ct)
            => Task.FromResult(Ready
                ? new GuestInvitationLink(GuestLinkStatus.Ready, $"https://hop.example.vn/guest/{invitationId:D}#token=t")
                : new GuestInvitationLink(GuestLinkStatus.Unavailable));
    }
}
