using ProwlersAndParagonsAutomation.Engine.Models;

namespace ProwlersAndParagonsAutomation.Engine;

/// <summary>
/// Calculates Hero Point costs for all character traits.
/// All methods are pure — they read from sheet and rules but never mutate.
/// </summary>
public sealed class CostCalculator
{
    private readonly RulesRepository _rules;

    public CostCalculator(RulesRepository rules) => _rules = rules;

    // ── Abilities ────────────────────────────────────────────────────────

    /// <summary>
    /// HP spent on abilities, 1 per rank. A package already covers every ability up to
    /// its own rank, so only the ranks bought above that are charged again here.
    ///
    /// <para>Abilities can carry Pros and Cons of their own. Overkill on Might is the
    /// rulebook's Brute Option, which halves it; other modifiers are flat. An Ability
    /// never costs less than nothing, however many Cons are piled on it.</para>
    /// </summary>
    public int AbilityCost(CharacterSheet sheet)
    {
        var covered = SelectedPackage(sheet)?.AbilitiesRank ?? 0;

        // Ranks against an Ability the rulebook does not have are not charged for. They used to
        // be: the total included them, so a character with a misspelled Ability was quoted a
        // price that covered a Trait it could not possibly have — a figure printed as fact
        // beside the error saying the Trait does not exist. UNKNOWN_ABILITY reports it; the
        // cost should not also invent it.
        return sheet.AbilityRanks.Where(e => _rules.GetAbility(e.Key) is not null).Sum(entry =>
        {
            var chargeable = Math.Max(0, entry.Value - covered);
            if (chargeable == 0) return 0;

            var modifiers = sheet.AbilityModifiers.GetValueOrDefault(entry.Key) ?? [];

            // The Brute Option: Overkill on **Might** buys it at 1 HP per 2 ranks. The
            // Ability is part of the rule, not decoration — the id was not checked, so
            // Overkill or Weak on any of the six halved it, and 12d Intellect for 6 HP was a
            // legal character with one Con on it. Ch.2 p.17 names Might and nothing else.
            var cost = entry.Key == "might" && modifiers.Any(m => m.Id is "overkill" or "weak")
                ? (int)Math.Ceiling(chargeable / 2.0)
                : chargeable;

            var flat = modifiers
                .Where(m => m.Id is not ("overkill" or "weak"))
                .Sum(m => _rules.GetCon(m.Id) is not null ? ResolveConCost(m) : ResolveProCost(m));

            return Math.Max(0, cost + flat);
        });
    }

    // ── Talents ──────────────────────────────────────────────────────────

    /// <summary>
    /// HP spent on talents, 1 per rank, above whatever a package already covers.
    /// </summary>
    public int TalentCost(CharacterSheet sheet)
    {
        var covered = SelectedPackage(sheet)?.TalentsRank ?? 0;

        // Unknown ids are not charged for — see AbilityCost for why.
        return sheet.TalentRanks
            .Where(entry => _rules.GetTalent(entry.Key) is not null)
            .Sum(entry => Math.Max(0, entry.Value - covered));
    }

    // ── Package ──────────────────────────────────────────────────────────

    /// <summary>
    /// Flat HP cost of the selected optional package (Civilian/Hero/Superhero), or 0 if none.
    ///
    /// <para>A package buys the ranks it grants — that is the whole point of it, since the
    /// rulebook sells them "at a small discount". The Superhero Package costs 50 for 3d in
    /// six Abilities and twelve Talents, which is 54 HP bought separately. Charging the
    /// package price on top of every rank would double-pay for the ranks it grants and
    /// make taking one strictly worse than not.</para>
    /// </summary>
    public int PackageCost(CharacterSheet sheet) => SelectedPackage(sheet)?.Cost ?? 0;

    private OptionalPackage? SelectedPackage(CharacterSheet sheet) =>
        sheet.SelectedPackageId is null
            ? null
            : _rules.CreationRules.OptionalPackages
                    .FirstOrDefault(p => p.Id == sheet.SelectedPackageId);

    // ── Powers ───────────────────────────────────────────────────────────

