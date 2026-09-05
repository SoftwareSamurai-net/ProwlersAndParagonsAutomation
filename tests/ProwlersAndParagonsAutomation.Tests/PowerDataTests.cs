using System.Globalization;
using ProwlersAndParagonsAutomation.Engine;
using ProwlersAndParagonsAutomation.Engine.Models;
using ProwlersAndParagonsAutomation.Sheets;

namespace ProwlersAndParagonsAutomation.Tests;

/// <summary>
/// Checks powers.json against <see cref="CanonicalPowers"/> — the Range, Rank and Cost
/// transcribed from Chapter 2 — and against the schema invariants those fields imply.
///
/// <para>The history this guards: every entry once carried a uniform 1 HP per rank,
/// which was wrong for 91 of them, and the single needs_review flag failed to show it.</para>
/// </summary>
[Collection(SharedRules.Name)]
public sealed class PowerDataTests
{
    private readonly RulesFixture _f;

    public PowerDataTests(RulesFixture fixture) => _f = fixture;

    private static readonly string[] BaselineRelationships =
    [
        "baseline_equal", "baseline_half", "baseline_fixed",
        "baseline_greater_of", "baseline_selected_trait"
    ];

    private static readonly string[] Ranges =
        ["self", "touch", "ranged", "zone", "special"];

    private static readonly string[] VerifiableFields =
        ["range", "rank_type", "cost", "prerequisite", "description", "pros_cons"];

    public static TheoryData<string> AllPowerIds()
    {
        var data = new TheoryData<string>();
        foreach (var e in CanonicalPowers.All) data.Add(e.Id);
        return data;
    }

    /// <summary>
    /// Rebuilds a power's cost into the same compact form <see cref="CanonicalPowers"/>
    /// records, so a mismatch names the exact discrepancy.
    /// </summary>
    private static string CostSpec(PowerModel p)
    {
        static string Num(double d) => d.ToString("0.###", CultureInfo.InvariantCulture);

        static string Variants(PowerModel p) => string.Join(",",
            (p.CostVariants ?? new Dictionary<string, double>())
                .OrderByDescending(v => v.Value)
                .ThenBy(v => v.Key, StringComparer.Ordinal)
                .Select(v => $"{v.Key}:{Num(v.Value)}"));

        return p.CostType switch
        {
            "per_rank"          => $"per_rank={Num(p.CostPerRank ?? -1)}",
            "flat"              => $"flat={p.CostFlat?.ToString(CultureInfo.InvariantCulture)}",
            "per_unit"          => $"per_unit={p.CostPerUnit?.ToString(CultureInfo.InvariantCulture)}/{p.CostUnitLabel}",
            "per_rank_variable" => $"per_rank_variable={Variants(p)}",
            "flat_variable"     => "flat_variable=" + string.Join(",",
                                       (p.CostVariants ?? new Dictionary<string, double>())
                                           .OrderBy(v => v.Value)
                                           .Select(v => $"{v.Key}:{Num(v.Value)}")),
            "special"           => "special",
            _                   => $"UNKNOWN({p.CostType})"
        };
    }

    // ── Against the rulebook ─────────────────────────────────────────────────

    [Fact]
    public void EveryRulebookPowerIsPresentAndNothingExtraIs()
    {
        var expected = CanonicalPowers.All.Select(e => e.Id).OrderBy(x => x, StringComparer.Ordinal);
        var actual   = _f.Rules.Powers.Select(p => p.Id).OrderBy(x => x, StringComparer.Ordinal);

        Assert.Equal(expected, actual);
    }

    [Fact]
    public void ThePowerCountMatchesTheRulebook() =>
        Assert.Equal(141, _f.Rules.Powers.Count);

    [Theory]
    [MemberData(nameof(AllPowerIds))]
    public void PowerMatchesItsRulebookStatLine(string id)
    {
        var expected = CanonicalPowers.All.Single(e => e.Id == id);
        var power    = _f.Rules.GetPower(id);

        Assert.NotNull(power);
        Assert.Equal(expected.Range, power.Range);
        Assert.Equal(expected.RankType, power.RankType);
        Assert.Equal(expected.CostSpec, CostSpec(power));
        Assert.Equal($"Ultimate Edition, Ch.2 Powers, p.{expected.Page}", power.SourceRef);
    }

    [Fact]
    public void NoPowerHasAUniformPlaceholderCost()
    {
        // Regression: the entire file was once per_rank / 1.
        var perRankOne = _f.Rules.Powers
            .Count(p => p is { CostType: "per_rank", CostPerRank: 1 });

        Assert.Equal(34, perRankOne);
        Assert.NotEqual(_f.Rules.Powers.Count, perRankOne);
    }

