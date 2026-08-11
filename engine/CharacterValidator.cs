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
/// <para>All of it is optional and every property has a null or empty default, so the 26
/// construction sites that only ever wanted a sentence are unchanged. An issue that fills
/// none of them is not defective; some findings genuinely have nothing to locate.</para>
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
        var selectionsResolvable = CheckPowerSelections(sheet, issues);

        // Same reason, for gear: an unknown feature or a graded one with no grade cannot
        // be priced, so the gap has to be reported before anything asks for a total.
        var gearResolvable = CheckGear(sheet, issues);

        if (tier is not null)
        {
            if (selectionsResolvable && gearResolvable) CheckHpBudget(sheet, tier, issues);
            CheckTraitCap(sheet, tier, issues);
            CheckIconicTier(tier, issues);
        }

        CheckTraitIds(sheet, issues);
        CheckFlawCount(sheet, issues);
        CheckFlawIds(sheet, issues);
        if (selectionsResolvable) CheckPowerCosts(sheet, issues);
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
                SubjectKind = ValidationSubject.Character,
                Options     = TierIds
            });
        }
    }

    /// <summary>
    /// The starting package, which is optional and so is only ever wrong by being unknown.
    /// An unrecognised one costs nothing and grants nothing, so it was silently no package at
    /// all — and the character was priced at full rate for ranks the package would have paid
    /// for, which is a quiet 4 to 15 Hero Points of difference.
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
            Options     = packages.Select(p => p.Id).ToList()
        });
    }

    private void CheckHpBudget(CharacterSheet sheet, TierModel tier, List<ValidationIssue> issues)
    {
        var total = _costs.TotalCost(sheet);
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
                    $"{_rules.GetAbility(id)?.Name ?? id} is {rank}d, above the Trait Cap of {cap}d.")
                {
                    SubjectKind = ValidationSubject.Ability,
                    SubjectId   = id,
                    Value       = rank,
                    Limit       = cap
                });

        foreach (var (id, rank) in sheet.TalentRanks)
            if (rank > cap)
                issues.Add(new(ValidationSeverity.Error, "TRAIT_ABOVE_CAP",
                    $"{_rules.GetTalent(id)?.Name ?? id} is {rank}d, above the Trait Cap of {cap}d.")
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
                SubjectKind = ValidationSubject.Tier,
                SubjectId   = tier.Id,
                Limit       = tier.HeroPoints
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
    private bool CheckGear(CharacterSheet sheet, List<ValidationIssue> issues)
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

                if (feature.CostType != "flat" && f.GradeKey is null)
                {
                    issues.Add(new(ValidationSeverity.Error, "GEAR_FEATURE_NEEDS_GRADE",
                        $"{gear.Name}'s {feature.Name} feature is priced by grade, and no grade "
                        + $"has been chosen. Pick one of: {Names(feature.CostRange?.Keys)}.")
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
            if (itemResolvable && gear.Cons.Count > 0 && _costs.GearCost(gear) == 0)
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
                    SubjectKind = ValidationSubject.Gear,
                    SubjectId   = gear.Name,
                    Options     = ["two_fisted"]
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
                    SubjectId   = traitId
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

            if (power.CostType is "per_rank_variable" or "flat_variable" && sp.CostVariantKey is null)
            {
                issues.Add(new(ValidationSeverity.Error, "POWER_VARIANT_NOT_CHOSEN",
                    $"{power.Name} costs a different amount depending on which version you "
                    + $"take, and none has been chosen. Pick one of: {Names(power.CostVariants?.Keys)}.")
                {
                    SubjectKind = ValidationSubject.Power,
                    SubjectId   = sp.PowerId,
                    Options     = Keys(power.CostVariants?.Keys)
                });
                resolvable = false;
            }

            if (power.Prerequisite?.Relationship == "baseline_selected_trait" && sp.BaselineTraitId is null)
            {
                issues.Add(new(ValidationSeverity.Error, "POWER_BASELINE_TRAIT_NOT_CHOSEN",
                    $"Power '{power.Name}' derives its baseline rank from a Trait the player " +
                    "nominates, but none has been recorded.")
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
    private string PowerName(string powerId) => _rules.GetPower(powerId)?.Name ?? powerId;

    private static string Flaws(int count) => count == 1 ? "1 flaw" : $"{count} flaws";

    /// <summary>The six Sources, for an issue whose fix is choosing one of them.</summary>
    private IReadOnlyList<string> SourceIds => _rules.Sources.Select(s => s.Id).ToList();

    /// <summary>The six tiers, likewise.</summary>
    private IReadOnlyList<string> TierIds => _rules.Tiers.Select(t => t.Id).ToList();

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