    /// <summary>
    /// HP cost for a single selected power including its pros and cons.
    ///
    /// Ranked Powers (per_rank and its variable form):
    ///   1. Start from the Power's HP per rank. Overkill and Weak each reduce that
    ///      rate by 1 HP per rank, floored at 0.5 — the rulebook's "1 Hero Point per
    ///      rank becomes 1 Hero Point per 2 ranks".
    ///   2. base = ⌈purchasedRanks × rate⌉
    ///   3. Add pro costs and con discounts (cons are stored negative).
    ///   4. Floor the result at the rulebook minimum: no Power costs less than
    ///      1 HP per rank, or 1 HP per 2 ranks once a rate-reducing Con applies.
    ///
    /// Unranked Powers (flat, flat_variable, per_unit) ignore purchased ranks and are
    /// floored at 1 HP, except Specialty, which the rulebook makes free.
    /// </summary>
    public int PowerCost(SelectedPower selection) => CostParts.For(this, selection).Total();

    /// <summary>
    /// A Power's cost taken apart: the price its ranks or flat rate come to, the flat
    /// change its Pros and Cons make, and the rulebook floor beneath the two combined.
    ///
    /// <para>They are kept separate only because Super Senses needs them that way — it is
    /// a single Power bought as several entries here, so its options are summed before the
    /// Cons and the floor are applied once. See <see cref="SuperSensesCost"/>.</para>
    /// </summary>
    private readonly record struct CostParts(int Base, int Flat, int Minimum)
    {
        public int Total() => Math.Max(Minimum, Base + Flat);

        public static CostParts For(CostCalculator calc, SelectedPower selection)
        {
            var power = calc._rules.GetPower(selection.PowerId)
                        ?? throw new InvalidOperationException($"Unknown power id '{selection.PowerId}'.");

            var (flat, rate) = calc.ResolveModifiers(power, selection);

            // Specialty is the one Power the rulebook prices at 0 HP, so it has no floor.
            CostParts Fixed(int cost) => power.Id == "specialty" ? new(0, 0, 0) : new(cost, flat, 1);

            return power.CostType switch
            {
                "per_rank" or "per_rank_variable" or "special" =>
                    calc.RankedParts(power, selection, flat, rate),

                "flat" =>
                    Fixed(power.CostFlat ?? throw MissingCost(power, "cost_flat")),

                "flat_variable" =>
                    Fixed((int)calc.ResolveVariant(power, selection)),

                "per_unit" =>
                    Fixed((power.CostPerUnit ?? throw MissingCost(power, "cost_per_unit")) * selection.Units),

                _ => throw new InvalidOperationException(
                         $"Unknown cost_type '{power.CostType}' on power '{power.Id}'.")
            };
        }
    }

    /// <summary>
    /// Totals a selection's Pros and Cons, split by how they apply. Generic ones from
    /// pros.json / cons.json are always flat; the ones printed inside a Power's own entry
    /// can instead change its Hero Points per rank.
    /// </summary>
    private (int Flat, double Rate) ResolveModifiers(PowerModel power, SelectedPower selection)
    {
        var flat = 0;
        var rate = 0.0;

        foreach (var (choice, isPro) in selection.Pros.Select(p => (p, true))
                                       .Concat(selection.Cons.Select(c => (c, false))))
        {
            // A Pro or Con printed in this Power's entry takes precedence over a generic
            // one of the same name, since it is the one the entry is talking about.
            var specific = (isPro ? power.PowerPros : power.PowerCons)
                .FirstOrDefault(x => x.Id == choice.Id);

            if (specific is not null)
            {
                var (f, r) = ResolvePowerProCon(power, specific, choice, selection);
                flat += f;
                rate += r;
                continue;
            }

            flat += isPro ? ResolveProCost(choice) : ResolveConCost(choice);
        }

        return (flat, rate);
    }

