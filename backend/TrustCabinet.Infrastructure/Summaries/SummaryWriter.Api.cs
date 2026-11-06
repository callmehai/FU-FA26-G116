using System.Text;
using TrustCabinet.Domain.Summaries;

namespace TrustCabinet.Infrastructure.Summaries;

/// <summary>
/// ADR-0056 — the writer's harness for a hosted model (DeepSeek V4.1 Flash reads a million tokens). The in-house path,
/// <see cref="WriteAsync"/>, is untouched: this only runs when the draft goes to an API. Same prompts as the in-house
/// path, but each agenda item's talk is read in one call (no 9,000-character pieces to merge) and up to
/// <c>parallelism</c> items are written at once. AI-eCabinet's shape, the whole meeting in one call, was measured on the
/// eight bench meetings (07/11/2026) and was no faster, so it was not kept.
/// </summary>
public sealed partial class SummaryWriter
{
    /// <summary>About 80,000 tokens of Vietnamese: three hours of talk is one call, far inside a hosted model's context.</summary>
    internal const int ApiChunkChars = 200_000;

    public async Task<SummaryDraft> WritePartsAsync(MeetingFacts facts, int parallelism, CancellationToken ct)
    {
        var parts = Split(facts);
        var texts = new string[parts.Count];
        using var gate = new SemaphoreSlim(Math.Max(1, parallelism));
        await Task.WhenAll(parts.Select(async (part, i) =>
        {
            if (part.Lines.Count == 0)
            {
                texts[i] = NoDiscussion;
                return;
            }
            await gate.WaitAsync(ct);
            try
            {
                texts[i] = await SummarisePartAsync(facts, part, ct);
            }
            finally
            {
                gate.Release();
            }
        }));
        var written = parts.Select((p, i) => (p.Title, Presented(p, texts[i]))).ToList();
        var (objectives, conclusions, tasks) = await OverviewAsync(facts, written, ct);
        return Compose(facts, parts, written, objectives, conclusions, tasks);
    }

    /// <summary>The in-house path's rule for an item its presenter introduced and nobody discussed.</summary>
    private static string Presented(Part part, string text)
        => part.Presenter is not null && part.Lines.Count == 0 ? $"{part.Presenter} trình bày nội dung. {NoDiscussion}" : text;

    /// <summary>Sections I–III from the parts' texts, as <see cref="WriteAsync"/> lays them out.</summary>
    private static SummaryDraft Compose(MeetingFacts facts, IReadOnlyList<Part> parts,
        IReadOnlyList<(string Title, string Text)> written, string objectives, string conclusions, string tasks)
    {
        var discussion = new StringBuilder();
        for (var i = 0; i < parts.Count; i++)
        {
            if (i > 0) discussion.Append('\n');
            discussion.Append(NoAgenda(facts) && parts[i].Lines.Count > 0
                ? Issues(written[i].Text) : $"{i + 1}. {written[i].Title}\n{written[i].Text}");
        }
        var third = new StringBuilder().AppendLine(ChairHeading).AppendLine(conclusions);
        if (facts.Votes.Count > 0)
        {
            third.AppendLine(BallotsHeading);
            foreach (var vote in facts.Votes) third.AppendLine($"- {vote}");
        }
        third.AppendLine(TasksHeading).Append(tasks);
        var sources = parts.Select(p => new SummarySource(p.AgendaItemId, p.Title,
            p.Lines.Count > 0 ? p.Lines[0].At : null, p.Lines.Count > 0 ? p.Lines[^1].At : null, p.Lines.Count)).ToList();
        return new(Tidy(objectives), Tidy(discussion.ToString()), Tidy(third.ToString()), sources);
    }
}