    [Fact]
    public void CostRatesAreOnlyTheFourTheRulebookUses()
    {
        var rates = _f.Rules.Powers
            .Where(p => p.CostType == "per_rank")
            .Select(p => p.CostPerRank)
            .Distinct()
            .Order();

        Assert.Equal([0.5, 1.0, 2.0, 3.0], rates);
    }

    // ── Schema invariants ────────────────────────────────────────────────────

    [Fact]
    public void PowerIdsAreUnique()
    {
        var duplicates = _f.Rules.Powers
            .GroupBy(p => p.Id, StringComparer.Ordinal)
            .Where(g => g.Count() > 1)
            .Select(g => g.Key);

        Assert.Empty(duplicates);
    }

    [Theory]
    [MemberData(nameof(AllPowerIds))]
    public void PowerHasTheCostFieldsItsCostTypeRequires(string id)
    {
        var p = _f.Rules.GetPower(id)!;

        switch (p.CostType)
        {
            case "per_rank":
                Assert.NotNull(p.CostPerRank);
                Assert.Null(p.CostFlat);
                Assert.Null(p.CostPerUnit);
                break;
            case "flat":
                Assert.NotNull(p.CostFlat);
                Assert.Null(p.CostPerRank);
                break;
            case "per_unit":
                Assert.NotNull(p.CostPerUnit);
                Assert.False(string.IsNullOrWhiteSpace(p.CostUnitLabel));
                break;
            case "per_rank_variable":
            case "flat_variable":
                Assert.NotNull(p.CostVariants);
                Assert.NotEmpty(p.CostVariants);
                break;
            case "special":
                break;
            default:
                Assert.Fail($"Power '{id}' has unknown cost_type '{p.CostType}'.");
                break;
        }
    }

    [Theory]
    [MemberData(nameof(AllPowerIds))]
    public void RanksArePurchasableExactlyWhenTheCostIsPerRank(string id)
    {
        var p = _f.Rules.GetPower(id)!;

        var rankless   = p.RankType is "default" or "special";
        var boughtFlat = p.CostType is "flat" or "flat_variable" or "per_unit";

        if (rankless || boughtFlat)
            Assert.Equal(0, p.MaxRank);
        else
            Assert.Null(p.MaxRank);
    }

    [Theory]
    [MemberData(nameof(AllPowerIds))]
    public void BaselineRankPowersHaveAPrerequisiteAndNoOthersDo(string id)
    {
        var p = _f.Rules.GetPower(id)!;

        if (p.RankType == "baseline")
            Assert.NotNull(p.Prerequisite);
        else
            Assert.Null(p.Prerequisite);
    }

    [Fact]
    public void AllTwentySevenBaselinePowersAreAccountedFor() =>
        Assert.Equal(27, _f.Rules.Powers.Count(p => p.RankType == "baseline"));

    [Theory]
    [MemberData(nameof(AllPowerIds))]
    public void PrerequisiteRelationshipIsOneTheCalculatorHandles(string id)
    {
        var q = _f.Rules.GetPower(id)!.Prerequisite;
        if (q is null) return;

        Assert.Contains(q.Relationship, BaselineRelationships);

        switch (q.Relationship)
        {
            case "baseline_equal":
            case "baseline_half":
                // Must name a real ability, since the baseline is read from it.
                Assert.NotNull(q.Ability);
                Assert.NotNull(_f.Rules.GetAbility(q.Ability));
                break;
            case "baseline_fixed":
                Assert.NotNull(q.FixedValue);
                break;
            case "baseline_greater_of":
                Assert.NotNull(q.Ability);
                Assert.NotEmpty(q.Powers);
                Assert.All(q.Powers, pid => Assert.NotNull(_f.Rules.GetPower(pid)));
                break;
            case "baseline_selected_trait":
                // The Trait is chosen at purchase, so nothing is fixed in the data.
                Assert.Null(q.Ability);
                break;
        }
    }

    [Theory]
    [MemberData(nameof(AllPowerIds))]
    public void RangeIsOneOfTheFiveTheRulebookDefines(string id) =>
        Assert.Contains(_f.Rules.GetPower(id)!.Range, Ranges);

    /// <summary>
    /// Powers used to carry hand-written <c>available_pros</c> / <c>available_cons</c>
    /// lists, and this test checked the ids in them resolved. The lists are gone —
    /// applicability is derived from each generic option's own entry — so what is worth
    /// asserting now is that no Power is left with nothing to choose from. Under the old
    /// lists 68 of the 141 offered no generic Pro at all.
    /// </summary>
    [Theory]
    [MemberData(nameof(AllPowerIds))]
    public void EveryPowerIsOfferedGenericProsAndCons(string id)
    {
        var p = _f.Rules.GetPower(id)!;
        var applicability = new ProConApplicability(_f.Rules);

        Assert.NotEmpty(applicability.ProsFor(p));
        Assert.NotEmpty(applicability.ConsFor(p));
    }

