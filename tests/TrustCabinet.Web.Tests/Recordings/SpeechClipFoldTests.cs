using System.Text.Json;
using TrustCabinet.Domain.Recordings;

namespace TrustCabinet.Web.Tests.Recordings;

/// <summary>
/// SCRUM-109 — what one person's mic overheard from another. The cases live in speech-fold-cases.json, which
/// workers/speech/tests/test_fold_port.py also runs, so the Python copy that measured the rule marks what this marks.
/// </summary>
public sealed class SpeechClipFoldTests
{
    private static readonly string CasesPath = Path.Combine(FindRepoRoot(), "tests", "TrustCabinet.Web.Tests", "Recordings",
        "speech-fold-cases.json");

    public static TheoryData<string> Cases()
    {
        var data = new TheoryData<string>();
        using var json = JsonDocument.Parse(File.ReadAllText(CasesPath));
        foreach (var item in json.RootElement.GetProperty("cases").EnumerateArray()) data.Add(item.GetProperty("name").GetString()!);
        return data;
    }

    [Theory]
    [MemberData(nameof(Cases))]
    public void Marks_what_the_shared_case_expects(string name)
    {
        using var json = JsonDocument.Parse(File.ReadAllText(CasesPath));
        var item = json.RootElement.GetProperty("cases").EnumerateArray().Single(c => c.GetProperty("name").GetString() == name);
        var clips = item.GetProperty("clips").EnumerateArray().Select(c => new FoldClip(
            c.GetProperty("id").GetGuid(), c.GetProperty("speaker").GetGuid(), c.GetProperty("offsetMs").GetInt64(),
            c.GetProperty("durationMs").GetInt64(), c.GetProperty("text").GetString()!,
            [.. c.GetProperty("segments").EnumerateArray().Select(s => new SpeechClipSegment(s[0].GetInt64(), s[1].GetInt64(),
                s[2].GetInt32(), s[3].ValueKind == JsonValueKind.Null ? null : s[3].GetDouble()))],
            c.GetProperty("excluded").GetBoolean(), c.GetProperty("edited").GetBoolean())).ToList();

        var marks = SpeechClipFold.Fold(clips);

        var expected = item.GetProperty("expect").EnumerateObject().ToDictionary(p => Guid.Parse(p.Name), p => (
            LeakOf: p.Value.GetProperty("leakOf").ValueKind == JsonValueKind.Null ? (Guid?)null : p.Value.GetProperty("leakOf").GetGuid(),
            Spans: p.Value.GetProperty("spans").EnumerateArray()
                .Select(s => new LeakSpan(s[0].GetInt64(), s[1].GetInt64(), s[2].GetGuid())).ToList()));
        Assert.Equal(expected.Keys.Order(), marks.Keys.Order());
        foreach (var (id, mark) in marks)
        {
            Assert.Equal(expected[id].LeakOf, mark.LeakOf);
            Assert.Equal(expected[id].Spans, mark.Spans);
        }
    }

    [Fact]
    public void Words_are_compared_without_case_or_punctuation()
    {
        var tokens = SpeechClipFold.Tokens("Xin chào, các anh chị. Xin CHÀO?");
        Assert.Equal(2, tokens["xin"]);
        Assert.Equal(2, tokens["chào"]);
        Assert.Equal(5, tokens.Count);
    }

    private static string FindRepoRoot()
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory is not null && !File.Exists(Path.Combine(directory.FullName, "TrustCabinet.sln")))
            directory = directory.Parent;
        return directory?.FullName ?? throw new InvalidOperationException("Repository root not found.");
    }
}
