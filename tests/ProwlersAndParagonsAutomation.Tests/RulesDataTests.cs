namespace ProwlersAndParagonsAutomation.Tests;

/// <summary>
/// The rest of the rules data, checked against Chapter 2: tiers, abilities, talents,
/// pros, cons, perks and flaws. Costs are transcribed from the book, so an edit that
/// contradicts it fails here.
/// </summary>
[Collection(SharedRules.Name)]
public sealed class RulesDataTests
{
    private readonly RulesFixture _f;

    public RulesDataTests(RulesFixture fixture) => _f = fixture;

    // ── Tiers (Power Levels table, p.17) ─────────────────────────────────────

    [Theory]
    [InlineData("street_level", 75, 8)]
    [InlineData("low_level", 100, 10)]
    [InlineData("standard", 125, 12)]
    [InlineData("high_level", 150, 16)]
    [InlineData("legendary", 175, 20)]
    [InlineData("iconic", 200, 24)]
    public void TierBudgetsAndCapsMatchThePowerLevelsTable(string id, int heroPoints, int traitCap)
    {
        var tier = _f.Rules.GetTier(id);

        Assert.NotNull(tier);
        Assert.Equal(heroPoints, tier.HeroPoints);
        Assert.Equal(traitCap, tier.TraitCapRank);
    }

    [Fact]
    public void ThereAreSixTiers() => Assert.Equal(6, _f.Rules.Tiers.Count);

    // ── Abilities and talents ────────────────────────────────────────────────

    [Fact]
    public void TheSixAbilitiesAreTheRulebookSix()
    {
        string[] expected = ["agility", "intellect", "might", "perception", "toughness", "willpower"];
        Assert.Equal(expected, _f.Rules.Abilities.Select(a => a.Id).Order(StringComparer.Ordinal));
    }

    [Fact]
    public void TheTwelveTalentsAreTheRulebookTwelve()
    {
        string[] expected =
        [
            "academics", "charm", "command", "covert", "investigation", "medicine",
            "professional", "science", "streetwise", "survival", "technology", "vehicles"
        ];
        Assert.Equal(expected, _f.Rules.Talents.Select(t => t.Id).Order(StringComparer.Ordinal));
    }

    [Fact]
    public void AbilitiesAndTalentsAllCostOneHeroPointPerRank()
    {
        Assert.All(_f.Rules.Abilities, a => Assert.Equal(1, a.CostPerRank));
        Assert.All(_f.Rules.Talents, t => Assert.Equal(1, t.CostPerRank));
    }

    [Fact]
    public void EveryTalentNamesAnAbilityThatExists() =>
        Assert.All(_f.Rules.Talents, t =>
        {
            Assert.False(string.IsNullOrWhiteSpace(t.LinkedAbility));
            Assert.NotNull(_f.Rules.GetAbility(t.LinkedAbility));
        });

    // ── Pros (pp.48-53) ──────────────────────────────────────────────────────

    [Theory]
    [InlineData("affect_inanimate", 1)]
    [InlineData("armor_piercing", 2)]
    [InlineData("carrier_attack", 6)]
    [InlineData("close", 2)]
    [InlineData("contagious", 4)]
    [InlineData("fuse", 2)]
    [InlineData("imbue", 4)]
    [InlineData("independent", 6)]
    [InlineData("ongoing", 6)]
    [InlineData("overload", 2)]
    [InlineData("penetrating", 4)]
    [InlineData("phase_shift", 4)]
    [InlineData("resisted", 6)]
    [InlineData("ricochet", 2)]
    [InlineData("selective", 4)]
    [InlineData("subtle", 2)]
    [InlineData("undetectable", 6)]
    [InlineData("trap", 2)]
    public void FixedCostProsMatchTheRulebook(string id, int expected)
    {
        var pro = _f.Rules.GetPro(id);

        Assert.NotNull(pro);
        Assert.Equal(expected, pro.CostModifier);
    }

