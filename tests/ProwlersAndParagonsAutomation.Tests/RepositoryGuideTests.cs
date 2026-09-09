using System.Text.RegularExpressions;

namespace ProwlersAndParagonsAutomation.Tests;

/// <summary>
/// Holds <c>CLAUDE.md</c> and the guide set in <c>docs/guide/</c> to each other.
///
/// <para><b>Why this exists.</b> <c>CLAUDE.md</c> reached 1,431 lines by accretion — one reasonable
/// paragraph at a time — and the cost was not aesthetic. Pull request #79 shipped three whole
/// subsystems (<c>RulebookProse</c>/<c>BookText</c>, the explained sheet, and
/// <c>DiscardedCharacter</c>) and <em>none</em> of the three was written down in it, while a bullet
/// describing a confirm that same pull request had removed sat there being wrong for three more
/// merges. A document nobody finishes is a document whose last two hundred lines do not fire. The
/// split moved everything area-specific into <c>docs/guide/</c> and left behind the rules that
/// apply whatever you are working on.</para>
///
/// <para><b>What that split buys, and what it costs.</b> It buys a smaller always-loaded file. It
/// costs a new failure mode that the single file did not have: <b>a pointer can rot independently
/// of the thing it points at.</b> A guide nobody names is a guide nobody reads, and an index naming
/// a file that has been renamed away sends a reader hunting for rules that are still in force
/// somewhere else. Both are silent — a renamed markdown file breaks no build. These tests are the
/// only thing that would notice, which is the whole reason they are worth their weight.</para>
///
/// <para><b>The budget test is the load-bearing one and it is deliberately awkward.</b> Without it
/// the file regrows and the split has bought a year, not a fix. It is meant to be an obstacle at
/// exactly the moment somebody is about to add an area-specific paragraph to the index.</para>
/// </summary>
public sealed class RepositoryGuideTests
{
    private static string RepoRoot => RulesFixture.RepoRoot;
    private static string GuideDirectory => Path.Combine(RepoRoot, "docs", "guide");
    private static string IndexPath => Path.Combine(RepoRoot, "CLAUDE.md");

    /// <summary>
    /// The ceiling on the index, in lines.
    ///
    /// <para>Set with headroom over what the split actually produced rather than snug against it —
    /// a budget that fails on the next honest sentence teaches people to raise the budget, which is
    /// the one outcome that makes it worthless. It is here to catch a <em>section</em> being added,
    /// not a line.</para>
    /// </summary>
    private const int IndexLineBudget = 400;

    private static string[] IndexLines() => File.ReadAllLines(IndexPath);

    private static List<string> GuideFiles() =>
        Directory.Exists(GuideDirectory)
            ? Directory.GetFiles(GuideDirectory, "*.md").Select(Path.GetFileName).OfType<string>()
                .OrderBy(n => n, StringComparer.Ordinal).ToList()
            : [];

    /// <summary>
    /// Every <c>docs/guide/*.md</c> path the index names, as written.
    ///
    /// <para><b>Matched on the path, never with <c>Contains</c> on the file name.</b> The whole
    /// point is to catch a renamed file, and a bare name search finds <c>browser.md</c> inside
    /// <c>docs/guide/browser.md</c> whether the link is well-formed or not — and would find it in
    /// prose that merely mentions the file too. This repository has been bitten by a
    /// <c>Contains</c> guard five separate times; see the notes on <c>EffectiveValue</c> and on the
    /// accounts server's routing check.</para>
    /// </summary>
    private static List<string> GuidePathsNamedByIndex()
    {
        var text = File.ReadAllText(IndexPath);
        return Regex.Matches(text, @"docs/guide/([A-Za-z0-9._-]+\.md)")
            .Select(m => m.Groups[1].Value)
            .Distinct(StringComparer.Ordinal)
            .OrderBy(n => n, StringComparer.Ordinal)
            .ToList();
    }

    /// <summary>
    /// A guide the index does not name is a guide nobody will open, because the index is the only
    /// file that is loaded for you.
    /// </summary>
    [Fact]
    public void EveryGuideIsNamedByTheIndex()
    {
        var guides = GuideFiles();

        // Positive control on the instrument itself. An empty guide directory — a bad path, a
        // rename of the directory, a checkout that did not include it — satisfies every "no
        // orphans" assertion completely while proving nothing, which is the exact shape of guard
        // failure this repository has shipped four times.
        Assert.True(
            guides.Count >= 5,
            $"Expected the guide set to hold several files; found {guides.Count} in {GuideDirectory}. "
            + "If the guides really were consolidated away, this test and the index's routing table "
            + "both need rewriting — do not simply lower this number.");

        var named = GuidePathsNamedByIndex();
        var orphans = guides.Where(g => !named.Contains(g, StringComparer.Ordinal)).ToList();

        Assert.True(
            orphans.Count == 0,
            "These guide files are not named by CLAUDE.md's routing table, so nothing would ever "
            + $"send a reader to them: {string.Join(", ", orphans)}. Add a row to the table, or "
            + "fold the file into a guide that is named.");
    }

