using ProwlersAndParagonsAutomation.Engine.Models;

namespace ProwlersAndParagonsAutomation.Engine;

public enum ValidationSeverity { Error, Warning }

/// <summary>
/// What kind of thing an issue is about, so a caller can find it without parsing the
/// sentence. <see cref="ValidationSubject.Character"/> is the whole sheet — a budget or a
/// flaw count belongs to no single Trait.
/// </summary>
public enum ValidationSubject
{
    None,
    Character,
    Tier,
    Ability,
    Talent,
    Power,
    Gear,
    GearFeature,
    Flaw,

    /// <summary>A vehicle this character owns outright (Ch.6 pp.94-100).</summary>
    Vehicle,

    /// <summary>A headquarters this character owns outright (Ch.6 pp.100-103).</summary>
    Headquarters,

    /// <summary>A Gadget built under p.94 — the one thing here that pays Hero Points out.</summary>
    Gadget,

    /// <summary>
    /// A feature bought for a vehicle or a base. One kind for both tables, because the finding
    /// carries the owner's name and the owner is what says which table it came off — the same
    /// relationship <see cref="GearFeature"/> has to <see cref="Gear"/>.
    /// </summary>
    AssetFeature
}

/// <summary>
/// One finding. <see cref="Message"/> is the whole of it for a person — every message is a
/// sentence a player can act on, and there is a test that says so.
///
/// <para><b>The rest is for a caller that has to act on it without reading English.</b> A
/// sentence is enough for a human and not for a repair loop: "Intellect is 40d, above the
/// Trait Cap of 12d" would have to be parsed back into the three facts it was built from.
/// So the facts travel beside it — which Trait, what it is, what it may be, and where a fix
/// has to be chosen from a fixed set.</para>
///
/// <para>All of it is optional and every property has a null or empty default, so the
/// constructor call is unchanged wherever a sentence was all that was wanted, and no message
/// moved. An issue that fills none of them is not defective; some findings genuinely have
/// nothing to locate.</para>
/// </summary>
public record ValidationIssue(ValidationSeverity Severity, string Code, string Message)
{
    /// <summary>What kind of thing <see cref="SubjectId"/> names.</summary>
    public ValidationSubject SubjectKind { get; init; } = ValidationSubject.None;

    /// <summary>
    /// The id of the thing at fault — an ability, talent, power, flaw or gear feature id, or
    /// a piece of gear's name, which is all gear has. Null when the subject is the whole
    /// character.
    /// </summary>
    public string? SubjectId { get; init; }

    /// <summary>
    /// The thing the subject sits on, where the subject is not top-level: the Power carrying a
    /// Pro, or the piece of gear carrying a feature. Without it a caller can find the feature
    /// id and not the item to change it on.
    ///
    /// <para><b>An id, not a printed name</b> — except for gear, which has only a name. It held
    /// the Power's printed name at first, so a caller told a Pro was wrong on "Super Senses —
    /// Thermal Vision" had a display string and nothing it could look up.</para>
    /// </summary>
    public string? OwnerId { get; init; }

    /// <summary>What the character has: a rank, a total cost, a count of flaws.</summary>
    public int? Value { get; init; }

    /// <summary>What the rules allow, in the same unit as <see cref="Value"/>.</summary>
    public int? Limit { get; init; }

    /// <summary>
    /// The values a fix has to be chosen from, where the rules fix the set: the cost
    /// variants of a Power, the grades of a gear feature, the six Sources. Empty when the
    /// fix is a number rather than a choice.
    /// </summary>
    public IReadOnlyList<string> Options { get; init; } = [];
}

public record ValidationResult(IReadOnlyList<ValidationIssue> Issues)
{
    public bool IsValid => Issues.All(i => i.Severity != ValidationSeverity.Error);

    public IEnumerable<ValidationIssue> Errors =>
        Issues.Where(i => i.Severity == ValidationSeverity.Error);

    public IEnumerable<ValidationIssue> Warnings =>
        Issues.Where(i => i.Severity == ValidationSeverity.Warning);
}

/// <summary>
/// Validates a CharacterSheet against all rules constraints.
/// Returns a ValidationResult with any errors and warnings found.
/// </summary>
public sealed class CharacterValidator
{
    private readonly RulesRepository _rules;
    private readonly CostCalculator _costs;
    private readonly DerivedStatsCalculator _derived;

    public CharacterValidator(
        RulesRepository rules,
        CostCalculator costs,
        DerivedStatsCalculator derived)
    {
        _rules   = rules;
        _costs   = costs;
        _derived = derived;
    }

    public ValidationResult Validate(CharacterSheet sheet)
    {
        var issues = new List<ValidationIssue>();

        CheckTierSelected(sheet, issues);
        CheckPackage(sheet, issues);

        var tier = sheet.SelectedTierId is not null
            ? _rules.GetTier(sheet.SelectedTierId)
            : null;

        // Selections must be checked before anything that prices a power: a power
        // missing its cost variant or nominated Trait cannot be costed at all, and
        // asking for its cost would throw instead of reporting the gap.
        CheckPowerRanks(sheet, issues);
        CheckQuantities(sheet, issues);
        var selectionsResolvable = CheckPowerSelections(sheet, issues);

        // Pros and Cons are priced the same way wherever they sit, so they are checked in
        // one place for all three: Powers, gear, and Abilities.
        var modifiersResolvable = CheckModifiers(sheet, issues);
        var perksResolvable     = CheckPerks(sheet, issues);

        // Same reason, for gear: an unknown feature or a graded one with no grade cannot
        // be priced, so the gap has to be reported before anything asks for a total.
        var gearResolvable = CheckGear(sheet, issues, modifiersResolvable);

        // Same reason again, for Chapter 6's other three: a feature the rulebook does not have, or
        // a graded one with no grade, cannot be priced — and VehiclePointsSpent throws on either
        // rather than guessing, which is what makes reporting first load-bearing.
        // Its own budget checks are gated internally rather than by the caller: unlike the Hero
        // Point total, a vehicle's Vehicle Point total is per machine, so one unpriceable feature
        // silences that machine alone and not the sheet.
        CheckAssets(sheet, issues, modifiersResolvable);

        // Outside the tier block on purpose: a house cap below 1d is nonsense whether or not the
        // character has found a tier yet, and half of what this reports needs no tier to say.
        CheckHouseTraitCap(sheet, tier, issues);

        // Also outside it, and for the same reason: a table's price for Immortality is bounded by
        // the Power's own entry rather than by any tier, so there is nothing here a missing tier
        // would make unanswerable.
        CheckHouseImmortalityCost(sheet, issues);

        if (tier is not null)
        {
            if (selectionsResolvable && modifiersResolvable && perksResolvable && gearResolvable)
                CheckHpBudget(sheet, tier, issues);
            CheckTraitCap(sheet, tier, issues);
            CheckIconicTier(tier, issues);
        }

        CheckTraitIds(sheet, issues);
        CheckTraitMinimums(sheet, issues);
        CheckPackageFloors(sheet, issues);
        CheckFlawCount(sheet, issues);
        CheckFlawIds(sheet, issues);
        CheckDuplicateFlaws(sheet, issues);
        CheckDuplicatePowers(sheet, issues);
        // Both flags: this prices each Power, and a Pro or Con the rulebook does not have
        // throws from there just as surely as a missing cost variant does.
        if (selectionsResolvable && modifiersResolvable) CheckPowerCosts(sheet, issues);
        CheckUnverifiedPowers(sheet, issues);
        CheckSources(sheet, issues);
        CheckVariant(sheet, issues);

        return new ValidationResult(issues);
    }

    // ── Checks ────────────────────────────────────────────────────────────

    /// <summary>
    /// <para><b>An unknown tier is an error, not a shrug.</b> Every check that depends on the
    /// tier — the Hero Point budget and the Trait Cap, which is to say both of the limits a
    /// character can break — is skipped when the tier cannot be found, so a misspelling used
    /// to produce a character with a 99d Ability and no findings at all. That is the worst
    /// answer this validator can give: legal, confidently, about something that is not.</para>
    /// </summary>
    private void CheckTierSelected(CharacterSheet sheet, List<ValidationIssue> issues)
    {
        if (sheet.SelectedTierId is null)
        {
            issues.Add(new(ValidationSeverity.Error, "NO_TIER_SELECTED",
                "No tier has been chosen. The tier sets the Hero Point budget and the Trait "
                + "Cap, so nothing else can be checked until it is.")
            {
                SubjectKind = ValidationSubject.Character,
                Options     = TierIds
            });
        }
        else if (_rules.GetTier(sheet.SelectedTierId) is null)
        {
            issues.Add(new(ValidationSeverity.Error, "UNKNOWN_TIER",
                $"There is no tier called '{sheet.SelectedTierId}' in the rulebook data. "
                + "Without a tier there is no Hero Point budget and no Trait Cap, so neither "
                + "can be checked.")
            {
                // The offending id, not just "the character": a caller should not have to
                // read it back out of a different part of the report to know what to replace.
                SubjectKind = ValidationSubject.Tier,
                SubjectId   = sheet.SelectedTierId,
                Options     = TierIds
            });
        }
    }

    /// <summary>
    /// The starting package, which is optional and so is only ever wrong by being unknown.
    ///
    /// <para>An unrecognised one costs nothing and grants nothing, so it was silently no
    /// package at all. Two things follow, and the second is the larger: the character loses the
    /// package's discount — 4 Hero Points on the Superhero Package, 2 on the Hero, 1 on the
    /// Civilian — and, because a package's ranks are implicit, it loses <em>the ranks
    /// themselves</em> unless they were also written out by hand.</para>
    /// </summary>
    private void CheckPackage(CharacterSheet sheet, List<ValidationIssue> issues)
    {
        if (sheet.SelectedPackageId is null) return;

        var packages = _rules.CreationRules.OptionalPackages;
        if (packages.Any(p => p.Id == sheet.SelectedPackageId)) return;

        issues.Add(new(ValidationSeverity.Error, "UNKNOWN_PACKAGE",
            $"There is no starting package called '{sheet.SelectedPackageId}' in the rulebook "
            + "data. Leave it unset for a character who took none.")
        {
            SubjectKind = ValidationSubject.Character,
            SubjectId   = sheet.SelectedPackageId,
            Options     = packages.Select(p => p.Id).ToList()
        });
    }

    /// <summary>
    /// <para><b>The <c>try</c> is the backstop, and it is deliberately not a substitute for
    /// the checks above.</b> Each of those reports a gap a caller can act on; this one only
    /// promises that <see cref="Validate"/> returns findings rather than throwing. The list of
    /// things the engine refuses to price is not knowable from here — it grows every time
    /// somebody adds a field — and a validator that throws is worse than one that says it
    /// cannot answer, because the caller loses every other finding with it.</para>
    ///
    /// <para>This caught ten submitted shapes at once when it was added: unknown Pro, Con,
    /// Perk and nominated-Trait ids, and wrong-but-present variant and grade keys. All ten are
    /// now reported by name as well, which is what a repair loop needs; if an eleventh appears
    /// it is reported here instead, imprecisely but without taking the run down.</para>
    /// </summary>
    private void CheckHpBudget(CharacterSheet sheet, TierModel tier, List<ValidationIssue> issues)
    {
        int total;
        try
        {
            total = _costs.TotalCost(sheet);
        }
        catch (Exception e) when (e is InvalidOperationException or KeyNotFoundException
                                    or ArgumentException or NullReferenceException
                                    or FormatException or OverflowException)
        {
            issues.Add(new(ValidationSeverity.Error, "CHARACTER_NOT_PRICEABLE",
                "Something on this character has no cost the rulebook can supply, so the "
                + "Hero Point total cannot be worked out. The other findings say what.")
            {
                SubjectKind = ValidationSubject.Character
            });
            return;
        }

        if (total > tier.HeroPoints)
            issues.Add(new(ValidationSeverity.Error, "HP_BUDGET_EXCEEDED",
                $"Character costs {total} HP but the {tier.Name} tier budget is {tier.HeroPoints} HP " +
                $"({total - tier.HeroPoints} HP over).")
            {
                SubjectKind = ValidationSubject.Character,
                Value       = total,
                Limit       = tier.HeroPoints
            });
    }

    /// <summary>
    /// <b>The house Trait Cap itself, before anything is measured against it.</b>
    /// <see cref="CharacterSheet.TraitCapRank"/> is a tighter ceiling a table has imposed —
    /// Pinnacle City's 6d for a non-superhuman — and the two ways of writing one down that
    /// cannot mean what they say are reported here.
    ///
    /// <para><b>Reported, and still used.</b> Both findings leave
    /// <see cref="DerivedStatsCalculator.EffectiveTraitCap"/> answering the number as written, so
    /// the Resolve and the cap findings beside these agree with the character's own file. Clamping
    /// would be the engine making a design decision about somebody's game, and would hide the
    /// mistake behind figures that look ordinary.</para>
    /// </summary>
    private static void CheckHouseTraitCap(
        CharacterSheet sheet, TierModel? tier, List<ValidationIssue> issues)
    {
        if (sheet.TraitCapRank is not { } house) return;

        // No Trait can be lower than 1d (Ch.2), so a cap under 1d is a ceiling below the floor:
        // every one of the eighteen Traits breaks it and no legal character can be built to it.
        if (house < 1)
            issues.Add(new(ValidationSeverity.Error, "TRAIT_CAP_BELOW_MINIMUM",
                $"This character is built to a house Trait Cap of {house}d, and no Ability or "
                + "Talent can be lower than 1d — so no legal character fits under it.")
            {
                SubjectKind = ValidationSubject.Character,
                Value       = house,
                Limit       = 1
            });

        // A house cap is a table tightening the tier's ceiling. Above it, it is not a house rule
        // at all — it is a character quietly playing above the power level everybody agreed on,
        // and it raises Resolve as well as the ranks, which is the half nobody would notice.
        if (tier is not null && house > tier.TraitCapRank)
            issues.Add(new(ValidationSeverity.Error, "TRAIT_CAP_ABOVE_TIER",
                $"This character is built to a house Trait Cap of {house}d, above the "
                + $"{tier.Name} tier's {tier.TraitCapRank}d. A house cap tightens the tier's "
                + $"ceiling and never loosens it. The figures here still use {house}d, as "
                + "written — lower it or raise the tier.")
            {
                SubjectKind = ValidationSubject.Character,
                Value       = house,
                Limit       = tier.TraitCapRank
            });
    }

