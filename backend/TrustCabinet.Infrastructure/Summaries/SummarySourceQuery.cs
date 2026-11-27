using Microsoft.EntityFrameworkCore;
using TrustCabinet.Application.Summaries;
using TrustCabinet.Domain.Audit;
using TrustCabinet.Domain.Entities;
using TrustCabinet.Domain.Enums;

namespace TrustCabinet.Infrastructure.Summaries;

public sealed class SummarySourceQuery(TrustCabinetDbContext db) : ISummarySourceQuery
{
    public Task<string> FindTitleAsync(Guid meetingId, CancellationToken ct)
        => db.Meetings.Where(m => m.Id == meetingId).Select(m => m.Title).SingleAsync(ct);

    public async Task<IReadOnlyList<AgendaFacts>> ListAgendaAsync(Guid meetingId, CancellationToken ct)
        => await db.AgendaItems.AsNoTracking()
            .Where(a => a.MeetingId == meetingId)
            .OrderBy(a => a.Position)
            .Select(a => new AgendaFacts(a.Id, a.Title,
                a.Presenter != null ? a.Presenter.FullName
                    : a.PresenterGuestInvitation != null ? a.PresenterGuestInvitation.SelfDeclaredDisplayName ?? a.PresenterGuestInvitation.InvitedName
                    : a.LegacyPresenterName != "" ? a.LegacyPresenterName : null,
                a.Conclusion, a.ActualStartedAt, a.ActualEndedAt))
            .ToListAsync(ct);

    public async Task<IReadOnlyList<string>> ListDocumentNamesAsync(Guid meetingId, CancellationToken ct)
        => await db.MeetingDocuments.AsNoTracking()
            .Where(d => d.MeetingId == meetingId && d.RemovedAt == null)
            .OrderBy(d => d.CreatedAt)
            .Select(d => d.FileName)
            .ToListAsync(ct);

    public async Task<IReadOnlyList<ClosedVote>> ListClosedVotesAsync(Guid meetingId, CancellationToken ct)
        => await db.VotingSessions.AsNoTracking()
            .Where(v => v.MeetingId == meetingId && v.Status == VotingStatus.Closed)
            .OrderBy(v => v.ClosedAt)
            .Select(v => new ClosedVote(v.Title, v.IsAnonymous, v.Conclusion, v.ConclusionNote,
                v.Options.OrderBy(o => o.Position).Select(o => new ClosedVoteOption(o.Label, o.BallotCount)).ToList()))
            .ToListAsync(ct);

    /// <summary>The transcripts a draft reads: of recordings still available.</summary>
    private IQueryable<Transcript> Transcripts(Guid meetingId) => db.Transcripts.Where(t => t.Recording.MeetingId == meetingId
        && t.Recording.Status == RecordingStatus.Available && t.Recording.DeletedAt == null);

    public async Task<int> CountSentencesAsync(Guid meetingId, CancellationToken ct)
        => await db.SpeechClips.CountAsync(c => c.MeetingId == meetingId && c.ExcludedAt == null && c.Text != "", ct)
            + await Transcripts(meetingId).Where(t => t.Status == TranscriptStatus.Completed).SelectMany(t => t.Segments).CountAsync(ct);

    public async Task<SummaryReadiness> ReadinessAsync(Guid meetingId, CancellationToken ct)
    {
        var sentences = await CountSentencesAsync(meetingId, ct);
        var transcripts = Transcripts(meetingId);
        var transcribing = await transcripts.CountAsync(t => t.Status == TranscriptStatus.Pending || t.Status == TranscriptStatus.Processing, ct);
        var failed = await transcripts.CountAsync(t => t.Status == TranscriptStatus.Failed, ct);
        var unnamed = await transcripts.Where(t => t.Status == TranscriptStatus.Completed).SelectMany(t => t.Speakers)
            // A voice none of whose lines are left in it (all moved to other voices) has nothing to name.
            .CountAsync(s => s.AssignedUserId == null && s.AssignedGuestId == null
                && s.Transcript.Segments.Any(x => x.SpeakerLabel == s.Label), ct);
        var agenda = await db.AgendaItems.Where(a => a.MeetingId == meetingId).Select(a => a.ActualStartedAt).ToListAsync(ct);
        var votes = await db.VotingSessions.CountAsync(v => v.MeetingId == meetingId && v.Status == VotingStatus.Closed, ct);
        var changed = new[]
        {
            await db.SpeechClips.Where(c => c.MeetingId == meetingId).MaxAsync(c => (DateTimeOffset?)c.UpdatedAt, ct),
            await transcripts.MaxAsync(t => (DateTimeOffset?)t.UpdatedAt, ct),
            await transcripts.SelectMany(t => t.Segments).MaxAsync(s => (DateTimeOffset?)s.UpdatedAt, ct),
            await transcripts.SelectMany(t => t.Speakers).MaxAsync(s => (DateTimeOffset?)s.UpdatedAt, ct),
            // What was erased leaves nothing above to date it, yet a draft written before still holds it: personal data
            // the meeting no longer keeps. A recording keeps its row and its time of erasure; a sentence leaves only the
            // audit entry of its deletion.
            await db.Recordings.Where(r => r.MeetingId == meetingId).MaxAsync(r => r.DeletedAt, ct),
            await db.Database.SqlQuery<DateTimeOffset?>($"""
                SELECT max(occurred_at) AS "Value" FROM audit_log
                WHERE action = {AuditActions.SpeechClipDeleted} AND details->>'MeetingId' = {meetingId.ToString()}
                """).SingleAsync(ct),
        }.Max();
        return new(sentences, unnamed, transcribing, failed, agenda.Count, agenda.Count(a => a is not null), votes,
            changed);
    }
}
