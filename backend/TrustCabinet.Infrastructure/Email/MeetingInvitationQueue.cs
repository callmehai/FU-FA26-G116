using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using System.Text;
using TrustCabinet.Domain.Entities;
using TrustCabinet.Domain.Enums;
using TrustCabinet.Domain.Notifications;
using TrustCabinet.Domain.Participants;

namespace TrustCabinet.Infrastructure.Email;

public sealed class MeetingInvitationQueue(
    TrustCabinetDbContext db,
    IOptions<EmailDeliveryOptions> options,
    TimeProvider time,
    ILogger<MeetingInvitationQueue> logger) : IMeetingInvitationQueue
{
    private static readonly TimeZoneInfo VietnamTimeZone =
        TimeZoneInfo.FindSystemTimeZoneById("Asia/Ho_Chi_Minh");

    public async Task<int> QueueForPublishedMeetingAsync(
        Guid meetingId, CancellationToken ct = default)
    {
        if (!db.Database.IsRelational()) return await QueueLegacyRecipientsAsync(meetingId, ct);
        await using var tx = await db.Database.BeginTransactionAsync(ct);
        await db.Meetings.FromSqlInterpolated($"SELECT * FROM meetings WHERE id = {meetingId} FOR UPDATE").AsNoTracking().ToArrayAsync(ct);
        var count = await QueueLegacyRecipientsAsync(meetingId, ct);
        await tx.CommitAsync(ct);
        return count;
    }

    private async Task<int> QueueLegacyRecipientsAsync(Guid meetingId, CancellationToken ct)
    {
        var emailOptions = options.Value;
        if (!emailOptions.Smtp.Enabled)
        {
            logger.LogInformation(
                "Email delivery is disabled; no invitation emails were queued for meeting {MeetingId}.",
                meetingId);
            return 0;
        }

        var meeting = await db.Meetings
            .Include(item => item.Participants)
                .ThenInclude(participant => participant.User)
            .SingleOrDefaultAsync(item => item.Id == meetingId, ct);

        if (meeting is null)
        {
            throw new InvalidOperationException($"Meeting {meetingId} does not exist.");
        }

        if (meeting.Status is not (MeetingStatus.Published or MeetingStatus.InProgress))
        {
            throw new InvalidOperationException(
                $"Meeting {meetingId} is {meeting.Status}, so its invitations cannot be queued.");
        }

        var meetingUrl = BuildMeetingUrl(emailOptions.ApplicationBaseUrl, meeting.Id);
        var now = time.GetUtcNow();
        var recipients = meeting.Participants
            .Where(ParticipantRules.Active)
            .Select(participant => new
            {
                User = participant.User,
                DeduplicationKey = MeetingInvitationEvents.InitialKey(participant),
            })
            .ToArray();
        var candidateKeys = recipients.Select(item => item.DeduplicationKey).ToArray();
        var existingKeys = new HashSet<string>(
            await db.EmailMessages
                .Where(item => candidateKeys.Contains(item.DeduplicationKey))
                .Select(item => item.DeduplicationKey)
                .ToListAsync(ct),
            StringComparer.Ordinal);
        // New producers own their durable dispatch. Only materialize legacy recipients here.
        existingKeys.UnionWith(await db.MeetingInvitationDispatches.Where(d => candidateKeys.Contains(d.DeduplicationKey))
            .Select(d => d.DeduplicationKey).ToArrayAsync(ct));
        var messages = recipients
            .Where(item => !existingKeys.Contains(item.DeduplicationKey))
            .Select(item => new EmailMessage
            {
                DeduplicationKey = item.DeduplicationKey,
                ToAddress = item.User.Email,
                Subject = BuildSubject(meeting.Title),
                Body = BuildBody(item.User, meeting, meetingUrl),
                CalendarAttachment = CalendarInvitationBuilder.Build(
                    meeting.Id,
                    meeting.Title,
                    meeting.Description,
                    FormatPlace(meeting),
                    meeting.PlannedStartTime,
                    meeting.PlannedEndTime,
                    item.User.FullName,
                    item.User.Email,
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

    public async Task<Guid?> QueueNextUninitializedPublishedMeetingAsync(
        CancellationToken ct = default)
    {
        if (!options.Value.Smtp.Enabled)
        {
            return null;
        }

        var meetingId = await db.Meetings
            .Where(meeting => meeting.Status == MeetingStatus.Published || meeting.Status == MeetingStatus.InProgress)
            .Where(meeting => meeting.Participants.Any(p => p.User.IsActive && p.AssignmentStatus == AssignmentStatus.Current
                && p.Role != MeetingRole.Guest && p.Response != InvitationResponse.Delegated && p.DelegatedToUserId == null
                && !db.EmailMessages.Any(e => e.DeduplicationKey == (p.InitialInvitationDeliveryKey ?? "meeting:" + meeting.Id.ToString() + ":published:" + p.UserId.ToString()))
                && !db.MeetingInvitationDispatches.Any(d => d.DeduplicationKey == (p.InitialInvitationDeliveryKey ?? "meeting:" + meeting.Id.ToString() + ":published:" + p.UserId.ToString()))))
            .OrderBy(meeting => meeting.CreatedAt)
            .ThenBy(meeting => meeting.Id)
            .Select(meeting => (Guid?)meeting.Id)
            .FirstOrDefaultAsync(ct);

        if (meetingId is null)
        {
            return null;
        }

        await QueueForPublishedMeetingAsync(meetingId.Value, ct);
        return meetingId;
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
        return prefix + TruncateByRunes(singleLineTitle, maxSubjectLength - prefix.Length);
    }

    private static string BuildBody(User user, Meeting meeting, string meetingUrl)
    {
        var startsAt = TimeZoneInfo.ConvertTime(meeting.PlannedStartTime, VietnamTimeZone);
        var endsAt = TimeZoneInfo.ConvertTime(meeting.PlannedEndTime, VietnamTimeZone);

        return $"""
            Xin chào {user.FullName},

            Bạn được mời tham dự cuộc họp "{meeting.Title}".

            Bắt đầu: {startsAt:dd/MM/yyyy HH:mm zzz}
            Kết thúc: {endsAt:dd/MM/yyyy HH:mm zzz}
            Hình thức: {FormatMeetingMode(meeting.Mode)}
            Địa điểm: {FormatPlace(meeting)}

            Xem chi tiết và tài liệu cuộc họp: {meetingUrl}

            Tệp lịch .ics được đính kèm để bạn thêm cuộc họp vào Outlook hoặc Google Calendar.
            """;
    }

    /// <summary>An online meeting has no room: it happens at its link, or in the TrustCabinet room.</summary>
    private static string FormatPlace(Meeting meeting)
    {
        var link = meeting.OnlineLink ?? "Phòng họp trực tuyến TrustCabinet";
        return meeting.Mode switch
        {
            MeetingMode.Online => link,
            MeetingMode.Hybrid when meeting.Location.Length > 0 => $"{meeting.Location} · {link}",
            MeetingMode.Hybrid => link,
            _ => meeting.Location,
        };
    }

    private static string FormatMeetingMode(MeetingMode mode)
        => mode switch
        {
            MeetingMode.InPerson => "Trực tiếp",
            MeetingMode.Online => "Trực tuyến",
            MeetingMode.Hybrid => "Kết hợp",
            _ => mode.ToString(),
        };

    private static string TruncateByRunes(string value, int maxLength)
    {
        var result = new StringBuilder(Math.Min(value.Length, maxLength));
        foreach (var rune in value.EnumerateRunes())
        {
            if (result.Length + rune.Utf16SequenceLength > maxLength)
            {
                break;
            }

            result.Append(rune.ToString());
        }

        return result.ToString();
    }
}