    /// <summary>
    /// <b>The price a table charges for Immortality, against the range Immortality's own entry
    /// prints.</b>
    ///
    /// <para>Ch.2 p.31 prices the Power at 3 Hero Points and then hands the price to the GM: "In a
    /// game where Heroes can die, GMs should charge more for this — somewhere between 6 and 12
    /// Hero Points." That range is <c>campaign_cost_min</c> and <c>campaign_cost_max</c> on the
    /// entry, and it is read from there rather than written here — a bound spelled in C# would be
    /// a rule this project had invented, unreachable by the audit that holds every other price to
    /// a page.</para>
    ///
    /// <para><b>The other half of this — a house price on a character that belongs to no game — is
    /// deliberately not here.</b> A house price is a fact about a <em>table</em>, so a sheet
    /// carrying one and naming no game has been hand-edited, or has left a campaign without the
    /// price going with it. Saying so means reading the field that names the game, and no rules
    /// code may read that field at all: <c>PresentationFlagsTests</c> bars it outright — it is an
    /// indirection, and the only thing rules code could do with one is resolve it, which means
    /// storage. So <c>CampaignJoin.Inspect</c> in <c>web/</c> reports
    /// <c>IMMORTALITY_COST_WITHOUT_CAMPAIGN</c> instead, beside the tier and cap mismatches, where
    /// every other finding about a character's relationship to its game already lives. <b>That is
    /// not a workaround.</b> This method judges a price, which is the engine's business; that one
    /// judges a membership, which is a host's — the same line that already runs between
    /// <c>TRAIT_CAP_ABOVE_TIER</c> here and <c>CAMPAIGN_TRAIT_CAP_MISMATCH</c> there.</para>
    ///
    /// <para><b>Charged as written either way.</b> The engine reports and does not repair, and
    /// clamping would be worse here than usual: the cost would then look right on every screen
    /// while the campaign's actual setting said something else.</para>
    ///
    /// <para><b>Silent on a character that carries no house price</b>, which is every character
    /// stored before this existed, and silent on one that does not have the Power — the price is
    /// still wrong, and saying so under a character it costs nothing is a finding a reader cannot
    /// act on. It is the campaign's screen that reports a bad price to the GM who set it.</para>
    /// </summary>
    private void CheckHouseImmortalityCost(CharacterSheet sheet, List<ValidationIssue> issues)
    {
        if (sheet.ImmortalityCost is not { } house) return;

        var power = _rules.GetPower(ImmortalityId);

        // A rules file that has lost the entry is not this check's business to report — the
        // Power's own id is checked where every other unknown id is — and inventing a range to
        // judge against would be exactly the figure-in-C# this reads the data to avoid.
        if (power is not { CampaignCostMin: { } min, CampaignCostMax: { } max }) return;

        if (house < min || house > max)
        {
            issues.Add(new(ValidationSeverity.Error, "IMMORTALITY_COST_OUTSIDE_RANGE",
                $"This character's table charges {house} Hero Points for {power.Name}, and the "
                + $"rulebook puts a table's price between {min} and {max}. The figures here still "
                + $"use {house}, as written — change the campaign's setting.")
            {
                SubjectKind = ValidationSubject.Power,
                SubjectId   = power.Id,
                Value       = house,
                Limit       = max
            });
        }
    }

    /// <summary>
    /// The two structural things this engine can say about a
    /// <see cref="CharacterSheet.Variant"/> without seeing the roster: whether its kind is one of
    /// the three this app knows, and whether it names a root at all.
    ///
    /// <para><b>Whether that root is actually held by anybody is not this method's question.</b>
    /// The engine cannot see a roster at all — an id here could point at a live character or at
    /// nothing, and answering that means asking storage — so <c>VARIANT_ROOT_NOT_HELD</c> is a
    /// browser-side finding, the same shape as <c>UNKNOWN_CAMPAIGN</c>. A cycle (A of B, B of A)
    /// or a self-link is refused where the link is made rather than reported after the fact, for
    /// the same reason: repairing one here would mean walking every other character's own
    /// <see cref="CharacterSheet.Variant"/>, which is exactly the roster this method cannot
    /// see.</para>
    /// </summary>
    private static void CheckVariant(CharacterSheet sheet, List<ValidationIssue> issues)
    {
        if (sheet.Variant is not { } variant) return;

        if (string.IsNullOrWhiteSpace(variant.OfCharacterId))
            issues.Add(new(ValidationSeverity.Error, "VARIANT_WITHOUT_ROOT",
                "This character is recorded as a version of another one, but names no root "
                + "character. Name which character this is a version of, or clear the link.")
            {
                SubjectKind = ValidationSubject.Character
            });

        if (!CharacterVariant.Kinds.Contains(variant.Kind, StringComparer.Ordinal))
            issues.Add(new(ValidationSeverity.Error, "UNKNOWN_VARIANT_KIND",
                $"'{variant.Kind}' is not a kind of version this app knows. Choose one of the "
                + "kinds below, or clear the link.")
            {
                SubjectKind = ValidationSubject.Character,
                SubjectId   = variant.Kind,
                Options     = CharacterVariant.Kinds
            });
    }

    /// <summary>
    /// The one Power whose printed entry hands its price to the table. Named once, because a
    /// second spelling of it is how the check above and the cost calculation come to disagree
    /// about which Power they are talking about.
    /// </summary>
    private const string ImmortalityId = "immortality";

    /// <summary>
    /// Every Trait against the ceiling this character is built to — <b>the house cap where it
    /// has one, and the tier's otherwise</b>, which is
    /// <see cref="DerivedStatsCalculator.EffectiveTraitCap"/> and must not be spelled out a
    /// second time here. The same figure decides Resolve, and the two disagreeing would report a
    /// Trait as legal while paying it Resolve for room it does not have.
    /// </summary>
    private void CheckTraitCap(CharacterSheet sheet, TierModel tier, List<ValidationIssue> issues)
    {
        var cap = DerivedStatsCalculator.EffectiveTraitCap(sheet, tier) ?? tier.TraitCapRank;

        // Every one of these names the Trait the way the rulebook prints it. They used to
        // print the id — "Ability 'intellect'", "Power 'super_senses_thermal_vision'" — at a
        // player holding a book that calls them Intellect and Super Senses — Thermal Vision.
        foreach (var (id, rank) in sheet.AbilityRanks)
            if (rank > cap)
                issues.Add(new(ValidationSeverity.Error, "TRAIT_ABOVE_CAP",
                    $"{TraitName(_rules.GetAbility(id)?.Name, id)} is {rank}d, above the Trait Cap of {cap}d.")
                {
                    SubjectKind = ValidationSubject.Ability,
                    SubjectId   = id,
                    Value       = rank,
                    Limit       = cap
                });

        foreach (var (id, rank) in sheet.TalentRanks)
            if (rank > cap)
                issues.Add(new(ValidationSeverity.Error, "TRAIT_ABOVE_CAP",
                    $"{TraitName(_rules.GetTalent(id)?.Name, id)} is {rank}d, above the Trait Cap of {cap}d.")
                {
                    SubjectKind = ValidationSubject.Talent,
                    SubjectId   = id,
                    Value       = rank,
                    Limit       = cap
                });

        foreach (var sp in sheet.SelectedPowers)
        {
            // A Power the rules do not have cannot have an effective rank, and asking for
            // one throws. CheckPowerSelections reports the unknown id; this must not turn
            // that report into a crash on the way past. The same ordering trap was fixed
            // for gear once already.
            if (Power(sp.PowerId) is null) continue;

            var effective = _derived.GetEffectiveRank(sp, sheet);
            if (effective > cap)
                // Value is the rank the Power *reaches*, not the ranks bought for it: 27
                // Powers take a free baseline from another Trait and purchased ranks stack on
                // top, so a caller shedding the difference has to take it off the purchased
                // ranks — and a Power already over the cap on its baseline alone cannot be
                // fixed here at all, only by lowering the Trait it derives from.
                issues.Add(new(ValidationSeverity.Error, "TRAIT_ABOVE_CAP",
                    $"{PowerName(sp.PowerId)} reaches {effective}d, above the Trait Cap of {cap}d.")
                {
                    SubjectKind = ValidationSubject.Power,
                    SubjectId   = sp.PowerId,
                    Value       = effective,
                    Limit       = cap
                });
        }
    }

    private static void CheckIconicTier(TierModel tier, List<ValidationIssue> issues)
    {
        // The message used to say the tier "is marked needs_review", which was both jargon
        // and untrue — nothing in data/rules/ carries such a flag, and this fires on the
        // Iconic tier's id regardless. What it is actually reporting is the rulebook's own
        // open end: Ch.2 p.15 calls Iconic's 200 Hero Points a bare minimum.
        if (tier.NeedsReview || tier.Id == "iconic")
            issues.Add(new(ValidationSeverity.Warning, "ICONIC_TIER_OPEN_BUDGET",
                $"The {tier.Name} tier's Hero Point budget is a minimum rather than a limit " +
                "(Ch.2, Power Level, p.15), so how far above it you go is the GM's call.")
            {
                // No Limit. Iconic's 200 is a floor, and Limit is documented and used
                // everywhere else as a ceiling something has breached from above — a repair
                // loop reading it uniformly would treat this tier's minimum as its maximum
                // and shrink a character to meet it.
                SubjectKind = ValidationSubject.Tier,
                SubjectId   = tier.Id
            });
    }

    private void CheckFlawCount(CharacterSheet sheet, List<ValidationIssue> issues)
    {
        var flawRules = _rules.CreationRules.FlawRules;
        var count = sheet.Flaws.Count;

        if (count < flawRules.MinAtCreation)
            issues.Add(new(ValidationSeverity.Error, "FLAW_MIN_NOT_MET",
                $"A character needs at least {Flaws(flawRules.MinAtCreation)} at creation, "
                + $"and this one has {count}.")
            {
                SubjectKind = ValidationSubject.Character,
                Value       = count,
                Limit       = flawRules.MinAtCreation,
                Options     = _rules.Flaws.Select(f => f.Id).ToList()
            });

        if (count > flawRules.MaxAtCreation)
            issues.Add(new(ValidationSeverity.Error, "FLAW_MAX_EXCEEDED",
                $"A character may take at most {Flaws(flawRules.MaxAtCreation)} at creation, "
                + $"and this one has {count}. Each one beyond that costs {flawRules.ExtraFlawCostHp} HP.")
            {
                SubjectKind = ValidationSubject.Character,
                Value       = count,
                Limit       = flawRules.MaxAtCreation
            });
    }

    /// <summary>
    /// Ranks bought against a Trait that does not exist.
    ///
    /// <para>There are six Abilities and twelve Talents and both dictionaries are keyed by
    /// id, so an id that is not one of the eighteen is a rank the character paid for and does
    /// not have. It was charged and then ignored: it counted towards the Hero Point total,
    /// was checked against the Trait Cap, and printed nowhere on the sheet — so the character
    /// was simply poorer than it looked, for nothing.</para>
    ///
    /// <para>The wizard cannot produce this, because it offers a list. A character written by
    /// hand or by another program can, and does — the skill's own example named a Talent the
    /// rulebook does not have, and nothing said so.</para>
    /// </summary>
    /// <summary>
    /// <b>Every Ability and every Talent has a minimum of 1d, and a character has all eighteen.</b>
    /// Ch.2 says it twice, once for each: "No Ability can have a rank lower than 1d or higher than
    /// the game's Trait Cap. Ordinary people have 2d in every Ability", and the same sentence again
    /// for Talents. So 0d is not a rank a character can hold — it is the absence of a Trait nobody
    /// can be without.
    ///
    /// <para>This was enforced nowhere. The Abilities editor already used a floor of 1d while a
    /// fresh sheet started every Ability at 0d, so the default sat below the floor and nothing
    /// objected; the Talents editor used a floor of 0d, which contradicts the book outright.</para>
    ///
    /// <para><b>It costs Hero Points, and the packages are the corroboration.</b> Without a package
    /// a character pays for all eighteen Traits at 1d, which is 18 HP to exist — and the Civilian
    /// Package is 35 HP for 2d in all eighteen, which is 36 points of ranks. That is the "small
    /// discount" the rulebook says a package is. All twenty published Heroes take a package, so
    /// every one of their Talents is covered by its floor, which is why rebuilding them never
    /// caught this.</para>
    /// </summary>
    private void CheckTraitMinimums(CharacterSheet sheet, List<ValidationIssue> issues)
    {
        foreach (var ability in _rules.Abilities)
            Report(ability.Id, ability.Name, sheet.GetAbilityRank(ability.Id), ValidationSubject.Ability);

        foreach (var talent in _rules.Talents)
            Report(talent.Id, talent.Name, sheet.GetTalentRank(talent.Id), ValidationSubject.Talent);

        void Report(string id, string name, int rank, ValidationSubject kind)
        {
            if (rank >= MinimumTraitRank) return;

            issues.Add(new(ValidationSeverity.Error, "TRAIT_BELOW_MINIMUM",
                $"{name} is {rank}d. No Ability or Talent can be lower than {MinimumTraitRank}d — "
                + "every character has all of them, and ordinary people have 2d in each.")
            {
                SubjectKind = kind,
                SubjectId   = id,
                Value       = rank,
                Limit       = MinimumTraitRank
            });
        }
    }

    private int MinimumTraitRank => _rules.CreationRules.TraitRankLimits.Minimum;

    /// <summary>
    /// <b>A package's granted ranks are a floor, not a starting offer.</b> Every package says so:
    /// "Cannot lower any of these below the package rank." A Trait recorded beneath it is not a
    /// cheaper character, it is an impossible one — and it costs nothing either way, because
    /// <c>AbilityCost</c> and <c>TalentCost</c> charge only for ranks above what the package
    /// covers, so the mistake is free and therefore silent.
    ///
    /// <para>This is the rule that proved Herald (Airmid)'s recorded package impossible, and it
    /// was a test over the twenty published Heroes before it was a check here. The samples then
    /// showed why it belongs here too: filling their Talents in at 1d looked right, sat below the
    /// 2d their Hero Package grants, and cost nothing.</para>
    /// </summary>
    private void CheckPackageFloors(CharacterSheet sheet, List<ValidationIssue> issues)
    {
        // Resolved here rather than through CostCalculator, whose own lookup is private and
        // should stay that way: this is a question about the rules, not about a price.
        if (sheet.SelectedPackageId is null) return;
        if (_rules.CreationRules.OptionalPackages
                .FirstOrDefault(p => p.Id == sheet.SelectedPackageId) is not { } package) return;

        foreach (var ability in _rules.Abilities)
            Report(ability.Id, ability.Name, sheet.GetAbilityRank(ability.Id),
                   package.AbilitiesRank, ValidationSubject.Ability);

        foreach (var talent in _rules.Talents)
            Report(talent.Id, talent.Name, sheet.GetTalentRank(talent.Id),
                   package.TalentsRank, ValidationSubject.Talent);

        void Report(string id, string name, int rank, int granted, ValidationSubject kind)
        {
            if (rank >= granted) return;

            issues.Add(new(ValidationSeverity.Error, "TRAIT_BELOW_PACKAGE",
                $"{name} is {rank}d, below the {granted}d the {package.Name} grants. A package "
                + "cannot be taken and then lowered below what it gives.")
            {
                SubjectKind = kind,
                SubjectId   = id,
                Value       = rank,
                Limit       = granted
            });
        }
    }

    private void CheckTraitIds(CharacterSheet sheet, List<ValidationIssue> issues)
    {
        foreach (var id in sheet.AbilityRanks.Keys.Where(id => _rules.GetAbility(id) is null))
            issues.Add(new(ValidationSeverity.Error, "UNKNOWN_ABILITY",
                $"Ranks are recorded against '{id}', which is not one of the six Abilities "
                + "in the rulebook.")
            {
                SubjectKind = ValidationSubject.Ability,
                SubjectId   = id,
                Options     = _rules.Abilities.Select(a => a.Id).ToList()
            });

        foreach (var id in sheet.TalentRanks.Keys.Where(id => _rules.GetTalent(id) is null))
            issues.Add(new(ValidationSeverity.Error, "UNKNOWN_TALENT",
                $"Ranks are recorded against '{id}', which is not one of the twelve Talents "
                + "in the rulebook.")
            {
                SubjectKind = ValidationSubject.Talent,
                SubjectId   = id,
                Options     = _rules.Talents.Select(t => t.Id).ToList()
            });
    }

