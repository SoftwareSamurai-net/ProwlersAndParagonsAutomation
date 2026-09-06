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
    /// Turns a printed sheet into a <see cref="CharacterSheet"/>.
    ///
    /// <para><b>It lives in <see cref="PrebuiltHeroSheets"/> rather than here, because a second
    /// suite needs it.</b> The encounter server's tests put two published Heroes into a fight, and
    /// the honest way to do that is to build them the way this class does rather than to write two
    /// more sheets by hand that nobody has checked against the book. A private copy in each place
    /// would be two transcriptions of one rulebook page.</para>
    /// </summary>
    private CharacterSheet Build(PrebuiltHeroes.Hero hero) =>
        PrebuiltHeroSheets.Build(_f.Rules, _f.Derived, hero);

    /// <summary>
    /// Overload used only by <see cref="NoOtherPackageLandsAnyOfTheThreeUnclosedHeroesOnExactly125"/>
    /// to rebuild a Hero under a package other than the one recorded against them, so that test can
    /// sweep every package rather than trusting the one <see cref="PrebuiltHeroes.BuildByHero"/>
    /// already picked as closest.
    /// </summary>
    private CharacterSheet Build(PrebuiltHeroes.Hero hero, string packageId) =>
        PrebuiltHeroSheets.Build(_f.Rules, _f.Derived, hero, packageId);

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

    /// <summary>
    /// <b>Scáthach's printed sheet is the one published datum that says a Talent nomination must
    /// not count towards Resolve, and it is worth an assertion of its own.</b> Ch.8 p.135 prints
    /// Expertise (Academics: Strategy and Tactics) at 12d — the Standard tier's Trait Cap exactly
    /// — beside a Resolve of 5. Resolve is measured from the gap between the cap and the highest
    /// relevant rank, so an Expertise sitting *on* the cap and counting would open her on nothing
    /// but her Determination and her Flaw. The book says 5.
    ///
    /// <para><b>Both figures come out of the engine, because the point is the difference.</b> The
    /// second sheet is hers with the nomination moved from a Talent to Might and the purchased
    /// ranks adjusted so the Expertise still lands on 12d — one field changed, the rank held —
    /// and it opens on 3. A reading of "combat skills" that reached Academics would print 3 for a
    /// Hero the book prints at 5, which is the whole of why no Talent is in the set.</para>
    ///
    /// <para><see cref="HeroResolveMatchesTheRulebook"/> covers her too, and would go red on the
    /// same mistake. This is here because that test says nothing about *why* she is a hard case,
    /// and she is the only published Hero whose Expertise reaches the cap.</para>
    /// </summary>
    [Fact]
    public void ScathachsPrintedResolveIsWhatSaysATalentNominationDoesNotCount()
    {
        var hero  = PrebuiltHeroes.All.Single(h => h.Name == "Herald (Scathach)");
        var sheet = Build(hero);

        var expertise = sheet.SelectedPowers.Single(sp => sp.PowerId == "expertise");

        // Positive controls on the reconstruction: the nomination really is a Talent, and the
        // Expertise really does reach the cap. Either being untrue would make the two figures
        // below differ for a reason that has nothing to do with the carve-out.
        Assert.Equal("academics", expertise.BaselineTraitId);
        Assert.NotNull(_f.Rules.GetTalent(expertise.BaselineTraitId!));
        Assert.Equal(12, _f.Derived.GetEffectiveRank(expertise, sheet));

        Assert.Equal(5, hero.Resolve);
        Assert.Equal(hero.Resolve, _f.Derived.CalculateResolve(sheet));

        // The same sheet with the nomination moved into the combat set and the rank held at 12d:
        // Might is 8d, so four purchased ranks reach the cap where ten did over Academics 2d.
        var counting  = Build(hero);
        var index     = counting.SelectedPowers.FindIndex(sp => sp.PowerId == "expertise");
        var mightRank = counting.GetAbilityRank("might");

        counting.SelectedPowers[index] = counting.SelectedPowers[index] with
        {
            PurchasedRanks  = 12 - mightRank,
            BaselineTraitId = "might"
        };

        Assert.Contains(
            "might",
            _f.Rules.GetPower("expertise")!.AffectsResolveWhenNominated,
            StringComparer.Ordinal);
        Assert.Equal(12, _f.Derived.GetEffectiveRank(counting.SelectedPowers[index], counting));

        Assert.Equal(3, _f.Derived.CalculateResolve(counting));
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
        // Matched by position rather than by id, because a sheet can print the same Power twice:
        // Herald (Airmid) carries Expertise for Medicine and again for Science, and looking one up
        // by id threw rather than checking either of them.
        var ranked = hero.Powers.Select((p, index) => (p, index)).Where(x => x.p.EffectiveRank > 0);

        foreach (var (p, index) in ranked)
        {
            var selection = sheet.SelectedPowers[index];

            Assert.Equal(p.Id, selection.PowerId);
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
    /// Sixteen of the twenty rebuild to exactly their 125 Hero Point budget. That is the
    /// whole engine end to end — ability and talent costs against a starting package,
    /// baseline ranks, every cost type, and both generic and Power-specific Pros and Cons
    /// — landing on a number the authors published.
    ///
    /// <para>Herald (Airmid) is a seventeenth exact Hero and is deliberately not listed here:
    /// she has her own test, <see cref="TheHeraldsAirmidCarriesTwoExpertisePowers"/>, which asserts
    /// the same 125 alongside the transcription fault that produced it. Both counts are read
    /// off <see cref="PrebuiltHeroes.BuildByHero"/> by
    /// <see cref="MostHeroesReconcileExactly"/>, which is what holds them together.</para>
    ///
    /// <para>T-Kay joined this list on 2026-09-06, when the owner ruled her
    /// <c>Limited: only for Telekinesis</c> Con is *somewhat* limited. She is here because of
    /// the ruling; the ruling is not here because of her total.</para>
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
    [InlineData("T-Kay")]
    [InlineData("Talon")]
    [InlineData("Vector")]
    public void HeroRebuildsToExactly125(string name)
    {
        var hero = PrebuiltHeroes.All.Single(h => h.Name == name);

        Assert.Equal(0, PrebuiltHeroes.BuildByHero[name].Residual);
        Assert.Equal(125, _f.Costs.TotalCost(Build(hero)));
    }

    /// <summary>
    /// The other three, held at the residual they currently show so a change that moves one
    /// is noticed. Each residual has a reason recorded in
    /// <see cref="PrebuiltHeroes.BuildByHero"/>. All three are 1 Hero Point out.
    ///
    /// <para>Herald (Airmid) used to be here at +2, the worst of them. She is exact now: her sheet
    /// prints two Expertise Powers and only one was transcribed, and the missing one is worth
    /// exactly the 5 Hero Points her wrongly-attributed package was absorbing.</para>
    ///
    /// <para>T-Kay was the fourth until 2026-09-06, when the owner ruled her
    /// <c>Limited: only for Telekinesis</c> Con is <em>somewhat</em> limited rather than
    /// significantly limited. That is a reading of the Con, decided by the owner; it closes her
    /// at 125 as a consequence, and she is asserted by
    /// <see cref="HeroRebuildsToExactly125"/> now.</para>
    /// </summary>
    [Theory]
    [InlineData("Herald (Scathach)")]
    [InlineData("Shadow")]
    [InlineData("Vigilant")]
    public void HeroRebuildsToItsKnownResidual(string name)
    {
        var hero     = PrebuiltHeroes.All.Single(h => h.Name == name);
        var residual = PrebuiltHeroes.BuildByHero[name].Residual;

        Assert.NotEqual(0, residual);
        Assert.Equal(125 + residual, _f.Costs.TotalCost(Build(hero)));
    }

    /// <summary>
    /// A per-element breakdown instrument built independently of this file (a scratch console
    /// project against the same engine, in the session that added this test) recomputed every
    /// Ability, Talent, Power, Perk and package line for these Heroes by hand from
    /// <c>data/rules</c> and found no mispriced element — the same negative result
    /// <c>PROGRESS.md</c> already recorded. One question that instrument could answer cheaply
    /// and that nothing before it had checked directly: does <em>any</em> package other than the
    /// one <see cref="PrebuiltHeroes.BuildByHero"/> already records as "closest" land the Hero on
    /// exactly 125? It does not, for any of the three, for any package whose granted ranks the
    /// Hero's printed Traits do not fall below. This pins that answer so the "closest package"
    /// inference is not re-litigated by hand again.
    ///
    /// <para>The sweep covered T-Kay too while she was unclosed, and answered no for her as
    /// well. She left this set on 2026-09-06 on the owner's ruling about her <c>Limited</c>
    /// grade — not because a package was found for her.</para>
    /// </summary>
    [Theory]
    [InlineData("Herald (Scathach)")]
    [InlineData("Shadow")]
    [InlineData("Vigilant")]
    public void NoOtherPackageLandsAnyOfTheThreeUnclosedHeroesOnExactly125(string name)
    {
        var hero     = PrebuiltHeroes.All.Single(h => h.Name == name);
        var recorded = PrebuiltHeroes.BuildByHero[name].Package;

        var minAbility = new[] { hero.Agility, hero.Intellect, hero.Might, hero.Perception, hero.Toughness, hero.Willpower }.Min();
        var minTalent  = PrebuiltHeroes.TalentsByHero[name].Min();

        var candidates = _f.Rules.CreationRules.OptionalPackages
            .Where(p => p.Id != recorded)
            .Where(p => minAbility >= p.AbilitiesRank && minTalent >= p.TalentsRank)
            .ToList();

        // A positive control: at least one alternate package must actually be tried, or this
        // test would pass by having nothing to check — the failure shape CLAUDE.md warns about.
        Assert.NotEmpty(candidates);

        Assert.All(candidates, package =>
        {
            var total = _f.Costs.TotalCost(Build(hero, package.Id));
            Assert.NotEqual(125, total);
        });
    }

    [Fact]
    public void MostHeroesReconcileExactly()
    {
        var exact = PrebuiltHeroes.BuildByHero.Count(kv => kv.Value.Residual == 0);
        Assert.Equal(17, exact);

        // Nothing is more than 1 Hero Point out. Airmid was the only 2, and closing her tightened
        // this from 2 — the bound has only ever moved down: 6, then 2, now 1.
        Assert.All(PrebuiltHeroes.BuildByHero,
            kv => Assert.True(Math.Abs(kv.Value.Residual) <= 1, $"{kv.Key} is {kv.Value.Residual} out."));
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

        // Compared as a multiset, not a set: a sheet may print the same Power twice, and Herald
        // (Airmid) does — Expertise for Medicine and again for Science. Requiring the ids to be
        // distinct rejected a faithful transcription.
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
    /// <see cref="TheHeraldsAirmidCarriesTwoExpertisePowers"/>.</para>
    /// </summary>
    [Theory]
    [MemberData(nameof(HeroNames))]
    public void NoHeroPrintsATraitBelowWhatItsPackageGrants(string name)
    {




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
    /// <para><b>It was the lead that closed her</b>, and the closing needed no guesswork: her
    /// sheet prints <em>two</em> Expertise Powers, "Expertise (Medicine: Ancient Remedies) 12d"
    /// and "Expertise (Science: Botany) 12d", and only the first was transcribed. Expertise costs
    /// half a Hero Point per rank and takes its baseline from the nominated Trait, so 12d over
    /// Science 2d is ten purchased ranks and exactly 5 HP — which is what the wrong package was
    /// absorbing. With the second Expertise transcribed and the package corrected to the one her
    /// printed 2d Talents allow, she lands on 125 to the point.</para>
    ///
    /// <para>Both halves are forced by the printed page rather than chosen to make the number
    /// come out, which is the distinction this item turns on.</para>
    /// </summary>
    [Fact]
    public void TheHeraldsAirmidCarriesTwoExpertisePowers()
    {
        var airmid = PrebuiltHeroes.All.Single(h => h.Name == "Herald (Airmid)");

        var expertise = airmid.Powers.Where(p => p.Id == "expertise").ToList();

        Assert.Equal(2, expertise.Count);
        Assert.Equal(["medicine", "science"], expertise.Select(p => p.BaselineTrait).Order());

        // Half a Hero Point per rank is what makes the second one worth exactly the 5 HP the
        // wrong package was absorbing.
        Assert.Equal(0.5, _f.Rules.GetPower("expertise")!.CostPerRank);
        Assert.Equal("hero_package", PrebuiltHeroes.BuildByHero["Herald (Airmid)"].Package);
        Assert.Equal(0, PrebuiltHeroes.BuildByHero["Herald (Airmid)"].Residual);
    }


    /// <summary>
    /// Every published Hero is a legal character, and the only error the validator may
    /// raise about one is the Hero Point budget of the three that do not reconcile.
    ///
    /// <para><b>Nothing asked this before, and two rules were wrong because of it.</b> The
    /// other tests here ask what a Hero <em>costs</em> and what their derived stats come to;
    /// none asked whether the character the authors printed is one this tool would accept.
    /// Both faults it found made a Hero in the book unbuildable: Blastwave's six energy
    /// types came back as four <c>DUPLICATE_PRO</c> errors while the calculator charged all
    /// five copies and landed him exactly on 125, and T-Kay's printed
    /// <c>Force Field 12d (Zone)</c> came back <c>PRO_NOT_APPLICABLE</c> because Force Field
    /// is Self range — which the Power's own entry overrides in as many words.</para>
    ///
    /// <para>The budget exemption is deliberately narrow: it is keyed to the residual
    /// recorded in <see cref="PrebuiltHeroes.BuildByHero"/>, so a Hero who starts costing
    /// the wrong amount fails <see cref="HeroRebuildsToItsKnownResidual"/> rather than being
    /// excused here.</para>
    /// </summary>
    [Theory]
    [MemberData(nameof(HeroNames))]
    public void EveryPublishedHeroIsALegalCharacter(string name)
    {
        var hero  = PrebuiltHeroes.All.Single(h => h.Name == name);
        var sheet = Build(hero);

        var errors = _f.Validator.Validate(sheet).Issues
            .Where(i => i.Severity == ValidationSeverity.Error)
            .Where(i => !(i.Code == "HP_BUDGET_EXCEEDED"
                          && PrebuiltHeroes.BuildByHero[hero.Name].Residual > 0))
            .ToList();

        Assert.True(errors.Count == 0,
            $"{hero.Name} is printed in the rulebook and this tool refuses them: "
            + string.Join(" | ", errors.Select(e => $"{e.Code}: {e.Message}")));
    }

    [Fact]
    public void EveryHeroIsCitedInsideChapterEight()
    {
        Assert.All(PrebuiltHeroes.All, hero => Assert.InRange(hero.Page, 127, 146));

        Assert.Equal(PrebuiltHeroes.All.Count,
                     PrebuiltHeroes.All.Select(h => h.Page).Distinct().Count());
    }
}
