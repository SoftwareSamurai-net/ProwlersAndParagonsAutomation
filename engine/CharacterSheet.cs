using System.Text.Json.Serialization;

namespace ProwlersAndParagonsAutomation.Engine;

/// <summary>
/// Records a perk selected for a character. Units is always 1 for flat-cost perks;
/// for per_unit perks it holds the quantity purchased (e.g. number of contact types).
/// </summary>
public record SelectedPerk(string PerkId, int Units = 1, string? NarrativeDetail = null);

/// <summary>
/// Records a flaw selected for a character, pairing the flaw id with any
/// narrative detail required by the flaw's NarrativeConstraint.
/// </summary>
public record SelectedFlaw(string FlawId, string? NarrativeDetail = null);

/// <summary>
/// Records which variant of a variable-cost pro/con the player has chosen.
/// For a fixed-cost pro/con, VariantKey is null.
/// For a variable-cost one (e.g. Charges, Area/Burst), VariantKey is the
/// key from CostModifierRange (e.g. "3_per_scene", "area").
/// </summary>
public record SelectedProCon(string Id, string? VariantKey = null)
{
    /// <summary>
    /// Quantity for a Power-specific Pro or Con that scales — how many extra Sources
    /// Also X covers, for instance. Null means "however many units the Power itself has",
    /// which is what Alternate Form's Independent Forms wants.
    /// </summary>
    public int? Units { get; init; }
}

/// <summary>
/// A power as it appears on the character sheet: the power id,
/// how many ranks were purchased (above the free baseline), and
/// which pros/cons have been applied.
/// </summary>
/// <remarks>
/// The primary constructor is marked for the serializer because this record has two, and a
/// deserializer given a choice makes none — it throws. That is the only annotation on the
/// type and it changes nothing else: it lets a host round-trip a whole sheet (the browser
/// keeps one in local storage between visits) without the engine growing a parallel set of
/// data-transfer types that would then have to be kept in step with it.
/// </remarks>
[method: JsonConstructor]
public record SelectedPower(
    string PowerId,
    int PurchasedRanks,
    IReadOnlyList<SelectedProCon> Pros,
    IReadOnlyList<SelectedProCon> Cons)
{
    public SelectedPower(string powerId, int purchasedRanks)
        : this(powerId, purchasedRanks, [], []) { }

    /// <summary>
    /// Which variant was chosen for a Power whose cost varies — a key from
    /// <see cref="Models.PowerModel.CostVariants"/> such as "broad" for Omni-Power
    /// or "extreme_range" for Stretching. Null for fixed-cost Powers.
    /// </summary>
    public string? CostVariantKey { get; init; }

    /// <summary>
    /// How many units were bought for a per_unit Power: immunities for Immunity,
    /// Resolve for Determination, power levels for Alternate Form. Always 1 otherwise.
    /// </summary>
    public int Units { get; init; } = 1;

    /// <summary>
    /// The Trait the player nominated for a baseline_selected_trait Power (Boost,
    /// Expertise). Holds an ability, talent or power id. For Boost this also sets
    /// the per-rank cost, which matches the affected Trait's own cost per rank.
    /// </summary>
    public string? BaselineTraitId { get; init; }

    /// <summary>
    /// Which of the six Sources this Power comes from (Ch.2 p.16). Costs nothing and never
    /// changes a Power's rank, but a published sheet groups Powers under Source headings,
    /// and a rankless Power takes its default rank from the Source's Ability. Null means
    /// the player has not said yet.
    /// </summary>
    public string? SourceId { get; init; }
}

/// <summary>
/// A custom feature bought for a piece of gear. GradeKey picks between the two grades of
/// the features the rulebook prices at 1 to 2 HP (Accurate/Very Accurate,
/// Powerful/Very Powerful); it is null for the ten flat ones.
/// </summary>
public record SelectedGearFeature(string FeatureId, string? GradeKey = null);

/// <summary>
/// A piece of gear on the character sheet.
///
/// <para>Ch.6 makes mundane gear free and explicitly untracked, so a plain item is just a
/// name and costs nothing. Only custom features and Pros and Cons cost Hero Points.</para>
/// </summary>
public record SelectedGear(string Name)
{
    /// <summary>Custom features bought for this item (Ch.6, p.93). Usually empty.</summary>
    public IReadOnlyList<SelectedGearFeature> Features { get; init; } = [];

    /// <summary>Pros applied to this item. They cost the same on gear as on a Power.</summary>
    public IReadOnlyList<SelectedProCon> Pros { get; init; } = [];

    /// <summary>Cons applied to this item. They never pay out — gear floors at 0 HP.</summary>
    public IReadOnlyList<SelectedProCon> Cons { get; init; } = [];

    /// <summary>
    /// Set when this entry stands for a matched pair customised under the Two-Fisted
    /// Power, which the rulebook lets you buy "for the price of one". Recording the pair
    /// as a single item already charges once; the flag exists so the sheet says why, and
    /// so the validator can check the Power is actually there.
    /// </summary>
    public bool PairedUnderTwoFisted { get; init; }

    /// <summary>True if this item costs Hero Points at all.</summary>
    public bool IsCustomised => Features.Count > 0 || Pros.Count > 0 || Cons.Count > 0;
}

