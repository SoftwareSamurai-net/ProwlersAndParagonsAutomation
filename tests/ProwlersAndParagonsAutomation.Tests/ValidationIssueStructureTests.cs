using System.Text.RegularExpressions;
using ProwlersAndParagonsAutomation.Engine;

namespace ProwlersAndParagonsAutomation.Tests;

/// <summary>
/// The machine-readable half of a validation issue, which exists so that something other
/// than a person can act on a finding.
///
/// <para><b>Asserting that the fields are populated would be theatre.</b> A field can hold
/// the wrong id, the value and the limit can be the wrong way round, and an option list can
/// offer something the data will not accept — and every one of those passes a test that only
/// checks for a non-null. So most of what follows <em>uses</em> the structure: it reads an
/// issue, applies the repair the structure implies, re-validates, and asserts the finding is
/// gone. A field that does not survive that is not carrying what it claims.</para>
/// </summary>
[Collection(SharedRules.Name)]
public sealed class ValidationIssueStructureTests
{
    private readonly RulesFixture _f;

    public ValidationIssueStructureTests(RulesFixture f) => _f = f;

    /// <summary>
    /// A legal starting point — which means all eighteen Traits at the 1d minimum, not an empty
    /// sheet with a flaw on it. Ch.2 says no Ability or Talent can be lower than 1d, so the
    /// emptier version was never legal; it merely passed a validator that did not check.
    /// </summary>
    private CharacterSheet Legal()
    {
        var sheet = _f.LegalSheet();
        sheet.Flaws.Clear();
        sheet.Flaws.Add(new SelectedFlaw("code"));
        return sheet;
    }

    private ValidationIssue Issue(CharacterSheet sheet, string code) =>
        Assert.Single(_f.Validator.Validate(sheet).Issues, i => i.Code == code);

    private bool Reports(CharacterSheet sheet, string code) =>
        _f.Validator.Validate(sheet).Issues.Any(i => i.Code == code);

    // ── Repairing from the structure alone ────────────────────────────────

    /// <summary>
    /// The canonical repair: a Trait over the cap. Everything needed to fix it — which
    /// Trait, which kind of Trait, what it is and what it may be — comes off the issue, and
    /// nothing is read out of the sentence.
    /// </summary>
    [Fact]
    public void ATraitOverTheCapCanBeBroughtBackFromTheIssueAlone()
    {
        var sheet = Legal();
        sheet.AbilityRanks["intellect"] = 40;

        var issue = Issue(sheet, "TRAIT_ABOVE_CAP");

        Assert.Equal(ValidationSubject.Ability, issue.SubjectKind);
        Assert.Equal("intellect", issue.SubjectId);
        Assert.Equal(40, issue.Value);
        Assert.Equal(_f.Rules.GetTier("standard")!.TraitCapRank, issue.Limit);

        // The repair, made entirely out of the issue.
        sheet.AbilityRanks[issue.SubjectId!] = issue.Limit!.Value;

        Assert.False(Reports(sheet, "TRAIT_ABOVE_CAP"));
    }

    /// <summary>
    /// The same, for a Talent — because the kind is what tells a caller which dictionary to
    /// write into, and getting it wrong would put an ability id among the talents, where it
    /// would be reported as an unknown Trait rather than fixed.
    /// </summary>
    [Fact]
    public void ATalentOverTheCapNamesTheTalentsRatherThanTheAbilities()
    {
        var sheet = Legal();
        sheet.TalentRanks["academics"] = 40;

        var issue = Issue(sheet, "TRAIT_ABOVE_CAP");

        Assert.Equal(ValidationSubject.Talent, issue.SubjectKind);
        Assert.Equal("academics", issue.SubjectId);

        sheet.TalentRanks[issue.SubjectId!] = issue.Limit!.Value;
        Assert.False(Reports(sheet, "TRAIT_ABOVE_CAP"));
    }

    /// <summary>
    /// <b>A house Trait Cap is the cap Traits are measured against, and the finding says so.</b>
    /// A 7d Ability is legal at the Standard tier's 12d and is not legal under a table's 6d, and
    /// the <c>Limit</c> a repair loop reads is <b>6</b> — the ceiling the character is actually
    /// built to, not the one its tier would allow. A limit naming the tier here would have a
    /// caller "repair" the Ability to 12d and be told again.
    /// </summary>
    [Fact]
    public void ATraitUnderTheTiersCapAndOverTheHouseOneIsReportedAgainstTheHouseOne()
    {
        var sheet = Legal();
        sheet.AbilityRanks["intellect"] = 7;

        // The positive control: 7d is legal at this tier, so what follows is the house cap
        // doing the work rather than the tier's 12d catching it anyway.
        Assert.False(Reports(sheet, "TRAIT_ABOVE_CAP"));

        sheet.TraitCapRank = 6;

        var issue = Issue(sheet, "TRAIT_ABOVE_CAP");

        Assert.Equal(ValidationSubject.Ability, issue.SubjectKind);
        Assert.Equal("intellect", issue.SubjectId);
        Assert.Equal(7, issue.Value);
        Assert.Equal(6, issue.Limit);

        // The repair, made entirely out of the issue, as for the tier's cap.
        sheet.AbilityRanks[issue.SubjectId!] = issue.Limit!.Value;

        Assert.False(Reports(sheet, "TRAIT_ABOVE_CAP"));
    }

    /// <summary>
    /// <b>A house cap above the tier's is reported and still used.</b> The engine is a judge and
    /// does not repair, so the error carries the house cap in <c>Value</c> and the tier's in
    /// <c>Limit</c> — and a 20d Ability under a 20d house cap at a 12d tier is <em>not</em>
    /// separately reported as a Trait over the cap, because the cap it is built to really is 20d.
    /// A validator that clamped instead would report the Trait and hide the cap.
    /// </summary>
    [Fact]
    public void AHouseCapAboveTheTiersIsTheErrorAndIsStillTheCapInForce()
    {
        var sheet = Legal();
        sheet.TraitCapRank = 20;
        sheet.AbilityRanks["intellect"] = 20;

        var issue = Issue(sheet, "TRAIT_CAP_ABOVE_TIER");

        Assert.Equal(ValidationSubject.Character, issue.SubjectKind);
        Assert.Equal(20, issue.Value);
        Assert.Equal(_f.Rules.GetTier("standard")!.TraitCapRank, issue.Limit);
        Assert.False(Reports(sheet, "TRAIT_ABOVE_CAP"));

        // The repair the structure implies: bring the house cap down to the tier's. The Ability
        // is then over the cap, which is the finding that was true all along and was hidden
        // behind a cap nobody was allowed to set.
        sheet.TraitCapRank = issue.Limit!.Value;

        Assert.False(Reports(sheet, "TRAIT_CAP_ABOVE_TIER"));
        Assert.True(Reports(sheet, "TRAIT_ABOVE_CAP"));
    }

    /// <summary>
    /// A ceiling below the 1d floor no Trait may go under. Reported on its own rather than folded
    /// into the finding above, because the two are different mistakes with different repairs and
    /// one of them does not need a tier to be wrong.
    /// </summary>
    [Theory]
    [InlineData(0)]
    [InlineData(-3)]
    public void AHouseCapBelowTheTraitFloorIsRefused(int house)
    {
        var sheet = Legal();
        sheet.TraitCapRank = house;

        var issue = Issue(sheet, "TRAIT_CAP_BELOW_MINIMUM");

        Assert.Equal(ValidationSubject.Character, issue.SubjectKind);
        Assert.Equal(house, issue.Value);
        Assert.Equal(1, issue.Limit);

        // And it is reported with no tier at all, which is the reason it is checked outside the
        // tier block: a cap under the floor is nonsense before anybody has picked a power level.
        var untiered = new CharacterSheet { TraitCapRank = house };
        Assert.True(Reports(untiered, "TRAIT_CAP_BELOW_MINIMUM"));
        Assert.False(Reports(untiered, "TRAIT_CAP_ABOVE_TIER"));
    }

    /// <summary>
    /// A choice the rules fix the set of. The options have to be the keys the data accepts,
    /// not the prose the message sets them as: the message humanises
    /// <c>very_accurate</c> to "Very Accurate", and writing that back would be a second
    /// unknown key rather than a repair.
    /// </summary>
    [Fact]
    public void AVariantCanBeChosenFromTheOptionsTheIssueOffers()
    {
        var sheet = Legal();
        sheet.SelectedPowers.Add(new SelectedPower("omni_power", 2));

        var issue = Issue(sheet, "POWER_VARIANT_NOT_CHOSEN");
        Assert.NotEmpty(issue.Options);

        foreach (var option in issue.Options)
        {
            var repaired = Legal();
            repaired.SelectedPowers.Add(new SelectedPower("omni_power", 2) { CostVariantKey = option });

            Assert.False(Reports(repaired, "POWER_VARIANT_NOT_CHOSEN"));

            // And the choice is one the engine can actually price, which is the whole reason
            // the finding is an error rather than a warning.
            Assert.True(_f.Costs.TotalCost(repaired) > 0);
        }
    }

    /// <summary>
    /// A gear feature is the one subject that is not top-level: it sits on an item, and the
    /// item is named by nothing else. Without the owner a caller can find the feature and not
    /// the thing to change it on.
    /// </summary>
    [Fact]
    public void AGearFeatureIssueNamesBothTheFeatureAndTheItemItIsOn()
    {
        var sheet = Legal();
        sheet.Gear.Add(new SelectedGear("Pistol") { Features = [new("accurate")] });

        var issue = Issue(sheet, "GEAR_FEATURE_NEEDS_GRADE");

        Assert.Equal(ValidationSubject.GearFeature, issue.SubjectKind);
        Assert.Equal("accurate", issue.SubjectId);
        Assert.Equal("Pistol", issue.OwnerId);

        foreach (var grade in issue.Options)
        {
            var repaired = Legal();
            repaired.Gear.Add(new SelectedGear(issue.OwnerId!)
            {
                Features = [new(issue.SubjectId!, grade)]
            });

            Assert.False(Reports(repaired, "GEAR_FEATURE_NEEDS_GRADE"));
        }
    }

    /// <summary>
    /// The six Sources, offered wherever one is missing or wrong. Every option has to be a
    /// Source the rules have, or the repair swaps one unknown id for another.
    /// </summary>
    [Fact]
    public void ASourceIssueOffersTheSixSourcesAndEachOneClosesIt()
    {
        var sheet = Legal();
        sheet.SelectedPowers.Add(new SelectedPower("communications", 0));

        var issue = Issue(sheet, "RANKLESS_POWER_WITHOUT_SOURCE");

        Assert.Equal(_f.Rules.Sources.Select(s => s.Id).Order(), issue.Options.Order());
        Assert.All(issue.Options, o => Assert.NotNull(_f.Rules.GetSource(o)));

        foreach (var source in issue.Options)
        {
            var repaired = Legal();
            repaired.SelectedPowers.Add(new SelectedPower("communications", 0) { SourceId = source });

            Assert.False(Reports(repaired, "RANKLESS_POWER_WITHOUT_SOURCE"));
        }
    }

    /// <summary>
    /// <b>Every Source finding, said outright rather than through a list.</b>
    /// <see cref="MustOfferOptions"/> is a hand-maintained set, and deleting a code's
    /// <c>Options</c> line <em>and</em> its entry in that set is a coordinated edit both halves
    /// of the machinery miss — so nothing anywhere stated that <c>POWER_WITHOUT_SOURCE</c> must
    /// offer the six Sources, which is the finding the whole option check was written for.
    /// Naming them here does not depend on the list at all.
    ///
    /// <para><b>There are five places that offer the Sources, not four</b>, and an earlier
    /// version of this test and of the account in <c>PROGRESS.md</c> both said four — counting a
    /// line shared by the Ability and Talent arms twice, and missing the fifth entirely. The one
    /// missed was the <em>blank</em> Source, which has its own sentence because printed through
    /// the other one it read "names a Source, '', that is not one of the six": a Trait that names
    /// a Source and then names none. It is reachable from exactly the hand-written or stale saved
    /// character this validator exists for, and its options could be made perk ids with the whole
    /// suite green — the same dead-branch defect as the Talent arm, in the same method.</para>
    /// </summary>
    [Fact]
    public void EveryFindingAboutAMissingSourceOffersTheSixSources()
    {
        var sources = _f.Rules.Sources.Select(s => s.Id).Order().ToList();

        var withoutSource = Legal();
        withoutSource.SelectedPowers.Add(new SelectedPower("blast", 3));
        Assert.Equal(sources, Issue(withoutSource, "POWER_WITHOUT_SOURCE").Options.Order());

        var ranklessWithoutSource = Legal();
        ranklessWithoutSource.SelectedPowers.Add(new SelectedPower("communications", 0));
        Assert.Equal(sources, Issue(ranklessWithoutSource, "RANKLESS_POWER_WITHOUT_SOURCE").Options.Order());

        var badOnAPower = Legal();
        badOnAPower.SelectedPowers.Add(new SelectedPower("blast", 3) { SourceId = "cosmic" });
        Assert.Equal(sources, Issue(badOnAPower, "UNKNOWN_SOURCE").Options.Order());

        var badOnAnAbility = Legal();
        badOnAnAbility.AbilitySources["might"] = "cosmic";
        Assert.Equal(sources, Issue(badOnAnAbility, "UNKNOWN_SOURCE").Options.Order());

        var badOnATalent = Legal();
        badOnATalent.TalentSources["academics"] = "cosmic";
        Assert.Equal(sources, Issue(badOnATalent, "UNKNOWN_SOURCE").Options.Order());

        // The fifth: a Source recorded with no value, which is its own arm and its own sentence.
        var blankOnAnAbility = Legal();
        blankOnAnAbility.AbilitySources["might"] = "";
        Assert.Equal(sources, Issue(blankOnAnAbility, "UNKNOWN_SOURCE").Options.Order());
    }

    /// <summary>
    /// A tier has to be chosen before anything else can be checked, so the one finding a
    /// fresh character always produces carries the list of tiers to choose from.
    /// </summary>
    [Fact]
    public void TheMissingTierCarriesTheTiersToChooseFrom()
    {
        var issue = Issue(new CharacterSheet(), "NO_TIER_SELECTED");

        Assert.Equal(ValidationSubject.Character, issue.SubjectKind);
        Assert.Equal(_f.Rules.Tiers.Select(t => t.Id).Order(), issue.Options.Order());

        foreach (var tier in issue.Options)
            Assert.False(Reports(new CharacterSheet { SelectedTierId = tier }, "NO_TIER_SELECTED"));
    }

    /// <summary>
    /// <b>A misspelled tier used to be reported as a legal character.</b> Both limits a
    /// character can break — the Hero Point budget and the Trait Cap — hang off the tier, and
    /// both were skipped when it could not be found. So a 99d Ability on tier
    /// <c>"stanadrd"</c> came back with no findings at all, which is the worst answer a
    /// validator has available: confident, and wrong.
    /// </summary>
    [Fact]
    public void AnUnknownTierIsReportedRatherThanTurningEveryLimitOff()
    {
        var sheet = Legal();
        sheet.SelectedTierId = "stanadrd";
        sheet.AbilityRanks["might"] = 99;

        var issue = Issue(sheet, "UNKNOWN_TIER");

        Assert.Equal(ValidationSubject.Tier, issue.SubjectKind);
        Assert.Equal("stanadrd", issue.SubjectId);
        Assert.Equal(_f.Rules.Tiers.Select(t => t.Id).Order(), issue.Options.Order());
        Assert.False(_f.Validator.Validate(sheet).IsValid);

        // And choosing a real one from the options gets **both** limits back, not just the
        // one. Checking only the Trait Cap here left deleting the budget check undetected.
        sheet.SelectedTierId = issue.Options[0];
        Assert.True(Reports(sheet, "TRAIT_ABOVE_CAP"));

        var expensive = Legal();
        expensive.SelectedTierId = "stanadrd";
        foreach (var a in _f.Rules.Abilities) expensive.AbilityRanks[a.Id] = 12;
        foreach (var t in _f.Rules.Talents) expensive.TalentRanks[t.Id] = 12;

        Assert.False(Reports(expensive, "HP_BUDGET_EXCEEDED"));
        expensive.SelectedTierId = "standard";
        Assert.True(Reports(expensive, "HP_BUDGET_EXCEEDED"));
    }

