using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using MimeKit;
using TrustCabinet.Domain.Entities;
using TrustCabinet.Domain.Enums;
using TrustCabinet.Infrastructure;
using TrustCabinet.Infrastructure.Email;

namespace TrustCabinet.Web.Tests.Email;

public sealed class EmailDeliveryTests
{
    private static readonly DateTimeOffset Now = new(2026, 10, 1, 2, 0, 0, TimeSpan.Zero);

    [Fact]
    public async Task Published_meeting_is_queued_once_per_participant_with_calendar_attachment()
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
            Assert.Contains($"/mgmt/meetings/{meeting.Id:D}", message.Body, StringComparison.Ordinal);
            Assert.Contains("BEGIN:VCALENDAR\r\n", message.CalendarAttachment, StringComparison.Ordinal);
            Assert.Contains("METHOD:REQUEST\r\n", message.CalendarAttachment, StringComparison.Ordinal);
            Assert.Contains("DTSTART:20261001T020000Z\r\n", message.CalendarAttachment, StringComparison.Ordinal);
        });
        Assert.Equal(
            ["an@trustcabinet.local", "binh@trustcabinet.local"],
            messages.Select(item => item.ToAddress));

        var repeatedCount = await queue.QueueForPublishedMeetingAsync(meeting.Id);
        Assert.Equal(0, repeatedCount);
        Assert.Equal(2, await db.EmailMessages.CountAsync());
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
    public async Task Processor_sends_queued_messages_oldest_first_and_marks_them_sent()
    {
        await using var db = NewDatabase();
        var newer = QueuedMessage("newer@trustcabinet.local", Now.AddMinutes(1));
        var older = QueuedMessage("older@trustcabinet.local", Now);
        db.EmailMessages.AddRange(newer, older);
        await db.SaveChangesAsync();
        var sender = new RecordingSender();
        var processor = Processor(db, sender);

        var first = await processor.ProcessNextAsync();
        var second = await processor.ProcessNextAsync();
        var noWork = await processor.ProcessNextAsync();

        Assert.Equal(EmailProcessingOutcome.Sent, first.Outcome);
        Assert.Equal(EmailProcessingOutcome.Sent, second.Outcome);
        Assert.Equal(EmailProcessingOutcome.NoWork, noWork.Outcome);
        Assert.Equal(
            ["older@trustcabinet.local", "newer@trustcabinet.local"],
            sender.Recipients);
        Assert.All(await db.EmailMessages.ToListAsync(), message =>
        {
            Assert.Equal(EmailStatus.Sent, message.Status);
            Assert.Equal(1, message.AttemptCount);
            Assert.Equal(Now, message.SentAt);
        });
    }

    [Fact]
    public async Task Processor_retries_then_preserves_a_permanent_failure_for_audit()
    {
        await using var db = NewDatabase();
        var message = QueuedMessage("loi@trustcabinet.local", Now);
        db.EmailMessages.Add(message);
        await db.SaveChangesAsync();
        var sender = new RecordingSender(new SmtpExceptionForTest("SMTP unavailable"));
        var time = new AdjustableTimeProvider(Now);
        var processor = Processor(db, sender, maxAttempts: 2, time: time);

        var first = await processor.ProcessNextAsync();
        Assert.Equal(EmailProcessingOutcome.RetryScheduled, first.Outcome);
        var afterFirstAttempt = await db.EmailMessages.SingleAsync();
        Assert.Equal(EmailStatus.Queued, afterFirstAttempt.Status);
        Assert.Equal(1, afterFirstAttempt.AttemptCount);
        Assert.Equal(Now.AddSeconds(1), afterFirstAttempt.NextAttemptAt);
        Assert.Null(afterFirstAttempt.LeaseToken);

        var tooEarly = await processor.ProcessNextAsync();
        Assert.Equal(EmailProcessingOutcome.NoWork, tooEarly.Outcome);

        time.Advance(TimeSpan.FromSeconds(1));
        var second = await processor.ProcessNextAsync();
        Assert.Equal(EmailProcessingOutcome.PermanentlyFailed, second.Outcome);
        var afterSecondAttempt = await db.EmailMessages.SingleAsync();
        Assert.Equal(EmailStatus.Failed, afterSecondAttempt.Status);
        Assert.Equal(2, afterSecondAttempt.AttemptCount);
        Assert.Null(afterSecondAttempt.NextAttemptAt);
        Assert.Null(afterSecondAttempt.SentAt);
        Assert.Contains("SMTP unavailable", afterSecondAttempt.LastError, StringComparison.Ordinal);
        Assert.Equal(2, sender.Recipients.Count);
    }

    [Fact]
    public async Task Processor_recovers_an_expired_lease_but_skips_an_active_lease()
    {
        await using var db = NewDatabase();
        var expired = QueuedMessage("expired@trustcabinet.local", Now.AddMinutes(-2));
        expired.Status = EmailStatus.Processing;
        expired.AttemptCount = 1;
        expired.LeaseToken = Guid.NewGuid();
        expired.LeaseExpiresAt = Now.AddSeconds(-1);
        var active = QueuedMessage("active@trustcabinet.local", Now.AddMinutes(-1));
        active.Status = EmailStatus.Processing;
        active.AttemptCount = 1;
        active.LeaseToken = Guid.NewGuid();
        active.LeaseExpiresAt = Now.AddMinutes(1);
        db.EmailMessages.AddRange(expired, active);
        await db.SaveChangesAsync();
        var sender = new RecordingSender();
        var processor = Processor(db, sender);

        var recovered = await processor.ProcessNextAsync();
        var noWork = await processor.ProcessNextAsync();

        Assert.Equal(EmailProcessingOutcome.Sent, recovered.Outcome);
        Assert.Equal(2, recovered.AttemptCount);
        Assert.Equal(EmailProcessingOutcome.NoWork, noWork.Outcome);
        Assert.Equal(["expired@trustcabinet.local"], sender.Recipients);
        var messages = await db.EmailMessages.ToDictionaryAsync(item => item.ToAddress);
        Assert.Equal(EmailStatus.Sent, messages["expired@trustcabinet.local"].Status);
        Assert.Equal(EmailStatus.Processing, messages["active@trustcabinet.local"].Status);
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

    private static EmailQueueProcessor Processor(
        TrustCabinetDbContext db,
        IEmailSender sender,
        int maxAttempts = 3,
        TimeProvider? time = null)
    {
        var options = DeliveryOptions();
        options.MaxAttempts = maxAttempts;
        return new EmailQueueProcessor(
            db,
            sender,
            Options.Create(options),
            time ?? new FixedTimeProvider(Now),
            NullLogger<EmailQueueProcessor>.Instance);
    }

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
            StartsAt = Now,
            EndsAt = Now.AddHours(1),
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
            MaxAttempts = 3,
            RetryDelaySeconds = 1,
            PollingIntervalSeconds = 1,
            ProcessingLeaseSeconds = 120,
            Smtp = new SmtpOptions
            {
                Enabled = true,
                Host = "smtp.example.vn",
                FromAddress = "no-reply@example.vn",
                FromName = "TrustCabinet",
            },
        };

    private sealed class RecordingSender(Exception? exception = null) : IEmailSender
    {
        public List<string> Recipients { get; } = [];

        public Task SendAsync(EmailMessage message, CancellationToken ct = default)
        {
            ct.ThrowIfCancellationRequested();
            Recipients.Add(message.ToAddress);
            return exception is null ? Task.CompletedTask : Task.FromException(exception);
        }
    }

    private sealed class FixedTimeProvider(DateTimeOffset now) : TimeProvider
    {
        public override DateTimeOffset GetUtcNow() => now;
    }

    private sealed class AdjustableTimeProvider(DateTimeOffset now) : TimeProvider
    {
        public override DateTimeOffset GetUtcNow() => now;

        public void Advance(TimeSpan duration) => now = now.Add(duration);
    }

    private sealed class SmtpExceptionForTest(string message) : Exception(message);
}
