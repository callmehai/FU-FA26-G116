using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Migrations.Operations;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using MimeKit;
using System.Text;
using TrustCabinet.Domain.Entities;
using TrustCabinet.Domain.Enums;
using TrustCabinet.Infrastructure;
using TrustCabinet.Infrastructure.Email;
using TrustCabinet.Infrastructure.Migrations;

namespace TrustCabinet.Web.Tests.Email;

public sealed class EmailDeliveryTests
{
    private static readonly DateTimeOffset Now = new(2026, 10, 1, 2, 0, 0, TimeSpan.Zero);

    [Fact]
    public void Migration_does_not_depend_on_machine_local_schema_history()
    {
        var migration = new ReliableEmailDelivery();

        Assert.Empty(migration.UpOperations.OfType<SqlOperation>());
        Assert.Empty(migration.DownOperations.OfType<SqlOperation>());
        Assert.Equal(
            ["deduplication_key", "lease_expires_at", "lease_token", "next_attempt_at"],
            migration.UpOperations
                .OfType<AddColumnOperation>()
                .Select(operation => operation.Name)
                .Order(StringComparer.Ordinal));
        Assert.False(Assert.Single(migration.UpOperations.OfType<AddColumnOperation>(),
            operation => operation.Name == "deduplication_key").IsNullable);
    }

    [Fact]
    public async Task Published_meeting_is_queued_once_per_participant_without_a_calendar_attachment()
    {
        await using var db = NewDatabase();
        var meeting = PublishedMeeting();
        db.Meetings.Add(meeting);
        await db.SaveChangesAsync();

        var queue = new MeetingInvitationQueue(
            db,
            Options.Create(DeliveryOptions()),
            new FixedTimeProvider(Now),
            NullLogger<MeetingInvitationQueue>.Instance);

        var count = await queue.QueueForPublishedMeetingAsync(meeting.Id);

        Assert.Equal(2, count);
        var messages = await db.EmailMessages.OrderBy(item => item.ToAddress).ToListAsync();
        Assert.All(messages, message =>
        {
            Assert.Equal(EmailStatus.Queued, message.Status);
            Assert.Equal(meeting.Id, message.MeetingId);
            Assert.True(message.Subject.Length <= 300);
            Assert.Contains("Hình thức: Kết hợp", message.Body, StringComparison.Ordinal);
            Assert.Contains("Địa điểm:", message.Body, StringComparison.Ordinal);
            Assert.Contains($"/mgmt/meetings/{meeting.Id:D}", message.Body, StringComparison.Ordinal);
            Assert.Null(message.CalendarAttachment);
        });
        Assert.Equal(
            ["an@trustcabinet.local", "binh@trustcabinet.local"],
            messages.Select(item => item.ToAddress));

        var repeatedCount = await queue.QueueForPublishedMeetingAsync(meeting.Id);
        Assert.Equal(0, repeatedCount);
        Assert.Equal(2, await db.EmailMessages.CountAsync());
    }

    [Fact]
    public async Task Inactive_participants_are_not_queued()
    {
        await using var db = NewDatabase();
        var meeting = PublishedMeeting();
        meeting.Participants.First().User.IsActive = false;
        db.Meetings.Add(meeting);
        await db.SaveChangesAsync();

        var count = await Queue(db).QueueForPublishedMeetingAsync(meeting.Id);

        Assert.Equal(1, count);
        Assert.DoesNotContain(
            await db.EmailMessages.Select(item => item.ToAddress).ToListAsync(),
            address => address == "an@trustcabinet.local");
    }

    [Fact]
    public async Task Reconciliation_queues_a_published_meeting_that_has_no_invitation_jobs()
    {
        await using var db = NewDatabase();
        var alreadyInitialized = PublishedMeeting();
        var missingJobs = PublishedMeeting();
        missingJobs.CreatedAt = alreadyInitialized.CreatedAt.AddMinutes(1);
        db.Meetings.AddRange(alreadyInitialized, missingJobs);
        await db.SaveChangesAsync();
        await Queue(db).QueueForPublishedMeetingAsync(alreadyInitialized.Id);

        var reconciledMeetingId = await Queue(db).QueueNextUninitializedPublishedMeetingAsync();

        Assert.Equal(missingJobs.Id, reconciledMeetingId);
        Assert.Equal(
            2,
            await db.EmailMessages.CountAsync(message => message.MeetingId == missingJobs.Id));
        Assert.Empty(missingJobs.StatusHistory);
    }

