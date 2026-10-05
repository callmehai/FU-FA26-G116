using System.Text.RegularExpressions;

namespace TrustCabinet.Web.Tests;

/// <summary>
/// Holds the frontend, the backend and docs/03-api/00-danh-muc-ma-thong-bao.md to one set of
/// message codes. Reads source files rather than running anything: a code the frontend matches
/// but the backend never sends is a silent bug — the branch just never fires — so it has to be
/// caught by reading both sides.
///
/// The chain it closes: every (status, code) the frontend branches on is a catalogue row; every
/// code the backend declares is a catalogue row with an HTTP status; every such code is asserted
/// by some endpoint test, which pins the status the backend really sends.
/// </summary>
public sealed partial class MessageCodeCatalogueTests
{
    private static readonly string RepoRoot = FindRepoRoot();

    private static readonly string CataloguePath =
        Path.Combine(RepoRoot, "docs", "03-api", "00-danh-muc-ma-thong-bao.md");

    /// <summary>A catalogue row: "| `MSG05` | 401 | …" — or "| `MSG06` | — | …" for FE/reserved.</summary>
    [GeneratedRegex(@"^\|\s*`(MSG\d+)`\s*\|\s*(\d{3}|—)\s*\|", RegexOptions.Multiline)]
    private static partial Regex CatalogueRow();

    /// <summary>Every place the frontend branches on a code goes through <c>isApiError</c>.</summary>
    [GeneratedRegex(@"isApiError\(\s*\w+\s*,\s*(\d{3})\s*,\s*'([^']*)'\s*\)")]
    private static partial Regex FrontendMatch();

    [GeneratedRegex(@"private const string \w+ = ""(MSG\d+)"";")]
    private static partial Regex BackendConstant();

    [GeneratedRegex(@"""(MSG\d+)""")]
    private static partial Regex CodeLiteral();

    [GeneratedRegex(@"\*\*`(MSG\d+)`\*\*")]
    private static partial Regex NextCode();

    [GeneratedRegex(@"\b(\d{3})\s+(MSG\d+)\b")]
    private static partial Regex DiagramResponseCode();

    /// <summary>A scanner that finds nothing would make every other test here pass vacuously.</summary>
    [Fact]
    public void The_scanners_find_codes_on_all_three_sides()
    {
        Assert.True(CatalogueStatuses().Count >= 30, "Catalogue rows not found — has the table format changed?");
        Assert.True(BackendCodes().Count >= 30, "Backend constants not found — has the declaration style changed?");
        Assert.True(FrontendMatches().Count >= 10, "Frontend isApiError matches not found — has the helper changed?");
    }

    [Fact]
    public void Every_code_the_frontend_matches_is_catalogued_with_that_status()
    {
        var catalogue = CatalogueStatuses();

        var mismatches = FrontendMatches()
            .Where(m => !catalogue.TryGetValue(m.Code, out var statuses) || !statuses.Contains(m.Status))
            .Select(m => $"{m.File}: isApiError(…, {m.Status}, '{m.Code}')")
            .ToList();

        Assert.True(mismatches.Count == 0,
            "The frontend matches a (status, code) the catalogue does not list:\n" + string.Join("\n", mismatches));
    }

    [Fact]
    public void The_frontend_only_matches_on_MSG_codes()
    {
        var bad = FrontendMatches().Where(m => !Regex.IsMatch(m.Code, @"^MSG\d+$")).ToList();

        Assert.True(bad.Count == 0,
            "String codes such as MEETING_* are gone — use the catalogue's MSGnn:\n"
            + string.Join("\n", bad.Select(m => $"{m.File}: '{m.Code}'")));
    }

    [Fact]
    public void Every_code_the_backend_declares_is_catalogued_with_an_http_status()
    {
        var catalogue = CatalogueStatuses();

        var missing = BackendCodes()
            .Where(code => !catalogue.TryGetValue(code, out var statuses) || statuses.Count == 0)
            .ToList();

        Assert.True(missing.Count == 0,
            "Declared in the backend but not catalogued with an HTTP status: " + string.Join(", ", missing));
    }

    [Fact]
    public void Every_code_the_catalogue_says_the_backend_sends_is_declared_there()
    {
        var declared = BackendCodes();

        var phantom = CatalogueStatuses()
            .Where(entry => entry.Value.Count > 0 && !declared.Contains(entry.Key))
            .Select(entry => entry.Key)
            .ToList();

        Assert.True(phantom.Count == 0,
            "Catalogued with an HTTP status but the backend never declares it: " + string.Join(", ", phantom));
    }

