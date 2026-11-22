using TrustCabinet.Application.Ai;
using TrustCabinet.Application.Summaries;
using TrustCabinet.Domain.Entities;
using TrustCabinet.Domain.Enums;

namespace TrustCabinet.Application.Tests.Fakes;

/// <summary>A language model that answers every prompt with <see cref="Answer"/> and keeps what it was asked.</summary>
public sealed class FakeLanguageModel(string model = "fake-llm", bool remote = false) : ILanguageModel
{
    public bool Configured { get; set; } = true;
    public string Answer { get; set; } = "MỤC TIÊU:\nRà soát tiến độ.\nKẾT LUẬN:\nThống nhất.\nNHIỆM VỤ:\nKhông có.";
    public Exception? Fail { get; set; }
    public List<string> Systems { get; } = [];

    public bool IsConfigured => Configured;
    public string Model => model;
    public bool Remote => remote;

    public Task<string> CompleteAsync(string system, string user, CancellationToken ct)
    {
        Systems.Add(system);
        return Fail is { } failure ? Task.FromException<string>(failure) : Task.FromResult(Answer);
    }
}

public sealed class FakeLanguageModelProvider(ILanguageModel model) : ILanguageModelProvider
{
    public ILanguageModel Model { get; set; } = model;
    public Task<ILanguageModel> ForAsync(Sensitivity sensitivity, CancellationToken ct) => Task.FromResult(Model);
}

/// <summary>Reversible and visibly not the plain text: the key reversed, behind a marker byte.</summary>
public sealed class FakeAiKeyProtector : IAiKeyProtector
{
    public bool Broken { get; set; }

    public byte[] Protect(string apiKey, Guid settingId) => [0xAE, .. System.Text.Encoding.UTF8.GetBytes(new string([.. apiKey.Reverse()]))];

    public string Unprotect(byte[] ciphertext, Guid settingId)
        => Broken ? throw new System.Security.Cryptography.CryptographicException("changed key")
            : new string([.. System.Text.Encoding.UTF8.GetString(ciphertext[1..]).Reverse()]);
}

public sealed class FakeApiLanguageModelFactory : IApiLanguageModelFactory
{
    public List<(string BaseUrl, string Model, string ApiKey, int TimeoutMinutes, int MaxTokens, string? ReasoningEffort)> Created { get; } = [];
    public FakeLanguageModel? Last { get; private set; }

    public ILanguageModel Create(string baseUrl, string model, string apiKey, int timeoutMinutes, int maxTokens,
        string? reasoningEffort)
    {
        Created.Add((baseUrl, model, apiKey, timeoutMinutes, maxTokens, reasoningEffort));
        return Last = new FakeLanguageModel(model, remote: true);
    }
}

public sealed class FakeAiProviderSettingRepository : IAiProviderSettingRepository
{
    public AiProviderSetting? Row { get; set; }
    public Exception? Fail { get; set; }

    public Task<AiProviderSetting?> FindAsync(Guid id, bool tracked, CancellationToken ct)
        => Fail is { } failure ? Task.FromException<AiProviderSetting?>(failure) : Task.FromResult(Row?.Id == id ? Row : null);

    public Task<AiProviderSetting?> FindCurrentAsync(CancellationToken ct) => Task.FromResult(Row);

    public Task<AiSettingsRow?> FindViewAsync(Guid id, CancellationToken ct)
        => Fail is { } failure ? Task.FromException<AiSettingsRow?>(failure)
            : Task.FromResult(Row is { } r && r.Id == id
                ? new AiSettingsRow(r.Mode, r.ApiBaseUrl, r.ApiModel, r.ApiKeyCiphertext != null, r.ApiKeyHint, r.UpdatedAt, null)
                : null);

    public void Add(AiProviderSetting setting) => Row = setting;
}

/// <summary>The summary rows in memory; the conditional writes check the status as the SQL does.</summary>
public sealed class FakeMeetingSummaryRepository : IMeetingSummaryRepository
{
    public List<MeetingSummary> Rows { get; } = [];
    public Exception? Fail { get; set; }

