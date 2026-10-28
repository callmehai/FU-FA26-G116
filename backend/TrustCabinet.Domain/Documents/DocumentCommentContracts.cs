namespace TrustCabinet.Domain.Documents;

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
    /// <summary>UC-35: the threads of a document still in the meeting, for anyone who can view the meeting (BR-249, BR-275).</summary>
    Task<CommentOutcome<DocumentCommentThreads>> ListAsync(Guid meetingId, Guid meetingDocumentId, Guid actorId,
        CancellationToken ct = default);

    /// <summary>
    /// UC-35: a comment, or (AF-2) a reply that joins the thread of <paramref name="parentCommentId"/>'s top-level
    /// comment. <paramref name="pageNumber"/> is kept on a PDF only (BR-248).
    /// </summary>
    Task<CommentOutcome<DocumentCommentRow>> AddAsync(Guid meetingId, Guid meetingDocumentId, Guid actorId, string? body,
        int? pageNumber, Guid? parentCommentId, CancellationToken ct = default);
}
