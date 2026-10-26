using TrustCabinet.Domain.Meetings;
using TrustCabinet.Domain.Entities;

namespace TrustCabinet.Domain.Agenda;

public enum AgendaOperation { Create, Update, Delete, Reorder, Start, Complete, RecordConclusion }
public enum AgendaFailure
{
    None, NotFound, Inactive, Forbidden, Locked, Invalid, UnsupportedMedia, TooLarge,
    ReadFailed, SaveFailed, DeleteLocked, InvalidOrder, StaleOrder,
    AlreadyCurrent, CannotStart, CannotComplete
}

public sealed record AgendaOutcome(AgendaFailure Failure = AgendaFailure.None, object? Data = null,
    IReadOnlyDictionary<string, string[]>? Errors = null, Guid? ItemId = null);

public sealed record AgendaInput(string? Title = null, string? Description = null,
    Guid? PresenterId = null, Guid? PresenterGuestInvitationId = null,
    IReadOnlyList<Guid>? DocumentIds = null, string? OrderVersion = null,
    IReadOnlyList<Guid>? OrderedItemIds = null, string? Conclusion = null, AgendaFailure ParseFailure = AgendaFailure.None);

public sealed record AgendaDocument(Guid Id, string Title);
public sealed record AgendaDocumentOption(Guid Id, string Title, IReadOnlyList<Guid> AgendaItemIds);
public sealed record AgendaPresenter(Guid Id, string Kind, string FullName);
public sealed record AgendaEditorOptions(IReadOnlyList<AgendaPresenter> Presenters,
    IReadOnlyList<AgendaDocumentOption> Documents);
public sealed record AgendaPermissions(bool CanCreate, bool CanReorder);
public sealed record AgendaActions(bool CanEdit, bool CanEditPlanningFields, bool CanDelete,
    bool CanSetCurrent, bool CanComplete, bool CanRecordConclusion);
public sealed record AgendaItemView(Guid Id, int Position, string Title, string Description,
    Guid? PresenterId, Guid? PresenterGuestInvitationId, string? PresenterName, string? LegacyPresenterName, string Status,
    DateTimeOffset? ActualStartedAt, DateTimeOffset? ActualEndedAt,
    string? Conclusion, Guid? ConclusionUpdatedById, string? ConclusionUpdatedByName, DateTimeOffset? ConclusionUpdatedAt,
    bool CanReadDocuments, IReadOnlyList<AgendaDocument> Documents, AgendaActions Actions);
public sealed record AgendaSnapshot(Guid MeetingId, string MeetingStatus,
    DateTimeOffset PlannedStartTime, DateTimeOffset PlannedEndTime, DateTimeOffset ServerTime,
    string OrderVersion, AgendaPermissions Permissions, IReadOnlyList<AgendaItemView> Items);
public sealed record AgendaChange(Guid EventId, Guid MeetingId, string ChangeType,
    Guid? AgendaItemId, string? Title, DateTimeOffset OccurredAt);

public sealed record AgendaPresenterAssignment(Guid EventId, Guid MeetingId, Guid AgendaItemId,
    Guid? PresenterId, Guid? PresenterGuestInvitationId, DateTimeOffset OccurredAt);

public interface IAgendaPresenterNotifier
{
    Task NotifyAsync(AgendaPresenterAssignment assignment, CancellationToken ct);
}

public interface IAgendaService
{
    Task<AgendaOutcome> GetAsync(Guid meetingId, Guid actorId, CancellationToken ct = default);
    Task<AgendaOutcome> GetForGuestAsync(Guid meetingId, MeetingGuestSession guest, CancellationToken ct = default);
    Task<AgendaOutcome> EditorOptionsAsync(Guid meetingId, Guid actorId, CancellationToken ct = default);
    Task<AgendaOutcome> CreateAsync(Guid meetingId, Guid actorId, AgendaInput input, CancellationToken ct = default);
    Task<AgendaOutcome> UpdateAsync(Guid meetingId, Guid itemId, Guid actorId, AgendaInput input, CancellationToken ct = default);
    Task<AgendaOutcome> DeleteAsync(Guid meetingId, Guid itemId, Guid actorId, CancellationToken ct = default);
    Task<AgendaOutcome> ReorderAsync(Guid meetingId, Guid actorId, AgendaInput input, CancellationToken ct = default);
    Task<AgendaOutcome> StartAsync(Guid meetingId, Guid itemId, Guid actorId, CancellationToken ct = default);
    Task<AgendaOutcome> RecordConclusionAsync(Guid meetingId, Guid itemId, Guid actorId, AgendaInput input, CancellationToken ct = default);
    Task<AgendaOutcome> CompleteAsync(Guid meetingId, Guid itemId, Guid actorId, CancellationToken ct = default);
}

public interface IAgendaRepository
{
    Task<IReadOnlyList<AgendaItem>> ListAsync(Guid meetingId, bool tracked, CancellationToken ct);
    Task<IReadOnlyList<MeetingGuestInvitation>> CurrentGuestsAsync(Guid meetingId, CancellationToken ct);
    Task<bool> HasMinutesAsync(Guid meetingId, CancellationToken ct);
    void Add(AgendaItem item);
    void Remove(AgendaItem item);
}

public interface IAgendaDocumentRepository
{
    Task<IReadOnlyList<AgendaDocumentOption>> ListAsync(Guid meetingId, CancellationToken ct);
    Task<bool> CanAssociateAsync(Guid meetingId, Guid? itemId, IReadOnlyList<Guid> documentIds, CancellationToken ct);
    Task AssociateAsync(Guid meetingId, Guid itemId, IReadOnlyList<Guid> documentIds, CancellationToken ct);
}

public interface IAgendaChangePublisher
{
    Task PublishAsync(AgendaChange change, CancellationToken ct);
}

/// <summary>Caller owns the meeting lock, transaction, SaveChanges and event publication.</summary>
public interface IAgendaMeetingEnd
{
    Task<AgendaChange?> PrepareAsync(Guid meetingId, DateTimeOffset endedAt, CancellationToken ct = default);
}
