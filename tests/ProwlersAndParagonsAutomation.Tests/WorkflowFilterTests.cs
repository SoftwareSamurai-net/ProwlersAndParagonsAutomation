using System.Text.RegularExpressions;

namespace ProwlersAndParagonsAutomation.Tests;

/// <summary>
/// The build's <c>paths-ignore</c> list, held to the claim it makes.
///
/// <para><b>A path filter is a promise that nothing under it can break the build, and this
/// repository is an unusually bad place to assume that about Markdown.</b> Almost every
/// documentation file here is read by a test — <c>CLAUDE.md</c> and <c>docs/guide/*.md</c> by
/// <see cref="RepositoryGuideTests"/>, <c>docs/ACCOUNTS-SETUP.md</c> by the accounts contract,
/// <c>docs/MCP-SETUP.md</c> and <c>README.md</c> by the MCP setup tests,
/// <c>mcp/QUESTION-POLICY.md</c> by the question policy. Editing the index past its line budget,
/// or adding a guide the routing table does not name, is a red build. "It is only docs" is false
/// here far more often than it looks.</para>
///
/// <para><b>So the dangerous direction is the one this proves.</b> Every Markdown file a test
/// opens by name is collected out of the test sources themselves and checked against the ignore
/// list. That is the direction whose failure costs a missed regression, and it needs no list kept
/// by hand.</para>
///
/// <para><b>The safe direction is an allowlist, and it is honest about being one.</b> Nothing can
/// prove a file is unread — a test could compose its path a way no scan sees — so growing
/// <c>paths-ignore</c> has to be a deliberate act, and this is what makes it one. If you are here
/// because this test failed after you added a path: open the file, satisfy yourself no test reads
/// it, and add it below with the same care.</para>
/// </summary>
public sealed class WorkflowFilterTests
{
    private static string RepoRoot => RulesFixture.RepoRoot;

    private static string BuildWorkflow =>
        File.ReadAllText(Path.Combine(RepoRoot, ".github", "workflows", "build.yml"));

    /// <summary>
    /// The paths the build is allowed to skip. Two, and both are files no test opens: the log of
    /// what is done and what is left, and the handover written at the end of a slice. They are
    /// also the two that change on almost every piece of work, which is what makes skipping them
    /// worth anything at all.
    /// </summary>
    private static readonly string[] KnownInert = ["PROGRESS.md", "docs/HANDOVER.md"];

    private static List<string> Ignored()
    {
        var found = new List<string>();
        var inBlock = false;

        foreach (var line in BuildWorkflow.Split('\n'))
        {
            var trimmed = line.Trim();

            if (trimmed.StartsWith("paths-ignore:", StringComparison.Ordinal)) { inBlock = true; continue; }
            if (!inBlock) continue;

            var entry = Match(trimmed);
            if (entry is null) { inBlock = false; continue; }

            found.Add(entry);
        }

        return found;
    }

    /// <summary>A `- 'thing'` list item, or null for anything else — which ends the block.</summary>
    private static string? Match(string line)
    {
        var m = Regex.Match(line, @"^-\s*'([^']+)'\s*$", RegexOptions.None, TimeSpan.FromSeconds(5));

        return m.Success ? m.Groups[1].Value : null;
    }

    /// <summary>
    /// Every Markdown file named as a whole string literal anywhere in the test sources. A crude
    /// instrument on purpose: it over-collects rather than under-collects, and over-collecting only
    /// costs a path staying in the build.
    /// </summary>
    private static HashSet<string> ReadBySomeTest()
    {
        var names = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        foreach (var file in Directory.EnumerateFiles(
                     Path.Combine(RepoRoot, "tests"), "*.cs", SearchOption.AllDirectories))
        {
            // **Not this file.** The allowlist below necessarily names every path it vouches for,
            // so a scan that read its own source would find them and report the list as unsafe —
            // which it did, on the first run.
            if (Path.GetFileName(file) == "WorkflowFilterTests.cs") continue;

            foreach (Match m in Regex.Matches(
                         File.ReadAllText(file), @"""([A-Za-z0-9._-]+\.md)""",
                         RegexOptions.None, TimeSpan.FromSeconds(5)))
            {
                names.Add(m.Groups[1].Value);
            }
        }

        return names;
    }

    /// <summary>
    /// <b>The positive control, and it is not ceremony.</b> Every assertion below is an absence,
    /// and a parser that had stopped finding the block would satisfy all of them.
    /// </summary>
    [Fact]
    public void TheBuildReallyDoesSkipSomething()
    {
        Assert.NotEmpty(Ignored());
        Assert.NotEmpty(ReadBySomeTest());
    }

