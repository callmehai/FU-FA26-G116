namespace TrustCabinet.Web.Tests;

/// <summary>
/// Claude Code loads the project skills from .claude/skills, Codex and other agents from
/// .agents/skills (AGENTS.md, "Skill location"). The copies have to stay identical, or the team's
/// agents follow different rules: the first mirror was already a version behind when it was made.
/// </summary>
public sealed class SkillMirrorTests
{
    private static readonly string RepoRoot = FindRepoRoot();

    public static TheoryData<string> ProjectSkills() =>
        new(Directory.GetDirectories(Path.Combine(RepoRoot, ".claude", "skills")).Select(Path.GetFileName)!);

    [Theory]
    [MemberData(nameof(ProjectSkills))]
    public void Every_project_skill_is_mirrored_unchanged_into_agents_skills(string skill)
    {
        var canonical = Path.Combine(RepoRoot, ".claude", "skills", skill);
        var mirror = Path.Combine(RepoRoot, ".agents", "skills", skill);
        var fix = $"Copy it again: rm -rf .agents/skills/{skill} && cp -R .claude/skills/{skill} .agents/skills/";

        Assert.True(Directory.Exists(mirror), $".agents/skills/{skill} is missing. {fix}");
        Assert.Equal(Files(canonical), Files(mirror));
        foreach (var file in Files(canonical))
        {
            Assert.True(
                File.ReadAllBytes(Path.Combine(canonical, file)).AsSpan()
                    .SequenceEqual(File.ReadAllBytes(Path.Combine(mirror, file))),
                $".agents/skills/{skill}/{file} differs from .claude/skills/{skill}/{file}. {fix}");
        }
    }

    private static string[] Files(string root) => Directory
        .EnumerateFiles(root, "*", SearchOption.AllDirectories)
        .Select(path => Path.GetRelativePath(root, path).Replace('\\', '/'))
        .Order(StringComparer.Ordinal)
        .ToArray();

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