    /// <summary>
    /// A package that is not one of the three grants nothing and costs nothing, so it was
    /// silently no package — and the character paid full rate for the ranks it would have
    /// covered, which is where the published Heroes were found to be 4 HP out once before.
    /// </summary>
    [Fact]
    public void AnUnknownStartingPackageIsReported()
    {
        var sheet = Legal();
        sheet.SelectedPackageId = "hero";       // the id is hero_package

        var issue = Issue(sheet, "UNKNOWN_PACKAGE");

        Assert.Equal(_f.Rules.CreationRules.OptionalPackages.Select(p => p.Id).Order(),
                     issue.Options.Order());

        foreach (var package in issue.Options)
        {
            var repaired = Legal();
            repaired.SelectedPackageId = package;
            Assert.False(Reports(repaired, "UNKNOWN_PACKAGE"));
        }
    }

    /// <summary>
    /// Ranks bought against a Trait that does not exist. The character was charged for them
    /// and did not have them: the total counted the ranks, the Trait Cap check saw them, and
    /// the sheet printed nothing. It cost Hero Points for a Trait that is not in the game.
    ///
    /// <para>Found through the skill's own example, which named a Talent the rulebook does not
    /// have and produced a clean report.</para>
    /// </summary>
    [Theory]
    [InlineData("athletics", "UNKNOWN_TALENT", false)]
    [InlineData("strength", "UNKNOWN_ABILITY", true)]
    public void RanksAgainstATraitThatDoesNotExistAreReported(string id, string code, bool ability)
    {
        var sheet = Legal();
        if (ability) sheet.AbilityRanks[id] = 4; else sheet.TalentRanks[id] = 4;

        var issue = Issue(sheet, code);

        Assert.Equal(id, issue.SubjectId);
        Assert.NotEmpty(issue.Options);
        Assert.False(_f.Validator.Validate(sheet).IsValid);

        // Every option is a Trait of the right kind, so a caller repairing by choosing one
        // cannot land in the other dictionary.
        Assert.All(issue.Options, o => Assert.NotNull(
            ability ? _f.Rules.GetAbility(o)?.Id : _f.Rules.GetTalent(o)?.Id));
    }

    /// <summary>
    /// <b>The cheapest character in the game.</b> Nothing rejected the same Con listed twice,
    /// and every cost floors at zero, so three Burnouts cancelled a 12d Ability exactly: six
    /// Abilities at the Trait Cap for 0 Hero Points, reported legal with an empty issue list.
    /// Scaled up with Powers it bought 216 HP of character inside a 125 HP budget.
    /// </summary>
    [Fact]
    public void TheSameConTwiceIsRefusedRatherThanDiscountedTwice()
    {
        var sheet = Legal();
        foreach (var ability in _f.Rules.Abilities)
        {
            sheet.AbilityRanks[ability.Id] = 12;
            sheet.AbilityModifiers[ability.Id] = [new("burnout"), new("burnout"), new("burnout")];
        }

        Assert.True(Reports(sheet, "DUPLICATE_CON"));
        Assert.False(_f.Validator.Validate(sheet).IsValid);

        // And with the duplicates gone the character is priced, not free.
        var honest = Legal();
        foreach (var ability in _f.Rules.Abilities) honest.AbilityRanks[ability.Id] = 12;

        Assert.True(_f.Costs.TotalCost(honest) > 0);
        Assert.False(Reports(honest, "DUPLICATE_CON"));
    }

    /// <summary>
    /// <b>The other half of that rule, which was missing and made a published Hero
    /// illegal.</b> Three options are bought again rather than repeated by mistake, and each
    /// says so in its own entry. Blastwave (Ch.8 p.129) prints six energy types on Energy
    /// Absorption, so five copies of Also X — "You can absorb one extra type of energy …
    /// each time you select this Pro" (Ch.2 p.28). The calculator charged all five, which is
    /// exactly what lands him on his printed 125, while the validator returned four errors
    /// on him.
    ///
    /// <para>The repeat is charged, not merely tolerated: a test that only checked the
    /// finding had gone would pass just as well if the copies had become free.</para>
    /// </summary>
    [Fact]
    public void AProTheRulebookRepeatsIsChargedAgainRatherThanRefused()
    {
        var once = Legal();
        once.SelectedPowers.Add(new SelectedPower("energy_absorption", 6,
            [new SelectedProCon("also_x")], []) { SourceId = "super", CostVariantKey = "kinetic" });

        var twice = Legal();
        twice.SelectedPowers.Add(new SelectedPower("energy_absorption", 6,
            [new SelectedProCon("also_x"), new SelectedProCon("also_x")], [])
        { SourceId = "super", CostVariantKey = "kinetic" });

        Assert.False(Reports(twice, "DUPLICATE_PRO"));

        // Also X is +2 Hero Points a copy, and the second one is paid for.
        Assert.Equal(_f.Costs.PowerCost(once.SelectedPowers[^1]) + 2,
                     _f.Costs.PowerCost(twice.SelectedPowers[^1]));

        // Narrow: a Pro the rulebook does not repeat is still refused on the same Power.
        var repeated = Legal();
        repeated.SelectedPowers.Add(new SelectedPower("energy_absorption", 6,
            [new SelectedProCon("armor_piercing"), new SelectedProCon("armor_piercing")], [])
        { SourceId = "super", CostVariantKey = "kinetic" });

        Assert.True(Reports(repeated, "DUPLICATE_PRO"));

        // <b>And narrow on the Power-specific branch, which is a different lookup.</b> The
        // check above uses a generic Pro, so it never reaches the branch that reads a Power's
        // own entry — loosening that branch to "any Power-specific option repeats" left the
        // whole suite green while three copies of Flight's Levitation Con stacked into a
        // character 2 Hero Points cheaper with an empty error list. That is the "three
        // Burnouts cancelled a 12d Ability" hole again, one lookup over.
        Assert.False(_f.Rules.GetPower("flight")!.PowerCons.Single(c => c.Id == "levitation").Repeatable);

        var stacked = Legal();
        stacked.SelectedPowers.Add(new SelectedPower("flight", 12, [],
            [new SelectedProCon("levitation"), new SelectedProCon("levitation")])
        { SourceId = "super" });

        Assert.True(Reports(stacked, "DUPLICATE_CON"));
    }

    /// <summary>
    /// <b>The generic half of the same rule, which nothing exercised.</b> Affect Inanimate is
    /// the case the rulebook states generically — "You can apply this Pro multiple times to
    /// affect different types of inanimate beings" (Ch.2 p.48) — and it resolves through a
    /// different branch from Also X, which is printed inside a Power's own entry.
    ///
    /// <para>Stubbing that branch to <c>return false</c> left all 3391 tests green, because
    /// the only repeat test used a Power-specific option. The picker shows Affect Inanimate's
    /// own description saying it may be applied multiple times, so the bug it hid was the
    /// tool refusing what it had just told the player to do.</para>
    /// </summary>
    [Fact]
    public void AGenericProTheRulebookRepeatsIsChargedAgainRatherThanRefused()
    {
        Assert.True(_f.Rules.GetPro("affect_inanimate")!.Repeatable);

        var once = Legal();
        once.SelectedPowers.Add(new SelectedPower("blast", 6,
            [new SelectedProCon("affect_inanimate")], []) { SourceId = "tech" });

        var twice = Legal();
        twice.SelectedPowers.Add(new SelectedPower("blast", 6,
            [new SelectedProCon("affect_inanimate"), new SelectedProCon("affect_inanimate")], [])
        { SourceId = "tech" });

        Assert.False(Reports(twice, "DUPLICATE_PRO"));

        // +1 Hero Point a copy, and the second one is paid for. Asserting the cost as well as
        // the finding matters: a fix that made the copies free would satisfy the finding alone.
        Assert.Equal(_f.Costs.PowerCost(once.SelectedPowers[^1]) + 1,
                     _f.Costs.PowerCost(twice.SelectedPowers[^1]));
    }

    /// <summary>
    /// <b>The validator asks the engine which grades a Power may pick, and that wiring needs
    /// its own test.</b> Force Field is Self range and reaches the Zone Pro through its own
    /// printed text; the Pro is priced by the base Power's Range, +2 from Ranged and +4 from
    /// Touch, and the rulebook prices no Self figure — so both were accepted and T-Kay's
    /// printed character costed two ways depending on which key was typed.
    ///
    /// <para>The published Heroes cover the accept path only: T-Kay is recorded with the
    /// Ranged grade, so <c>EveryPublishedHeroIsALegalCharacter</c> never sees the refusal, and
    /// reverting the validator to the option's full grade list left the whole suite green.</para>
    /// </summary>
    [Fact]
    public void ASelfPowerIsRefusedTheTouchGradeOfAProItsOwnTextAllows()
    {
        static CharacterSheet WithZone(CharacterSheet sheet, string grade)
        {
            sheet.SelectedPowers.Add(new SelectedPower("force_field", 6,
                [new SelectedProCon("zone_nova", grade)], []) { SourceId = "super" });
            return sheet;
        }

        var refused = Issue(WithZone(Legal(), "zone_touch"), "PRO_VARIANT_NOT_CHOSEN");

        Assert.Equal("zone_nova", refused.SubjectId);
        Assert.Equal("force_field", refused.OwnerId);

        // The repair is named, and names only the grades this Power may take.
        Assert.Equal(["zone_ranged", "nova_ranged"], refused.Options);

        // And the grade it may take is clean, so this is a narrowing rather than a refusal
        // of the Pro the rulebook prints on T-Kay.
        Assert.False(Reports(WithZone(Legal(), "zone_ranged"), "PRO_VARIANT_NOT_CHOSEN"));
        Assert.False(Reports(WithZone(Legal(), "zone_ranged"), "PRO_NOT_APPLICABLE"));
    }

    /// <summary>The same trap on a Power, where the floor is per Power rather than per Trait.</summary>
    [Fact]
    public void TheSameConTwiceOnAPowerIsRefused()
    {
        var sheet = Legal();
        sheet.SelectedPowers.Add(new SelectedPower("blast", 12,
            [], [new SelectedProCon("burnout"), new SelectedProCon("burnout")])
        { SourceId = "tech" });

        var issue = Issue(sheet, "DUPLICATE_CON");
        Assert.Equal("burnout", issue.SubjectId);

        // The Power's id, not its printed name: the owner is there to be looked up.
        Assert.Equal("blast", issue.OwnerId);
    }

    /// <summary>
    /// A flaw taken twice counted twice: twice against the maximum of three, and twice into
    /// Resolve for a Condition or Plot Hook, so three copies of one flaw were worth 21 Resolve
    /// where one is worth 19.
    /// </summary>
    [Fact]
    public void TheSameFlawTwiceIsRefused()
    {
        var sheet = RulesFixture.StandardSheet();
        sheet.AbilityRanks["might"] = 3;
        sheet.Flaws.Add(new SelectedFlaw("enemy"));
        sheet.Flaws.Add(new SelectedFlaw("enemy"));

        var issue = Issue(sheet, "DUPLICATE_FLAW");

        Assert.Equal(ValidationSubject.Flaw, issue.SubjectKind);
        Assert.Equal("enemy", issue.SubjectId);
        Assert.False(_f.Validator.Validate(sheet).IsValid);

        // One copy is the ordinary case, and its Resolve is the figure the duplicate inflated.
        var single = RulesFixture.StandardSheet();
        single.AbilityRanks["might"] = 3;
        single.Flaws.Add(new SelectedFlaw("enemy"));

        Assert.False(Reports(single, "DUPLICATE_FLAW"));
        Assert.True(_f.Derived.CalculateResolve(sheet) > _f.Derived.CalculateResolve(single));
    }

    /// <summary>
    /// <b>A Power whose own entry says to buy it again is not "listed twice".</b> Ch.2 p.21:
    /// "Buy this Power multiple times if you want multiple forms"; p.27: "You can buy this
    /// Power multiple times if you want to be able to create multiple duplicates". Found by the
    /// alternate-form slice, whose own remedy for a second form — every member pays the Power
    /// again — drew a <c>DUPLICATE_POWER</c> warning on every member's own report for doing
    /// what the book says. The mark is <c>repeatable</c> on the entry, the shape an option
    /// that may be bought again already uses; the test below holds it to the printed text.
    /// </summary>
    [Theory]
    [InlineData("alternate_form")]
    [InlineData("duplication")]
    public void APowerWhoseEntrySaysToBuyItAgainIsNotADuplicate(string powerId)
    {
        var sheet = Legal();
        sheet.SelectedPowers.Add(new SelectedPower(powerId, 0) { SourceId = "tech", Units = 3 });
        sheet.SelectedPowers.Add(new SelectedPower(powerId, 0) { SourceId = "tech", Units = 3 });

        Assert.True(_f.Rules.GetPower(powerId)!.Repeatable);
        Assert.False(Reports(sheet, "DUPLICATE_POWER"));

        // Both are still charged for: the mark changes the warning, not the arithmetic.
        Assert.Equal(2 * _f.Costs.PowerCost(sheet.SelectedPowers[^1]),
            _f.Costs.TotalPowersCost(sheet) - _f.Costs.TotalPowersCost(Legal()));
    }

    /// <summary>
    /// The <c>repeatable</c> mark on a Power is a record of printed text and nothing else: the
    /// entries marked are exactly the ones whose Ch.2 text says "buy this Power multiple
    /// times". Dazzle's "attacking the same target multiple times" is not that sentence.
    /// </summary>
    [Fact]
    public void ThePowersMarkedRepeatableAreExactlyThoseWhoseEntrySaysToBuyThemAgain()
    {
        using var chapter = System.Text.Json.JsonDocument.Parse(File.ReadAllText(
            Path.Combine(RulesFixture.RepoRoot, "data", "rulebook", "ch02-characters.json")));
        var says = new Regex(@"buy this Power multiple times", RegexOptions.IgnoreCase, TimeSpan.FromSeconds(5));

        var printed = chapter.RootElement.GetProperty("sections").EnumerateArray()
            .Where(s => says.IsMatch(s.GetProperty("text").GetString() ?? ""))
            .Select(s => s.GetProperty("heading").GetString()!)
            .Order()
            .ToList();

        var marked = _f.Rules.Powers.Where(p => p.Repeatable)
            .Select(p => p.Name.ToUpperInvariant())
            .Order()
            .ToList();

        Assert.Equal(["ALTERNATE FORM", "DUPLICATION"], printed);
        Assert.Equal(printed, marked);
    }

