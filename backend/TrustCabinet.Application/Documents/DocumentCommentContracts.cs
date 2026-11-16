using TrustCabinet.Application.Participants;
using TrustCabinet.Domain.Entities;

namespace TrustCabinet.Application.Documents;

public enum CommentFailure { None, Blank, TooLong, NotFound, Ended, Cancelled, SaveFailed, ReadFailed }

/// <summary>Who wrote a comment: an internal user, or a Guest under their invitation with the name they gave (ADR-0016).</summary>
public sealed record DocumentCommentAuthor(Guid? UserId, Guid? GuestInvitationId, string Name, bool IsGuest);

/// <summary>A comment; a top-level one carries its replies, oldest first, one level deep (BR-317).</summary>
public sealed record DocumentCommentRow(Guid Id, string Body, int? PageNumber, DocumentCommentAuthor Author,
    DateTimeOffset CreatedAt, IReadOnlyList<DocumentCommentRow> Replies);

/// <summary><see cref="CanComment"/> is false once the meeting has ended, when the comments become read-only (BR-282).</summary>
public sealed record DocumentCommentThreads(Guid MeetingDocumentId, bool CanComment, IReadOnlyList<DocumentCommentRow> Comments);

public sealed record CommentOutcome<T>(CommentFailure Failure = CommentFailure.None, T? Value = default);

public interface IDocumentCommentService
{
    /// <summary>
    /// UC-35: the threads of a document still in the meeting, for anyone who can view the meeting (BR-249, BR-275), a
    /// verified guest of it included while their UC-04 session is current (BR-181).
    /// </summary>
    Task<CommentOutcome<DocumentCommentThreads>> ListAsync(Guid meetingId, Guid meetingDocumentId, ParticipantActor actor,
        CancellationToken ct = default);

    /// <summary>
    /// UC-35: a comment, or (AF-2) a reply that joins the thread of <paramref name="parentCommentId"/>'s top-level
    /// comment. <paramref name="pageNumber"/> is kept on a PDF only (BR-248). A guest writes under their invitation
    /// (ADR-0016), shown with the name they declared.
    /// </summary>
    Task<CommentOutcome<DocumentCommentRow>> AddAsync(Guid meetingId, Guid meetingDocumentId, ParticipantActor actor, string? body,
        int? pageNumber, Guid? parentCommentId, CancellationToken ct = default);
}

/// <summary>One stored comment with the name its author shows under (UC-04 step 5, ADR-0016).</summary>
public sealed record DocumentCommentFlatRow(Guid Id, Guid? ParentCommentId, string Body, int? PageNumber,
    Guid? AuthorId, Guid? GuestInvitationId, string Name, DateTimeOffset CreatedAt);

/// <summary>The stored file behind a commentable document: what a page number is checked against (BR-248).</summary>
public sealed record DocumentPageInfo(string ContentType, int? PageCount);

/// <summary>UC-35's comments as the use case reads and writes them.</summary>
public interface IDocumentCommentRepository
{
    /// <summary>
    /// Takes a shared lock on the one meeting row until the current transaction ends, so a comment sent as the
    /// meeting ends or is cancelled is refused rather than slipped in after it (BR-282), while comments do not
    /// queue behind one another. Locks no other row. Take before the session lock.
    /// </summary>
    Task LockMeetingSharedAsync(Guid meetingId, CancellationToken ct);

    /// <summary>Whether the document is still attached to the meeting (BR-211, BR-253).</summary>
    Task<bool> IsAttachedAsync(Guid meetingId, Guid meetingDocumentId, bool includeDrafts, CancellationToken ct);

    /// <summary>The threads' comments, oldest first, with the name each author shows under.</summary>
    Task<IReadOnlyList<DocumentCommentFlatRow>> ListAsync(Guid meetingDocumentId, CancellationToken ct);

    /// <summary>The parent comment with its thread; null when it is not on this document.</summary>
    Task<(Guid Id, Guid? ParentCommentId)?> FindParentAsync(Guid parentId, Guid meetingDocumentId,
        CancellationToken ct);

    /// <summary>
    /// The document's stored file, enough to check a page number. Null when removed, of another meeting,
    /// or only on a draft vote the actor may not see (BR-211, BR-253).
    /// </summary>
    Task<DocumentPageInfo?> FindPageInfoAsync(Guid meetingId, Guid meetingDocumentId, bool includeDrafts,
        CancellationToken ct);

    /// <summary>The name the guest shows under; throws when the invitation does not exist.</summary>
    Task<string> FindGuestDisplayNameAsync(Guid invitationId, CancellationToken ct);

    /// <summary>Stages the comment; written by the next <c>IUnitOfWork.SaveChangesAsync</c>.</summary>
    void Add(DocumentComment comment);
}
