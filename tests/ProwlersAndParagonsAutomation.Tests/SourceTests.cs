using System.Text.Json;
using ProwlersAndParagonsAutomation.Cli.Export;
using ProwlersAndParagonsAutomation.Engine;
using ProwlersAndParagonsAutomation.Sheets;

namespace ProwlersAndParagonsAutomation.Tests;

/// <summary>
/// Sources (Ch.2, p.16). Six of them, each naming the Ability that stands in as a rankless
/// Power's rank when another Power acts on it.
///
/// <para>Values are transcribed from the Sources table. If one of these fails, check p.16 —
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
            // The page, not just the chapter. Reverting all six back to the p.15 they used to
            // carry left the suite green, and p.15 is Power Levels — the Sources table is on
            // p.16 (PDF 19; the footers print each number twice, interleaved).
            Assert.Contains("Ch.2 Sources, p.16", s.SourceRef, StringComparison.Ordinal);
            Assert.Contains(s.DefaultRankAbility, DefaultRankAbilities, StringComparer.Ordinal);
        });

    // ── Default rank ─────────────────────────────────────────────────────────

    /// <summary>
    /// Ch.2: Powers with no rank "use a default rank in place of their rank when dealing
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
            i => i is { Code: "RANKLESS_POWER_WITHOUT_SOURCE", Severity: ValidationSeverity.Warning });
    }

    [Fact]
    public void AnUnknownSourceIsAnError()
    {
        var sheet = RulesFixture.StandardSheet();
        sheet.SelectedPowers.Add(new SelectedPower("blast", 4) { SourceId = "cosmic" });

        Assert.Contains(_f.Validator.Validate(sheet).Issues,
            i => i is { Code: "UNKNOWN_SOURCE", Severity: ValidationSeverity.Error });
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

        var headings = new SourceGrouping(_f.Rules).GroupBySource(sheet).Select(g => g.Heading);

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

        var groups = new SourceGrouping(_f.Rules).GroupBySource(sheet);

        Assert.Equal<IEnumerable<string>>(["TECH POWERS", "POWERS"], groups.Select(g => g.Heading));
        Assert.Equal(sheet.SelectedPowers.Count, groups.Sum(g => g.Powers.Count));
    }

    /// <summary>
    /// A Power naming a Source the rulebook does not have still prints, under the plain
    /// heading, exactly like one with no Source at all. Only the null case was covered, so
    /// narrowing the fallback to <c>SourceId is null</c> dropped such a Power from every
    /// sheet with the suite green — against this class's own stated principle that leaving a
    /// Power off its own character sheet is worse than showing it unsourced.
    /// </summary>
    [Fact]
    public void APowerWithAnUnknownSourceIsListedRatherThanDropped()
    {
        var sheet = RulesFixture.StandardSheet();
        sheet.SelectedPowers.Add(new SelectedPower("blast", 4) { SourceId = "tech" });
        sheet.SelectedPowers.Add(new SelectedPower("armor", 4) { SourceId = "cosmic" });

        var groups = new SourceGrouping(_f.Rules).GroupBySource(sheet);

        Assert.Equal<IEnumerable<string>>(["TECH POWERS", "POWERS"], groups.Select(g => g.Heading));
        Assert.Equal<IEnumerable<string>>(
            ["armor"], groups[^1].Powers.Select(p => p.PowerId));

        // Nothing is lost and nothing is duplicated, whichever bucket a Power lands in.
        Assert.Equal(sheet.SelectedPowers.Count, groups.Sum(g => g.Powers.Count));
    }

    /// <summary>
    /// The same for a Trait: a Source that is not one of the six prints under the plain
    /// heading rather than vanishing. The validator errors either way, but a Trait the player
    /// marked and no surface ever mentions again is the worse of the two failures, and the
    /// principle was being applied to Powers only.
    /// </summary>
    [Fact]
    public void ATraitWithAnUnknownSourceIsListedRatherThanDropped()
    {
        var sheet = RulesFixture.StandardSheet();
        sheet.AbilitySources["might"]     = "cosmic";
        sheet.TalentSources["academics"]  = "cosmic";

        var group = Assert.Single(new SourceGrouping(_f.Rules).GroupBySource(sheet));

        Assert.Equal("POWERS", group.Heading);
        Assert.Null(group.Source);
        Assert.Equal<IEnumerable<string>>(
            ["Abilities (Might)", "Talents (Academics)"], group.TraitLines);
    }

    /// <summary>
    /// A blank Source reads as "on the default" everywhere, including in the effective
    /// Source the JSON export writes. Reading the dictionary directly there made one export
    /// contradict itself: <c>effective_source: ""</c> beside a group structure that showed
    /// the Trait on its default.
    /// </summary>
    [Fact]
    public void ABlankSourceReadsAsTheDefaultEverywhere()
    {
        var sheet = RulesFixture.StandardSheet();
        sheet.AbilitySources["might"]    = "";
        sheet.TalentSources["academics"] = "  ";

        var grouping = new SourceGrouping(_f.Rules);

        Assert.Empty(grouping.GroupBySource(sheet));
        Assert.Equal(SourceGrouping.DefaultAbilitySourceId, grouping.EffectiveAbilitySource(sheet, "might"));
        Assert.Equal(SourceGrouping.DefaultTalentSourceId, grouping.EffectiveTalentSource(sheet, "academics"));
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

    // ── Sources on Abilities and Talents ─────────────────────────────────────

    /// <summary>
    /// A Trait left alone prints nothing. Ch.2 p.16 gives Abilities and Talents a default —
    /// Innate and Trained — so silence is an answer, not a gap, and a sheet that listed
    /// every Trait under INNATE POWERS would be reporting the rule back at the reader.
    /// </summary>
    [Fact]
    public void ATraitOnItsDefaultSourcePrintsNothing()
    {
        var sheet = RulesFixture.StandardSheet();
        sheet.AbilityRanks["might"]      = 8;
        sheet.TalentRanks["academics"]   = 6;

        Assert.Empty(new SourceGrouping(_f.Rules).GroupBySource(sheet));

        // Setting a Trait explicitly to its own default is the same statement as silence,
        // and prints the same nothing rather than an empty INNATE POWERS box. Enforced in
        // the engine rather than left to the editors, because a stored or hand-edited
        // character reaches the renderers without passing through either of them.
        sheet.AbilitySources["might"]    = SourceGrouping.DefaultAbilitySourceId;
        sheet.TalentSources["academics"] = SourceGrouping.DefaultTalentSourceId;

        Assert.Empty(new SourceGrouping(_f.Rules).GroupBySource(sheet));

        // A blank one is not something an editor writes, but stored data can carry it. It
        // prints nothing too — and, unlike the two above, the validator reports it.
        sheet.AbilitySources["might"] = "";

        Assert.Empty(new SourceGrouping(_f.Rules).GroupBySource(sheet));
    }

    /// <summary>
    /// "(All)" is a claim about the line it appears on, not about the Source. When a Con
    /// splits the Abilities onto two lines, the unmodified line names its Traits — it is no
    /// longer all of them. Counting the Source instead printed <c>Abilities (All)</c> above
    /// a line listing four of the six, with the other two on the line below it.
    /// </summary>
    [Fact]
    public void AllIsCountedOnTheLineNotOnTheSource()
    {
        var sheet = RulesFixture.StandardSheet();
        foreach (var ability in _f.Rules.Abilities) sheet.AbilitySources[ability.Id] = "tech";
        sheet.AbilityModifiers["might"]     = [new SelectedProCon("item")];
        sheet.AbilityModifiers["toughness"] = [new SelectedProCon("item")];

        var lines = new SourceGrouping(_f.Rules).GroupBySource(sheet).Single().TraitLines;

        Assert.Equal<IEnumerable<string>>(
            [
                "Abilities (Agility, Intellect, Perception, Willpower)",
                "Abilities (Might, Toughness) (Item)"
            ],
            lines);

        // The same trap one level up: the whole-character collapse must not fire either,
        // and every Ability must still be named exactly once across the lines.
        Assert.DoesNotContain("All", string.Join(" ", lines), StringComparison.Ordinal);
        Assert.All(_f.Rules.Abilities, a =>
            Assert.Equal(1, lines.Count(l => l.Contains(a.Name, StringComparison.Ordinal))));
    }

    /// <summary>
    /// A blank Source is reported rather than thrown. The type says a value here cannot be
    /// null and the deserializer does not care, so a stored character can carry one — and
    /// the lookup threw <c>ArgumentNullException</c> on it before the unknown-Source check
    /// could run. The same ordering trap has caught this validator twice already, on an
    /// unknown Power id and on gear that could not be priced.
    /// </summary>
    [Fact]
    public void ABlankTraitSourceIsReportedRatherThanThrown()
    {
        var sheet = RulesFixture.StandardSheet();
        sheet.AbilitySources["might"]    = null!;
        sheet.TalentSources["academics"] = "   ";

        var result = _f.Validator.Validate(sheet);

        Assert.Equal(2, result.Errors.Count(e => e.Code == "UNKNOWN_SOURCE"));
        Assert.Contains(result.Errors, e => e.Message.Contains("Ability 'Might'", StringComparison.Ordinal));
        Assert.Contains(result.Errors, e => e.Message.Contains("Talent 'Academics'", StringComparison.Ordinal));
    }

    /// <summary>
    /// Named Traits print in the rulebook's order, not the order they were set. Both
    /// published sheets that name more than one — Darkwolf and Stronghold — print Agility,
    /// Might, Perception, Toughness, which is <c>abilities.json</c> order.
    ///
    /// <para><b>No test here can tell that order from alphabetical</b>, because the rulebook
    /// happens to list both Abilities and Talents alphabetically. So this catches insertion
    /// order, which is the mistake actually available to make, and nothing more. A previous
    /// version of this comment claimed the two orders differed and gave an example that does
    /// not; it does not, and there is no counterexample to write.</para>
    /// </summary>
    [Fact]
    public void NamedTraitsPrintInTheRulebooksOrder()
    {
        var sheet = RulesFixture.StandardSheet();
        sheet.AbilitySources["toughness"]  = "tech";
        sheet.AbilitySources["agility"]    = "tech";
        sheet.AbilitySources["perception"] = "tech";
        sheet.AbilitySources["might"]      = "tech";

        var group = new SourceGrouping(_f.Rules).GroupBySource(sheet).Single();

        Assert.Equal("TECH POWERS", group.Heading);
        Assert.Equal<IEnumerable<string>>(
            ["Abilities (Agility, Might, Perception, Toughness)"], group.TraitLines);
    }

    /// <summary>
    /// Every Ability and every Talent on one Source collapses to the single line the two
    /// Heralds and Nano are printed with. A partial set must not: naming all six Abilities
    /// and eleven of the twelve Talents is not "All".
    /// </summary>
    [Fact]
    public void AWhollySingleSourcedCharacterCollapsesToOneLine()
    {
        var sheet = RulesFixture.StandardSheet();
        foreach (var ability in _f.Rules.Abilities) sheet.AbilitySources[ability.Id] = "magic";
        foreach (var talent in _f.Rules.Talents)    sheet.TalentSources[talent.Id]   = "magic";

        Assert.Equal<IEnumerable<string>>(
            ["Abilities and Talents (All)"],
            new SourceGrouping(_f.Rules).GroupBySource(sheet).Single().TraitLines);

        // One Talent short of the whole character, and the collapse must not happen.
        sheet.TalentSources.Remove(_f.Rules.Talents[^1].Id);

        var lines = new SourceGrouping(_f.Rules).GroupBySource(sheet).Single().TraitLines;
        Assert.Equal(2, lines.Count);
        Assert.Equal("Abilities (All)", lines[0]);
        Assert.DoesNotContain(_f.Rules.Talents[^1].Name, lines[1], StringComparison.Ordinal);
    }

    /// <summary>
    /// The Pros and Cons on a printed trait line belong to the whole line — Stronghold's
    /// four Abilities share one <c>(Item: armor)</c>. So Abilities on the same Source with
    /// different Cons print as separate lines rather than as one line carrying a Con that
    /// only applies to part of it.
    /// </summary>
    [Fact]
    public void AbilitiesAreSplitByTheModifiersTheyCarry()
    {
        var sheet = RulesFixture.StandardSheet();
        sheet.AbilitySources["might"]     = "tech";
        sheet.AbilitySources["toughness"] = "tech";
        sheet.AbilitySources["agility"]   = "tech";
        sheet.AbilityModifiers["might"]     = [new SelectedProCon("item")];
        sheet.AbilityModifiers["toughness"] = [new SelectedProCon("item")];

        var lines = new SourceGrouping(_f.Rules).GroupBySource(sheet).Single().TraitLines;

        Assert.Equal<IEnumerable<string>>(
            ["Abilities (Agility)", "Abilities (Might, Toughness) (Item)"], lines);
    }

    /// <summary>
    /// A Source group can exist with no Powers in it at all: a Trait bought through powered
    /// armour on a character who has no Tech Power. The heading is still the sheet's, and
    /// dropping the group would lose the only record of where that Trait came from.
    /// </summary>
    [Fact]
    public void ASourceWithOnlyATraitStillPrintsItsHeading()
    {
        var sheet = RulesFixture.StandardSheet();
        sheet.AbilitySources["might"] = "tech";
        sheet.SelectedPowers.Add(new SelectedPower("flight", 4) { SourceId = "magic" });

        var groups = new SourceGrouping(_f.Rules).GroupBySource(sheet);

        Assert.Equal<IEnumerable<string>>(["MAGIC POWERS", "TECH POWERS"], groups.Select(g => g.Heading));
        Assert.Empty(groups.Single(g => g.Heading == "TECH POWERS").Powers);
    }

    /// <summary>
    /// The default is what the Trait <em>is</em>, which is a different question from what the
    /// sheet prints. A front end asks this so it does not have to know the two defaults.
    /// </summary>
    [Fact]
    public void EffectiveSourceFallsBackToTheRulebookDefault()
    {
        var sheet = RulesFixture.StandardSheet();
        var grouping = new SourceGrouping(_f.Rules);

        Assert.Equal("innate",  grouping.EffectiveAbilitySource(sheet, "might"));
        Assert.Equal("trained", grouping.EffectiveTalentSource(sheet, "academics"));

        sheet.AbilitySources["might"] = "tech";
        Assert.Equal("tech", grouping.EffectiveAbilitySource(sheet, "might"));

        // Both defaults are Sources the rulebook actually lists, which a bare string is not.
        Assert.NotNull(_f.Rules.GetSource(SourceGrouping.DefaultAbilitySourceId));
        Assert.NotNull(_f.Rules.GetSource(SourceGrouping.DefaultTalentSourceId));
    }

    /// <summary>
    /// The text sheet prints the trait line inside its Source group, above the Powers, which
    /// is where a published sheet puts it — not on the Abilities block.
    /// </summary>
    [Fact]
    public void TheExportedSheetPrintsTheTraitLineInsideTheGroup()
    {
        var sheet = RulesFixture.StandardSheet();
        sheet.Name = "Trait Sources";
        sheet.AbilityRanks["might"]   = 8;
        sheet.AbilitySources["might"] = "tech";
        sheet.SelectedPowers.Add(new SelectedPower("blast", 4) { SourceId = "tech" });

        var txt = CharacterSheetRenderer.RenderText(
            sheet, _f.Rules, _f.Costs, _f.Derived, _f.Validator.Validate(sheet), DateTime.UnixEpoch);

        var heading = txt.IndexOf("TECH POWERS", StringComparison.Ordinal);
        var line    = txt.IndexOf("Abilities (Might)", StringComparison.Ordinal);
        var power   = txt.IndexOf("Blast", StringComparison.Ordinal);

        Assert.True(heading >= 0 && line > heading && power > line);

        // And not on the Abilities block, which stays a plain list of ranks.
        var abilities = txt.IndexOf("─── ABILITIES", StringComparison.Ordinal);
        Assert.True(abilities < heading);
        Assert.DoesNotContain("Abilities (Might)",
            txt[abilities..heading], StringComparison.Ordinal);
    }

    /// <summary>
    /// A character with a Trait Source and <b>no Powers at all</b> still gets its group on
    /// the text sheet. Both renderers used to decide "is there a Powers section?" by asking
    /// whether any Power was selected, and reverting either one leaves every other test
    /// green — a character built entirely out of powered armour prints "(none)" and loses
    /// the only record of where the armour came from.
    /// </summary>
    [Fact]
    public void ATraitSourceWithNoPowersAtAllStillPrintsOnTheTextSheet()
    {
        var sheet = RulesFixture.StandardSheet();
        sheet.Name = "No Powers At All";
        sheet.AbilityRanks["might"]   = 8;
        sheet.AbilitySources["might"] = "tech";

        Assert.Empty(sheet.SelectedPowers);

        var txt = CharacterSheetRenderer.RenderText(
            sheet, _f.Rules, _f.Costs, _f.Derived, _f.Validator.Validate(sheet), DateTime.UnixEpoch);

        Assert.Contains("TECH POWERS", txt, StringComparison.Ordinal);
        Assert.Contains("Abilities (Might)", txt, StringComparison.Ordinal);

        // And the "no Powers" placeholder must not also appear — the sheet either has a
        // Powers section or it does not.
        Assert.DoesNotContain("─── POWERS ", txt, StringComparison.Ordinal);
    }

    /// <summary>
    /// The other half: a character with neither Powers nor Trait Sources still prints the
    /// empty Powers section. Without this, deleting the placeholder branch entirely would
    /// satisfy the test above.
    /// </summary>
    [Fact]
    public void ACharacterWithNothingAtAllStillPrintsAnEmptyPowersSection()
    {
        var sheet = RulesFixture.StandardSheet();

        var txt = CharacterSheetRenderer.RenderText(
            sheet, _f.Rules, _f.Costs, _f.Derived, _f.Validator.Validate(sheet), DateTime.UnixEpoch);

        Assert.Contains("─── POWERS ", txt, StringComparison.Ordinal);
        Assert.Contains("(none)", txt, StringComparison.Ordinal);
    }

    /// <summary>
    /// The <c>Talents (…)</c> line, spelled out. Nothing else asserts its text: the
    /// whole-character collapse hides it, and the bUnit test compares the rendered markup
    /// with the engine's own answer, so both could be wrong together.
    /// </summary>
    [Fact]
    public void ATalentsLineNamesItsTalentsInTheRulebooksOrder()
    {
        var sheet = RulesFixture.StandardSheet();
        sheet.TalentSources["streetwise"] = "psychic";
        sheet.TalentSources["academics"]  = "psychic";

        var group = new SourceGrouping(_f.Rules).GroupBySource(sheet).Single();

        Assert.Equal("PSYCHIC POWERS", group.Heading);
        Assert.Equal<IEnumerable<string>>(["Talents (Academics, Streetwise)"], group.TraitLines);
    }

    /// <summary>
    /// All twelve Talents on one Source, but not all the Abilities, collapses the Talents
    /// half alone. No published sheet shows this shape — the three that mark Talents mark
    /// every Trait — so it is the branch most likely to be wrong and least likely to be seen.
    /// </summary>
    [Fact]
    public void AllTwelveTalentsAloneCollapseToAll()
    {
        var sheet = RulesFixture.StandardSheet();
        foreach (var talent in _f.Rules.Talents) sheet.TalentSources[talent.Id] = "magic";
        sheet.AbilitySources["willpower"] = "magic";

        var group = new SourceGrouping(_f.Rules).GroupBySource(sheet).Single();

        Assert.Equal<IEnumerable<string>>(
            ["Abilities (Willpower)", "Talents (All)"], group.TraitLines);
    }

    /// <summary>
    /// The whole-character collapse only fires when no Ability carries a Pro or Con, because
    /// "Abilities and Talents (All)" has nowhere to print one. Deleting that guard stayed
    /// green: the modifier test never set a Talent Source, so it could not reach this branch.
    /// </summary>
    [Fact]
    public void AModifierStopsTheWholeCharacterCollapse()
    {
        var sheet = RulesFixture.StandardSheet();
        foreach (var ability in _f.Rules.Abilities) sheet.AbilitySources[ability.Id] = "tech";
        foreach (var talent in _f.Rules.Talents)    sheet.TalentSources[talent.Id]   = "tech";

        // Without a modifier this is the collapsed single line.
        Assert.Equal<IEnumerable<string>>(
            ["Abilities and Talents (All)"],
            new SourceGrouping(_f.Rules).GroupBySource(sheet).Single().TraitLines);

        sheet.AbilityModifiers["might"] = [new SelectedProCon("item")];

        var lines = new SourceGrouping(_f.Rules).GroupBySource(sheet).Single().TraitLines;

        Assert.Equal<IEnumerable<string>>(
            [
                "Abilities (Agility, Intellect, Perception, Toughness, Willpower)",
                "Abilities (Might) (Item)",
                "Talents (All)"
            ],
            lines);
    }

    /// <summary>
    /// A Pro on an Ability is labelled by name like a Con. Only Cons are exercised anywhere
    /// else — both the published Stronghold and the Hero sample use Item — so dropping the
    /// Pro half of the lookup would print a raw id on the sheet and nothing would notice.
    /// </summary>
    [Fact]
    public void AProOnAnAbilityIsNamedNotPrintedAsAnId()
    {
        var pro = _f.Rules.Pros[0];

        var sheet = RulesFixture.StandardSheet();
        sheet.AbilitySources["might"]   = "tech";
        sheet.AbilityModifiers["might"] = [new SelectedProCon(pro.Id)];

        var lines = new SourceGrouping(_f.Rules).GroupBySource(sheet).Single().TraitLines;

        Assert.Equal<IEnumerable<string>>([$"Abilities (Might) ({pro.Name})"], lines);
        Assert.DoesNotContain(pro.Id, lines[0], StringComparison.Ordinal);
    }

    /// <summary>
    /// The JSON export carries the Trait Sources: the recorded value and the effective one
    /// per Trait, and the printed group structure. None of it was asserted anywhere — the
    /// whole <c>source_groups</c> array could be emptied and the suite stayed green.
    ///
    /// <para>Both per-Trait fields matter and they answer different questions.
    /// <c>source</c> is the input a rebuild needs and is null when nothing was recorded;
    /// <c>effective_source</c> is what the Trait actually is, default included.</para>
    /// </summary>
    [Fact]
    public void TheJsonExportCarriesTheTraitSources()
    {
        var sheet = RulesFixture.StandardSheet();
        sheet.AbilityRanks["might"]       = 8;
        sheet.TalentRanks["academics"]    = 4;
        sheet.AbilitySources["might"]     = "tech";
        sheet.SelectedPowers.Add(new SelectedPower("blast", 4) { SourceId = "tech" });

        var json = JsonDocument.Parse(CharacterSheetRenderer.RenderJson(
            sheet, _f.Rules, _f.Costs, _f.Derived, _f.Validator.Validate(sheet), DateTime.UnixEpoch));

        var might = json.RootElement.GetProperty("abilities")
            .EnumerateArray().Single(a => a.GetProperty("id").GetString() == "might");

        Assert.Equal("tech", might.GetProperty("source").GetString());
        Assert.Equal("tech", might.GetProperty("effective_source").GetString());

        // Untouched: nothing recorded, but it still *is* something.
        var agility = json.RootElement.GetProperty("abilities")
            .EnumerateArray().Single(a => a.GetProperty("id").GetString() == "agility");

        Assert.Equal(JsonValueKind.Null, agility.GetProperty("source").ValueKind);
        Assert.Equal("innate", agility.GetProperty("effective_source").GetString());

        // A Talent takes the other default.
        var academics = json.RootElement.GetProperty("talents")
            .EnumerateArray().Single(t => t.GetProperty("id").GetString() == "academics");

        Assert.Equal(JsonValueKind.Null, academics.GetProperty("source").ValueKind);
        Assert.Equal("trained", academics.GetProperty("effective_source").GetString());

        // A Talent carrying a Source but no rank is still exported, or the `source` field a
        // rebuild needs is missing for exactly the Talents this feature exists to record.
        // The rank filter alone dropped it while every sheet printed its line.
        sheet.TalentSources["survival"] = "magic";
        Assert.Equal(0, sheet.GetTalentRank("survival"));

        var reexported = JsonDocument.Parse(CharacterSheetRenderer.RenderJson(
            sheet, _f.Rules, _f.Costs, _f.Derived, _f.Validator.Validate(sheet), DateTime.UnixEpoch));

        var survival = reexported.RootElement.GetProperty("talents")
            .EnumerateArray().Single(t => t.GetProperty("id").GetString() == "survival");

        Assert.Equal("magic", survival.GetProperty("source").GetString());
        Assert.Equal("magic", survival.GetProperty("effective_source").GetString());

        // And the group structure, with the printed line inside it.
        var group = json.RootElement.GetProperty("source_groups")
            .EnumerateArray().Single(g => g.GetProperty("source").GetString() == "tech");

        Assert.Equal("TECH POWERS", group.GetProperty("heading").GetString());
        Assert.Equal<IEnumerable<string>>(
            ["Abilities (Might)"],
            group.GetProperty("trait_lines").EnumerateArray().Select(l => l.GetString()!));
        Assert.Equal<IEnumerable<string>>(
            ["blast"],
            group.GetProperty("power_ids").EnumerateArray().Select(p => p.GetString()!));
    }

    [Fact]
    public void GroupingNeverLosesOrDuplicatesAPower()
    {
        var sheet = RulesFixture.StandardSheet();
        sheet.SelectedPowers.Add(new SelectedPower("blast", 4) { SourceId = "tech" });
        sheet.SelectedPowers.Add(new SelectedPower("armor", 4) { SourceId = "tech" });
        sheet.SelectedPowers.Add(new SelectedPower("flight", 4) { SourceId = "magic" });
        sheet.SelectedPowers.Add(new SelectedPower("leaping", 4));

        var listed = new SourceGrouping(_f.Rules).GroupBySource(sheet)
            .SelectMany(g => g.Powers).Select(p => p.PowerId).ToList();

        Assert.Equal(sheet.SelectedPowers.Select(p => p.PowerId).Order(), listed.Order());
        Assert.Equal(listed.Count, listed.Distinct(StringComparer.Ordinal).Count());
    }
}
