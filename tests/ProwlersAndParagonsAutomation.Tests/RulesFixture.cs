using ProwlersAndParagonsAutomation.Engine;

namespace ProwlersAndParagonsAutomation.Tests;

/// <summary>
/// Loads the real data/rules JSON once for the whole test run. Tests assert against
/// the shipped data deliberately: the point is to catch a rules file drifting away
/// from the rulebook, not to exercise hand-built fixtures.
/// </summary>
public sealed class RulesFixture
{
    public RulesRepository Rules { get; }
    public CostCalculator Costs { get; }
    public DerivedStatsCalculator Derived { get; }
    public CharacterValidator Validator { get; }

    public RulesFixture()
    {
        Rules     = RulesRepository.FromBasePath(FindRepoRoot());
        Costs     = new CostCalculator(Rules);
        Derived   = new DerivedStatsCalculator(Rules);
        Validator = new CharacterValidator(Rules, Costs, Derived);
    }

    /// <summary>The data/rules directory the fixture loaded, for tests that read the raw JSON.</summary>
    public static string DataPath => Path.Combine(FindRepoRoot(), "data", "rules");

    /// <summary>
    /// The repository root, for tests that read source files rather than call into them —
    /// <see cref="WebPresentationTests"/> holds the browser front end to rules that are
    /// about how its markup and stylesheets are written, which no assembly exposes.
    /// </summary>
    public static string RepoRoot => FindRepoRoot();

    /// <summary>A Standard-tier sheet (125 HP, 12d trait cap) with no traits bought.</summary>
    public static CharacterSheet StandardSheet() => new() { SelectedTierId = "standard" };

    /// <summary>
    /// A Standard-tier sheet that is actually legal: every Ability and Talent at the 1d minimum,
    /// and one flaw.
    ///
    /// <para><b>This exists because "an empty sheet plus the thing under test" was not legal and
    /// several tests assumed it was.</b> Ch.2 states, once for Abilities and again for Talents,
    /// that no rank can be lower than 1d — a character has all eighteen. Until that was enforced,
    /// a sheet with one Ability set and seventeen Traits at 0d validated clean, and every test
    /// built on one was quietly asserting things about an impossible character.</para>
    /// </summary>
    public CharacterSheet LegalSheet()
    {
        var sheet = StandardSheet();

        foreach (var ability in Rules.Abilities) sheet.AbilityRanks[ability.Id] = 1;
        foreach (var talent in Rules.Talents) sheet.TalentRanks[talent.Id] = 1;

        sheet.Flaws.Add(new SelectedFlaw(Rules.Flaws[0].Id));

        return sheet;
    }

    private static string FindRepoRoot()
    {
        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        while (dir is not null)
        {
            if (dir.GetFiles("*.sln").Length > 0) return dir.FullName;
            dir = dir.Parent;
        }

        throw new InvalidOperationException(
            $"Could not locate the repository root (no .sln found above {AppContext.BaseDirectory}).");
    }
}

/// <summary>
/// Shares one <see cref="RulesFixture"/> across every test class, so the JSON is
/// parsed once per run. Not named *Collection: CA1711 reserves that suffix.
/// </summary>
[CollectionDefinition(Name)]
public sealed class SharedRules : ICollectionFixture<RulesFixture>
{
    public const string Name = "rules";
}