    [Fact]
    public async Task Reconciliation_does_nothing_when_all_published_meetings_are_initialized()
    {
        await using var db = NewDatabase();
        var meeting = PublishedMeeting();
        db.Meetings.Add(meeting);
        await db.SaveChangesAsync();
        var queue = Queue(db);
        await queue.QueueForPublishedMeetingAsync(meeting.Id);

        var reconciledMeetingId = await queue.QueueNextUninitializedPublishedMeetingAsync();

        Assert.Null(reconciledMeetingId);
        Assert.Equal(2, await db.EmailMessages.CountAsync());
    }

    [Fact]
    public async Task Disabled_email_delivery_does_not_queue_malformed_messages()
    {
        await using var db = NewDatabase();
        var meeting = PublishedMeeting();
        db.Meetings.Add(meeting);
        await db.SaveChangesAsync();
        var options = DeliveryOptions();
        options.Smtp.Enabled = false;
        options.ApplicationBaseUrl = string.Empty;
        options.Smtp.FromAddress = string.Empty;

        var count = await new MeetingInvitationQueue(
                db,
                Options.Create(options),
                new FixedTimeProvider(Now),
                NullLogger<MeetingInvitationQueue>.Instance)
            .QueueForPublishedMeetingAsync(meeting.Id);

        Assert.Equal(0, count);
        Assert.Empty(await db.EmailMessages.ToListAsync());
    }

    [Fact]
    public async Task Subject_truncation_does_not_split_an_emoji()
    {
        await using var db = NewDatabase();
        var meeting = PublishedMeeting();
        const string prefix = "Thư mời họp: ";
        meeting.Title = new string('a', 300 - prefix.Length - 2) + "😀";
        db.Meetings.Add(meeting);
        await db.SaveChangesAsync();

        await Queue(db).QueueForPublishedMeetingAsync(meeting.Id);

        var subject = (await db.EmailMessages.FirstAsync()).Subject;
        Assert.Equal(300, subject.Length);
        Assert.EndsWith("😀", subject, StringComparison.Ordinal);
        _ = new UTF8Encoding(encoderShouldEmitUTF8Identifier: false, throwOnInvalidBytes: true)
            .GetBytes(subject);
    }

    [Fact]
    public void Calendar_parameter_names_are_quoted_and_rfc_6868_encoded()
    {
        var calendar = CalendarInvitationBuilder.Build(
            Guid.CreateVersion7(),
            "Cuộc họp",
            "",
            "Phòng A",
            Now,
            Now.AddHours(1),
            "Nguyễn, Văn A; Thư ký: \"chính\"",
            "attendee@example.vn",
            "https://hop.example.vn/meeting",
            "Ban tổ chức",
            "organizer@example.vn",
            Now);
        var unfolded = calendar.Replace("\r\n ", string.Empty, StringComparison.Ordinal);

        Assert.Contains(
            "ATTENDEE;CN=\"Nguyễn, Văn A; Thư ký: ^'chính^'\";RSVP=TRUE:mailto:attendee@example.vn",
            unfolded,
            StringComparison.Ordinal);
        Assert.Contains(
            "ORGANIZER;CN=\"Ban tổ chức\":mailto:organizer@example.vn",
            unfolded,
            StringComparison.Ordinal);
    }

    [Fact]
    public async Task Non_published_meeting_cannot_queue_invitations()
    {
        await using var db = NewDatabase();
        var meeting = PublishedMeeting();
        meeting.Status = MeetingStatus.Approved;
        db.Meetings.Add(meeting);
        await db.SaveChangesAsync();

        var queue = new MeetingInvitationQueue(
            db,
            Options.Create(DeliveryOptions()),
            new FixedTimeProvider(Now),
            NullLogger<MeetingInvitationQueue>.Instance);

        await Assert.ThrowsAsync<InvalidOperationException>(
            () => queue.QueueForPublishedMeetingAsync(meeting.Id));
        Assert.Empty(await db.EmailMessages.ToListAsync());
    }