    /// <summary>
    /// Ranks and quantities below zero.
    ///
    /// <para><b>A negative quantity on a per-unit Perk was worth unlimited Hero Points.</b>
    /// <c>PerkCost</c> multiplies a price by <c>Units</c> and the perk total has no floor, so
    /// <c>{"PerkId":"contacts","Units":-1000}</c> paid the character 1000 HP and the whole
    /// sheet reported legal at any size. That is the exact failure the headless command exists
    /// to prevent, reached through the one field nothing bounded.</para>
    ///
    /// <para>The ranks cannot be exploited the same way — costs floor at zero — but a Trait at
    /// −50d is not a character, and reporting it legal is the same wrong answer in a quieter
    /// voice. The wizard cannot produce any of this: it counts upwards from a menu.</para>
    ///
    /// <para><b>And it happened again the moment Chapter 6 added four collections.</b> A Perk
    /// allowance, a vehicle's three bought characteristics and a Gadget's Trait ranks are all the
    /// same unbounded field, and only <c>CampaignAssetContribution.HeroPoints</c> — the one that
    /// looks most like a spend — was checked. The other three each fund something for nothing.
    /// <b>The pattern to take from this is that a new collection needs a clause here</b>, and the
    /// way it stays true is that nothing here is derived: each clause is written out, so an
    /// absent one is visible.</para>
    /// </summary>
    private void CheckQuantities(CharacterSheet sheet, List<ValidationIssue> issues)
    {
        foreach (var (id, rank) in sheet.AbilityRanks.Where(a => a.Value < 0))
            issues.Add(Negative("NEGATIVE_RANK", ValidationSubject.Ability, id,
                $"{_rules.GetAbility(id)?.Name ?? $"'{id}'"} is {rank}d. A Trait cannot have "
                + "fewer than no ranks.", rank));

        foreach (var (id, rank) in sheet.TalentRanks.Where(t => t.Value < 0))
            issues.Add(Negative("NEGATIVE_RANK", ValidationSubject.Talent, id,
                $"{_rules.GetTalent(id)?.Name ?? $"'{id}'"} is {rank}d. A Trait cannot have "
                + "fewer than no ranks.", rank));

        foreach (var sp in sheet.SelectedPowers.Where(p => p.PurchasedRanks < 0))
            issues.Add(Negative("NEGATIVE_RANK", ValidationSubject.Power, sp.PowerId,
                $"{PowerName(sp.PowerId)} has {sp.PurchasedRanks} purchased ranks. A Power "
                + "cannot have fewer than no ranks.", sp.PurchasedRanks));

        foreach (var sp in sheet.SelectedPowers.Where(p => p.Units < 0))
            issues.Add(Negative("NEGATIVE_UNITS", ValidationSubject.Power, sp.PowerId,
                $"{PowerName(sp.PowerId)} is bought {sp.Units} times. A Power cannot be "
                + "bought fewer than no times.", sp.Units));

        foreach (var perk in sheet.Perks.Where(p => p.Units < 0))
            issues.Add(Negative("NEGATIVE_UNITS", ValidationSubject.Character, perk.PerkId,
                $"The perk '{Perk(perk.PerkId)?.Name ?? perk.PerkId}' is bought "
                + $"{perk.Units} times, which would pay the character Hero Points rather than "
                + "cost them.", perk.Units));

        // A Pro or Con carries a quantity too, and it was the one field of the five that
        // nothing looked at. A Power-specific Pro priced per rank per unit at a quantity of
        // −1000 drove the Power's rate to −498, which the rulebook floor caught at half a point
        // per rank — so a 24 HP Power cost 6 and nothing said a word.
        foreach (var (owner, choice) in EveryModifier(sheet).Where(m => m.Choice.Units < 0))
            issues.Add(Negative("NEGATIVE_UNITS", ValidationSubject.Character, choice.Id,
                $"{owner}'s '{choice.Id}' is applied {choice.Units} times, which would "
                + "discount it rather than charge for it.", choice.Units!.Value));

        // Chapter 6's four collections, which arrived after every clause above and were bounded
        // by none of them. Each is the same exploit in a new field:
        //
        //  • a vehicle's Body/Speed/Weapons below zero **pays Vehicle Points back**, and a machine
        //    with Body −20 buys twenty points of features for nothing, inside its budget, silent.
        //    Control is the one that may be negative — p.96 says so and floors it at −3, which
        //    CheckVehicle reports — so it is deliberately not here.
        //  • a Perk allowance below zero is Hero Points *paid to the character*: PerkHeroPoints
        //    −1000 took a legal sheet from 18 HP to −982, reported only as a Vehicle Point budget
        //    finding that says nothing about the thousand Hero Points. CampaignAssets' own
        //    HeroPoints was already checked; these two are the same field and were not.
        //  • a Gadget's Trait ranks below zero pay its pool back: `might: -50` beside a 20d Blast
        //    spends −30 out of a pool of six, which is inside it.
        //
        // Reported, never repaired, like every other quantity here: the totals stay what the sheet
        // says, which is what makes the finding worth printing beside them.
        foreach (var vehicle in sheet.Vehicles.Where(v => v.Name is not null))
        {
            foreach (var (what, rank) in new[]
                     { ("Body", vehicle.Body), ("Speed", vehicle.Speed), ("Weapons", vehicle.Weapons ?? 0) }
                         .Where(c => c.Item2 < 0))
                issues.Add(Negative("NEGATIVE_RANK", ValidationSubject.Vehicle, vehicle.Name,
                    $"{vehicle.Name} has {what} {rank}d. A vehicle's characteristics are bought "
                    + "from nothing, and a rank below that pays Vehicle Points back rather than "
                    + "costing them. Only Control may be negative.", rank));

            if (vehicle.PerkHeroPoints < 0)
                issues.Add(Negative("NEGATIVE_UNITS", ValidationSubject.Vehicle, vehicle.Name,
                    $"{vehicle.Name} records {vehicle.PerkHeroPoints} Hero Points of the Unique "
                    + "Vehicle Perk, which would pay the character rather than cost them.",
                    vehicle.PerkHeroPoints));
        }

        foreach (var hq in sheet.Headquarters.Where(h => h.Name is not null && h.PerkHeroPoints < 0))
            issues.Add(Negative("NEGATIVE_UNITS", ValidationSubject.Headquarters, hq.Name,
                $"{hq.Name} records {hq.PerkHeroPoints} Hero Points of the Headquarters Perk, "
                + "which would pay the character rather than cost them.", hq.PerkHeroPoints));

        foreach (var gadget in sheet.Gadgets.Where(g => g.Name is not null))
        {
            // A Gadget's Powers are the same two quantity fields the character's own Powers
            // carry, one budget down, and the clauses above walk `sheet.SelectedPowers` alone.
            foreach (var sp in gadget.Powers.Where(p => p.PurchasedRanks < 0))
                issues.Add(Negative("NEGATIVE_RANK", ValidationSubject.Gadget, gadget.Name,
                    $"{GadgetPowerName(gadget, sp)} has {sp.PurchasedRanks} purchased ranks. A "
                    + "Power cannot have fewer than no ranks.", sp.PurchasedRanks));

            foreach (var sp in gadget.Powers.Where(p => p.Units < 0))
                issues.Add(Negative("NEGATIVE_UNITS", ValidationSubject.Gadget, gadget.Name,
                    $"{GadgetPowerName(gadget, sp)} is bought {sp.Units} times. A Power cannot "
                    + "be bought fewer than no times.", sp.Units));

            foreach (var (id, rank) in gadget.AbilityRanks.Concat(gadget.TalentRanks).Where(e => e.Value < 0))
                issues.Add(new(ValidationSeverity.Error, "NEGATIVE_RANK",
                    $"{gadget.Name} has {rank}d of '{id}'. A Trait cannot have fewer than no "
                    + "ranks, and a negative one pays the Gadget's pool back rather than "
                    + "spending it.")
                {
                    SubjectKind = ValidationSubject.Gadget,
                    SubjectId   = gadget.Name,
                    OwnerId     = id,
                    Value       = rank,
                    Limit       = 0
                });
        }

        // Zero is not a purchase. A per-unit Perk or Power at no units costs nothing and does
        // nothing, so it is a line on the sheet the character did not buy.
        foreach (var perk in sheet.Perks.Where(p => p.Units == 0 && Perk(p.PerkId)?.CostType == "per_unit"))
            issues.Add(new(ValidationSeverity.Error, "PER_UNIT_WITHOUT_UNITS",
                $"The perk '{Perk(perk.PerkId)!.Name}' is priced by the unit and none "
                + "has been bought, so it would cost nothing and do nothing.")
            {
                SubjectKind = ValidationSubject.Character,
                SubjectId   = perk.PerkId,
                Value       = 0,
                Limit       = 1
            });

        foreach (var sp in sheet.SelectedPowers.Where(p => p.Units == 0 && Power(p.PowerId)?.CostType == "per_unit"))
            issues.Add(new(ValidationSeverity.Error, "PER_UNIT_WITHOUT_UNITS",
                $"{PowerName(sp.PowerId)} is priced by the unit and none has been bought, so it "
                + "would cost nothing and do nothing.")
            {
                SubjectKind = ValidationSubject.Power,
                SubjectId   = sp.PowerId,
                Value       = 0,
                Limit       = 1
            });
    }

    /// <summary>
    /// Every Pro and Con on the character with the name of whatever carries it, so a check that
    /// applies to all of them does not have to walk four collections itself.
    /// </summary>
    private IEnumerable<(string Owner, SelectedProCon Choice)> EveryModifier(CharacterSheet sheet)
    {
        foreach (var sp in sheet.SelectedPowers)
        {
            foreach (var p in sp.Pros.Concat(sp.Cons))
                if (p is not null) yield return (PowerName(sp.PowerId), p);
        }

        // A Gadget's Powers carry the same quantity field, and it buys the same discount one
        // budget down: `also_x` at −1000 units on a 12d Nullify inside a Gadget priced 24 Hero
        // Points of Power at 6, which is exactly a Complexity-3 pool, so the Gadget was inside
        // its pool with nothing said. The character's own Powers have reported this since the
        // per-rank-per-unit exploit was found; this collection arrived after that clause.
        foreach (var gadget in sheet.Gadgets)
        {
            foreach (var sp in gadget.Powers)
                foreach (var p in sp.Pros.Concat(sp.Cons))
                    if (p is not null) yield return (GadgetPowerName(gadget, sp), p);
        }

        foreach (var gear in sheet.Gear)
        {
            foreach (var p in gear.Pros.Concat(gear.Cons))
                if (p is not null) yield return (gear.Name, p);
        }

        foreach (var (abilityId, modifiers) in sheet.AbilityModifiers)
        {
            foreach (var p in modifiers ?? [])
                if (p is not null) yield return (_rules.GetAbility(abilityId)?.Name ?? abilityId, p);
        }
    }

    /// <summary>
    /// What to call a Power that is inside a Gadget: the Gadget names it, because "Nullify" alone
    /// in a finding is indistinguishable from the character's own and the two have separate
    /// budgets.
    /// </summary>
    private string GadgetPowerName(BuiltGadget gadget, SelectedPower power) =>
        $"{gadget.Name}'s {PowerName(power.PowerId)}";

    private static ValidationIssue Negative(
        string code, ValidationSubject kind, string id, string message, int value) =>
        new(ValidationSeverity.Error, code, message)
        {
            SubjectKind = kind,
            SubjectId   = id,
            Value       = value,
            Limit       = 0
        };

    /// <summary>
    /// Perks. The one top-level collection of ids that nothing checked: an unknown one threw
    /// out of <c>PerkCost</c> in the middle of totalling the character.
    /// </summary>
    /// <returns>False when a perk cannot be priced.</returns>
    private bool CheckPerks(CharacterSheet sheet, List<ValidationIssue> issues)
    {
        var resolvable = true;

        // `p.PerkId is null` first: GetPerk throws on a null id rather than answering, and a null
        // id is what a hand-written `{"Perks":[{}]}` produces. Reported by name, like every other
        // id the rulebook does not have — the alternative was the validator throwing and the whole
        // character coming back as merely "unusable".
        foreach (var perk in sheet.Perks.Where(p => p.PerkId is null || Perk(p.PerkId) is null))
        {
            issues.Add(new(ValidationSeverity.Error, "UNKNOWN_PERK",
                perk.PerkId is null
                    ? "One of the perks has no name at all."
                    : $"There is no perk called '{perk.PerkId}' in the rulebook data.")
            {
                SubjectKind = ValidationSubject.Character,
                SubjectId   = perk.PerkId,
                Options     = _rules.Perks.Select(p => p.Id).ToList()
            });
            resolvable = false;
        }

        return resolvable;
    }

    /// <summary>
    /// Every Pro and Con on the character, wherever it sits — on a Power, on a piece of gear,
    /// on an Ability, or on a Power inside a Gadget. They are priced the same way in all four
    /// places, so they go wrong the same way in all four, and an unknown id threw out of the
    /// middle of the total.
    ///
    /// <para>A Pro or Con printed inside a Power's own entry takes precedence over a generic
    /// one of the same name, exactly as <c>CostCalculator</c> resolves it. Checking only the
    /// generic list would report the eleven per-rank Power-specific ones as unknown.</para>
    /// </summary>
    /// <returns>False when a Pro or Con cannot be priced.</returns>
    private bool CheckModifiers(CharacterSheet sheet, List<ValidationIssue> issues)
    {
        var resolvable = true;

        foreach (var sp in sheet.SelectedPowers)
        {
            var power = Power(sp.PowerId);
            resolvable &= CheckModifierList(sp.Pros, isPro: true, power, sp.PowerId, PowerName(sp.PowerId), issues);
            resolvable &= CheckModifierList(sp.Cons, isPro: false, power, sp.PowerId, PowerName(sp.PowerId), issues);
        }

        foreach (var gear in sheet.Gear)
        {
            resolvable &= CheckModifierList(gear.Pros, isPro: true, null, gear.Name, gear.Name, issues);
            resolvable &= CheckModifierList(gear.Cons, isPro: false, null, gear.Name, gear.Name, issues);
        }

        // <b>A Gadget's Powers are the fourth place, and they were nowhere.</b> They are ordinary
        // SelectedPowers — p.94 buys them with the same rules as the character's own — so
        // GadgetSpend prices them through PowerCost, and PowerCost throws on an id the rulebook
        // does not have. Nothing walked them, so `modifiersResolvable` was true whatever they
        // carried and CheckGadget went straight on to price them: a submitted sheet with one
        // misspelled Con inside a Gadget took `Validate` out with an InvalidOperationException,
        // which is the one thing a validator may never do.
        foreach (var gadget in sheet.Gadgets)
            foreach (var sp in gadget.Powers)
            {
                var power = Power(sp.PowerId);
                var name  = GadgetPowerName(gadget, sp);

                resolvable &= CheckModifierList(sp.Pros, isPro: true, power, sp.PowerId, name, issues);
                resolvable &= CheckModifierList(sp.Cons, isPro: false, power, sp.PowerId, name, issues);
            }

        foreach (var (abilityId, modifiers) in sheet.AbilityModifiers)
        {
            var name = _rules.GetAbility(abilityId)?.Name;

            // An Ability that does not exist, or one with no ranks bought: AbilityCost walks
            // AbilityRanks, so modifiers keyed anywhere else are never resolved and the Con
            // the player recorded is silently worth nothing.
            if (name is null || !sheet.AbilityRanks.ContainsKey(abilityId))
            {
                issues.Add(new(ValidationSeverity.Error, "MODIFIER_ON_UNBOUGHT_ABILITY",
                    $"Pros or Cons are recorded against '{abilityId}', which is not an Ability "
                    + "this character has bought ranks in, so they would change nothing.")
                {
                    // The six Abilities, not the ones this character happens to have bought: a
                    // sheet with none bought offered an empty list, which is no help at all —
                    // and with the 1d minimum enforced, every Ability has ranks anyway.
                    SubjectKind = ValidationSubject.Ability,
                    SubjectId   = abilityId,
                    Options     = _rules.Abilities.Select(a => a.Id).ToList()
                });
                continue;
            }

            // Cons only, on the Cons list, is how the pickers offer them — but a submitted
            // file can put anything here, and both are priced, so both are checked.
            resolvable &= CheckModifierList(modifiers, isPro: false, null, abilityId, name, issues,
                alsoTryPros: true);
        }

        return resolvable;
    }

