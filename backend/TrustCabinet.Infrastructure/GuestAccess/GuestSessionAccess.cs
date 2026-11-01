using Microsoft.EntityFrameworkCore;
using TrustCabinet.Domain.Agenda;
using TrustCabinet.Domain.Entities;
using TrustCabinet.Domain.Meetings;
using TrustCabinet.Domain.Participants;

namespace TrustCabinet.Infrastructure.GuestAccess;

/// <summary>
/// UC-04's live check behind every guest read: Agenda, Participants, Attendance and the meeting hub.
/// Reads the session, its invitation and the meeting on every call, so logout, revocation, expiry and
/// the meeting ending take effect at once whatever the token still says. Persistence errors propagate.
/// </summary>
internal sealed class GuestSessionAccess(TrustCabinetDbContext db, TimeProvider clock) : IAgendaGuestAccess, IParticipantGuestAccess
{
    public async Task<bool> CanReadAsync(MeetingGuestSession session, CancellationToken ct)
        => session.IsValid && await Admitted(session.MeetingId)
            .AnyAsync(x => x.Id == session.SessionId && x.InvitationId == session.InvitationId, ct);

    public Task<bool> CanAccessAsync(MeetingGuestSession session, CancellationToken ct) => CanReadAsync(session, ct);

    public async Task<IReadOnlyList<MeetingGuestSession>> ListReadersAsync(Guid meetingId, CancellationToken ct)
        => await Admitted(meetingId).Select(x => new MeetingGuestSession(meetingId, x.InvitationId, x.Id)).ToListAsync(ct);

    public async Task<IReadOnlyDictionary<Guid, GuestDisplayProfile>> GetProfilesAsync(Guid meetingId,
        IReadOnlyCollection<Guid> invitationIds, CancellationToken ct)
        => await db.MeetingGuestInvitations.AsNoTracking()
            .Where(g => g.MeetingId == meetingId && invitationIds.Contains(g.Id) && g.SelfDeclaredDisplayName != null)
            .ToDictionaryAsync(g => g.Id, g => new GuestDisplayProfile(g.SelfDeclaredDisplayName, g.SelfDeclaredJobTitle), ct);

    /// <summary>Current sessions of the meeting whose guest finished UC-04 step 5 (the display name).</summary>
    private IQueryable<GuestSession> Admitted(Guid meetingId)
        => GuestAccessService.CurrentSessions(db, clock.GetUtcNow()).Where(x => x.Invitation.MeetingId == meetingId && x.Invitation.SelfDeclaredDisplayName != null);
}
