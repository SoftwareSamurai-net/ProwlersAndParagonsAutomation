namespace ProwlersAndParagonsAutomation.Tests;

/// <summary>
/// The two fields on a character that the engine is never given.
///
/// <para><b>This test is the whole justification for those fields existing.</b>
/// <c>CharacterSheet.IsVillain</c> and <c>CharacterSheet.UnlimitedBudget</c> are presentation:
/// they are on the character so that an exported sheet keeps its palette and its sandbox setting
/// when it is read back, and for no other reason. <c>CLAUDE.md</c> refused a mode field here for
/// years, correctly, because "Villain" then meant a palette <em>and</em> no Hero Point budget —
/// and the second half is mechanical. Splitting them is what made a field defensible, and what
/// keeps it defensible is that nothing in the rules can see it.</para>
///
/// <para><b>Without this, the fields rot into real inputs the first time somebody finds one
/// convenient.</b> A single <c>if (sheet.IsVillain)</c> in the validator would make the browser
/// decide a rule, silently, and every other test in this suite would stay green — the sample
/// characters are legal either way and no printed Hero would move. That is exactly the shape of
/// defect this repository keeps finding late.</para>
/// </summary>
public sealed class PresentationFlagsTests
{
    /// <summary>The names no rules code may mention.</summary>
    private static readonly string[] Flags = ["IsVillain", "UnlimitedBudget"];

    /// <summary>The projects that decide a cost, a rank, a figure or a verdict.</summary>
    private static readonly string[] RulesProjects = ["engine", "sheets"];

    /// <summary>
    /// The one file allowed to name them: the declaration itself. <c>CharacterSheetJson</c> is
    /// not on this list and does not need to be — it round-trips the whole type by reflection
    /// and names no property at all, which is why the fields persist without anybody wiring
    /// them up.
    /// </summary>
    private const string Declaration = "CharacterSheet.cs";

    [Fact]
    public void NoRulesCodeReadsAPresentationFlag()
    {
        var offenders = new List<string>();

        foreach (var file in RulesSources())
        {
            if (Path.GetFileName(file) == Declaration) continue;

            var text = File.ReadAllText(file);

            foreach (var flag in Flags)
            {
                if (!text.Contains(flag, StringComparison.Ordinal)) continue;
                offenders.Add($"{Path.GetFileName(file)} names {flag}");
            }
        }

        Assert.True(
            offenders.Count == 0,
            "These are rules code and must not be able to see a presentation flag — a rule that "
            + "branches on one is the browser deciding a rule:\n  "
            + string.Join("\n  ", offenders));
    }

    /// <summary>
    /// The positive control, and it is not optional.
    ///
    /// <para><b>A scan for two names is satisfied completely by two names that no longer
    /// exist.</b> Rename either field and the guard above passes on every file for the wrong
    /// reason, reporting a discipline it has stopped checking. So the declaration is required to
    /// still contain both, and the search is required to have looked at a real set of files.</para>
    /// </summary>
    [Fact]
    public void TheGuardIsLookingAtSomething()
    {
        var files = RulesSources().ToList();

        // A path that has moved leaves the scan above with nothing to scan and nothing to say.
        Assert.True(files.Count > 10, $"only {files.Count} rules source files found");

        var declaration = files.SingleOrDefault(f => Path.GetFileName(f) == Declaration);
        Assert.NotNull(declaration);

        var text = File.ReadAllText(declaration);
        Assert.All(Flags, flag =>
            Assert.True(text.Contains(flag, StringComparison.Ordinal),
                $"{Declaration} no longer declares {flag}, so the guard is scanning for a name "
                + "that does not exist and would pass whatever the rules code did."));
    }

    /// <summary>
    /// Everything that decides a cost, a rank, a figure or a verdict: the engine and the
    /// renderers that read it. The hosts are deliberately absent — presentation is their job,
    /// and the browser reads both flags on purpose.
    /// </summary>
    private static IEnumerable<string> RulesSources() =>
        RulesProjects
            .Select(dir => Path.Combine(RulesFixture.RepoRoot, dir))
            .Where(Directory.Exists)
            .SelectMany(dir => Directory.EnumerateFiles(dir, "*.cs", SearchOption.AllDirectories))
            .Where(f => !f.Contains($"{Path.DirectorySeparatorChar}obj{Path.DirectorySeparatorChar}", StringComparison.Ordinal)
                     && !f.Contains($"{Path.DirectorySeparatorChar}bin{Path.DirectorySeparatorChar}", StringComparison.Ordinal));
}