    /// <summary>
    /// A Power listed twice is a warning, not an error: the rulebook does not forbid it, and
    /// two Blasts with different Pros is a shape a player might want. What is certainly wrong
    /// is that the budget charges for both while the sheet shows the first.
    /// </summary>
    [Fact]
    public void TheSamePowerTwiceIsAWarningRatherThanARefusal()
    {
        var sheet = Legal();
        sheet.SelectedPowers.Add(new SelectedPower("blast", 2) { SourceId = "tech" });
        sheet.SelectedPowers.Add(new SelectedPower("blast", 4) { SourceId = "tech" });

        var issue = Issue(sheet, "DUPLICATE_POWER");

        Assert.Equal(ValidationSeverity.Warning, issue.Severity);
        Assert.True(_f.Validator.Validate(sheet).IsValid);
        Assert.Equal("blast", issue.SubjectId);
    }

    /// <summary>
    /// <b>The Brute Option is Overkill on Might.</b> The Ability was not checked, so Overkill
    /// or Weak on any of the six halved it — 12d Intellect for 6 HP, legal, with one Con on it.
    /// Ch.2 p.17 names Might and nothing else.
    /// </summary>
    [Fact]
    public void OnlyMightIsHalvedByTheBruteOption()
    {
        // Compared against each other rather than against a constant, because a legal sheet now
        // carries all eighteen Traits at 1d and both totals include that floor.
        var might = Legal();
        might.AbilityRanks["might"] = 12;
        might.AbilityModifiers["might"] = [new("overkill")];

        var intellect = Legal();
        intellect.AbilityRanks["intellect"] = 12;
        intellect.AbilityModifiers["intellect"] = [new("overkill")];

        var plain = Legal();
        plain.AbilityRanks["might"] = 12;

        // Might is halved: 12 ranks become 6, so it saves 6 against the undiscounted version.
        Assert.Equal(_f.Costs.AbilityCost(plain) - 6, _f.Costs.AbilityCost(might));

        // Intellect is not: Overkill on it is flat, and Overkill's flat value is nothing.
        var plainIntellect = Legal();
        plainIntellect.AbilityRanks["intellect"] = 12;

        Assert.Equal(_f.Costs.AbilityCost(plainIntellect), _f.Costs.AbilityCost(intellect));
    }

    /// <summary>
    /// A total that wraps is not a total. Each component sums checked, but the additions
    /// between them did not, so a large enough character came to a negative number of Hero
    /// Points and the budget check passed in silence.
    /// </summary>
    [Fact]
    public void ATotalThatWouldOverflowIsReportedRatherThanWrapped()
    {
        var sheet = Legal();
        sheet.SelectedPowers.Add(new SelectedPower("determination", 0)
        { Units = 400_000_000, SourceId = "innate" });
        sheet.Perks.Add(new SelectedPerk("contacts", 200_000_000));

        var result = _f.Validator.Validate(sheet);

        Assert.False(result.IsValid);
        Assert.Contains(result.Issues,
            i => i.Code is "CHARACTER_NOT_PRICEABLE" or "HP_BUDGET_EXCEEDED");
        Assert.Throws<OverflowException>(() => _f.Costs.TotalCost(sheet));
    }

    /// <summary>
    /// Ranks against a Trait the rulebook does not have are reported and <b>not charged for</b>.
    /// The total used to include them, so the report printed a price covering a Trait the
    /// character could not possibly have, beside the error saying it does not exist.
    /// </summary>
    [Fact]
    public void AnUnknownTraitIsNotChargedFor()
    {
        var sheet = Legal();
        sheet.AbilityRanks["might"]    = 4;
        sheet.TalentRanks["athletics"] = 3;
        sheet.AbilityRanks["strength"] = 5;

        var honest = Legal();
        honest.AbilityRanks["might"] = 4;

        Assert.Equal(_f.Costs.TotalCost(honest), _f.Costs.TotalCost(sheet));
        Assert.True(Reports(sheet, "UNKNOWN_TALENT"));
        Assert.True(Reports(sheet, "UNKNOWN_ABILITY"));
    }

    /// <summary>
    /// <b>A Power's own Pro or Con needs its grade checked too.</b> The branch that resolves a
    /// Pro or Con printed inside a Power's entry skipped the grade check and went straight on,
    /// so Drain carrying its own ungraded Only X threw out of the validator — the eleventh
    /// shape of character to do that, on the one branch the tenth fix did not cover.
    /// </summary>
    [Theory]
    [InlineData(null)]
    [InlineData("nope")]
    public void APowerSpecificConPricedByGradeIsCheckedLikeAnyOther(string? key)
    {
        var sheet = Legal();
        sheet.SelectedPowers.Add(new SelectedPower("drain", 6,
            [], [new SelectedProCon("only_x", key)]) { SourceId = "magic" });

        var issue = Issue(sheet, "CON_VARIANT_NOT_CHOSEN");
        Assert.NotEmpty(issue.Options);

        foreach (var grade in issue.Options)
        {
            var repaired = Legal();
            repaired.SelectedPowers.Add(new SelectedPower("drain", 6,
                [], [new SelectedProCon("only_x", grade)]) { SourceId = "magic" });

            Assert.False(Reports(repaired, "CON_VARIANT_NOT_CHOSEN"));
            Assert.True(_f.Costs.TotalCost(repaired) > 0);
        }
    }

    /// <summary>
    /// A quantity on a Pro or Con was the one of the five that nothing bounded, and it buys a
    /// discount: a per-rank-per-unit Pro at −1000 drove a Power's rate to −498, which the
    /// rulebook floor caught at half a point per rank, so a 24 HP Power cost 6 in silence.
    /// </summary>
    [Fact]
    public void ANegativeQuantityOnAProIsRefused()
    {
        var sheet = Legal();
        sheet.SelectedPowers.Add(new SelectedPower("nullify", 12,
            [new SelectedProCon("also_x") { Units = -1000 }], []) { SourceId = "magic" });

        var issue = Issue(sheet, "NEGATIVE_UNITS");

        Assert.Equal("also_x", issue.SubjectId);
        Assert.Equal(-1000, issue.Value);
        Assert.False(_f.Validator.Validate(sheet).IsValid);
    }

    /// <summary>
    /// <b>Five of the six quantity-carrying fields had no test that isolated their own branch.</b>
    /// An adversarial mutation audit weakened <c>&lt; 0</c> to <c>&lt; -1000</c> on the Power
    /// purchased-ranks branch of <c>CheckQuantities</c>, and separately on the Perk-Units branch,
    /// and the whole suite stayed green. The reason is <c>Build("negative quantities")</c>: one
    /// sheet carrying all six fields negative at once, so a shared meta-check ("some
    /// <c>NEGATIVE_RANK</c>/<c>NEGATIVE_UNITS</c> issue exists somewhere on this sheet") was
    /// satisfied by one of the *other* five sources every time, whichever branch the mutation
    /// disabled. Each test below puts exactly one field negative on an otherwise-legal sheet, so
    /// nothing else on the character can supply the finding it asserts on.
    /// </summary>
    [Fact]
    public void ANegativeAbilityRankIsRefused()
    {
        var sheet = Legal();
        sheet.AbilityRanks["might"] = -5;

        var issue = Issue(sheet, "NEGATIVE_RANK");

        Assert.Equal(ValidationSubject.Ability, issue.SubjectKind);
        Assert.Equal("might", issue.SubjectId);
        Assert.Equal(-5, issue.Value);
        Assert.False(_f.Validator.Validate(sheet).IsValid);
    }

    [Fact]
    public void ANegativeTalentRankIsRefused()
    {
        var sheet = Legal();
        sheet.TalentRanks["academics"] = -3;

        var issue = Issue(sheet, "NEGATIVE_RANK");

        Assert.Equal(ValidationSubject.Talent, issue.SubjectKind);
        Assert.Equal("academics", issue.SubjectId);
        Assert.Equal(-3, issue.Value);
        Assert.False(_f.Validator.Validate(sheet).IsValid);
    }

    /// <summary>
    /// A Power's purchased ranks. <c>PowerCost</c> floors a per-rank Power's minimum at 0 once
    /// its ranks are non-positive, so a negative rank does not pay Hero Points back — it costs
    /// exactly 0, silently, which is not a refusal. The guard, not the price, is what keeps a
    /// character carrying a nonsensical negative Power rank off a legal sheet.
    /// </summary>
    [Fact]
    public void ANegativePowerPurchasedRanksIsRefused()
    {
        var sheet     = Legal();
        var selection = new SelectedPower("blast", -4) { SourceId = "tech" };
        sheet.SelectedPowers.Add(selection);

        var issue = Issue(sheet, "NEGATIVE_RANK");

        Assert.Equal(ValidationSubject.Power, issue.SubjectKind);
        Assert.Equal("blast", issue.SubjectId);
        Assert.Equal(-4, issue.Value);

        // The positive control for "the total cost is not silently reduced": the raw calculator
        // really does answer 0 for this, not a refusal and not a negative number, so the guard
        // is the only thing standing between this and a legal, free Power.
        Assert.Equal(0, _f.Costs.PowerCost(selection));
        Assert.False(_f.Validator.Validate(sheet).IsValid);
    }

    /// <summary>
    /// A Power's <c>Units</c>, on a Power priced by the unit. Every Power floors at 1 HP
    /// regardless of Cons (<c>CostParts.Fixed</c>), so a negative quantity here does not pay
    /// Hero Points either — it silently costs the floor, hiding that the number recorded makes
    /// no sense rather than refusing it.
    /// </summary>
    [Fact]
    public void ANegativePowerUnitsIsRefused()
    {
        var sheet     = Legal();
        var selection = new SelectedPower("immunity", 0) { Units = -20, SourceId = "tech" };
        sheet.SelectedPowers.Add(selection);

        var issue = Issue(sheet, "NEGATIVE_UNITS");

        Assert.Equal(ValidationSubject.Power, issue.SubjectKind);
        Assert.Equal("immunity", issue.SubjectId);
        Assert.Equal(-20, issue.Value);

        Assert.Equal(1, _f.Costs.PowerCost(selection));
        Assert.False(_f.Validator.Validate(sheet).IsValid);
    }

    /// <summary>
    /// A Perk's <c>Units</c> is the one quantity with no floor underneath it at all —
    /// <c>PerkCost</c> multiplies straight through with no <c>Math.Max</c> beneath it. This is
    /// the harm CLAUDE.md records by name: unguarded, a negative quantity here really does pay
    /// the character Hero Points, which can report an over-budget character legal at exit 0.
    /// </summary>
    [Fact]
    public void ANegativePerkUnitsIsRefused()
    {
        var sheet = Legal();
        var perk  = new SelectedPerk("contacts", -1000);
        sheet.Perks.Add(perk);

        var issue = Issue(sheet, "NEGATIVE_UNITS");

        Assert.Equal(ValidationSubject.Character, issue.SubjectKind);
        Assert.Equal("contacts", issue.SubjectId);
        Assert.Equal(-1000, issue.Value);

        // The positive control: unguarded, this really would pay the character 1,000 Hero
        // Points rather than cost them — the total is not silently reduced, it is silently
        // reversed, and the guard is what stops that reaching a legal sheet.
        Assert.True(_f.Costs.PerkCost(perk) < 0);
        Assert.False(_f.Validator.Validate(sheet).IsValid);
    }

    /// <summary>
    /// A quantity large enough to wrap the multiplication. Making the grand total checked was
    /// not enough — the wrap happened in the per-unit multiplication underneath it, so
    /// Determination at 500,000,000 units cost 5 HP and gave 500,000,016 Resolve at exit 0.
    /// </summary>
    [Theory]
    [InlineData("determination")]
    [InlineData("alternate_form")]
    public void AQuantityLargeEnoughToWrapACostIsReported(string powerId)
    {
        var sheet = Legal();
        sheet.SelectedPowers.Add(new SelectedPower(powerId, 0)
        { Units = 600_000_000, SourceId = "innate" });

        Assert.False(_f.Validator.Validate(sheet).IsValid);
        Assert.Throws<OverflowException>(() => _f.Costs.TotalCost(sheet));
    }

    /// <summary>
    /// Zero of a thing priced by the unit is not a purchase: it costs nothing and does nothing,
    /// so it is a line on the sheet the character did not buy.
    /// </summary>
    [Fact]
    public void APerUnitPurchaseOfNothingIsReported()
    {
        var sheet = Legal();
        sheet.Perks.Add(new SelectedPerk("contacts", 0));

        var issue = Issue(sheet, "PER_UNIT_WITHOUT_UNITS");

        Assert.Equal("contacts", issue.SubjectId);
        Assert.Equal(0, issue.Value);
        Assert.Equal(1, issue.Limit);

        // A flat perk is not affected: Units means nothing to one.
        var flat = Legal();
        flat.Perks.Add(new SelectedPerk("fame"));
        Assert.False(Reports(flat, "PER_UNIT_WITHOUT_UNITS"));
    }

    /// <summary>
    /// <b>Exactly on budget is legal.</b> The budget check is a strict comparison, and turning it
    /// into <c>&gt;=</c> reported every character that spends its last Hero Point as over budget —
    /// with the whole suite green, including the twenty published Heroes, because those assert
    /// their totals and never their legality. Fifteen of them are on exactly 125.
    /// </summary>
    [Theory]
    [InlineData(0, false)]      // exactly the budget
    [InlineData(-1, false)]     // a point under
    [InlineData(1, true)]       // a point over
    public void TheBudgetIsBreachedOnlyByGoingOverIt(int offset, bool shouldReport)
    {
        var tier  = _f.Rules.GetTier("standard")!;
        var sheet = Legal();

        // A legal sheet already carries all eighteen Traits at 1d, so the twelve Talents are
        // 12 HP of the total before anything is bought. Abilities cost 1 HP per rank with no
        // package, so the rest of the budget goes into Might.
        var wanted = tier.HeroPoints + offset;
        var floor  = _f.Costs.TotalCost(sheet);

        sheet.AbilityRanks["might"] = 1 + (wanted - floor);

        Assert.Equal(wanted, _f.Costs.TotalCost(sheet));
        Assert.Equal(shouldReport, Reports(sheet, "HP_BUDGET_EXCEEDED"));
    }

    /// <summary>
    /// <b>The backstop must stay unreachable.</b> <c>CHARACTER_NOT_PRICEABLE</c> is the
    /// <c>catch</c> inside the budget check, and its whole value is that every gap which used to
    /// reach it is now reported by name. Without this, a check could stop reporting its own
    /// finding and the backstop would quietly cover for it — which is exactly what happened when
    /// two resolvability flags were made to lie: the tests stayed green because *something* was
    /// reported, just not the thing that names the fault.
    /// </summary>
    [Theory]
    [MemberData(nameof(Cases))]
    public void TheUnpriceableBackstopIsNeverWhatAnswers(string which)
    {
        var codes = IssuesFor(which).Select(i => i.Code).ToList();

        Assert.DoesNotContain("CHARACTER_NOT_PRICEABLE", codes);
    }

