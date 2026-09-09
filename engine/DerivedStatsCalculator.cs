using ProwlersAndParagonsAutomation.Engine.Models;

namespace ProwlersAndParagonsAutomation.Engine;

/// <summary>
/// Computes derived statistics (Edge, Health) and power baseline ranks
/// from a CharacterSheet and the rules data.
/// </summary>
public sealed class DerivedStatsCalculator
{
    private readonly RulesRepository _rules;

    public DerivedStatsCalculator(RulesRepository rules) => _rules = rules;

    // ── Edge ─────────────────────────────────────────────────────────────

    /// <summary>
    /// Edge = Perception + max(Agility, Intellect), then the three Powers the rulebook
    /// says can affect it (Ch.2 p.60):
    /// <list type="bullet">
    ///   <item>Danger Sense <b>replaces</b> Perception in the sum — "Use this Power instead of
    ///   Perception when making rolls to detect danger and when determining your
    ///   Edge" — it is not added on top.</item>
    ///   <item>Lightning Reflexes adds a flat +6. It has no rank, so nothing scales.</item>
    ///   <item>Super Speed sets Edge to its rank × 3, taken if that beats the total.</item>
    /// </list>
    /// </summary>
    public int CalculateEdge(CharacterSheet sheet)
    {
        var perception = sheet.GetAbilityRank("perception");
        var agility    = sheet.GetAbilityRank("agility");
        var intellect  = sheet.GetAbilityRank("intellect");

        // Danger Sense stands in for Perception when the character has it.
        var dangerSense = sheet.GetPower("danger_sense");
        var perceptual  = dangerSense is not null
            ? GetEffectiveRank(dangerSense, sheet)
            : perception;

        var edge = perceptual + Math.Max(agility, intellect);

        // Lightning Reflexes: flat +6, verified against Ch.2 ("Increase your Edge by 6").
        if (sheet.HasPower("lightning_reflexes"))
            edge += LightningReflexesEdgeBonus;

        // Super Speed: "your Edge equals your Super Speed rank times 3". Treated as a
        // floor rather than an override so it never lowers an already-higher Edge.
        var superSpeed = sheet.GetPower("super_speed");
        if (superSpeed is not null)
            edge = Math.Max(edge, GetEffectiveRank(superSpeed, sheet) * 3);

        return edge;
    }

    /// <summary>Flat Edge bonus granted by Lightning Reflexes (Ch.2).</summary>
    public const int LightningReflexesEdgeBonus = 6;

    /// <summary>Hero Points that buy 1 extra starting Resolve via Determination (Ch.2).</summary>
    public const int DeterminationHpPerResolve = 5;

    // ── Health ───────────────────────────────────────────────────────────

    /// <summary>
    /// Health = max(⌈(Toughness + Might) / 2⌉, ⌈(Toughness + Willpower) / 2⌉)
    /// </summary>
    public int CalculateHealth(CharacterSheet sheet)
    {
        var toughness  = sheet.GetAbilityRank("toughness");
        var might      = sheet.GetAbilityRank("might");
        var willpower  = sheet.GetAbilityRank("willpower");

        var mightHealth    = (int)Math.Ceiling((toughness + might)    / 2.0);
        var willpowerHealth = (int)Math.Ceiling((toughness + willpower) / 2.0);

        return Math.Max(mightHealth, willpowerHealth);
    }

    // ── The Trait Cap ────────────────────────────────────────────────────────