    [Theory]
    [MemberData(nameof(AllPowerIds))]
    public void EveryPowerHasANameCategoryAndDescription(string id)
    {
        var p = _f.Rules.GetPower(id)!;

        Assert.False(string.IsNullOrWhiteSpace(p.Name));
        Assert.False(string.IsNullOrWhiteSpace(p.Category));
        Assert.False(string.IsNullOrWhiteSpace(p.Description));
    }

    /// <summary>
    /// <b>The one nomination-dependent Resolve rule in the book, and the set it is read as.</b>
    /// Ch.5 p.83 exempts "Expertise (except for combat skills)", so <c>affects_resolve: false</c> is
    /// the Expertise entry's default answer and <c>affects_resolve_when_nominated</c> names the
    /// nominations that overturn it. <see cref="ExpertiseCombatSkillNominations"/> carries the
    /// reading and where each half of it comes from.
    ///
    /// <para><b>Both edges are asserted, because widening is the likelier error and it is silent.</b>
    /// A set that had grown a Talent, or an Ability the book never puts in a fight, would quietly
    /// stop exempting Expertises the page exempts — Resolve is measured from the gap, so the sheet
    /// simply comes out lower and nothing looks wrong. So: the ids are exactly the reading, every
    /// one of them is a real Ability, none is a Talent, and no other Power in the file carries the
    /// field at all.</para>
    /// </summary>
    [Fact]
    public void OnlyExpertiseCarriesTheCombatSkillCarveOutAndItNamesTheCombatAbilities()
    {
        var expertise = _f.Rules.GetPower("expertise");

        Assert.NotNull(expertise);
        Assert.Equal(ExpertiseCombatSkillNominations, expertise.AffectsResolveWhenNominated);

        // The default the carve-out is an exception to. Flipping this to true would exempt nothing
        // and is the opposite error — see PlayRulesDataTests for the arithmetic that catches it.
        Assert.False(expertise.AffectsResolve);

        var abilityIds = _f.Rules.Abilities.Select(a => a.Id).ToHashSet(StringComparer.Ordinal);
        var talentIds  = _f.Rules.Talents.Select(t => t.Id).ToHashSet(StringComparer.Ordinal);

        // Positive control on the fixture: an empty rules set would satisfy the subset check below
        // and the disjointness check too.
        Assert.Equal(6, abilityIds.Count);
        Assert.Equal(12, talentIds.Count);

        Assert.All(expertise.AffectsResolveWhenNominated, id => Assert.Contains(id, abilityIds));
        Assert.All(expertise.AffectsResolveWhenNominated, id => Assert.DoesNotContain(id, talentIds));

        var others = _f.Rules.Powers
            .Where(p => p.Id != "expertise" && p.AffectsResolveWhenNominated.Count > 0)
            .Select(p => p.Id);

        Assert.Empty(others);
    }

    /// <summary>
    /// <b>This repository's reading of "combat skills", kept here rather than in
    /// <see cref="CanonicalResolveRules"/> — that file is the rulebook and a reading must never be
    /// in it.</b> The phrase appears exactly once in the whole extracted corpus, on p.83, and the
    /// book never says what a combat skill is. Three printed sentences narrow it, and
    /// <see cref="RulebookCorpusTests.ThePagesBehindTheCombatSkillReadingSayWhatItRestsOn"/> holds
    /// each of them to the page it is claimed from.
    ///
    /// <para><b>No Talent is a combat Talent</b> — p.18 names all twelve (Academics, Charm, Command,
    /// Covert, Investigation, Medicine, Professional, Science, Streetwise, Survival, Technology,
    /// Vehicles) and not one of them attacks or defends.</para>
    ///
    /// <para><b>Two Abilities are.</b> p.17, Might: "It is used to perform armed and unarmed close
    /// combat attacks". p.17, Agility: "Agility also applies when firing mundane ranged weapons and
    /// defending against attacks." Toughness and Willpower are p.75's <em>passive</em> defences —
    /// resisting is not a skill exercised — and Intellect and Perception are neither.</para>
    ///
    /// <para><b>And an Ability is the granularity a nomination has.</b> Ch.2 p.28: "Your
    /// specialization must fall under one of your Abilities or Talents … For example, you could have
    /// Expertise (Agility: Firearms)". Firearms is the book's own combat specialisation, so a set
    /// without Agility would exclude the printed example.</para>
    ///
    /// <para>What that granularity costs is recorded as an <c>ambiguity</c> on
    /// <c>resolve_exceptions</c>: the specialisation itself is free text, so Expertise (Agility:
    /// Acrobatics) counts here where a GM probably would not count it. p.83 gives the GM the last
    /// word in every case.</para>
    /// </summary>
    private static readonly string[] ExpertiseCombatSkillNominations = ["might", "agility"];

