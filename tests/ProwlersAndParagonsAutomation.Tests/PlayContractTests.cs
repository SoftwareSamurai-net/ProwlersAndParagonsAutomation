using System.Text.RegularExpressions;

namespace ProwlersAndParagonsAutomation.Tests;

/// <summary>
/// <b>The arrow from <c>engine/</c> to <c>play/</c> points one way, and <c>play/</c> prices nothing
/// and judges nothing.</b>
///
/// <para><c>CLAUDE.md</c> settles the first half in a line — <c>engine/</c> is the authority on cost
/// and validity and knows nothing about resolving an action, so a combat simulator is a
/// <em>second</em> engine beside it rather than a change to it. The project reference makes that
/// true at build time in one direction; nothing but this says the other direction is empty by
/// intent rather than by accident, and "no reference yet" and "no reference ever" look identical
/// from inside a build that happens to compile.</para>
///
/// <para><b>The second half is the one an ordinary-looking commit would break.</b> A simulator that
/// reached for <c>CostCalculator</c> to ask what an attack was worth, or for
/// <c>CharacterValidator</c> to ask whether a combatant was legal, would compile, pass, and quietly
/// make the encounter an authority on questions it has no business answering. <c>play/</c> resolves
/// an action; it does not price one and it does not rule on one. An illegal character is reported
/// by the judge, never repaired — and never re-judged here.</para>
///
/// <para>The two promises about accounts, the filesystem and the network are held by
/// <see cref="AccountsContractTests"/>, which lists <c>play/</c> among its rules projects, and the
/// promise about presentation flags by <see cref="PresentationFlagsTests"/>, which does too. They
/// are not repeated here.</para>
/// </summary>
public sealed class PlayContractTests
{
    private static string RepoRoot => RulesFixture.RepoRoot;

    /// <summary>The play project's assembly name and root namespace, as a source file would spell them.</summary>
    private const string PlayAssembly = "ProwlersAndParagons.Play";

    /// <inheritdoc cref="PlayAssembly"/>
    private const string PlayNamespace = "ProwlersAndParagonsAutomation.Play";

    private static IEnumerable<string> SourcesUnder(string tree, string pattern) =>
        Directory.Exists(Path.Combine(RepoRoot, tree))
            ? Directory.EnumerateFiles(Path.Combine(RepoRoot, tree), pattern, SearchOption.AllDirectories)
                .Where(f => !f.Contains($"{Path.DirectorySeparatorChar}obj{Path.DirectorySeparatorChar}", StringComparison.Ordinal)
                         && !f.Contains($"{Path.DirectorySeparatorChar}bin{Path.DirectorySeparatorChar}", StringComparison.Ordinal))
            : [];

    private static List<string> PlaySources() => SourcesUnder("play", "*.cs").ToList();

    /// <summary>
    /// <b>Nothing under <c>engine/</c> or <c>sheets/</c> reaches the second engine, in either
    /// spelling.</b> A project reference is the loud way it would happen; a <c>using</c> smuggled in
    /// by a shared build property is the quiet one, so both are checked.
    /// </summary>
    [Fact]
    public void NothingBelowTheSecondEngineReferencesIt()
    {
        var faults = new List<string>();

        foreach (var tree in new[] { "engine", "sheets" })
        {
            foreach (var project in SourcesUnder(tree, "*.csproj"))
            {
                if (File.ReadAllText(project).Contains(PlayAssembly, StringComparison.Ordinal))
                    faults.Add($"{Path.GetRelativePath(RepoRoot, project)} references {PlayAssembly}");
            }

            foreach (var file in SourcesUnder(tree, "*.cs"))
            {
                if (File.ReadAllText(file).Contains(PlayNamespace, StringComparison.Ordinal))
                    faults.Add($"{Path.GetRelativePath(RepoRoot, file)} names {PlayNamespace}");
            }
        }

        Assert.True(faults.Count == 0,
            "engine/ and sheets/ are the layer play/ is built on, so a reference back would make "
            + "the dependency arrow a cycle and the character engine an authority on resolving an "
            + "action: " + string.Join(", ", faults));
    }

    /// <summary>
    /// The positive control for the scan above, and it is not a formality: two constants that no
    /// longer name anything would satisfy every absence assertion in this file at once.
    /// </summary>
    [Fact]
    public void TheGuardIsLookingAtSomething()
    {
        var play = PlaySources();

        Assert.True(play.Count >= 5,
            $"only {play.Count} source files found under play/, so these scans would pass by "
            + "measuring nothing.");

        var csproj = Path.Combine(RepoRoot, "play", $"{PlayAssembly}.csproj");
        Assert.True(File.Exists(csproj), $"{csproj} is not there, so the assembly name scanned for is not a real one.");

        Assert.Contains(
            $"namespace {PlayNamespace}",
            string.Concat(play.Select(File.ReadAllText)),
            StringComparison.Ordinal);

        // And the reference that *should* exist does: play/ reads the character engine. Without
        // this the whole file would be satisfied by a project that referenced nothing at all.
        Assert.Contains("ProwlersAndParagons.Engine", File.ReadAllText(csproj), StringComparison.Ordinal);
    }