    public Task<MeetingSummary?> FindByMeetingAsync(Guid meetingId, bool tracked, CancellationToken ct)
        => Fail is { } failure ? Task.FromException<MeetingSummary?>(failure) : Task.FromResult(Rows.SingleOrDefault(s => s.MeetingId == meetingId));

    public Task<MeetingSummary?> FindWithPeopleAsync(Guid meetingId, CancellationToken ct) => FindByMeetingAsync(meetingId, false, ct);

    public void Add(MeetingSummary summary) => Rows.Add(summary);

    private Task<int> Change(Guid id, Func<MeetingSummary, bool> when, Action<MeetingSummary> change)
    {
        if (Fail is { } failure) return Task.FromException<int>(failure);
        var row = Rows.SingleOrDefault(s => s.Id == id);
        if (row is null || !when(row)) return Task.FromResult(0);
        change(row);
        return Task.FromResult(1);
    }

    private static bool Idle(MeetingSummary s) => s.Status is not (SummaryStatus.Queued or SummaryStatus.Running);

    public Task<int> SaveEditAsync(Guid id, string content, Guid actorId, DateTimeOffset now, CancellationToken ct)
        => Change(id, Idle, s => { s.Content = content; s.EditedById = actorId; s.EditedAt = now; s.ReviewedById = null; s.ReviewedAt = null; });

    public Task<int> MarkReviewedAsync(Guid id, Guid actorId, DateTimeOffset now, CancellationToken ct)
        => Change(id, Idle, s => { s.ReviewedById = actorId; s.ReviewedAt = now; });

    public Task<QueuedSummary?> FindNextQueuedAsync(CancellationToken ct)
        => Task.FromResult(Rows.Where(s => s.Status == SummaryStatus.Queued).OrderBy(s => s.RequestedAt)
            .Select(s => new QueuedSummary(s.Id, s.MeetingId)).FirstOrDefault());

    /// <summary>Another worker claims every draft first: the claim answers 0 and the row is already Running.</summary>
    public bool LoseClaims { get; set; }

    public Task<int> ClaimAsync(Guid id, DateTimeOffset now, CancellationToken ct)
    {
        if (!LoseClaims) return Change(id, s => s.Status == SummaryStatus.Queued, s => s.Status = SummaryStatus.Running);
        Rows.Single(s => s.Id == id).Status = SummaryStatus.Running;
        return Task.FromResult(0);
    }

    /// <summary>Thrown by storing a finished draft: the store rejects it.</summary>
    public Exception? StoreDraftFails { get; set; }

    public Task<int> StoreDraftAsync(Guid id, string content, string model, DateTimeOffset generatedAt, DateTimeOffset now,
        CancellationToken ct)
        => StoreDraftFails is { } failure ? Task.FromException<int>(failure) : Change(id, _ => true, s => { s.Status = SummaryStatus.Ready; s.Content = content; s.Model = model; s.GeneratedAt = generatedAt; });

    public Task<int> MarkFailedAsync(Guid id, string error, DateTimeOffset now, CancellationToken ct)
    {
        var row = Rows.Single(s => s.Id == id);
        row.Status = SummaryStatus.Failed;
        row.ErrorMessage = error;
        return Task.FromResult(1);
    }

    public Task<int> RequeueRunningAsync(CancellationToken ct)
    {
        var running = Rows.Where(s => s.Status == SummaryStatus.Running).ToList();
        running.ForEach(s => s.Status = SummaryStatus.Queued);
        return Task.FromResult(running.Count);
    }
}

public sealed class FakeSummarySourceQuery : ISummarySourceQuery
{
    public string Title { get; set; } = "Họp giao ban";
    public Sensitivity Sensitivity { get; set; } = Sensitivity.Normal;
    public List<AgendaFacts> Agenda { get; } = [];
    public List<ClosedVote> Votes { get; } = [];
    public SummaryReadiness Readiness { get; set; } = new(0, 0, 0, 0, 0, 0, 0, null);