    /// <summary>
    /// Whether the rulebook lets this option be taken more than once on the same owner.
    /// A Power's own entry wins over a generic one of the same id, as it does in
    /// <see cref="CostCalculator.ResolveModifiers"/>, so the two cannot disagree.
    /// </summary>
    private bool IsRepeatable(string id, bool isPro, PowerModel? power)
    {
        var specific = (isPro ? power?.PowerPros : power?.PowerCons)
            ?.FirstOrDefault(x => x.Id == id);

        if (specific is not null) return specific.Repeatable;

        return isPro ? _rules.GetPro(id)?.Repeatable ?? false
                     : _rules.GetCon(id)?.Repeatable ?? false;
    }

    /// <summary>
    /// One list of Pros or Cons, on whatever carries it.
    ///
    /// <para><b><c>ownerId</c> is an id and <c>ownerName</c> is the printed name</b>, and the two
    /// are separate on purpose: the id goes on the issue and the name goes in the sentence. The
    /// issue carried the printed name at first, so a caller told a Pro was wrong on "Super Senses
    /// — Thermal Vision" had a display string and nothing it could look up. Gear has only a name,
    /// so for gear the two are the same string.</para>
    /// </summary>

    private bool CheckModifierList(
        IReadOnlyList<SelectedProCon> modifiers,
        bool isPro,
        PowerModel? power,
        string ownerId,
        string ownerName,
        List<ValidationIssue> issues,
        bool alsoTryPros = false)
    {
        var resolvable = true;
        var kind       = isPro ? "Pro" : "Con";
        var seen       = new HashSet<string>(StringComparer.Ordinal);

        foreach (var choice in modifiers)
        {
            // <b>The same Con listed twice was the cheapest character in the game.</b> Nothing
            // rejected a repeat, and every cost here floors at zero, so three Burnouts on a
            // 12d Ability cancelled it exactly: six Abilities at the Trait Cap for 0 HP,
            // reported legal with an empty issue list. A Trait or a Power carries a given Pro
            // or Con once — a second copy is not a second discount, and a sheet prints it once
            // either way.
            // A null id — or a null entry, which is the same thing one level out — is a Pro or Con
            // with no name, which only a hand-written file produces. Reported as unknown, which is
            // what it is, and refused before anything looks it up.
            if (choice?.Id is null)
            {
                issues.Add(new(ValidationSeverity.Error, isPro ? "UNKNOWN_PRO" : "UNKNOWN_CON",
                    $"{ownerName} has a {kind} with no name at all.")
                {
                    SubjectKind = ValidationSubject.Character,
                    OwnerId     = ownerId
                });
                resolvable = false;
                continue;
            }

            // <b>Three entries in the rulebook are bought again rather than repeated by
            // mistake</b>, and each says so in its own text: Also X on Energy Absorption
            // ("each time you select this Pro") and on Energy Form ("for every 2 extra Hero
            // Points"), and the generic Affect Inanimate ("You can apply this Pro multiple
            // times"). <b>Two of the seven Also X entries, not seven</b> — the other five are
            // priced per unit, where the quantity is the mechanism and a second copy would
            // charge twice for one thing. The calculator has always charged
            // every copy, which is what puts Blastwave's six energy types on his printed 125
            // — so refusing them here made the two halves of the engine contradict each
            // other about a Hero in the book. Repeatability is data, not a list of ids: see
            // PowerProConModel.Repeatable and IGenericProCon.Repeatable.
            if (!seen.Add(choice.Id) && !IsRepeatable(choice.Id, isPro, power))
            {
                issues.Add(new(ValidationSeverity.Error,
                    isPro ? "DUPLICATE_PRO" : "DUPLICATE_CON",
                    $"{ownerName} carries the {kind} '{choice.Id}' more than once. It applies "
                    + "once, and a second copy would discount the same thing twice.")
                {
                    SubjectKind = ValidationSubject.Character,
                    SubjectId   = choice.Id,
                    OwnerId     = ownerId
                });
                continue;
            }

            // The Power's own entry wins, as in CostCalculator.ResolveModifiers.
            var specific = (isPro ? power?.PowerPros : power?.PowerCons)
                ?.FirstOrDefault(x => x.Id == choice.Id);

            if (specific is not null)
            {
                // <b>A Power's own Pro or Con needs its grade checked too.</b> This branch used
                // to `continue` straight past, so the eleventh shape of character that made the
                // validator throw was Drain carrying its own Only X Con with no grade — the one
                // finding this method exists to produce, never produced, on the one branch that
                // skipped it. The variant lives in a different field for a Power-specific entry
                // (CostModifierRange or CostPerRankRange by cost type), which is why the model
                // answers NeedsVariant rather than the caller guessing.
                if (specific.NeedsVariant)
                {
                    // Materialised once: it is asked three questions below, and a lazy
                    // Keys projection would answer each by walking the dictionary again.
                    var keys = Keys(specific.CostModifierRange?.Keys
                                    ?? specific.CostPerRankRange?.Keys.AsEnumerable());

                    if (choice.VariantKey is null || !keys.Contains(choice.VariantKey))
                    {
                        issues.Add(new(ValidationSeverity.Error,
                            isPro ? "PRO_VARIANT_NOT_CHOSEN" : "CON_VARIANT_NOT_CHOSEN",
                            $"{ownerName}'s {kind} '{specific.Name}' is priced by grade, and no "
                            + $"grade the rulebook lists has been chosen. Pick one of: {Names(keys)}.")
                        {
                            SubjectKind = ValidationSubject.Character,
                            SubjectId   = choice.Id,
                            OwnerId     = ownerId,
                            Options     = keys
                        });
                        resolvable = false;
                    }
                }

                continue;
            }

            var range = isPro ? _rules.GetPro(choice.Id)?.CostModifierRange
                              : _rules.GetCon(choice.Id)?.CostModifierRange;

            var known = (isPro ? _rules.GetPro(choice.Id) is not null
                               : _rules.GetCon(choice.Id) is not null)
                        || (alsoTryPros && _rules.GetPro(choice.Id) is not null);

            if (!known)
            {
                issues.Add(new(ValidationSeverity.Error, isPro ? "UNKNOWN_PRO" : "UNKNOWN_CON",
                    $"{ownerName} has a {kind}, '{choice.Id}', that is not one the rulebook "
                    + "gives.")
                {
                    SubjectKind = ValidationSubject.Character,
                    SubjectId   = choice.Id,
                    OwnerId     = ownerId
                });
                resolvable = false;
                continue;
            }

            // <b>What the rulebook prints for every Power: the option's own Range and Rank-type
            // constraints.</b> Both editors' pickers filter on these and the validator did not,
            // so a submitted character could carry the Ranged Pro on a Self-range Power — which
            // raises a Touch Power to Distant Range, and has nothing to raise on a Power that
            // affects only you — and come back legal. That was a hole in the one claim this
            // whole surface makes.
            //
            // Only here, and only for a generic option on a Power. A Pro printed inside a
            // Power's own entry is applicable to that Power by definition, and gear and
            // Abilities are not Powers, so neither has a Range for an option to object to.
            var option = isPro
                ? (IGenericProCon?)_rules.GetPro(choice.Id)
                : _rules.GetCon(choice.Id);

            if (power is not null)
            {
                if (option is not null && !ProConApplicability.IsApplicable(option, power))
                {
                    issues.Add(new(ValidationSeverity.Error,
                        isPro ? "PRO_NOT_APPLICABLE" : "CON_NOT_APPLICABLE",
                        $"The {kind} '{option.Name}' cannot be applied to {ownerName}. "
                        + $"{Applicability(option)}")
                    {
                        SubjectKind = ValidationSubject.Character,
                        SubjectId   = choice.Id,
                        OwnerId     = ownerId
                    });

                    // Still priceable: the engine knows what it costs, it just may not be taken.
                    continue;
                }
            }

            if (range is null) continue;

            // Not every grade the option prints is on offer here. Zone/Nova and Ranged price
            // by the base Power's Range, and a Power that reaches them through its own text
            // has a Range the rulebook does not price — so Force Field, which is Self, offers
            // the Ranged grades and not the Touch ones. Without this, T-Kay's printed
            // Force Field 12d (Zone) was accepted at +2 and at +4, with no finding either way.
            var grades = option is null
                ? Keys(range.Keys)
                : ProConApplicability.GradesFor(option, power, range.Keys);

            // Priced by grade. Absent and wrong are the same repair — choose one of these —
            // so they are one finding with the keys attached.
            if (choice.VariantKey is null || !grades.Contains(choice.VariantKey, StringComparer.Ordinal))
            {
                issues.Add(new(ValidationSeverity.Error,
                    isPro ? "PRO_VARIANT_NOT_CHOSEN" : "CON_VARIANT_NOT_CHOSEN",
                    $"{ownerName}'s {kind} '{choice.Id}' is priced by grade, and no grade the "
                    + $"rulebook lists has been chosen. Pick one of: {Names(grades)}.")
                {
                    SubjectKind = ValidationSubject.Character,
                    SubjectId   = choice.Id,
                    OwnerId     = ownerId,
                    Options     = grades
                });
                resolvable = false;
            }
        }

        return resolvable;
    }

    /// <summary>
    /// The same flaw twice. It counted as two: two against the maximum of three, and — for a
    /// Condition or Plot Hook — two points of Resolve for one drawback, so three copies of
    /// <c>enemy</c> gave 21 Resolve where one gives 19. A character has a flaw or does not.
    /// </summary>
    private void CheckDuplicateFlaws(CharacterSheet sheet, List<ValidationIssue> issues)
    {
        var seen = new HashSet<string>(StringComparer.Ordinal);

        foreach (var sf in sheet.Flaws.Where(f => f.FlawId is not null && !seen.Add(f.FlawId)))
            issues.Add(new(ValidationSeverity.Error, "DUPLICATE_FLAW",
                $"The flaw '{Flaw(sf.FlawId)?.Name ?? sf.FlawId}' is taken more than "
                + "once. Taking it twice counts twice against the limit and pays Resolve twice "
                + "for one drawback.")
            {
                SubjectKind = ValidationSubject.Flaw,
                SubjectId   = sf.FlawId
            });
    }

    /// <summary>
    /// The same Power listed twice, which is a <b>warning</b> rather than an error on purpose.
    ///
    /// <para>The rulebook does not say a Power may not be taken twice, and two Blasts with
    /// different Pros is a shape a player might well want — so refusing it would be this tool
    /// deciding a rules question it cannot cite. What is certainly wrong is that the two
    /// disagree: the budget charges for both while <c>CharacterSheet.GetPower</c> answers with
    /// the first, so the sheet shows one Power and the total pays for two.</para>
    ///
    /// <para><b>Except where the entry says to buy it again.</b> Alternate Form (p.21) and
    /// Duplication (p.27) each print "buy this Power multiple times", and a second purchase of
    /// either is the book's own instruction — one per extra form, one per extra duplicate. Those
    /// entries carry <see cref="PowerModel.Repeatable"/>, and a second purchase of one is not
    /// warned about here: the alternate-form check's own remedy for a second form is that every
    /// member pays again, and a warning on the sheet for taking it would contradict the roster.</para>
    /// </summary>
    private void CheckDuplicatePowers(CharacterSheet sheet, List<ValidationIssue> issues)
    {
        var seen = new HashSet<string>(StringComparer.Ordinal);

        foreach (var sp in sheet.SelectedPowers.Where(p => p.PowerId is not null
                                                         && !seen.Add(p.PowerId)
                                                         && Power(p.PowerId)?.Repeatable != true))
            issues.Add(new(ValidationSeverity.Warning, "DUPLICATE_POWER",
                $"{PowerName(sp.PowerId)} is listed more than once. Both are charged for, but "
                + "a sheet shows the first, so check this is what was meant.")
            {
                SubjectKind = ValidationSubject.Power,
                SubjectId   = sp.PowerId
            });
    }

    private void CheckFlawIds(CharacterSheet sheet, List<ValidationIssue> issues)
    {
        foreach (var sf in sheet.Flaws)
        {
            if (sf.FlawId is null || Flaw(sf.FlawId) is null)
                issues.Add(new(ValidationSeverity.Error, "UNKNOWN_FLAW",
                    sf.FlawId is null
                        ? "One of the flaws has no name at all."
                        : $"There is no flaw called '{sf.FlawId}' in the rulebook data.")
                {
                    SubjectKind = ValidationSubject.Flaw,
                    SubjectId   = sf.FlawId,
                    Options     = _rules.Flaws.Select(f => f.Id).ToList()
                });
        }
    }

    /// <summary>
    /// Custom gear (Ch.6, p.93). Mundane gear is free and untracked, so an uncustomised
    /// item is never an issue; these only bite once Hero Points are involved.
    ///
    /// <para>Returns false if any item cannot be priced at all, which stops the caller
    /// asking for a total that would throw.</para>
    /// </summary>
    private bool CheckGear(
        CharacterSheet sheet, List<ValidationIssue> issues, bool modifiersResolvable)
    {
        var resolvable = true;

        foreach (var gear in sheet.Gear)
        {
            // Gear is identified by its name and has no id, so a nameless item cannot be
            // reported about, printed, or repaired — every message about it would name nothing.
            if (string.IsNullOrWhiteSpace(gear.Name))
            {
                issues.Add(new(ValidationSeverity.Error, "GEAR_WITHOUT_NAME",
                    "A piece of gear has no name. Gear is identified by its name, so an item "
                    + "without one cannot be put on a sheet.")
                {
                    // Character, not Gear: gear is identified by its name and this one has
                    // none, so there is no item for a caller to go and look at. Naming the kind
                    // Gear while being unable to say which would be worse than saying neither.
                    SubjectKind = ValidationSubject.Character
                });
                resolvable = false;
                continue;
            }

            // A catalogue row that resolves to nothing. **Reported, never repaired**: the id is
            // what makes an item a Battle Axe rather than a name somebody typed, and dropping it
            // would silently turn one into the other — a legal, cheaper, differently-armed
            // character nobody was told about, which is the same failure as a misspelled field
            // name in a submitted payload. Null is not an error: p.91's list is "examples, not a
            // catalogue of prices", so most gear names no row at all.
            if (gear.CatalogueId is { } rowId && _rules.Catalogue.Find(rowId) is null)
            {
                issues.Add(new(ValidationSeverity.Error, "UNKNOWN_GEAR_CATALOGUE_ROW",
                    $"'{gear.Name}' names a Chapter 6 catalogue row, '{rowId}', that is not one "
                    + "the rulebook has. Its bonus and features cannot be read.")
                {
                    SubjectKind = ValidationSubject.Gear,
                    SubjectId   = gear.Name,

                    // Deliberately no Options. There are 108 rows and the right one is a question
                    // about what the character carries, not a value to pick off a list — and an
                    // option list a screen would offer to choose from is a repair this engine does
                    // not make.
                });

                // **And `resolvable` is deliberately left alone.** That flag means one thing —
                // this item cannot be priced — and the caller spends it on one thing: whether to
                // run the Hero Point budget check. `GearCost` never reads `CatalogueId`, so an id
                // that resolves to nothing prices exactly as it did before. Clearing the flag here
                // dropped `HP_BUDGET_EXCEEDED` from a character that really was over, which is a
                // misspelling buying silence on one of the two limits a character can break.
            }

            var itemResolvable = true;

            foreach (var f in gear.Features)
            {
                // A null entry, or one with no id: reported as a feature the rulebook does not
                // have, which is what it is. The lookup would throw on the id rather than answer.
                var feature = f?.FeatureId is null ? null : _rules.GetGearFeature(f.FeatureId);
                if (feature is null)
                {
                    issues.Add(new(ValidationSeverity.Error, "UNKNOWN_GEAR_FEATURE",
                        f?.FeatureId is null
                            ? $"{gear.Name} has a custom feature with no name at all."
                            : $"{gear.Name} has a custom feature, '{f.FeatureId}', that is not one "
                              + "the rulebook lists.")
                    {
                        SubjectKind = ValidationSubject.GearFeature,
                        SubjectId   = f?.FeatureId,
                        OwnerId     = gear.Name,
                        Options     = _rules.GearFeatures.Select(g => g.Id).ToList()
                    });
                    itemResolvable = false;
                    continue;
                }

                // f is not null here: a null entry has no feature and was reported above.
                if (feature.CostType != "flat"
                    && (f!.GradeKey is null || feature.CostRange?.ContainsKey(f.GradeKey) != true))
                {
                    issues.Add(new(ValidationSeverity.Error, "GEAR_FEATURE_NEEDS_GRADE",
                        $"{gear.Name}'s {feature.Name} feature is priced by grade, and no grade "
                        + $"the rulebook lists has been chosen. "
                        + $"Pick one of: {Names(feature.CostRange?.Keys)}.")
                    {
                        SubjectKind = ValidationSubject.GearFeature,
                        SubjectId   = f.FeatureId,
                        OwnerId     = gear.Name,
                        Options     = Keys(feature.CostRange?.Keys)
                    });
                    itemResolvable = false;
                }
            }

            resolvable &= itemResolvable;

            // "Regardless of Cons, no piece of gear can cost less than 0 Hero Points."
            // Cons past that point buy the character nothing, so say so rather than
            // letting a player think they are still saving.
            // modifiersResolvable as well as itemResolvable: this is the one place gear is
            // priced during validation, and a Con with an id or a grade the rulebook does not
            // have throws out of GearCost. The features were checked above; the Pros and Cons
            // are CheckModifiers' business, and its answer has to be respected here or this
            // warning takes the whole validation down with it.
            if (itemResolvable && modifiersResolvable && gear.Cons.Count > 0 && _costs.GearCost(gear) == 0)
                issues.Add(new(ValidationSeverity.Warning, "GEAR_COST_AT_MINIMUM",
                    $"Gear '{gear.Name}' is already free after its cons. " +
                    "No piece of gear can cost less than 0 HP, so further cons will not help.")
                {
                    SubjectKind = ValidationSubject.Gear,
                    SubjectId   = gear.Name,
                    Value       = 0,
                    Limit       = 0
                });

            // Two-Fisted is what allows a matched pair to be customised for one price.
            if (gear.PairedUnderTwoFisted && !sheet.HasPower("two_fisted"))
                issues.Add(new(ValidationSeverity.Error, "TWO_FISTED_PAIR_WITHOUT_POWER",
                    $"Gear '{gear.Name}' is recorded as a Two-Fisted pair, but the character " +
                    "does not have the Two-Fisted Power that allows paying once for both.")
                {
                    // The subject is the gear, and no Options: the repair is adding a Power
                    // to the character, not writing a value into this item. An option list
                    // whose values do not go into the subject reads as though it would.
                    SubjectKind = ValidationSubject.Gear,
                    SubjectId   = gear.Name
                });
        }

        return resolvable;
    }


