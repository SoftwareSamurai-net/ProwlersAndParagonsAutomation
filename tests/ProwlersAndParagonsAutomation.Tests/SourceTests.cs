using ProwlersAndParagonsAutomation.Cli.Export;
using ProwlersAndParagonsAutomation.Engine;

namespace ProwlersAndParagonsAutomation.Tests;

/// <summary>
/// Sources (Ch.2, p.15). Six of them, each naming the Ability that stands in as a rankless
/// Power's rank when another Power acts on it.
///
/// <para>Values are transcribed from the Sources table. If one of these fails, check p.15 —
/// do not edit the expectation to match the code.</para>
/// </summary>
[Collection(SharedRules.Name)]
public sealed class SourceTests
{
    private readonly RulesFixture _f;

    public SourceTests(RulesFixture fixture) => _f = fixture;

    /// <summary>The only two Abilities the Sources table names as a default rank.</summary>
    private static readonly string[] DefaultRankAbilities = ["toughness", "willpower"];

    // ── Against the rulebook ─────────────────────────────────────────────────

    /// <summary>
    /// The Sources table, in full. The split is even but not obvious: a Source is not
    /// "physical means Toughness" — Trained uses Willpower.
    /// </summary>
    [Theory]
    [InlineData("innate",  "toughness")]
    [InlineData("magic",   "willpower")]
    [InlineData("psychic", "willpower")]
    [InlineData("super",   "toughness")]
    [InlineData("tech",    "toughness")]
    [InlineData("trained", "willpower")]
    public void DefaultRankAbilityMatchesTheRulebook(string id, string ability)
    {
        var source = _f.Rules.GetSource(id);

        Assert.NotNull(source);
        Assert.Equal(ability, source.DefaultRankAbility);
    }

    [Fact]
    public void ThereAreExactlySixSources() => Assert.Equal(6, _f.Rules.Sources.Count);

    [Fact]
    public void EverySourceIsNamedDescribedAndSourced() =>
        Assert.All(_f.Rules.Sources, s =>
        {
            Assert.False(string.IsNullOrWhiteSpace(s.Name));
            Assert.False(string.IsNullOrWhiteSpace(s.Description));
            Assert.Contains("Ch.2", s.SourceRef, StringComparison.Ordinal);
            Assert.Contains(s.DefaultRankAbility, DefaultRankAbilities, StringComparer.Ordinal);
        });

    // ── Default rank ─────────────────────────────────────────────────────────

    /// <summary>
    /// Ch.2: a Power with no rank "uses a default rank in place of their rank when dealing
    /// with Powers that affect other Powers". Attuned is rankless, so its stand-in comes
    /// from the Source: Toughness under Tech, Willpower under Magic.
    /// </summary>
    [Theory]
    [InlineData("tech", 7)]      // Toughness
    [InlineData("innate", 7)]
    [InlineData("super", 7)]
    [InlineData("magic", 4)]     // Willpower
    [InlineData("psychic", 4)]
    [InlineData("trained", 4)]
    public void ARanklessPowerTakesItsDefaultRankFromItsSource(string sourceId, int expected)
    {
        var sheet = RulesFixture.StandardSheet();
        sheet.AbilityRanks["toughness"] = 7;
        sheet.AbilityRanks["willpower"] = 4;
        sheet.SelectedPowers.Add(new SelectedPower("attuned", 0) { SourceId = sourceId });

        Assert.Equal(expected, _f.Derived.GetRankAgainstPowers(sheet.SelectedPowers[0], sheet));
    }

    /// <summary>
    /// The default rank stands in only against other Powers. It is not the Power's rank,
    /// and folding it into the effective rank would raise Edge and Resolve above the
    /// figures the published sheets print.
    /// </summary>
    [Fact]
    public void TheDefaultRankIsNotTheEffectiveRank()
    {
        var sheet = RulesFixture.StandardSheet();
        sheet.AbilityRanks["toughness"] = 9;
        var attuned = new SelectedPower("attuned", 0) { SourceId = "tech" };
        sheet.SelectedPowers.Add(attuned);

        Assert.Equal(0, _f.Derived.GetEffectiveRank(attuned, sheet));
        Assert.Equal(9, _f.Derived.GetRankAgainstPowers(attuned, sheet));
    }

    [Fact]
    public void ARankedPowerUsesItsOwnRankAgainstOtherPowers()
    {
        var sheet = RulesFixture.StandardSheet();
        sheet.AbilityRanks["toughness"] = 12;
        var blast = new SelectedPower("blast", 8) { SourceId = "tech" };
        sheet.SelectedPowers.Add(blast);

        // Its own 8 ranks, not Toughness 12.
        Assert.Equal(8, _f.Derived.GetRankAgainstPowers(blast, sheet));
    }

    [Fact]
    public void ARanklessPowerWithNoSourceHasNoDefaultRank()
    {
        var sheet = RulesFixture.StandardSheet();
        sheet.AbilityRanks["toughness"] = 9;
        var attuned = new SelectedPower("attuned", 0);
        sheet.SelectedPowers.Add(attuned);

        Assert.Equal(0, _f.Derived.GetRankAgainstPowers(attuned, sheet));
        Assert.Contains(_f.Validator.Validate(sheet).Issues,
            i => i.Code == "RANKLESS_POWER_WITHOUT_SOURCE" && i.Severity == ValidationSeverity.Warning);
    }

