using TrustCabinet.Domain.Enums;

namespace TrustCabinet.Domain.Participants;

/// <summary>UC-27/30 permissions, evaluated only after validating the actor's current access.</summary>
public static class ParticipantReadPermissions
{
    public static ParticipantPermissions Evaluate(ParticipantSnapshot s, ParticipantActor actor)
    {
        var own = actor.Guest is null
            ? s.Meeting.Participants.SingleOrDefault(p => p.UserId == actor.UserId && ParticipantRules.Active(p)) : null;
        var secretary = own?.Role == MeetingRole.Secretary;
        var chair = own?.Role == MeetingRole.Chair;
        var keys = s.Grants.Where(g => actor.Guest is { } guest ? g.GuestInvitationId == guest.InvitationId
            : own is not null && g.MeetingParticipantId == own.Id).Select(g => g.PermissionKey).ToHashSet(StringComparer.Ordinal);
        var published = ParticipantRules.Published(s.Meeting.Status);
        return new(secretary || (published && (chair || keys.Contains(MeetingPermissionCatalog.ViewParticipants))),
            published && (secretary || chair || keys.Contains(MeetingPermissionCatalog.ViewResponses)),
            secretary && ParticipantRules.Editable(s.Meeting.Status),
            actor.Guest is null && actor.UserId is { } id && ParticipantRules.CanRecover(s.Meeting, id),
            (own is not null || actor.Guest is not null) && ParticipantRules.Respondable(s.Meeting.Status));
    }
}
