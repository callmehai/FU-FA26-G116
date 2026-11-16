using TrustCabinet.Domain.Entities;
using TrustCabinet.Domain.Enums;

namespace TrustCabinet.Application.CalendarSharing;

/// <summary>What a share needs to know about one of its people right now: who, active, Meeting Organizer (BR-424, BR-430).</summary>
public sealed record PersonState(Guid Id, string FullName, string Email, bool IsActive, bool IsOrganizer)
{
    public SharePerson View => new(Id, FullName, Email);
}

internal static class CalendarSharePeople
{
    /// <summary>BR-424: an open share lives while both accounts are active and the viewer is still a Meeting Organizer.</summary>
    internal static bool IsLive(CalendarShare share, IReadOnlyDictionary<Guid, PersonState> people)
        => people.TryGetValue(share.OwnerId, out var owner) && owner.IsActive
            && people.TryGetValue(share.ViewerId, out var viewer) && viewer.IsActive && viewer.IsOrganizer;

    /// <summary>Why a dead share died: a locked account wins over a lost right.</summary>
    internal static CalendarShareStopReason ReasonFor(CalendarShare share, IReadOnlyDictionary<Guid, PersonState> people)
        => people.TryGetValue(share.OwnerId, out var owner) && owner.IsActive
            && people.TryGetValue(share.ViewerId, out var viewer) && viewer.IsActive
            ? CalendarShareStopReason.ViewerLostMeetingCreator
            : CalendarShareStopReason.AccountDeactivated;

    internal static string NameOf(IReadOnlyDictionary<Guid, PersonState> people, Guid id)
        => people.TryGetValue(id, out var person) ? person.FullName : "";
}
