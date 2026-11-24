namespace TrustCabinet.Domain.Agenda;

public enum AgendaFailure
{
    None, NotFound, Inactive, Forbidden, Locked, Invalid, UnsupportedMedia, TooLarge,
    ReadFailed, SaveFailed, DeleteLocked, InvalidOrder, StaleOrder,
    AlreadyCurrent, CannotStart, CannotComplete
}

public sealed record AgendaInput(string? Title = null, string? Description = null,
    Guid? PresenterId = null, Guid? PresenterGuestInvitationId = null,
    IReadOnlyList<Guid>? DocumentIds = null, string? OrderVersion = null,
    IReadOnlyList<Guid>? OrderedItemIds = null, string? Conclusion = null, AgendaFailure ParseFailure = AgendaFailure.None);

/// <summary><c>CanLinkDocuments</c> follows UC-36b (Chair or Secretary until the meeting ends), not planning.</summary>
public sealed record AgendaActions(bool CanEdit, bool CanEditPlanningFields, bool CanDelete,
    bool CanSetCurrent, bool CanComplete, bool CanRecordConclusion, bool CanLinkDocuments);