    /// <summary>
    /// <b>Which kind of thing each code is about, pinned per code.</b> The two invariants below
    /// assert that a subject is present and named; neither asks whether the kind is <em>right</em>,
    /// so labelling a Talent problem as an Ability walked through both — and a repair loop
    /// following it writes into the wrong dictionary, which is the failure one test already
    /// guards for one code. This is that guard for all of them.
    /// </summary>
    [Theory]
    [MemberData(nameof(Cases))]
    public void EachCodeReportsTheKindOfThingItIsAbout(string which)
    {
        foreach (var issue in IssuesFor(which))
        {
            // <b>Not TryGetValue-and-continue.</b> That is what this test used to do, and it made
            // the table's omissions into exemptions: eighteen of the validator's forty-five codes
            // were absent, so relabelling DUPLICATE_PRO as an Ability problem — verbatim the
            // failure this exists to prevent — walked straight through. A table that has to be
            // exhaustive must fail on a code it does not mention, which is what
            // <see cref="TheKindOfEveryCodeIsWrittenDown"/> asserts and this now relies on.
            Assert.Contains(issue.SubjectKind, ExpectedKinds[issue.Code]);
        }
    }

    /// <summary>
    /// <b>The table above is only worth the codes it names.</b> Nothing held it to the validator,
    /// so a code added later was silently exempt from the kind check — and eighteen already were.
    /// The same guarantee <see cref="EveryCodeTheValidatorCanReportIsProvokedBySomeCase"/> gives
    /// for the case list, given for this table: adding a code to the validator fails here until
    /// somebody writes down what it is about.
    /// </summary>
    [Fact]
    public void TheKindOfEveryCodeIsWrittenDown()
    {
        var missing = DeclaredCodes(EngineSource).Except(ExpectedKinds.Keys, StringComparer.Ordinal)
            .Order(StringComparer.Ordinal)
            .ToList();

        Assert.True(missing.Count == 0,
            "The validator can report these codes and ExpectedKinds does not say what kind of "
            + "thing they are about: " + string.Join(", ", missing));

        // And the other way, so a code that has been removed does not leave a line here claiming
        // to guard something.
        var stale = ExpectedKinds.Keys.Except(DeclaredCodes(EngineSource), StringComparer.Ordinal)
            .Order(StringComparer.Ordinal)
            .ToList();

        Assert.True(stale.Count == 0,
            "ExpectedKinds names codes the validator no longer has: " + string.Join(", ", stale));
    }

    /// <summary>
    /// The kind each code must carry. Deliberately a table rather than a rule: the point is that
    /// somebody wrote down what each one is about, so a change to one of them has to disagree
    /// with a line here rather than with nothing.
    ///
    /// <para><c>Character</c> on the Pro and Con findings is not an oversight. A Pro sits on a
    /// Power, a piece of gear or an Ability, and the thing it sits on is named by
    /// <c>OwnerId</c> — so the subject kind is the sheet, and <c>SubjectId</c> is the option.
    /// Calling one an Ability problem would send a repair loop into <c>AbilityRanks</c>.</para>
    /// </summary>
    private static readonly Dictionary<string, ValidationSubject[]> ExpectedKinds = new(StringComparer.Ordinal)
    {
        ["NO_TIER_SELECTED"]                = [ValidationSubject.Character],
        ["UNKNOWN_TIER"]                    = [ValidationSubject.Tier],
        ["ICONIC_TIER_OPEN_BUDGET"]         = [ValidationSubject.Tier],
        ["UNKNOWN_PACKAGE"]                 = [ValidationSubject.Character],
        ["HP_BUDGET_EXCEEDED"]              = [ValidationSubject.Character],

        // The house Trait Cap is a field on the character rather than on any one Trait, so
        // both findings about the cap itself are about the sheet. The findings about Traits
        // measured against it are TRAIT_ABOVE_CAP, further down, and carry the Trait's kind.
        ["TRAIT_CAP_ABOVE_TIER"]            = [ValidationSubject.Character],
        ["TRAIT_CAP_BELOW_MINIMUM"]         = [ValidationSubject.Character],

        // A table's price for Immortality is about the Power whose own entry hands the price to
        // the table, and not about the sheet — unlike the cap above it, which is a ceiling over
        // every Trait. A caller repairing it has one entry to look at, so it names it.
        ["IMMORTALITY_COST_OUTSIDE_RANGE"]  = [ValidationSubject.Power],
        ["CHARACTER_NOT_PRICEABLE"]         = [ValidationSubject.Character],
        ["FLAW_MIN_NOT_MET"]                = [ValidationSubject.Character],
        ["FLAW_MAX_EXCEEDED"]               = [ValidationSubject.Character],
        ["UNKNOWN_FLAW"]                    = [ValidationSubject.Flaw],
        ["DUPLICATE_FLAW"]                  = [ValidationSubject.Flaw],
        ["UNKNOWN_ABILITY"]                 = [ValidationSubject.Ability],
        ["UNKNOWN_TALENT"]                  = [ValidationSubject.Talent],
        ["MODIFIER_ON_UNBOUGHT_ABILITY"]    = [ValidationSubject.Ability],
        ["UNKNOWN_POWER"]                   = [ValidationSubject.Power],

        // PROGRESS.md item 36: a Gadget's Powers are ordinary Powers one budget down (p.94 buys
        // them "under the ordinary rules"), so each of these six is filed against the Gadget by
        // name — the same shape UNKNOWN_GADGET_POWER already takes below — when the Power that
        // triggers it sits inside one rather than on the character's own sheet.
        ["DUPLICATE_POWER"]                 = [ValidationSubject.Power, ValidationSubject.Gadget],
        ["POWER_HAS_NO_RANK"]               = [ValidationSubject.Power, ValidationSubject.Gadget],
        ["POWER_VARIANT_NOT_CHOSEN"]        = [ValidationSubject.Power],
        ["POWER_BASELINE_TRAIT_NOT_CHOSEN"] = [ValidationSubject.Power],

        // The Expertise, not the Power it was wrongly nominated to: the repair is rewriting
        // BaselineTraitId on this purchase, and naming the nominated Power would send a repair
        // loop looking for a Power to change.
        ["EXPERTISE_NOMINATION_NOT_A_TRAIT"] = [ValidationSubject.Power],

        ["POWER_COST_AT_MINIMUM"]           = [ValidationSubject.Power, ValidationSubject.Gadget],
        ["POWER_WITHOUT_SOURCE"]            = [ValidationSubject.Power, ValidationSubject.Gadget],
        ["RANKLESS_POWER_WITHOUT_SOURCE"]   = [ValidationSubject.Power, ValidationSubject.Gadget],
        ["POWER_UNIT_NAMES_BELOW_UNITS"]    = [ValidationSubject.Power, ValidationSubject.Gadget],
        ["POWER_UNIT_NAMES_EXCEED_UNITS"]   = [ValidationSubject.Power, ValidationSubject.Gadget],
        ["POWER_MECHANICS_UNVERIFIED"]      = [ValidationSubject.Power, ValidationSubject.Gadget],

        // The one finding with no subject at all, and deliberately: it names every Power whose
        // wording is unverified in a single sentence, so there is no one thing it is about.
        ["POWER_DESCRIPTION_UNVERIFIED"]    = [ValidationSubject.None],

        ["UNKNOWN_GEAR_FEATURE"]            = [ValidationSubject.GearFeature],
        ["GEAR_FEATURE_NEEDS_GRADE"]        = [ValidationSubject.GearFeature],
        ["GEAR_COST_AT_MINIMUM"]            = [ValidationSubject.Gear],

        // The item, because that is the thing whose id is wrong and the thing a screen can put
        // in front of somebody. There is no Options list: the right row out of 108 is a question
        // about what the character carries, not a value to pick — see CheckGear.
        ["UNKNOWN_GEAR_CATALOGUE_ROW"]      = [ValidationSubject.Gear],
        ["TWO_FISTED_PAIR_WITHOUT_POWER"]   = [ValidationSubject.Gear],
        ["UNKNOWN_PERK"]                    = [ValidationSubject.Character],
        ["GEAR_WITHOUT_NAME"]               = [ValidationSubject.Character],

        // Also the feature, since Chapter 6's two feature tables price by the unit as well. The
        // three kinds are the three collections a repair loop would write into.
        ["PER_UNIT_WITHOUT_UNITS"]          =
            [ValidationSubject.Character, ValidationSubject.Power, ValidationSubject.AssetFeature],

        // ── Chapter 6's vehicles, headquarters and Gadgets ────────────────────
        //
        // A machine, a base or a Gadget is identified by its name, exactly as gear is, so a
        // nameless one is filed against the character: there is no subject a message could name.
        ["VEHICLE_WITHOUT_NAME"]            = [ValidationSubject.Character],
        ["HEADQUARTERS_WITHOUT_NAME"]       = [ValidationSubject.Character],
        ["GADGET_WITHOUT_NAME"]             = [ValidationSubject.Character],

        // The machine, because the budget and both Control rules are facts about the whole of it.
        ["VEHICLE_OVER_BUDGET"]                 = [ValidationSubject.Vehicle],
        ["VEHICLE_CONTROL_ABOVE_HALF_SPEED"]    = [ValidationSubject.Vehicle],
        ["VEHICLE_CONTROL_BELOW_MINIMUM"]       = [ValidationSubject.Vehicle],
        ["HEADQUARTERS_OVER_BUDGET"]            = [ValidationSubject.Headquarters],

        // The feature, with the machine or base in OwnerId — the same shape a gear feature's
        // finding takes, and the reason there is one AssetFeature kind rather than two.
        ["UNKNOWN_ASSET_FEATURE"]           = [ValidationSubject.AssetFeature],
        ["ASSET_FEATURE_NEEDS_GRADE"]       = [ValidationSubject.AssetFeature],
        ["MECHA_MIGHT_BELOW_HALF_BODY"]     = [ValidationSubject.AssetFeature],

        // Ruling 2 (PROGRESS.md item 33): Submersible without Swimming, or Transforming without
        // two of its four movement features. The feature, with the vehicle's name in OwnerId —
        // the same shape MECHA_MIGHT_BELOW_HALF_BODY takes.
        ["VEHICLE_FEATURE_PREREQUISITE_BELOW_MINIMUM"] = [ValidationSubject.AssetFeature],

        ["GADGET_COMPLEXITY_BELOW_MINIMUM"]         = [ValidationSubject.Gadget],
        ["GADGET_COMPLEXITY_ABOVE_TECHNOLOGY"]      = [ValidationSubject.Gadget],
        ["GADGET_BUILDER_BELOW_TECHNOLOGY_MINIMUM"] = [ValidationSubject.Gadget],
        ["GADGET_OVER_POOL"]                        = [ValidationSubject.Gadget],
        ["UNKNOWN_GADGET_POWER"]                    = [ValidationSubject.Gadget],
        ["UNKNOWN_GADGET_ABILITY"]                  = [ValidationSubject.Gadget],
        ["UNKNOWN_GADGET_TALENT"]                   = [ValidationSubject.Gadget],

        // A contribution to a campaign's shared object belongs to the sheet: what the object is
        // belongs to the campaign, and there is no vehicle or base here to name.
        ["CAMPAIGN_ASSET_WITHOUT_ID"]       = [ValidationSubject.Character],
        ["UNKNOWN_CAMPAIGN_ASSET_KIND"]     = [ValidationSubject.Character, ValidationSubject.Vehicle,
                                                 ValidationSubject.Headquarters],
        ["ASSET_PERK_RECORDED_TWICE"]       = [ValidationSubject.Character],

        // Item 21: both are about the character's own Variant field rather than about anything
        // it names — the engine cannot see the roster, so there is no root or child to point a
        // repair at.
        ["VARIANT_WITHOUT_ROOT"]            = [ValidationSubject.Character],
        ["UNKNOWN_VARIANT_KIND"]            = [ValidationSubject.Character],

        // Item 21 slice two: AlternateForms is handed a roster and reports about whole sheets —
        // SubjectId is the roster id of the form or root at fault and OwnerId the root's, which
        // is what a caller with two files open needs to know which one to edit.
        ["ALTERNATE_FORM_ROOT_NOT_IN_ROSTER"] = [ValidationSubject.Character],
        ["ALTERNATE_FORM_NOT_PAID"]           = [ValidationSubject.Character],
        ["ALTERNATE_FORM_COST_DIFFERS"]       = [ValidationSubject.Character],
        ["ALTERNATE_FORM_ABOVE_ROOT_LEVEL"]   = [ValidationSubject.Character],
        ["ALTERNATE_FORM_LEVEL_NOT_PAID"]     = [ValidationSubject.Character],
        ["ALTERNATE_FORM_PAID_NOT_IN_ROSTER"] = [ValidationSubject.Character],
        ["ALTERNATE_FORM_CAP_NOT_ROOTS"]      = [ValidationSubject.Character],

        // Ruling 7: reported against the character when CheckCampaignAssets finds it on a
        // contribution, and against the shared object when CheckSharedAsset finds it while
        // walking every member's contribution to that object.
        ["CAMPAIGN_ASSET_CONTRIBUTION_TOO_LARGE"] = [ValidationSubject.Character,
                                                       ValidationSubject.Vehicle,
                                                       ValidationSubject.Headquarters],

        // Ruling 8: CheckContributionAgainstAsset has only the contribution and the asset in
        // hand, and a contribution belongs to the character the same way its own kind and id do.
        ["CAMPAIGN_ASSET_KIND_MISMATCH"]    = [ValidationSubject.Character],

        // Rulings 5+6: a proposal's Hero Points buy more than the build spends. Filed against the
        // character the same way every other CampaignAssetContribution-shaped code is — there is
        // no vehicle or base here yet, only a player's own sheet.
        ["CAMPAIGN_ASSET_SURPLUS"]          = [ValidationSubject.Character],

        // A Trait or a Power over the cap, under the 1d floor, under its package's floor, or
        // recorded with a negative quantity: the kind says which collection to write into.
        ["TRAIT_ABOVE_CAP"]      = [ValidationSubject.Ability, ValidationSubject.Talent, ValidationSubject.Power],
        ["TRAIT_BELOW_MINIMUM"]  = [ValidationSubject.Ability, ValidationSubject.Talent],
        ["TRAIT_BELOW_PACKAGE"]  = [ValidationSubject.Ability, ValidationSubject.Talent],
        ["NEGATIVE_RANK"]        = [ValidationSubject.Ability, ValidationSubject.Talent, ValidationSubject.Power,

            // …and Chapter 6's two: a vehicle's bought characteristics, and a Gadget's own Trait
            // ranks. Both pay a currency back when they go below zero.
            ValidationSubject.Vehicle, ValidationSubject.Gadget],

        // A quantity, which sits on a Power, a Perk or a Pro — the last two under Character,
        // since a Perk is not one of the kinds and an option belongs to its owner.
        ["NEGATIVE_UNITS"]       = [ValidationSubject.Power, ValidationSubject.Character,

            // A Perk allowance recorded against a machine or a base is the same field as a Perk's
            // Units and pays the character the same way.
            ValidationSubject.Vehicle, ValidationSubject.Headquarters],

        ["UNKNOWN_PRO"]              = [ValidationSubject.Character],
        ["UNKNOWN_CON"]              = [ValidationSubject.Character],
        ["DUPLICATE_PRO"]            = [ValidationSubject.Character],
        ["DUPLICATE_CON"]            = [ValidationSubject.Character],
        ["PRO_NOT_APPLICABLE"]       = [ValidationSubject.Character],
        ["CON_NOT_APPLICABLE"]       = [ValidationSubject.Character],
        ["PRO_VARIANT_NOT_CHOSEN"]   = [ValidationSubject.Character],
        ["CON_VARIANT_NOT_CHOSEN"]   = [ValidationSubject.Character],

        // A Source is wrong on a Power or on a Trait, and the Trait cases carry the Trait's kind.
        // A Power case is a Gadget's own when the Power sits inside one (item 36).
        ["UNKNOWN_SOURCE"]       = [ValidationSubject.Power, ValidationSubject.Gadget,
                                     ValidationSubject.Ability, ValidationSubject.Talent],
        ["UNKNOWN_TRAIT_SOURCE"] = [ValidationSubject.Ability, ValidationSubject.Talent],
    };