    // ── Vehicles, headquarters and Gadgets (Ch.6 pp.94-103) ───────────────────

    /// <summary>
    /// What a character owns beside their own body, and what they built out of a Gadget's pool.
    ///
    /// <para><b>Reported, never repaired</b>, like everything else here: a machine over its budget
    /// is still priced at what it says, and a Gadget whose Complexity is above its builder's
    /// Technology is still worth twice its Complexity. The engine is a judge and does not make
    /// design decisions about somebody's vehicle any more than about their Powers.</para>
    ///
    /// <para><b>The budget checks are per asset and gated per asset.</b> A feature the rulebook
    /// does not have takes <c>VehiclePointsSpent</c> down rather than letting it guess, so a
    /// machine that cannot be priced is reported and skipped — and the machine beside it is still
    /// checked, which is what a single sheet-wide gate would have thrown away.</para>
    /// </summary>
    private void CheckAssets(
        CharacterSheet sheet, List<ValidationIssue> issues, bool modifiersResolvable)
    {
        foreach (var vehicle in sheet.Vehicles) CheckVehicle(vehicle, issues);
        foreach (var hq in sheet.Headquarters) CheckHeadquarters(hq, issues);
        foreach (var gadget in sheet.Gadgets) CheckGadget(sheet, gadget, issues, modifiersResolvable);

        CheckCampaignAssets(sheet, issues);
        CheckAssetPerksAreNotRecordedTwice(sheet, issues);
    }

    /// <summary>
    /// One vehicle: its features, its two printed constraints, and its Vehicle Point budget.
    /// </summary>
    private void CheckVehicle(OwnedVehicle vehicle, List<ValidationIssue> issues)
    {
        // A vehicle is identified by its name, exactly as a piece of gear is, so a nameless one
        // cannot be reported about or printed — every message would name nothing.
        if (string.IsNullOrWhiteSpace(vehicle.Name))
        {
            issues.Add(new(ValidationSeverity.Error, "VEHICLE_WITHOUT_NAME",
                "A vehicle has no name. A vehicle is identified by its name, so one without a "
                + "name cannot be put on a sheet.")
            {
                SubjectKind = ValidationSubject.Character
            });
            return;
        }

        var priceable = CheckAssetFeatures(
            vehicle.Name, vehicle.Features,
            id => _rules.Assets.FindVehicleFeature(id) is { } f
                ? (f.Name, f.CostType, f.CostRange) : null,
            _rules.Assets.VehicleFeatures.Select(f => f.Id).ToList(), issues);

        CheckControl(vehicle.Name, vehicle.Control, vehicle.Speed, issues);

        CheckMechaMight(vehicle, issues);

        CheckVehicleFeaturePrerequisites(vehicle.Name, vehicle.Features, issues);

        if (!priceable) return;

        var spent  = _costs.VehiclePointsSpent(vehicle);
        var budget = _costs.VehiclePointBudget(vehicle);

        if (spent > budget)
            issues.Add(new(ValidationSeverity.Error, "VEHICLE_OVER_BUDGET",
                $"{vehicle.Name} costs {spent} Vehicle Points and the Unique Vehicle Perk bought "
                + $"it {budget} — {vehicle.PerkHeroPoints} Hero "
                + $"Point{(vehicle.PerkHeroPoints == 1 ? "" : "s")} at "
                + $"{_rules.Assets.VehiclePointsPerHeroPoint} each.")
            {
                SubjectKind = ValidationSubject.Vehicle,
                SubjectId   = vehicle.Name,
                Value       = spent,
                Limit       = budget
            });
    }

    /// <summary>
    /// Half of a rank, the way the rulebook means it: p.7's glossary, "Whenever we refer to half
    /// of an odd number (or half of an odd number of dice), always round up, regardless of the
    /// context."
    ///
    /// <para><b>It is here because a cap and a floor do not round the same way when the halving is
    /// open-coded, and only one of the two open codings was right.</b> A floor written
    /// <c>units * 2 &gt;= body</c> is exactly <c>units &gt;= ceil(body / 2)</c> over the integers,
    /// so the Mecha check was correct by luck. The same trick on a cap,
    /// <c>control * 2 &gt; speed</c>, is <c>control &gt; floor(speed / 2)</c> — a rank tighter at
    /// every odd Speed, and it rejected three machines p.97 prints. Both call this now, so there
    /// is one halving to be wrong about rather than two.</para>
    /// </summary>
    private static int HalfRoundedUp(int rank) => (rank + 1) / 2;

    /// <summary>
    /// p.99's Mecha, the one vehicle feature that prints a floor beside its price: "A vehicle's
    /// Might may not be lower than half its Body."
    ///
    /// <para><b>A floor on what the limbs cost, not a cap</b> — a 14d Body Mecha owes at least
    /// seven Vehicle Points of Might before anything else. The Might is the feature's own unit
    /// count, which is what <c>vehicles.json</c> prices it per.</para>
    /// </summary>
    private void CheckMechaMight(OwnedVehicle vehicle, List<ValidationIssue> issues)
    {
        var mecha = vehicle.Features.FirstOrDefault(
            f => string.Equals(f.FeatureId, AssetCatalogue.MechaFeatureId, StringComparison.Ordinal));

        if (mecha is null || mecha.Units >= HalfRoundedUp(vehicle.Body)) return;

        issues.Add(new(ValidationSeverity.Error, "MECHA_MIGHT_BELOW_HALF_BODY",
            $"{vehicle.Name} is a Mecha with Might {mecha.Units} against Body {vehicle.Body}. "
            + "A Mecha's Might may not be lower than half its Body.")
        {
            SubjectKind = ValidationSubject.AssetFeature,
            SubjectId   = AssetCatalogue.MechaFeatureId,
            OwnerId     = vehicle.Name,
            Value       = mecha.Units,

            // Half the Body, which p.7 rounds up like every other half in the book — the same
            // call the comparison above makes, so the figure quoted is the figure tested.
            Limit       = HalfRoundedUp(vehicle.Body)
        });
    }

    /// <summary>
    /// <b>Ruling 2 (owner, 2026-09-10; PROGRESS.md item 33): a vehicle feature's structured
    /// prerequisite, checked.</b> Submersible needs Swimming and Transforming needs two of four
    /// movement features — both p.100, both stated in <c>vehicles.json</c>'s
    /// <c>requires_features</c> now that the owner has said the prose should be enforced.
    ///
    /// <para><b>A Warning, not an Error</b> — the owner's ruling — and reported rather than
    /// repaired: this engine does not decide which feature the player meant to add or drop. A base
    /// feature never reaches here, because <c>BaseFeatureRow</c> carries no such field: nothing on
    /// pp.100-103 prints a prerequisite of this shape (see <c>docs/guide/rules-engine.md</c>).</para>
    /// </summary>
    private void CheckVehicleFeaturePrerequisites(
        string vehicleName, IReadOnlyList<SelectedAssetFeature> features, List<ValidationIssue> issues)
    {
        var owned = new HashSet<string>(StringComparer.Ordinal);
        foreach (var selected in features)
            if (selected?.FeatureId is { } ownedId) owned.Add(ownedId);

        foreach (var selection in features)
        {
            if (selection?.FeatureId is not { } featureId) continue;
            if (_rules.Assets.FindVehicleFeature(featureId) is not { RequiresFeatures: { } need } feature)
                continue;

            var have = need.AnyOf.Count(id => owned.Contains(id));
            if (have >= need.Min) continue;

            var names = string.Join(", ",
                need.AnyOf.Select(id => _rules.Assets.FindVehicleFeature(id)?.Name ?? id));

            issues.Add(new(ValidationSeverity.Warning, "VEHICLE_FEATURE_PREREQUISITE_BELOW_MINIMUM",
                $"{vehicleName}'s {feature.Name} needs at least {need.Min} of: {names} "
                + $"(Ch.6 p.{feature.PrintedPage}), and this vehicle has "
                + $"{(have == 0 ? "none of them" : $"only {have}")}.")
            {
                SubjectKind = ValidationSubject.AssetFeature,
                SubjectId   = selection.FeatureId,
                OwnerId     = vehicleName,
                Value       = have,
                Limit       = need.Min
            });
        }
    }

    /// <summary>
    /// p.96's two printed sentences about Control, for whichever machine is carrying it.
    ///
    /// <para><b>One home for the pair, because there are two records that carry them</b>: a
    /// machine one character owns and one the campaign does. They are the same printed rule read
    /// off the same rates, and a second copy is a second place to miss a correction — and, worse,
    /// a second place to have written no copy at all, which is what the campaign's own object
    /// shipped with.</para>
    /// </summary>
    /// <param name="name">What to call it in the sentence.</param>
    /// <param name="control">Its Control.</param>
    /// <param name="speed">Its Speed, which is what bounds Control from above.</param>
    /// <param name="issues">Where the findings go.</param>
    private void CheckControl(string name, int control, int speed, List<ValidationIssue> issues)
    {
        var rates = _rules.Assets.Characteristics;

        // p.96: "Control ... can't exceed half the vehicle's Speed." **Half rounds up**, because
        // p.7's glossary settles every halving in the book that way — "always round up, regardless
        // of the context" — and p.96 works that rule on this very page: a Foe-piloted sedan with
        // 7d Body is disabled after "4 points of damage (half of 7)".
        //
        // This was written `Control * 2 > Speed`, which is `Control > floor(Speed / 2)` and so
        // rejects a legal machine at every odd Speed. p.97 prints three of them — Helicopter
        // (Military), Helicopter (Personal) and Jet Pack, each Speed 7d with Control +4d.
        var controlCap = HalfRoundedUp(speed);

        if (control > 0 && control > controlCap)
            issues.Add(new(ValidationSeverity.Error, "VEHICLE_CONTROL_ABOVE_HALF_SPEED",
                $"{name} has Control {control} against Speed {speed}. "
                + "A vehicle's Control may not exceed half its Speed.")
            {
                SubjectKind = ValidationSubject.Vehicle,
                SubjectId   = name,
                Value       = control,
                Limit       = controlCap
            });

        // p.96: a negative Control pays two points back a rank, down to −3 and no further.
        if (control < rates.NegativeControlMinimum)
            issues.Add(new(ValidationSeverity.Error, "VEHICLE_CONTROL_BELOW_MINIMUM",
                $"{name} has Control {control}. A vehicle's Control cannot go "
                + $"below {rates.NegativeControlMinimum}, however many points that would pay back.")
            {
                SubjectKind = ValidationSubject.Vehicle,
                SubjectId   = name,
                Value       = control,
                Limit       = rates.NegativeControlMinimum
            });
    }

    /// <summary>One headquarters: its features and its Base Point budget.</summary>
    private void CheckHeadquarters(OwnedHeadquarters headquarters, List<ValidationIssue> issues)
    {
        if (string.IsNullOrWhiteSpace(headquarters.Name))
        {
            issues.Add(new(ValidationSeverity.Error, "HEADQUARTERS_WITHOUT_NAME",
                "A headquarters has no name. A headquarters is identified by its name, so one "
                + "without a name cannot be put on a sheet.")
            {
                SubjectKind = ValidationSubject.Character
            });
            return;
        }

        var priceable = CheckAssetFeatures(
            headquarters.Name, headquarters.Features,
            id => _rules.Assets.FindBaseFeature(id) is { } f
                ? (f.Name, f.CostType, f.CostRange) : null,
            _rules.Assets.BaseFeatures.Select(f => f.Id).ToList(), issues);

        if (!priceable) return;

        var spent  = _costs.BasePointsSpent(headquarters);
        var budget = _costs.BasePointBudget(headquarters);

        if (spent > budget)
            issues.Add(new(ValidationSeverity.Error, "HEADQUARTERS_OVER_BUDGET",
                $"{headquarters.Name} costs {spent} Base Points and the Headquarters Perk bought "
                + $"it {budget} — {headquarters.PerkHeroPoints} Hero "
                + $"Point{(headquarters.PerkHeroPoints == 1 ? "" : "s")} at "
                + $"{_rules.Assets.BasePointsPerHeroPoint} each. The building itself is free; "
                + "these are the features.")
            {
                SubjectKind = ValidationSubject.Headquarters,
                SubjectId   = headquarters.Name,
                Value       = spent,
                Limit       = budget
            });
    }