    /// <summary>
    /// <b>Exactly two projects in the solution reference <c>play/</c>: the test project and the
    /// encounter server.</b>
    ///
    /// <para><b>This used to say "one, and it is the test project", and the host it was waiting for
    /// has arrived.</b> The sentence it was protecting was never "nothing references it" for its own
    /// sake — it was that the first host to reach for the second engine should have to come past
    /// this test and its message rather than past nobody. It did: <c>mcp-play/</c> is that host, and
    /// it is a server of its own precisely so that the arrow lands somewhere that answers no
    /// question about cost or validity.</para>
    ///
    /// <para><b>What has not changed is which projects may not.</b> <c>engine/</c>, <c>sheets/</c>,
    /// <c>cli/</c>, <c>web/</c> and <c>mcp/</c> are all still forbidden, by exactly this list — a
    /// character server that referenced the second engine would be one program answering both
    /// questions, which is the arrangement CLAUDE.md settles against in a line. An allowlist rather
    /// than a denylist, so a third host added under a name nobody anticipated is flagged rather than
    /// missed.</para>
    /// </summary>
    [Fact]
    public void OnlyTheTestProjectAndTheEncounterServerReferenceTheSecondEngine()
    {
        var referencing = Directory
            .EnumerateFiles(RepoRoot, "*.csproj", SearchOption.AllDirectories)
            .Where(f => !f.Contains($"{Path.DirectorySeparatorChar}obj{Path.DirectorySeparatorChar}", StringComparison.Ordinal)
                     && !f.Contains($"{Path.DirectorySeparatorChar}bin{Path.DirectorySeparatorChar}", StringComparison.Ordinal))
            .Where(f => !string.Equals(Path.GetFileName(f), $"{PlayAssembly}.csproj", StringComparison.Ordinal))
            .Where(f => File.ReadAllText(f).Contains($"{PlayAssembly}.csproj", StringComparison.Ordinal))
            .Select(f => Path.GetFileName(f))
            .Order(StringComparer.Ordinal)
            .ToList();

        Assert.Equal(
            ["ProwlersAndParagons.McpPlay.csproj", "ProwlersAndParagonsAutomation.Tests.csproj"],
            referencing);
    }

    /// <summary>
    /// <b><c>play/</c> prices nothing and judges nothing.</b> The two type names are the whole of
    /// the claim: <c>CostCalculator</c> is the authority on what something costs in Hero Points and
    /// <c>CharacterValidator</c> on whether a character is legal, and neither question is asked
    /// during a fight. A simulator that consulted either would be answering a design question about
    /// somebody's character in the middle of resolving an action.
    ///
    /// <para><b>Comments are blanked first</b>, for the reason
    /// <see cref="AccountsContractTests.TheEngineHasNoFilesystemAccess"/> blanks them: this very
    /// rule has to be explained in a doc comment somewhere in <c>play/</c>, and a guard that cannot
    /// tell an explanation from a call taxes the explanation.</para>
    /// </summary>
    [Fact]
    public void TheSecondEngineNeitherPricesNorJudges()
    {
        string[] banned = ["CostCalculator", "CharacterValidator", "ValidationResult", "ValidationIssue"];

        var faults = new List<string>();

        foreach (var file in PlaySources())
        {
            var live = WithoutCsComments(File.ReadAllText(file));

            faults.AddRange(
                banned.Where(name => Regex.IsMatch(live, $@"\b{name}\b", RegexOptions.None, TimeSpan.FromSeconds(5)))
                      .Select(name => $"{Path.GetFileName(file)} names {name}"));
        }

        Assert.True(faults.Count == 0,
            "play/ resolves an action. It does not decide what anything costs and it does not "
            + "decide whether a character is legal — engine/ is the judge, and the judge reports "
            + "rather than repairs. These reach for one of its answers: " + string.Join(", ", faults));
    }

    /// <summary>
    /// The positive control for the ban above, in the shape this repository learned to insist on: a
    /// scan for four names that no longer exist passes on everything. Each has to be a real type
    /// somewhere under <c>engine/</c>.
    /// </summary>
    [Fact]
    public void TheNamesTheSecondEngineMayNotUseAreRealTypes()
    {
        var engine = string.Concat(SourcesUnder("engine", "*.cs").Select(File.ReadAllText));

        foreach (var name in new[] { "CostCalculator", "CharacterValidator", "ValidationResult", "ValidationIssue" })
        {
            Assert.True(
                Regex.IsMatch(engine, $@"(class|record|enum)\s+{name}\b", RegexOptions.None, TimeSpan.FromSeconds(5)),
                $"engine/ declares no type called {name}, so the ban above is scanning for a name "
                + "that does not exist and would pass whatever play/ did.");
        }
    }

    /// <summary>
    /// C# source with its comments blanked, so a match inside an explanation cannot anchor a scan
    /// meant to read live code. The same routine
    /// <see cref="AccountsContractTests.TheEngineHasNoFilesystemAccess"/> uses.
    /// </summary>
    private static string WithoutCsComments(string source)
    {
        var output = new System.Text.StringBuilder(source.Length);
        var i = 0;

        while (i < source.Length)
        {
            if (i + 1 < source.Length && source[i] == '/' && source[i + 1] == '/')
            {
                while (i < source.Length && source[i] != '\n') { output.Append(' '); i++; }
                continue;
            }

            if (i + 1 < source.Length && source[i] == '/' && source[i + 1] == '*')
            {
                while (i + 1 < source.Length && !(source[i] == '*' && source[i + 1] == '/'))
                {
                    output.Append(source[i] == '\n' ? '\n' : ' ');
                    i++;
                }

                for (var k = 0; k < 2 && i < source.Length; k++, i++) output.Append(' ');
                continue;
            }

            output.Append(source[i]);
            i++;
        }

        return output.ToString();
    }
}