    /// <summary>
    /// <b>A code whose fix is a choice must offer the choices.</b>
    /// <see cref="EveryOptionOfferedIsOneTheRulesAccept"/> walks the option list, so emptying one
    /// passes it by having nothing to walk — three codes had their options deleted with the suite
    /// green. This is the other half: these codes are useless without them.
    /// </summary>
    [Theory]
    [MemberData(nameof(Cases))]
    public void ACodeWhoseFixIsAChoiceOffersTheChoices(string which)
    {
        foreach (var issue in IssuesFor(which).Where(i => MustOfferOptions.Contains(i.Code)))
            Assert.NotEmpty(issue.Options);
    }

    private static readonly HashSet<string> MustOfferOptions = new(StringComparer.Ordinal)
    {
        "NO_TIER_SELECTED", "UNKNOWN_TIER", "UNKNOWN_PACKAGE", "UNKNOWN_FLAW", "UNKNOWN_PERK",
        "UNKNOWN_ABILITY", "UNKNOWN_TALENT", "UNKNOWN_GEAR_FEATURE", "GEAR_FEATURE_NEEDS_GRADE",
        "UNKNOWN_SOURCE", "UNKNOWN_TRAIT_SOURCE", "POWER_VARIANT_NOT_CHOSEN",
        "PRO_VARIANT_NOT_CHOSEN", "CON_VARIANT_NOT_CHOSEN", "RANKLESS_POWER_WITHOUT_SOURCE",
        "POWER_WITHOUT_SOURCE", "MODIFIER_ON_UNBOUGHT_ABILITY", "FLAW_MIN_NOT_MET",
        "UNKNOWN_ASSET_FEATURE", "ASSET_FEATURE_NEEDS_GRADE", "UNKNOWN_CAMPAIGN_ASSET_KIND",
        "UNKNOWN_GADGET_ABILITY", "UNKNOWN_GADGET_TALENT", "UNKNOWN_VARIANT_KIND",
        "ALTERNATE_FORM_LEVEL_NOT_PAID"
    };

    /// <summary>
    /// <b>The same guarantee for the list above, which was the identical hole one table over.</b>
    /// <see cref="MustOfferOptions"/> is hand-maintained, and nothing held it to the validator: a
    /// new code whose fix is a choice could be added with <c>Options = []</c> and left out of the
    /// list, and both this and <see cref="EveryOptionOfferedIsOneTheRulesAccept"/> would pass —
    /// the first by not listing it, the second by having nothing to walk.
    ///
    /// <para>Read off the source rather than off a run, because a code that offers options
    /// <em>sometimes</em> is the case a behavioural check cannot see: the finding with the empty
    /// list is exactly the one that needs catching.</para>
    /// </summary>
    [Fact]
    public void EveryCodeThatOffersOptionsSaysSoInTheList()
    {
        // The source is cut at each finding rather than matched as a block: a finding written
        // without an object initialiser has no closing brace to stop at, so a block pattern runs
        // on into the next one and inherits its Options. Each finding therefore owns the text
        // from its own code to where the next one starts.
        var positions = new Regex(
            @"ValidationSeverity\.(?:Error|Warning),\s*([^,]*),",
            RegexOptions.Singleline, TimeSpan.FromSeconds(5))
            .Matches(EngineSource);

        var literals = new Regex(@"""([A-Z][A-Z0-9_]*)""", RegexOptions.None, TimeSpan.FromSeconds(5));

        // `Options` with its assignment, not the bare word: one finding carries the comment
        // "No Options here, deliberately", which is a statement that it has none.
        var assigned = new Regex(@"\bOptions\s*=", RegexOptions.None, TimeSpan.FromSeconds(5));

        var offering = new HashSet<string>(StringComparer.Ordinal);

        for (var i = 0; i < positions.Count; i++)
        {
            var start = positions[i].Index;
            var end   = i + 1 < positions.Count ? positions[i + 1].Index : EngineSource.Length;

            if (!assigned.IsMatch(EngineSource[start..end])) continue;

            foreach (Match code in literals.Matches(positions[i].Groups[1].Value))
                offering.Add(code.Groups[1].Value);
        }

        Assert.NotEmpty(offering);

        Assert.True(offering.SetEquals(MustOfferOptions),
            "MustOfferOptions and the validator disagree about which findings carry a choice. "
            + "Only in the validator: "
            + string.Join(", ", offering.Except(MustOfferOptions, StringComparer.Ordinal).Order(StringComparer.Ordinal))
            + ". Only in the list: "
            + string.Join(", ", MustOfferOptions.Except(offering, StringComparer.Ordinal).Order(StringComparer.Ordinal)));
    }

    /// <summary>
    /// Severity is what decides whether a character is legal, and several tests asserted a code
    /// was reported without asserting it was an error — so downgrading it to a warning left a
    /// character on a misspelled package validating clean.
    /// </summary>
    [Theory]
    [InlineData("UNKNOWN_PACKAGE")]
    [InlineData("PER_UNIT_WITHOUT_UNITS")]
    [InlineData("UNKNOWN_PERK")]
    [InlineData("DUPLICATE_FLAW")]
    [InlineData("MODIFIER_ON_UNBOUGHT_ABILITY")]
    public void ACodeThatMakesACharacterIllegalIsAnError(string code)
    {
        var sheets = CaseNames.Select(Build)
            .Where(s => _f.Validator.Validate(s).Issues.Any(i => i.Code == code))
            .ToList();

        Assert.NotEmpty(sheets);

        foreach (var sheet in sheets)
        {
            var result = _f.Validator.Validate(sheet);

            Assert.All(result.Issues.Where(i => i.Code == code),
                i => Assert.Equal(ValidationSeverity.Error, i.Severity));
            Assert.False(result.IsValid);
        }
    }

    /// <summary>
    /// <b>The constraints the rulebook prints inside an option are now enforced on a submitted
    /// character, not only by the two editors' pickers.</b> Until this, a file could carry the
    /// Ranged Pro on a Self-range Power — which raises a Touch Power to Distant Range and has
    /// nothing to raise on a Power that affects only you — and come back legal at exit 0, which
    /// was a hole in the one claim the whole surface makes.
    /// </summary>
    [Theory]
    [InlineData("ranged", true, "PRO_NOT_APPLICABLE")]
    [InlineData("close", false, "CON_NOT_APPLICABLE")]
    public void AnOptionTheRulebookDoesNotAllowOnThisPowerIsRefused(string id, bool isPro, string code)
    {
        var sheet = Legal();
        sheet.SelectedPowers.Add(isPro
            ? new SelectedPower("armor", 2, [new SelectedProCon(id, "from_touch")], [])
            : new SelectedPower("armor", 2, [], [new SelectedProCon(id)]));

        var issue = Issue(sheet, code);

        Assert.Equal(id, issue.SubjectId);
        Assert.Equal("armor", issue.OwnerId);
        Assert.False(_f.Validator.Validate(sheet).IsValid);

        // The pickers and the validator have to agree, or a character the editors built would be
        // refused — or worse, one they refused to build would be accepted.
        var armor = _f.Rules.GetPower("armor")!;
        Assert.DoesNotContain(id, new ProConApplicability(_f.Rules).ProsFor(armor).Select(p => p.Id));
        Assert.DoesNotContain(id, new ProConApplicability(_f.Rules).ConsFor(armor).Select(c => c.Id));
    }

    /// <summary>
    /// <b>And the same option on a Power it does allow is accepted.</b> Half a rule enforced in
    /// only one direction would be worse than none: the previous per-Power lists were deleted
    /// because they refused options the rulebook permits, and this must not reintroduce that.
    /// </summary>
    [Fact]
    public void AnOptionTheRulebookDoesAllowIsNotRefused()
    {
        var applicability = new ProConApplicability(_f.Rules);

        foreach (var power in _f.Rules.Powers)
        {
            var sheet = Legal();
            sheet.SelectedPowers.Add(new SelectedPower(power.Id, 0,
                [.. applicability.ProsFor(power).Select(p => new SelectedProCon(p.Id, FirstKey(p.CostModifierRange)))],
                [.. applicability.ConsFor(power).Select(c => new SelectedProCon(c.Id, FirstKey(c.CostModifierRange)))])
            { SourceId = "innate" });

            var refused = _f.Validator.Validate(sheet).Issues
                .Where(i => i.Code is "PRO_NOT_APPLICABLE" or "CON_NOT_APPLICABLE")
                .ToList();

            Assert.True(refused.Count == 0,
                $"{power.Id} was offered options the validator then refused: "
                + string.Join(", ", refused.Select(i => i.SubjectId)));
        }
    }

    private static string? FirstKey(IReadOnlyDictionary<string, int>? range) => range?.Keys.FirstOrDefault();

    /// <summary>
    /// <b>A caveat must never become a filter.</b> Most of what an option states — "Powers that
    /// inflict physical or energy damage" — is not something the rulebook prints per Power, and
    /// enforcing it would mean about a thousand fresh judgements. Only Range and Rank type are
    /// enforced, and this says so in the only way that stays true: an option with neither
    /// constraint is applicable to every one of the 141 Powers.
    /// </summary>
    [Fact]
    public void AnOptionWithNoPrintedConstraintAppliesToEveryPower()
    {
        // Concat with the interface named once, rather than a Cast on each side: a list of
        // Pros is already a sequence of the interface, so the casts were doing nothing.
        var unconstrained = _f.Rules.Pros.Concat<Engine.Models.IGenericProCon>(_f.Rules.Cons)
            .Where(o => o.AppliesToRanges.Count == 0 && o.AppliesToRankTypes.Count == 0)
            .ToList();

        Assert.NotEmpty(unconstrained);

        foreach (var option in unconstrained)
            Assert.All(_f.Rules.Powers, p => Assert.True(ProConApplicability.IsApplicable(option, p)));
    }

    /// <summary>No package at all is the ordinary case and is not a finding.</summary>
    [Fact]
    public void NoStartingPackageIsNotAnIssue()
    {
        Assert.False(Reports(Legal(), "UNKNOWN_PACKAGE"));
    }

    // ── Invariants across every finding ───────────────────────────────────

    /// <summary>
    /// Sheets chosen to make the validator say as many different things as it can, so that
    /// the invariants below are asserted over the whole surface rather than over the handful
    /// of findings somebody remembered.
    /// </summary>
    public static TheoryData<string> Cases() => [.. CaseNames];

    /// <summary>
    /// <b>Internal rather than private, because it is the one list in the suite that is held to
    /// the validator's own source.</b> <see cref="McpServerTests"/> reads every field of every
    /// issue the MCP report serialises and needs the same sheets: a second list would be the one
    /// that goes stale when a code is added, which is the failure
    /// <see cref="EveryCodeTheValidatorCanReportIsProvokedBySomeCase"/> exists to record.
    /// </summary>
    internal static readonly string[] CaseNames =
    [
        "no tier", "over budget", "above cap", "no flaws", "too many flaws",
        "unknown ids", "gear", "ranks on a rankless power", "unresolved selections",
        "an expertise under a power",
        "iconic", "unknown tier", "unknown package", "unknown traits", "unknown modifiers",
        "ungraded modifiers", "negative quantities", "options that do not apply",
        "no traits at all", "below its package", "gear without a name", "gear at its floor", "power at its floor",
        "unpriceable", "duplicates", "per-unit with no units",
        "power-specific ungraded", "sample villain",
        "house cap above the tier", "house cap below one",
        "a table's price for immortality",
        "a vehicle", "a headquarters", "a gadget", "assets without names", "a shared asset",
        "a variant", "a proposal", "unnamed units"
    ];