/// <summary>
/// Mutable state object for a character being built in the wizard.
/// All calculators and validators receive this and read from it.
/// </summary>
public class CharacterSheet
{
    /// <summary>
    /// Whether this character is a Villain rather than a Hero. <b>Presentation only: no rules
    /// code may read this, and there is a test that none does.</b>
    ///
    /// <para><b>Ch.9 builds Villains by exactly the Hero rules and prints no separate stat-block
    /// format</b>, so this changes no cost, no rank, no derived figure and no verdict. It is here
    /// rather than on a front end because a character that is a Villain should still be one after
    /// it has been exported and read back, and a palette held outside the character is a palette
    /// the file does not carry.</para>
    ///
    /// <para><b>This field replaces a rule that used to forbid it, and the reason it is now
    /// allowed is worth keeping.</b> The old refusal was correct while "Villain" meant two things
    /// at once — a palette <i>and</i> no Hero Point budget — because the second is mechanical and
    /// a mechanical flag here would be the browser deciding a rule. The budget half has moved to
    /// <see cref="UnlimitedBudget"/>, so what is left really is only a colour. Put a mechanic back
    /// on this field and the old objection applies again in full.</para>
    /// </summary>
    public bool IsVillain { get; set; }

    /// <summary>
    /// Whether this character is being built without a Hero Point limit — the sandbox.
    /// <b>Also presentation only, and held to the same test.</b>
    ///
    /// <para><b>It is independent of <see cref="IsVillain"/> on purpose.</b> A Hero can be built
    /// in the sandbox and a Villain can be held to a budget; one control deciding the other is
    /// exactly the conflation this pair was split to end.</para>
    ///
    /// <para><b>The validator is still never told, and still reports the budget.</b> A host
    /// decides what to do with the finding: the browser shows a running total and no cap, while
    /// <c>build --from</c> reports every finding the engine returns, because a report that
    /// quietly dropped one on the strength of a flag in its own input would be worth less than no
    /// report. The engine answers; hosts present.</para>
    /// </summary>
    public bool UnlimitedBudget { get; set; }

    /// <summary>Id of the selected tier, or null if not yet chosen.</summary>
    public string? SelectedTierId { get; set; }

    /// <summary>
    /// Id of the optional package applied (Civilian/Hero/Superhero), or null.
    /// The package sets a floor on ability and talent ranks — it does not
    /// prevent buying higher ranks on top.
    /// </summary>
    public string? SelectedPackageId { get; set; }

    /// <summary>Purchased ability ranks, keyed by ability id.</summary>
    public Dictionary<string, int> AbilityRanks { get; } = new();

    /// <summary>
    /// Pros and Cons applied to an Ability rather than a Power, keyed by ability id.
    /// The rulebook allows this — the Brute Option is Overkill on Might, and the
    /// published Stronghold buys four Abilities through his armour with the Item Con.
    /// </summary>
    public Dictionary<string, List<SelectedProCon>> AbilityModifiers { get; } = new();

    /// <summary>Purchased talent ranks, keyed by talent id.</summary>
    public Dictionary<string, int> TalentRanks { get; } = new();

    /// <summary>
    /// Sources for Abilities, keyed by ability id. Ch.2 p.16: every Ability, Talent and
    /// Power has a Source, and Abilities are usually Innate "at least when dealing with
    /// ordinary people. When dealing with supers and characters who aren't human, however,
    /// anything goes." An entry here is therefore a Trait whose Source is <em>not</em> the
    /// default — which is exactly what a published sheet prints, as an
    /// <c>Abilities (…)</c> line inside a Power group.
    ///
    /// <para><b>This cannot be derived from rank.</b> Ch.2 p.64 says "Sources for your
    /// Powers and Abilities with a rank of 7d or greater", which reads like a threshold and
    /// is not one — it is an instruction about which Traits to roll Sources for during
    /// random generation. The printed sheets disagree with it in both directions: Alabama
    /// Slammer marks 6d Perception and Toughness, and Citizen Soldier leaves 9d Willpower
    /// unmarked. Storing the answer is the only way to reproduce them.</para>
    /// </summary>
    public Dictionary<string, string> AbilitySources { get; } = new();

    /// <summary>
    /// Sources for Talents, keyed by talent id. Absent means the Trained default. Kept apart
    /// from <see cref="AbilitySources"/> because the two defaults differ, so a single
    /// dictionary could not say what a missing entry meant.
    /// </summary>
    public Dictionary<string, string> TalentSources { get; } = new();

    /// <summary>Powers on this character sheet.</summary>
    public List<SelectedPower> SelectedPowers { get; } = new();

    /// <summary>Perks purchased for this character.</summary>
    public List<SelectedPerk> Perks { get; } = new();

    /// <summary>Flaws selected for this character (min 1, max 3 at creation).</summary>
    public List<SelectedFlaw> Flaws { get; } = new();

    // ── Convenience helpers ───────────────────────────────────────────────

    public int GetAbilityRank(string abilityId) =>
        AbilityRanks.GetValueOrDefault(abilityId, 0);

    public int GetTalentRank(string talentId) =>
        TalentRanks.GetValueOrDefault(talentId, 0);

    public SelectedPower? GetPower(string powerId) =>
        SelectedPowers.FirstOrDefault(p => p.PowerId == powerId);

    public bool HasPower(string powerId) =>
        SelectedPowers.Any(p => p.PowerId == powerId);

    // ── Narrative & flavour (CLI-facing, not used by engine logic) ────────

    public string Name { get; set; } = "";
    public string Appearance { get; set; } = "";
    public string Motivation { get; set; } = "";
    public string Quote { get; set; } = "";
    public List<string> Connections { get; } = [];

    /// <summary>
    /// Gear carried. Mundane gear is free, so most entries cost nothing and this stays a
    /// narrative list — but a customised item does spend Hero Points, which is why it is
    /// not simply a list of strings. See <see cref="CostCalculator.TotalGearCost"/>.
    /// </summary>
    public List<SelectedGear> Gear { get; } = [];
}
