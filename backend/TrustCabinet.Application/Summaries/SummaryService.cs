using System.Text.Encodings.Web;
using System.Text.Json;
using System.Text.Unicode;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using TrustCabinet.Application.Abstractions;
using TrustCabinet.Application.Ai;
using TrustCabinet.Application.Meetings;
using TrustCabinet.Domain.Entities;
using TrustCabinet.Domain.Enums;
using TrustCabinet.Domain.Meetings;
using TrustCabinet.Domain.Participants;
using TrustCabinet.Domain.Summaries;

namespace TrustCabinet.Application.Summaries;

/// <summary>
/// SCRUM-36 — the AI draft of a meeting. The current Chair or Secretary asks for it once the meeting has started, reads
/// and corrects it, and reviews it; only then do the other participants see it. The AI writes nothing else: the minutes
/// (SCRUM-32) take the draft as their starting point, and a person is answerable for what they say.
/// </summary>
public sealed class SummaryService(IMeetingSummaryRepository summaries, ISummarySourceQuery sources, IUnitOfWork unitOfWork,
    IMeetingRepository meetings, ILanguageModelProvider models, MeetingTranscriptReader transcript, TimeProvider clock,
    ILogger<SummaryService> logger, IOptions<LanguageModelOptions> aiOptions) : ISummaryService
{
    /// <summary>ADR-0056 — the hosted harness: whole agenda items, <c>Ai:ApiParallelism</c> of them at once.</summary>
    private Task<SummaryDraft> WriteThroughApiAsync(ILanguageModel model, MeetingFacts facts, CancellationToken ct)
        => new SummaryWriter(model, SummaryWriter.ApiChunkChars, SummaryWriter.ApiSpellingRule)
            .WritePartsAsync(facts, Math.Clamp(aiOptions.Value.ApiParallelism, 1, 8), ct);

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
            return await ReadAsync(async token =>
            {
                var (meeting, failure) = await ReadableAsync(meetingId, actorId, token);
                if (meeting is null) return new SummaryOutcome(failure);
                var manages = Manages(meeting, actorId);
                var row = await summaries.FindWithPeopleAsync(meetingId, token);
                var visible = row is not null && (manages || row.ReviewedAt is not null);
                // Ready for this meeting: an ordinary one may be written by the API, any other only by the in-house model.
                var ready = (await models.ForAsync(meeting.Sensitivity, token)).IsConfigured;
                return new(Data: new SummaryState(visible ? View(row!) : null, manages, ready,
                    manages ? await sources.ReadinessAsync(meetingId, token) : null));
            }, ct);
        }
        catch (Exception error) when (error is not OperationCanceledException)
        {
            logger.LogError((error as PersistenceException)?.InnerException ?? error, "Could not read the summary of meeting {MeetingId}", meetingId);
            return new(SummaryFailure.ReadFailed);
        }
    }

    public async Task<SummaryOutcome> RequestAsync(Guid meetingId, Guid actorId, CancellationToken ct = default)
    {
        try
        {
            return await ReadAsync(async token =>
            {
                var (meeting, failure) = await ReadableAsync(meetingId, actorId, token);
                if (meeting is null) return new SummaryOutcome(failure);
                if (!Manages(meeting, actorId)) return new(SummaryFailure.Forbidden);
                if (meeting.Status is not (MeetingStatus.InProgress or MeetingStatus.Ended)) return new(SummaryFailure.WrongMeetingStatus);
                if (!(await models.ForAsync(meeting.Sensitivity, token)).IsConfigured) return new(SummaryFailure.AiUnavailable);
                var row = await summaries.FindByMeetingAsync(meetingId, true, token);
                if (row is { Status: SummaryStatus.Queued or SummaryStatus.Running }) return new(SummaryFailure.Busy);
                var now = clock.GetUtcNow();
                if (row is null)
                {
                    row = new MeetingSummary { MeetingId = meetingId };
                    summaries.Add(row);
                }
                row.Status = SummaryStatus.Queued;
                row.ErrorMessage = "";
                row.RequestedById = actorId;
                row.RequestedAt = now;
                row.UpdatedAt = now;
                await unitOfWork.SaveChangesAsync(token);
                return await GetAsync(meetingId, actorId, token);
            }, ct);
        }
        catch (UniqueConflictException error)
        {
            // Two requests at once: the unique meeting index lets one through, the other reads as busy.
            logger.LogWarning(error.InnerException ?? error, "Concurrent summary request for meeting {MeetingId}", meetingId);
            return new(SummaryFailure.Busy);
        }
        catch (Exception error) when (error is not OperationCanceledException)
        {
            logger.LogError((error as PersistenceException)?.InnerException ?? error, "Could not queue the summary of meeting {MeetingId}", meetingId);
            return new(SummaryFailure.SaveFailed);
        }
    }

    public async Task<SummaryOutcome> EditAsync(Guid meetingId, Guid actorId, SummaryEdit edit, CancellationToken ct = default)
    {
        try
        {
            return await ReadAsync(async token =>
            {
                var (meeting, failure) = await ReadableAsync(meetingId, actorId, token);
                if (meeting is null) return new SummaryOutcome(failure);
                if (!Manages(meeting, actorId)) return new(SummaryFailure.Forbidden);
                var row = await summaries.FindByMeetingAsync(meetingId, true, token);
                var draft = row is null ? null : Read(row);
                if (row is null || draft is null) return new(SummaryFailure.NotDrafted);
                if (row.Status is SummaryStatus.Queued or SummaryStatus.Running) return new(SummaryFailure.Busy);
                var errors = Validate(edit);
                if (errors.Count > 0) return new(SummaryFailure.Invalid, Errors: errors);
                var content = JsonSerializer.Serialize(draft with
                {
                    Objectives = SummaryWriter.Tidy(edit.Objectives!), Discussion = SummaryWriter.Tidy(edit.Discussion!),
                    Conclusions = SummaryWriter.Tidy(edit.Conclusions!),
                }, Json);
                var now = clock.GetUtcNow();
                // Not while the AI writes it again: its draft would replace this edit unannounced, and minutes built from the
                // edit would date from after the new draft's reading and never read as behind. Checked in the write itself,
                // so a request queued since the read above still wins.
                var saved = await summaries.SaveEditAsync(row.Id, content, actorId, now, token);
                if (saved == 0) return new(SummaryFailure.Busy);
                return await GetAsync(meetingId, actorId, token);
            }, ct);
        }
        catch (Exception error) when (error is not OperationCanceledException)
        {
            logger.LogError((error as PersistenceException)?.InnerException ?? error, "Could not save the summary of meeting {MeetingId}", meetingId);
            return new(SummaryFailure.SaveFailed);
        }
    }

    public async Task<SummaryOutcome> ReviewAsync(Guid meetingId, Guid actorId, CancellationToken ct = default)
    {
        try
        {
            return await ReadAsync(async token =>
            {
                var (meeting, failure) = await ReadableAsync(meetingId, actorId, token);
                if (meeting is null) return new SummaryOutcome(failure);
                if (!Manages(meeting, actorId)) return new(SummaryFailure.Forbidden);
                var row = await summaries.FindByMeetingAsync(meetingId, true, token);
                if (row is null || Read(row) is null) return new(SummaryFailure.NotDrafted);
                // Nor reviewed while the AI writes it again: the mark would end up on a text nobody has read.
                if (row.Status is SummaryStatus.Queued or SummaryStatus.Running) return new(SummaryFailure.Busy);
                var now = clock.GetUtcNow();
                var saved = await summaries.MarkReviewedAsync(row.Id, actorId, now, token);
                if (saved == 0) return new(SummaryFailure.Busy);
                return await GetAsync(meetingId, actorId, token);
            }, ct);
        }
        catch (Exception error) when (error is not OperationCanceledException)
        {
            logger.LogError((error as PersistenceException)?.InnerException ?? error, "Could not review the summary of meeting {MeetingId}", meetingId);
            return new(SummaryFailure.SaveFailed);
        }
    }

    public async Task<bool> ProcessNextAsync(CancellationToken ct = default)
    {
        var next = await ReadAsync(summaries.FindNextQueuedAsync, ct);
        if (next is null) return false;
        // Claim it: only one worker turns Queued into Running.
        var claimed = await ReadAsync(token => summaries.ClaimAsync(next.Id, clock.GetUtcNow(), token), ct);
        if (claimed == 0) return true;

        string? content = null;
        ILanguageModel? model = null;
        var error = "";
        // The draft is of the record as it stood now, when it is read: a line corrected while the model writes (minutes
        // on a laptop) is not in it, so the draft must read as behind that correction (readiness.transcriptChangedAt).
        var readAt = clock.GetUtcNow();
        try
        {
            var facts = await ReadAsync(token => FactsAsync(next.MeetingId, token), ct);
            // Chosen now, as the job runs: a meeting marked Internal or Secret since it was queued stays on the in-house model.
            var sensitivity = await ReadAsync(token => sources.FindSensitivityAsync(next.MeetingId, token), ct);
            model = await models.ForAsync(sensitivity, ct);
            // The in-house model keeps its own harness untouched; a hosted API reads whole agenda items, side by side or in one call.
            var draft = model.Remote
                ? await WriteThroughApiAsync(model, facts, ct)
                : await new SummaryWriter(model).WriteAsync(facts, ct);
            content = JsonSerializer.Serialize(draft, Json);
        }
        catch (LanguageModelException failure)
        {
            logger.LogWarning(failure, "The AI could not draft meeting {MeetingId}", next.MeetingId);
            error = failure.Message;
        }
        catch (Exception failure) when (failure is not OperationCanceledException)
        {
            logger.LogError((failure as PersistenceException)?.InnerException ?? failure, "Drafting meeting {MeetingId} failed", next.MeetingId);
            error = "Không viết được dự thảo. Vui lòng thử lại.";
        }

        var now = clock.GetUtcNow();
        if (content is not null)
        {
            try
            {
                // A new draft replaces the old one whole: what was reviewed or corrected was the old text.
                await ReadAsync(token => summaries.StoreDraftAsync(next.Id, content, model!.Model, readAt, now, token), ct);
                return true;
            }
            catch (Exception failure) when (failure is not OperationCanceledException)
            {
                // Left Running, the job would block every later request and come back after each restart.
                logger.LogError((failure as PersistenceException)?.InnerException ?? failure, "Could not store the draft of meeting {MeetingId}", next.MeetingId);
                error = "Không lưu được dự thảo. Vui lòng thử lại.";
            }
        }
        await ReadAsync(token => summaries.MarkFailedAsync(next.Id, error, now, token), ct);
        return true;
    }

    public Task<int> RequeueInterruptedAsync(CancellationToken ct = default)
        => ReadAsync(summaries.RequeueRunningAsync, ct);

    private async Task<MeetingFacts> FactsAsync(Guid meetingId, CancellationToken ct)
    {
        var title = await sources.FindTitleAsync(meetingId, ct);
        var agenda = await sources.ListAgendaAsync(meetingId, ct);
        var documents = await sources.ListDocumentNamesAsync(meetingId, ct);
        return new(title, agenda, await VoteLinesAsync(sources, meetingId, ct), documents, await transcript.ReadAsync(meetingId, ct));
    }

    /// <summary>
    /// Each closed vote as one line, straight from the data and never through the model (as AI-eCabinet re-inserts its
    /// ballot lines): counts per option and the Chair's conclusion. Never who voted for what.
    /// </summary>
    internal static async Task<IReadOnlyList<string>> VoteLinesAsync(ISummarySourceQuery sources, Guid meetingId, CancellationToken ct)
    {
        var votes = await sources.ListClosedVotesAsync(meetingId, ct);
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

    /// <summary>Repository calls with no transaction of their own, a few conditional writes among them, as before.</summary>
    private Task<T> ReadAsync<T>(Func<CancellationToken, Task<T>> work, CancellationToken ct)
        => unitOfWork.ExecuteAsync(work, TransactionMode.None, ct);
}