    /// <summary>The sheet for one case name. Internal for the reason <see cref="CaseNames"/> is.</summary>
    internal CharacterSheet Build(string which)
    {
        switch (which)
        {
            case "no tier":
                return new CharacterSheet();

            case "over budget":
            {
                var sheet = Legal();
                foreach (var a in _f.Rules.Abilities) sheet.AbilityRanks[a.Id] = 12;
                foreach (var t in _f.Rules.Talents) sheet.TalentRanks[t.Id] = 12;
                return sheet;
            }

            case "above cap":
            {
                var sheet = Legal();
                sheet.AbilityRanks["intellect"] = 40;
                sheet.TalentRanks["academics"]  = 40;
                sheet.SelectedPowers.Add(new SelectedPower("blast", 40));
                return sheet;
            }

            // A house Trait Cap looser than the tier's is not a house rule, it is a character
            // playing above the agreed power level — and it raises Resolve as well as the ranks.
            case "house cap above the tier":
            {
                var sheet = Legal();
                sheet.TraitCapRank = 40;
                return sheet;
            }

            // And a ceiling below the 1d floor, which no legal character can fit under.
            case "house cap below one":
            {
                var sheet = Legal();
                sheet.TraitCapRank = 0;
                return sheet;
            }

            // Ch.2 p.31 puts a table's price between 6 and 12; 20 is outside it and is charged
            // as written, the way every other bad figure here is.
            // Immunity bought three times with one name, and a name recorded past the count on a
            // second, counted Power — both findings from one sheet.
            case "unnamed units":
            {
                var sheet = Legal();
                sheet.SelectedPowers.Add(new SelectedPower("immunity", 0)
                {
                    Units = 3, SourceId = "tech", UnitNames = ["Toxins"]
                });
                sheet.SelectedPowers.Add(new SelectedPower("determination", 0)
                {
                    Units = 1, SourceId = "innate", UnitNames = ["", "Grit"]
                });
                return sheet;
            }

            case "a table's price for immortality":
            {
                var sheet = Legal();
                sheet.SelectedPowers.Add(new SelectedPower("immortality", 0));
                sheet.ImmortalityCost = 20;
                return sheet;
            }

            case "no flaws":
                return RulesFixture.StandardSheet();

            case "too many flaws":
            {
                var sheet = RulesFixture.StandardSheet();
                foreach (var flaw in _f.Rules.Flaws.Take(10)) sheet.Flaws.Add(new SelectedFlaw(flaw.Id));
                return sheet;
            }

            case "unknown ids":
            {
                var sheet = Legal();
                sheet.Flaws.Add(new SelectedFlaw("being_far_too_tall"));
                sheet.SelectedPowers.Add(new SelectedPower("chronomancy", 3));
                sheet.SelectedPowers.Add(new SelectedPower("blast", 3) { SourceId = "cosmic" });
                sheet.AbilitySources["might"]     = "cosmic";
                sheet.TalentSources["academics"]  = "cosmic";
                sheet.AbilitySources["telepathy"] = "tech";

                // <b>And the Talent half of the same finding, which nothing reached.</b>
                // UNKNOWN_TRAIT_SOURCE was provoked on Abilities alone, and the guarantee here
                // is per *code* rather than per construction site — so the Talent arm of its
                // option list was dead, and could be made to offer perk ids with the whole suite
                // green. That is the defect this file's option check exists for, alive on the
                // branch no sheet visited.
                sheet.TalentSources["telekinesis"] = "magic";

                // And the blank Source, which is a third arm of the same method with its own
                // sentence and its own option list, reached by no other case here.
                sheet.TalentSources["covert"] = "";
                return sheet;
            }

            case "gear":
            {
                var sheet = Legal();
                sheet.Gear.Add(new SelectedGear("Mystery box") { Features = [new("teleporting")] });
                sheet.Gear.Add(new SelectedGear("Pistol") { Features = [new("accurate")] });
                sheet.Gear.Add(new SelectedGear("Jo Sticks")
                {
                    Features = [new("upgraded")],
                    PairedUnderTwoFisted = true
                });

                // A Chapter 6 catalogue row that does not resolve. Reported, never repaired: the
                // id is what makes an item a Battle Axe rather than a name somebody typed.
                sheet.Gear.Add(new SelectedGear("Battle Axe")
                {
                    CatalogueId = GearCatalogue.WeaponPrefix + "battel_axe"
                });

                // And one that does, so the finding above is about a wrong id rather than about
                // recording an id at all.
                sheet.Gear.Add(new SelectedGear("Baton")
                {
                    CatalogueId = GearCatalogue.WeaponPrefix + "baton"
                });

                return sheet;
            }

            case "ranks on a rankless power":
            {
                var sheet = Legal();
                sheet.SelectedPowers.Add(new SelectedPower("invisibility", 4));
                sheet.SelectedPowers.Add(new SelectedPower("communications", 0));
                return sheet;
            }

            case "unresolved selections":
            {
                var sheet = Legal();
                sheet.SelectedPowers.Add(new SelectedPower("boost", 2));
                sheet.SelectedPowers.Add(new SelectedPower("omni_power", 4));
                return sheet;
            }

            // An Expertise nominated to a Power, which Ch.2 p.28 does not allow — beside a Boost
            // nominated to the same Power, which its own text does. The pair is the case: a rule
            // written across both would report one of these two and be wrong either way.
            case "an expertise under a power":
            {
                var sheet = Legal();
                sheet.SelectedPowers.Add(
                    new SelectedPower("expertise", 2) { BaselineTraitId = "martial_arts" });
                sheet.SelectedPowers.Add(
                    new SelectedPower("boost", 2) { BaselineTraitId = "martial_arts" });
                return sheet;
            }

            case "iconic":
            {
                var sheet = Legal();
                sheet.SelectedTierId = "iconic";
                return sheet;
            }

            case "unknown tier":
            {
                var sheet = Legal();
                sheet.SelectedTierId = "stanadrd";
                return sheet;
            }

            case "unknown package":
            {
                var sheet = Legal();
                sheet.SelectedPackageId = "hero";
                return sheet;
            }

            case "unknown traits":
            {
                var sheet = Legal();
                sheet.AbilityRanks["strength"] = 4;
                sheet.TalentRanks["athletics"] = 4;
                sheet.Perks.Add(new SelectedPerk("time_machine"));
                return sheet;
            }

            case "unknown modifiers":
            {
                var sheet = Legal();
                sheet.AbilityRanks["might"] = 6;
                sheet.SelectedPowers.Add(new SelectedPower("armor", 2,
                    [new SelectedProCon("teleporty")], [new SelectedProCon("wibble")])
                { SourceId = "tech" });
                sheet.Gear.Add(new SelectedGear("Sword") { Cons = [new("nonsuch")] });
                sheet.AbilityModifiers["intellect"] = [new SelectedProCon("overkill")];
                return sheet;
            }

            case "ungraded modifiers":
            {
                // A Pro and a Con priced by grade, one with no key and one with a key the
                // rulebook does not have. Both threw out of the middle of the total before
                // they were checked.
                var sheet = Legal();
                sheet.SelectedPowers.Add(new SelectedPower("blast", 3,
                    [new SelectedProCon("area_burst")],
                    [new SelectedProCon("charges", "eleventy_per_scene")])
                { SourceId = "tech" });
                sheet.SelectedPowers.Add(new SelectedPower("boost", 2) { BaselineTraitId = "wibble" });
                sheet.SelectedPowers.Add(new SelectedPower("omni_power", 1) { CostVariantKey = "narrow-ish" });
                sheet.Gear.Add(new SelectedGear("Pistol")
                {
                    Features = [new("accurate", "extremely_accurate")]
                });
                return sheet;
            }

            case "negative quantities":
            {
                var sheet = Legal();
                sheet.AbilityRanks["might"]    = -50;
                sheet.TalentRanks["academics"] = -3;
                sheet.SelectedPowers.Add(new SelectedPower("blast", -4) { SourceId = "tech" });
                sheet.SelectedPowers.Add(new SelectedPower("immunity", 0)
                { Units = -20, SourceId = "tech" });
                sheet.Perks.Add(new SelectedPerk("contacts", -1000));
                sheet.SelectedPowers.Add(new SelectedPower("nullify", 6,
                    [new SelectedProCon("also_x") { Units = -1000 }], []) { SourceId = "magic" });
                return sheet;
            }

            case "power-specific ungraded":
            {
                // Drain's own Only X Con is priced by grade, and the branch that handles a
                // Power's own Pros and Cons skipped the grade check entirely — so this threw
                // out of the validator rather than being reported.
                var sheet = Legal();
                sheet.SelectedPowers.Add(new SelectedPower("drain", 6,
                    [], [new SelectedProCon("only_x")]) { SourceId = "magic" });
                return sheet;
            }

            case "per-unit with no units":
            {
                var sheet = Legal();
                sheet.Perks.Add(new SelectedPerk("contacts", 0));
                sheet.SelectedPowers.Add(new SelectedPower("determination", 0)
                { Units = 0, SourceId = "innate" });
                return sheet;
            }

            case "options that do not apply":
            {
                // The Ranged Pro raises a Touch or Zone Power to Distant Range, and has nothing
                // to raise on Armor, which affects only you. Close is a Con for Ranged Powers.
                var sheet = Legal();
                sheet.SelectedPowers.Add(new SelectedPower("armor", 2,
                    [new SelectedProCon("ranged", "from_touch")],
                    [new SelectedProCon("close")]) { SourceId = "tech" });
                return sheet;
            }

            case "no traits at all":
            {
                // Deliberately the bare sheet: every Trait at 0d, which is what an unenforced
                // 1d minimum used to allow, plus a Con keyed to an Ability that has no ranks.
                var sheet = RulesFixture.StandardSheet();
                sheet.Flaws.Add(new SelectedFlaw("code"));
                sheet.AbilityModifiers["intellect"] = [new SelectedProCon("overkill")];
                return sheet;
            }

            case "below its package":
            {
                // A package grants a floor and cannot be lowered below it — the rule that proved
                // Airmid's recorded package impossible, checked here on a submitted character.
                var sheet = Legal();
                sheet.SelectedPackageId = "superhero_package";
                return sheet;
            }

            // ── Chapter 6's vehicles, headquarters and Gadgets ────────────────────

            case "a vehicle":
            {
                // Over its Vehicle Point budget, Control above half its Speed, a feature the
                // rulebook does not have, and a Mecha whose limbs are too weak for its Body.
                var sheet = Legal();
                sheet.Vehicles.Add(new OwnedVehicle("The Wing")
                {
                    PerkHeroPoints = 1,
                    Body = 20, Speed = 4, Control = 3,
                    Features = [new SelectedAssetFeature("mecha") { Units = 2 }]
                });
                sheet.Vehicles.Add(new OwnedVehicle("The Sub")
                {
                    // Control below its floor is legal-shaped and reported; Body below zero is
                    // not a rank at all and pays Vehicle Points back, and the Perk allowance
                    // below zero pays Hero Points back. Submersible with no Swimming is Ruling 2's
                    // finding. Four different findings on one machine.
                    PerkHeroPoints = -2, Control = -9, Body = -4,
                    Features = [new SelectedAssetFeature("submersible")]
                });
                // Both unpriceable faults on one machine, and deliberately on a machine of their
                // own: an unpriceable feature silences that vehicle's budget check, so putting one
                // on The Wing would have hidden the finding this case is mostly for.
                sheet.Vehicles.Add(new OwnedVehicle("The Mystery")
                {
                    PerkHeroPoints = 1,
                    Features =
                    [
                        new SelectedAssetFeature("teleport_bay"),
                        new SelectedAssetFeature("passengers") { Units = 0 }
                    ]
                });
                return sheet;
            }

            case "a headquarters":
            {
                // Over its Base Point budget, and a graded feature with no grade chosen.
                var sheet = Legal();
                sheet.Headquarters.Add(new OwnedHeadquarters("The Vault")
                {
                    PerkHeroPoints = 1,
                    Features =
                    [
                        new SelectedAssetFeature("hidden"),
                        new SelectedAssetFeature("disguised"),
                        new SelectedAssetFeature("tesseract")
                    ]
                });
                sheet.Headquarters.Add(new OwnedHeadquarters("The Loft")
                {
                    PerkHeroPoints = 4,
                    Features = [new SelectedAssetFeature("size")]
                });

                // …and an allowance below zero, which pays Hero Points to the character.
                sheet.Headquarters.Add(new OwnedHeadquarters("The Overdraft") { PerkHeroPoints = -3 });

                // …and the Perk recorded a second time beside the bases it already paid for.
                sheet.Perks.Add(new SelectedPerk("headquarters", 2));
                return sheet;
            }

            case "a gadget":
            {
                // Complexity below the floor, a builder with too little Technology, a Complexity
                // above what Technology allows, a pool overspent, and a Power that is not one.
                // A builder who can build at all, so the Technology floor is not what answers
                // here — that one is on "assets without names", because a builder is either below
                // the floor or not and one sheet cannot be both.
                var sheet = Legal();
                sheet.TalentRanks["technology"] = 6;

                sheet.Gadgets.Add(new BuiltGadget("Trinket") { Complexity = 1 });
                sheet.Gadgets.Add(new BuiltGadget("Whatsit")
                {
                    Complexity = 3,
                    Powers = [new SelectedPower("time_ray", 4)],

                    // And a Trait id that is not one either. All three collections a Gadget
                    // carries can name something the rulebook does not have, and only the Powers
                    // were being asked — the other two silently spent nothing.
                    AbilityRanks = new Dictionary<string, int> { ["mightt"] = 4 },
                    TalentRanks  = new Dictionary<string, int> { ["technologee"] = 2 }
                });
                sheet.Gadgets.Add(new BuiltGadget("Overreach")
                {
                    Complexity   = 9,
                    AbilityRanks = new Dictionary<string, int> { ["might"] = -50 }
                });
                sheet.Gadgets.Add(new BuiltGadget("Freeze Ray")
                {
                    Complexity = 3,
                    Powers = [new SelectedPower("blast", 12) { SourceId = "tech" }]
                });
                return sheet;
            }

            case "assets without names":
            {
                var sheet = Legal();
                sheet.Vehicles.Add(new OwnedVehicle("  "));
                sheet.Headquarters.Add(new OwnedHeadquarters(""));
                sheet.Gadgets.Add(new BuiltGadget(" ") { Complexity = 3 });

                // The Technology floor, which the case above cannot also reach: a builder is
                // either below it or not, and the sheet above is not.
                sheet.Gadgets.Add(new BuiltGadget("Bodge") { Complexity = 3 });
                return sheet;
            }

            case "a shared asset":
            {
                var sheet = Legal();
                sheet.CampaignAssets.Add(new CampaignAssetContribution("")
                {
                    Name = "The Aerie", Kind = "space_station", HeroPoints = -2
                });

                // Ruling 7: a contribution above the owner's cap is reported before the figure
                // ever reaches CostCalculator.CampaignAssetBudget's arithmetic.
                sheet.CampaignAssets.Add(new CampaignAssetContribution("asset-1")
                {
                    Name = "The Wing", Kind = CampaignAssetContribution.Vehicle,
                    HeroPoints = CampaignAssetContribution.MaxHeroPoints + 1
                });
                return sheet;
            }

            case "a variant":
            {
                // Both structural findings at once: a blank root and an unrecognised kind. The
                // engine cannot see the roster, so whether the root is actually held by anybody
                // is not this validator's question — see CheckVariant's own remarks.
                var sheet = Legal();
                sheet.Variant = new CharacterVariant("", "cursed_mirror");
                return sheet;
            }

            case "a proposal":
            {
                // PROGRESS item 33, rulings 5+6: a proposal is a CampaignAsset riding the
                // contribution. Cheap and empty on purpose, so the only thing it provokes is the
                // surplus the Hero Points buy and nothing has spent — CAMPAIGN_ASSET_SURPLUS is
                // the one code in this file that only Validate(sheet) alone can construct, since
                // CheckSharedAsset and CheckContributionAgainstAsset both need a second object in
                // hand that this sheet does not carry.
                var sheet = Legal();
                sheet.CampaignAssets.Add(new CampaignAssetContribution("asset-p1")
                {
                    Name = "Skyhook", Kind = CampaignAssetContribution.Vehicle, HeroPoints = 1,
                    Proposal = new CampaignAsset("asset-p1", CampaignAssetContribution.Vehicle, "Skyhook")
                });
                return sheet;
            }

            case "gear without a name":
            {
                var sheet = Legal();
                sheet.Gear.Add(new SelectedGear("  "));
                return sheet;
            }

            case "gear at its floor":
            {
                var sheet = Legal();
                sheet.Gear.Add(new SelectedGear("Battered helmet")
                {
                    Features = [new("bonded")],
                    Cons     = [new("item"), new("unreliable")]
                });
                return sheet;
            }

            case "power at its floor":
            {
                var sheet = Legal();
                sheet.SelectedPowers.Add(new SelectedPower("armor", 4,
                    [], [new SelectedProCon("burnout")])
                { SourceId = "tech" });
                return sheet;
            }

            case "duplicates":
            {
                var sheet = Legal();
                sheet.Flaws.Add(new SelectedFlaw("code"));
                sheet.AbilityRanks["might"] = 12;
                sheet.AbilityModifiers["might"] = [new("burnout"), new("burnout")];
                sheet.SelectedPowers.Add(new SelectedPower("blast", 2,
                    [new SelectedProCon("subtle"), new SelectedProCon("subtle")], [])
                { SourceId = "tech" });
                sheet.SelectedPowers.Add(new SelectedPower("blast", 3) { SourceId = "tech" });
                return sheet;
            }

            case "unpriceable":
            {
                // The backstop. Nothing here should reach it — every gap above is reported by
                // name — so this case exists to prove the report stays a report if one ever
                // does, rather than to provoke a particular code.
                var sheet = Legal();
                sheet.SelectedPowers.Add(new SelectedPower("boost", 2));
                sheet.Perks.Add(new SelectedPerk("time_machine"));
                return sheet;
            }

            default:
                return SampleCharacters.Villain();
        }
    }

    private List<ValidationIssue> IssuesFor(string which) =>
        [.. _f.Validator.Validate(Build(which)).Issues];

