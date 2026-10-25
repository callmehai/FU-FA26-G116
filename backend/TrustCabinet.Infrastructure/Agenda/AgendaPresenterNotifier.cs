using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using TrustCabinet.Domain.Agenda;
using TrustCabinet.Domain.Entities;
using TrustCabinet.Domain.Enums;

namespace TrustCabinet.Infrastructure.Agenda;

/// <summary>Best-effort enqueue after the agenda transaction, with an independent unit of work.</summary>
public sealed class AgendaPresenterNotifier(IServiceScopeFactory scopes) : IAgendaPresenterNotifier
{
    public async Task NotifyAsync(AgendaPresenterAssignment assignment, CancellationToken ct)
    {
        await using var scope = scopes.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<TrustCabinetDbContext>();
        await using var tx = await db.Database.BeginTransactionAsync(ct);
        var meeting = (await db.Meetings.FromSqlInterpolated($"SELECT * FROM meetings WHERE id = {assignment.MeetingId} FOR UPDATE")
            .AsNoTracking().ToListAsync(ct)).SingleOrDefault();
        if (meeting is null) return;
        var item = await db.AgendaItems.AsNoTracking().SingleOrDefaultAsync(i => i.Id == assignment.AgendaItemId
            && i.MeetingId == assignment.MeetingId, ct);
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
            var participants = await db.MeetingParticipants.Include(p => p.User)
                .Where(p => p.MeetingId == assignment.MeetingId && p.UserId == userId).ToListAsync(ct);
            if (!participants.Any(AgendaRules.CurrentParticipant)
                || await db.Notifications.AnyAsync(n => n.DeduplicationKey == key, ct)) return;
            db.Notifications.Add(new Notification
            {
                UserId = userId, Type = NotificationType.AgendaPresenterAssigned,
                Title = title, Body = body, MeetingId = assignment.MeetingId,
                TargetType = "Meeting", TargetId = assignment.MeetingId, DeduplicationKey = key,
                CreatedAt = assignment.OccurredAt, UpdatedAt = assignment.OccurredAt
            });
        }
        else
        {
            var guest = await db.MeetingGuestInvitations.AsNoTracking().SingleOrDefaultAsync(g =>
                g.Id == recipient && g.MeetingId == assignment.MeetingId && g.RevokedAt == null, ct);
            if (guest is null || await db.EmailMessages.AnyAsync(e => e.DeduplicationKey == key, ct)) return;
            db.EmailMessages.Add(new EmailMessage
            {
                ToAddress = guest.Email, Subject = title, Body = body, MeetingId = assignment.MeetingId,
                DeduplicationKey = key, CreatedAt = assignment.OccurredAt, UpdatedAt = assignment.OccurredAt
            });
        }
        await db.SaveChangesAsync(ct);
        await tx.CommitAsync(ct);
    }
}