    [Theory]
    // Area +2 / Burst +1 (p.48).
    [InlineData("area_burst", "area", 2)]
    [InlineData("area_burst", "burst", 1)]
    // Zone/Nova cost double when applied to a Touch Power (p.48).
    [InlineData("zone_nova", "zone_ranged", 2)]
    [InlineData("zone_nova", "nova_ranged", 1)]
    [InlineData("zone_nova", "zone_touch", 4)]
    [InlineData("zone_nova", "nova_touch", 2)]
    // Ranged: +2 from Close Range, +4 from Touch.
    [InlineData("ranged", "from_close_range", 2)]
    [InlineData("ranged", "from_touch", 4)]
    // Expansive: 6 Close, 12 Distant, 18 Extreme.
    [InlineData("expansive", "close_range", 6)]
    [InlineData("expansive", "distant_range", 12)]
    [InlineData("expansive", "extreme_range", 18)]
    // Line of Sight: +2 from Distant, +4 from Close, +6 from Touch.
    [InlineData("line_of_sight", "from_distant_range", 2)]
    [InlineData("line_of_sight", "from_close_range", 4)]
    [InlineData("line_of_sight", "from_touch", 6)]
    public void VariableCostProsMatchTheRulebook(string id, string variant, int expected)
    {
        var pro = _f.Rules.GetPro(id);

        Assert.NotNull(pro);
        Assert.NotNull(pro.CostModifierRange);
        Assert.True(pro.CostModifierRange.TryGetValue(variant, out var actual),
            $"Pro '{id}' has no variant '{variant}'. Has: {string.Join(", ", pro.CostModifierRange.Keys)}");
        Assert.Equal(expected, actual);
    }

    [Fact]
    public void ProCostsAreAllPositive() =>
        Assert.All(_f.Rules.Pros, p =>
        {
            if (p.CostModifier is { } flat) Assert.True(flat > 0, $"Pro '{p.Id}' has a non-positive cost.");
            if (p.CostModifierRange is not null)
                Assert.All(p.CostModifierRange, kv =>
                    Assert.True(kv.Value > 0, $"Pro '{p.Id}' variant '{kv.Key}' is not positive."));
        });

    // ── Cons (pp.48-53) ──────────────────────────────────────────────────────

    [Theory]
    [InlineData("build_up", -2)]
    [InlineData("burnout", -4)]
    [InlineData("close", -2)]
    [InlineData("concentration", -4)]
    [InlineData("constant", -2)]
    [InlineData("costly", -4)]
    [InlineData("degrades", -2)]
    [InlineData("delay", -4)]
    [InlineData("exclusive", -1)]
    [InlineData("item", -1)]
    [InlineData("readied", -2)]
    [InlineData("recharge", -4)]
    [InlineData("signature", -1)]
    [InlineData("sustained", -2)]
    [InlineData("toxin", -2)]
    [InlineData("two_handed", -1)]
    [InlineData("uncontrolled", -2)]
    [InlineData("unreliable", -2)]
    public void FixedCostConsMatchTheRulebook(string id, int expected)
    {
        var con = _f.Rules.GetCon(id);

        Assert.NotNull(con);
        Assert.Equal(expected, con.CostModifier);
    }

    [Theory]
    // Charges: -1 for 6 uses per scene, -2 for 3, -4 for 1.
    [InlineData("charges", "6_per_scene", -1)]
    [InlineData("charges", "3_per_scene", -2)]
    [InlineData("charges", "1_per_scene", -4)]
    // Touch: -2 from Close Range, -4 from Distant Range.
    [InlineData("touch", "from_close_range", -2)]
    [InlineData("touch", "from_distant_range", -4)]
    // Only Inanimate: -1 any Source, -2 two Sources, -4 one Source.
    [InlineData("only_inanimate", "any_inanimate_source", -1)]
    [InlineData("only_inanimate", "two_inanimate_sources", -2)]
    [InlineData("only_inanimate", "one_inanimate_source", -4)]
    // Harmful: -1 for 1 damage per use, -2 for 1d3, -4 for 1d6.
    [InlineData("harmful", "1_damage_per_use", -1)]
    [InlineData("harmful", "1d3_damage_per_use", -2)]
    [InlineData("harmful", "1d6_damage_per_use", -4)]
    public void VariableCostConsMatchTheRulebook(string id, string variant, int expected)
    {
        var con = _f.Rules.GetCon(id);

        Assert.NotNull(con);
        Assert.NotNull(con.CostModifierRange);
        Assert.True(con.CostModifierRange.TryGetValue(variant, out var actual),
            $"Con '{id}' has no variant '{variant}'. Has: {string.Join(", ", con.CostModifierRange.Keys)}");
        Assert.Equal(expected, actual);
    }

    [Theory]
    [InlineData("conditional")]
    [InlineData("limited")]
    [InlineData("shutdown")]
    [InlineData("side_effect")]
    public void GradedConsRunFromMinusOneToMinusFour(string id)
    {
        var con = _f.Rules.GetCon(id);

        Assert.NotNull(con);
        Assert.NotNull(con.CostModifierRange);
        Assert.Equal([-4, -2, -1], con.CostModifierRange.Values.Order());
    }

    [Fact]
    public void ConCostsAreAllNegative() =>
        Assert.All(_f.Rules.Cons, c =>
        {
            if (c.CostModifier is { } flat) Assert.True(flat < 0, $"Con '{c.Id}' has a non-negative cost.");
            if (c.CostModifierRange is not null)
                Assert.All(c.CostModifierRange, kv =>
                    Assert.True(kv.Value < 0, $"Con '{c.Id}' variant '{kv.Key}' is not negative."));
        });