    [Fact]
    public void Backend_endpoints_use_named_constants_rather_than_inline_codes()
    {
        var inline = EndpointFiles()
            .SelectMany(file => File.ReadLines(file)
                .Where(line => CodeLiteral().IsMatch(line) && !BackendConstant().IsMatch(line))
                .Select(line => $"{Path.GetFileName(file)}: {line.Trim()}"))
            .ToList();

        Assert.True(inline.Count == 0,
            "Declare the code as a private const at the top of the file:\n" + string.Join("\n", inline));
    }

    [Fact]
    public void Every_code_the_backend_declares_is_asserted_by_a_test()
    {
        var testSources = string.Join('\n', Directory
            .EnumerateFiles(Path.Combine(RepoRoot, "tests"), "*.cs", SearchOption.AllDirectories)
            .Where(file => !IsBuildOutput(file) && Path.GetFileName(file) != $"{nameof(MessageCodeCatalogueTests)}.cs")
            .Select(File.ReadAllText));

        var untested = BackendCodes().Where(code => !testSources.Contains($"\"{code}\"")).ToList();

        Assert.True(untested.Count == 0,
            "No test asserts these codes, so nothing pins the status they are sent with: "
            + string.Join(", ", untested));
    }

    [Fact]
    public void The_next_code_follows_the_highest_one_in_use()
    {
        var text = File.ReadAllText(CataloguePath);
        var highest = CatalogueRow().Matches(text)
            .Concat(Regex.Matches(text, @"^\|\s*`(MSG\d+)`", RegexOptions.Multiline))
            .Max(m => int.Parse(m.Groups[1].Value[3..]));

        var next = NextCode().Match(text);

        Assert.True(next.Success, "The catalogue has no **`MSGnn`** under \"Mã tiếp theo\".");
        Assert.Equal(highest + 1, int.Parse(next.Groups[1].Value[3..]));
    }

    [Theory]
    [InlineData("MSG51", "UC-29")]
    [InlineData("MSG52", "UC-30")]
    [InlineData("MSG57", "UC-29")]
    [InlineData("MSG58", "UC-29")]
    [InlineData("MSG59", "UC-30")]
    [InlineData("MSG60", "UC-31")]
    [InlineData("MSG61", "UC-32")]
    [InlineData("MSG62", "UC-33")]
    [InlineData("MSG63", "UC-34")]
    [InlineData("MSG64", "UC-35")]
    [InlineData("MSG65", "UC-36")]
    [InlineData("MSG66", "UC-31 / UC-32")]
    [InlineData("MSG67", "UC-31 / UC-32 / UC-33 / UC-34")]
    [InlineData("MSG68", "UC-31")]
    [InlineData("MSG69", "UC-32")]
    [InlineData("MSG70", "UC-33")]
    [InlineData("MSG71", "UC-34")]
    [InlineData("MSG72", "UC-35")]
    [InlineData("MSG73", "UC-35")]
    [InlineData("MSG74", "UC-36")]
    [InlineData("MSG75", "UC-35")]
    [InlineData("MSG76", "UC-36")]
    [InlineData("MSG77", "UC-28")]
    public void Meeting_workflow_preserves_main_code_reservations(string code, string source)
    {
        var endpoint = File.ReadAllText(Path.Combine(
            RepoRoot, "backend", "TrustCabinet.Web", "Endpoints", "MeetingEndpoints.cs"));
        Assert.DoesNotContain(code, BackendConstant().Matches(endpoint)
            .Select(match => match.Groups[1].Value));
        var row = Assert.Single(Regex.Matches(File.ReadAllText(CataloguePath),
            @"^\|\s*`(MSG\d+)`\s*\|\s*((?:UC-|ADR-)[^|]+)\|", RegexOptions.Multiline),
            match => match.Groups[1].Value == code);
        Assert.Contains(source, row.Groups[2].Value, StringComparison.Ordinal);
    }

