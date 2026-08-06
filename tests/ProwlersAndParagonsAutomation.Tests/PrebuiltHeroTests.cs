using ProwlersAndParagonsAutomation.Engine;

namespace ProwlersAndParagonsAutomation.Tests;

/// <summary>
/// Rebuilds each of the twenty pre-built Heroes from Ch.8 on a CharacterSheet and checks
/// the engine produces the Edge, Health and Resolve the rulebook prints for them.
///
/// <para>This is the strongest check in the suite. The other tests confirm the engine
/// matches a rule as transcribed; these confirm it matches a finished character the
/// authors built and published, which catches misreadings of the rules themselves.</para>
/// </summary>
[Collection(SharedRules.Name)]
public sealed class PrebuiltHeroTests
{
    private readonly RulesFixture _f;

    public PrebuiltHeroTests(RulesFixture fixture) => _f = fixture;

    public static TheoryData<string> HeroNames()
    {
        var data = new TheoryData<string>();
        foreach (var h in PrebuiltHeroes.All) data.Add(h.Name);
        return data;
    }

    /// <summary>
    /// Turns a printed sheet into a CharacterSheet. Printed Power ranks are final ranks,
    /// so a baseline-rank Power's purchased ranks are the difference between the printed
    /// rank and the baseline its Traits provide.
    /// </summary>
    private CharacterSheet Build(PrebuiltHeroes.Hero hero)
    {
        var sheet = new CharacterSheet { SelectedTierId = "standard" };

        sheet.AbilityRanks["agility"]    = hero.Agility;
        sheet.AbilityRanks["intellect"]  = hero.Intellect;
        sheet.AbilityRanks["might"]      = hero.Might;
        sheet.AbilityRanks["perception"] = hero.Perception;
        sheet.AbilityRanks["toughness"]  = hero.Toughness;
        sheet.AbilityRanks["willpower"]  = hero.Willpower;

        var talents = PrebuiltHeroes.TalentsByHero[hero.Name];
        Assert.Equal(PrebuiltHeroes.TalentIds.Length, talents.Length);
        for (var i = 0; i < talents.Length; i++)
            sheet.TalentRanks[PrebuiltHeroes.TalentIds[i]] = talents[i];

        foreach (var perk in PrebuiltHeroes.PerksByHero[hero.Name])
        {
            Assert.NotNull(_f.Rules.GetPerk(perk.Id));
            sheet.Perks.Add(new SelectedPerk(perk.Id, perk.Units));
        }

        foreach (var p in hero.Powers)
        {
            var power = _f.Rules.GetPower(p.Id);
            Assert.NotNull(power);

            // Determination records how much Resolve was bought, not a rank.
            if (p.Id == "determination")
            {
                sheet.SelectedPowers.Add(new SelectedPower(p.Id, 0) { Units = hero.DeterminationResolve });
                continue;
            }

            if (power.MaxRank == 0)
            {
                sheet.SelectedPowers.Add(new SelectedPower(p.Id, 0)
                {
                    BaselineTraitId = p.BaselineTrait,
                    Units           = p.Units,
                    CostVariantKey  = p.CostVariant
                });
                continue;
            }

            var probe    = new SelectedPower(p.Id, 0) { BaselineTraitId = p.BaselineTrait };
            var baseline = _f.Derived.GetBaselineRank(power, sheet, probe);
            var purchased = Math.Max(0, p.EffectiveRank - baseline);

            sheet.SelectedPowers.Add(new SelectedPower(p.Id, purchased)
            {
                BaselineTraitId = p.BaselineTrait,
                Units           = p.Units,
                CostVariantKey  = p.CostVariant
            });
        }

        foreach (var flawId in hero.Flaws)
        {
            Assert.NotNull(_f.Rules.GetFlaw(flawId));
            sheet.Flaws.Add(new SelectedFlaw(flawId));
        }

        return sheet;
    }

    [Theory]
    [MemberData(nameof(HeroNames))]
    public void HeroEdgeMatchesTheRulebook(string name)
    {
        var hero  = PrebuiltHeroes.All.Single(h => h.Name == name);
        var sheet = Build(hero);

        Assert.Equal(hero.Edge, _f.Derived.CalculateEdge(sheet));
    }

    [Theory]
    [MemberData(nameof(HeroNames))]
    public void HeroHealthMatchesTheRulebook(string name)
    {
        var hero  = PrebuiltHeroes.All.Single(h => h.Name == name);
        var sheet = Build(hero);

        Assert.Equal(hero.Health, _f.Derived.CalculateHealth(sheet));
    }

    [Theory]
    [MemberData(nameof(HeroNames))]
    public void HeroResolveMatchesTheRulebook(string name)
    {
        var hero  = PrebuiltHeroes.All.Single(h => h.Name == name);
        var sheet = Build(hero);

        Assert.Equal(hero.Resolve, _f.Derived.CalculateResolve(sheet));
    }