    /// <summary>
    /// <b>The one answer to "what ceiling is this character built to".</b> The house cap on the
    /// character if it has one, otherwise the tier's, otherwise nothing — a character with
    /// neither has no cap to measure anything against, and every caller here already had a
    /// branch for that.
    ///
    /// <para><b>It substitutes rather than gates, and that is the whole of the design
    /// question.</b> <see cref="CalculateResolve"/> reads the cap as the datum Resolve is
    /// measured from, so a house cap that only gated validation would pay a character the
    /// tier's Resolve for a restraint the campaign imposed on them —
    /// <c>(12−4)×2 = 16</c> on a 4d character at Standard under a 6d house rule, rather than
    /// the <c>(6−4)×2 = 4</c> the room they actually have is worth. The owner settled it on
    /// 2026-09-05: the rules tie the two together and the trade is the player's to make.</para>
    ///
    /// <para><b>Static, and given the tier rather than resolving one.</b> Two of its six
    /// callers hold a tier and no rules repository, and the browser's session holds one it has
    /// already looked up. It reads two fields of the character and nothing else — a house cap
    /// reaches the engine by having been written onto the sheet, never by an id this layer
    /// would have to resolve, which would mean asking storage.</para>
    ///
    /// <para><b>It answers with the number as written, including a nonsensical one.</b> A house
    /// cap above the tier's, or below 1d, is an error <c>CharacterValidator</c> reports and this
    /// still returns: the engine is a judge and does not repair somebody's character, and a cap
    /// silently clamped here would make the finding beside it read as a lie.</para>
    /// </summary>
    /// <param name="sheet">The character, whose <see cref="CharacterSheet.TraitCapRank"/> wins.</param>
    /// <param name="tier">The character's tier, already resolved, or null where it has none.</param>
    public static int? EffectiveTraitCap(CharacterSheet sheet, Models.TierModel? tier)
    {
        ArgumentNullException.ThrowIfNull(sheet);

        return sheet.TraitCapRank ?? tier?.TraitCapRank;
    }

    // ── Resolve ──────────────────────────────────────────────────────────────

    /// <summary>
    /// Starting Resolve at the beginning of an issue.
    ///
    /// Formula (Chapter 5): the Resolve table runs Trait Cap → 0, Cap-1d → 2, Cap-2d → 4,
    /// which is the arithmetic below.
    ///   base = max(0, (TraitCap - highestRelevantRank) × 2)
    ///   + 1 per <see cref="DeterminationHpPerResolve"/> HP spent on Determination
    ///   + 1 per Condition or Plot Hook flaw
    ///
    /// Relevant ranks: all Ability ranks; Power effective ranks where the power
    /// affects Resolve. Talents excluded. Movement and Sensory category powers
    /// excluded by default; explicit overrides via PowerModel.AffectsResolve, and the
    /// nomination-dependent carve-out via <see cref="ResolveAffectedBySelection"/>.
    ///
    /// <para><b>The cap is <see cref="EffectiveTraitCap"/>, so a house cap moves this figure.</b>
    /// That is the one read this method makes of the cap and it is deliberately the only one —
    /// see that method for why substituting is the honest answer and gating is not.</para>
    /// </summary>
    public int CalculateResolve(CharacterSheet sheet)
    {
        if (sheet.SelectedTierId is null) return 0;
        var tier = _rules.GetTier(sheet.SelectedTierId);
        if (tier is null) return 0;

        var traitCap = EffectiveTraitCap(sheet, tier) ?? tier.TraitCapRank;

        var highestAbility = sheet.AbilityRanks.Values.DefaultIfEmpty(0).Max();

        var highestPower = sheet.SelectedPowers
            .Select(sp => ResolveAffectedBySelection(sp) ? GetEffectiveRank(sp, sheet) : 0)
            .DefaultIfEmpty(0)
            .Max();

        var highestRelevant = Math.Max(highestAbility, highestPower);
        var baseResolve     = Math.Max(0, (traitCap - highestRelevant) * 2);

        // Determination has no rank: 5 HP buys 1 extra starting Resolve, and Units
        // holds how many Resolve were bought.
        var determination      = sheet.GetPower("determination");
        var determinationBonus = determination?.Units ?? 0;

        // Condition / Plot Hook flaws: +1 each at start of every issue
        var flawBonus = sheet.Flaws.Count(sf =>
        {
            var flaw = _rules.GetFlaw(sf.FlawId);
            return flaw?.FlawType is "condition" or "plot_hook" or "plot_hook_and_condition";
        });

        return baseResolve + determinationBonus + flawBonus;
    }

    /// <summary>
    /// Returns whether a power's effective rank contributes to the Resolve calculation.
    /// Movement and Sensory powers are excluded by default (cannot be used for
    /// attack, defense, or to affect other characters/objects — Chapter 5).
    /// An explicit AffectsResolve value on the model overrides the category default.
    /// </summary>
    public static bool ResolveAffectedByPower(PowerModel power)
    {
        ArgumentNullException.ThrowIfNull(power);

        if (power.AffectsResolve.HasValue) return power.AffectsResolve.Value;

        return power.Category switch
        {
            "Movement" => false,
            "Sensory"  => false,
            _          => true
        };
    }

