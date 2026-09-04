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

    private static string DeployWorkflow =>
        File.ReadAllText(Path.Combine(RepoRoot, ".github", "workflows", "deploy.yml"));

    /// <summary>
    /// The paths the build is allowed to skip. <b>None</b> — the one entry this ever held,
    /// <c>docs/HANDOVER.md</c>, has been deleted rather than replaced.
    ///
    /// <para><b>It was two, then one, and there is no third waiting.</b> <c>PROGRESS.md</c> came
    /// off deliberately when <see cref="ProgressArchiveTests"/> began holding its completed-work
    /// section to being a pointer rather than a place entries pile up — a skipped path whose
    /// contents a test checks is a change that merges without the build that would have caught it,
    /// which is the exact failure <see cref="NoFileATestReadsIsSkippedByTheBuild"/> exists to
    /// report. <b>It reported it</b>, on the commit that added that test. <c>docs/HANDOVER.md</c>
    /// held the same status until it was deleted, and almost every other Markdown file here is
    /// opened by some test by name, so there is nothing left to vouch for.</para>
    ///
    /// <para><b>Do not add a path here to give the workflow's skip key something to point at.</b>
    /// This is an allowlist because nothing can prove a file unread — a test could compose a path
    /// no scan sees. Earning a place means actually satisfying yourself no test reads it, the way
    /// the paragraph above did twice. Empty is the honest state until a real candidate turns up.</para>
    /// </summary>
    private static readonly string[] KnownInert = [];

    private static List<string> Ignored() => ParsePathsIgnore(BuildWorkflow);

    /// <summary>
    /// Every skip-list entry in some workflow YAML, flattened in the order it appears.
    ///
    /// <para>Split out of <see cref="Ignored"/> so <see cref="TheParserStillFindsASkipList"/> can
    /// drive <b>this exact method</b> against a fixture. A second parser written for the fixture
    /// would only prove that two copies agree with each other.</para>
    /// </summary>
    private static List<string> ParsePathsIgnore(string yaml)
    {
        var found = new List<string>();
        var inBlock = false;

        foreach (var line in yaml.Split('\n'))
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
    /// <b>The positive control, and it is not ceremony — read this before touching
    /// <see cref="ParsePathsIgnore"/> or <see cref="Match"/>.</b>
    ///
    /// <para>This used to assert <c>Assert.NotEmpty(Ignored())</c> straight against the workflow,
    /// on the sound reasoning that every other assertion here is an <em>absence</em>: a parser
    /// that had stopped finding the block would return nothing and satisfy all of them.</para>
    ///
    /// <para><b>That stopped being able to tell the truth from a broken parser the moment
    /// <c>docs/HANDOVER.md</c> was deleted.</b> It was the build's last skippable path, so
    /// <see cref="Ignored"/> now legitimately returns nothing — and a bare non-empty assertion
    /// could no longer distinguish "the parser broke" from "there is honestly nothing to skip",
    /// which is precisely the ambiguity a positive control exists to remove.</para>
    ///
    /// <para>So the fixture is what has to be non-empty, and the real workflow is free to be
    /// empty. The <c>types:</c> line after the list is deliberate: a parser that failed to end the
    /// block there would run on into content that was never part of it, and the expected list
    /// would not match.</para>
    /// </summary>
    [Fact]
    public void TheParserStillFindsASkipList()
    {
        const string fixture = """
            pull_request:
              paths-ignore:
                - 'fixture/one.md'
                - 'fixture/two.md'
              types: [opened]
            push:
              branches:
                - main
            """;

        Assert.Equal(["fixture/one.md", "fixture/two.md"], ParsePathsIgnore(fixture));
    }

    /// <summary>
    /// <b>The other half of the old positive control, kept because it is still a real claim about
    /// this repository.</b> Test sources open dozens of Markdown files by name, and
    /// <see cref="Ignored"/> being empty says nothing about whether that scan still works — so it
    /// is asserted directly rather than leaning on the workflow having something to compare with.
    /// </summary>
    [Fact]
    public void SomeTestStillOpensAMarkdownFileByName()
    {
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
    /// <b>A directory some test reads by wildcard is never skipped wholesale.</b> There are two,
    /// and the scan above cannot see either of them: <see cref="RepositoryGuideTests"/> enumerates
    /// <c>docs/guide/</c> and <see cref="ProgressArchiveTests"/> enumerates <c>docs/progress/</c>,
    /// neither by naming its files. So a `docs/**` entry — or either directory on its own — would
    /// hide exactly the change those tests exist to catch: a guide the routing table does not name,
    /// or an archive file with no title.
    ///
    /// <para><b>The archive was very nearly added to the skip list while it was being created</b>,
    /// on the reasoning that finished work is inert. It is not inert while a test reads it, and
    /// that is the whole rule this test states.</para>
    /// </summary>
    [Fact]
    public void ADirectoryReadByWildcardIsNeverSkipped()
    {
        foreach (var path in Ignored())
        {
            Assert.False(
                path.StartsWith("docs/guide", StringComparison.OrdinalIgnoreCase)
                || path.StartsWith("docs/progress", StringComparison.OrdinalIgnoreCase)
                || path is "docs/**" or "docs/*" or "**/*.md" or "*.md",
                $"build.yml skips '{path}', which covers a directory a test enumerates — and that "
                + "test is the only thing holding its contents to a shape.");
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
        // **Checked two ways, because one of them alone has a hole.** `Ignored()` being empty is
        // the ordinary case — an entry added under one trigger and not the other. But a bare skip
        // key with no list items under it parses to nothing, so `Ignored()` would stay empty and
        // notice nothing, while the workflow had quietly grown a filter on one trigger. The text
        // check is what closes that, and it is the mutation this test was watched to fail on.
        Assert.DoesNotContain("paths-ignore:", BuildWorkflow, StringComparison.Ordinal);
        Assert.Empty(Ignored());
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

    /// <summary>
    /// <b>The silent-failure one.</b> Without <c>d1/migrations/**</c> in <c>deploy.yml</c>'s push
    /// <c>paths:</c> list, a pull request that adds ONLY a migration file never triggers this
    /// workflow at all — <c>scripts/apply-migrations.sh</c>, which applies exactly that kind of
    /// change, would simply never run for it. A merged migration could then sit unapplied on
    /// production until some unrelated <c>web/</c> or <c>worker/</c> change happened to trigger
    /// the next deploy — the same shape of outage <c>docs/guide/hosting.md</c> records for
    /// <c>0005_campaigns.sql</c>, one layer further back.
    /// </summary>
    [Fact]
    public void DeployTriggersOnAMigrationFileAlone()
    {
        var text = DeployWorkflow;

        // The positive control: the push trigger really does declare a paths filter, so the
        // assertion below is checking a real list rather than passing vacuously against a typo
        // that renamed the block.
        Assert.True(text.Contains("paths:", StringComparison.Ordinal),
            "deploy.yml's push trigger names no paths filter at all.");

        Assert.Contains("'d1/migrations/**'", text, StringComparison.Ordinal);
    }

    /// <summary>
    /// <b>Ordering is the whole correctness of the migration-apply step.</b> It has to run before
    /// the Pages upload: applying after would leave the deployed code briefly ahead of the schema
    /// it depends on, which is the exact outage <c>docs/guide/hosting.md</c> records — every
    /// migration in <c>d1/migrations</c> is written to tolerate the other order, code depending on
    /// a schema that has not shipped yet is not.
    ///
    /// <para><b>Anchored on the step's own invocation, not on the script's name, and the first
    /// version of this was defeated by exactly the duplicate this repository has been bitten by
    /// five times.</b> It read <c>IndexOf("apply-migrations.sh")</c> — which finds the copy inside
    /// the <c>paths:</c> filter near the top of the file, twenty lines above the deploy step and a
    /// hundred above the apply step. So the comparison was satisfied by the path filter whatever
    /// order the steps were in: moving the apply step to <em>after</em> the Pages upload left this
    /// green. Proved by mutation.</para>
    ///
    /// <para>It reads the two <em>invocations</em> now — the <c>run:</c> line that actually calls
    /// the script and the <c>uses:</c> line that actually deploys — with comments blanked first,
    /// because the apply step's own comment names the script too.</para>
    /// </summary>
    [Fact]
    public void MigrationsAreAppliedBeforeThePagesDeploy()
    {
        // Comment lines dropped, the same instrument
        // SupersededRunsAreCancelledExceptWhereThatWouldBreakSomething uses and for the same
        // reason: prose in this file names both landmarks.
        var text = string.Join(
            '\n',
            DeployWorkflow.Split('\n').Where(l => !l.TrimStart().StartsWith('#')));

        var apply = Regex.Match(text, @"^\s*run:\s*\./scripts/apply-migrations\.sh\s*$",
            RegexOptions.Multiline, TimeSpan.FromSeconds(5));

        var deploy = Regex.Match(text, @"^\s*uses:\s*cloudflare/wrangler-action@",
            RegexOptions.Multiline, TimeSpan.FromSeconds(5));

        // The positive control: both invocations must actually be found, or the ordering
        // comparison below passes vacuously — which is how its first version passed against a
        // deploy.yml with the steps in the wrong order.
        Assert.True(apply.Success,
            "deploy.yml has no step whose `run:` is ./scripts/apply-migrations.sh, so nothing "
            + "applies a pending migration and this test is asserting nothing.");
        Assert.True(deploy.Success,
            "deploy.yml no longer deploys via cloudflare/wrangler-action, so this test cannot see "
            + "what the apply step has to come before.");

        Assert.True(apply.Index < deploy.Index,
            "scripts/apply-migrations.sh must run BEFORE cloudflare/wrangler-action's Pages "
            + "deploy — applying it after would ship code ahead of the schema it depends on. "
            + $"The apply step is at character {apply.Index} and the deploy at {deploy.Index}.");
    }

    /// <summary>
    /// <b>Every script a workflow runs as <c>./path</c> is executable in the index.</b>
    ///
    /// <para><b>This is not hygiene; it is the failure it was written after.</b>
    /// <c>scripts/apply-migrations.sh</c> was committed <c>100644</c>, so the first deploy after it
    /// merged answered <c>Permission denied</c> and exited <b>126</b> — before running a line of
    /// the gate it drives, and before the Pages upload that gate exists to hold back. The ordering
    /// held and the site was never at risk, but nothing could ship and nothing in five suites had
    /// a word to say about it.</para>
    ///
    /// <para><b>No Windows checkout could have caught it, which is the whole reason this is a test
    /// rather than a habit.</b> Git for Windows does not honour the mode bit in the working tree,
    /// so <c>./scripts/apply-migrations.sh</c> runs perfectly on the machine it was written on and
    /// fails on every Linux runner. The mode is real either way — it is in the index — so this
    /// reads it out of git rather than off the filesystem, which is the only place the answer is
    /// the same on both.</para>
    ///
    /// <para>Collected out of the workflows themselves, so a script added later is covered without
    /// anybody remembering this file exists. Watched to fail three ways: the real defect, the same
    /// bit cleared on a script that was already right, and the scan matching nothing.</para>
    /// </summary>
    [Fact]
    public void EveryScriptAWorkflowRunsDirectlyIsExecutable()
    {
        var workflows = Directory.GetFiles(
            Path.Combine(RepoRoot, ".github", "workflows"), "*.yml");

        var invoked = workflows
            .SelectMany(file => Regex.Matches(
                File.ReadAllText(file),
                @"^\s*run:\s*\./(?<path>[\w./-]+\.sh)",
                RegexOptions.Multiline, TimeSpan.FromSeconds(5)))
            .Select(m => m.Groups["path"].Value)
            .Distinct(StringComparer.Ordinal)
            .Order(StringComparer.Ordinal)
            .ToList();

        // Without this, a scan that stopped matching would leave an empty set and pass.
        Assert.NotEmpty(invoked);

        var modes = Modes();

        foreach (var script in invoked)
        {
            Assert.True(modes.TryGetValue(script, out var mode),
                $"a workflow runs ./{script}, which git is not tracking at all.");

            Assert.True(mode == "100755",
                $"a workflow runs ./{script} directly and git records it as {mode}, not 100755. "
                + "On a Linux runner that is `Permission denied` and exit 126, which is exactly "
                + "how the first deploy of scripts/apply-migrations.sh failed. Fix it with "
                + $"`git update-index --chmod=+x {script}` — and note a Windows checkout runs it "
                + "happily either way, so this cannot be checked by hand here.");
        }
    }

    /// <summary>Every tracked script's mode, read out of git — the one place Windows agrees.</summary>
    private static Dictionary<string, string> Modes()
    {
        using var git = System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo
        {
            FileName = "git",
            Arguments = "ls-files --stage -- scripts",
            WorkingDirectory = RepoRoot,
            RedirectStandardOutput = true,
            UseShellExecute = false,
        });

        Assert.NotNull(git);

        var listed = git!.StandardOutput.ReadToEnd();
        git.WaitForExit();

        Assert.Equal(0, git.ExitCode);

        return listed
            .Split('\n', StringSplitOptions.RemoveEmptyEntries)
            .Select(line => line.Split('\t', 2))
            .Where(parts => parts.Length == 2)
            .ToDictionary(
                parts => parts[1].Trim(),
                parts => parts[0].Split(' ', 2)[0],
                StringComparer.Ordinal);
    }

    /// <summary>
    /// <b>Every place that names a wrangler version names the same one.</b>
    ///
    /// <para>There are three, and until this test they were kept in step by a note asking the
    /// next reader to remember. <c>deploy.yml</c>'s <c>wranglerVersion:</c> input is what the
    /// Pages upload actually runs; the <c>#&#160;wrangler=</c> comment on the <c>uses:</c> line
    /// above it is parsed by <c>build.yml</c>'s pull-request dry-run so it bundles against the
    /// same one; and <c>scripts/apply-migrations.sh</c>'s <c>WRANGLER_VERSION</c> is what lists
    /// and applies D1 migrations on the step before. Two of the three used to be different
    /// versions on purpose, for a reason that has since expired — see that script's header.</para>
    ///
    /// <para><b>The comment is the one that rots, and it has a documented history of it.</b>
    /// Commit <c>a4ec6ef</c> fixed the dry-run's <c>sed</c> to match any major precisely because
    /// Dependabot would one day bump the pin — and it ends by warning that the version *inside*
    /// that comment is still hand-maintained, so a major bump has to update it or the two drift
    /// apart again. This is that warning turned into something that fails.</para>
    ///
    /// <para><b>And the input matters as much as the comment now, which it did not before.</b>
    /// <c>wrangler-action@v3</c> hard-coded <c>DEFAULT_WRANGLER_VERSION = "3.90.0"</c>, so
    /// omitting the input was itself a pin. <c>@v4</c>'s default is the range <c>"4"</c>, so
    /// omitting it would let the deploy float to whatever 4.x npm serves that minute while the
    /// dry-run and the migration step stayed fixed. Deleting the input is therefore a real defect
    /// and not a tidy-up, and this test treats a missing one as a failure rather than as nothing
    /// to compare.</para>
    ///
    /// <para>Watched to fail four ways: each of the three versions changed on its own, and the
    /// <c>wranglerVersion:</c> line deleted entirely.</para>
    /// </summary>
    [Fact]
    public void WranglerIsPinnedToOneVersion()
    {
        var deploy = DeployWorkflow;

        var comment = Regex.Match(
            deploy,
            @"^\s*uses:\s*cloudflare/wrangler-action@\S+\s+#\s*wrangler=(?<version>[0-9][0-9.]*)",
            RegexOptions.Multiline,
            TimeSpan.FromSeconds(5));

        var input = Regex.Match(
            deploy,
            @"^\s*wranglerVersion:\s*""(?<version>[0-9][0-9.]*)""\s*$",
            RegexOptions.Multiline,
            TimeSpan.FromSeconds(5));

        var script = Regex.Match(
            File.ReadAllText(Path.Combine(RepoRoot, "scripts", "apply-migrations.sh")),
            @"^WRANGLER_VERSION=""(?<version>[0-9][0-9.]*)""\s*$",
            RegexOptions.Multiline,
            TimeSpan.FromSeconds(5));

        // The positive controls. Any one of these missing makes the comparison below pass
        // vacuously, which is the shape of every guard this repository has had to fix twice.
        Assert.True(comment.Success,
            "deploy.yml's wrangler-action line carries no `# wrangler=<version>` comment, so "
            + "build.yml's dry-run has nothing to read and would refuse. Restore it.");

        Assert.True(input.Success,
            "deploy.yml's deploy step names no `wranglerVersion:`, so the action falls back to "
            + "its own default — which on @v4 is the range \"4\", not a version. The upload would "
            + "float to whatever 4.x npm serves while everything else here stays pinned.");

        Assert.True(script.Success,
            "scripts/apply-migrations.sh no longer declares WRANGLER_VERSION=\"<version>\", so "
            + "this test cannot see what the migration step runs.");

        var fromComment = comment.Groups["version"].Value;
        var fromInput = input.Groups["version"].Value;
        var fromScript = script.Groups["version"].Value;

        Assert.True(
            fromComment == fromInput && fromInput == fromScript,
            $"Three places name a wrangler version and they disagree: deploy.yml's "
            + $"`# wrangler=` comment says {fromComment}, its `wranglerVersion:` input says "
            + $"{fromInput}, and scripts/apply-migrations.sh says {fromScript}. The comment is "
            + "what build.yml's pull-request dry-run bundles with, the input is what the Pages "
            + "upload runs, and the script is what lists and applies migrations against the real "
            + "database — so a disagreement means at least one of the three is testing or "
            + "deploying with a wrangler nothing else uses.");
    }
}