    /// <summary>
    /// <b>The invariants below are only worth what <see cref="Cases"/> reaches, and nothing
    /// used to hold that list to anything.</b> Two codes added in the same change as those
    /// invariants were not in it, and one of the invariants would have failed had they been —
    /// so all three ran green over a surface that did not include the work they were written
    /// for.
    ///
    /// <para>So the list is checked against the validator's own source: every code it can
    /// construct has to be provoked by some case here. A check added later cannot be quietly
    /// exempt from the structural rules, because adding it fails this test until a sheet that
    /// produces it exists.</para>
    /// </summary>
    internal static readonly string[] UnprovokableCodes =
    [
        "POWER_MECHANICS_UNVERIFIED", "POWER_DESCRIPTION_UNVERIFIED", "CHARACTER_NOT_PRICEABLE",

        // Ruling 8's CAMPAIGN_ASSET_KIND_MISMATCH comes only out of
        // CharacterValidator.CheckContributionAgainstAsset, which takes a contribution and a
        // campaign's own asset rather than a CharacterSheet — nothing Validate(sheet) walks can
        // ever reach it, by the same no-storage line CheckSharedAsset is exempt for. Exercised
        // directly by CampaignAssetTests instead.
        "CAMPAIGN_ASSET_KIND_MISMATCH",

        // Item 21 slice two (plus the p.21 Trait Cap ruling): the seven ALTERNATE_FORM_* codes come only out of
        // AlternateForms.Families, which takes a roster — two sheets at once is the whole point
        // of them, and Validate(sheet) has one. Every one is provoked, with its structure used
        // for a repair, by AlternateFormTests instead; that file's own scan holds its case list
        // to this one.
        "ALTERNATE_FORM_ROOT_NOT_IN_ROSTER", "ALTERNATE_FORM_NOT_PAID", "ALTERNATE_FORM_COST_DIFFERS",
        "ALTERNATE_FORM_ABOVE_ROOT_LEVEL", "ALTERNATE_FORM_LEVEL_NOT_PAID",
        "ALTERNATE_FORM_PAID_NOT_IN_ROSTER", "ALTERNATE_FORM_CAP_NOT_ROOTS"
    ];

    /// <summary>
    /// <b>Every source file in the engine, not just the validator.</b> Reading
    /// <c>CharacterValidator.cs</c> alone scoped all three guarantees to one file: moving the
    /// codes to a constants class, or making the validator partial, left a code with no recorded
    /// subject kind and no provoking case, with the whole suite green. Both are ordinary
    /// refactors and neither should be able to void a guarantee silently.
    /// </summary>
    /// <remarks>
    /// Build output is excluded. <c>AllDirectories</c> swept in six generated files from
    /// <c>obj/</c>, which keyed three guarantees to whether the project had been built and in
    /// which configuration — nothing broke, because none of them holds an all-capitals literal,
    /// but a generated one would have been demanded to have a subject kind and a provoking case.
    /// </remarks>
    private static string EngineSource { get; } = string.Join("\n",
        Directory.GetFiles(Path.Combine(RulesFixture.RepoRoot, "engine"), "*.cs",
                           SearchOption.AllDirectories)
            .Where(f => !f.Contains($"{Path.DirectorySeparatorChar}obj{Path.DirectorySeparatorChar}",
                                    StringComparison.Ordinal)
                     && !f.Contains($"{Path.DirectorySeparatorChar}bin{Path.DirectorySeparatorChar}",
                                    StringComparison.Ordinal))
            .OrderBy(f => f, StringComparer.Ordinal)
            .Select(File.ReadAllText));

    /// <summary>
    /// The one all-capitals literal in the engine that is not a validation code: the heading a
    /// sheet prints over Powers with no Source (<c>SourceGrouping</c>). Named rather than matched
    /// by shape, so a code that happens to look like it is not swept up with it.
    /// </summary>
    private static readonly string[] NotCodes = ["POWERS"];

    /// <summary>
    /// <b>Every code-shaped literal in the validator, found by its case rather than by its
    /// punctuation.</b> This is what the two tables above and the invariants below are all
    /// measured against, so what it fails to see is exempt from every one of them.
    ///
    /// <para>It has been too narrow twice. First it matched only a literal written immediately
    /// after a severity, which missed eight of forty — a
    /// <c>isPro ? "UNKNOWN_PRO" : "UNKNOWN_CON"</c> ternary and a helper taking the code as an
    /// argument. Then it matched <c>[A-Z]+(?:_[A-Z]+)+</c>, which requires an underscore: a
    /// clean A/B on the same unreachable check proved it, with <c>TOO_MANY_CONNECTIONS</c> going
    /// red and <c>TOOMANYCONNECTIONS</c> staying green. Both times the escape was <em>how the
    /// code was spelled</em>, which is exactly how the next one will be spelled too.</para>
    ///
    /// <para>So it now takes any all-capitals literal, and
    /// <see cref="TheCodeScanIsNotDefeatedByHowACodeIsSpelled"/> drives it against a synthetic
    /// source rather than the real one — because a scan that reads only the shipped file cannot
    /// tell you what it would miss in a file that is not there yet.</para>
    /// </summary>
    private static HashSet<string> DeclaredCodes(string source)
    {
        var found = new Regex(@"""([A-Z][A-Z0-9_]*)""", RegexOptions.None, TimeSpan.FromSeconds(5))
            .Matches(source)
            .Select(m => m.Groups[1].Value)
            .ToHashSet(StringComparer.Ordinal);

        found.ExceptWith(NotCodes);
        return found;
    }

    /// <summary>
    /// <b>The scan above is by case, so the case is enforced rather than assumed.</b> That is the
    /// half that was missing: spelling a code <c>"TooManyConnections"</c> made it invisible to
    /// every guarantee built on the scan, with the suite green — the same escape as
    /// <c>TOOMANYCONNECTIONS</c> one spelling further on, and the reason widening the pattern
    /// again would not have closed it. A pattern can always be out-spelled; a convention checked
    /// at the point the code is written cannot.
    ///
    /// <para>Both places a code is written are read: the second argument of the issue
    /// constructor, and the first of the <c>Negative</c> helper. The capture stops at the next
    /// comma, which is why the <c>isPro ? "UNKNOWN_PRO" : "UNKNOWN_CON"</c> ternaries come
    /// through whole — they contain no comma — and both arms are checked.</para>
    /// </summary>
    [Fact]
    public void EveryCodeIsWrittenInTheOneSpellingTheScanCanSee()
    {
        var written = CodeExpressions(EngineSource);

        // A pattern that matched nothing would pass this test in silence, which is the failure
        // this whole area keeps having.
        Assert.True(written.Count >= 40,
            $"Only {written.Count} places construct a finding, which means this scan has stopped "
            + "finding them rather than that the validator has shrunk.");

        foreach (var (expression, literal) in written)
            Assert.True(Regex.IsMatch(literal, "^[A-Z][A-Z0-9_]*$", RegexOptions.None, TimeSpan.FromSeconds(5)),
                $"The code '{literal}' is written in a spelling DeclaredCodes cannot see, so it "
                + "would be exempt from every structural rule here. Codes are ALL CAPITALS. "
                + $"Written as: {expression.Trim()}");

        // And the two scans agree, so neither can quietly stop seeing what the other does.
        Assert.Empty(written.Select(w => w.Literal).Except(DeclaredCodes(EngineSource), StringComparer.Ordinal));
    }

    /// <summary>
    /// <b>Exactly one place builds a finding from a code it was handed rather than one written
    /// there, and that is what makes the spelling convention reach everything.</b>
    ///
    /// <para><c>CodeExpressions</c> reads two call shapes, so a third — a helper taking the code
    /// as a parameter — hides both the helper's own construction site (its code is an identifier,
    /// not a literal) and its call sites (an unrecognised shape). A code spelled
    /// <c>"TooManyConnections"</c> and passed through one is then exempt from the convention, the
    /// kind table, the provoking-case guarantee and the option check together.</para>
    ///
    /// <para><b>An earlier version pinned the <em>methods</em> that return a
    /// <see cref="ValidationIssue"/>, and that was the same mistake one spelling on</b> — it
    /// matched on the return type, so <c>AddFinding(List&lt;ValidationIssue&gt; into, string code,
    /// …)</c> slipped past on the angle bracket, and appending to a passed-in list is this
    /// validator's house idiom rather than an exotic shape. A generic <c>Finding&lt;T&gt;</c> got
    /// past it too. So this counts <em>construction sites</em> instead, which is the thing that
    /// actually makes a finding and cannot be hidden behind a signature.</para>
    /// </summary>
    [Fact]
    public void OnlyOneIndirectionBuildsAFindingFromACodeItWasHanded()
    {
        var sites = new Regex(
            @"new(?:\s+ValidationIssue)?\(\s*ValidationSeverity\.(?:Error|Warning),\s*([^,]*),",
            RegexOptions.Singleline, TimeSpan.FromSeconds(5))
            .Matches(EngineSource);

        Assert.True(sites.Count >= 40,
            $"Only {sites.Count} findings are constructed in the engine, which means this scan has "
            + "stopped finding them rather than that the validator has shrunk.");

        var literal = new Regex(@"""[^""]*""", RegexOptions.None, TimeSpan.FromSeconds(5));

        var indirect = sites.Select(m => m.Groups[1].Value)
            .Where(expression => !literal.IsMatch(expression))
            .ToList();

        Assert.True(indirect.Count == 1,
            $"{indirect.Count} places build a finding from a code they were handed — "
            + string.Join(", ", indirect.Select(e => $"'{e.Trim()}'"))
            + ". There must be exactly one, Negative, because CodeExpressions knows that call "
            + "shape and scans its arguments. Any other indirection puts its codes beyond the "
            + "spelling convention and so beyond every structural rule here.");
    }

    /// <summary>
    /// Every literal written where a code goes, with the expression it came from for the failure
    /// message.
    /// </summary>
    private static List<(string Expression, string Literal)> CodeExpressions(string source)
    {
        var positions = new Regex(
            @"ValidationSeverity\.(?:Error|Warning),\s*([^,]*),|Negative\(\s*([^,]*),",
            RegexOptions.Singleline, TimeSpan.FromSeconds(5));

        var literals = new Regex(@"""([^""]*)""", RegexOptions.None, TimeSpan.FromSeconds(5));

        return
        [
            .. positions.Matches(source)
                .Select(m => m.Groups[1].Success ? m.Groups[1].Value : m.Groups[2].Value)
                .SelectMany(expression => literals.Matches(expression)
                    .Select(l => (Expression: expression, Literal: l.Groups[1].Value)))
        ];
    }

    /// <summary>
    /// The scan above, driven against source it has never seen. Reading the real validator
    /// cannot tell the difference between a pattern that finds every code and one that finds
    /// every code <em>somebody happened to spell with an underscore</em>; these five lines can.
    /// </summary>
    [Fact]
    public void TheCodeScanIsNotDefeatedByHowACodeIsSpelled()
    {
        const string synthetic = """
            issues.Add(new(ValidationSeverity.Error, "TOO_MANY_CONNECTIONS", "A sentence."));
            issues.Add(new(ValidationSeverity.Error, "TOOMANYCONNECTIONS", "Another."));
            issues.Add(Negative(isPro ? "UNKNOWN_PRO2" : "UNKNOWN_CON", ValidationSubject.Power));
            if (power.CostType is "per_unit" or "flat_variable") Report("Pro", power.Range);
            """;

        var found = DeclaredCodes(synthetic);

        // The underscored spelling, the one without an underscore, one carrying a digit, and both
        // arms of a ternary.
        Assert.Contains("TOO_MANY_CONNECTIONS", found);
        Assert.Contains("TOOMANYCONNECTIONS", found);
        Assert.Contains("UNKNOWN_PRO2", found);
        Assert.Contains("UNKNOWN_CON", found);

        // And it does not sweep up the ordinary strings a validator is full of: the rules-data
        // values it compares against, the words it builds sentences from, and prose.
        Assert.DoesNotContain("per_unit", found);
        Assert.DoesNotContain("flat_variable", found);
        Assert.DoesNotContain("Pro", found);
        Assert.DoesNotContain("A sentence.", found);
    }

    [Fact]
    public void EveryCodeTheValidatorCanReportIsProvokedBySomeCase()
    {
        var declared = DeclaredCodes(EngineSource);

        Assert.NotEmpty(declared);

        // Three codes cannot be provoked from the shipped rules, and each is exempt for a
        // reason rather than for convenience:
        //
        //   The two POWER_*_UNVERIFIED warnings fire on a per-entry flag that no entry in
        //   data/rules/ sets — every Power is verified. Provoking them would mean shipping a
        //   rules file with a flag set to make a test go green.
        //
        //   CHARACTER_NOT_PRICEABLE is the backstop inside CheckHpBudget, and being
        //   unreachable is the whole of its job: every gap that used to reach it is now
        //   reported by name. Demanding a payload for it would mean leaving one of those
        //   holes open on purpose. If one is ever found, it belongs in Cases() as a bug.
        declared.ExceptWith(UnprovokableCodes);

        var provoked = CaseNames
            .SelectMany(IssuesFor)
            .Select(i => i.Code)
            .ToHashSet(StringComparer.Ordinal);

        var missing = declared.Except(provoked).Order().ToList();

        Assert.True(missing.Count == 0,
            "No sheet in Cases() produces: " + string.Join(", ", missing) +
            ". Add one, or the structural invariants below never see these codes.");
    }

    /// <summary>
    /// <b>Every error says what it is about.</b> This is the rule a check added later would
    /// otherwise quietly break — a new error with no subject reads exactly like the others
    /// until something tries to act on it.
    ///
    /// <para>Warnings are deliberately exempt: one of them names every Power whose wording is
    /// unverified in a single finding, and there is no one subject for it to have.</para>
    /// </summary>
    [Theory]
    [MemberData(nameof(Cases))]
    public void EveryErrorSaysWhatItIsAbout(string which)
    {
        foreach (var issue in IssuesFor(which).Where(i => i.Severity == ValidationSeverity.Error))
            Assert.True(issue.SubjectKind != ValidationSubject.None,
                $"The error {issue.Code} carries no subject, so nothing can act on it: {issue.Message}");
    }

    /// <summary>
    /// An issue about a named thing has to name it. A subject kind with no id says "something
    /// among the abilities is wrong", which is not a repair anybody can make.
    /// </summary>
    [Theory]
    [MemberData(nameof(Cases))]
    public void ASubjectThatIsNotTheWholeCharacterIsNamed(string which)
    {
        foreach (var issue in IssuesFor(which))
        {
            if (issue.SubjectKind is ValidationSubject.None or ValidationSubject.Character) continue;

            Assert.False(string.IsNullOrWhiteSpace(issue.SubjectId),
                $"{issue.Code} says it is about a {issue.SubjectKind} and does not say which.");
        }
    }