    /// <summary>
    /// Whether <em>this purchase</em> of a Power contributes its effective rank to Resolve.
    ///
    /// <para><b>This is <see cref="ResolveAffectedByPower"/> plus the one thing a
    /// <see cref="PowerModel"/> on its own cannot answer.</b> Ch.5 p.83 exempts "Expertise
    /// (except for combat skills)", which is a carve-out and not an exemption: whether a given
    /// Expertise counts depends on the Trait the player nominated, so the question has to be
    /// asked of the selection rather than of the entry. Every other Power answers identically
    /// either way, because <see cref="PowerModel.AffectsResolveWhenNominated"/> is empty on all
    /// of them.</para>
    ///
    /// <para><b>The nomination is looked up in the entry's list and nowhere else.</b> Ch.2 p.28
    /// says "Your specialization must fall under one of your Abilities or Talents", so those are
    /// the only two kinds of nomination an Expertise can legally carry, and
    /// <see cref="PowerModel.AffectsResolveWhenNominated"/> names the ones that count: Might,
    /// Agility, Toughness and Willpower, the four Abilities Ch.4 p.75's Attack and Defense table
    /// uses to attack or defend. A nomination to a <em>Power</em> is not a legal Expertise at all
    /// and gets no branch here — <c>CharacterValidator</c> reports it as
    /// <c>EXPERTISE_NOMINATION_NOT_A_TRAIT</c>, and an illegal character is reported, never
    /// repaired. An earlier version of this method asked the nominated Power p.83's own
    /// attack-or-defence question instead, which quietly gave an illegal sheet a defensible
    /// Resolve and hid the finding.</para>
    ///
    /// <para>An unknown Power id answers false rather than throwing, matching
    /// <see cref="CalculateResolve"/>'s existing tolerance — an id nobody can resolve is
    /// <c>CharacterValidator</c>'s finding to report, not a crash in a derived stat. A nomination
    /// this list does not name falls through to the entry's own answer for the same reason.</para>
    /// </summary>
    /// <param name="selected">The purchase, whose <see cref="SelectedPower.BaselineTraitId"/> is the nomination.</param>
    public bool ResolveAffectedBySelection(SelectedPower selected)
    {
        ArgumentNullException.ThrowIfNull(selected);

        var power = _rules.GetPower(selected.PowerId);
        if (power is null) return false;

        if (selected.BaselineTraitId is { Length: > 0 } traitId
            && power.AffectsResolveWhenNominated.Contains(traitId, StringComparer.Ordinal))
            return true;

        return ResolveAffectedByPower(power);
    }

    // ── Teamwork, from a base's Training Facilities ───────────────────────────

    /// <summary>
    /// Points of Teamwork this character opens each issue with: one for every headquarters they
    /// share that has Training Facilities (Ch.6 p.103).
    ///
    /// <para><b>It behaves exactly like Resolve, and the data says so in that word</b> — the
    /// entry's own <c>behaves_like</c> is <c>resolve</c> — except that it may only be spent
    /// assisting an ally. So it is handled exactly as Resolve is: <b>computed for anybody, quoted
    /// for a Hero.</b> Only Heroes hold Resolve, the GM gets Adversity instead, and this engine
    /// has no way of telling which kind of character it has been handed — nor should it, since
    /// that flag is presentation and a rule branching on it would be the browser deciding a rule.
    /// A host that knows it is showing a Villain does not print this figure, the same silence it
    /// keeps about Resolve.</para>
    ///
    /// <para><b>Only bases this character owns are counted, and that is a real gap rather than a
    /// simplification.</b> The grant is to "every character who shares the headquarters", and a
    /// shared base belongs to a campaign — a <c>CampaignAssetContribution</c> records the Hero
    /// Points this character put in and nothing about what the base turned out to have. So a team
    /// member who paid into a base with Training Facilities gets nothing here, correctly: the
    /// sheet cannot know, and inventing the point would be this engine answering a question only
    /// the campaign can. The campaign slice is where that is answered.</para>
    ///
    /// <para><b>Two bases with the feature grant two points</b>, which the page neither states nor
    /// forbids, and which follows from the grant being per headquarters. Nothing says a character
    /// may not own two, and the arithmetic is the one the entry prints.</para>
    /// </summary>
    public int CalculateTeamwork(CharacterSheet sheet)
    {
        ArgumentNullException.ThrowIfNull(sheet);

        var teamwork = _rules.Assets.Teamwork;

        return sheet.Headquarters.Count(hq => hq.Features.Any(
                   f => string.Equals(f.FeatureId, teamwork.GrantedByFeature, StringComparison.Ordinal)))
             * teamwork.PointsPerIssue;
    }