    /// <summary>
    /// One Gadget: what the builder needed, what the pool paid out, and what was spent from it.
    ///
    /// <para><b>The one place in this validator where the budget runs the other way.</b> A Gadget
    /// costs the character nothing; what it can be over is its own pool, which is twice its
    /// Complexity.</para>
    /// </summary>
    private void CheckGadget(
        CharacterSheet sheet, BuiltGadget gadget, List<ValidationIssue> issues, bool modifiersResolvable)
    {
        if (string.IsNullOrWhiteSpace(gadget.Name))
        {
            issues.Add(new(ValidationSeverity.Error, "GADGET_WITHOUT_NAME",
                "A Gadget has no name. A Gadget is identified by its name, so one without a name "
                + "cannot be put on a sheet.")
            {
                SubjectKind = ValidationSubject.Character
            });
            return;
        }

        var minimum = _rules.Assets.MinimumGadgetComplexity;

        if (gadget.Complexity < minimum)
            issues.Add(new(ValidationSeverity.Error, "GADGET_COMPLEXITY_BELOW_MINIMUM",
                $"{gadget.Name} has Complexity {gadget.Complexity}. A Gadget's Complexity starts "
                + $"at {minimum}.")
            {
                SubjectKind = ValidationSubject.Gadget,
                SubjectId   = gadget.Name,
                Value       = gadget.Complexity,
                Limit       = minimum
            });

        // p.94's two prerequisites, both of which this sheet can actually answer. The third —
        // no more than half the builder's Intellect in one issue — is about an issue rather than
        // about a sheet, and nothing here knows which issue it is looking at, so it is not
        // reported. See docs/guide/rules-engine.md.
        var prerequisites = _rules.Gadgets.Entries
            .Single(e => e.Id == "gadget_prerequisites").Prerequisites!;

        var technology = sheet.GetTalentRank("technology");

        if (technology < prerequisites.MinimumTechnologyRank)
            issues.Add(new(ValidationSeverity.Error, "GADGET_BUILDER_BELOW_TECHNOLOGY_MINIMUM",
                $"{gadget.Name} was built with Technology {technology}d. Building a Gadget at all "
                + $"needs {prerequisites.MinimumTechnologyRank}d.")
            {
                SubjectKind = ValidationSubject.Gadget,
                SubjectId   = gadget.Name,
                Value       = technology,
                Limit       = prerequisites.MinimumTechnologyRank
            });
        else if (gadget.Complexity > technology)
            issues.Add(new(ValidationSeverity.Error, "GADGET_COMPLEXITY_ABOVE_TECHNOLOGY",
                $"{gadget.Name} has Complexity {gadget.Complexity} and its builder's Technology "
                + $"is {technology}d. A Gadget's Complexity reaches as far as the builder's "
                + "Technology and no further.")
            {
                SubjectKind = ValidationSubject.Gadget,
                SubjectId   = gadget.Name,
                Value       = gadget.Complexity,
                Limit       = technology
            });

        // A Power the rulebook does not have, or one missing a cost variant, throws out of
        // GadgetSpend exactly as it would out of any other pricing. Those are reported against the
        // character's own Powers by the checks above; a Gadget's are its own, so they are asked
        // here and the pool comparison is skipped when the answer cannot be had.
        if (!modifiersResolvable || !GadgetIsPriceable(gadget, issues)) return;

        var pool  = _costs.GadgetPool(gadget);
        var spent = _costs.GadgetSpend(gadget, sheet.ImmortalityCost);

        if (spent > pool)
            issues.Add(new(ValidationSeverity.Error, "GADGET_OVER_POOL",
                $"{gadget.Name} spends {spent} Hero Points and its build paid out {pool} — twice "
                + $"its Complexity of {gadget.Complexity}. The pool is not the character's own "
                + "budget and cannot be topped up from it.")
            {
                SubjectKind = ValidationSubject.Gadget,
                SubjectId   = gadget.Name,
                Value       = spent,
                Limit       = pool
            });
    }

    /// <summary>
    /// Whether a Gadget's own Powers, Abilities and Talents can be priced at all, reporting each
    /// gap by name. Unknown ids are reported and not charged for, which is the answer
    /// <c>AbilityCost</c> already gives for the character's own.
    ///
    /// <para><b>All three collections, and the Abilities and Talents were missed.</b> This method
    /// walked <see cref="BuiltGadget.Powers"/> alone while its own summary claimed otherwise, and
    /// <c>GadgetSpend</c> skips a rank whose Trait id resolves to nothing — so a Gadget carrying
    /// <c>"mightt": 99</c> spent nothing out of its pool, stayed inside it, printed the ranks and
    /// raised no finding at all. That is the silence a misspelled id bought in <c>gear.json</c>'s
    /// own review one slice earlier, in a collection nothing was walking. The character's own
    /// Traits are covered by <c>CheckUnknownTraits</c>; a Gadget's are its own dictionaries and
    /// were covered by nothing.</para>
    /// </summary>
    private bool GadgetIsPriceable(BuiltGadget gadget, List<ValidationIssue> issues)
    {
        var priceable = true;

        foreach (var power in gadget.Powers)
        {
            if (power.PowerId is not null && _rules.GetPower(power.PowerId) is { } model)
            {
                priceable &= CheckPowerIsPriceable(power, model, gadget.Name, issues);
                continue;
            }

            issues.Add(new(ValidationSeverity.Error, "UNKNOWN_GADGET_POWER",
                power.PowerId is null
                    ? $"{gadget.Name} carries a Power with no id at all."
                    : $"{gadget.Name} carries a Power, '{power.PowerId}', that is not one the "
                      + "rulebook has, so what the Gadget spent cannot be worked out.")
            {
                SubjectKind = ValidationSubject.Gadget,
                SubjectId   = gadget.Name,
                OwnerId     = power.PowerId
            });
            priceable = false;
        }

        foreach (var id in gadget.AbilityRanks.Keys.Where(id => _rules.GetAbility(id) is null))
        {
            issues.Add(new(ValidationSeverity.Error, "UNKNOWN_GADGET_ABILITY",
                $"{gadget.Name} has ranks against '{id}', which is not one of the six Abilities "
                + "in the rulebook, so what the Gadget spent cannot be worked out.")
            {
                SubjectKind = ValidationSubject.Gadget,
                SubjectId   = gadget.Name,
                OwnerId     = id,
                Options     = _rules.Abilities.Select(a => a.Id).ToList()
            });
            priceable = false;
        }

        foreach (var id in gadget.TalentRanks.Keys.Where(id => _rules.GetTalent(id) is null))
        {
            issues.Add(new(ValidationSeverity.Error, "UNKNOWN_GADGET_TALENT",
                $"{gadget.Name} has ranks against '{id}', which is not one of the twelve Talents "
                + "in the rulebook, so what the Gadget spent cannot be worked out.")
            {
                SubjectKind = ValidationSubject.Gadget,
                SubjectId   = gadget.Name,
                OwnerId     = id,
                Options     = _rules.Talents.Select(t => t.Id).ToList()
            });
            priceable = false;
        }

        return priceable;
    }

    /// <summary>
    /// Hero Points put into a campaign's shared vehicle or base. There is nothing else on this
    /// sheet to check — what the object turned out to be is the campaign's answer — so this is
    /// the two things a record can be wrong about on its own.
    ///
    /// <para><b>Except when the contribution carries a <see cref="CampaignAssetContribution.Proposal"/>.</b>
    /// PROGRESS item 33, rulings 5+6: the player builds the object and the GM approves it, so a
    /// proposal is a full <see cref="CampaignAsset"/> riding the character submission rather than
    /// a row the GM already typed. It is held to the same printed rules a GM-typed object is
    /// (<see cref="CheckSharedAsset"/>), to the same kind agreement a mismatched contribution is
    /// (<see cref="CheckContributionAgainstAsset"/>), and to a warning of its own —
    /// <c>CAMPAIGN_ASSET_SURPLUS</c> — when the Hero Points buy more than the build spends, so a
    /// proposer sees it before submitting rather than the GM discovering it unannounced.</para>
    /// </summary>
    private void CheckCampaignAssets(CharacterSheet sheet, List<ValidationIssue> issues)
    {
        foreach (var contribution in sheet.CampaignAssets)
        {
            if (string.IsNullOrWhiteSpace(contribution.AssetId))
                issues.Add(new(ValidationSeverity.Error, "CAMPAIGN_ASSET_WITHOUT_ID",
                    $"Hero Points have been put into a campaign asset with no id"
                    + $"{(string.IsNullOrWhiteSpace(contribution.Name) ? "" : $" ('{contribution.Name}')")}. "
                    + "The id is what a campaign sums a shared vehicle or base on.")
                {
                    SubjectKind = ValidationSubject.Character
                });

            if (!CampaignAssetContribution.Kinds.Contains(contribution.Kind, StringComparer.Ordinal))
                issues.Add(new(ValidationSeverity.Error, "UNKNOWN_CAMPAIGN_ASSET_KIND",
                    $"A contribution names a kind of shared asset, '{contribution.Kind}', that "
                    + "Chapter 6 does not have. Hero Points can be pooled on a vehicle or on a "
                    + "headquarters.")
                {
                    SubjectKind = ValidationSubject.Character,
                    SubjectId   = contribution.AssetId,
                    Options     = CampaignAssetContribution.Kinds
                });

            if (contribution.HeroPoints < 0)
                issues.Add(new(ValidationSeverity.Error, "NEGATIVE_UNITS",
                    $"'{(string.IsNullOrWhiteSpace(contribution.Name) ? contribution.AssetId : contribution.Name)}' "
                    + $"records {contribution.HeroPoints} Hero Points put in, which would pay the "
                    + "character rather than cost them.")
                {
                    SubjectKind = ValidationSubject.Character,
                    SubjectId   = contribution.AssetId,
                    Value       = contribution.HeroPoints,
                    Limit       = 0
                });

            // The owner's 2026-09-10 ruling: a contribution above CampaignAssetContribution's own
            // cap is not a huge campaign, it is a mistake, and it is reported before it ever
            // reaches CostCalculator.CampaignAssetBudget's arithmetic.
            if (contribution.HeroPoints > CampaignAssetContribution.MaxHeroPoints)
                issues.Add(new(ValidationSeverity.Error, "CAMPAIGN_ASSET_CONTRIBUTION_TOO_LARGE",
                    $"'{(string.IsNullOrWhiteSpace(contribution.Name) ? contribution.AssetId : contribution.Name)}' "
                    + $"records {contribution.HeroPoints} Hero Points put in, above the "
                    + $"{CampaignAssetContribution.MaxHeroPoints} the owner has ruled a game would "
                    + "never exceed on a single Hero.")
                {
                    SubjectKind = ValidationSubject.Character,
                    SubjectId   = contribution.AssetId,
                    Value       = contribution.HeroPoints,
                    Limit       = CampaignAssetContribution.MaxHeroPoints
                });

            if (contribution.Proposal is { } proposal)
                CheckProposal(contribution, proposal, issues);
        }
    }

    /// <summary>
    /// A proposal a member is presenting for the GM to approve, refuse or amend — the build itself
    /// rides the contribution, so it is checked here rather than waiting for a campaign to exist.
    ///
    /// <para><b>The same printed rules a GM-typed object is held to</b>, via
    /// <see cref="CheckSharedAsset"/> with no other contributions in hand — a proposal is checked
    /// against Chapter 6 on its own, not against a campaign's budget it does not belong to yet.
    /// The kind agreement between the contribution and its own proposal is
    /// <see cref="CheckContributionAgainstAsset"/>, the same check a mismatched contribution
    /// against an already-adopted object gets.</para>
    ///
    /// <para><b><c>CAMPAIGN_ASSET_SURPLUS</c> is new, and it is the dissolution of ruling 5.</b>
    /// "A surplus contribution is unreported" stopped being a question the GM discovers once the
    /// player who is spending the Hero Points sees the figure before ever submitting — reported as
    /// a Warning, because an unspent balance is not illegal, only worth naming.</para>
    /// </summary>
    private void CheckProposal(
        CampaignAssetContribution contribution, CampaignAsset proposal, List<ValidationIssue> issues)
    {
        issues.AddRange(CheckContributionAgainstAsset(contribution, proposal));
        issues.AddRange(CheckSharedAsset(proposal));

        int spent;
        try
        {
            spent = _costs.CampaignAssetPointsSpent(proposal);
        }
        catch (Exception e) when (e is InvalidOperationException or OverflowException)
        {
            // Unpriceable — CheckSharedAsset has already said which feature or grade is missing,
            // and a surplus figure computed over a build that cannot be priced would be a second,
            // contradicting number about the same mistake.
            return;
        }

        var bought = contribution.HeroPoints * _costs.CampaignAssetPointsPerHeroPoint(proposal);
        var unspent = bought - spent;
        if (unspent <= 0) return;

        var name = string.IsNullOrWhiteSpace(contribution.Name) ? contribution.AssetId : contribution.Name;
        var currency = proposal.IsHeadquarters ? "Base Points" : "Vehicle Points";

        issues.Add(new(ValidationSeverity.Warning, "CAMPAIGN_ASSET_SURPLUS",
            $"'{name}' costs {contribution.HeroPoints} Hero Points, buying {bought} {currency}, "
            + $"but the build only spends {spent}. {unspent} {currency} are unspent.")
        {
            SubjectKind = ValidationSubject.Character,
            SubjectId   = contribution.AssetId,
            Value       = unspent,
            Limit       = 0
        });
    }

