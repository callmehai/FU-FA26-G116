using Microsoft.EntityFrameworkCore;
using TrustCabinet.Application.Summaries;
using TrustCabinet.Domain.Entities;
using TrustCabinet.Domain.Enums;

namespace TrustCabinet.Infrastructure.Summaries;

public sealed class MeetingSummaryRepository(TrustCabinetDbContext db) : IMeetingSummaryRepository
{
    public Task<MeetingSummary?> FindByMeetingAsync(Guid meetingId, bool tracked, CancellationToken ct)
        => tracked
            ? db.MeetingSummaries.FirstOrDefaultAsync(s => s.MeetingId == meetingId, ct)
            : db.MeetingSummaries.AsNoTracking().FirstOrDefaultAsync(s => s.MeetingId == meetingId, ct);

    public Task<MeetingSummary?> FindWithPeopleAsync(Guid meetingId, CancellationToken ct)
        => db.MeetingSummaries.AsNoTracking()
            .Include(s => s.RequestedBy).Include(s => s.ReviewedBy).Include(s => s.EditedBy)
            .FirstOrDefaultAsync(s => s.MeetingId == meetingId, ct);

    public void Add(MeetingSummary summary) => db.MeetingSummaries.Add(summary);

    public Task<int> SaveEditAsync(Guid id, string content, Guid actorId, DateTimeOffset now, CancellationToken ct)
        => db.MeetingSummaries
            .Where(s => s.Id == id && s.Status != SummaryStatus.Queued && s.Status != SummaryStatus.Running)
            .ExecuteUpdateAsync(u => u.SetProperty(x => x.Content, content).SetProperty(x => x.EditedById, actorId)
                .SetProperty(x => x.EditedAt, now)
                // A changed text has not been vouched for: back to the Chair and the Secretary until reviewed again.
                .SetProperty(x => x.ReviewedById, (Guid?)null).SetProperty(x => x.ReviewedAt, (DateTimeOffset?)null)
                .SetProperty(x => x.UpdatedAt, now), ct);

    public Task<int> MarkReviewedAsync(Guid id, Guid actorId, DateTimeOffset now, CancellationToken ct)
        => db.MeetingSummaries
            .Where(s => s.Id == id && s.Status != SummaryStatus.Queued && s.Status != SummaryStatus.Running)
            .ExecuteUpdateAsync(u => u.SetProperty(x => x.ReviewedById, actorId).SetProperty(x => x.ReviewedAt, now)
                .SetProperty(x => x.UpdatedAt, now), ct);

    public Task<QueuedSummary?> FindNextQueuedAsync(CancellationToken ct)
        => db.MeetingSummaries.AsNoTracking()
            .Where(s => s.Status == SummaryStatus.Queued)
            .OrderBy(s => s.RequestedAt)
            .Select(s => new QueuedSummary(s.Id, s.MeetingId))
            .FirstOrDefaultAsync(ct);

    public Task<int> ClaimAsync(Guid id, DateTimeOffset now, CancellationToken ct)
        => db.MeetingSummaries
            .Where(s => s.Id == id && s.Status == SummaryStatus.Queued)
            .ExecuteUpdateAsync(s => s.SetProperty(x => x.Status, SummaryStatus.Running)
                .SetProperty(x => x.UpdatedAt, now), ct);

    public Task<int> StoreDraftAsync(Guid id, string content, string model, DateTimeOffset generatedAt, DateTimeOffset now,
        CancellationToken ct)
        // A new draft replaces the old one whole: what was reviewed or corrected was the old text.
        => db.MeetingSummaries.Where(s => s.Id == id).ExecuteUpdateAsync(s => s
            .SetProperty(x => x.Status, SummaryStatus.Ready)
            .SetProperty(x => x.Content, content)
            .SetProperty(x => x.Model, model)
            .SetProperty(x => x.GeneratedAt, generatedAt)
            .SetProperty(x => x.ErrorMessage, "")
            .SetProperty(x => x.ReviewedById, (Guid?)null)
            .SetProperty(x => x.ReviewedAt, (DateTimeOffset?)null)
            .SetProperty(x => x.EditedById, (Guid?)null)
            .SetProperty(x => x.EditedAt, (DateTimeOffset?)null)
            .SetProperty(x => x.UpdatedAt, now), ct);

    public Task<int> MarkFailedAsync(Guid id, string error, DateTimeOffset now, CancellationToken ct)
        => db.MeetingSummaries.Where(s => s.Id == id).ExecuteUpdateAsync(s => s
            .SetProperty(x => x.Status, SummaryStatus.Failed)
            .SetProperty(x => x.ErrorMessage, error)
            .SetProperty(x => x.UpdatedAt, now), ct);

    public Task<int> RequeueRunningAsync(CancellationToken ct)
        => db.MeetingSummaries.Where(s => s.Status == SummaryStatus.Running)
            .ExecuteUpdateAsync(s => s.SetProperty(x => x.Status, SummaryStatus.Queued), ct);
}
