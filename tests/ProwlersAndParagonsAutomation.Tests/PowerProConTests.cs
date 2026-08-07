using System.Globalization;
using ProwlersAndParagonsAutomation.Engine;
using ProwlersAndParagonsAutomation.Engine.Models;

namespace ProwlersAndParagonsAutomation.Tests;

/// <summary>
/// The Pros and Cons printed inside individual Power entries: that the data matches the
/// rulebook, and that the engine applies them the right way round — flat ones change the
/// total, per-rank ones change the rate.
/// </summary>
[Collection(SharedRules.Name)]
public sealed class PowerProConTests
{
    private readonly RulesFixture _f;

    public PowerProConTests(RulesFixture fixture) => _f = fixture;

    public static TheoryData<string, string, string> AllEntries()
    {
        var data = new TheoryData<string, string, string>();
        foreach (var e in CanonicalPowerProsCons.All) data.Add(e.PowerId, e.Kind, e.Id);
        return data;
    }

    private static string CostSpec(PowerProConModel e)
    {
        static string Num(double d) => d.ToString("0.###", CultureInfo.InvariantCulture);

        return e.CostType switch
        {
            "flat"              => $"flat={e.CostModifier}",
            "per_rank"          => $"per_rank={Num(e.CostPerRank ?? 0)}",
            "per_unit"          => $"per_unit={e.CostPerUnit}/{e.CostUnitLabel}",
            "per_rank_per_unit" => $"per_rank_per_unit={Num(e.CostPerRank ?? 0)}/{e.CostUnitLabel}",
            "flat_variable"     => "flat_variable=" + string.Join(",",
                                       (e.CostModifierRange ?? new Dictionary<string, int>())
                                           .OrderBy(v => v.Value).Select(v => $"{v.Key}:{v.Value}")),
            "per_rank_variable" => "per_rank_variable=" + string.Join(",",
                                       (e.CostPerRankRange ?? new Dictionary<string, double>())
                                           .OrderBy(v => v.Value).Select(v => $"{v.Key}:{Num(v.Value)}")),
            _ => $"UNKNOWN({e.CostType})"
        };
    }

    private PowerProConModel Find(string powerId, string kind, string id)
    {
        var power = _f.Rules.GetPower(powerId);
        Assert.NotNull(power);

        var entry = (kind == "pro" ? power.PowerPros : power.PowerCons).FirstOrDefault(x => x.Id == id);
        Assert.True(entry is not null, $"Power '{powerId}' has no {kind} '{id}'.");
        return entry!;
    }

    // ── Against the rulebook ─────────────────────────────────────────────────

    [Fact]
    public void TheRulebookPrints105PowerSpecificProsAndCons()
    {
        // 102 inside Power entries in Ch.2, plus the three toxin ones in Ch.7 (p.108).
        var actual = _f.Rules.Powers.Sum(p => p.PowerPros.Count + p.PowerCons.Count);

        Assert.Equal(105, CanonicalPowerProsCons.All.Count);
        Assert.Equal(105, actual);
    }

    [Fact]
    public void SixtyTwoPowersCarryAtLeastOne() =>
        Assert.Equal(62, _f.Rules.Powers.Count(p => p.PowerPros.Count + p.PowerCons.Count > 0));

    /// <summary>
    /// The three toxin entries are the only Pros and Cons the rulebook prints outside
    /// Chapter 2, and each names the one Power it applies to. Non-Lethal Disease is Stun,
    /// not Slay, which is easy to get backwards given it sits beside Lethal Disease.
    /// </summary>
    [Theory]
    [InlineData("stun", "con", "caustic", -2)]
    [InlineData("slay", "pro", "lethal_disease", 6)]
    [InlineData("stun", "pro", "non_lethal_disease", 2)]
    public void TheToxinEntriesBelongToTheirNamedPower(string powerId, string kind, string id, int modifier)
    {
        Assert.Equal(modifier, Find(powerId, kind, id).CostModifier);

        // ...and to no other Power.
        var others = _f.Rules.Powers.Where(p => p.Id != powerId);
        Assert.All(others, p => Assert.DoesNotContain(
            p.PowerPros.Concat(p.PowerCons), x => x.Id == id));
    }