    /// <summary>
    /// <b>A campaign's own shared vehicle or base, against the same printed rules a machine one
    /// character owns is held to.</b>
    ///
    /// <para><b>Why it is a method of its own rather than a clause of <see cref="Validate"/>.</b>
    /// A <see cref="CampaignAsset"/> is not on a <see cref="CharacterSheet"/> — that is the whole
    /// of the owner's answer to the pooling question — so nothing that walks a sheet will ever
    /// reach one. It is still rules, and rules do not go in a host: p.96's two sentences about
    /// Control and the fact that a characteristic is bought upward from nothing are printed, and
    /// a browser comparing <c>Control * 2 &gt; Speed</c> would be a front end holding a rule.</para>
    ///
    /// <para><b>What it is not is storage.</b> The object is handed in, exactly as
    /// <see cref="CostCalculator.CampaignAssetBudget"/>'s contributions are, so this stays as pure
    /// and as synchronous as everything else here. Joining an object to the campaign it belongs to
    /// is a host's job and stays one.</para>
    ///
    /// <para><b>Every code here is one this validator already reports about a machine one
    /// character owns</b>, deliberately: a shared machine is over the same table, and a second
    /// vocabulary for the same fault would be a second thing for a reader to learn and a second
    /// list for <c>ValidationIssueStructureTests</c> to hold.</para>
    ///
    /// <para><b>Reported, never repaired.</b> Every figure the ledger prints still answers what
    /// the campaign says, which is what makes the finding worth printing beside it.</para>
    /// </summary>
    /// <param name="asset">The campaign's shared object.</param>
    /// <param name="contributions">
    /// Every contribution the caller has collected, from any member — the same set
    /// <see cref="CostCalculator.CampaignAssetBudget"/> is handed. Optional, and null when a
    /// caller has only the object: a contribution naming another asset is skipped, exactly as
    /// <c>CampaignAssetBudget</c> skips it, and each one naming this asset is held to the same cap
    /// <see cref="CheckCampaignAssets"/> already holds a character's own contributions to, plus the
    /// kind-mismatch check <see cref="CheckContributionAgainstAsset"/> makes — belt and braces with
    /// that check, since a campaign page reviewing every member's contribution against the object
    /// they funded is exactly the place both would otherwise go unnoticed.
    /// </param>
    /// <returns>What is wrong with it, or nothing.</returns>
    public IReadOnlyList<ValidationIssue> CheckSharedAsset(
        CampaignAsset asset, IEnumerable<CampaignAssetContribution>? contributions = null)
    {
        ArgumentNullException.ThrowIfNull(asset);

        var issues = new List<ValidationIssue>();

        foreach (var contribution in contributions ?? [])
        {
            if (!string.Equals(contribution.AssetId, asset.Id, StringComparison.Ordinal)) continue;

            issues.AddRange(CheckContributionAgainstAsset(contribution, asset));

            if (contribution.HeroPoints > CampaignAssetContribution.MaxHeroPoints)
                issues.Add(new(ValidationSeverity.Error, "CAMPAIGN_ASSET_CONTRIBUTION_TOO_LARGE",
                    $"'{(string.IsNullOrWhiteSpace(contribution.Name) ? contribution.AssetId : contribution.Name)}' "
                    + $"records {contribution.HeroPoints} Hero Points put in, above the "
                    + $"{CampaignAssetContribution.MaxHeroPoints} the owner has ruled a game would "
                    + "never exceed on a single Hero.")
                {
                    SubjectKind = asset.IsHeadquarters
                        ? ValidationSubject.Headquarters
                        : ValidationSubject.Vehicle,
                    SubjectId   = string.IsNullOrWhiteSpace(asset.Name) ? asset.Id : asset.Name,
                    Value       = contribution.HeroPoints,
                    Limit       = CampaignAssetContribution.MaxHeroPoints
                });
        }

        // A shared object is named by the table and identified by its id, so — unlike a machine on
        // a sheet — a blank name is not a reason to stop. The id is what every contribution names
        // and it is what a sentence can be written about.
        var name = string.IsNullOrWhiteSpace(asset.Name) ? asset.Id : asset.Name;

        var kind = asset.IsHeadquarters
            ? ValidationSubject.Headquarters
            : ValidationSubject.Vehicle;

        // **A kind that is neither spelling is read as a vehicle and said out loud**, which is
        // what CampaignAsset.IsHeadquarters' own remarks promise and what nothing was doing: the
        // reading is silent by design, so that one mistyped field cannot take a campaign page
        // down, and the whole of that trade is that somebody says so instead. The same code the
        // contribution's own kind is reported under — one word for one mistake.
        if (!CampaignAssetContribution.Kinds.Contains(asset.Kind, StringComparer.Ordinal))
            issues.Add(new(ValidationSeverity.Error, "UNKNOWN_CAMPAIGN_ASSET_KIND",
                $"{name} is written down as a '{asset.Kind}', which Chapter 6 does not have. It is "
                + "priced as a vehicle. Hero Points can be pooled on a vehicle or on a "
                + "headquarters.")
            {
                SubjectKind = kind,
                SubjectId   = name,
                Options     = CampaignAssetContribution.Kinds
            });

        // The two feature tables, the same lookup CheckVehicle and CheckHeadquarters make. An
        // unknown id, a graded feature with no grade and a per-unit one bought no times or fewer
        // are all already spelled there, and all three reach a campaign's payload the same way.
        CheckAssetFeatures(
            name, asset.Features,
            id => asset.IsHeadquarters
                ? _rules.Assets.FindBaseFeature(id) is { } b ? (b.Name, b.CostType, b.CostRange) : null
                : _rules.Assets.FindVehicleFeature(id) is { } v ? (v.Name, v.CostType, v.CostRange) : null,
            [.. (asset.IsHeadquarters
                    ? _rules.Assets.BaseFeatures.Select(f => f.Id)
                    : _rules.Assets.VehicleFeatures.Select(f => f.Id))],
            issues);

        // pp.100-103 give a headquarters no characteristics at all, so there is nothing below to
        // say about one — CampaignAssetPointsSpent charges none of them for the same reason. The
        // structured prerequisite is the same story: BaseFeatureRow carries no such field, because
        // nothing on those pages prints one, so this only ever has something to say about a
        // vehicle.
        if (asset.IsHeadquarters) return issues;

        CheckVehicleFeaturePrerequisites(name, asset.Features, issues);

        // p.96 opens Body, Speed and Control at nothing and you spend upward, so a rank below that
        // **pays Vehicle Points back**: Body at −20 buys twenty points of features for nothing and
        // reads as an object comfortably inside its budget. Control is the one that may be
        // negative — the book says so and floors it — and it is checked by CheckControl instead.
        foreach (var (what, rank) in new[]
                 { ("Body", asset.Body), ("Speed", asset.Speed), ("Weapons", asset.Weapons ?? 0) }
                     .Where(c => c.Item2 < 0))
            issues.Add(Negative("NEGATIVE_RANK", kind, name,
                $"{name} has {what} {rank}d. A vehicle's characteristics are bought from nothing, "
                + "and a rank below that pays Vehicle Points back rather than costing them. Only "
                + "Control may be negative.", rank));

        CheckControl(name, asset.Control, asset.Speed, issues);

        return issues;
    }

    /// <summary>
    /// <b>Ruling 8: a contribution whose declared kind disagrees with the object it names.</b> A
    /// <see cref="CampaignAssetContribution"/> copies its <c>Kind</c> from the campaign's asset
    /// when a host writes it, but nothing stops a hand-written payload — a build that spelled the
    /// id right and the kind wrong, or an asset whose kind changed after the contribution was
    /// saved — from disagreeing. <see cref="CostCalculator.CampaignAssetBudget"/> and
    /// <see cref="CostCalculator.CampaignAssetPointsPerHeroPoint"/> both read the <em>asset's</em>
    /// kind, so a mismatched contribution is silently priced at the object's own currency rather
    /// than the one it claims — which is exactly the silence the owner ruled should end.
    ///
    /// <para><b>Pure, and handed both objects rather than resolving either.</b> A contribution and
    /// the asset it names live on opposite sides of the engine's no-storage line — one is on a
    /// <see cref="CharacterSheet"/>, the other belongs to a <see cref="Campaign"/> — and nothing
    /// in <c>engine/</c> may join them. A host that already has both in hand (a campaign page
    /// reviewing a member's contribution, or a member's own screen once its campaign is resolved)
    /// calls this directly; <see cref="CheckSharedAsset"/> also calls it for every contribution it
    /// is handed, so a campaign-wide review reaches it without a second call.</para>
    ///
    /// <para><b>Reported, never repaired.</b> The engine does not decide which of the two the
    /// player meant — see <c>CLAUDE.md</c>'s rule that an illegal character is reported, not
    /// fixed.</para>
    /// </summary>
    /// <param name="contribution">One character's contribution.</param>
    /// <param name="asset">The campaign's own record of the object the contribution names.</param>
    /// <returns>
    /// A single <c>CAMPAIGN_ASSET_KIND_MISMATCH</c> error when the two disagree, or nothing.
    /// Nothing at all when <paramref name="contribution"/> names a different object — that is
    /// <see cref="CampaignAssets.Orphaned"/>'s question, not this one.
    /// </returns>
    public static IReadOnlyList<ValidationIssue> CheckContributionAgainstAsset(
        CampaignAssetContribution contribution, CampaignAsset asset)
    {
        ArgumentNullException.ThrowIfNull(contribution);
        ArgumentNullException.ThrowIfNull(asset);

        if (!string.Equals(contribution.AssetId, asset.Id, StringComparison.Ordinal)) return [];
        if (string.Equals(contribution.Kind, asset.Kind, StringComparison.Ordinal)) return [];

        var name = string.IsNullOrWhiteSpace(contribution.Name) ? contribution.AssetId : contribution.Name;

        return [new(ValidationSeverity.Error, "CAMPAIGN_ASSET_KIND_MISMATCH",
            $"'{name}' is recorded as a '{contribution.Kind}' contribution, but the campaign's own "
            + $"object is a '{asset.Kind}'. Priced silently at the object's own currency, which is "
            + "not what the contribution says it is buying.")
        {
            SubjectKind = ValidationSubject.Character,
            SubjectId   = contribution.AssetId,
            OwnerId     = asset.Kind
        }];
    }

    /// <summary>
    /// <b>The same machine recorded twice is paid for twice.</b> A vehicle carries the Hero Points
    /// its own Unique Vehicle Perk cost, and a <c>SelectedPerk</c> naming that Perk is a second
    /// spend somebody wrote down — <see cref="CostCalculator.TotalCost"/> charges both, correctly,
    /// because both are on the sheet.
    ///
    /// <para><b>A warning rather than an error, and the distinction is the point.</b> Nothing here
    /// is illegal: a character may buy the Perk twice over, and a machine the player has not
    /// detailed yet is a perfectly ordinary way to hold the points. What is likely is that they
    /// meant one. Reported, never repaired — the total stays what the sheet says.</para>
    /// </summary>
    private static void CheckAssetPerksAreNotRecordedTwice(
        CharacterSheet sheet, List<ValidationIssue> issues)
    {
        Report("unique_vehicle", "Unique Vehicle", sheet.Vehicles.Count,
               sheet.Vehicles.Sum(v => v.PerkHeroPoints), "vehicle");

        Report("headquarters", "Headquarters", sheet.Headquarters.Count,
               sheet.Headquarters.Sum(h => h.PerkHeroPoints), "headquarters");

        void Report(string perkId, string perkName, int owned, int onTheAssets, string noun)
        {
            if (owned == 0) return;

            var onThePerk = sheet.Perks
                .Where(p => string.Equals(p.PerkId, perkId, StringComparison.Ordinal))
                .Sum(p => p.Units);

            if (onThePerk == 0) return;

            issues.Add(new(ValidationSeverity.Warning, "ASSET_PERK_RECORDED_TWICE",
                $"The {perkName} Perk is bought for {onThePerk} Hero "
                + $"Point{(onThePerk == 1 ? "" : "s")} and the {noun}"
                + $"{(owned == 1 ? "" : "s")} on this sheet already record {onTheAssets}. Both are "
                + "charged, so the same purchase may have been paid for twice.")
            {
                SubjectKind = ValidationSubject.Character,
                SubjectId   = perkId,
                Value       = onThePerk + onTheAssets,
                Limit       = onTheAssets
            });
        }
    }

    /// <summary>
    /// The features on one vehicle or base: an id the rulebook does not have, a graded one with no
    /// grade, and a per-unit one bought no times. Written once because both tables price features
    /// the same three ways and only the currency differs.
    ///
    /// <para>Every finding files itself against the feature with the owner's <em>name</em> in
    /// <see cref="ValidationIssue.OwnerId"/> — the same shape a gear feature's finding takes, and
    /// the reason this needs no owner-kind argument: a caller looking for the row already has the
    /// collection it came from.</para>
    ///
    /// <para>Returns false when anything here cannot be priced, which is what stops the caller
    /// asking for a total that would throw.</para>
    /// </summary>
    private static bool CheckAssetFeatures(
        string ownerName,
        IReadOnlyList<SelectedAssetFeature> features,
        Func<string, (string Name, string CostType, IReadOnlyDictionary<string, int>? Grades)?> lookup,
        IReadOnlyList<string> everyId,
        List<ValidationIssue> issues)
    {
        var priceable = true;

        foreach (var selection in features)
        {
            var found = selection?.FeatureId is null ? null : lookup(selection.FeatureId);

            if (found is not { } feature)
            {
                issues.Add(new(ValidationSeverity.Error, "UNKNOWN_ASSET_FEATURE",
                    selection?.FeatureId is null
                        ? $"{ownerName} has a feature with no name at all."
                        : $"{ownerName} has a feature, '{selection.FeatureId}', that is not one "
                          + "Chapter 6 lists.")
                {
                    SubjectKind = ValidationSubject.AssetFeature,
                    SubjectId   = selection?.FeatureId,
                    OwnerId     = ownerName,
                    Options     = everyId
                });
                priceable = false;
                continue;
            }

            if (feature.CostType == "flat_variable"
                && (selection!.GradeKey is null || feature.Grades?.ContainsKey(selection.GradeKey) != true))
            {
                issues.Add(new(ValidationSeverity.Error, "ASSET_FEATURE_NEEDS_GRADE",
                    $"{ownerName}'s {feature.Name} is priced by grade, and no grade Chapter 6 "
                    + $"lists has been chosen. Pick one of: {Names(feature.Grades?.Keys)}.")
                {
                    SubjectKind = ValidationSubject.AssetFeature,
                    SubjectId   = selection.FeatureId,
                    OwnerId     = ownerName,
                    Options     = [.. feature.Grades?.Keys ?? []]
                });
                priceable = false;
            }

            if (feature.CostType == "per_unit" && selection!.Units <= 0)
            {
                issues.Add(new(ValidationSeverity.Error, "PER_UNIT_WITHOUT_UNITS",
                    $"{ownerName}'s {feature.Name} is priced by the unit and "
                    + $"{(selection.Units == 0 ? "none has been bought" : $"{selection.Units} have been bought")}, "
                    + "so it would "
                    + (selection.Units == 0
                        ? "cost nothing and do nothing."
                        : "pay points back rather than cost them."))
                {
                    SubjectKind = ValidationSubject.AssetFeature,
                    SubjectId   = selection.FeatureId,
                    OwnerId     = ownerName,
                    Value       = selection.Units,
                    Limit       = 1
                });
                priceable = false;
            }
        }

        return priceable;
    }

    /// <summary>
    /// Sources (Ch.2, p.16). A Source costs nothing and changes no rank, so a missing one
    /// is never an error — but a Power the rulebook gives no rank needs its Source to know
    /// which Ability stands in when another Power acts on it, so that gap is worth saying.
    ///
    /// <para><b>An Ability or Talent with no Source is not a gap at all</b>, and is
    /// deliberately not reported. The rulebook supplies a default for both — Abilities are
    /// usually Innate, Talents usually Trained — so silence there means "on its default",
    /// not "unanswered". Powers have no such default, which is why they warn and Traits do
    /// not.</para>
    /// </summary>
    private void CheckSources(CharacterSheet sheet, List<ValidationIssue> issues)
    {
        CheckTraitSources(sheet.AbilitySources, ValidationSubject.Ability, "Ability", "Abilities",
            id => _rules.GetAbility(id)?.Name, issues);
        CheckTraitSources(sheet.TalentSources, ValidationSubject.Talent, "Talent", "Talents",
            id => _rules.GetTalent(id)?.Name, issues);

        foreach (var sp in sheet.SelectedPowers)
        {
            if (sp.SourceId is not null && _rules.GetSource(sp.SourceId) is null)
            {
                issues.Add(new(ValidationSeverity.Error, "UNKNOWN_SOURCE",
                    $"{PowerName(sp.PowerId)} names a Source, '{sp.SourceId}', that is not one "
                    + "of the six the rulebook gives.")
                {
                    SubjectKind = ValidationSubject.Power,
                    SubjectId   = sp.PowerId,
                    Options     = SourceIds
                });
                continue;
            }

            var power = Power(sp.PowerId);
            if (power is null || sp.SourceId is not null) continue;

            if (power.RankType is "default" or "special")
                issues.Add(new(ValidationSeverity.Warning, "RANKLESS_POWER_WITHOUT_SOURCE",
                    $"Power '{power.Name}' has no rank of its own, so it needs a Source to " +
                    "supply the default rank used when another Power acts on it " +
                    "(Drain, Nullify, Dispel, Power Absorption, Power Mimicry).")
                {
                    SubjectKind = ValidationSubject.Power,
                    SubjectId   = sp.PowerId,
                    Options     = SourceIds
                });
            else
                issues.Add(new(ValidationSeverity.Warning, "POWER_WITHOUT_SOURCE",
                    $"Power '{power.Name}' has no Source recorded. A published sheet groups " +
                    "Powers under Source headings, so the sheet will list it as unsourced.")
                {
                    SubjectKind = ValidationSubject.Power,
                    SubjectId   = sp.PowerId,
                    Options     = SourceIds
                });
        }
    }

