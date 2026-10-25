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
            PlannedStartTime = Now.AddDays(1),
            PlannedEndTime = Now.AddDays(1).AddHours(2),
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
