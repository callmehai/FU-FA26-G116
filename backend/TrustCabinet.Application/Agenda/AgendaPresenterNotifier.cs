using Microsoft.Extensions.DependencyInjection;
using TrustCabinet.Application.Abstractions;
using TrustCabinet.Application.Email;
using TrustCabinet.Application.Notifications;
using TrustCabinet.Domain.Agenda;
using TrustCabinet.Domain.Entities;
using TrustCabinet.Domain.Enums;

namespace TrustCabinet.Application.Agenda;

/// <summary>Best-effort enqueue after the agenda transaction, with an independent unit of work.</summary>
public sealed class AgendaPresenterNotifier(IServiceScopeFactory scopes) : IAgendaPresenterNotifier
{
    public async Task NotifyAsync(AgendaPresenterAssignment assignment, CancellationToken ct)
    {
        await using var scope = scopes.CreateAsyncScope();
        var unitOfWork = scope.ServiceProvider.GetRequiredService<IUnitOfWork>();
        var agenda = scope.ServiceProvider.GetRequiredService<IAgendaRepository>();
        var notifications = scope.ServiceProvider.GetRequiredService<INotificationRepository>();
        var emails = scope.ServiceProvider.GetRequiredService<IEmailMessageRepository>();
        // An early return writes nothing, so committing it is the same as the rollback the old block left it to.
        await unitOfWork.ExecuteAsync(async token =>
        {
            var meeting = await agenda.LockMeetingAsync(assignment.MeetingId, token);
            if (meeting is null) return;
            var item = await agenda.FindAsync(assignment.MeetingId, assignment.AgendaItemId, token);
            if (item is null || item.PresenterId != assignment.PresenterId
                || item.PresenterGuestInvitationId != assignment.PresenterGuestInvitationId) return;
            var recipient = assignment.PresenterId ?? assignment.PresenterGuestInvitationId;
            if (recipient is null) return;
            var key = $"agenda-presenter:{assignment.EventId:D}:{recipient:D}";
            const string title = "Phân công trình bày mục nghị sự";
            // A guest has no other way to tell which meeting this is, so name it and its time.
            var starts = TimeZoneInfo.ConvertTime(meeting.PlannedStartTime, TimeZoneInfo.FindSystemTimeZoneById("Asia/Ho_Chi_Minh"));
            var body = $"Bạn được phân công trình bày mục nghị sự \"{item.Title}\" trong cuộc họp \"{meeting.Title}\" "
                + $"lúc {starts:HH:mm dd/MM/yyyy}.";
            if (assignment.PresenterId is { } userId)
            {
                var participants = await agenda.ListParticipationsAsync(assignment.MeetingId, userId, token);
                if (!participants.Any(AgendaRules.CurrentParticipant)
                    || await notifications.ExistsWithDeduplicationKeyAsync(key, token)) return;
                notifications.Add(new Notification
                {
                    UserId = userId, Type = NotificationType.AgendaPresenterAssigned,
                    Title = title, Body = body, MeetingId = assignment.MeetingId,
                    TargetType = "Meeting", TargetId = assignment.MeetingId, DeduplicationKey = key,
                    CreatedAt = assignment.OccurredAt, UpdatedAt = assignment.OccurredAt
                });
            }
            else
            {
                var guest = await agenda.FindCurrentGuestAsync(assignment.MeetingId, recipient.Value, token);
                if (guest is null || await emails.ExistsWithDeduplicationKeyAsync(key, token)) return;
                emails.Add(new EmailMessage
                {
                    ToAddress = guest.Email, Subject = title, Body = body, MeetingId = assignment.MeetingId,
                    DeduplicationKey = key, CreatedAt = assignment.OccurredAt, UpdatedAt = assignment.OccurredAt
                });
            }
            await unitOfWork.SaveChangesAsync(token);
        }, TransactionMode.ReadCommitted, ct);
    }
}