    // ── Baseline rank ─────────────────────────────────────────────────────

    /// <summary>
    /// Returns the free baseline rank contributed by a character's ability scores
    /// for the given power's prerequisite relationship.
    ///
    /// baseline_equal:          baseline = ability rank
    /// baseline_half:           baseline = ⌈ability rank / 2⌉
    /// baseline_fixed:          baseline = FixedValue (3 for Running)
    /// baseline_greater_of:     baseline = max(ability rank, effective ranks of the named Powers)
    /// baseline_selected_trait: baseline = rank of the Trait nominated on the selection
    /// null prerequisite:       baseline = 0
    /// </summary>
    public int GetBaselineRank(PowerModel power, CharacterSheet sheet, SelectedPower? selection = null)
    {
        var prereq = power.Prerequisite;
        if (prereq is null) return 0;

        return prereq.Relationship switch
        {
            "baseline_equal" => sheet.GetAbilityRank(prereq.Ability!),
            "baseline_half"  => (int)Math.Ceiling(sheet.GetAbilityRank(prereq.Ability!) / 2.0),
            "baseline_fixed" => prereq.FixedValue ?? 0,

            // Strike: the greater of Might and Martial Arts.
            "baseline_greater_of" => prereq.Powers
                .Select(id => sheet.GetPower(id) is { } sp ? GetEffectiveRank(sp, sheet) : 0)
                .Append(prereq.Ability is not null ? sheet.GetAbilityRank(prereq.Ability) : 0)
                .Max(),

            // Boost and Expertise: the player nominates the Trait at purchase. Until
            // they do, the baseline is 0 and the validator reports the gap.
            "baseline_selected_trait" => selection?.BaselineTraitId is { } traitId
                ? GetTraitRank(traitId, sheet)
                : 0,

            _ => throw new InvalidOperationException(
                     $"Unknown prerequisite relationship '{prereq.Relationship}' on power '{power.Id}'.")
        };
    }

    /// <summary>
    /// The Trait ids a Power's baseline rank is read from, in the order the rulebook
    /// names them — Armor answers <c>toughness</c>, Strike answers <c>might</c> and
    /// <c>martial_arts</c>, Boost answers whatever was nominated on the selection.
    ///
    /// <para><b>It answers "from where", never "how much"</b>, which is
    /// <see cref="GetBaselineRank"/>'s question. A reader asking whether a Power justifies
    /// an effective rank needs both: the rank on its own says nothing about whether it was
    /// bought or derived, and a derived one is only as good as the Trait underneath it.</para>
    ///
    /// <para>Empty where there is nothing to name: a Power with no prerequisite, a fixed
    /// baseline printed in the entry (Running's 3d), or a nominated Trait nobody has
    /// nominated yet — which the validator reports separately.</para>
    /// </summary>
    public static IReadOnlyList<string> BaselineTraitIds(PowerModel power, SelectedPower? selection = null)
    {
        ArgumentNullException.ThrowIfNull(power);

        var prereq = power.Prerequisite;
        if (prereq is null) return [];

        return prereq.Relationship switch
        {
            "baseline_equal" or "baseline_half" =>
                prereq.Ability is null ? [] : [prereq.Ability],

            "baseline_fixed" => [],

            "baseline_greater_of" =>
                [.. (prereq.Ability is null ? Array.Empty<string>() : [prereq.Ability])
                    .Concat(prereq.Powers)],

            "baseline_selected_trait" =>
                selection?.BaselineTraitId is { } traitId ? [traitId] : [],

            _ => throw new InvalidOperationException(
                     $"Unknown prerequisite relationship '{prereq.Relationship}' on power '{power.Id}'.")
        };
    }