    private static (int Flat, double Rate) ResolvePowerProCon(
        PowerModel power, PowerProConModel entry, SelectedProCon choice, SelectedPower selection)
    {
        // Units defaults to the Power's own quantity, which is what Alternate Form's
        // Independent Forms means by "per power level".
        var units = choice.Units ?? selection.Units;

        switch (entry.CostType)
        {
            case "flat":
                return (entry.CostModifier ?? throw MissingProConCost(power, entry, "cost_modifier"), 0);

            case "per_rank":
                return (0, entry.CostPerRank ?? throw MissingProConCost(power, entry, "cost_per_rank"));

            case "per_unit":
                return ((entry.CostPerUnit ?? throw MissingProConCost(power, entry, "cost_per_unit")) * units, 0);

            case "per_rank_per_unit":
                return (0, (entry.CostPerRank ?? throw MissingProConCost(power, entry, "cost_per_rank")) * units);

            case "flat_variable":
            {
                var range = entry.CostModifierRange ?? throw MissingProConCost(power, entry, "cost_modifier_range");
                return (PickVariant(power, entry, choice, range), 0);
            }

            case "per_rank_variable":
            {
                var range = entry.CostPerRankRange ?? throw MissingProConCost(power, entry, "cost_per_rank_range");
                return (0, PickVariant(power, entry, choice, range));
            }

            default:
                throw new InvalidOperationException(
                    $"Unknown cost_type '{entry.CostType}' on '{entry.Id}' of power '{power.Id}'.");
        }
    }

    private static T PickVariant<T>(
        PowerModel power, PowerProConModel entry, SelectedProCon choice, IReadOnlyDictionary<string, T> range)
    {
        var key = choice.VariantKey
            ?? throw new InvalidOperationException(
                   $"'{entry.Name}' on power '{power.Id}' has a variable cost and needs a variant. " +
                   $"Valid keys: {string.Join(", ", range.Keys)}");

        if (range.TryGetValue(key, out var value)) return value;

        throw new InvalidOperationException(
            $"'{entry.Name}' on power '{power.Id}' has no variant '{key}'. " +
            $"Valid keys: {string.Join(", ", range.Keys)}");
    }

    private static InvalidOperationException MissingProConCost(
        PowerModel power, PowerProConModel entry, string field) =>
        new($"'{entry.Id}' on power '{power.Id}' has cost_type '{entry.CostType}' but no {field}.");

    /// <summary>
    /// Cost of a Power priced per rank. <paramref name="flatModifiers"/> is the summed
    /// flat pro/con total (cons negative); <paramref name="rateModifiers"/> is the change
    /// those Pros and Cons make to the Hero Points per rank.
    /// </summary>
    private CostParts RankedParts(PowerModel power, SelectedPower selection, int flatModifiers, double rateModifiers)
    {
        var rate = PerRankRate(power, selection) + rateModifiers;

        // Overkill and Weak reduce the rate by 1 HP per rank each rather than halving
        // it (Ch.2: "reduces a Power's base cost by 1 Hero Point per rank"). For a
        // 1 HP/rank Power that lands on 1 HP per 2 ranks, which is the rulebook floor.
        var reductions = selection.Cons.Count(c => c.Id is "overkill" or "weak");
        var effectiveRate = Math.Max(0.5, rate - reductions);

        return new((int)Math.Ceiling(selection.PurchasedRanks * effectiveRate),
                   flatModifiers,
                   MinimumRankedCost(selection.PurchasedRanks));
    }

    /// <summary>
    /// The rulebook floor for a ranked Power: "No Power can ever cost less than 1 Hero
    /// Point (or 1 Hero Point per 2 ranks) regardless of its Cons." The parenthesis is
    /// the ranked form of the same rule, so the floor is 1 Hero Point per 2 ranks
    /// whatever the Power's own rate — not 1 per rank. Reading it as 1 per rank made
    /// every Con worthless on a Power priced at 1 Hero Point per rank.
    /// </summary>
    private static int MinimumRankedCost(int purchasedRanks)
    {
        if (purchasedRanks <= 0) return 0;
        return Math.Max(1, (int)Math.Ceiling(purchasedRanks / 2.0));
    }