    /// <summary>
    /// <b>And the id it names has to be a thing of the kind it claims.</b> This is the rule
    /// <see cref="EachCodeReportsTheKindOfThingItIsAbout"/> cannot state: that table is keyed by
    /// code, and seven codes legitimately serve more than one kind — <c>TRAIT_ABOVE_CAP</c> is
    /// about an Ability, a Talent or a Power depending on which loop raised it. So a swap
    /// <em>within</em> a code's list was invisible to it. Changing <c>CheckTraitSources</c>'s
    /// Ability argument to Talent reported <c>UNKNOWN_SOURCE</c> on <c>toughness</c> as a Talent
    /// problem with the whole suite green, and a repair loop following that writes into
    /// <c>TalentSources</c> and never terminates.
    ///
    /// <para>Asserting the id against the kind removes the need for the table to be precise about
    /// which of several kinds a given finding took, and catches the swap in both directions.</para>
    ///
    /// <para><b>Deliberately no exemption list.</b> Half the codes here report an id the rulebook
    /// does <em>not</em> have — that is the whole of what <c>UNKNOWN_ABILITY</c> says — so
    /// "resolves against the rules" alone would have to excuse them, and excusing them was what
    /// let the mutation through in the first place. So the id may resolve against the rules
    /// <em>or</em> against the part of the character the kind names: <c>strength</c> is an
    /// Ability problem because it is a key of <c>AbilityRanks</c>, whatever the rulebook thinks
    /// of it, and it is still not a Talent problem.</para>
    ///
    /// <para><b><c>Character</c> is checked too, and skipping it was a hole of its own.</b> An
    /// earlier version of this passed over it — and an earlier version of the account in
    /// <c>PROGRESS.md</c> claimed that covered every finding, which it did not. Eleven codes are
    /// Character-kinded by design, and two more may be Character <em>or</em> Power, so mutating
    /// a subject kind <em>towards</em> Character walked through this test, through
    /// <see cref="ASubjectThatIsNotTheWholeCharacterIsNamed"/>, and through the kind table, which
    /// accepts any entry in the code's list. <c>PER_UNIT_WITHOUT_UNITS</c> could report a Power
    /// id as a fault of "the character".</para>
    ///
    /// <para>What a Character-kinded finding may name is narrow: a Perk, a starting package, or a
    /// Pro or Con — the three things that belong to the sheet rather than to a Trait. A Power id
    /// is not one of them.</para>
    /// </summary>
    [Theory]
    [MemberData(nameof(Cases))]
    public void ASubjectIdNamesSomethingOfTheKindTheIssueClaims(string which)
    {
        var sheet = Build(which);

        foreach (var issue in _f.Validator.Validate(sheet).Issues)
        {
            if (issue.SubjectKind is ValidationSubject.None) continue;
            if (issue.SubjectId is not { } id) continue;

            var resolves = issue.SubjectKind switch
            {
                ValidationSubject.Ability => _f.Rules.GetAbility(id) is not null
                    || sheet.AbilityRanks.ContainsKey(id)
                    || sheet.AbilitySources.ContainsKey(id)
                    || sheet.AbilityModifiers.ContainsKey(id),

                ValidationSubject.Talent => _f.Rules.GetTalent(id) is not null
                    || sheet.TalentRanks.ContainsKey(id)
                    || sheet.TalentSources.ContainsKey(id),

                ValidationSubject.Power => _f.Rules.GetPower(id) is not null
                    || sheet.SelectedPowers.Any(p => p.PowerId == id),

                ValidationSubject.Flaw => _f.Rules.GetFlaw(id) is not null
                    || sheet.Flaws.Any(f => f.FlawId == id),

                ValidationSubject.Tier => _f.Rules.GetTier(id) is not null
                    || sheet.SelectedTierId == id,

                ValidationSubject.GearFeature => _f.Rules.GetGearFeature(id) is not null
                    || sheet.Gear.Any(g => g.Features.Any(f => f?.FeatureId == id)),

                // Gear has no id — its name is all it has — so it is looked up on the character.
                ValidationSubject.Gear => sheet.Gear.Any(g => g.Name == id),

                // The same for Chapter 6's three: a machine, a base and a Gadget are identified by
                // their names, which is why a nameless one is filed against the character instead.
                ValidationSubject.Vehicle      => sheet.Vehicles.Any(v => v.Name == id),
                ValidationSubject.Headquarters => sheet.Headquarters.Any(h => h.Name == id),
                ValidationSubject.Gadget       => sheet.Gadgets.Any(g => g.Name == id),

                // A feature off either of Chapter 6's two tables. The owner is in OwnerId, which
                // is what says which table — the same division a gear feature's finding uses.
                ValidationSubject.AssetFeature =>
                    _f.Rules.Assets.FindVehicleFeature(id) is not null
                    || _f.Rules.Assets.FindBaseFeature(id) is not null
                    || sheet.Vehicles.Any(v => v.Features.Any(f => f?.FeatureId == id))
                    || sheet.Headquarters.Any(h => h.Features.Any(f => f?.FeatureId == id)),

                // The whole sheet: a Perk, a starting package, or a Pro or Con. Those are the
                // three things that belong to the character rather than to one of its Traits.
                ValidationSubject.Character =>
                    _f.Rules.GetPerk(id) is not null
                    || sheet.Perks.Any(p => p.PerkId == id)
                    || _f.Rules.CreationRules.OptionalPackages.Any(p => p.Id == id)
                    || sheet.SelectedPackageId == id
                    || _f.Rules.GetPro(id) is not null
                    || _f.Rules.GetCon(id) is not null
                    || EveryChoiceOn(sheet).Any(c => c.Id == id)

                    // …and a campaign's shared vehicle or base, which is a fourth thing that
                    // belongs to the sheet rather than to any Trait: what this character put in.
                    || sheet.CampaignAssets.Any(a => a.AssetId == id)

                    // …and the kind on this character's own Variant link: UNKNOWN_VARIANT_KIND
                    // names the offending kind, which sits on the sheet rather than on any Trait.
                    || sheet.Variant?.Kind == id,

                _ => true
            };

            Assert.True(resolves,
                $"{issue.Code} says it is about the {issue.SubjectKind} '{id}', which is not a "
                + $"{issue.SubjectKind} in the rules or on this character. A caller repairing it "
                + "writes into the wrong collection and gets the same finding back.");
        }
    }

    /// <summary>
    /// <b>The thing a Pro or Con sits on has to be findable, and nothing said so.</b>
    /// <c>OwnerId</c> is the whole reason a caller told "this Pro is wrong" can go and change it,
    /// and it was asserted in three spot tests — which left the two sites they do not cover free
    /// to hold the printed <em>name</em> instead. That is not hypothetical: the validator's own
    /// comment records it happening once already, when a caller told a Pro was wrong on
    /// "Super Senses — Thermal Vision" got a display string and nothing it could look up.
    /// </summary>
    [Theory]
    [MemberData(nameof(Cases))]
    public void AnOwnerIsNamedBySomethingTheCallerCanLookUp(string which)
    {
        var sheet = Build(which);

        foreach (var issue in _f.Validator.Validate(sheet).Issues)
        {
            if (issue.OwnerId is not { } owner) continue;

            // A Power by id, a piece of gear by name — gear has nothing else — or an Ability by
            // id, which are the three things a Pro or Con can sit on.
            var findable =
                _f.Rules.GetPower(owner) is not null
                || sheet.SelectedPowers.Any(p => p.PowerId == owner)
                || sheet.Gear.Any(g => g.Name == owner)
                || _f.Rules.GetAbility(owner) is not null
                || sheet.AbilityModifiers.ContainsKey(owner)

                // A vehicle or a base carrying a Chapter 6 feature, by name — they have no id
                // either, for the same reason gear does not.
                || sheet.Vehicles.Any(v => v.Name == owner)
                || sheet.Headquarters.Any(h => h.Name == owner)

                // A Gadget's own Power, by id: UNKNOWN_GADGET_POWER names the Gadget as the
                // subject and the Power it could not price as the owner. Its Abilities and
                // Talents are the same shape — the key on the Gadget's own dictionary is what a
                // caller repairing UNKNOWN_GADGET_ABILITY writes over.
                || sheet.Gadgets.Any(g => g.Powers.Any(p => p.PowerId == owner)
                                          || g.AbilityRanks.ContainsKey(owner)
                                          || g.TalentRanks.ContainsKey(owner));

            Assert.True(findable,
                $"{issue.Code} says its subject sits on '{owner}', which is not a Power, a piece "
                + "of this character's gear, or an Ability. A printed name here is a display "
                + "string the caller cannot look anything up by.");
        }
    }

    /// <summary>Every Pro and Con on a character, wherever it sits.</summary>
    private static IEnumerable<SelectedProCon> EveryChoiceOn(CharacterSheet sheet) =>
        sheet.SelectedPowers.SelectMany(p => p.Pros.Concat(p.Cons))
            .Concat(sheet.Gear.SelectMany(g => g.Pros.Concat(g.Cons)))
            .Concat(sheet.AbilityModifiers.Values.SelectMany(m => m ?? []))
            .Where(c => c is not null);

    /// <summary>
    /// <b>Value and limit the right way round.</b> Swapping them is the likeliest way this
    /// structure goes wrong and the hardest to see: the report still reads plausibly, and a
    /// repair loop moves the character further from legal on every pass.
    /// </summary>
    [Theory]
    [MemberData(nameof(Cases))]
    public void AValueBreachesItsLimitInTheDirectionTheCodeNames(string which)
    {
        foreach (var issue in IssuesFor(which))
        {
            if (issue.Value is not { } value || issue.Limit is not { } limit) continue;

            if (issue.Code.Contains("MIN_NOT_MET", StringComparison.Ordinal)
                // Any "below" code, rather than the two suffixes this used to name. Chapter 6
                // prints a floor that is neither — a Mecha's Might below half its Body — and
                // spelling the suffixes out would have exempted it from the direction check
                // silently, which is the shape of hole this whole file exists to close.
                || issue.Code.Contains("_BELOW_", StringComparison.Ordinal)
                || issue.Code.StartsWith("NEGATIVE_", StringComparison.Ordinal)
                || issue.Code.EndsWith("WITHOUT_UNITS", StringComparison.Ordinal))
                Assert.True(value < limit, $"{issue.Code}: {value} is not below its minimum of {limit}.");
            else if (issue.Code.EndsWith("AT_MINIMUM", StringComparison.Ordinal))
                Assert.Equal(limit, value);      // already at the floor; that is the finding
            else
                Assert.True(value > limit, $"{issue.Code}: {value} does not exceed its limit of {limit}.");
        }
    }

    /// <summary>
    /// An option is a value the caller writes back into the character, so every one of them
    /// has to be a value the data will accept <b>in the field this code is about</b>.
    ///
    /// <para><b>Asking only whether a string is an id of anything was not that.</b> It was a
    /// union over ten collections, which every id in the rules satisfies — so
    /// <c>POWER_WITHOUT_SOURCE</c> could offer the six Ability ids and pass. A repair loop
    /// following that writes <c>"agility"</c> into the Power's Source, gets
    /// <c>UNKNOWN_SOURCE</c> back, and never terminates. Offering an id from the wrong
    /// collection is worse than offering nothing, because it looks actionable — so what each
    /// code offers is written down per code below.</para>
    /// </summary>
    [Theory]
    [MemberData(nameof(Cases))]
    public void EveryOptionOfferedIsOneTheRulesAccept(string which)
    {
        foreach (var issue in IssuesFor(which))
        {
            foreach (var option in issue.Options)
            {
                Assert.False(string.IsNullOrWhiteSpace(option), $"{issue.Code} offers a blank option.");

                var accepted = IsAnOptionThisCodeCanOffer(issue, option);

                Assert.True(accepted is not null,
                    $"{issue.Code} offers options and nothing here says what kind they are, so "
                    + "any id in the rules would satisfy this test. Add it to "
                    + "IsAnOptionThisCodeCanOffer.");

                Assert.True(accepted.Value,
                    $"{issue.Code} offers '{option}', which is not a value the field it is about "
                    + "accepts. A caller writing it back gets another finding, not a repair.");
            }
        }
    }

    /// <summary>
    /// What each code's options are, per code. Null means no code says — which fails, because a
    /// list of options nobody has characterised is the hole this replaced.
    /// </summary>
    private bool? IsAnOptionThisCodeCanOffer(ValidationIssue issue, string option) => issue.Code switch
    {
        "NO_TIER_SELECTED" or "UNKNOWN_TIER" => _f.Rules.GetTier(option) is not null,

        "UNKNOWN_PACKAGE" => _f.Rules.CreationRules.OptionalPackages.Any(p => p.Id == option),

        // Chapter 6's two feature tables. The whole list for an id that resolves to nothing, and
        // the feature's own grades for one that needs a grade — the two shapes Options takes.
        "UNKNOWN_ASSET_FEATURE" =>
            _f.Rules.Assets.FindVehicleFeature(option) is not null
            || _f.Rules.Assets.FindBaseFeature(option) is not null,

        "ASSET_FEATURE_NEEDS_GRADE" => issue.SubjectId is { } featureId
            && ((_f.Rules.Assets.FindVehicleFeature(featureId)?.CostRange?.ContainsKey(option) ?? false)
                || (_f.Rules.Assets.FindBaseFeature(featureId)?.CostRange?.ContainsKey(option) ?? false)),

        "UNKNOWN_CAMPAIGN_ASSET_KIND" =>
            CampaignAssetContribution.Kinds.Contains(option, StringComparer.Ordinal),

        "UNKNOWN_VARIANT_KIND" => CharacterVariant.Kinds.Contains(option, StringComparer.Ordinal),

        "FLAW_MIN_NOT_MET" or "UNKNOWN_FLAW" => _f.Rules.GetFlaw(option) is not null,

        "UNKNOWN_PERK" => _f.Rules.GetPerk(option) is not null,

        "UNKNOWN_ABILITY" or "MODIFIER_ON_UNBOUGHT_ABILITY" or "UNKNOWN_GADGET_ABILITY"
            => _f.Rules.GetAbility(option) is not null,

        "UNKNOWN_TALENT" or "UNKNOWN_GADGET_TALENT" => _f.Rules.GetTalent(option) is not null,

        "UNKNOWN_GEAR_FEATURE" => _f.Rules.GetGearFeature(option) is not null,

        // The three Source findings all offer the six Sources, wherever the Source sits.
        "UNKNOWN_SOURCE" or "POWER_WITHOUT_SOURCE" or "RANKLESS_POWER_WITHOUT_SOURCE"
            => _f.Rules.GetSource(option) is not null,

        // The exception: this one is about the Trait the Source was recorded against, not the
        // Source, so it offers Traits — and which kind depends on which dictionary it came from.
        "UNKNOWN_TRAIT_SOURCE" => issue.SubjectKind == ValidationSubject.Ability
            ? _f.Rules.GetAbility(option) is not null
            : _f.Rules.GetTalent(option) is not null,

        // Keys rather than ids, and they have to be keys of *this* subject's own range.
        "POWER_VARIANT_NOT_CHOSEN" or "PRO_VARIANT_NOT_CHOSEN" or "CON_VARIANT_NOT_CHOSEN"
            or "GEAR_FEATURE_NEEDS_GRADE" => IsAVariantKey(issue, option),

        _ => null
    };

    /// <summary>
    /// The one kind of option with no printed name of its own: a key inside a Power's cost
    /// variants or a gear feature's grades. It has to be a key of <em>this</em> subject's
    /// range rather than any key anywhere.
    /// </summary>
    private bool IsAVariantKey(ValidationIssue issue, string option)
    {
        if (issue.SubjectId is null) return false;

        var variants = _f.Rules.GetPower(issue.SubjectId)?.CostVariants?.Keys;
        var grades   = _f.Rules.GetGearFeature(issue.SubjectId)?.CostRange?.Keys;
        var proRange = _f.Rules.GetPro(issue.SubjectId)?.CostModifierRange?.Keys;
        var conRange = _f.Rules.GetCon(issue.SubjectId)?.CostModifierRange?.Keys;

        // A Pro or Con printed inside a Power's own entry keeps its grades there, so the owner
        // is where to look. This is why OwnerId has to be an id: with the printed name in it
        // there was nothing to look the Power up by.
        var owner = issue.OwnerId is null ? null : _f.Rules.GetPower(issue.OwnerId);
        var specific = owner?.PowerPros.Concat(owner.PowerCons)
            .FirstOrDefault(x => x.Id == issue.SubjectId);

        return (variants?.Contains(option) ?? false)
            || (grades?.Contains(option) ?? false)
            || (proRange?.Contains(option) ?? false)
            || (conRange?.Contains(option) ?? false)
            || (specific?.CostModifierRange?.ContainsKey(option) ?? false)
            || (specific?.CostPerRankRange?.ContainsKey(option) ?? false);
    }
}