    [Theory]
    [MemberData(nameof(HeroNames))]
    public void HeroPowerRanksNeverExceedTheTraitCap(string name)
    {
        var hero  = PrebuiltHeroes.All.Single(h => h.Name == name);
        var sheet = Build(hero);

        // Standard tier caps Traits at 12d, and the published Heroes respect it.
        foreach (var sp in sheet.SelectedPowers)
            Assert.True(_f.Derived.GetEffectiveRank(sp, sheet) <= 12,
                $"{hero.Name}: {sp.PowerId} exceeds the 12d Standard trait cap.");
    }

    [Theory]
    [MemberData(nameof(HeroNames))]
    public void HeroPrintedPowerRanksAreReproduced(string name)
    {
        var hero  = PrebuiltHeroes.All.Single(h => h.Name == name);
        var sheet = Build(hero);

        // A baseline that already exceeded the printed rank would silently inflate the
        // Power and skew Resolve, so check the reconstruction actually lands.
        foreach (var p in hero.Powers.Where(p => p.EffectiveRank > 0))
        {
            var selection = sheet.SelectedPowers.Single(sp => sp.PowerId == p.Id);
            Assert.Equal(p.EffectiveRank, _f.Derived.GetEffectiveRank(selection, sheet));
        }
    }

    /// <summary>
    /// Every pre-built Hero was built on 125 Hero Points, so rebuilding one should land
    /// near that. It cannot land exactly, because the transcription omits the Pros and
    /// Cons printed on the sheets: many are Power-specific ones this project has not
    /// extracted yet (Regeneration's Fast, Strike's Throw, Telepathy's Mind Link), and a
    /// few sheets also carry custom gear, which is priced by the Chapter 6 rules that are
    /// likewise not modelled. Omitted Pros push the reconstruction under budget; omitted
    /// Cons push it over.
    ///
    /// <para>An exact reconciliation is therefore not possible yet, and is tracked on the
    /// roadmap behind extracting those Power-specific Pros and Cons. What is asserted here
    /// is the part that is fully determined: abilities, talents and perks all cost a known
    /// amount with no modifiers in play.</para>
    /// </summary>
    [Theory]
    [MemberData(nameof(HeroNames))]
    public void HeroAbilityTalentAndPerkCostsAreExact(string name)
    {
        var hero  = PrebuiltHeroes.All.Single(h => h.Name == name);
        var sheet = Build(hero);

        var expectedAbilities = hero.Agility + hero.Intellect + hero.Might
                              + hero.Perception + hero.Toughness + hero.Willpower;
        var expectedTalents   = PrebuiltHeroes.TalentsByHero[hero.Name].Sum();

        Assert.Equal(expectedAbilities, _f.Costs.AbilityCost(sheet));
        Assert.Equal(expectedTalents, _f.Costs.TalentCost(sheet));

        // Perks are flat or per-unit with no modifiers, so these are exact too.
        var expectedPerks = PrebuiltHeroes.PerksByHero[hero.Name].Sum(p =>
        {
            var perk = _f.Rules.GetPerk(p.Id)!;
            return perk.CostType == "flat" ? perk.Cost ?? 0 : (perk.CostPerUnit ?? 0) * p.Units;
        });
        Assert.Equal(expectedPerks, _f.Costs.TotalPerksCost(sheet));
    }

    /// <summary>
    /// Every Power the published Heroes use must be present in powers.json and priceable.
    /// A Power the data is missing, or one whose cost cannot be resolved, throws here.
    /// </summary>
    [Theory]
    [MemberData(nameof(HeroNames))]
    public void EveryPowerAPublishedHeroUsesIsPriceable(string name)
    {
        var hero  = PrebuiltHeroes.All.Single(h => h.Name == name);
        var sheet = Build(hero);

        foreach (var sp in sheet.SelectedPowers)
        {
            var power = _f.Rules.GetPower(sp.PowerId);
            Assert.NotNull(power);

            var cost = _f.Costs.PowerCost(sp);
            if (sp.PowerId == "specialty") continue;   // the one 0 HP Power
            Assert.True(cost > 0, $"{hero.Name}: {sp.PowerId} priced at {cost} HP.");
        }

        // And the sheet as a whole must cost something sane for a 125 HP Standard Hero.
        var total = _f.Costs.TotalCost(sheet);
        Assert.InRange(total, 90, 160);
    }

    [Fact]
    public void AllTwentyHeroesAreTranscribed() =>
        Assert.Equal(20, PrebuiltHeroes.All.Count);

    [Fact]
    public void EveryHeroIsStandardTierAndSoCapsAt12d() =>
        Assert.Equal(12, _f.Rules.GetTier("standard")!.TraitCapRank);
}
