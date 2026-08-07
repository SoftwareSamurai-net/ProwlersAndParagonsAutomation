using System.Globalization;
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
    /// Parses the Pros and Cons a sheet prints for one Power, in the
    /// <c>kind:id[:variant][#units]</c> form <see cref="PrebuiltHeroes.ProsConsByHero"/> uses.
    /// </summary>
    private static (List<SelectedProCon> Pros, List<SelectedProCon> Cons) ProsCons(
        PrebuiltHeroes.Hero hero, string powerId)
    {
        var pros = new List<SelectedProCon>();
        var cons = new List<SelectedProCon>();

        if (!PrebuiltHeroes.ProsConsByHero.TryGetValue($"{hero.Name}|{powerId}", out var entries))
            return (pros, cons);

        foreach (var raw in entries)
        {
            var text  = raw;
            int? units = null;

            var hash = text.IndexOf('#', StringComparison.Ordinal);
            if (hash >= 0)
            {
                units = int.Parse(text[(hash + 1)..], CultureInfo.InvariantCulture);
                text  = text[..hash];
            }

            var parts = text.Split(':');
            Assert.True(parts.Length is 2 or 3, $"Malformed pro/con '{raw}' on {hero.Name}/{powerId}.");

            var choice = new SelectedProCon(parts[1], parts.Length == 3 ? parts[2] : null) { Units = units };
            (parts[0] == "pro" ? pros : cons).Add(choice);
        }

        return (pros, cons);
    }

    /// <summary>
    /// Turns a printed sheet into a CharacterSheet. Printed Power ranks are final ranks,
    /// so a baseline-rank Power's purchased ranks are the difference between the printed
    /// rank and the baseline its Traits provide.
    /// </summary>
    private CharacterSheet Build(PrebuiltHeroes.Hero hero)
    {
        var sheet = new CharacterSheet
        {
            SelectedTierId    = "standard",
            SelectedPackageId = PrebuiltHeroes.BuildByHero[hero.Name].Package
        };

        sheet.AbilityRanks["agility"]    = hero.Agility;
        sheet.AbilityRanks["intellect"]  = hero.Intellect;
        sheet.AbilityRanks["might"]      = hero.Might;
        sheet.AbilityRanks["perception"] = hero.Perception;
        sheet.AbilityRanks["toughness"]  = hero.Toughness;
        sheet.AbilityRanks["willpower"]  = hero.Willpower;

        foreach (var abilityId in sheet.AbilityRanks.Keys.ToList())
        {
            if (!PrebuiltHeroes.AbilityModifiersByHero.TryGetValue($"{hero.Name}|{abilityId}", out var mods))
                continue;

            sheet.AbilityModifiers[abilityId] =
                mods.Select(m => new SelectedProCon(m.Split(':')[1])).ToList();
        }

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

            var (pros, cons) = ProsCons(hero, p.Id);

            // Determination records how much Resolve was bought, not a rank.
            if (p.Id == "determination")
            {
                sheet.SelectedPowers.Add(new SelectedPower(p.Id, 0) { Units = hero.DeterminationResolve });
                continue;
            }

            if (power.MaxRank == 0)
            {
                sheet.SelectedPowers.Add(new SelectedPower(p.Id, 0, pros, cons)
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

            sheet.SelectedPowers.Add(new SelectedPower(p.Id, purchased, pros, cons)
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

        // A package already pays for every Trait up to its own rank, so only what was
        // bought above that is charged again.
        var package = _f.Rules.CreationRules.OptionalPackages
            .Single(p => p.Id == PrebuiltHeroes.BuildByHero[hero.Name].Package);

        (string Id, int Rank)[] abilities =
        [
            ("agility", hero.Agility), ("intellect", hero.Intellect), ("might", hero.Might),
            ("perception", hero.Perception), ("toughness", hero.Toughness), ("willpower", hero.Willpower)
        ];

        var expectedAbilities = abilities.Sum(a =>
        {
            var chargeable = Math.Max(0, a.Rank - package.AbilitiesRank);
            if (chargeable == 0) return 0;

            // An Ability the sheet buys through an item carries the Item Con like any
            // other purchase would.
            var mods = PrebuiltHeroes.AbilityModifiersByHero
                .GetValueOrDefault($"{hero.Name}|{a.Id}", []);
            var flat = mods.Sum(m => _f.Rules.GetCon(m.Split(':')[1])?.CostModifier ?? 0);

            return Math.Max(0, chargeable + flat);
        });
        var expectedTalents   = PrebuiltHeroes.TalentsByHero[hero.Name]
                                    .Sum(r => Math.Max(0, r - package.TalentsRank));

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

    /// <summary>
    /// Twelve of the twenty rebuild to exactly their 125 Hero Point budget. That is the
    /// whole engine end to end — ability and talent costs against a starting package,
    /// baseline ranks, every cost type, and both generic and Power-specific Pros and Cons
    /// — landing on a number the authors published.
    /// </summary>
    [Theory]
    [InlineData("Alabama Slammer")]
    [InlineData("Black Dragon")]
    [InlineData("Blastwave")]
    [InlineData("Citizen Soldier")]
    [InlineData("Combustion")]
    [InlineData("Darkwolf")]
    [InlineData("Eidolon")]
    [InlineData("Nano")]
    [InlineData("Pandora")]
    [InlineData("Psi Lance")]
    [InlineData("Psidearm")]
    [InlineData("Siren")]
    [InlineData("Stronghold")]
    public void HeroRebuildsToExactly125(string name)
    {
        var hero = PrebuiltHeroes.All.Single(h => h.Name == name);

        Assert.Equal(0, PrebuiltHeroes.BuildByHero[name].Residual);
        Assert.Equal(125, _f.Costs.TotalCost(Build(hero)));
    }

    /// <summary>
    /// The other eight, held at the residual they currently show so a change that moves
    /// one is noticed. Each residual has a reason recorded in
    /// <see cref="PrebuiltHeroes.BuildByHero"/>; closing the Chapter 6 gear gap should
    /// take several of them to zero.
    /// </summary>
    [Theory]
    [InlineData("Herald (Airmid)")]
    [InlineData("Herald (Scathach)")]
    [InlineData("Shadow")]
    [InlineData("T-Kay")]
    [InlineData("Talon")]
    [InlineData("Vector")]
    [InlineData("Vigilant")]
    public void HeroRebuildsToItsKnownResidual(string name)
    {
        var hero     = PrebuiltHeroes.All.Single(h => h.Name == name);
        var residual = PrebuiltHeroes.BuildByHero[name].Residual;

        Assert.NotEqual(0, residual);
        Assert.Equal(125 + residual, _f.Costs.TotalCost(Build(hero)));
    }

    [Fact]
    public void MostHeroesReconcileExactly()
    {
        var exact = PrebuiltHeroes.BuildByHero.Count(kv => kv.Value.Residual == 0);
        Assert.Equal(13, exact);

        // Nothing is more than 6 Hero Points out.
        Assert.All(PrebuiltHeroes.BuildByHero,
            kv => Assert.True(Math.Abs(kv.Value.Residual) <= 6, $"{kv.Key} is {kv.Value.Residual} out."));
    }

    [Fact]
    public void EveryHeroRecordsAPackageThatExists() =>
        Assert.All(PrebuiltHeroes.BuildByHero, kv =>
            Assert.Contains(_f.Rules.CreationRules.OptionalPackages, p => p.Id == kv.Value.Package));

    [Fact]
    public void AllTwentyHeroesAreTranscribed() =>
        Assert.Equal(20, PrebuiltHeroes.All.Count);

    [Fact]
    public void EveryHeroIsStandardTierAndSoCapsAt12d() =>
        Assert.Equal(12, _f.Rules.GetTier("standard")!.TraitCapRank);
}
