using Microsoft.EntityFrameworkCore;
using TrustCabinet.Application.Email;
using TrustCabinet.Domain.Entities;
using TrustCabinet.Domain.Enums;
using TrustCabinet.Domain.Participants;

namespace TrustCabinet.Infrastructure.Email;

public sealed class MeetingReminderQuery(TrustCabinetDbContext db) : IMeetingReminderQuery
{
    public async Task<IReadOnlyList<ReminderMeeting>> ListStartingAsync(DateTimeOffset now, DateTimeOffset cutoff,
        CancellationToken ct = default)
        => (await db.Meetings.AsNoTracking()
                .Where(m => m.Status == MeetingStatus.Published && m.PlannedStartTime > now && m.PlannedStartTime <= cutoff)
                .Select(m => new
                {
                    Meeting = m,
                    Organizer = new { m.CreatedBy.FullName, m.CreatedBy.Email },
                })
                .ToListAsync(ct))
            .Select(m => new ReminderMeeting(m.Meeting, m.Organizer.FullName, m.Organizer.Email)).ToList();

    public async Task<IReadOnlyList<ReminderParticipant>> ListParticipantsAsync(Guid[] meetingIds, CancellationToken ct = default)
        => await db.MeetingParticipants.AsNoTracking().Where(ParticipantRules.ActiveExpression)
            .Where(p => meetingIds.Contains(p.MeetingId) && p.User.IsActive)
            .Select(p => new ReminderParticipant(p.Id, p.MeetingId, p.Role, p.User.FullName, p.User.Email))
            .ToListAsync(ct);

    public async Task<IReadOnlyList<ReminderGuest>> ListGuestsAsync(Guid[] meetingIds, CancellationToken ct = default)
        => await db.MeetingGuestInvitations.AsNoTracking()
            .Where(g => meetingIds.Contains(g.MeetingId) && g.RevokedAt == null)
            .Select(g => new ReminderGuest(g.Id, g.MeetingId, g.InvitedName, g.Email))
            .ToListAsync(ct);

    public async Task<IReadOnlyList<ReminderInvitation>> ListInvitationsAsync(Guid[] meetingIds, CancellationToken ct = default)
        => await db.MeetingInvitationDispatches.AsNoTracking()
            .Where(d => meetingIds.Contains(d.MeetingId) && d.EventKind == InvitationEventKind.Invitation)
            .Select(d => new ReminderInvitation(d.MeetingId, d.ParticipantId ?? d.GuestInvitationId, d.CreatedAt))
            .ToListAsync(ct);

    public async Task<IReadOnlyList<string>> ListReminderKeysAsync(Guid[] meetingIds, CancellationToken ct = default)
        => await db.EmailMessages.AsNoTracking()
            .Where(e => e.MeetingId != null && meetingIds.Contains(e.MeetingId.Value)
                && e.DeduplicationKey.StartsWith(MeetingReminderEmails.KeyPrefix))
            .Select(e => e.DeduplicationKey).ToListAsync(ct);
}
