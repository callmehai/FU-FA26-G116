using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using TrustCabinet.Application.Email;
using TrustCabinet.Application.Participants;
using TrustCabinet.Domain.Entities;
using TrustCabinet.Domain.Enums;
using TrustCabinet.Domain.Participants;
using TrustCabinet.Infrastructure;
using TrustCabinet.Infrastructure.Email;
using TrustCabinet.Infrastructure.Persistence;

namespace TrustCabinet.Web.Tests.Email;

/// <summary>The reminder email beside the in-app reminder (PO 15/11/2026).</summary>
public sealed class MeetingReminderEmailsTests
{
    private static readonly DateTimeOffset Now = new(2026, 11, 16, 1, 50, 0, TimeSpan.Zero);

    [Fact]
    public async Task Every_active_invitee_and_current_guest_gets_one_email_with_a_way_in()
    {
        await using var db = NewDb();
        var organizer = NewUser("thu@example.vn", "Lê Thị Thu");
        var chair = NewUser("binh@example.vn", "Trần Văn Bình");
        var member = NewUser("an@example.vn", "Nguyễn Văn An");
        var gone = NewUser("gone@example.vn", "Đã nghỉ");
        gone.IsActive = false;
        var meeting = NewMeeting(organizer, Now.AddMinutes(10));
        var later = NewMeeting(organizer, Now.AddMinutes(40));
        var draft = NewMeeting(organizer, Now.AddMinutes(5));
        draft.Status = MeetingStatus.Draft;
        db.Users.AddRange(organizer, chair, member, gone);
        db.Meetings.AddRange(meeting, later, draft);
        db.MeetingParticipants.AddRange(
            new MeetingParticipant { MeetingId = meeting.Id, UserId = chair.Id, Role = MeetingRole.Chair },
            new MeetingParticipant { MeetingId = meeting.Id, UserId = member.Id, Role = MeetingRole.Member },
            new MeetingParticipant { MeetingId = meeting.Id, UserId = gone.Id, Role = MeetingRole.Member },
            new MeetingParticipant { MeetingId = later.Id, UserId = member.Id, Role = MeetingRole.Member },
            new MeetingParticipant { MeetingId = draft.Id, UserId = member.Id, Role = MeetingRole.Member });
        var guest = new MeetingGuestInvitation { MeetingId = meeting.Id, InvitedName = "Khách Hoa", Email = "hoa@guest.vn", InvitedById = organizer.Id };
        var revoked = new MeetingGuestInvitation
        {
            MeetingId = meeting.Id, InvitedName = "Khách cũ", Email = "cu@guest.vn", InvitedById = organizer.Id, RevokedAt = Now,
        };
        db.MeetingGuestInvitations.AddRange(guest, revoked);
        await db.SaveChangesAsync();
        var reminders = NewService(db);

        Assert.Equal(3, await reminders.QueueDueAsync());
        Assert.Equal(0, await reminders.QueueDueAsync());

        var emails = await db.EmailMessages.ToListAsync();
        Assert.Equal(["an@example.vn", "binh@example.vn", "hoa@guest.vn"], emails.Select(e => e.ToAddress).Order());
        Assert.All(emails, e => Assert.StartsWith("Cuộc họp sắp bắt đầu: Họp giao ban", e.Subject));
        var staff = emails.Single(e => e.ToAddress == "an@example.vn");
        // An online meeting in TrustCabinet's own room: straight into the room.
        Assert.Contains($"href=\"https://hop.example.vn/mgmt/meetings/{meeting.Id:D}/room\"", staff.HtmlBody);
        Assert.Contains("Trần Văn Bình", staff.HtmlBody);
        Assert.Contains("Lê Thị Thu (thu@example.vn)", staff.HtmlBody);
        var forGuest = emails.Single(e => e.ToAddress == "hoa@guest.vn");
        Assert.Contains($"href=\"https://hop.example.vn/guest/{guest.Id:D}#token=t\"", forGuest.HtmlBody);
        Assert.DoesNotContain("/mgmt/meetings", forGuest.HtmlBody);
        Assert.DoesNotContain("thu@example.vn", forGuest.HtmlBody);
        Assert.Contains($"/guest/{guest.Id:D}#token=t", forGuest.Body);

        // Moved to another time inside the window: reminded again for the new start.
        meeting.PlannedStartTime = Now.AddMinutes(12);
        await db.SaveChangesAsync();
        Assert.Equal(3, await reminders.QueueDueAsync());
    }