    /// <summary>
    /// <b>Nothing a test opens is skipped.</b> This is the direction that costs a missed
    /// regression: a docs change that would have failed the build, merged green because the build
    /// never ran.
    /// </summary>
    [Fact]
    public void NoFileATestReadsIsSkippedByTheBuild()
    {
        var read = ReadBySomeTest();

        foreach (var path in Ignored())
        {
            var name = path[(path.LastIndexOf('/') + 1)..];

            Assert.False(read.Contains(name),
                $"build.yml skips '{path}', but a test opens '{name}' by name — so a change to it "
                + "would merge without the build that would have caught it.");
        }
    }

    /// <summary>
    /// <b>And the guide directory is never skipped wholesale.</b> <see cref="RepositoryGuideTests"/>
    /// reads it with a wildcard rather than by naming each file, so the scan above cannot see it:
    /// adding a guide the routing table does not name is a red build, and a `docs/**` or
    /// `docs/guide/**` entry would hide exactly that.
    /// </summary>
    [Fact]
    public void TheGuidesAreNeverSkipped()
    {
        foreach (var path in Ignored())
        {
            Assert.False(
                path.StartsWith("docs/guide", StringComparison.OrdinalIgnoreCase)
                || path is "docs/**" or "docs/*" or "**/*.md" or "*.md",
                $"build.yml skips '{path}', which covers the guide set — and RepositoryGuideTests "
                + "holds the routing table and docs/guide/ to each other.");
        }
    }

    /// <summary>
    /// <b>Growing the list is deliberate.</b> Nothing can prove a file is unread, so this is an
    /// allowlist and says so — see the class remarks.
    /// </summary>
    [Fact]
    public void EverySkippedPathWasDecidedOnPurpose()
    {
        foreach (var path in Ignored())
        {
            Assert.True(KnownInert.Contains(path, StringComparer.Ordinal),
                $"build.yml skips '{path}', which nobody has vouched for here. Open it, satisfy "
                + "yourself no test reads it, then add it to KnownInert with the same care.");
        }
    }

    /// <summary>
    /// <b>Both triggers carry the same list.</b> A filter on the pull request and not on the push
    /// to <c>master</c> — or the reverse — is a build that runs in one place and not the other,
    /// which is worse than either choice made consistently.
    /// </summary>
    [Fact]
    public void ThePullRequestAndThePushSkipTheSameThings()
    {
        var blocks = Regex.Matches(BuildWorkflow, "paths-ignore:", RegexOptions.None, TimeSpan.FromSeconds(5));

        Assert.Equal(2, blocks.Count);
        Assert.Equal(KnownInert.Length * 2, Ignored().Count);
    }

    /// <summary>
    /// <b>A superseded run is cancelled, and a deploy never is.</b> A force-push to a branch used
    /// to leave the previous run burning to completion on a commit nobody would merge; a cancelled
    /// half-finished deploy is a site serving a partial upload. Opposite answers, both deliberate.
    /// </summary>
    [Theory]
    [InlineData("build.yml", "true")]
    [InlineData("qodana_code_quality.yml", "true")]
    [InlineData("deploy.yml", "false")]
    public void SupersededRunsAreCancelledExceptWhereThatWouldBreakSomething(string workflow, string expected)
    {
        // **Comment lines dropped first.** build.yml's own note explains why `deploy.yml` sets the
        // opposite value, so the first textual match in that file is inside prose rather than in
        // the setting — which is exactly what this read on its first run.
        var text = string.Join(
            '\n',
            File.ReadAllLines(Path.Combine(RepoRoot, ".github", "workflows", workflow))
                .Where(l => !l.TrimStart().StartsWith('#')));

        var m = Regex.Match(text, @"cancel-in-progress:\s*(\w+)", RegexOptions.None, TimeSpan.FromSeconds(5));

        Assert.True(m.Success, $"{workflow} declares no cancel-in-progress at all.");
        Assert.Equal(expected, m.Groups[1].Value);
    }

