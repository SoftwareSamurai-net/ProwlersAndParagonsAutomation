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
    Flaw
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
    /// The thing the subject sits on, where the subject is not top-level: the name of the
    /// piece of gear a feature belongs to. Without it a caller can find the feature id and
    /// not the item to change it on.
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

        if (tier is not null)
        {
            if (selectionsResolvable && modifiersResolvable && perksResolvable && gearResolvable)
                CheckHpBudget(sheet, tier, issues);
            CheckTraitCap(sheet, tier, issues);
            CheckIconicTier(tier, issues);
        }

        CheckTraitIds(sheet, issues);
        CheckFlawCount(sheet, issues);
        CheckFlawIds(sheet, issues);
        CheckDuplicateFlaws(sheet, issues);
        CheckDuplicatePowers(sheet, issues);
        // Both flags: this prices each Power, and a Pro or Con the rulebook does not have
        // throws from there just as surely as a missing cost variant does.
        if (selectionsResolvable && modifiersResolvable) CheckPowerCosts(sheet, issues);
        CheckUnverifiedPowers(sheet, issues);
        CheckSources(sheet, issues);

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

    private void CheckTraitCap(CharacterSheet sheet, TierModel tier, List<ValidationIssue> issues)
    {
        var cap = tier.TraitCapRank;

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
            if (_rules.GetPower(sp.PowerId) is null) continue;

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
                $"The perk '{_rules.GetPerk(perk.PerkId)?.Name ?? perk.PerkId}' is bought "
                + $"{perk.Units} times, which would pay the character Hero Points rather than "
                + "cost them.", perk.Units));
    }

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

        foreach (var perk in sheet.Perks.Where(p => _rules.GetPerk(p.PerkId) is null))
        {
            issues.Add(new(ValidationSeverity.Error, "UNKNOWN_PERK",
                $"There is no perk called '{perk.PerkId}' in the rulebook data.")
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
    /// or on an Ability. They are priced the same way in all three places, so they go wrong
    /// the same way in all three, and an unknown id threw out of the middle of the total.
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
            var power = _rules.GetPower(sp.PowerId);
            resolvable &= CheckModifierList(sp.Pros, isPro: true, power, PowerName(sp.PowerId), issues);
            resolvable &= CheckModifierList(sp.Cons, isPro: false, power, PowerName(sp.PowerId), issues);
        }

        foreach (var gear in sheet.Gear)
        {
            resolvable &= CheckModifierList(gear.Pros, isPro: true, null, gear.Name, issues);
            resolvable &= CheckModifierList(gear.Cons, isPro: false, null, gear.Name, issues);
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
                    SubjectKind = ValidationSubject.Ability,
                    SubjectId   = abilityId,
                    Options     = sheet.AbilityRanks.Keys.ToList()
                });
                continue;
            }

            // Cons only, on the Cons list, is how the pickers offer them — but a submitted
            // file can put anything here, and both are priced, so both are checked.
            resolvable &= CheckModifierList(modifiers, isPro: false, null, name, issues, alsoTryPros: true);
        }

        return resolvable;
    }

    private bool CheckModifierList(
        IReadOnlyList<SelectedProCon> modifiers,
        bool isPro,
        PowerModel? power,
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
            // A null id is a Pro or Con with no name, which only a hand-written file produces.
            // Reported as unknown, which is what it is, and refused before anything looks it up.
            if (choice.Id is null)
            {
                issues.Add(new(ValidationSeverity.Error, isPro ? "UNKNOWN_PRO" : "UNKNOWN_CON",
                    $"{ownerName} has a {kind} with no name at all.")
                {
                    SubjectKind = ValidationSubject.Character,
                    OwnerId     = ownerName
                });
                resolvable = false;
                continue;
            }

            if (!seen.Add(choice.Id))
            {
                issues.Add(new(ValidationSeverity.Error,
                    isPro ? "DUPLICATE_PRO" : "DUPLICATE_CON",
                    $"{ownerName} carries the {kind} '{choice.Id}' more than once. It applies "
                    + "once, and a second copy would discount the same thing twice.")
                {
                    SubjectKind = ValidationSubject.Character,
                    SubjectId   = choice.Id,
                    OwnerId     = ownerName
                });
                continue;
            }

            // The Power's own entry wins, as in CostCalculator.ResolveModifiers.
            var specific = (isPro ? power?.PowerPros : power?.PowerCons)
                ?.FirstOrDefault(x => x.Id == choice.Id);

            if (specific is not null) continue;

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
                    OwnerId     = ownerName
                });
                resolvable = false;
                continue;
            }

            if (range is null) continue;

            // Priced by grade. Absent and wrong are the same repair — choose one of these —
            // so they are one finding with the keys attached.
            if (choice.VariantKey is null || !range.ContainsKey(choice.VariantKey))
            {
                issues.Add(new(ValidationSeverity.Error,
                    isPro ? "PRO_VARIANT_NOT_CHOSEN" : "CON_VARIANT_NOT_CHOSEN",
                    $"{ownerName}'s {kind} '{choice.Id}' is priced by grade, and no grade the "
                    + $"rulebook lists has been chosen. Pick one of: {Names(range.Keys)}.")
                {
                    SubjectKind = ValidationSubject.Character,
                    SubjectId   = choice.Id,
                    OwnerId     = ownerName,
                    Options     = Keys(range.Keys)
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
                $"The flaw '{_rules.GetFlaw(sf.FlawId)?.Name ?? sf.FlawId}' is taken more than "
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
    /// </summary>
    private void CheckDuplicatePowers(CharacterSheet sheet, List<ValidationIssue> issues)
    {
        var seen = new HashSet<string>(StringComparer.Ordinal);

        foreach (var sp in sheet.SelectedPowers.Where(p => p.PowerId is not null && !seen.Add(p.PowerId)))
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
            if (_rules.GetFlaw(sf.FlawId) is null)
                issues.Add(new(ValidationSeverity.Error, "UNKNOWN_FLAW",
                    $"There is no flaw called '{sf.FlawId}' in the rulebook data.")
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
            var itemResolvable = true;

            foreach (var f in gear.Features)
            {
                var feature = _rules.GetGearFeature(f.FeatureId);
                if (feature is null)
                {
                    issues.Add(new(ValidationSeverity.Error, "UNKNOWN_GEAR_FEATURE",
                        $"{gear.Name} has a custom feature, '{f.FeatureId}', that is not one "
                        + "the rulebook lists.")
                    {
                        SubjectKind = ValidationSubject.GearFeature,
                        SubjectId   = f.FeatureId,
                        OwnerId     = gear.Name,
                        Options     = _rules.GearFeatures.Select(g => g.Id).ToList()
                    });
                    itemResolvable = false;
                    continue;
                }

                if (feature.CostType != "flat"
                    && (f.GradeKey is null || feature.CostRange?.ContainsKey(f.GradeKey) != true))
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

            var power = _rules.GetPower(sp.PowerId);
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

            var power = _rules.GetPower(sp.PowerId);
            if (power is null) continue;

            // Report when Cons have driven the cost down to the rulebook floor, since
            // any further Cons on this Power buy the character nothing.
            var cost    = _costs.PowerCost(sp);
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
            var power = _rules.GetPower(sp.PowerId);
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
            var power = _rules.GetPower(sp.PowerId);
            if (power is null)
            {
                issues.Add(new(ValidationSeverity.Error, "UNKNOWN_POWER",
                    $"There is no Power called '{sp.PowerId}' in the rulebook data.")
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

            // Absent and present-but-unknown are one finding: the repair is the same, and the
            // second reached CostCalculator and threw where the first was reported.
            if (power.CostType is "per_rank_variable" or "flat_variable"
                && (sp.CostVariantKey is null || power.CostVariants?.ContainsKey(sp.CostVariantKey) != true))
            {
                issues.Add(new(ValidationSeverity.Error, "POWER_VARIANT_NOT_CHOSEN",
                    $"{power.Name} costs a different amount depending on which version you "
                    + $"take, and none the rulebook lists has been chosen. "
                    + $"Pick one of: {Names(power.CostVariants?.Keys)}.")
                {
                    SubjectKind = ValidationSubject.Power,
                    SubjectId   = sp.PowerId,
                    Options     = Keys(power.CostVariants?.Keys)
                });
                resolvable = false;
            }

            // Again absent and unknown together. An unknown nomination threw for Boost, whose
            // cost comes from the nominated Trait, and silently gave Expertise a baseline of
            // nothing — two different wrong answers to the same mistake.
            if (power.Prerequisite?.Relationship == "baseline_selected_trait"
                && (sp.BaselineTraitId is null || !IsATrait(sp.BaselineTraitId)))
            {
                issues.Add(new(ValidationSeverity.Error, "POWER_BASELINE_TRAIT_NOT_CHOSEN",
                    $"Power '{power.Name}' derives its baseline rank from a Trait the player " +
                    "nominates, and no Trait the rulebook has is recorded.")
                {
                    // The nomination may be any ability, talent or power, so there is no short
                    // list to offer — which is itself the answer, and the code says which
                    // field is missing.
                    SubjectKind = ValidationSubject.Power,
                    SubjectId   = sp.PowerId
                });

                // Boost also takes its cost per rank from that Trait.
                if (power.CostType == "special") resolvable = false;
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
            var power = _rules.GetPower(sp.PowerId);
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
            .Select(sp => _rules.GetPower(sp.PowerId))
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
    private string PowerName(string powerId) => TraitName(_rules.GetPower(powerId)?.Name, powerId);

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
