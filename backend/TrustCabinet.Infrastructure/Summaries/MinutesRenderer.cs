using TrustCabinet.Application.Summaries;

namespace TrustCabinet.Infrastructure.Summaries;

/// <summary><see cref="MinutesHtml"/> (AngleSharp), <see cref="MinutesPdf"/> (QuestPDF) and <see cref="MinutesDocx"/>, unchanged.</summary>
public sealed class MinutesRenderer : IMinutesRenderer
{
    public string FromLines(string lineText) => MinutesHtml.FromLines(lineText);

    public bool TooDeep(string html) => MinutesHtml.TooDeep(html);

    public string Sanitize(string html) => MinutesHtml.Sanitize(html);

    public bool HasText(string html) => MinutesHtml.HasText(html);

    public string Normalize(string content) => MinutesHtml.Normalize(content);

    public byte[] BuildPdf(string content) => MinutesPdf.Build(content);

    public byte[] BuildDocx(string content) => MinutesDocx.Build(content);
}
