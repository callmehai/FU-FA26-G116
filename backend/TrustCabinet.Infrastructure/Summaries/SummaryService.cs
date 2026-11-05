using System.Text.Encodings.Web;
using System.Text.Json;
using System.Text.Unicode;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using TrustCabinet.Domain.Entities;
using TrustCabinet.Domain.Enums;
using TrustCabinet.Domain.Meetings;
using TrustCabinet.Domain.Participants;
using TrustCabinet.Domain.Summaries;

namespace TrustCabinet.Infrastructure.Summaries;

/// <summary>
/// SCRUM-36 — the AI draft of a meeting. The current Chair or Secretary asks for it once the meeting has started, reads
/// and corrects it, and reviews it; only then do the other participants see it. The AI writes nothing else: the minutes
/// (SCRUM-32) take the draft as their starting point, and a person is answerable for what they say.
/// </summary>
public sealed class SummaryService(TrustCabinetDbContext db, IMeetingRepository meetings, ILanguageModel model,
    MeetingTranscriptReader transcript, TimeProvider clock, ILogger<SummaryService> logger) : ISummaryService
{
    /// <summary>
    /// Vietnamese letters are stored as they are: the default encoder writes each one as a six-character <c>\uXXXX</c>,
    /// which more than doubles the text and overflows the column well inside the documented limits.
    /// </summary>
    internal static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web)
    {
        Encoder = JavaScriptEncoder.Create(UnicodeRanges.All),
    };

    public async Task<SummaryOutcome> GetAsync(Guid meetingId, Guid actorId, CancellationToken ct = default)
    {
        try
        {
            var (meeting, failure) = await ReadableAsync(meetingId, actorId, ct);
            if (meeting is null) return new(failure);
            var manages = Manages(meeting, actorId);
            var row = await Query().FirstOrDefaultAsync(s => s.MeetingId == meetingId, ct);
            var visible = row is not null && (manages || row.ReviewedAt is not null);
            return new(Data: new SummaryState(visible ? View(row!) : null, manages, model.IsConfigured,
                manages ? await ReadinessAsync(meetingId, ct) : null));
        }
        catch (Exception error) when (error is not OperationCanceledException)
        {
            logger.LogError(error, "Could not read the summary of meeting {MeetingId}", meetingId);
            return new(SummaryFailure.ReadFailed);
        }
    }

    public async Task<SummaryOutcome> RequestAsync(Guid meetingId, Guid actorId, CancellationToken ct = default)
    {
        try
        {
            var (meeting, failure) = await ReadableAsync(meetingId, actorId, ct);
            if (meeting is null) return new(failure);
            if (!Manages(meeting, actorId)) return new(SummaryFailure.Forbidden);
            if (meeting.Status is not (MeetingStatus.InProgress or MeetingStatus.Ended)) return new(SummaryFailure.WrongMeetingStatus);
            if (!model.IsConfigured) return new(SummaryFailure.AiUnavailable);
            var row = await db.MeetingSummaries.FirstOrDefaultAsync(s => s.MeetingId == meetingId, ct);
            if (row is { Status: SummaryStatus.Queued or SummaryStatus.Running }) return new(SummaryFailure.Busy);
            var now = clock.GetUtcNow();
            if (row is null)
            {
                row = new MeetingSummary { MeetingId = meetingId };
                db.MeetingSummaries.Add(row);
            }
            row.Status = SummaryStatus.Queued;
            row.ErrorMessage = "";
            row.RequestedById = actorId;
            row.RequestedAt = now;
            row.UpdatedAt = now;
            await db.SaveChangesAsync(ct);
            return await GetAsync(meetingId, actorId, ct);
        }
        catch (DbUpdateException error)
        {
            // Two requests at once: the unique meeting index lets one through, the other reads as busy.
            logger.LogWarning(error, "Concurrent summary request for meeting {MeetingId}", meetingId);
            return new(SummaryFailure.Busy);
        }
        catch (Exception error) when (error is not OperationCanceledException)
        {
            logger.LogError(error, "Could not queue the summary of meeting {MeetingId}", meetingId);
            return new(SummaryFailure.SaveFailed);
        }
    }

    public async Task<SummaryOutcome> EditAsync(Guid meetingId, Guid actorId, SummaryEdit edit, CancellationToken ct = default)
    {
        try
        {
            var (meeting, failure) = await ReadableAsync(meetingId, actorId, ct);
            if (meeting is null) return new(failure);
            if (!Manages(meeting, actorId)) return new(SummaryFailure.Forbidden);
            var row = await db.MeetingSummaries.FirstOrDefaultAsync(s => s.MeetingId == meetingId, ct);
            var draft = row is null ? null : Read(row);
            if (row is null || draft is null) return new(SummaryFailure.NotDrafted);
            var errors = Validate(edit);
            if (errors.Count > 0) return new(SummaryFailure.Invalid, Errors: errors);
            row.Content = JsonSerializer.Serialize(draft with
            {
                Objectives = SummaryWriter.Tidy(edit.Objectives!), Discussion = SummaryWriter.Tidy(edit.Discussion!),
                Conclusions = SummaryWriter.Tidy(edit.Conclusions!),
            }, Json);
            var now = clock.GetUtcNow();
            row.EditedById = actorId;
            row.EditedAt = now;
            // A changed text has not been vouched for: it goes back to the Chair and the Secretary until reviewed again.
            row.ReviewedById = null;
            row.ReviewedAt = null;
            row.UpdatedAt = now;
            await db.SaveChangesAsync(ct);
            return await GetAsync(meetingId, actorId, ct);
        }
        catch (Exception error) when (error is not OperationCanceledException)
        {
            logger.LogError(error, "Could not save the summary of meeting {MeetingId}", meetingId);
            return new(SummaryFailure.SaveFailed);
        }
    }

    public async Task<SummaryOutcome> ReviewAsync(Guid meetingId, Guid actorId, CancellationToken ct = default)
    {
        try
        {
            var (meeting, failure) = await ReadableAsync(meetingId, actorId, ct);
            if (meeting is null) return new(failure);
            if (!Manages(meeting, actorId)) return new(SummaryFailure.Forbidden);
            var row = await db.MeetingSummaries.FirstOrDefaultAsync(s => s.MeetingId == meetingId, ct);
            if (row is null || Read(row) is null) return new(SummaryFailure.NotDrafted);
            var now = clock.GetUtcNow();
            row.ReviewedById = actorId;
            row.ReviewedAt = now;
            row.UpdatedAt = now;
            await db.SaveChangesAsync(ct);
            return await GetAsync(meetingId, actorId, ct);
        }
        catch (Exception error) when (error is not OperationCanceledException)
        {
            logger.LogError(error, "Could not review the summary of meeting {MeetingId}", meetingId);
            return new(SummaryFailure.SaveFailed);
        }
    }

    public async Task<bool> ProcessNextAsync(CancellationToken ct = default)
    {
        var next = await db.MeetingSummaries.AsNoTracking()
            .Where(s => s.Status == SummaryStatus.Queued)
            .OrderBy(s => s.RequestedAt)
            .Select(s => new { s.Id, s.MeetingId })
            .FirstOrDefaultAsync(ct);
        if (next is null) return false;
        // Claim it: only one worker turns Queued into Running.
        var claimed = await db.MeetingSummaries
            .Where(s => s.Id == next.Id && s.Status == SummaryStatus.Queued)
            .ExecuteUpdateAsync(s => s.SetProperty(x => x.Status, SummaryStatus.Running)
                .SetProperty(x => x.UpdatedAt, clock.GetUtcNow()), ct);
        if (claimed == 0) return true;

        string? content = null;
        var error = "";
        try
        {
            var facts = await FactsAsync(next.MeetingId, ct);
            var draft = await new SummaryWriter(model).WriteAsync(facts, ct);
            content = JsonSerializer.Serialize(draft, Json);
        }
        catch (LanguageModelException failure)
        {
            logger.LogWarning(failure, "The AI could not draft meeting {MeetingId}", next.MeetingId);
            error = failure.Message;
        }
        catch (Exception failure) when (failure is not OperationCanceledException)
        {
            logger.LogError(failure, "Drafting meeting {MeetingId} failed", next.MeetingId);
            error = "Không viết được dự thảo. Vui lòng thử lại.";
        }

        var now = clock.GetUtcNow();
        if (content is not null)
        {
            try
            {
                // A new draft replaces the old one whole: what was reviewed or corrected was the old text.
                await db.MeetingSummaries.Where(s => s.Id == next.Id).ExecuteUpdateAsync(s => s
                    .SetProperty(x => x.Status, SummaryStatus.Ready)
                    .SetProperty(x => x.Content, content)
                    .SetProperty(x => x.Model, model.Model)
                    .SetProperty(x => x.GeneratedAt, now)
                    .SetProperty(x => x.ErrorMessage, "")
                    .SetProperty(x => x.ReviewedById, (Guid?)null)
                    .SetProperty(x => x.ReviewedAt, (DateTimeOffset?)null)
                    .SetProperty(x => x.EditedById, (Guid?)null)
                    .SetProperty(x => x.EditedAt, (DateTimeOffset?)null)
                    .SetProperty(x => x.UpdatedAt, now), ct);
                return true;
            }
            catch (Exception failure) when (failure is not OperationCanceledException)
            {
                // Left Running, the job would block every later request and come back after each restart.
                logger.LogError(failure, "Could not store the draft of meeting {MeetingId}", next.MeetingId);
                error = "Không lưu được dự thảo. Vui lòng thử lại.";
            }
        }
        await db.MeetingSummaries.Where(s => s.Id == next.Id).ExecuteUpdateAsync(s => s
            .SetProperty(x => x.Status, SummaryStatus.Failed)
            .SetProperty(x => x.ErrorMessage, error)
            .SetProperty(x => x.UpdatedAt, now), ct);
        return true;
    }

    public Task<int> RequeueInterruptedAsync(CancellationToken ct = default)
        => db.MeetingSummaries.Where(s => s.Status == SummaryStatus.Running)
            .ExecuteUpdateAsync(s => s.SetProperty(x => x.Status, SummaryStatus.Queued), ct);

    private async Task<MeetingFacts> FactsAsync(Guid meetingId, CancellationToken ct)
    {
        var title = await db.Meetings.Where(m => m.Id == meetingId).Select(m => m.Title).SingleAsync(ct);
        var agenda = await db.AgendaItems.AsNoTracking()
            .Where(a => a.MeetingId == meetingId)
            .OrderBy(a => a.Position)
            .Select(a => new AgendaFacts(a.Id, a.Title,
                a.Presenter != null ? a.Presenter.FullName
                    : a.PresenterGuestInvitation != null ? a.PresenterGuestInvitation.SelfDeclaredDisplayName ?? a.PresenterGuestInvitation.InvitedName
                    : a.LegacyPresenterName != "" ? a.LegacyPresenterName : null,
                a.Conclusion, a.ActualStartedAt, a.ActualEndedAt))
            .ToListAsync(ct);
        var documents = await db.MeetingDocuments.AsNoTracking()
            .Where(d => d.MeetingId == meetingId && d.RemovedAt == null)
            .OrderBy(d => d.CreatedAt)
            .Select(d => d.FileName)
            .ToListAsync(ct);
        return new(title, agenda, await VoteLinesAsync(db, meetingId, ct), documents, await transcript.ReadAsync(meetingId, ct));
    }

    /// <summary>AI-eCabinet's minutes readiness: what the draft would be missing if asked for now.</summary>
    private async Task<SummaryReadiness> ReadinessAsync(Guid meetingId, CancellationToken ct)
    {
        var clips = await db.SpeechClips.CountAsync(c => c.MeetingId == meetingId && c.ExcludedAt == null && c.Text != "", ct);
        var transcripts = db.Transcripts.Where(t => t.Recording.MeetingId == meetingId
            && t.Recording.Status == RecordingStatus.Available && t.Recording.DeletedAt == null);
        var segments = await transcripts.Where(t => t.Status == TranscriptStatus.Completed).SelectMany(t => t.Segments).CountAsync(ct);
        var transcribing = await transcripts.CountAsync(t => t.Status == TranscriptStatus.Pending || t.Status == TranscriptStatus.Processing, ct);
        var failed = await transcripts.CountAsync(t => t.Status == TranscriptStatus.Failed, ct);
        var unnamed = await transcripts.Where(t => t.Status == TranscriptStatus.Completed).SelectMany(t => t.Speakers)
            .CountAsync(s => s.AssignedUserId == null && s.AssignedGuestId == null, ct);
        var agenda = await db.AgendaItems.Where(a => a.MeetingId == meetingId).Select(a => a.ActualStartedAt).ToListAsync(ct);
        var votes = await db.VotingSessions.CountAsync(v => v.MeetingId == meetingId && v.Status == VotingStatus.Closed, ct);
        return new(clips + segments, unnamed, transcribing, failed, agenda.Count, agenda.Count(a => a is not null), votes);
    }

    /// <summary>
    /// Each closed vote as one line, straight from the data and never through the model (as AI-eCabinet re-inserts its
    /// ballot lines): counts per option and the Chair's conclusion. Never who voted for what.
    /// </summary>
    internal static async Task<IReadOnlyList<string>> VoteLinesAsync(TrustCabinetDbContext db, Guid meetingId, CancellationToken ct)
    {
        var votes = await db.VotingSessions.AsNoTracking()
            .Where(v => v.MeetingId == meetingId && v.Status == VotingStatus.Closed)
            .OrderBy(v => v.ClosedAt)
            .Select(v => new
            {
                v.Title, v.IsAnonymous, v.Conclusion, v.ConclusionNote,
                Options = v.Options.OrderBy(o => o.Position).Select(o => new { o.Label, o.BallotCount }).ToList(),
            })
            .ToListAsync(ct);
        return [.. votes.Select(v =>
        {
            var counts = v.Options.Sum(o => o.BallotCount) == 0
                ? "chưa có kết quả kiểm phiếu"
                : string.Join(", ", v.Options.Select(o => $"{o.Label} {o.BallotCount} phiếu"));
            var outcome = v.Conclusion switch
            {
                VoteConclusion.Passed => "Thông qua",
                VoteConclusion.NotPassed => "Không thông qua",
                VoteConclusion.Deferred => "Hoãn",
                _ => "chưa ghi nhận",
            };
            var note = string.IsNullOrWhiteSpace(v.ConclusionNote) ? "" : $" ({v.ConclusionNote.Trim()})";
            return $"{v.Title}{(v.IsAnonymous ? " (bỏ phiếu kín)" : "")}: {counts}. Kết luận: {outcome}{note}.";
        })];
    }

    private static Dictionary<string, string[]> Validate(SummaryEdit edit)
    {
        var errors = new Dictionary<string, string[]>();
        void Check(string field, string? text, int max)
        {
            if (string.IsNullOrWhiteSpace(text) || text.Length > max) errors[field] = [$"Nhập nội dung, tối đa {max:N0} ký tự."];
        }
        Check("objectives", edit.Objectives, SummaryLimits.Objectives);
        Check("discussion", edit.Discussion, SummaryLimits.Discussion);
        Check("conclusions", edit.Conclusions, SummaryLimits.Conclusions);
        return errors;
    }

    internal static SummaryDraft? Read(MeetingSummary row)
    {
        // A rerun keeps the last draft readable until the new one lands.
        if (string.IsNullOrEmpty(row.Content)) return null;
        try
        {
            return JsonSerializer.Deserialize<SummaryDraft>(row.Content, Json);
        }
        catch (JsonException)
        {
            return null;
        }
    }

    private static SummaryView View(MeetingSummary row) => new(row.Status.ToString(), Read(row), row.Model,
        string.IsNullOrEmpty(row.Content) ? null : row.GeneratedAt, row.RequestedAt, Person(row.RequestedBy), row.ErrorMessage,
        Person(row.ReviewedBy), row.ReviewedAt, Person(row.EditedBy), row.EditedAt);

    private static SummaryPerson? Person(User? user) => user is null ? null : new(user.Id, user.FullName);

    private IQueryable<MeetingSummary> Query() => db.MeetingSummaries.AsNoTracking()
        .Include(s => s.RequestedBy).Include(s => s.ReviewedBy).Include(s => s.EditedBy);

    /// <summary>The current Chair or Secretary (BR-357's pair): they own the record of the meeting.</summary>
    internal static bool Manages(Meeting meeting, Guid actor) => meeting.Participants.Any(p => p.MeetingId == meeting.Id
        && p.UserId == actor && (p.Role == MeetingRole.Chair || p.Role == MeetingRole.Secretary) && ParticipantRules.Active(p));

    private async Task<(Meeting? Meeting, SummaryFailure Failure)> ReadableAsync(Guid meetingId, Guid actorId, CancellationToken ct)
    {
        if (!await meetings.IsUserActiveAsync(actorId, ct)) return (null, SummaryFailure.Inactive);
        var meeting = await meetings.FindWorkflowAsync(meetingId, false, ct);
        if (meeting is null || !MeetingReadPolicy.CanRead(meeting, actorId)) return (null, SummaryFailure.NotFound);
        return (meeting, SummaryFailure.None);
    }
}
