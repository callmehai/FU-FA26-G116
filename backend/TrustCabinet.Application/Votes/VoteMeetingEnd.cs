using System.Text.Json;
using TrustCabinet.Application.Abstractions;
using TrustCabinet.Application.Audit;
using TrustCabinet.Domain.Audit;
using TrustCabinet.Domain.Entities;

namespace TrustCabinet.Application.Votes;

/// <summary>
/// Deletes the drafts a meeting still has when it ends (PO 08/11/2026, SCRUM-28): opening needs the meeting
/// InProgress (BR-160), so a draft left at the end can never be used. Each deletion leaves one audit row naming what
/// the draft said, since the draft, its options and its document links are gone (the database cascades them, as when
/// the Chair or Secretary deletes a draft). Open and Closed votes are untouched (BR-189).
/// </summary>
public sealed class VoteMeetingEnd(IVoteRepository votes, IAuditLogRepository auditLog, IUnitOfWork unitOfWork)
    : IVoteMeetingEnd
{
    // TransactionMode.None: this runs inside the caller's meeting transaction and never opens one of its own, so a
    // caller without one is refused by ListDraftsAsync rather than given a transaction that commits on its own.
    public Task<IReadOnlyList<VoteChange>> DeleteDraftsAsync(Guid meetingId, Guid? endedById, DateTimeOffset endedAt,
        CancellationToken ct = default)
        => unitOfWork.ExecuteAsync<IReadOnlyList<VoteChange>>(async token =>
        {
            // Vote writes take the meeting lock first, so holding it here means no draft is edited or opened under us.
            var drafts = await votes.ListDraftsAsync(meetingId, token);
            foreach (var d in drafts)
            {
                var v = d.Vote;
                auditLog.Add(new AuditLogEntry
                {
                    ActorId = null, Action = AuditActions.VoteDraftDeletedOnMeetingEnd, ObjectType = AuditActions.VoteObjectType,
                    ObjectId = v.Id,
                    Details = JsonSerializer.Serialize(new
                    {
                        meetingId, endedById, reason = "meeting_ended", title = v.Title, description = v.Description,
                        isAnonymous = v.IsAnonymous, eligibleRoles = v.EligibleRoles.Select(r => r.ToString()).ToArray(),
                        options = d.Options, meetingDocumentIds = d.Documents, createdById = v.CreatedById, createdAt = v.CreatedAt
                    }),
                    OccurredAt = endedAt, CreatedAt = endedAt, UpdatedAt = endedAt
                });
                votes.Remove(v);
            }
            return drafts.Select(d => new VoteChange(Guid.CreateVersion7(), meetingId, d.Vote.Id, "DraftDeleted", null, endedAt))
                .ToArray();
        }, TransactionMode.None, ct);
}
