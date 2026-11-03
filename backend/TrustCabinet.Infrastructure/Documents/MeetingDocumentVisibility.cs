using TrustCabinet.Domain.Entities;
using TrustCabinet.Domain.Enums;

namespace TrustCabinet.Infrastructure.Documents;

internal static class MeetingDocumentVisibility
{
    /// <summary>
    /// The meeting documents a reader may open. A document linked to no agenda item is general only while no vote
    /// links it either (BR-280, option B); one reached only through draft votes is the Chair's and the Secretary's
    /// alone (BR-253), so without <paramref name="includeDrafts"/> it reads as not in the meeting at all.
    /// </summary>
    public static IQueryable<MeetingDocument> VisibleTo(this IQueryable<MeetingDocument> documents, bool includeDrafts)
        => includeDrafts
            ? documents
            : documents.Where(d => d.AgendaItemLinks.Any() || !d.VoteLinks.Any()
                || d.VoteLinks.Any(l => l.VotingSession.Status != VotingStatus.Draft));
}
