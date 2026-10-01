using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using System.Text;
using TrustCabinet.Domain.Entities;
using TrustCabinet.Domain.Enums;
using TrustCabinet.Domain.Notifications;

namespace TrustCabinet.Infrastructure.Email;

public sealed class MeetingInvitationQueue(
    TrustCabinetDbContext db,
    IOptions<EmailDeliveryOptions> options,
    TimeProvider time,
    ILogger<MeetingInvitationQueue> logger) : IMeetingInvitationQueue
{
    private static readonly TimeZoneInfo VietnamTimeZone = TimeZoneInfo.FindSystemTimeZoneById("Asia/Ho_Chi_Minh");

    public async Task<int> QueueForPublishedMeetingAsync(MeetingPublicationInvitation publication, CancellationToken ct = default)
    {
        _ = time;
        if (db.Database.IsRelational() && db.Database.CurrentTransaction is null)
            throw new InvalidOperationException("Invitation jobs must share the Publish transaction.");
        var settings = options.Value;
        // SMTP enablement controls the worker, never creation of the durable publication jobs.
        if (!Uri.TryCreate(settings.ApplicationBaseUrl, UriKind.Absolute, out var origin)
            || origin.Scheme is not ("http" or "https")
            || !MimeKit.MailboxAddress.TryParse(settings.Smtp.FromAddress, out _))
            throw new MeetingInvitationQueueException("Invitation URL and sender must be configured before Publish.");

        var meetingUrl = $"{settings.ApplicationBaseUrl.Trim().TrimEnd('/')}/mgmt/meetings/{publication.MeetingId:D}";
        var candidates = publication.Recipients.Select(recipient => new
        {
            Recipient = recipient,
            Key = $"meeting:{publication.MeetingId:D}:published:{publication.OriginHistoryId:D}:{recipient.UserId:D}",
        }).ToArray();
        var keys = candidates.Select(item => item.Key).ToArray();
        var existing = new HashSet<string>(await db.EmailMessages
            .Where(item => keys.Contains(item.DeduplicationKey))
            .Select(item => item.DeduplicationKey).ToListAsync(ct), StringComparer.Ordinal);
        var messages = candidates.Where(item => !existing.Contains(item.Key)).Select(item => new EmailMessage
        {
            DeduplicationKey = item.Key,
            ToAddress = item.Recipient.Email,
            Subject = BuildSubject(publication.Title),
            Body = BuildBody(item.Recipient, publication, meetingUrl),
            CalendarAttachment = CalendarInvitationBuilder.Build(
                publication.MeetingId, publication.Title, publication.Description, publication.Location,
                publication.StartsAt, publication.EndsAt, item.Recipient.FullName, item.Recipient.Email,
                meetingUrl, settings.Smtp.FromName, settings.Smtp.FromAddress, publication.PublishedAt),
            MeetingId = publication.MeetingId,
            OriginHistoryId = publication.OriginHistoryId,
            RecipientUserId = item.Recipient.UserId,
            MessageType = EmailMessageType.MeetingInvitation,
            Status = EmailStatus.Queued,
            CreatedAt = publication.PublishedAt,
            UpdatedAt = publication.PublishedAt,
        }).ToArray();
        db.EmailMessages.AddRange(messages);
        await db.SaveChangesAsync(ct);
        logger.LogInformation("Queued {EmailCount} invitations for publication {HistoryId}.", messages.Length, publication.OriginHistoryId);
        return messages.Length;
    }

    private static string BuildSubject(string title)
    {
        const string prefix = "Thư mời họp: ";
        var result = new StringBuilder(prefix, 300);
        foreach (var rune in title.Replace('\r', ' ').Replace('\n', ' ').EnumerateRunes())
        {
            if (result.Length + rune.Utf16SequenceLength > 300) break;
            result.Append(rune.ToString());
        }
        return result.ToString();
    }

    private static string BuildBody(MeetingInvitationRecipient recipient, MeetingPublicationInvitation meeting, string url)
    {
        var start = TimeZoneInfo.ConvertTime(meeting.StartsAt, VietnamTimeZone);
        var end = meeting.EndsAt is null ? string.Empty
            : $"\nKết thúc: {TimeZoneInfo.ConvertTime(meeting.EndsAt.Value, VietnamTimeZone):dd/MM/yyyy HH:mm zzz}";
        return $"""
            Xin chào {recipient.FullName},

            Bạn được mời tham dự cuộc họp "{meeting.Title}".

            Bắt đầu: {start:dd/MM/yyyy HH:mm zzz}{end}
            Địa điểm/Hình thức: {meeting.Location}

            Xem chi tiết và tài liệu cuộc họp: {url}

            Tệp lịch .ics được đính kèm để bạn thêm cuộc họp vào Outlook hoặc Google Calendar.
            """;
    }
}