    public Task<string> FindTitleAsync(Guid meetingId, CancellationToken ct) => Task.FromResult(Title);
    public Task<Sensitivity> FindSensitivityAsync(Guid meetingId, CancellationToken ct) => Task.FromResult(Sensitivity);
    public Task<IReadOnlyList<AgendaFacts>> ListAgendaAsync(Guid meetingId, CancellationToken ct) => Task.FromResult<IReadOnlyList<AgendaFacts>>(Agenda);
    public Task<IReadOnlyList<string>> ListDocumentNamesAsync(Guid meetingId, CancellationToken ct) => Task.FromResult<IReadOnlyList<string>>([]);
    public Task<IReadOnlyList<ClosedVote>> ListClosedVotesAsync(Guid meetingId, CancellationToken ct) => Task.FromResult<IReadOnlyList<ClosedVote>>(Votes);
    public int Sentences { get; set; } = 1;
    public Task<int> CountSentencesAsync(Guid meetingId, CancellationToken ct) => Task.FromResult(Sentences);
    public Task<SummaryReadiness> ReadinessAsync(Guid meetingId, CancellationToken ct) => Task.FromResult(Readiness);
}

public sealed class FakeMeetingTranscriptQuery : IMeetingTranscriptQuery
{
    public List<SpokenClip> Clips { get; } = [];

    public Task<bool> IsHybridAsync(Guid meetingId, CancellationToken ct) => Task.FromResult(false);
    public Task<IReadOnlyList<SpokenClip>> ListClipsAsync(Guid meetingId, CancellationToken ct) => Task.FromResult<IReadOnlyList<SpokenClip>>(Clips);
    public Task<IReadOnlyList<TranscribedRecording>> ListRecordingsAsync(Guid meetingId, CancellationToken ct) => Task.FromResult<IReadOnlyList<TranscribedRecording>>([]);
    public Task<IReadOnlyDictionary<string, string?>> ListSpeakerNamesAsync(Guid transcriptId, CancellationToken ct) => Task.FromResult<IReadOnlyDictionary<string, string?>>(new Dictionary<string, string?>());
    public Task<IReadOnlyList<SpokenSegment>> ListSegmentsAsync(Guid transcriptId, CancellationToken ct) => Task.FromResult<IReadOnlyList<SpokenSegment>>([]);
}

/// <summary>The minutes revisions in memory; <see cref="Locked"/> records each revision read under its row lock.</summary>
public sealed class FakeMeetingMinutesRepository : IMeetingMinutesRepository
{
    public List<MeetingMinutes> Rows { get; } = [];
    public List<Guid> Locked { get; } = [];
    public Exception? Fail { get; set; }

    public Task<MeetingMinutes?> FindLatestAsync(Guid meetingId, CancellationToken ct)
        => Task.FromResult(Rows.Where(m => m.MeetingId == meetingId).OrderByDescending(m => m.RevisionNumber).FirstOrDefault());

    public Task<MeetingMinutes?> FindAsync(Guid minutesId, CancellationToken ct) => Task.FromResult(Rows.SingleOrDefault(m => m.Id == minutesId));
    public Task<MeetingMinutes> GetAsync(Guid minutesId, CancellationToken ct) => Task.FromResult(Rows.Single(m => m.Id == minutesId));
    public Task<MeetingMinutes?> FindWithPeopleAsync(Guid minutesId, CancellationToken ct) => FindAsync(minutesId, ct);

    public Task<Guid?> FindMeetingIdAsync(Guid minutesId, CancellationToken ct)
        => Task.FromResult(Rows.SingleOrDefault(m => m.Id == minutesId)?.MeetingId);

    /// <summary>Runs as each revision lock is taken, to see what the caller already holds.</summary>
    public Action? BeforeLock { get; set; }

    public Task<MeetingMinutes?> LockAsync(Guid minutesId, CancellationToken ct)
    {
        BeforeLock?.Invoke();
        Locked.Add(minutesId);
        return FindAsync(minutesId, ct);
    }

    public Task<bool> HasLaterRevisionAsync(Guid meetingId, int revisionNumber, CancellationToken ct)
        => Task.FromResult(Rows.Any(m => m.MeetingId == meetingId && m.RevisionNumber > revisionNumber));

