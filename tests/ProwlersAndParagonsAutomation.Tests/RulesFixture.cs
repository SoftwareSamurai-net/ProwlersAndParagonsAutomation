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

    /// <summary>A Standard-tier sheet (125 HP, 12d trait cap) with no traits bought.</summary>
    public static CharacterSheet StandardSheet() => new() { SelectedTierId = "standard" };

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
