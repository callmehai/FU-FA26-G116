using Microsoft.Extensions.Options;
using TrustCabinet.Application.Abstractions;
using TrustCabinet.Application.Notifications;
using TrustCabinet.Application.Participants;
using TrustCabinet.Domain.Entities;
using TrustCabinet.Domain.Enums;

namespace TrustCabinet.Application.Email;

/// <summary>
/// Queues the reminder email for meetings starting within 15 minutes, beside the in-app reminder (BR-303). Unlike the
/// bell it also reaches Guests, who have no inbox in the app. The key holds the planned start, so a rescheduled meeting
/// is reminded again, and the unique key makes a second worker or a retry harmless.
/// </summary>
public sealed class MeetingReminderEmails(
    IUnitOfWork unitOfWork,
    IMeetingReminderQuery reminders,
    IEmailMessageRepository emails,
    IGuestInvitationLinkProvider guestLinks,
    IOptions<EmailDeliveryOptions> options,
    TimeProvider clock) : IMeetingReminderEmails
{
    /// <summary>The start of every reminder email's deduplication key.</summary>
    public const string KeyPrefix = "email-starting-soon:";

    private const string Subject = "Cuộc họp sắp bắt đầu";

    private static readonly TimeSpan Window = TimeSpan.FromMinutes(15);

    // No transaction, as before: the reads, the staged emails and one save when any was queued.
    public Task<int> QueueDueAsync(CancellationToken ct = default)
        => unitOfWork.ExecuteAsync(QueueAsync, TransactionMode.None, ct);

    private async Task<int> QueueAsync(CancellationToken ct)
    {
        var now = clock.GetUtcNow();
        var cutoff = now + Window;
        var meetings = await reminders.ListStartingAsync(now, cutoff, ct);
        if (meetings.Count == 0) return 0;

        var ids = meetings.Select(m => m.Meeting.Id).ToArray();
        var participants = await reminders.ListParticipantsAsync(ids, ct);
        var guests = await reminders.ListGuestsAsync(ids, ct);
        // Invited inside the reminder window: the invitation already gives the time, so a second email a minute later only
        // repeats it (PO 15/11/2026). Queued rows count too, sent or not yet. The bell still rings.
        var invitedLate = (await reminders.ListInvitationsAsync(ids, ct))
            .Where(d => d.Owner is not null && d.CreatedAt >= meetings.First(m => m.Meeting.Id == d.MeetingId).Meeting.PlannedStartTime - Window)
            .Select(d => d.Owner!.Value)
            .ToHashSet();
        var sent = (await reminders.ListReminderKeysAsync(ids, ct)).ToHashSet(StringComparer.Ordinal);

        var baseUrl = options.Value.ApplicationBaseUrl.TrimEnd('/');
        var queued = 0;
        foreach (var item in meetings)
        {
            var meeting = item.Meeting;
            var chair = participants.FirstOrDefault(p => p.MeetingId == meeting.Id && p.Role == MeetingRole.Chair)?.FullName;
            var staff = new MeetingPeople(chair, item.OrganizerName, item.OrganizerEmail);
            var forGuest = staff with { OrganizerEmail = null };
            var key = Prefix(meeting.Id) + meeting.PlannedStartTime.ToUnixTimeSeconds();
            // A TrustCabinet room is entered from the app; a meeting held elsewhere or in a room is read on its page.
            var inRoom = meeting.Mode != MeetingMode.InPerson && meeting.OnlineLink is null;
            var button = inRoom
                ? new EmailButton("Vào phòng họp", $"{baseUrl}/mgmt/meetings/{meeting.Id:D}/room")
                : new EmailButton("Xem cuộc họp", $"{baseUrl}/mgmt/meetings/{meeting.Id:D}");
            var place = MeetingInvitationEvents.PlaceOf(meeting);

            foreach (var p in participants.Where(p => p.MeetingId == meeting.Id))
            {
                var dedup = $"{key}:{p.Id:D}";
                if (invitedLate.Contains(p.Id) || !sent.Add(dedup)) continue;
                var role = MeetingInvitationEvents.RoleLabel(p.Role.ToString());
                Add(meeting, dedup, p.Email, now,
                    Text(p.FullName, meeting, place, button.Url, staff.Contact),
                    EmailTemplates.Reminder(p.FullName, meeting.Title, meeting.PlannedStartTime, meeting.PlannedEndTime,
                        meeting.Mode.ToString(), place, role, staff, button));
                queued++;
            }

            foreach (var g in guests.Where(g => g.MeetingId == meeting.Id))
            {
                var dedup = $"{key}:{g.Id:D}";
                if (invitedLate.Contains(g.Id) || sent.Contains(dedup)) continue;
                // No link, no email: a Guest reminded without a way in only has to dig the invitation out.
                var link = await guestLinks.PrepareAsync(g.Id, ct);
                if (link is not { Status: GuestLinkStatus.Ready, Url: { } url }) continue;
                sent.Add(dedup);
                Add(meeting, dedup, g.Email, now,
                    Text(g.InvitedName, meeting, null, url, forGuest.Contact),
                    EmailTemplates.Reminder(g.InvitedName, meeting.Title, meeting.PlannedStartTime, meeting.PlannedEndTime,
                        meeting.Mode.ToString(), null, null, forGuest, new EmailButton("Xác minh email để vào cuộc họp", url)));
                queued++;
            }
        }

        if (queued > 0) await unitOfWork.SaveChangesAsync(ct);
        return queued;
    }

    private static string Prefix(Guid meetingId) => $"{KeyPrefix}{meetingId:D}:";

    private void Add(Meeting meeting, string key, string to, DateTimeOffset now, string text, string html)
        => emails.Add(new EmailMessage
        {
            DeduplicationKey = key, ToAddress = to, Subject = SubjectFor(meeting.Title),
            Body = text, HtmlBody = html, MeetingId = meeting.Id, CreatedAt = now, UpdatedAt = now,
        });

    /// <summary>One line, within the 300 characters the column holds.</summary>
    internal static string SubjectFor(string meetingTitle)
    {
        var subject = $"{Subject}: {meetingTitle}".Replace('\r', ' ').Replace('\n', ' ');
        return subject.Length <= 300 ? subject : subject[..(char.IsHighSurrogate(subject[299]) ? 299 : 300)];
    }

    private static string Text(string name, Meeting meeting, string? place, string link, string contact)
        => $"Xin chào {name},\n\nCuộc họp \"{meeting.Title}\" bắt đầu lúc {EmailLayout.At(meeting.PlannedStartTime)}.\n"
            + $"Thời gian: {EmailLayout.When(meeting.PlannedStartTime, meeting.PlannedEndTime)}\n"
            + (place is null ? "" : $"Địa điểm: {place}\n")
            + $"\n{(place is null ? "Xác minh email để vào cuộc họp" : "Vào cuộc họp")}: {link}\n"
            + $"\nNgười tổ chức: {contact}\n";
}