    [Theory]
    [InlineData("Msg78Submitted", "MSG78")]
    [InlineData("Msg79ReturnedToDraft", "MSG79")]
    [InlineData("Msg80InvalidTransition", "MSG80")]
    [InlineData("Msg81SubmissionNotReady", "MSG81")]
    [InlineData("Msg82ApproverInvalid", "MSG82")]
    [InlineData("Msg83RejectionReasonRequired", "MSG83")]
    [InlineData("Msg84RejectionReasonTooLong", "MSG84")]
    [InlineData("Msg85ActionForbidden", "MSG85")]
    [InlineData("Msg86HistoryInvalidQuery", "MSG86")]
    [InlineData("Msg87BodyTooLarge", "MSG87")]
    [InlineData("Msg88TransitionFailed", "MSG88")]
    [InlineData("Msg89OrganizerTransferred", "MSG89")]
    [InlineData("Msg136WorkflowReadFailed", "MSG136")]
    [InlineData("Msg130ApproversLoadFailed", "MSG130")]
    [InlineData("Msg131WorkflowActiveUserCheckFailed", "MSG131")]
    [InlineData("Msg132ApprovalConflict", "MSG132")]
    [InlineData("Msg133ReturnToDraftConflict", "MSG133")]
    [InlineData("Msg134OrganizerInvalid", "MSG134")]
    [InlineData("Msg135OrganizerTransferConflict", "MSG135")]
    [InlineData("Msg137PublishRosterNotReady", "MSG137")]
    public void Workflow_codes_use_the_allocation_after_main_reservations(string constant, string code)
    {
        var endpoint = File.ReadAllText(Path.Combine(
            RepoRoot, "backend", "TrustCabinet.Web", "Endpoints", "MeetingEndpoints.cs"));
        Assert.Contains($"private const string {constant} = \"{code}\";", endpoint, StringComparison.Ordinal);
    }

    [Theory]
    [InlineData("SubmitMeetingForApproval")]
    [InlineData("ReviewMeetingApproval")]
    [InlineData("PublishMeeting")]
    public void Workflow_sequences_use_catalogued_codes_with_the_documented_status(string diagram)
    {
        var source = File.ReadAllText(Path.Combine(
            RepoRoot, "docs", "assets", "diagrams", "meeting-management", $"{diagram}.mmd"));
        Assert.DoesNotMatch(@"\bMEETING_[A-Z_]+\b", source);
        var responses = DiagramResponseCode().Matches(source);
        Assert.NotEmpty(responses);
        var catalogue = CatalogueStatuses();
        foreach (Match response in responses)
        {
            var status = int.Parse(response.Groups[1].Value);
            var code = response.Groups[2].Value;
            Assert.True(catalogue.TryGetValue(code, out var statuses) && statuses.Contains(status),
                $"{diagram}: {status} {code} does not match the message catalogue.");
        }
    }

    /// <summary>Code → the HTTP statuses the catalogue lists for it; empty for FE-only or reserved.</summary>
    private static Dictionary<string, HashSet<int>> CatalogueStatuses()
    {
        var result = new Dictionary<string, HashSet<int>>();
        foreach (Match row in CatalogueRow().Matches(File.ReadAllText(CataloguePath)))
        {
            var statuses = result.TryGetValue(row.Groups[1].Value, out var existing)
                ? existing
                : result[row.Groups[1].Value] = [];
            if (int.TryParse(row.Groups[2].Value, out var status))
            {
                statuses.Add(status);
            }
        }

        return result;
    }

    private static HashSet<string> BackendCodes() => EndpointFiles()
        .SelectMany(file => BackendConstant().Matches(File.ReadAllText(file)))
        .Select(m => m.Groups[1].Value)
        .ToHashSet();

    private static IEnumerable<string> EndpointFiles() => Directory.EnumerateFiles(
        Path.Combine(RepoRoot, "backend", "TrustCabinet.Web", "Endpoints"), "*.cs", SearchOption.AllDirectories);

    private static List<(string File, int Status, string Code)> FrontendMatches() => Directory
        .EnumerateFiles(Path.Combine(RepoRoot, "frontend", "src"), "*.*", SearchOption.AllDirectories)
        .Where(file => file.EndsWith(".ts", StringComparison.Ordinal) || file.EndsWith(".tsx", StringComparison.Ordinal))
        .SelectMany(file => FrontendMatch().Matches(File.ReadAllText(file))
            .Select(m => (
                Path.GetRelativePath(RepoRoot, file),
                int.Parse(m.Groups[1].Value),
                m.Groups[2].Value)))
        .ToList();

    private static bool IsBuildOutput(string file)
        => file.Contains($"{Path.DirectorySeparatorChar}bin{Path.DirectorySeparatorChar}", StringComparison.Ordinal)
           || file.Contains($"{Path.DirectorySeparatorChar}obj{Path.DirectorySeparatorChar}", StringComparison.Ordinal);

    private static string FindRepoRoot()
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory is not null && !File.Exists(Path.Combine(directory.FullName, "TrustCabinet.sln")))
        {
            directory = directory.Parent;
        }

        return directory?.FullName
            ?? throw new InvalidOperationException("TrustCabinet.sln not found above the test binaries.");
    }
}
