using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using TrustCabinet.Domain.Audit;
using TrustCabinet.Domain.Entities;
using TrustCabinet.Domain.Enums;
using TrustCabinet.Domain.Votes;

namespace TrustCabinet.Infrastructure.Votes;

/// <summary>
/// Deletes the drafts a meeting still has when it ends (PO 08/11/2026, SCRUM-28) or is cancelled (ADR-0067): opening
/// needs the meeting InProgress (BR-160), so a draft left at the end, or in a meeting that will never start, can never
/// be used. Each deletion leaves one audit row naming what the draft said, since the draft, its options and its document
/// links are gone (the database cascades them, as when the Chair or Secretary deletes a draft). Open and Closed votes are
/// untouched (BR-189).
/// </summary>
public sealed class VoteMeetingEnd(TrustCabinetDbContext db) : IVoteMeetingEnd
{
    public Task<IReadOnlyList<VoteChange>> DeleteDraftsAsync(Guid meetingId, Guid? endedById, DateTimeOffset endedAt,
        CancellationToken ct = default)
        => DeleteAsync(meetingId, endedAt, AuditActions.VoteDraftDeletedOnMeetingEnd, "endedById", endedById, "meeting_ended", ct);

    public Task<IReadOnlyList<VoteChange>> DeleteDraftsOnCancelAsync(Guid meetingId, Guid? cancelledById, DateTimeOffset at,
        CancellationToken ct = default)
        => DeleteAsync(meetingId, at, AuditActions.VoteDraftDeletedOnMeetingCancel, "cancelledById", cancelledById,
            "meeting_cancelled", ct);

    private async Task<IReadOnlyList<VoteChange>> DeleteAsync(Guid meetingId, DateTimeOffset endedAt, string action,
        string byKey, Guid? byId, string reason, CancellationToken ct)
    {
        // Vote writes take the meeting lock first, so holding it here means no draft is edited or opened under us.
        if (db.Database.CurrentTransaction is null)
            throw new InvalidOperationException("Deleting a meeting's drafts requires the caller's meeting transaction and lock.");
        var drafts = await db.VotingSessions
            .Where(v => v.MeetingId == meetingId && v.Status == VotingStatus.Draft)
            .OrderBy(v => v.CreatedAt).ThenBy(v => v.Id)
            .Select(v => new
            {
                Vote = v,
                Options = v.Options.OrderBy(o => o.Position).Select(o => o.Label).ToArray(),
                Documents = v.Documents.Select(d => d.MeetingDocumentId).ToArray()
            })
            .ToListAsync(ct);
        foreach (var d in drafts)
        {
            var v = d.Vote;
            var details = new Dictionary<string, object?>
            {
                ["meetingId"] = meetingId, [byKey] = byId, ["reason"] = reason, ["title"] = v.Title,
                ["description"] = v.Description, ["isAnonymous"] = v.IsAnonymous,
                ["eligibleRoles"] = v.EligibleRoles.Select(r => r.ToString()).ToArray(), ["options"] = d.Options,
                ["meetingDocumentIds"] = d.Documents, ["createdById"] = v.CreatedById, ["createdAt"] = v.CreatedAt,
            };
            db.AuditLog.Add(new AuditLogEntry
            {
                ActorId = null, Action = action, ObjectType = AuditActions.VoteObjectType,
                ObjectId = v.Id,
                Details = JsonSerializer.Serialize(details),
                OccurredAt = endedAt, CreatedAt = endedAt, UpdatedAt = endedAt
            });
            db.VotingSessions.Remove(v);
        }
        return drafts.Select(d => new VoteChange(Guid.CreateVersion7(), meetingId, d.Vote.Id, "DraftDeleted", null, endedAt))
            .ToArray();
    }
}