    /// <summary>
    /// Rank of any Trait by id — ability, talent or power — for the Powers whose
    /// baseline is whatever Trait the player nominated.
    /// </summary>
    private int GetTraitRank(string traitId, CharacterSheet sheet)
    {
        if (sheet.AbilityRanks.TryGetValue(traitId, out var ability)) return ability;
        if (sheet.TalentRanks.TryGetValue(traitId, out var talent))   return talent;

        if (sheet.GetPower(traitId) is not { } sp) return 0;

        // A nominated Power whose own baseline is player-nominated could point back
        // here and recurse. Only these Powers create that indirection, so refusing to
        // follow one is enough to make the resolution terminate.
        var nominated = _rules.GetPower(traitId);
        if (nominated?.Prerequisite?.Relationship == "baseline_selected_trait") return 0;

        return GetEffectiveRank(sp, sheet);
    }

    /// <summary>
    /// Total effective rank of a selected power: baseline rank + purchased ranks.
    /// Powers the rulebook gives no rank (rank_type "default" or "special") have no
    /// effective rank and return 0.
    /// </summary>
    public int GetEffectiveRank(SelectedPower selected, CharacterSheet sheet)
    {
        var power = _rules.GetPower(selected.PowerId)
                    ?? throw new InvalidOperationException($"Unknown power id '{selected.PowerId}'.");

        if (power.RankType is "default" or "special") return 0;

        return GetBaselineRank(power, sheet, selected) + selected.PurchasedRanks;
    }

    /// <summary>
    /// The rank a Power uses when another Power acts on it — Drain, Nullify, Dispel, Power
    /// Absorption, Power Mimicry.
    ///
    /// <para>For a ranked Power that is simply its effective rank. For one the rulebook
    /// gives no rank, Ch.2 p.16 substitutes a <em>default rank</em> taken from an Ability
    /// chosen by the Power's Source: Toughness for Innate, Super and Tech; Willpower for
    /// Magic, Psychic and Trained.</para>
    ///
    /// <para>Deliberately separate from <see cref="GetEffectiveRank"/>, which still answers
    /// 0 for a rankless Power. The default rank stands in only against other Powers; it is
    /// not the Power's rank, and folding it into the effective rank would feed Edge and
    /// Resolve figures the published Hero sheets contradict.</para>
    ///
    /// <para>Returns 0 for a rankless Power with no Source recorded, since there is then
    /// no Ability to read. The validator reports that gap.</para>
    /// </summary>
    public int GetRankAgainstPowers(SelectedPower selected, CharacterSheet sheet)
    {
        ArgumentNullException.ThrowIfNull(selected);
        ArgumentNullException.ThrowIfNull(sheet);

        var power = _rules.GetPower(selected.PowerId)
                    ?? throw new InvalidOperationException($"Unknown power id '{selected.PowerId}'.");

        if (power.RankType is not ("default" or "special"))
            return GetEffectiveRank(selected, sheet);

        if (selected.SourceId is null) return 0;

        var source = _rules.GetSource(selected.SourceId)
                     ?? throw new InvalidOperationException(
                            $"Power '{selected.PowerId}' names unknown Source '{selected.SourceId}'.");

        return sheet.GetAbilityRank(source.DefaultRankAbility);
    }

    // ── Armour worn as mundane gear ──────────────────────────────────────────

    /// <summary>
    /// The Gear Limit in force for this character when nothing has raised it: <b>6d</b>, Ch.6
    /// p.87.
    ///
    /// <para><b>It is a figure and not a switch, and it lives here rather than in a rules file
    /// this engine can read, which is worth saying plainly.</b> The Gear Limit is extracted, and
    /// it is extracted in <c>data/rules/play/equipment.json</c> — the play store, which
    /// <c>engine/</c> may not read and <c>play/</c> cannot do without. The alternative was a
    /// second transcription of one number in <c>gear.json</c>, which is the duplication the
    /// weapon-table copy rule already carries for sixty-three rows and would be a poor trade for
    /// one. So the figure is here and
    /// <c>ArmourRankTests.TheDefaultGearLimitIsThePlayStoresAndTheBooksSameFigure</c> holds it
    /// equal to the play store's, which is itself held to p.87 — the same shape as the copy rule,
    /// a number instead of a table.</para>
    /// </summary>
    public const int DefaultGearLimitRank = 6;

