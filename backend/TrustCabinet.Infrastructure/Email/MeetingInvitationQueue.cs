using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
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
    public async Task<int> QueueForPublishedMeetingAsync(
        Guid meetingId, CancellationToken ct = default)
    {
        var meeting = await db.Meetings
            .Include(item => item.Participants)
                .ThenInclude(participant => participant.User)
            .SingleOrDefaultAsync(item => item.Id == meetingId, ct);

        if (meeting is null)
        {
            throw new InvalidOperationException($"Meeting {meetingId} does not exist.");
        }

        if (meeting.Status != MeetingStatus.Published)
        {
            throw new InvalidOperationException(
                $"Meeting {meetingId} is {meeting.Status}, so its invitations cannot be queued.");
        }

        var emailOptions = options.Value;
        var meetingUrl = BuildMeetingUrl(emailOptions.ApplicationBaseUrl, meeting.Id);
        var now = time.GetUtcNow();
        var existingAddresses = new HashSet<string>(
            await db.EmailMessages
                .Where(item => item.MeetingId == meeting.Id)
                .Select(item => item.ToAddress)
                .ToListAsync(ct),
            StringComparer.OrdinalIgnoreCase);
        var messages = meeting.Participants
            .Select(participant => participant.User)
            .Where(user => !existingAddresses.Contains(user.Email))
            .Select(user => new EmailMessage
            {
                ToAddress = user.Email,
                Subject = BuildSubject(meeting.Title),
                Body = BuildBody(user, meeting, meetingUrl),
                CalendarAttachment = CalendarInvitationBuilder.Build(
                    meeting.Id,
                    meeting.Title,
                    meeting.Description,
                    meeting.Location,
                    meeting.StartsAt,
                    meeting.EndsAt,
                    user.FullName,
                    user.Email,
                    meetingUrl,
                    emailOptions.Smtp.FromName,
                    emailOptions.Smtp.FromAddress,
                    now),
                MeetingId = meeting.Id,
                Status = EmailStatus.Queued,
                CreatedAt = now,
                UpdatedAt = now,
            })
            .ToArray();

        if (messages.Length == 0)
        {
            logger.LogInformation(
                "Published meeting {MeetingId} has no new participant invitation emails to queue.",
                meeting.Id);
            return 0;
        }

        db.EmailMessages.AddRange(messages);
        await db.SaveChangesAsync(ct);

        logger.LogInformation(
            "Queued {EmailCount} invitation emails for published meeting {MeetingId}.",
            messages.Length,
            meeting.Id);
        return messages.Length;
    }

    private static string BuildMeetingUrl(string applicationBaseUrl, Guid meetingId)
        => $"{applicationBaseUrl.Trim().TrimEnd('/')}/mgmt/meetings/{meetingId:D}";

    private static string BuildSubject(string title)
    {
        const string prefix = "Thư mời họp: ";
        const int maxSubjectLength = 300;
        var singleLineTitle = title
            .Replace('\r', ' ')
            .Replace('\n', ' ');
        return prefix + singleLineTitle[..Math.Min(
            singleLineTitle.Length,
            maxSubjectLength - prefix.Length)];
    }

    private static string BuildBody(User user, Meeting meeting, string meetingUrl)
    {
        var end = meeting.EndsAt is null
            ? string.Empty
            : $"\nKết thúc: {meeting.EndsAt.Value:dd/MM/yyyy HH:mm zzz}";

        return $"""
            Xin chào {user.FullName},

            Bạn được mời tham dự cuộc họp "{meeting.Title}".

            Bắt đầu: {meeting.StartsAt:dd/MM/yyyy HH:mm zzz}{end}
            Địa điểm/Hình thức: {meeting.Location}

            Xem chi tiết và tài liệu cuộc họp: {meetingUrl}

            Tệp lịch .ics được đính kèm để bạn thêm cuộc họp vào Outlook hoặc Google Calendar.
            """;
    }
}