    /// <summary>
    /// HP per rank before Cons. Resolves the variable cost types and the two Powers
    /// whose rate the rulebook defines as Special.
    /// </summary>
    private double PerRankRate(PowerModel power, SelectedPower selection)
    {
        if (power.CostType == "per_rank_variable")
            return ResolveVariant(power, selection);

        if (power.CostType == "special")
        {
            return power.Id switch
            {
                // Boost costs as many HP per rank as the Trait it raises.
                "boost" => BoostRate(selection),

                // Summoning costs 1 HP per rank for every 2d of Minion Threat,
                // rounded up. Units carries the chosen Threat rank.
                "summoning" => Math.Max(1, Math.Ceiling(selection.Units / 2.0)),

                _ => throw new InvalidOperationException(
                         $"Power '{power.Id}' has cost_type 'special' with no handler. " +
                         "Add one to CostCalculator.PerRankRate.")
            };
        }

        return power.CostPerRank ?? throw MissingCost(power, "cost_per_rank");
    }

    /// <summary>
    /// Boost's per-rank cost mirrors the nominated Trait: abilities and talents cost
    /// 1 HP per rank, a Power costs whatever that Power costs per rank.
    /// </summary>
    private double BoostRate(SelectedPower selection)
    {
        var traitId = selection.BaselineTraitId
            ?? throw new InvalidOperationException(
                   "Boost requires BaselineTraitId — its cost per rank matches the Trait it raises.");

        if (_rules.GetAbility(traitId) is not null || _rules.GetTalent(traitId) is not null)
            return 1.0;

        var target = _rules.GetPower(traitId)
            ?? throw new InvalidOperationException(
                   $"Boost names Trait '{traitId}', which is not a known ability, talent or power.");

        return target.CostPerRank
            ?? throw new InvalidOperationException(
                   $"Boost cannot mirror power '{traitId}': it is not priced per rank.");
    }

    private double ResolveVariant(PowerModel power, SelectedPower selection)
    {
        var variants = power.CostVariants
            ?? throw MissingCost(power, "cost_variants");

        var key = selection.CostVariantKey
            ?? throw new InvalidOperationException(
                   $"Power '{power.Id}' has a variable cost and needs a CostVariantKey. " +
                   $"Valid keys: {string.Join(", ", variants.Keys)}");

        if (variants.TryGetValue(key, out var value)) return value;

        throw new InvalidOperationException(
            $"Power '{power.Id}' has no cost variant '{key}'. " +
            $"Valid keys: {string.Join(", ", variants.Keys)}");
    }

    private static InvalidOperationException MissingCost(PowerModel power, string field) =>
        new($"Power '{power.Id}' has cost_type '{power.CostType}' but no {field} value.");

    /// <summary>
    /// Every Super Senses option shares this id prefix. They are separate entries in
    /// powers.json because each has its own price, but the rulebook is explicit that
    /// they are not separate Powers — see <see cref="SuperSensesCost"/>.
    /// </summary>
    private const string SuperSensesPrefix = "super_senses_";

    private static bool IsSuperSense(SelectedPower selection) =>
        selection.PowerId.StartsWith(SuperSensesPrefix, StringComparison.Ordinal);

    /// <summary>Total HP spent on all selected powers.</summary>
    public int TotalPowersCost(CharacterSheet sheet) =>
        sheet.SelectedPowers.Where(p => !IsSuperSense(p)).Sum(PowerCost)
        + SuperSensesCost(sheet.SelectedPowers.Where(IsSuperSense));