    /// <summary>
    /// A pointer to a file that is not there is worse than no pointer: it reads as though the rules
    /// were written down, and sends the reader looking in the one place they are not.
    /// </summary>
    [Fact]
    public void EveryGuideTheIndexNamesExists()
    {
        var named = GuidePathsNamedByIndex();

        // Positive control: an extraction that has stopped matching yields an empty set, which
        // would satisfy the absence assertion below without reading a single row of the table.
        Assert.True(
            named.Count >= 5,
            $"CLAUDE.md names only {named.Count} guide paths. The routing table is the point of the "
            + "file; if it has been emptied or its link format has changed, fix this extraction "
            + "rather than the assertion.");

        var missing = named.Where(n => !File.Exists(Path.Combine(GuideDirectory, n))).ToList();

        Assert.True(
            missing.Count == 0,
            "CLAUDE.md's routing table points at guide files that do not exist: "
            + $"{string.Join(", ", missing)}. A dead pointer sends a reader hunting for rules that "
            + "are still in force somewhere else.");
    }

    /// <summary>
    /// Keeps the index short enough to be read to the end.
    ///
    /// <para>This is the invariant the whole split exists to create. Everything else here is
    /// bookkeeping around it.</para>
    /// </summary>
    [Fact]
    public void TheIndexStaysShortEnoughToBeReadToTheEnd()
    {
        var lines = IndexLines().Length;

        Assert.True(
            lines <= IndexLineBudget,
            $"CLAUDE.md is {lines} lines, over its {IndexLineBudget}-line budget. It was 1,431 "
            + "lines once and the cost was that whole subsystems shipped undocumented while a "
            + "bullet describing a removed confirm sat there being wrong for three merges. If what "
            + "you are adding belongs to one area, it belongs in that area's file under "
            + "docs/guide/. Raise this number only if you are prepared to argue the index is still "
            + "read to the end.");
    }

    /// <summary>
    /// The routing table has to actually route — it is a table of what you are about to touch
    /// against what to read, and a guide reachable only from prose is one a reader scanning the
    /// table will miss.
    /// </summary>
    [Fact]
    public void EveryGuideIsReachableFromTheRoutingTableItself()
    {
        var tableRows = IndexLines()
            .Where(l => l.TrimStart().StartsWith('|') && l.Contains("docs/guide/", StringComparison.Ordinal))
            .ToList();

        Assert.True(
            tableRows.Count >= 5,
            $"Found {tableRows.Count} routing-table rows naming a guide. The table is how a reader "
            + "gets from 'I am about to edit web/' to the rules that govern it; if it has stopped "
            + "being a table, this test needs rewriting rather than deleting.");

        var routed = tableRows
            .SelectMany(r => Regex.Matches(r, @"docs/guide/([A-Za-z0-9._-]+\.md)").Select(m => m.Groups[1].Value))
            .Distinct(StringComparer.Ordinal)
            .ToList();

        var unrouted = GuideFiles().Where(g => !routed.Contains(g, StringComparer.Ordinal)).ToList();

        Assert.True(
            unrouted.Count == 0,
            "These guides are not reachable from the routing table, only from prose elsewhere in "
            + $"CLAUDE.md (or not at all): {string.Join(", ", unrouted)}.");
    }

    /// <summary>
    /// Each guide says what it is and points back at the index, so a reader who arrives at one
    /// directly — from a search, or from another guide — learns that the index carries the rules
    /// that apply everywhere and that open work is in <c>PROGRESS.md</c>.
    /// </summary>
    [Fact]
    public void EveryGuidePointsBackAtTheIndexAndAtProgress()
    {
        var faults = new List<string>();

        foreach (var name in GuideFiles())
        {
            var text = File.ReadAllText(Path.Combine(GuideDirectory, name));

            if (!text.Contains("../../CLAUDE.md", StringComparison.Ordinal))
                faults.Add($"{name} does not link back to CLAUDE.md");

            if (!text.Contains("../../PROGRESS.md", StringComparison.Ordinal))
                faults.Add($"{name} does not point at PROGRESS.md");

            if (!text.TrimStart().StartsWith("# ", StringComparison.Ordinal))
                faults.Add($"{name} does not open with a title");
        }

        Assert.True(faults.Count == 0, string.Join("; ", faults));
    }