    /// <summary>
    /// <b>Qodana still runs on the default branch, and on a schedule.</b> It came off every pull
    /// request because the process already requires <c>./scripts/qodana-scan.sh</c> locally,
    /// reading zero, before one is opened — but a scan of the default branch as merged is a
    /// different claim from a scan of the branches that went into it, and the weekly run is the
    /// backstop for a local step that was skipped. Losing either would turn a trade into a removal.
    ///
    /// <para><b>It no longer names the branch, and that is the fix rather than a loosening.</b>
    /// This asserted the literal <c>- master</c>, so renaming the default branch to <c>main</c>
    /// turned it red on the default branch itself — a test failing for a rename it had no opinion
    /// about. What it actually cares about is that the push trigger still names <em>a</em> branch;
    /// which one is <see cref="EveryPushTriggerNamesTheSameBranch"/>'s question, once, for all
    /// three workflows.</para>
    /// </summary>
    [Fact]
    public void QodanaStillWatchesTheDefaultBranchAndStillRunsOnASchedule()
    {
        var text = File.ReadAllText(
            Path.Combine(RepoRoot, ".github", "workflows", "qodana_code_quality.yml"));

        Assert.Contains("schedule:", text, StringComparison.Ordinal);
        Assert.Contains("cron:", text, StringComparison.Ordinal);

        Assert.NotEmpty(PushBranches("qodana_code_quality.yml"));
    }

    /// <summary>
    /// Every workflow that fires on a push to the default branch — and so every one a rename
    /// silences if it is missed.
    /// </summary>
    private static readonly string[] PushTriggered = ["build.yml", "deploy.yml", "qodana_code_quality.yml"];

    /// <summary>
    /// The branches named under a workflow's <c>push:</c> trigger.
    ///
    /// <para>Only the push list: <c>pull_request</c> here carries no branch filter at all, so a
    /// rename cannot silence it and it is not this test's business.</para>
    /// </summary>
    private static List<string> PushBranches(string workflow)
    {
        var text = File.ReadAllText(Path.Combine(RepoRoot, ".github", "workflows", workflow));

        var push = text.IndexOf("\n  push:", StringComparison.Ordinal);
        Assert.True(push >= 0, $"{workflow} has no push trigger, so this asserts nothing about it.");

        var branches = text.IndexOf("branches:", push, StringComparison.Ordinal);
        Assert.True(branches >= 0, $"{workflow}'s push trigger names no branches.");

        // To the end of that list: the first line that is neither a list item, a comment, nor blank.
        return text[branches..].Split('\n').Skip(1)
            .TakeWhile(l => l.TrimStart().StartsWith('-')
                         || l.TrimStart().StartsWith('#')
                         || l.Trim().Length == 0)
            .Where(l => l.TrimStart().StartsWith('-'))
            .Select(l => l.Trim().TrimStart('-').Trim())
            .ToList();
    }

    /// <summary>
    /// <b>All three push triggers name the same branch.</b>
    ///
    /// <para><b>Because the failure is silence, and a partial rename is the realistic shape of
    /// it.</b> Rename the default branch, update two workflows and miss the third, and that third
    /// simply stops matching: no run, no failure, nothing anywhere saying so. A repository with
    /// zero runs looks exactly like a quiet one — and if the one missed is <c>deploy.yml</c>, the
    /// live site freezes at whatever shipped last while every merge continues to look fine.</para>
    ///
    /// <para><b>It deliberately does not say which name is right.</b> Hardcoding one is what broke
    /// the Qodana test above during this very rename. The property that survives a rename is that
    /// the three agree with each other.</para>
    ///
    /// <para><b>What no test here can reach:</b> Cloudflare Pages holds its own
    /// <c>production_branch</c>. If that still names the old branch, a push to the new one deploys
    /// as a <em>preview</em> — a green run that never reaches the public site. That setting lives
    /// in Cloudflare.</para>
    /// </summary>
    [Fact]
    public void EveryPushTriggerNamesTheSameBranch()
    {
        var named = PushTriggered.ToDictionary(w => w, PushBranches);

        // The positive control: a parser that had stopped finding the lists would satisfy the
        // comparison below with three empty sets.
        foreach (var (workflow, branches) in named)
            Assert.True(branches.Count > 0, $"{workflow}'s push trigger parsed to no branches at all.");

        var distinct = named.Values
            .Select(b => string.Join(",", b.OrderBy(x => x, StringComparer.Ordinal)))
            .Distinct(StringComparer.Ordinal)
            .ToList();

        Assert.True(distinct.Count == 1,
            "The push triggers disagree about which branch they fire on, so at least one of them "
            + "runs on nothing and reports no failure for it:\n  "
            + string.Join("\n  ", named.Select(kv => $"{kv.Key}: [{string.Join(", ", kv.Value)}]")));
    }
}