    [Theory]
    [InlineData("overkill")]
    [InlineData("weak")]
    public void RateReducingConsCarryNoFlatModifier(string id)
    {
        // Overkill and Weak reduce the per-rank rate, so a flat modifier here would
        // double-count. CostCalculator handles them.
        var con = _f.Rules.GetCon(id);

        Assert.NotNull(con);
        Assert.Null(con.CostModifier);
        Assert.True(con.CostModifierRange is null or { Count: 0 });
    }

    [Fact]
    public void ThereAre23ProsAnd28Cons()
    {
        Assert.Equal(23, _f.Rules.Pros.Count);
        Assert.Equal(28, _f.Rules.Cons.Count);
    }

    // ── Perks (pp.54-55) ─────────────────────────────────────────────────────

    [Theory]
    [InlineData("authority", 3)]
    [InlineData("fame", 3)]
    [InlineData("fourth_wall", 6)]
    [InlineData("infamy", 3)]
    [InlineData("patron", 3)]
    [InlineData("reputation", 3)]
    [InlineData("resources", 1)]
    [InlineData("wealth", 3)]
    [InlineData("great_wealth", 6)]
    public void FlatPerksMatchTheRulebook(string id, int expected)
    {
        var perk = _f.Rules.GetPerk(id);

        Assert.NotNull(perk);
        Assert.Equal("flat", perk.CostType);
        Assert.Equal(expected, perk.Cost);
    }

    [Theory]
    // Each of these is 1 Hero Point per unit; the unit differs per Perk.
    [InlineData("contacts")]
    [InlineData("headquarters")]
    [InlineData("pet_sidekick")]
    [InlineData("unique_vehicle")]
    public void PerUnitPerksCostOneHeroPointPerUnit(string id)
    {
        var perk = _f.Rules.GetPerk(id);

        Assert.NotNull(perk);
        Assert.Equal("per_unit", perk.CostType);
        Assert.Equal(1, perk.CostPerUnit);
    }

    [Fact]
    public void ThereAre13Perks() => Assert.Equal(13, _f.Rules.Perks.Count);

    // ── Flaws (pp.55-59) ─────────────────────────────────────────────────────

    [Fact]
    public void ThereAre53Flaws() =>
        // 48 names in the rulebook, five of which pair two Flaws under one heading
        // (Compulsion/Severe, Heavy/Very Heavy, Reaction/Severe, Requirement/Severe,
        // Unlucky/Jinx) and are stored separately.
        Assert.Equal(53, _f.Rules.Flaws.Count);

    [Theory]
    // The four the rulebook names as examples when defining the two exceptions (p.55).
    [InlineData("enemy", "plot_hook")]
    [InlineData("relationship", "plot_hook")]
    [InlineData("blind_deaf", "condition")]
    [InlineData("vulnerability", "condition")]
    public void TheNamedPlotHooksAndConditionsAreClassifiedThatWay(string id, string expected)
    {
        var flaw = _f.Rules.GetFlaw(id);

        Assert.NotNull(flaw);
        Assert.Equal(expected, flaw.FlawType);
    }

    [Fact]
    public void EveryFlawTypeIsOneTheResolveCalculationUnderstands()
    {
        string[] known = ["regular", "condition", "plot_hook", "plot_hook_and_condition"];
        Assert.All(_f.Rules.Flaws, fl => Assert.Contains(fl.FlawType, known));
    }

    [Fact]
    public void FlawCreationLimitsMatchTheRulebook()
    {
        // "all Heroes can have up to 3 Flaws and must have at least one" (p.55).
        var rules = _f.Rules.CreationRules.FlawRules;

        Assert.Equal(1, rules.MinAtCreation);
        Assert.Equal(3, rules.MaxAtCreation);
    }

    // ── Nothing is left flagged ──────────────────────────────────────────────

    [Fact]
    public void NoRulesEntryIsStillFlaggedForReview()
    {
        var flagged = new List<string>();
        flagged.AddRange(_f.Rules.Pros.Where(x => x.NeedsReview).Select(x => $"pro:{x.Id}"));
        flagged.AddRange(_f.Rules.Cons.Where(x => x.NeedsReview).Select(x => $"con:{x.Id}"));
        flagged.AddRange(_f.Rules.Perks.Where(x => x.NeedsReview).Select(x => $"perk:{x.Id}"));
        flagged.AddRange(_f.Rules.Flaws.Where(x => x.NeedsReview).Select(x => $"flaw:{x.Id}"));
        flagged.AddRange(_f.Rules.Tiers.Where(x => x.NeedsReview).Select(x => $"tier:{x.Id}"));

        Assert.Empty(flagged);
    }
}
