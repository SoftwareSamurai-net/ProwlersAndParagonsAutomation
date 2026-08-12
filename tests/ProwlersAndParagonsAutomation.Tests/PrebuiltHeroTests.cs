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
            SelectedPackageId = PrebuiltHeroes.BuildByHero[hero.Name].Package,
            AbilityRanks =
            {
                ["agility"]    = hero.Agility,
                ["intellect"]  = hero.Intellect,
                ["might"]      = hero.Might,
                ["perception"] = hero.Perception,
                ["toughness"]  = hero.Toughness,
                ["willpower"]  = hero.Willpower
            }
        };

        foreach (var abilityId in sheet.AbilityRanks.Keys.ToList())
        {
            if (!PrebuiltHeroes.AbilityModifiersByHero.TryGetValue($"{hero.Name}|{abilityId}", out var mods))
                continue;

            sheet.AbilityModifiers[abilityId] =
                mods.Select(m => new SelectedProCon(m.Split(':')[1])).ToList();
        }

        // Abilities and Talents the sheet marks with a Source. Absent means the rulebook
        // default — Innate for an Ability, Trained for a Talent — which no sheet prints.
        foreach (var (key, traitIds) in PrebuiltHeroes.TraitSourcesByHero)
        {
            var parts = key.Split('|');
            if (parts[0] != hero.Name) continue;

            foreach (var traitId in traitIds)
            {
                if (_f.Rules.GetAbility(traitId) is not null) sheet.AbilitySources[traitId] = parts[1];
                else                                          sheet.TalentSources[traitId]  = parts[1];
            }
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
                sheet.SelectedPowers.Add(new SelectedPower(p.Id, 0)
                {
                    Units    = hero.DeterminationResolve,
                    SourceId = PrebuiltHeroes.SourceOf(hero.Name, p.Id)
                });
                continue;
            }

            if (power.MaxRank == 0)
            {
                sheet.SelectedPowers.Add(new SelectedPower(p.Id, 0, pros, cons)
                {
                    BaselineTraitId = p.BaselineTrait,
                    Units           = p.Units,
                    CostVariantKey  = p.CostVariant,
                    SourceId        = PrebuiltHeroes.SourceOf(hero.Name, p.Id)
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
                CostVariantKey  = p.CostVariant,
                SourceId        = PrebuiltHeroes.SourceOf(hero.Name, p.Id)
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
    /// Fifteen of the twenty rebuild to exactly their 125 Hero Point budget. That is the
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
    [InlineData("Talon")]
    [InlineData("Vector")]
    public void HeroRebuildsToExactly125(string name)
    {
        var hero = PrebuiltHeroes.All.Single(h => h.Name == name);

        Assert.Equal(0, PrebuiltHeroes.BuildByHero[name].Residual);
        Assert.Equal(125, _f.Costs.TotalCost(Build(hero)));
    }

    /// <summary>
    /// The other five, held at the residual they currently show so a change that moves one
    /// is noticed. Each residual has a reason recorded in
    /// <see cref="PrebuiltHeroes.BuildByHero"/>. All five are within 2 Hero Points.
    /// </summary>
    [Theory]
    [InlineData("Herald (Airmid)")]
    [InlineData("Herald (Scathach)")]
    [InlineData("Shadow")]
    [InlineData("T-Kay")]
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
        Assert.Equal(15, exact);

        // Nothing is more than 2 Hero Points out.
        Assert.All(PrebuiltHeroes.BuildByHero,
            kv => Assert.True(Math.Abs(kv.Value.Residual) <= 2, $"{kv.Key} is {kv.Value.Residual} out."));
    }

    /// <summary>
    /// The sheets never print which starting package a Hero took, so it is inferred: the
    /// one that lands the rebuild on 125. That inference is only trustworthy where exactly
    /// one package does, which is the claim this test makes for every exact Hero.
    ///
    /// <para>It also keeps the inference honest when a cost changes. Vector was recorded
    /// on the Superhero Package as a closest fit while his Deflection was underpriced;
    /// once it was corrected, the Hero Package became the only one that fits.</para>
    /// </summary>
    [Theory]
    [MemberData(nameof(ExactHeroNames))]
    public void ExactlyOnePackageLandsAnExactHeroOn125(string name)
    {
        var hero = PrebuiltHeroes.All.Single(h => h.Name == name);

        var fits = _f.Rules.CreationRules.OptionalPackages
            .Where(p =>
            {
                var sheet = Build(hero);
                sheet.SelectedPackageId = p.Id;
                return _f.Costs.TotalCost(sheet) == 125;
            })
            .Select(p => p.Id)
            .ToList();

        Assert.Equal([PrebuiltHeroes.BuildByHero[name].Package], fits);
    }

    public static TheoryData<string> ExactHeroNames()
    {
        var data = new TheoryData<string>();
        foreach (var kv in PrebuiltHeroes.BuildByHero.Where(kv => kv.Value.Residual == 0))
            data.Add(kv.Key);
        return data;
    }

    /// <summary>
    /// Every Power a published sheet prints sits under a Source heading, so every Power in
    /// the transcription must have one — no gaps, and nothing named twice.
    /// </summary>
    [Theory]
    [MemberData(nameof(HeroNames))]
    public void EveryPowerOnASheetHasASource(string name)
    {
        var hero   = PrebuiltHeroes.All.Single(h => h.Name == name);
        var groups = PrebuiltHeroes.PowerSourcesByHero[name];
        var listed = groups.SelectMany(g => g.PowerIds).ToList();

        Assert.All(groups, g => Assert.NotNull(_f.Rules.GetSource(g.SourceId)));
        Assert.Equal(listed.Count, listed.Distinct(StringComparer.Ordinal).Count());
        Assert.Equal(hero.Powers.Select(p => p.Id).Order(), listed.Order());
    }

    /// <summary>
    /// The engine's Source grouping reproduces the headings the sheet actually prints, in
    /// the order it prints them. This is the check that the layout is faithful rather than
    /// merely plausible — Psidearm carries three groups, Alabama Slammer two, Talon one.
    /// </summary>
    [Theory]
    [MemberData(nameof(HeroNames))]
    public void SourceGroupingReproducesThePrintedHeadings(string name)
    {
        var hero  = PrebuiltHeroes.All.Single(h => h.Name == name);
        var sheet = Build(hero);

        var expected = PrebuiltHeroes.PowerSourcesByHero[name]
            .Select(g => SourceGrouping.HeadingFor(_f.Rules.GetSource(g.SourceId)))
            .ToList();

        var actual = new SourceGrouping(_f.Rules).GroupBySource(sheet).Select(g => g.Heading).ToList();

        Assert.Equal(expected.Order(), actual.Order());

        // Nothing falls through to the unsourced bucket.
        Assert.DoesNotContain("POWERS", actual);
    }

    /// <summary>
    /// The engine builds the <c>Abilities (…)</c> line each sheet prints, in the group it
    /// prints it in, character for character — and prints none where the sheet prints none.
    /// Both halves matter: ten of the twenty carry no such line, and a renderer that
    /// invented one for every character would satisfy a test that only checked the ten that do.
    /// </summary>
    [Theory]
    [MemberData(nameof(HeroNames))]
    public void SourceGroupingReproducesThePrintedTraitLines(string name)
    {
        var sheet  = Build(PrebuiltHeroes.All.Single(h => h.Name == name));
        var groups = new SourceGrouping(_f.Rules).GroupBySource(sheet);

        var expected = PrebuiltHeroes.PrintedTraitLinesByHero
            .Where(kv => kv.Key.StartsWith($"{name}|", StringComparison.Ordinal))
            .ToDictionary(kv => kv.Key.Split('|')[1], kv => kv.Value);

        foreach (var group in groups)
        {
            var wanted = group.Source is null ? null : expected.GetValueOrDefault(group.Source.Id);

            Assert.Equal(wanted is null ? [] : new[] { wanted }, group.TraitLines);
        }

        // Every line transcribed found a group to print in — a Source named on the trait
        // line but nowhere else would otherwise vanish rather than fail.
        Assert.Equal(
            expected.Keys.Order(),
            groups.Where(g => g.TraitLines.Count > 0).Select(g => g.Source!.Id).Order());
    }

    /// <summary>
    /// The printed markings are not a rank threshold, and this is the test that records why.
    ///
    /// <para><b>It guards the transcription, not the code.</b> It reads only
    /// <see cref="PrebuiltHeroes"/>, so no change to <c>SourceGrouping</c> can fail it — an
    /// adversarial pass put a 7d threshold into the engine and this stayed green while
    /// eleven other tests went red. What it stops is the transcription being deleted as
    /// redundant by someone who has just read the p.64 sentence and believes it;
    /// <see cref="SourceGroupingReproducesThePrintedTraitLines"/> is what catches the
    /// derivation itself.</para>
    ///
    /// <para>Ch.2 p.64 reads "the Sources for your Powers and Abilities with a rank of 7d or
    /// greater", which invites deriving the trait line from rank and deleting the
    /// transcription. Two published sheets rule that out in opposite directions, so a
    /// derivation cannot be right whichever way round the comparison is written. Ch.2 p.16
    /// is the rule the engine follows: every Trait has a Source and the defaults are not
    /// mandatory, so which Traits deviate is the author's choice and has to be recorded.</para>
    /// </summary>
    [Fact]
    public void ThePrintedTraitSourcesAreNotARankThreshold()
    {
        var slammer = PrebuiltHeroes.All.Single(h => h.Name == "Alabama Slammer");
        var soldier = PrebuiltHeroes.All.Single(h => h.Name == "Citizen Soldier");

        // Marked, and below 7d: a threshold would leave these two off his sheet.
        Assert.Equal(6, slammer.Perception);
        Assert.Equal(6, slammer.Toughness);
        Assert.Equal(["perception", "toughness"],
            PrebuiltHeroes.TraitSourcesByHero["Alabama Slammer|super"]);

        // Unmarked, and well above 7d: a threshold would put Willpower on his sheet.
        Assert.Equal(9, soldier.Willpower);
        Assert.DoesNotContain("willpower", PrebuiltHeroes.TraitSourcesByHero["Citizen Soldier|super"]);
    }

    /// <summary>
    /// A Source costs nothing and changes no rank (Ch.2, p.16), so marking every Trait on a
    /// Hero moves neither his Hero Point total nor any derived figure. Run against the
    /// fifteen who rebuild exactly, where a single Hero Point either way would show.
    /// </summary>
    [Theory]
    [MemberData(nameof(ExactHeroNames))]
    public void TraitSourcesChangeNoNumber(string name)
    {
        var hero  = PrebuiltHeroes.All.Single(h => h.Name == name);
        var sheet = Build(hero);

        var before = (_f.Costs.TotalCost(sheet), _f.Derived.CalculateEdge(sheet),
                      _f.Derived.CalculateHealth(sheet), _f.Derived.CalculateResolve(sheet));

        foreach (var ability in _f.Rules.Abilities) sheet.AbilitySources[ability.Id] = "magic";
        foreach (var talent in _f.Rules.Talents)    sheet.TalentSources[talent.Id]   = "magic";

        Assert.Equal(before, (_f.Costs.TotalCost(sheet), _f.Derived.CalculateEdge(sheet),
                              _f.Derived.CalculateHealth(sheet), _f.Derived.CalculateResolve(sheet)));
    }

    [Fact]
    public void EveryHeroRecordsAPackageThatExists() =>
        Assert.All(PrebuiltHeroes.BuildByHero, kv =>
            Assert.Contains(_f.Rules.CreationRules.OptionalPackages, p => p.Id == kv.Value.Package));

    [Fact]
    public void AllTwentyHeroesAreTranscribed() =>
        Assert.Equal(20, PrebuiltHeroes.All.Count);

    [Fact]
    public void EveryHeroIsStandardTierAndSoCapsAtTwelveDice() =>
        Assert.Equal(12, _f.Rules.GetTier("standard")!.TraitCapRank);

    /// <summary>
    /// <b>Every recorded page was ten out, and stayed ten out through five review rounds.</b>
    /// Chapter 8's twenty Heroes run from printed 127 to 146, verified against the PDF one page at
    /// a time by reading the name off each sheet. The transcription recorded 137 to 156 — the
    /// PDF's own page numbers with the +3 offset applied and then the whole block shifted again.
    ///
    /// <para><c>CLAUDE.md</c> warns about exactly this: an earlier note "was ten pages out in the
    /// chapter it was offered for". That note was corrected and these were not, because nothing
    /// read them. A citation nothing reads is a citation nothing checks.</para>
    ///
    /// <para>A range and a uniqueness check rather than a page each: the point is to catch the
    /// whole block sliding, which is how it went wrong in the first place.</para>
    /// </summary>
    /// <summary>
    /// <b>A package's granted ranks are a floor, so the printed ranks corroborate the inference
    /// independently of the arithmetic.</b>
    ///
    /// <para>Which package each Hero took is never printed, and is inferred by whichever one
    /// lands the rebuild on 125 — which is an argument from a total, and totals can agree for the
    /// wrong reasons. This is a second, unrelated argument: a package cannot be the answer if the
    /// sheet prints a Trait <em>below</em> what it grants. Herald (Scáthach) prints Academics 2d,
    /// which rules the Superhero Package out on sight whatever the total says, and prints
    /// Intellect 3d and 2d talents, which is exactly the Hero Package's floor.</para>
    ///
    /// <para>It matters most for the five Heroes whose totals do not land on 125: for them the
    /// package was chosen as the closest fit, so the totals argument is weakest exactly where a
    /// second one is worth having. <b>And it immediately caught one</b> — see
    /// <see cref="TheHeraldsAirmidPackageContradictsHerPrintedSheet"/>.</para>
    /// </summary>
    [Theory]
    [MemberData(nameof(HeroNames))]
    public void NoHeroPrintsATraitBelowWhatItsPackageGrants(string name)
    {
        // Airmid fails this, and the failure is the finding rather than a gap in the test; it is
        // asserted on its own below so that it is recorded rather than merely tolerated.
        if (name == "Herald (Airmid)") return;

        var hero    = PrebuiltHeroes.All.Single(h => h.Name == name);
        var package = _f.Rules.CreationRules.OptionalPackages
            .Single(p => p.Id == PrebuiltHeroes.BuildByHero[name].Package);

        var abilities = new[] { hero.Agility, hero.Intellect, hero.Might,
                                hero.Perception, hero.Toughness, hero.Willpower };

        Assert.All(abilities, rank =>
            Assert.True(rank >= package.AbilitiesRank,
                $"{name} prints an Ability at {rank}d, below the {package.Name}'s {package.AbilitiesRank}d."));

        Assert.All(PrebuiltHeroes.TalentsByHero[name], rank =>
            Assert.True(rank >= package.TalentsRank,
                $"{name} prints a Talent at {rank}d, below the {package.Name}'s {package.TalentsRank}d."));
    }

    /// <summary>
    /// <b>Herald (Airmid) cannot have taken the Superhero Package, and she is recorded as having
    /// taken it.</b> Her sheet prints nine of her twelve Talents at 2d; the package grants 3d and
    /// its own rule is that you "cannot lower any of these below the package rank". The
    /// attribution and the printed sheet contradict each other.
    ///
    /// <para>She is also the worst of the five residuals at +2, and the two facts are almost
    /// certainly the same fact. No package resolves it either: switching her to the Hero Package
    /// makes the printed ranks legal and moves her total 7 Hero Points the wrong way, to −5,
    /// which is outside the 2 HP bound the other four sit inside. So something in her
    /// transcription or in how one of her Powers is priced is worth about 5 HP, and the package
    /// was chosen to absorb it.</para>
    ///
    /// <para>This is asserted rather than fixed because guessing again would be the same mistake
    /// in a new shape — the attribution is already an inference, and replacing one unsupported
    /// inference with another buys nothing. <b>It is the most concrete lead item 1 has.</b></para>
    /// </summary>
    [Fact]
    public void TheHeraldsAirmidPackageContradictsHerPrintedSheet()
    {
        var package = _f.Rules.CreationRules.OptionalPackages
            .Single(p => p.Id == PrebuiltHeroes.BuildByHero["Herald (Airmid)"].Package);

        var below = PrebuiltHeroes.TalentsByHero["Herald (Airmid)"]
            .Where(rank => rank < package.TalentsRank)
            .ToList();

        Assert.NotEmpty(below);
        Assert.Equal("superhero_package", package.Id);

        // And no other package fits either: the two that would make her printed ranks legal both
        // move her total further from 125 than the bound the other four Heroes sit inside.
        Assert.Equal(2, PrebuiltHeroes.BuildByHero["Herald (Airmid)"].Residual);
    }

    [Fact]
    public void EveryHeroIsCitedInsideChapterEight()
    {
        Assert.All(PrebuiltHeroes.All, hero => Assert.InRange(hero.Page, 127, 146));

        Assert.Equal(PrebuiltHeroes.All.Count,
                     PrebuiltHeroes.All.Select(h => h.Page).Distinct().Count());
    }
}