    // ── Verification bookkeeping ─────────────────────────────────────────────

    [Fact]
    public void EveryPowerIsMechanicallyVerifiedAgainstTheRulebook()
    {
        var unverified = _f.Rules.Powers
            .Where(p => !p.MechanicsVerified)
            .Select(p => $"{p.Id} (verified: {string.Join('/', p.VerifiedFields)})");

        Assert.Empty(unverified);
    }

    [Theory]
    [MemberData(nameof(AllPowerIds))]
    public void VerifiedFieldsOnlyNamesKnownFields(string id)
    {
        Assert.All(_f.Rules.GetPower(id)!.VerifiedFields, f => Assert.Contains(f, VerifiableFields));
    }

    [Fact]
    public void CostTypeDistributionMatchesTheRulebook()
    {
        var actual = _f.Rules.Powers
            .GroupBy(p => p.CostType, StringComparer.Ordinal)
            .ToDictionary(g => g.Key, g => g.Count(), StringComparer.Ordinal);

        Assert.Equal(71, actual["per_rank"]);
        Assert.Equal(62, actual["flat"]);
        Assert.Equal(3,  actual["per_unit"]);
        Assert.Equal(2,  actual["per_rank_variable"]);
        Assert.Equal(1,  actual["flat_variable"]);
        Assert.Equal(2,  actual["special"]);
    }

    [Fact]
    public void RankTypeDistributionMatchesTheRulebook()
    {
        var actual = _f.Rules.Powers
            .GroupBy(p => p.RankType, StringComparer.Ordinal)
            .ToDictionary(g => g.Key, g => g.Count(), StringComparer.Ordinal);

        Assert.Equal(63, actual["power"]);
        Assert.Equal(46, actual["default"]);
        Assert.Equal(27, actual["baseline"]);
        Assert.Equal(5,  actual["special"]);
    }

    [Fact]
    public void EverySpecialCostPowerHasAHandlerRatherThanThrowing()
    {
        // CostCalculator resolves special rates by power id; an unhandled one throws.
        foreach (var p in _f.Rules.Powers.Where(p => p.CostType == "special"))
        {
            var selection = p.Id switch
            {
                "boost"     => new SelectedPower(p.Id, 2) { BaselineTraitId = "might" },
                "summoning" => new SelectedPower(p.Id, 2) { Units = 4 },
                _           => new SelectedPower(p.Id, 2)
            };

            Assert.True(_f.Costs.PowerCost(selection) > 0,
                $"Power '{p.Id}' has cost_type 'special' with no CostCalculator handler.");
        }
    }

    private static string Keyed(SelectedProCon c) =>
        c.VariantKey is null ? c.Id : $"{c.Id}:{c.VariantKey}";

    /// <summary>
    /// <b>A repeated option prints once with its count.</b> Three options in the rulebook are
    /// bought again rather than repeated by mistake, and Blastwave's Energy Absorption carries
    /// five copies of Also X — which printed as "also_x, also_x, also_x, also_x, also_x" on
    /// the sheet, in the text export and in the wizard's review table.
    ///
    /// <para>The collapse itself was covered by nothing: reverting all three call sites to a
    /// plain join left the whole suite green, because every test around it asserts on
    /// characters that carry each option once.</para>
    /// </summary>
    [Fact]
    public void ARepeatedOptionPrintsOnceWithItsCount()
    {
        Assert.Equal("also_x ×5", PowerFormatter.ModifierLine(
            Enumerable.Repeat(new SelectedProCon("also_x"), 5), Keyed));

        // First-seen order, so a repeat does not reshuffle the line.
        Assert.Equal("item, also_x ×2, jamming", PowerFormatter.ModifierLine(
            [new("item"), new("also_x"), new("jamming"), new("also_x")], Keyed));

        // One copy stays a bare name: the count is only worth printing when there is one.
        Assert.Equal("item", PowerFormatter.ModifierLine([new SelectedProCon("item")], Keyed));

        Assert.Equal("", PowerFormatter.ModifierLine([], Keyed));
    }

    /// <summary>
    /// Two grades of one option are two different things, and must not collapse into each
    /// other. Charges at 3 per scene and at 1 per scene are priced differently, so a line
    /// reading "charges ×2" would hide which was taken — and the count would be a lie about a
    /// figure the reader is checking the character against.
    /// </summary>
    [Fact]
    public void TwoGradesOfOneOptionAreNotCollapsedTogether()
    {
        var line = PowerFormatter.ModifierLine(
            [new("charges", "3_per_scene"), new("charges", "1_per_scene"), new("charges", "3_per_scene")],
            Keyed);

        Assert.Equal("charges:3_per_scene ×2, charges:1_per_scene", line);
    }
}