    /// <summary>
    /// Super Senses costs as one Power, not as one per option.
    ///
    /// <para>Ch.2: "Regardless of the options you select, Super Senses is always considered
    /// a single Power with an effective rank equal to your Perception or your Acute X rank,
    /// whichever is greater." Each option is its own entry here only because each carries
    /// its own price; that is a storage decision, and the rulebook is explicit that it does
    /// not make them separate Powers.</para>
    ///
    /// <para>Two rules are stated per Power and so apply once to the whole group: a Con
    /// written against the group discounts the group, and the minimum-cost floor is one
    /// floor rather than one per option. The floor is what actually bites — most options
    /// cost 1 HP flat, so per option a Con would be swallowed by that option's own floor
    /// and be worth nothing at all.</para>
    ///
    /// <para>Super Senses is the only such group. Form leaves each version independent,
    /// and Transformation says "Regardless of which Transformation Power you possess",
    /// naming them Powers in the plural — so both stay priced separately.</para>
    /// </summary>
    private int SuperSensesCost(IEnumerable<SelectedPower> options)
    {
        var parts = options.Select(o => CostParts.For(this, o)).ToList();
        if (parts.Count == 0) return 0;

        return Math.Max(parts.Max(p => p.Minimum),
                        parts.Sum(p => p.Base) + parts.Sum(p => p.Flat));
    }

    // ── Perks ─────────────────────────────────────────────────────────────

    /// <summary>
    /// HP cost for a single selected perk.
    /// flat perks cost their fixed Cost value.
    /// per_unit perks cost CostPerUnit × Units.
    /// </summary>
    public int PerkCost(SelectedPerk selection)
    {
        var perk = _rules.GetPerk(selection.PerkId)
                   ?? throw new InvalidOperationException($"Unknown perk id '{selection.PerkId}'.");

        return perk.CostType switch
        {
            "flat"     => perk.Cost ?? 0,
            "per_unit" => (perk.CostPerUnit ?? 1) * selection.Units,
            _          => throw new InvalidOperationException(
                              $"Unknown cost_type '{perk.CostType}' on perk '{perk.Id}'.")
        };
    }

    /// <summary>Total HP spent on all selected perks.</summary>
    public int TotalPerksCost(CharacterSheet sheet) =>
        sheet.Perks.Sum(PerkCost);

    // ── Gear ──────────────────────────────────────────────────────────────

    /// <summary>
    /// HP cost of one piece of gear: its custom features, plus any Pros and Cons applied
    /// to it, which "cost the same when applied to gear as when applied to Powers".
    ///
    /// <para>The gear itself is free. Ch.6 is explicit that mundane gear is not tracked at
    /// all, so an item with nothing bought for it costs nothing, and this returns 0.</para>
    ///
    /// <para>Gear has its own floor and it is not a Power's: "Regardless of Cons, no piece
    /// of gear can cost less than 0 Hero Points (in other words, no piece of gear will end
    /// up granting you extra Hero Points)." So Cons discount an item down to free and stop,
    /// where a Power floors at 1.</para>
    ///
    /// <para>The Item Con is not charged or credited here. Ch.6 says "As physical objects,
    /// every piece of gear has the Item Con" — a statement of what gear is, not a discount
    /// to claim, and Item is absent from the list of Cons the same page says are commonly
    /// applied to gear. Crediting it would make every 1 HP feature free and leave the
    /// printed prices meaningless.</para>
    /// </summary>
    public int GearCost(SelectedGear gear)
    {
        var features = gear.Features.Sum(FeatureCost);
        var modifiers = gear.Pros.Sum(ResolveProCost) + gear.Cons.Sum(ResolveConCost);

        return Math.Max(0, features + modifiers);
    }

    private int FeatureCost(SelectedGearFeature selection)
    {
        var feature = _rules.GetGearFeature(selection.FeatureId)
            ?? throw new InvalidOperationException($"Unknown gear feature id '{selection.FeatureId}'.");

        if (feature.CostType == "flat")
            return feature.Cost
                ?? throw new InvalidOperationException(
                       $"Gear feature '{feature.Id}' has cost_type 'flat' but no cost.");

        var grades = feature.CostRange
            ?? throw new InvalidOperationException(
                   $"Gear feature '{feature.Id}' has cost_type '{feature.CostType}' but no cost_range.");

        var key = selection.GradeKey
            ?? throw new InvalidOperationException(
                   $"Gear feature '{feature.Name}' is graded and needs a grade. " +
                   $"Valid keys: {string.Join(", ", grades.Keys)}");

        if (grades.TryGetValue(key, out var cost)) return cost;

        throw new InvalidOperationException(
            $"Gear feature '{feature.Name}' has no grade '{key}'. " +
            $"Valid keys: {string.Join(", ", grades.Keys)}");
    }