    /// <summary>
    /// Sources recorded against Abilities or Talents. Two things can be wrong and both are
    /// errors rather than warnings: a Source that is not one of the six, and a Source
    /// recorded against a Trait that does not exist. Neither can be rendered, and neither
    /// is something a player chose — they mean a hand-edited or stale saved character.
    /// </summary>
    private void CheckTraitSources(
        IReadOnlyDictionary<string, string> sources,
        ValidationSubject kind,
        string traitKind,
        string traitKindPlural,
        Func<string, string?> nameOf,
        List<ValidationIssue> issues)
    {
        foreach (var (traitId, sourceId) in sources)
        {
            var name = nameOf(traitId);

            if (name is null)
            {
                // "the Abilities in the rulebook", not "the abilitys": the plural is written
                // out rather than built by appending an s to a lowercased word.
                issues.Add(new(ValidationSeverity.Error, "UNKNOWN_TRAIT_SOURCE",
                    $"A Source is recorded against '{traitId}', which is not one of the "
                    + $"{traitKindPlural} in the rulebook.")
                {
                    SubjectKind = kind,
                    SubjectId   = traitId,
                    Options     = kind == ValidationSubject.Ability
                        ? _rules.Abilities.Select(a => a.Id).ToList()
                        : _rules.Talents.Select(t => t.Id).ToList()
                });
                continue;
            }

            // Null-checked before the lookup, not after. A stored character can carry a null
            // here — the type says it cannot, and the deserializer does not care — and
            // GetSource would throw ArgumentNullException on it rather than report it. That
            // is the ordering trap this validator has already been caught by twice: an
            // unknown Power id, and gear that could not be priced.
            //
            // A blank gets its own sentence. Printed through the message below it read "names
            // a Source, '', that is not one of the six", which says the Trait names a Source
            // and then names none.
            if (string.IsNullOrWhiteSpace(sourceId))
                issues.Add(new(ValidationSeverity.Error, "UNKNOWN_SOURCE",
                    $"The {traitKind} '{name}' has a Source recorded against it with no value. "
                    + "Choose one of the six the rulebook gives, or leave it on its default.")
                {
                    SubjectKind = kind,
                    SubjectId   = traitId,
                    Options     = SourceIds
                });
            else if (_rules.GetSource(sourceId) is null)
                issues.Add(new(ValidationSeverity.Error, "UNKNOWN_SOURCE",
                    $"The {traitKind} '{name}' names a Source, '{sourceId}', that is not one "
                    + "of the six the rulebook gives.")
                {
                    SubjectKind = kind,
                    SubjectId   = traitId,
                    Options     = SourceIds
                });
        }
    }

    private void CheckPowerCosts(CharacterSheet sheet, List<ValidationIssue> issues)
    {
        foreach (var sp in sheet.SelectedPowers)
        {
            if (!sp.Cons.Any()) continue;

            var power = Power(sp.PowerId);
            if (power is null) continue;

            // Report when Cons have driven the cost down to the rulebook floor, since
            // any further Cons on this Power buy the character nothing.
            var cost    = _costs.PowerCost(sp, sheet.ImmortalityCost);
            var atFloor = power.CostType is "flat" or "flat_variable" or "per_unit"
                ? cost == 1
                : cost <= Math.Max(1, (int)Math.Ceiling(sp.PurchasedRanks / 2.0));

            if (atFloor)
                issues.Add(new(ValidationSeverity.Warning, "POWER_COST_AT_MINIMUM",
                    $"Power '{power.Name}' has reached the minimum cost the rulebook allows " +
                    $"({cost} HP) after its cons. Further cons will not reduce it.")
                {
                    SubjectKind = ValidationSubject.Power,
                    SubjectId   = sp.PowerId,
                    Value       = cost,
                    Limit       = cost
                });
        }
    }

    /// <summary>
    /// Powers the rulebook gives no rank cannot have ranks bought for them.
    /// </summary>
    private void CheckPowerRanks(CharacterSheet sheet, List<ValidationIssue> issues)
    {
        foreach (var sp in sheet.SelectedPowers)
        {
            var power = Power(sp.PowerId);
            if (power is null) continue;

            if (power.MaxRank == 0 && sp.PurchasedRanks > 0)
                issues.Add(new(ValidationSeverity.Error, "POWER_HAS_NO_RANK",
                    $"{power.Name} has no rank to buy — it is priced as a whole — but "
                    + $"{sp.PurchasedRanks} {(sp.PurchasedRanks == 1 ? "rank was" : "ranks were")} bought.")
                {
                    SubjectKind = ValidationSubject.Power,
                    SubjectId   = sp.PowerId,
                    Value       = sp.PurchasedRanks,
                    Limit       = 0
                });
        }
    }

    /// <summary>
    /// <b>The two things about a Power's own selection that make <c>CostCalculator</c> throw</b>:
    /// a cost variant that is absent or resolves to nothing, and a missing nomination on a Power
    /// whose rate comes from the nominated Trait. Both are absent-and-unknown together, because
    /// the repair is the same and the second reached the calculator where the first was reported.
    ///
    /// <para><b>Shared, because a Gadget's Powers are Powers.</b> p.94 buys them under the
    /// ordinary rules and <c>GadgetSpend</c> prices them through <c>PowerCost</c>, so they throw
    /// in exactly these two places — and <c>GadgetIsPriceable</c> checked the Power id alone. An
    /// Omni-Power inside a Gadget with a cost variant the rulebook does not have took
    /// <c>Validate</c> out with an <c>InvalidOperationException</c>. A second copy of these two
    /// clauses would be a second thing to fix one of; this is the one.</para>
    /// </summary>
    /// <param name="insideGadget">The Gadget carrying this Power, or null on the character's own.</param>
    /// <returns>False when the Power cannot be priced at all.</returns>
    private bool CheckPowerIsPriceable(
        SelectedPower sp, PowerModel power, string? insideGadget, List<ValidationIssue> issues)
    {
        var resolvable = true;

        // The owner clause, and nothing at all when the Power is the character's own: "Omni-Power"
        // alone in a finding does not say which of two budgets it is against.
        var whose = insideGadget is null ? "" : $"On the Gadget {insideGadget}: ";

        if (power.CostType is "per_rank_variable" or "flat_variable"
            && (sp.CostVariantKey is null || power.CostVariants?.ContainsKey(sp.CostVariantKey) != true))
        {
            issues.Add(new(ValidationSeverity.Error, "POWER_VARIANT_NOT_CHOSEN",
                $"{whose}{power.Name} costs a different amount depending on which version you "
                + $"take, and none the rulebook lists has been chosen. "
                + $"Pick one of: {Names(power.CostVariants?.Keys)}.")
            {
                SubjectKind = ValidationSubject.Power,
                SubjectId   = sp.PowerId,
                OwnerId     = insideGadget,
                Options     = Keys(power.CostVariants?.Keys)
            });
            resolvable = false;
        }

        // An unknown nomination threw for Boost, whose cost comes from the nominated Trait, and
        // silently gave Expertise a baseline of nothing — two wrong answers to the one mistake.
        if (power.Prerequisite?.Relationship == "baseline_selected_trait"
            && (sp.BaselineTraitId is null || !IsATrait(sp.BaselineTraitId)))
        {
            issues.Add(new(ValidationSeverity.Error, "POWER_BASELINE_TRAIT_NOT_CHOSEN",
                $"{whose}Power '{power.Name}' derives its baseline rank from a Trait the player " +
                "nominates, and no Trait the rulebook has is recorded.")
            {
                // The nomination may be any ability, talent or power, so there is no short
                // list to offer — which is itself the answer, and the code says which
                // field is missing.
                SubjectKind = ValidationSubject.Power,
                SubjectId   = sp.PowerId,
                OwnerId     = insideGadget
            });

            // Boost also takes its cost per rank from that Trait.
            if (power.CostType == "special") resolvable = false;
        }

        return resolvable;
    }

    /// <summary>
    /// Powers whose cost or baseline depends on a player choice are unresolvable until
    /// that choice is recorded on the selection.
    /// </summary>
    /// <returns>
    /// False when at least one power cannot be priced yet, so cost-dependent checks
    /// must be skipped this pass.
    /// </returns>
    private bool CheckPowerSelections(CharacterSheet sheet, List<ValidationIssue> issues)
    {
        var resolvable = true;

        foreach (var sp in sheet.SelectedPowers)
        {
            // Null-checked before the lookup: GetPower throws on a null id, which is what
            // `{"SelectedPowers":[{}]}` supplies.
            var power = Power(sp.PowerId);
            if (power is null)
            {
                issues.Add(new(ValidationSeverity.Error, "UNKNOWN_POWER",
                    sp.PowerId is null
                        ? "One of the Powers has no name at all."
                        : $"There is no Power called '{sp.PowerId}' in the rulebook data.")
                {
                    // No Options here, deliberately. There are 141 Powers and the caller
                    // already has the list; repeating it on every typo would make the report
                    // the largest thing in the exchange.
                    SubjectKind = ValidationSubject.Power,
                    SubjectId   = sp.PowerId
                });
                resolvable = false;
                continue;
            }

            resolvable &= CheckPowerIsPriceable(sp, power, insideGadget: null, issues);

            // A nomination that is missing or unresolvable is the finding above; one that resolves
            // to a *Power* is this one, and only on Expertise. Ch.2 p.28 narrows that Power alone
            // — "Your specialization must fall under one of your Abilities or Talents", and the
            // printed baseline sentence agrees: "the rank of the Ability or Talent it falls
            // under". Boost's own text (Ch.2 p.24) says "one specific Ability, Talent, or Power",
            // so the two cannot share a rule and this is scoped by id rather than by the
            // baseline_selected_trait relationship they both carry.
            //
            // Reported, never repaired: DerivedStatsCalculator used to answer p.83's own
            // attack-or-defence question of the nominated Power, which gave an illegal sheet a
            // defensible Resolve and left the mistake invisible.
            if (string.Equals(sp.PowerId, "expertise", StringComparison.Ordinal)
                && sp.BaselineTraitId is { Length: > 0 } nomination
                && _rules.GetAbility(nomination) is null
                && _rules.GetTalent(nomination) is null
                && _rules.GetPower(nomination) is { } nominatedPower)
            {
                issues.Add(new(ValidationSeverity.Error, "EXPERTISE_NOMINATION_NOT_A_TRAIT",
                    $"This Expertise falls under the Power '{nominatedPower.Name}', and a "
                    + "specialisation has to fall under one of your Abilities or Talents "
                    + "(Ch.2 p.28). Nominate an Ability or a Talent instead — Boost is the Power "
                    + "that may be nominated to another Power.")
                {
                    // No Options, for the reason the finding above has none: the six Abilities
                    // and twelve Talents are lists the caller already holds, and the subject is
                    // the Power, so an option list here would be values of the wrong kind.
                    SubjectKind = ValidationSubject.Power,
                    SubjectId   = sp.PowerId
                });
            }
        }

        return resolvable;
    }

    /// <summary>
    /// Reports powers whose mechanics have not been checked against the rulebook, and
    /// separately notes that descriptions are project paraphrase rather than rules text.
    /// </summary>
    private void CheckUnverifiedPowers(CharacterSheet sheet, List<ValidationIssue> issues)
    {
        foreach (var sp in sheet.SelectedPowers)
        {
            var power = Power(sp.PowerId);
            if (power is null) continue;

            if (power.NeedsReview)
                issues.Add(new(ValidationSeverity.Warning, "POWER_MECHANICS_UNVERIFIED",
                    $"{power.Name} has not been fully checked against the rulebook here. "
                    + "Read its entry and agree the numbers with your GM before play.")
                {
                    SubjectKind = ValidationSubject.Power,
                    SubjectId   = sp.PowerId
                });
        }

        var unverifiedText = sheet.SelectedPowers
            .Select(sp => Power(sp.PowerId))
            .OfType<PowerModel>()
            .Where(p => !p.DescriptionVerified)
            .Select(p => p.Name)
            .Distinct()
            .ToList();

        if (unverifiedText.Count > 0)
            issues.Add(new(ValidationSeverity.Warning, "POWER_DESCRIPTION_UNVERIFIED",
                $"The wording shown for {string.Join(", ", unverifiedText)} has not been checked "
                + "against the rulebook entry. Costs and ranks are unaffected — read the page "
                + "cited on the Power before relying on the description."));
    }

    /// <summary>
    /// A Power's printed name, falling back to its id only when the rules do not have it —
    /// which is itself an error reported elsewhere.
    ///
    /// <para>Every message here is shown to a player, on the GM review step and in both
    /// exports. None of them may print an id, a file name or an internal flag; there is a
    /// test that says so.</para>
    /// </summary>
    private string PowerName(string? powerId) =>
        powerId is null ? "A Power with no name" : TraitName(Power(powerId)?.Name, powerId);

    /// <summary>
    /// The rules lookups this class makes, made safe for an id that is null.
    ///
    /// <para><b>The repository's own lookups throw on a null id rather than answering</b>, which
    /// is right for it — a null id is a programming error to everything except this class, whose
    /// job is reading what somebody else wrote. <c>{"Perks":[{}]}</c> is well-formed JSON and
    /// supplies exactly that. Routing every lookup through these three is what makes "a null id
    /// is reported by name" true of the whole file rather than of the four places somebody
    /// remembered.</para>
    /// </summary>
    private PowerModel? Power(string? id) => id is null ? null : _rules.GetPower(id);

    private PerkModel? Perk(string? id) => id is null ? null : _rules.GetPerk(id);

    private FlawModel? Flaw(string? id) => id is null ? null : _rules.GetFlaw(id);

    /// <summary>
    /// A Trait's printed name, or its id **in quotes** when the rules do not have it.
    ///
    /// <para>The quotes are the whole point. A message may print an id the player themselves
    /// supplied — an unknown one has no name to print instead — but only inside quotes, which
    /// is how <c>ValidationMessageTests</c> tells "the rulebook calls this Intellect" from
    /// "you wrote this". The bare fallback here printed <c>hand_to_hand is 40d</c>, which
    /// broke both that rule and the one requiring a sentence to start with a capital.</para>
    /// </summary>
    private static string TraitName(string? name, string id) => name ?? $"'{id}'";

    private static string Flaws(int count) => count == 1 ? "1 flaw" : $"{count} flaws";

    /// <summary>The six Sources, for an issue whose fix is choosing one of them.</summary>
    private IReadOnlyList<string> SourceIds => _rules.Sources.Select(s => s.Id).ToList();

    /// <summary>
    /// What an option says about where it may be applied, as a sentence. The rulebook states
    /// this inside the option — "This Pro applies to Zone Powers" — so the message quotes the
    /// constraint rather than the Power, which is where a reader would otherwise go looking.
    /// </summary>
    private static string Applicability(IGenericProCon option)
    {
        var parts = new List<string>();

        if (option.AppliesToRanges.Count > 0)
            parts.Add($"a Range of {Names(option.AppliesToRanges)}");

        if (option.AppliesToRankTypes.Count > 0)
            parts.Add($"a rank of {Names(option.AppliesToRankTypes)}");

        return parts.Count == 0
            ? "The rulebook does not say where it applies."
            : $"It applies to Powers with {string.Join(" and ", parts)}.";
    }

    /// <summary>The six tiers, likewise.</summary>
    private IReadOnlyList<string> TierIds => _rules.Tiers.Select(t => t.Id).ToList();

    /// <summary>
    /// Whether an id names a Trait a Power can derive its baseline from. Ch.2 lets the
    /// nomination be an Ability, a Talent or another Power, which is why this is three
    /// lookups and not a list.
    /// </summary>
    private bool IsATrait(string id) =>
        _rules.GetAbility(id) is not null ||
        _rules.GetTalent(id) is not null ||
        _rules.GetPower(id) is not null;

    /// <summary>
    /// The same keys <see cref="Names"/> sets as prose, left as keys. The message humanises
    /// them for a reader; a caller has to write one back into the character, and
    /// <c>Very Accurate</c> is not a value the data accepts.
    /// </summary>
    private static List<string> Keys(IEnumerable<string>? keys) => (keys ?? []).ToList();

    /// <summary>
    /// A list of grade or variant keys, set as prose. These are the one kind of value in the
    /// rules with no printed name of its own — <c>very_accurate</c> is a dictionary key, not
    /// a field in the data — so the key is all there is, and it at least gets sentence case
    /// and an "or" before the last one.
    /// </summary>
    private static string Names(IEnumerable<string>? keys)
    {
        var names = (keys ?? []).Select(Humanise).ToList();

        return names.Count switch
        {
            0 => "none",
            1 => names[0],
            _ => $"{string.Join(", ", names.Take(names.Count - 1))} or {names[^1]}"
        };
    }

    private static string Humanise(string key) =>
        string.Join(' ', key.Split('_', StringSplitOptions.RemoveEmptyEntries)
            .Select(w => char.ToUpperInvariant(w[0]) + w[1..]));
}