    [Fact]
    public void AnUnknownSourceIsAnError()
    {
        var sheet = RulesFixture.StandardSheet();
        sheet.SelectedPowers.Add(new SelectedPower("blast", 4) { SourceId = "cosmic" });

        Assert.Contains(_f.Validator.Validate(sheet).Issues,
            i => i.Code == "UNKNOWN_SOURCE" && i.Severity == ValidationSeverity.Error);
    }

    /// <summary>A Source costs nothing — it says what a Trait is, it does not buy anything.</summary>
    [Fact]
    public void ChoosingASourceCostsNothing()
    {
        var without = RulesFixture.StandardSheet();
        without.SelectedPowers.Add(new SelectedPower("blast", 8));

        var with = RulesFixture.StandardSheet();
        with.SelectedPowers.Add(new SelectedPower("blast", 8) { SourceId = "tech" });

        Assert.Equal(_f.Costs.TotalCost(without), _f.Costs.TotalCost(with));
    }

    // ── Grouping for the sheet ───────────────────────────────────────────────

    [Fact]
    public void HeadingsReadTheWayAPublishedSheetPrintsThem()
    {
        Assert.Equal("TECH POWERS",  SourceGrouping.HeadingFor(_f.Rules.GetSource("tech")));
        Assert.Equal("MAGIC POWERS", SourceGrouping.HeadingFor(_f.Rules.GetSource("magic")));
        Assert.Equal("POWERS",       SourceGrouping.HeadingFor(null));
    }

    [Fact]
    public void GroupsFollowRulesFileOrderNotInsertionOrder()
    {
        var sheet = RulesFixture.StandardSheet();
        sheet.SelectedPowers.Add(new SelectedPower("blast", 4) { SourceId = "trained" });
        sheet.SelectedPowers.Add(new SelectedPower("armor", 4) { SourceId = "innate" });

        var headings = new SourceGrouping(_f.Rules).GroupPowers(sheet).Select(g => g.Heading);

        // innate precedes trained in sources.json, so a sheet does not reshuffle itself
        // as Powers are added.
        Assert.Equal<IEnumerable<string>>(["INNATE POWERS", "TRAINED POWERS"], headings);
    }

    /// <summary>
    /// A Power with no Source is still printed, under a plain heading at the end. The
    /// validator warns about it; leaving it off its own character sheet would be worse.
    /// </summary>
    [Fact]
    public void AnUnsourcedPowerIsListedLastRatherThanDropped()
    {
        var sheet = RulesFixture.StandardSheet();
        sheet.SelectedPowers.Add(new SelectedPower("blast", 4) { SourceId = "tech" });
        sheet.SelectedPowers.Add(new SelectedPower("armor", 4));

        var groups = new SourceGrouping(_f.Rules).GroupPowers(sheet);

        Assert.Equal<IEnumerable<string>>(["TECH POWERS", "POWERS"], groups.Select(g => g.Heading));
        Assert.Equal(sheet.SelectedPowers.Count, groups.Sum(g => g.Powers.Count));
    }

    /// <summary>
    /// The exported sheet prints the Source headings, not a flat Powers list. Grouping is
    /// only worth having if the thing a player actually reads uses it.
    /// </summary>
    [Fact]
    public void TheExportedSheetPrintsSourceHeadings()
    {
        var sheet = RulesFixture.StandardSheet();
        sheet.Name = "Source Rendering";
        sheet.AbilityRanks["toughness"] = 5;
        sheet.SelectedPowers.Add(new SelectedPower("telepathy", 9) { SourceId = "super" });
        sheet.SelectedPowers.Add(new SelectedPower("communications", 0) { SourceId = "tech" });
        sheet.SelectedPowers.Add(new SelectedPower("leaping", 3));

        var outDir = Path.Combine(Path.GetTempPath(), "pp-source-render-" + Guid.NewGuid().ToString("N"));
        try
        {
            var (txtPath, _) = new CharacterSheetExporter().Export(
                sheet, _f.Rules, _f.Costs, _f.Derived, _f.Validator.Validate(sheet), outDir);

            var txt = File.ReadAllText(txtPath);

            Assert.Contains("SUPER POWERS", txt, StringComparison.Ordinal);
            Assert.Contains("TECH POWERS", txt, StringComparison.Ordinal);

            // Groups follow sources.json order, and the unsourced Power trails the rest
            // rather than vanishing from the sheet.
            Assert.True(txt.IndexOf("SUPER POWERS", StringComparison.Ordinal) <
                        txt.IndexOf("TECH POWERS", StringComparison.Ordinal));
            Assert.Contains("Leaping", txt, StringComparison.Ordinal);
        }
        finally
        {
            if (Directory.Exists(outDir)) Directory.Delete(outDir, recursive: true);
        }
    }

    [Fact]
    public void GroupingNeverLosesOrDuplicatesAPower()
    {
        var sheet = RulesFixture.StandardSheet();
        sheet.SelectedPowers.Add(new SelectedPower("blast", 4) { SourceId = "tech" });
        sheet.SelectedPowers.Add(new SelectedPower("armor", 4) { SourceId = "tech" });
        sheet.SelectedPowers.Add(new SelectedPower("flight", 4) { SourceId = "magic" });
        sheet.SelectedPowers.Add(new SelectedPower("leaping", 4));

        var listed = new SourceGrouping(_f.Rules).GroupPowers(sheet)
            .SelectMany(g => g.Powers).Select(p => p.PowerId).ToList();

        Assert.Equal(sheet.SelectedPowers.Select(p => p.PowerId).Order(), listed.Order());
        Assert.Equal(listed.Count, listed.Distinct(StringComparer.Ordinal).Count());
    }
}
