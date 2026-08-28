namespace ProwlersAndParagonsAutomation.Tests;

/// <summary>
/// The three fields on a character that the engine is never given — two of them presentation,
/// and one of them an indirection.
///
/// <para><b>Two kinds of bar, and conflating them would lose the second.</b>
/// <c>CharacterSheet.IsVillain</c> and <c>CharacterSheet.UnlimitedBudget</c> are barred as
/// <i>presentation</i>: a palette and a way of working, and a rule branching on either is the
/// browser deciding a rule. <c>CharacterSheet.CampaignId</c> is barred as an <i>indirection</i>:
/// it is an id, and the only thing rules code could do with one is resolve it — which means
/// asking storage, which in a browser is asynchronous, which <c>IRulesSource</c> is deliberately
/// synchronous to forbid. <c>AccountsContractTests.TheEngineHasNoFilesystemAccess</c> and
/// <c>TheEngineHasNoNetwork</c> already ban both ways such a lookup could be written; this bans
/// the field it would start from. A campaign is resolved in <c>web/</c> and nowhere else.</para>
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
    private static readonly string[] Flags = ["IsVillain", "UnlimitedBudget", "CampaignId"];

    /// <summary>The projects that decide a cost, a rank, a figure or a verdict.</summary>
    private static readonly string[] RulesProjects = ["engine", "sheets"];

    /// <summary>
    /// The file the three fields are declared on, and the one the scan below skips.
    /// <c>CharacterSheetJson</c> is not on this list and does not need to be — it round-trips
    /// the whole type by reflection and names no property at all, which is why the fields
    /// persist without anybody wiring them up.
    /// </summary>
    private const string Declaration = "CharacterSheet.cs";

    /// <summary>
    /// The second file skipped, and it is skipped for a much narrower reason than the first.
    ///
    /// <para><c>Campaign</c> declares a setting of its own that happens to share a name with one
    /// of the flags above — the sandbox, one level up, on the game rather than on the character.
    /// The rule being enforced is "no rules code <em>reads</em> a flag off a sheet", and a record
    /// with no body cannot read anything, so a name collision here is not the thing this test is
    /// about. <b>What keeps that from being a loophole is
    /// <see cref="TheOnlyOtherSkippedFileHasNoLogicInItAtAll"/></b>, which requires the file to
    /// stay a bare record declaration: the moment somebody gives it a method, it stops being
    /// skipped for free.</para>
    /// </summary>
    private const string CampaignRecord = "Campaign.cs";

    [Fact]
    public void NoRulesCodeReadsAPresentationFlag()
    {
        var offenders = new List<string>();

        foreach (var file in RulesSources())
        {
            var name = Path.GetFileName(file);
            if (name == Declaration || name == CampaignRecord) continue;

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
    /// <para><b>A scan for three names is satisfied completely by three names that no longer
    /// exist.</b> Rename any of the fields and the guard above passes on every file for the wrong
    /// reason, reporting a discipline it has stopped checking. So the declaration is required to
    /// still contain all three, and the search is required to have looked at a real set of
    /// files.</para>
    ///
    /// <para><b>And "contains the name" is deliberately not enough.</b> A field renamed away
    /// usually leaves its old name behind in the paragraph explaining the rename — which is
    /// exactly the file this control reads, and exactly the reason a bare <c>Contains</c> has been
    /// defeated twice elsewhere in this repository. Each name has to appear as a real
    /// <c>public … Name { get; set; }</c> declaration, so a mention in prose cannot hold the
    /// control up on its own.</para>
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
            Assert.True(
                System.Text.RegularExpressions.Regex.IsMatch(
                    text, $@"public\s+[\w?<>\[\]]+\s+{flag}\s*\{{\s*get",
                    System.Text.RegularExpressions.RegexOptions.None, TimeSpan.FromSeconds(5)),
                $"{Declaration} no longer declares a property called {flag}, so the guard is "
                + "scanning for a name that does not exist and would pass whatever the rules "
                + "code did."));
    }

    /// <summary>
    /// The second skipped file is skipped for free only while it cannot read anything.
    ///
    /// <para><c>Campaign.cs</c> is excused from the scan because it declares a setting whose name
    /// collides with one of the flags, on the game rather than on the character. That excuse holds
    /// only for a bare record declaration: a record with a body could branch on a sheet's flag and
    /// be skipped while doing it. After comments are removed there is no brace in the file at all
    /// — no method, no property body, no initialiser — which is a crisp thing to check and turns
    /// red the moment somebody puts logic there.</para>
    /// </summary>
    [Fact]
    public void TheOnlyOtherSkippedFileHasNoLogicInItAtAll()
    {
        var path = RulesSources().SingleOrDefault(f => Path.GetFileName(f) == CampaignRecord);

        Assert.True(path is not null,
            $"{CampaignRecord} is skipped by the scan above and is not there, so the skip is "
            + "excusing a file that no longer exists.");

        var live = string.Concat(File.ReadAllLines(path!)
            .Where(line => !line.TrimStart().StartsWith("//", StringComparison.Ordinal)));

        // The positive control on the stripping: the declaration itself survives it. A stripper
        // that ate the whole file would satisfy the brace assertion by measuring nothing.
        Assert.Contains("record Campaign", live, StringComparison.Ordinal);

        Assert.DoesNotContain("{", live, StringComparison.Ordinal);
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