    /// <summary>
    /// Total HP spent on gear. Nearly always 0 — only customised items cost anything.
    ///
    /// <para>Two-Fisted lets a matched pair of weapons be customised "for the price of
    /// one". A pair is recorded as a single item with
    /// <see cref="SelectedGear.PairedUnderTwoFisted"/> set, so it is already charged once
    /// and needs no arithmetic of its own.</para>
    /// </summary>
    public int TotalGearCost(CharacterSheet sheet) => sheet.Gear.Sum(GearCost);

    // ── Total ────────────────────────────────────────────────────────────

    /// <summary>
    /// Grand total HP spend: package + abilities + talents + powers + perks + gear.
    /// This is compared against the tier's HeroPoints budget by CharacterValidator.
    /// </summary>
    /// <remarks>
    /// <b>Checked, because unchecked it wrapped to a negative total and the budget check then
    /// passed.</b> Each component sums with <c>Enumerable.Sum</c>, which is checked and throws
    /// — but the six additions between them were not, so Determination at 400,000,000 units
    /// plus a Perk at 200,000,000 came to −2,094,967,284 HP, reported legal with no findings
    /// at all. A quantity that large is nonsense either way; the point is that the answer has
    /// to be either right or refused, and silently negative is neither.
    ///
    /// <para><see cref="OverflowException"/> is one of the exceptions the validator turns into
    /// <c>CHARACTER_NOT_PRICEABLE</c>, so a character this big is now reported rather than
    /// certified.</para>
    /// </remarks>
    public int TotalCost(CharacterSheet sheet)
    {
        checked
        {
            return PackageCost(sheet)
                 + AbilityCost(sheet)
                 + TalentCost(sheet)
                 + TotalPowersCost(sheet)
                 + TotalPerksCost(sheet)
                 + TotalGearCost(sheet);
        }
    }

    // ── Private ──────────────────────────────────────────────────────────

    private int ResolveProCost(SelectedProCon selectedPro)
    {
        var pro = _rules.GetPro(selectedPro.Id)
                  ?? throw new InvalidOperationException($"Unknown pro id '{selectedPro.Id}'.");

        if (pro.CostModifier.HasValue)
            return pro.CostModifier.Value;

        if (pro.CostModifierRange is not null && selectedPro.VariantKey is not null)
        {
            if (pro.CostModifierRange.TryGetValue(selectedPro.VariantKey, out var variantCost))
                return variantCost;

            throw new InvalidOperationException(
                $"Pro '{pro.Id}' has no variant key '{selectedPro.VariantKey}'. " +
                $"Valid keys: {string.Join(", ", pro.CostModifierRange.Keys)}");
        }

        throw new InvalidOperationException(
            $"Pro '{pro.Id}' has no resolvable cost. " +
            "Either CostModifier must be set or CostModifierRange + VariantKey must both be provided.");
    }

    private int ResolveConCost(SelectedProCon selectedCon)
    {
        // Overkill and Weak are handled at the per-rank level above; their flat value is 0
        if (selectedCon.Id is "overkill" or "weak") return 0;

        var con = _rules.GetCon(selectedCon.Id)
                  ?? throw new InvalidOperationException($"Unknown con id '{selectedCon.Id}'.");

        if (con.CostModifier.HasValue)
            return con.CostModifier.Value; // already negative in data

        if (con.CostModifierRange is not null && selectedCon.VariantKey is not null)
        {
            if (con.CostModifierRange.TryGetValue(selectedCon.VariantKey, out var variantCost))
                return variantCost;

            throw new InvalidOperationException(
                $"Con '{con.Id}' has no variant key '{selectedCon.VariantKey}'. " +
                $"Valid keys: {string.Join(", ", con.CostModifierRange.Keys)}");
        }

        throw new InvalidOperationException(
            $"Con '{con.Id}' has no resolvable cost. " +
            "Either CostModifier must be set or CostModifierRange + VariantKey must both be provided.");
    }
}
