using TrustCabinet.Domain.Agenda;
using TrustCabinet.Domain.Meetings;
using TrustCabinet.Domain.Entities;

namespace TrustCabinet.Application.Agenda;

public enum AgendaOperation { Create, Update, Delete, Reorder, Start, Complete, RecordConclusion }

public sealed record AgendaOutcome(AgendaFailure Failure = AgendaFailure.None, object? Data = null,
    IReadOnlyDictionary<string, string[]>? Errors = null, Guid? ItemId = null);

public sealed record AgendaDocument(Guid Id, string Title);
public sealed record AgendaDocumentOption(Guid Id, string Title, IReadOnlyList<Guid> AgendaItemIds);
public sealed record AgendaPresenter(Guid Id, string Kind, string FullName);
public sealed record AgendaEditorOptions(IReadOnlyList<AgendaPresenter> Presenters,
    IReadOnlyList<AgendaDocumentOption> Documents);
public sealed record AgendaPermissions(bool CanCreate, bool CanReorder);
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
    /// <summary>
    /// Removes a Pending item. Notes typed on it (SCRUM-30) are appended to their author's meeting-wide note first, so
    /// the text survives and the meeting-wide note stays one per author (BR-397, BR-398).
    /// </summary>
    Task RemoveAsync(AgendaItem item, DateTimeOffset now, CancellationToken ct);

    /// <summary>
    /// Takes an exclusive lock on the one meeting row until the current transaction ends, and returns that meeting
    /// untracked and without its relations; null, and no lock, when there is none. Locks no other row. Throws
    /// <see cref="InvalidOperationException"/> when no transaction is open: the lock would be released at once, so
    /// the caller has not taken the meeting's lock it relies on.
    /// </summary>
    Task<Meeting?> LockMeetingAsync(Guid meetingId, CancellationToken ct);

    /// <summary>The meeting's item in progress, tracked; null when none is.</summary>
    Task<AgendaItem?> FindCurrentAsync(Guid meetingId, CancellationToken ct);

    /// <summary>One item of the meeting, untracked and without its relations; null when the meeting has no such item.</summary>
    Task<AgendaItem?> FindAsync(Guid meetingId, Guid itemId, CancellationToken ct);

    /// <summary>The user's rows in the meeting's participant list, one per role, tracked, with their account.</summary>
    Task<IReadOnlyList<MeetingParticipant>> ListParticipationsAsync(Guid meetingId, Guid userId, CancellationToken ct);

    /// <summary>The meeting's guest invitation, untracked, when it exists and has not been revoked.</summary>
    Task<MeetingGuestInvitation?> FindCurrentGuestAsync(Guid meetingId, Guid invitationId, CancellationToken ct);
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