    [Fact]
    public async Task A_meeting_held_elsewhere_links_its_page_not_the_room()
    {
        await using var db = NewDb();
        var organizer = NewUser("thu@example.vn", "Lê Thị Thu");
        var member = NewUser("an@example.vn", "Nguyễn Văn An");
        var meeting = NewMeeting(organizer, Now.AddMinutes(10));
        meeting.Mode = MeetingMode.InPerson;
        meeting.Location = "Phòng họp A";
        db.Users.AddRange(organizer, member);
        db.Meetings.Add(meeting);
        db.MeetingParticipants.Add(new MeetingParticipant { MeetingId = meeting.Id, UserId = member.Id, Role = MeetingRole.Member });
        await db.SaveChangesAsync();

        Assert.Equal(1, await NewService(db).QueueDueAsync());

        var email = await db.EmailMessages.SingleAsync();
        Assert.Contains($"href=\"https://hop.example.vn/mgmt/meetings/{meeting.Id:D}\"", email.HtmlBody);
        Assert.DoesNotContain("/room", email.HtmlBody);
        Assert.Contains("Phòng họp A", email.HtmlBody);
    }

    [Fact]
    public async Task Someone_invited_inside_the_window_is_not_emailed_twice()
    {
        await using var db = NewDb();
        var organizer = NewUser("thu@example.vn", "Lê Thị Thu");
        var early = NewUser("an@example.vn", "Nguyễn Văn An");
        var late = NewUser("binh@example.vn", "Trần Văn Bình");
        var meeting = NewMeeting(organizer, Now.AddMinutes(10));
        db.Users.AddRange(organizer, early, late);
        db.Meetings.Add(meeting);
        var invitedEarly = new MeetingParticipant { MeetingId = meeting.Id, UserId = early.Id, Role = MeetingRole.Member };
        var invitedLate = new MeetingParticipant { MeetingId = meeting.Id, UserId = late.Id, Role = MeetingRole.Member };
        var guest = new MeetingGuestInvitation { MeetingId = meeting.Id, InvitedName = "Khách Hoa", Email = "hoa@guest.vn", InvitedById = organizer.Id };
        db.MeetingParticipants.AddRange(invitedEarly, invitedLate);
        db.MeetingGuestInvitations.Add(guest);
        // An hour ago for one; two minutes ago — inside the 15 minutes before the start — for the other and the Guest.
        db.MeetingInvitationDispatches.AddRange(
            Invited(meeting, invitedEarly.Id, null, Now.AddHours(-1)),
            Invited(meeting, invitedLate.Id, null, Now.AddMinutes(-2)),
            Invited(meeting, null, guest.Id, Now.AddMinutes(-2)));
        await db.SaveChangesAsync();

        Assert.Equal(1, await NewService(db).QueueDueAsync());

        Assert.Equal("an@example.vn", (await db.EmailMessages.SingleAsync()).ToAddress);
    }

    private static MeetingInvitationDispatch Invited(Meeting meeting, Guid? participant, Guid? guest, DateTimeOffset at) => new()
    {
        EventId = Guid.CreateVersion7(), MeetingId = meeting.Id, ParticipantId = participant, GuestInvitationId = guest,
        EventKind = InvitationEventKind.Invitation, DeduplicationKey = $"test:{Guid.CreateVersion7():D}", PayloadJson = "{}",
        CreatedAt = at, UpdatedAt = at,
    };

    private static MeetingReminderEmails NewService(TrustCabinetDbContext db)
        => new(new EfUnitOfWork(db), new MeetingReminderQuery(db), new EmailMessageRepository(db), new Links(),
            Options.Create(new EmailDeliveryOptions { ApplicationBaseUrl = "https://hop.example.vn" }), new Clock());

    private static TrustCabinetDbContext NewDb()
        => new(new DbContextOptionsBuilder<TrustCabinetDbContext>().UseInMemoryDatabase(Guid.NewGuid().ToString()).Options);

    private static User NewUser(string email, string name) => new() { FullName = name, Email = email, PasswordHash = "hash" };

    private static Meeting NewMeeting(User organizer, DateTimeOffset start) => new()
    {
        Title = "Họp giao ban",
        Status = MeetingStatus.Published,
        Sensitivity = Sensitivity.Internal,
        Mode = MeetingMode.Online,
        PlannedStartTime = start,
        PlannedEndTime = start.AddHours(1),
        CreatedById = organizer.Id,
    };

    private sealed class Links : IGuestInvitationLinkProvider
    {
        public Task<bool> IsAvailableAsync(CancellationToken ct) => Task.FromResult(true);

        public Task<GuestInvitationLink> PrepareAsync(Guid invitationId, CancellationToken ct)
            => Task.FromResult(new GuestInvitationLink(GuestLinkStatus.Ready, $"https://hop.example.vn/guest/{invitationId:D}#token=t"));
    }

    private sealed class Clock : TimeProvider
    {
        public override DateTimeOffset GetUtcNow() => Now;
    }
}