    [Theory]
    [MemberData(nameof(AllEntries))]
    public void EntryMatchesTheRulebookCost(string powerId, string kind, string id) =>
        Assert.Equal(CanonicalPowerProsCons.All.Single(
                         e => e.PowerId == powerId && e.Kind == kind && e.Id == id).CostSpec,
                     CostSpec(Find(powerId, kind, id)));

    [Theory]
    [MemberData(nameof(AllEntries))]
    public void EntryHasANameAndADescription(string powerId, string kind, string id)
    {
        var e = Find(powerId, kind, id);

        Assert.False(string.IsNullOrWhiteSpace(e.Name));
        Assert.False(string.IsNullOrWhiteSpace(e.Description));
        Assert.EndsWith(".", e.Description.TrimEnd(), StringComparison.Ordinal);
    }

    [Theory]
    [MemberData(nameof(AllEntries))]
    public void ProsCostAndConsSave(string powerId, string kind, string id)
    {
        var e = Find(powerId, kind, id);
        var values = new List<double>();

        if (e.CostModifier is { } m) values.Add(m);
        if (e.CostPerRank is { } r) values.Add(r);
        if (e.CostPerUnit is { } u) values.Add(u);
        if (e.CostModifierRange is not null) values.AddRange(e.CostModifierRange.Values.Select(v => (double)v));
        if (e.CostPerRankRange is not null) values.AddRange(e.CostPerRankRange.Values);

        Assert.NotEmpty(values);
        if (kind == "pro") Assert.All(values, v => Assert.True(v > 0, $"{powerId}/{id} is {v}"));
        else               Assert.All(values, v => Assert.True(v < 0, $"{powerId}/{id} is {v}"));
    }

    [Fact]
    public void EveryPowerRecordsThatItsProsAndConsWereChecked() =>
        Assert.All(_f.Rules.Powers, p =>
            Assert.True(p.IsVerified("pros_cons"), $"Power '{p.Id}' does not list pros_cons as verified."));

    // ── Applying them ────────────────────────────────────────────────────────

    [Fact]
    public void AFlatProAddsToTheTotal()
    {
        // Strike at Might 0, 8 purchased ranks, 1 HP/rank = 8, plus Sweep at +4.
        var plain  = _f.Costs.PowerCost(new SelectedPower("strike", 8));
        var swept  = _f.Costs.PowerCost(new SelectedPower("strike", 8, [new SelectedProCon("sweep")], []));

        Assert.Equal(8, plain);
        Assert.Equal(12, swept);
    }

    [Fact]
    public void AFlatConSubtractsFromTheTotal()
    {
        // Armor 6 purchased ranks at 1 HP/rank, with Activated at -1.
        var plain     = _f.Costs.PowerCost(new SelectedPower("armor", 6));
        var activated = _f.Costs.PowerCost(new SelectedPower("armor", 6, [], [new SelectedProCon("activated")]));

        Assert.Equal(6, plain);
        Assert.Equal(5, activated);
    }

    [Fact]
    public void APerRankProRaisesTheRateNotTheTotal()
    {
        // Constructs is 3 HP per rank; Devices adds 2 per rank, making 5.
        var plain   = _f.Costs.PowerCost(new SelectedPower("constructs", 6));
        var devices = _f.Costs.PowerCost(new SelectedPower("constructs", 6, [new SelectedProCon("devices")], []));

        Assert.Equal(18, plain);
        Assert.Equal(30, devices);          // 6 × 5, not 18 + 2
        Assert.NotEqual(plain + 2, devices);
    }

    [Fact]
    public void APerRankConLowersTheRate()
    {
        // Elemental Control is 3 HP per rank; Only Control drops it to 2.
        var plain = _f.Costs.PowerCost(new SelectedPower("elemental_control", 10));
        var only  = _f.Costs.PowerCost(new SelectedPower("elemental_control", 10,
                        [], [new SelectedProCon("only_control")]));

        Assert.Equal(30, plain);
        Assert.Equal(20, only);
    }