    public Task<IReadOnlyList<MinutesRevisionRow>> ListRevisionsAsync(Guid meetingId, bool manages, CancellationToken ct)
        => Task.FromResult<IReadOnlyList<MinutesRevisionRow>>([.. Rows
            .Where(m => m.MeetingId == meetingId && (manages || m.Status is MinutesStatus.Issued or MinutesStatus.Superseded))
            .OrderByDescending(m => m.RevisionNumber)
            .Select(m => new MinutesRevisionRow(m.Id, m.RevisionNumber, m.Status, m.IssuedAt))]);

    public Task<int> SaveContentAsync(Guid minutesId, string content, DateTimeOffset from, DateTimeOffset to, DateTimeOffset now,
        CancellationToken ct)
    {
        if (Fail is { } failure) return Task.FromException<int>(failure);
        var row = Rows.SingleOrDefault(m => m.Id == minutesId && m.Status is MinutesStatus.Draft or MinutesStatus.Rejected
            && m.UpdatedAt >= from && m.UpdatedAt <= to);
        if (row is null) return Task.FromResult(0);
        row.Content = content;
        row.UpdatedAt = now;
        return Task.FromResult(1);
    }

    public void Add(MeetingMinutes minutes) => Rows.Add(minutes);
}

public sealed class FakeMinutesSourceQuery : IMinutesSourceQuery
{
    public List<MinutesAttendee> Attendees { get; } = [];
    public HashSet<Guid> ActiveUsers { get; } = [];

    public Task<IReadOnlyList<MinutesAttendee>> ListAttendeesAsync(Guid meetingId, CancellationToken ct) => Task.FromResult<IReadOnlyList<MinutesAttendee>>(Attendees);
    public Task<IReadOnlyList<MinutesGuest>> ListGuestsAsync(Guid meetingId, CancellationToken ct) => Task.FromResult<IReadOnlyList<MinutesGuest>>([]);
    public Task<IReadOnlyList<MinutesNote>> ListNotesAsync(Guid meetingId, IReadOnlyCollection<Guid> authorIds, CancellationToken ct) => Task.FromResult<IReadOnlyList<MinutesNote>>([]);
    public Task<IReadOnlyList<Guid>> ListActiveUsersAsync(IReadOnlyCollection<Guid> userIds, CancellationToken ct)
        => Task.FromResult<IReadOnlyList<Guid>>([.. userIds.Where(ActiveUsers.Contains)]);

    /// <summary>The reader ids each "minutes issued" email lookup was asked about, in order.</summary>
    public List<Guid[]> EmailLookups { get; } = [];

    /// <summary>How many times the Chair's and organizer's names were read.</summary>
    public int NameLookups { get; private set; }

    /// <summary>The active readers, each named "Người đọc" with an address made from the id.</summary>
    public Task<IReadOnlyList<MinutesEmailRecipient>> ListEmailRecipientsAsync(Guid[] readerIds, CancellationToken ct)
    {
        EmailLookups.Add(readerIds);
        return Task.FromResult<IReadOnlyList<MinutesEmailRecipient>>(readerIds.Where(ActiveUsers.Contains)
            .Select(id => new MinutesEmailRecipient(id, "Người đọc", $"{id:N}@example.vn")).ToList());
    }

    public Task<MinutesIssuedNames> GetIssuedEmailNamesAsync(Guid chairId, Guid organizerId, CancellationToken ct)
    {
        NameLookups++;
        return Task.FromResult(new MinutesIssuedNames("Nguyễn Chủ Toạ", "Trần Tổ Chức", "organizer@example.vn"));
    }
}

/// <summary>Leaves the content as it is: the allow-list itself is tested against AngleSharp in the web tests.</summary>
public sealed class FakeMinutesRenderer : IMinutesRenderer
{
    public string FromLines(string lineText) => $"<p>{lineText}</p>";
    public bool TooDeep(string html) => false;
    public string Sanitize(string html) => html;
    public bool HasText(string html) => html.Replace("<p>", "").Replace("</p>", "").Trim().Length > 0;
    public string Normalize(string content) => content;
    public byte[] BuildPdf(string content) => [0x25, 0x50, 0x44, 0x46];
    public byte[] BuildDocx(string content) => [0x50, 0x4B];
}