    /// <summary>
    /// Every source directory a pointer can be written in. <b>Build output is excluded and that is
    /// not tidiness</b>: <c>web/bin</c> holds framework assemblies whose bytes contain the letters
    /// <c>Tests</c>, so a sweep that read them would key this guarantee to whether the project had
    /// been built and in which configuration.
    /// </summary>
    private static IEnumerable<string> SourceFiles() =>
        new[] { "engine", "sheets", "web", "play", "cli", "mcp", "mcp-play", "mcp-shared" }
            .Select(d => Path.Combine(RepoRoot, d))
            .Where(Directory.Exists)
            .SelectMany(d => Directory.GetFiles(d, "*.cs", SearchOption.AllDirectories)
                .Concat(Directory.GetFiles(d, "*.razor", SearchOption.AllDirectories)))
            .Where(f => !f.Contains($"{Path.DirectorySeparatorChar}obj{Path.DirectorySeparatorChar}",
                                    StringComparison.Ordinal)
                     && !f.Contains($"{Path.DirectorySeparatorChar}bin{Path.DirectorySeparatorChar}",
                                    StringComparison.Ordinal));

    /// <summary>
    /// <b>A test class named in a doc comment has to exist.</b> The rule this file opens with —
    /// a dead pointer is worse than no pointer — was enforced for <c>docs/guide/</c> paths and for
    /// the test names <c>PROGRESS.md</c> gives, and for nothing a doc comment says. Those are the
    /// pointers most likely to rot, because a comment beside the code is where the argument for a
    /// guard actually lives: <c>CampaignAsset</c> said "<c>CampaignAssetShapeTests</c> holds the
    /// two name sets together" about a class that has never existed, and
    /// <c>PowerModel</c> named <c>ImmortalityCampaignCostTests</c> for a file called
    /// <c>ImmortalityHousePriceTests</c>. Both send a reader looking for a guarantee they cannot
    /// find, and neither breaks a build.
    ///
    /// <para><b>The class and not the method</b>, deliberately. A method name moves with an
    /// ordinary rename and would make this an obstacle at the wrong moment; a class that is not
    /// there at all is the failure worth catching, and it is the one both of these were.</para>
    ///
    /// <para>The count is the positive control. A sweep that had stopped finding pointers would
    /// satisfy an empty loop and say nothing, which is the single most common way a check in this
    /// repository has been wrong.</para>
    /// </summary>
    [Fact]
    public void EveryTestClassASourceCommentNamesExists()
    {
        var declared = new Regex(@"class\s+([A-Za-z][A-Za-z0-9]*Tests)\b",
                                 RegexOptions.None, TimeSpan.FromSeconds(5))
            .Matches(string.Join("\n", Directory
                .GetFiles(Path.Combine(RepoRoot, "tests"), "*.cs", SearchOption.AllDirectories)
                .Where(f => !f.Contains($"{Path.DirectorySeparatorChar}obj{Path.DirectorySeparatorChar}",
                                        StringComparison.Ordinal)
                         && !f.Contains($"{Path.DirectorySeparatorChar}bin{Path.DirectorySeparatorChar}",
                                        StringComparison.Ordinal))
                .Select(File.ReadAllText)))
            .Select(m => m.Groups[1].Value)
            .ToHashSet(StringComparer.Ordinal);

        Assert.True(declared.Count >= 50,
            $"Only {declared.Count} test classes were found, which means this scan has stopped "
            + "finding them rather than that the suite has shrunk.");

        var named = new Regex(@"[A-Za-z][A-Za-z0-9]*Tests", RegexOptions.None, TimeSpan.FromSeconds(5));

        var dead = new List<string>();
        var seen = 0;

        foreach (var file in SourceFiles())
        {
            foreach (var pointer in named.Matches(File.ReadAllText(file))
                         .Select(m => m.Value).Distinct(StringComparer.Ordinal))
            {
                seen++;

                if (!declared.Contains(pointer))
                    dead.Add($"{Path.GetFileName(file)} names {pointer}");
            }
        }

        Assert.True(seen >= 30,
            $"Only {seen} test pointers were found in the source, which means this scan has "
            + "stopped finding them rather than that the comments have stopped naming tests.");

        Assert.True(dead.Count == 0,
            "A doc comment names a test class that does not exist, which sends a reader hunting "
            + "for a guarantee they cannot find: " + string.Join("; ", dead));
    }
}