    [Theory]
    [InlineData("standard", -2)]
    [InlineData("narrow", -4)]
    public void AVariableConUsesTheChosenVariant(string variant, int expected)
    {
        // Animal Control at 1 HP/rank, 8 ranks, with Only X.
        var cost = _f.Costs.PowerCost(new SelectedPower("animal_control", 8,
                       [], [new SelectedProCon("only_x", variant)]));

        Assert.Equal(8 + expected, cost);
    }

    [Theory]
    [InlineData("narrow_selection", 1)]
    [InlineData("wide_variety", 2)]
    public void AVariablePerRankProUsesTheChosenRate(string variant, int extraPerRank)
    {
        // Summoning at Threat 4d is 2 HP per rank; Animals adds 1 or 2 more.
        var baseline = _f.Costs.PowerCost(new SelectedPower("summoning", 6) { Units = 4 });
        var withPro  = _f.Costs.PowerCost(new SelectedPower("summoning", 6,
                           [new SelectedProCon("animals", variant)], []) { Units = 4 });

        Assert.Equal(12, baseline);
        Assert.Equal(12 + 6 * extraPerRank, withPro);
    }

    [Fact]
    public void APerUnitConScalesWithThePowersOwnUnits()
    {
        // Alternate Form is 4 HP per power level; Independent Forms gives back 1 per level.
        var standard = new SelectedPower("alternate_form", 0) { Units = 3 };
        var withCon  = new SelectedPower("alternate_form", 0,
                           [], [new SelectedProCon("independent_forms")]) { Units = 3 };

        Assert.Equal(12, _f.Costs.PowerCost(standard));
        Assert.Equal(9, _f.Costs.PowerCost(withCon));
    }

    [Fact]
    public void AlsoXCostsPerRankForEachExtraSource()
    {
        // Dispel is 1 HP per 2 ranks; each extra Source adds another 1 per 2 ranks.
        var plain = _f.Costs.PowerCost(new SelectedPower("dispel", 8));
        var two   = _f.Costs.PowerCost(new SelectedPower("dispel", 8,
                        [new SelectedProCon("also_x") { Units = 1 }], []));
        var three = _f.Costs.PowerCost(new SelectedPower("dispel", 8,
                        [new SelectedProCon("also_x") { Units = 2 }], []));

        Assert.Equal(4, plain);    // 8 × 0.5
        Assert.Equal(8, two);      // 8 × 1.0
        Assert.Equal(12, three);   // 8 × 1.5
    }

    [Fact]
    public void APowerSpecificEntryWinsOverAGenericOneOfTheSameName()
    {
        // Both Strike and the generic list have nothing named "sweep", but Strike's
        // "weapons" Con is its own: resolving it must not fall through to pros.json.
        var power = _f.Rules.GetPower("strike")!;
        Assert.Contains(power.PowerCons, c => c.Id == "weapons");
        Assert.Null(_f.Rules.GetCon("weapons"));

        var cost = _f.Costs.PowerCost(new SelectedPower("strike", 8, [], [new SelectedProCon("weapons")]));
        Assert.Equal(7, cost);
    }

    [Fact]
    public void AVariableEntryWithoutAVariantThrows() =>
        Assert.Throws<InvalidOperationException>(() =>
            _f.Costs.PowerCost(new SelectedPower("animal_control", 8, [], [new SelectedProCon("only_x")])));

    [Fact]
    public void PowerSpecificIdsAreUniqueWithinTheirPower()
    {
        foreach (var p in _f.Rules.Powers)
        {
            var ids = p.PowerPros.Select(x => x.Id).Concat(p.PowerCons.Select(x => x.Id)).ToList();
            Assert.Equal(ids.Count, ids.Distinct(StringComparer.Ordinal).Count());
        }
    }
}