    /// <summary>
    /// The highest effective Trait rank mundane equipment will carry for this character: the
    /// table's raised limit if it has adopted one, or <see cref="DefaultGearLimitRank"/>.
    ///
    /// <para><b>Read the limit through here and nowhere else</b>, for the reason
    /// <see cref="EffectiveTraitCap"/> is read through one method: the raised limit is a pair —
    /// a switch and a rank — and a second spelling of
    /// <c>table.RaisedGearLimit ? table.GearLimitRank : 6</c> is how one surface ends up
    /// disagreeing with the figure printed beside it. A rank left on the table while the switch
    /// is off is a figure the table has not adopted; <c>play/</c> reads it that way and so does
    /// this.</para>
    /// </summary>
    public static int EffectiveGearLimit(CharacterSheet sheet)
    {
        ArgumentNullException.ThrowIfNull(sheet);

        return sheet.CampaignTable is { RaisedGearLimit: true, GearLimitRank: { } rank }
            ? rank
            : DefaultGearLimitRank;
    }

    /// <summary>
    /// The Armor rank a worn suit grants this character, or null if they are wearing none.
    ///
    /// <para><b>p.88 states the rank and p.87 caps it, so the answer is neither page alone.</b>
    /// p.88: wearing a suit hands you the Armor Power at your Toughness plus the suit's Armor
    /// Bonus, measured from your own Armor Power instead where you already have one. p.87: the
    /// Gear Limit is the maximum Trait rank you can apply when using mundane equipment that boosts
    /// your Traits "(usually armor and weapons)", and its worked example fixes the order of the
    /// arithmetic at limit-plus-bonus rather than trait-plus-bonus-then-capped. So the rank is
    /// <c>min(base, gear limit) + bonus</c>, and a 10d-Toughness Hero in Plate is 8d in a standard
    /// game, not 12d. p.87 says outright that this is what makes mundane armour less useful to a
    /// superhuman, so the surprising half is the printed intent. See
    /// <c>docs/guide/rules-engine.md</c>, which carries the whole argument.</para>
    ///
    /// <para><b>It is a figure this reports, not a Power it buys.</b> Nothing here touches
    /// <see cref="CharacterSheet.SelectedPowers"/>: mundane gear is free and untracked, an Armor
    /// Power on the sheet would cost Hero Points, and the engine does not make design decisions
    /// about somebody's character. A host prints the rank beside the suit.</para>
    ///
    /// <para><b>Only the best suit answers</b>, because a character wearing two is wearing one and
    /// carrying another, and adding them would pay twice for a thing the page grants once.</para>
    /// </summary>
    public int? ArmorFromGear(CharacterSheet sheet)
    {
        ArgumentNullException.ThrowIfNull(sheet);

        var best = sheet.Gear
            .Select(g => g.CatalogueId is null ? null : _rules.Catalogue.Find(g.CatalogueId))
            .Where(r => r is { Kind: GearCatalogueKind.Armor })
            .Select(r => r!.BonusDice ?? 0)
            .DefaultIfEmpty(-1)
            .Max();

        return best < 0 ? null : ArmorRankInSuit(sheet, best);
    }

    /// <summary>
    /// The rank a suit worth <paramref name="armorBonusDice"/> grants this character.
    ///
    /// <para>Public so a host can show what a row would be worth <em>before</em> it is chosen,
    /// which is the whole use of the figure on a picker. The base is the wearer's Toughness or
    /// their own Armor Power's effective rank, whichever is higher — p.88's second sentence, so
    /// "an armoured hero in a borrowed shell is not reduced to an ordinary person's baseline" —
    /// and a Power's rank substituted in that way is a Trait rank like any other and is capped the
    /// same.</para>
    /// </summary>
    public int ArmorRankInSuit(CharacterSheet sheet, int armorBonusDice)
    {
        ArgumentNullException.ThrowIfNull(sheet);

        var toughness = GetTraitRank("toughness", sheet);

        var ownArmor = sheet.GetPower("armor") is { } armor ? GetEffectiveRank(armor, sheet) : 0;

        return Math.Min(Math.Max(toughness, ownArmor), EffectiveGearLimit(sheet)) + armorBonusDice;
    }
}