    [Fact]
    public void Smtp_message_contains_a_calendar_request_and_stable_message_id()
    {
        var queued = QueuedMessage("recipient@example.vn", Now);
        var smtp = DeliveryOptions().Smtp;
        smtp.FromAddress = "no-reply@example.vn";

        var message = SmtpEmailSender.BuildMessage(queued, smtp);

        Assert.Equal($"{queued.Id:N}@example.vn", message.MessageId);
        Assert.Equal("no-reply@example.vn", Assert.IsType<MailboxAddress>(message.From.Single()).Address);
        Assert.Equal("recipient@example.vn", Assert.IsType<MailboxAddress>(message.To.Single()).Address);
        var calendar = Assert.Single(
            message.BodyParts.OfType<MimePart>(),
            part => part.ContentType.MimeType == "text/calendar");
        Assert.Equal("REQUEST", calendar.ContentType.Parameters["method"]);
        Assert.Equal("thu-moi-hop.ics", calendar.FileName);
    }

    private static MeetingInvitationQueue Queue(TrustCabinetDbContext db)
        => new(
            db,
            Options.Create(DeliveryOptions()),
            new FixedTimeProvider(Now),
            NullLogger<MeetingInvitationQueue>.Instance);

    private static TrustCabinetDbContext NewDatabase()
    {
        var options = new DbContextOptionsBuilder<TrustCabinetDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;
        return new TrustCabinetDbContext(options);
    }

    private static Meeting PublishedMeeting()
    {
        var creator = User("creator@trustcabinet.local", "Người tổ chức");
        var meeting = new Meeting
        {
            Title = "Giao ban tháng 10",
            Description = "Thảo luận kế hoạch tháng.",
            Status = MeetingStatus.Published,
            Sensitivity = Sensitivity.Internal,
            Mode = MeetingMode.Hybrid,
            Location = "Phòng A / trực tuyến",
            CreatedBy = creator,
            CreatedById = creator.Id,
        };

        foreach (var user in new[]
                 {
                     User("an@trustcabinet.local", "Nguyễn An"),
                     User("binh@trustcabinet.local", "Trần Bình"),
                 })
        {
            meeting.Participants.Add(new MeetingParticipant
            {
                Meeting = meeting,
                MeetingId = meeting.Id,
                User = user,
                UserId = user.Id,
            });
        }

        return meeting;
    }

    private static User User(string email, string fullName)
        => new()
        {
            Email = email,
            FullName = fullName,
            PasswordHash = "not-used-in-this-test",
        };

    private static EmailMessage QueuedMessage(string recipient, DateTimeOffset createdAt)
        => new()
        {
            DeduplicationKey = $"test:{Guid.CreateVersion7():D}",
            ToAddress = recipient,
            Subject = "Thư mời họp",
            Body = "Nội dung",
            CalendarAttachment = "BEGIN:VCALENDAR\r\nEND:VCALENDAR\r\n",
            Status = EmailStatus.Queued,
            CreatedAt = createdAt,
            UpdatedAt = createdAt,
        };

    private static EmailDeliveryOptions DeliveryOptions()
        => new()
        {
            ApplicationBaseUrl = "https://hop.example.vn",
            MaxAttempts = 8,
            RetryDelaySeconds = 60,
            PollingIntervalSeconds = 1,
            ProcessingLeaseSeconds = 120,
            SendAttemptTimeoutSeconds = 60,
            Smtp = new SmtpOptions
            {
                Enabled = true,
                Host = "smtp.example.vn",
                FromAddress = "no-reply@example.vn",
                FromName = "TrustCabinet",
            },
        };

    private sealed class FixedTimeProvider(DateTimeOffset now) : TimeProvider
    {
        public override DateTimeOffset GetUtcNow() => now;
    }

}
